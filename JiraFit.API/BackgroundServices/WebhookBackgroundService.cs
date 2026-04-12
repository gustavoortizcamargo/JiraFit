using JiraFit.Application.Interfaces;

namespace JiraFit.API.BackgroundServices;

public class WebhookBackgroundService : BackgroundService
{
    private readonly WebhookChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookBackgroundService> _logger;

    public WebhookBackgroundService(
        WebhookChannel channel,
        IServiceProvider serviceProvider,
        ILogger<WebhookBackgroundService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var payload in _channel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation($"Processing webhook payload for user {payload.UserPhoneNumber}");

                    // Need to create a new scope for scoped services (DbContext, Repositories, etc)
                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();
                    var messagingService = scope.ServiceProvider.GetRequiredService<IMessagingService>();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                    // Basic Logic flow:
                    // 1. Analyse using AI
                    var result = await aiService.AnalyzeMealAsync(payload, stoppingToken);

                    if (result.IsSuccess)
                    {
                        var analysis = result.Value;

                        // 2. Reply back
                        string responseMsg = $"🔍 Análise Nutricional:\n" +
                                             $"Calorias: {analysis.Calories} kcal\n" +
                                             $"Proteínas: {analysis.Proteins}g\n" +
                                             $"Carboidratos: {analysis.Carbs}g\n" +
                                             $"Gorduras: {analysis.Fats}g\n\n" +
                                             $"Feedback da IA: {analysis.Feedback}";

                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, responseMsg, stoppingToken);
                        
                        // TODO: Save to MealRepository
                    }
                    else
                    {
                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, 
                            "Desculpe, não consegui analisar sua refeição no momento.", stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing individual webhook payload.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }
}
