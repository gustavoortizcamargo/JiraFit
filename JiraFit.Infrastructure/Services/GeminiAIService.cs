using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JiraFit.Infrastructure.Services;

public class GeminiAIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAIService> _logger;

    public GeminiAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeminiAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<NutritionalAnalysisDto>> AnalyzeMealAsync(MealInputDto input, JiraFit.Domain.Entities.User currentUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return Result.Failure<NutritionalAnalysisDto>("Gemini API Key is not configured.");

            var parts = new List<object>();

            // 1. Text Parsing
            if (!string.IsNullOrEmpty(input.TextContent))
            {
                parts.Add(new { text = input.TextContent });
            }
            else if (!string.IsNullOrEmpty(input.MediaUrl))
            {
                // Fallback prompt para garantir que a IA tenha um contexto textual quando recebe apenas Mídia (Imagem sem legenda)
                parts.Add(new { text = "Analise o alimento apresentado nesta mídia." });
            }

            // 2. Media Fetching from Twilio (Image or Audio)
            if (!string.IsNullOrEmpty(input.MediaUrl))
            {
                var base64Media = await DownloadTwilioMediaAsBase64Async(input.MediaUrl, cancellationToken);
                if (base64Media != null)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = !string.IsNullOrEmpty(input.MediaType) ? input.MediaType : "image/jpeg",
                            data = base64Media
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to fetch media from Twilio. Uploading without it.");
                }
            }

            if (parts.Count == 0)
            {
                _logger.LogWarning("No text or media Parts constructed. Bailing out.");
                return Result.Failure<NutritionalAnalysisDto>("Provide text or an image.");
            }

            // 2. Build Text Prompt Payload
            var contextMetadataStr = string.IsNullOrEmpty(input.ContextMetadata) ? "" : $" [Resumo Do Dia Atual]: {input.ContextMetadata}";
            var userContext = $"[Contexto do Usuário] Nome: {(string.IsNullOrEmpty(currentUser.Name) ? "Desconhecido" : currentUser.Name)}. Peso: {(currentUser.Weight > 0 ? currentUser.Weight + "kg" : "Desconhecido")}. Altura: {(currentUser.Height > 0 ? currentUser.Height + "cm" : "Desconhecido")}.{contextMetadataStr} ";

            var behaviorLogic = input.IsSuggestionRequest 
                ? "IMPORTANTE: O usuário está pedindo uma SUGESTÃO de jantar/refeição. Como nutricionista gatinho, elabore uma receita (ingredientes e preparo) que cumpra EXATAMENTE o resto das calorias necessárias do dia e escreva essa resposta inteira DENTRO da chave 'Feedback' (string) do JSON. Deixe os outros macros (Calories/Proteins...) como 0." 
                : "SE a mensagem contiver comida (refeição), extraia os macros no formato JSON: 'Calories' (número), 'Proteins' (número), 'Carbs' (número), 'Fats' (número). ALÉM DISSO, preencha a chave 'Feedback' (string) com UMA ÚNICA FRASE curta, finalizando avisando que a refeição foi salva no diário. AGORA, SE a mensagem do usuário for solicitando a criação de um ALARME ou LEMBRETE (Ex: 'Me lembre de jantar as 20:00'), preencha ESTRITAMENTE as chaves do JSON: 'AlarmName' (string), 'AlarmHour' (número, 0-23), 'AlarmMinute' (número, 0-59). E mande uma confirmação entusiasmada no 'Feedback'.";

            var systemPrompt = "Você é o JiraFit, um assistente nutricional via WhatsApp extremamente enérgico e amigável. A sua persona é um gatinho! Use linguagem carinhosa e abuse de emojis de gato (😸, 🐾, 😹, 🐱, 😻) para dar vida a você. O usuário pode enviar áudios, imagens ou textos. " +
                               userContext + behaviorLogic +
                               " Sua resposta deve ser ESTRITAMENTE um JSON válido, sem utilizar blocos de código extra nem marcação markdown ```. Retorne aspas duplas, formato puro. SE a mensagem do usuário for dados básicos como NOME, PESO ou ALTURA, coloque nas chaves: 'ExtractedName' (string), 'ExtractedWeight' (número), 'ExtractedHeight' (número). " +
                               "Sempre retorne APENAS um objeto JSON válido.";

            // 3. Prepare Gemini Request Payload
            var requestPayload = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        parts = parts
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json"
                }
            };

            var geminiClient = _httpClientFactory.CreateClient("Gemini");
            var jsonPayload = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var url = $"v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
            
            var response = await geminiClient.PostAsync(url, content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Gemini API Error: {responseJson}");
                return Result.Failure<NutritionalAnalysisDto>("Failed to analyze meal from AI.");
            }

            var jsonDoc = JsonDocument.Parse(responseJson);
            var textResult = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (textResult == null)
            {
                _logger.LogError("Gemini returned a null text content for the JSON response.");
                return Result.Failure<NutritionalAnalysisDto>("Failed to parse response format.");
            }

            var dto = JsonSerializer.Deserialize<NutritionalAnalysisDto>(textResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null) 
            {
                _logger.LogError($"Failed to deserialize string to DTO: {textResult}");
                return Result.Failure<NutritionalAnalysisDto>("Failed to map AI response to DTO.");
            }

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AnalyzeMealAsync.");
            return Result.Failure<NutritionalAnalysisDto>(ex.Message);
        }
    }

    private async Task<string?> DownloadTwilioMediaAsBase64Async(string mediaUrl, CancellationToken cancellationToken)
    {
        try
        {
            var twilioClient = _httpClientFactory.CreateClient("Twilio");
            
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            
            twilioClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            var response = await twilioClient.GetAsync(mediaUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"Failed to download media from Twilio. Status: {response.StatusCode}, Details: {err}");
                return null;
            }

            var mediaBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return Convert.ToBase64String(mediaBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading media from Twilio.");
            return null;
        }
    }
}
