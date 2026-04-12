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

            // 1. Text Content
            if (!string.IsNullOrEmpty(input.TextContent))
            {
                parts.Add(new { text = input.TextContent });
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
            }

            if (parts.Count == 0)
                return Result.Failure<NutritionalAnalysisDto>("Provide text or an image.");

            var userContext = $"[Contexto do Usuário atual] Nome salvo: {(string.IsNullOrEmpty(currentUser.Name) ? "Desconhecido" : currentUser.Name)}. Peso: {(currentUser.Weight > 0 ? currentUser.Weight + "kg" : "Desconhecido")}. Altura: {(currentUser.Height > 0 ? currentUser.Height + "cm" : "Desconhecido")}. ";

            var systemPrompt = "Você é o JiraFit, um assistente nutricional via WhatsApp. O usuário pode enviar áudios, imagens ou textos. " +
                               userContext +
                               "SE a mensagem contiver comida (refeição), extraia rigorosamente os macros no formato JSON: 'Calories' (número), 'Proteins' (número), 'Carbs' (número), 'Fats' (número). SE o usuário preencheu essas macros e não forneceu mais nada, retorne o JSON preenchido. AGORA, SE a mensagem do usuário for ele dizendo o próprio NOME, PESO (kg) ou ALTURA (cm), você NÃO extrai macros, mas coloca os dados que percebeu nas propriedades do JSON: 'ExtractedName' (string), 'ExtractedWeight' (número), 'ExtractedHeight' (número). Se houver dados faltantes (ex: o usuário não tem nome ou peso ainda), fale gentilmente com ele através da chave 'Feedback' (string) pedindo essas informações. Retorne estritamente um objeto JSON sem blocos de código extra.";

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
                _logger.LogError("Gemini API Error: {Response}", responseJson);
                return Result.Failure<NutritionalAnalysisDto>("Failed to analyze meal with AI.");
            }

            var geminiResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var textResult = geminiResponse
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            if (string.IsNullOrEmpty(textResult))
                return Result.Failure<NutritionalAnalysisDto>("Empty response from AI.");

            var analysis = JsonSerializer.Deserialize<NutritionalAnalysisDto>(textResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (analysis == null)
                return Result.Failure<NutritionalAnalysisDto>("Failed to deserialize AI JSON.");

            return Result.Success(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Gemini AI Service");
            return Result.Failure<NutritionalAnalysisDto>(ex.Message);
        }
    }

    private async Task<string?> DownloadTwilioMediaAsBase64Async(string mediaUrl, CancellationToken cancellationToken)
    {
        try
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
            {
                _logger.LogWarning("Twilio credentials not found. Cannot download secure media from {Url}", mediaUrl);
                return null;
            }

            var twilioClient = _httpClientFactory.CreateClient("Twilio");
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            twilioClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            var response = await twilioClient.GetAsync(mediaUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download media from Twilio. Status: {Status}", response.StatusCode);
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
