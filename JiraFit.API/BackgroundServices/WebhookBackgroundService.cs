using JiraFit.Application.Interfaces;
using JiraFit.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();
                    var messagingService = scope.ServiceProvider.GetRequiredService<IMessagingService>();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                    var mealRepository = scope.ServiceProvider.GetRequiredService<IMealRepository>();

                    // 1. Fetch or Create User Profile
                    var currentUser = await userRepository.GetByPhoneNumberAsync(payload.UserPhoneNumber, stoppingToken);
                    if (currentUser == null)
                    {
                        currentUser = new User(payload.UserPhoneNumber);
                        await userRepository.AddAsync(currentUser, stoppingToken);
                        await userRepository.SaveChangesAsync(stoppingToken); // Commit to get an ID
                    }

                    // 2. Exact Command Interceptor
                    string text = payload.TextContent?.Trim().ToLowerInvariant() ?? "";
                    
                    if (text == "ajuda")
                    {
                        var msg = "🤖 *Comandos do JiraFit*:\n\n" +
                                  "• `resumo` ➜ Veja todas as calorias e refeições consumidas hoje.\n" +
                                  "• `apagar` ➜ Remove a última refeição que você gravou.\n" +
                                  "• `ajuda` ➜ Mostra esta mensagem novamente.\n\n" +
                                  "📸 *Dica*: Envie uma foto do seu prato, ou um áudio/texto dizendo o que comeu!";
                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, msg, stoppingToken);
                        continue;
                    }
                    if (text == "apagar")
                    {
                        var deleted = await mealRepository.DeleteLatestMealAsync(currentUser.Id, stoppingToken);
                        if (deleted)
                        {
                            await mealRepository.SaveChangesAsync(stoppingToken);
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "✅ Última refeição deletada com sucesso!", stoppingToken);
                        }
                        else
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "Não encontrei nenhuma refeição sua para deletar hoje.", stoppingToken);
                        }
                        continue;
                    }
                    if (text == "resumo")
                    {
                        var meals = await mealRepository.GetDailyMealsAsync(currentUser.Id, DateTime.UtcNow, stoppingToken);
                        if (!meals.Any())
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "Você ainda não registrou nenhuma refeição hoje.", stoppingToken);
                        }
                        else
                        {
                            var totalCals = meals.Sum(m => m.Calories);
                            var totalProt = meals.Sum(m => m.Proteins);
                            var totalCarbs = meals.Sum(m => m.Carbs);
                            var totalFats = meals.Sum(m => m.Fats);
                            
                            var targetTdee = currentUser.Tdee > 0 ? $"{currentUser.Tdee:F0}" : "?";
                            
                            var msg = $"📊 *Resumo do seu Dia:*\n\n" +
                                      $"Calorias: {totalCals:F0} / {targetTdee} kcal\n\n" +
                                      $"🥩 Proteínas: {totalProt:F0}g\n" +
                                      $"🥖 Carboidratos: {totalCarbs:F0}g\n" +
                                      $"🥑 Gorduras: {totalFats:F0}g\n\n" +
                                      $"*Refeições gravadas hoje ({meals.Count}):*\n";
                                      
                            foreach(var m in meals)
                            {
                                msg += $"• {m.Calories:F0} kcal às {m.Timestamp:HH:mm}h\n";
                            }
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, msg, stoppingToken);
                        }
                        continue;
                    }

                    // 3. Multimodal AI Processing
                    var result = await aiService.AnalyzeMealAsync(payload, currentUser, stoppingToken);

                    if (result.IsSuccess)
                    {
                        var analysis = result.Value;

                        // Onboarding updates:
                        if (!string.IsNullOrEmpty(analysis.ExtractedName) || analysis.ExtractedWeight > 0 || analysis.ExtractedHeight > 0)
                        {
                            currentUser.UpdateProfile(analysis.ExtractedName, analysis.ExtractedWeight, analysis.ExtractedHeight);
                            await userRepository.SaveChangesAsync(stoppingToken);
                        }

                        string responseMsg;

                        // Refeição rastreada
                        if (analysis.Calories > 0)
                        {
                            var meal = new Meal(currentUser.Id, payload.MediaUrl, payload.TextContent, analysis.Calories, analysis.Proteins, analysis.Carbs, analysis.Fats, analysis.Feedback);
                            await mealRepository.AddAsync(meal, stoppingToken);
                            await mealRepository.SaveChangesAsync(stoppingToken);

                            var displayFeedback = string.IsNullOrWhiteSpace(analysis.Feedback)
                                ? "Refeição gravada no seu diário com sucesso! Continue mantendo o foco!"
                                : analysis.Feedback;

                            responseMsg = $"🥘 *Refeição Registrada:*\n" +
                                          $"🔥 Calorias: {analysis.Calories} kcal\n" +
                                          $"🥩 Proteínas: {analysis.Proteins}g\n" +
                                          $"🥖 Carboidratos: {analysis.Carbs}g\n" +
                                          $"🥑 Gorduras: {analysis.Fats}g\n\n" +
                                          $"💡 {displayFeedback}";
                        }
                        else
                        {
                            // Apenas uma resposta conversacional
                            responseMsg = analysis.Feedback;
                        }

                        if (string.IsNullOrWhiteSpace(responseMsg))
                        {
                            responseMsg = "✅ Dados processados e salvos com sucesso! O que vamos comer agora?";
                        }

                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, responseMsg, stoppingToken);
                    }
                    else
                    {
                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, 
                            "Desculpe, não consegui compreender essa mensagem ou áudio. Tente novamente!", stoppingToken);
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
