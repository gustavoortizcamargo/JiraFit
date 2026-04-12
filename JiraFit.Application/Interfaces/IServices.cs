using JiraFit.Application.DTOs;
using JiraFit.Domain.Common;

namespace JiraFit.Application.Interfaces;

public interface IAIService
{
    Task<Result<NutritionalAnalysisDto>> AnalyzeMealAsync(MealInputDto input, JiraFit.Domain.Entities.User currentUser, CancellationToken cancellationToken = default);
}

public interface IMessagingService
{
    Task SendMessageAsync(string toPhoneNumber, string message, string? mediaUrl = null, CancellationToken cancellationToken = default);
}

public interface IWebhookProcessorService
{
    // The Webhook from Twilio is enqueued here to be processed in background
    ValueTask EnqueueWebhookPayloadAsync(MealInputDto payload, CancellationToken cancellationToken = default);
}
