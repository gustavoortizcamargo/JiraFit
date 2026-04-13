using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Domain.Common;
using JiraFit.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JiraFit.Infrastructure.Services;

public class OpenAIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OpenAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<NutritionalAnalysisDto>> AnalyzeMealAsync(MealInputDto input, User currentUser, CancellationToken cancellationToken)
    {
        try
        {
            var openAiClient = _httpClientFactory.CreateClient("OpenAI");
            var apiKey = _configuration["OpenAI:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return Result.Failure<NutritionalAnalysisDto>("OpenAI API Key is missing from configuration.");
            }
            
            openAiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Base Context & Prompt
            var contextMetadataStr = string.IsNullOrEmpty(input.ContextMetadata) ? "" : $" [Resumo Do Dia Atual]: {input.ContextMetadata}";
            var userContext = $"[Contexto do Usuário] Nome: {(string.IsNullOrEmpty(currentUser.Name) ? "Desconhecido" : currentUser.Name)}. Peso: {(currentUser.Weight > 0 ? currentUser.Weight + "kg" : "Desconhecido")}. Altura: {(currentUser.Height > 0 ? currentUser.Height + "cm" : "Desconhecido")}.{contextMetadataStr} ";

            var behaviorLogic = input.IsSuggestionRequest 
                ? "IMPORTANTE: O usuário está pedindo uma SUGESTÃO de jantar/refeição. Como nutricionista gatinho, elabore uma lista de ingredientes e preparo que se encaixe magicamente nas calorias diárias RESTANTES dele e envie toda essa receita detalhada dentro do campo 'Feedback' (string). Preencha Calories/Proteins/Carbs/Fats como 0." 
                : "SE a mensagem contiver comida, extraia rigorosamente: 'Calories' (número), 'Proteins' (número), 'Carbs' (número), 'Fats' (número). OBRIGATÓRIO: Forneça sempre 'MealHour' (inteiro, 0-23 ou null se o usuário não citou o horário da comida) e 'MealMinute' (inteiro, 0-59 ou null). ALÉM DISSO, adicione na chave 'Feedback' (string) UMA ÚNICA FRASE motivacional, confirmando sempre que a refeição foi salva no diário. SE a mensagem pedir criação de ALARME, preencha: 'AlarmName', 'AlarmHour' e 'AlarmMinute'. Confirme no 'Feedback'. SE for apenas dados cadastrais, preencha: 'ExtractedName' (string), 'ExtractedWeight' (número), 'ExtractedHeight' (número).";

            var systemPrompt = "Você é o JiraFit, um assistente nutricional caloroso via WhatsApp. Você é representado pela persona de um gatinho! Tente usar emojis de gato (🐱, 😸, 🐾, 😹) sempre que possível para manter sua essência fofa e carinhosa. " +
                               userContext + behaviorLogic + 
                               " Retorne APENAS um JSON válido de acordo com os campos citados, SEM usar blocos textuais fora do JSON, SEM markdown (como ```json).";
            
            // Tratamento especial se o conteúdo for Áudio
            var finalInputText = input.TextContent;

            if (!string.IsNullOrEmpty(input.MediaType) && input.MediaType.StartsWith("audio"))
            {
                var audioTranscription = await TranscribeAudioAsync(input.MediaUrl, apiKey, cancellationToken);
                if (audioTranscription != null)
                {
                    _logger.LogInformation($"OpenAI Whisper Transcribed: {audioTranscription}");
                    finalInputText = string.IsNullOrEmpty(finalInputText) ? audioTranscription : $"{finalInputText}\nTransmissão de Áudio: {audioTranscription}";
                }
            }

            // Construir bloco de Content do usuário Multimodal (Texto / Imagem)
            var userContentParts = new List<object>();

            if (!string.IsNullOrEmpty(finalInputText))
            {
                userContentParts.Add(new { type = "text", text = finalInputText });
            }
            else if (!string.IsNullOrEmpty(input.MediaUrl))
            {
                userContentParts.Add(new { type = "text", text = "Analise o alimento apresentado nesta imagem e me traga as calorias estimadas e macros em formato JSON." });
            }

            // Imagem
            if (!string.IsNullOrEmpty(input.MediaUrl) && input.MediaType.StartsWith("image"))
            {
                var base64Media = await DownloadTwilioMediaAsBase64Async(input.MediaUrl, cancellationToken);
                if (base64Media != null)
                {
                    userContentParts.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:{input.MediaType};base64,{base64Media}"
                        }
                    });
                }
            }

            if (userContentParts.Count == 0)
            {
                _logger.LogWarning("No data could be mapped for OpenAI analysis.");
                return Result.Failure<NutritionalAnalysisDto>("Formato de mídia não suportado ou vazio.");
            }

            var requestPayload = new
            {
                model = "gpt-4o-mini",
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userContentParts }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await openAiClient.PostAsync("chat/completions", content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"OpenAI API Error: {responseJson}");
                return Result.Failure<NutritionalAnalysisDto>("Falha ao conectar com o modelo de inteligência.");
            }

            var jsonDoc = JsonDocument.Parse(responseJson);
            var textResult = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (textResult == null)
            {
                return Result.Failure<NutritionalAnalysisDto>("OpenAI retornou json vazio.");
            }

            var dto = JsonSerializer.Deserialize<NutritionalAnalysisDto>(textResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null) 
            {
                return Result.Failure<NutritionalAnalysisDto>("Falha ao ler Output da OpenAI DTO.");
            }

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in OpenAIService AnalyzeMealAsync.");
            return Result.Failure<NutritionalAnalysisDto>(ex.Message);
        }
    }

    private async Task<string?> TranscribeAudioAsync(string mediaUrl, string openAiKey, CancellationToken cancellationToken)
    {
        try
        {
            var twilioBytes = await DownloadTwilioMediaAsBytesAsync(mediaUrl, cancellationToken);
            if (twilioBytes == null) return null;

            var openAiClient = _httpClientFactory.CreateClient("OpenAI");
            openAiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);

            using var form = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(twilioBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/ogg");
            // Whisper API requires filename
            form.Add(audioContent, "file", "audio.ogg");
            form.Add(new StringContent("whisper-1"), "model");

            var response = await openAiClient.PostAsync("audio/transcriptions", form, cancellationToken);
            var resultStr = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"OpenAI Whisper Error: {resultStr}");
                return null;
            }

            var jsonDoc = JsonDocument.Parse(resultStr);
            if (jsonDoc.RootElement.TryGetProperty("text", out var textProp))
                return textProp.GetString();

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OpenAI Whisper transcription");
            return null;
        }
    }

    private async Task<string?> DownloadTwilioMediaAsBase64Async(string mediaUrl, CancellationToken cancellationToken)
    {
        var bytes = await DownloadTwilioMediaAsBytesAsync(mediaUrl, cancellationToken);
        return bytes != null ? Convert.ToBase64String(bytes) : null;
    }

    private async Task<byte[]?> DownloadTwilioMediaAsBytesAsync(string mediaUrl, CancellationToken cancellationToken)
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
                _logger.LogWarning($"Failed to download media from Twilio. Code: {response.StatusCode}");
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception downloading from Twilio.");
            return null;
        }
    }
}
