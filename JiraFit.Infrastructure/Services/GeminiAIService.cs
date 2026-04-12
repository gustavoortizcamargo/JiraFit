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

    public async Task<Result<NutritionalAnalysisDto>> AnalyzeMealAsync(MealInputDto input, CancellationToken cancellationToken = default)
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

            // 2. Image Fetching from Twilio
            if (!string.IsNullOrEmpty(input.ImageUrl))
            {
                var base64Image = await DownloadTwilioImageAsBase64Async(input.ImageUrl, cancellationToken);
                if (base64Image != null)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = "image/jpeg",
                            data = base64Image
                        }
                    });
                }
            }

            if (parts.Count == 0)
                return Result.Failure<NutritionalAnalysisDto>("Provide text or an image.");

            // 3. Prepare Gemini Request Payload
            var requestPayload = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = "Você é um assistente pessoal de nutrição altamente inteligente chamado JiraFit. Avalie a refeição fornecida pelo usuário (seja por texto ou imagem). Retorne ESTRITAMENTE um objeto JSON válido, sem propriedades adicionais e sem formatação Markdown markdown, contendo estas chaves exatamente: 'Calories' (número inteiro ou decimal), 'Proteins' (g), 'Carbs' (g), 'Fats' (g), e 'Feedback' (string com um breve tom motivacional, profissional e rápido sobre a qualidade da refeição)." }
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

            var url = $"v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            
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

    private async Task<string?> DownloadTwilioImageAsBase64Async(string mediaUrl, CancellationToken cancellationToken)
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
