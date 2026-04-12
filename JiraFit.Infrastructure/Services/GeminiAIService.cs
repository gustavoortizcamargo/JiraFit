using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Domain.Common;

namespace JiraFit.Infrastructure.Services;

public class GeminiAIService : IAIService
{
    // Need to inject IHttpClientFactory in the future to call Google Gemini API
    
    public async Task<Result<NutritionalAnalysisDto>> AnalyzeMealAsync(MealInputDto input, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual HTTP call to Gemini 1.5 Flash
        // using the ImageUrl or TextContent
        // For now, return a mock result
        
        var mockValidation = new NutritionalAnalysisDto
        {
            Calories = 450,
            Proteins = 30,
            Carbs = 45,
            Fats = 15,
            Feedback = "Ótima refeição estruturada! Parabéns!"
        };

        return await Task.FromResult(Result.Success(mockValidation));
    }
}
