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
                        var msg = "🤖 *Guia Rápido do JiraFit*\n" +
                                  "Seu Personal Nutricional no WhatsApp! Veja como extrair o máximo:\n\n" +
                                  "📋 *Comandos Específicos:*\n" +
                                  "• `resumo` ➜ Traz a lista e somatória calórica do seu dia.\n" +
                                  "• `grafico` ➜ Envia a imagem do seu gráfico calórico dos últimos 7 dias.\n" +
                                  "• `alarmes` ➜ Lista todos os alarmes ativos.\n" +
                                  "• `apagar` ➜ Remove a última refeição gravada por engano hoje.\n\n" +
                                  "🎙️ *Mágica da IA (Ações Livres):*\n" +
                                  "• *Escreva ou grave Áudio*: _\"Comi 200g de frango com batata doce\"_.\n" +
                                  "• *Mande uma Foto*: O bot analisará imagem do prato para encontrar as calorias.\n" +
                                  "• *Crie Avisos*: _\"Me lembre de jantar sempre às 20h!\"_.\n" +
                                  "• *Atualize seu Perfil*: _\"Meu peso atual é 90kg e tenho 180cm\"_.\n\n" +
                                  "Dúvidas extras? Apenas converse com a IA! 💪🍎";
                        await messagingService.SendMessageAsync(payload.UserPhoneNumber, msg, stoppingToken);
                        continue;
                    }
                    if (text == "grafico")
                    {
                        var endDate = DateTime.UtcNow.AddHours(-3); // Horário de Brasília
                        var meals = await mealRepository.GetWeeklyMealsAsync(currentUser.Id, DateTime.UtcNow, stoppingToken);

                        if (!meals.Any())
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "Você ainda não registrou nenhuma refeição nos últimos dias para gerar um gráfico.", stoppingToken);
                            continue;
                        }

                        // Agrupar por data local (Brasília)
                        var grouped = meals
                            .GroupBy(m => m.Timestamp.AddHours(-3).Date)
                            .OrderBy(g => g.Key)
                            .ToList();

                        var labels = new List<string>();
                        var data = new List<double>();
                        
                        // Preenche os últimos 7 dias 
                        for (int i = 6; i >= 0; i--)
                        {
                            var targetDate = endDate.Date.AddDays(-i);
                            var dayStr = targetDate.ToString("dd/MM");
                            labels.Add(dayStr);
                            
                            var sumCals = grouped.FirstOrDefault(g => g.Key == targetDate)?.Sum(m => m.Calories) ?? 0;
                            data.Add(sumCals);
                        }

                        var labelsStr = string.Join(",", labels.Select(l => $"'{l}'")); // '12/04','13/04'...
                        var dataStr = string.Join(",", data); // 1500,2000...
                        var userTdee = currentUser.Tdee > 0 ? currentUser.Tdee : 2000;

                        var quickChartJson = $@"{{
                            type: 'bar',
                            data: {{
                                labels: [{labelsStr}],
                                datasets: [
                                    {{
                                        label: 'Calorias Consumidas',
                                        data: [{dataStr}],
                                        backgroundColor: 'rgba(54, 162, 235, 0.6)',
                                        borderColor: 'rgb(54, 162, 235)',
                                        borderWidth: 1
                                    }},
                                    {{
                                        type: 'line',
                                        label: 'Meta Diária',
                                        data: [{string.Join(",", data.Select(_ => userTdee))}],
                                        borderColor: 'rgba(255, 99, 132, 1)',
                                        borderWidth: 2,
                                        fill: false
                                    }}
                                ]
                            }},
                            options: {{ title: {{ display: true, text: 'Evolução Calorias Semanais' }} }}
                        }}";

                        var chartUrl = $"https://quickchart.io/chart?w=600&h=400&c={Uri.EscapeDataString(quickChartJson)}";

                        await messagingService.SendMessageWithMediaAsync(payload.UserPhoneNumber, "📊 Seu levantamento calórico dos últimos 7 dias está no gráfico acima! Parabéns pelo foco!", chartUrl, stoppingToken);
                        continue;
                    }
                    if (text == "alarmes")
                    {
                        var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
                        var activeAlarms = await alarmRepo.GetActiveAlarmsByUserAsync(currentUser.Id, stoppingToken);
                        if (!activeAlarms.Any())
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "Você não tem nenhum alarme ativo no momento.", stoppingToken);
                        }
                        else
                        {
                            var msg = "⏰ *Seus Lembretes Ativos:*\n\n";
                            foreach (var a in activeAlarms)
                            {
                                msg += $"• ID `{a.Id.ToString().Substring(0, 4)}` - {a.Name} às {a.Hour:D2}:{a.Minute:D2}\n";
                            }
                            msg += "\n*Dica*: Para apagar, envie: `apagar alarme [ID]` (Ex: apagar alarme a1b2)";
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, msg, stoppingToken);
                        }
                        continue;
                    }
                    if (text.StartsWith("apagar alarme "))
                    {
                        var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
                        var idPattern = text.Replace("apagar alarme ", "").Trim();
                        var activeAlarms = await alarmRepo.GetActiveAlarmsByUserAsync(currentUser.Id, stoppingToken);
                        
                        var toDelete = activeAlarms.FirstOrDefault(a => a.Id.ToString().StartsWith(idPattern));
                        if (toDelete != null)
                        {
                            await alarmRepo.DeleteAsync(toDelete.Id, stoppingToken);
                            await alarmRepo.SaveChangesAsync(stoppingToken);
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, $"✅ Alarme '{toDelete.Name}' deletado com sucesso!", stoppingToken);
                        }
                        else
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "Alarme não encontrado. Verifique o ID digitando `alarmes`.", stoppingToken);
                        }
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

                    // 3. Modo Sugestão Tática
                    bool isSuggestionMode = false;
                    if (text == "sugestao" || text == "sugestão")
                    {
                        if (currentUser.Tdee <= 0)
                        {
                            await messagingService.SendMessageAsync(payload.UserPhoneNumber, "⚠️ Para que eu não prejudique sua saúde, me fale seu *Peso atual* e *Altura* em uma mensagem para eu calcular a sua Meta Diária primeiro!", stoppingToken);
                            continue;
                        }
                        payload.TextContent = $"Por favor, me sugira MÁGICAMENTE uma receita deliciosa que preencha ESTRITAMENTE as calorias que ainda me faltam hoje. Liste-a.";
                        isSuggestionMode = true;
                    }

                    // 4. Injeção de Contexto Oculto Diário
                    var todayMeals = await mealRepository.GetDailyMealsAsync(currentUser.Id, DateTime.UtcNow.AddHours(-3), stoppingToken);
                    if (currentUser.Tdee > 0)
                    {
                        var calsToday = todayMeals.Sum(m => m.Calories);
                        var remaining = currentUser.Tdee - calsToday;
                        
                        var suggestionBoost = isSuggestionMode ? " CRIE A RECEITA PENSADA EXATAMENTE PARA PREENCHER ESTE ESPAÇO DE CALORIAS!" : " O feedback deve levar isso em consideração na sua empolgação conversacional.";
                        payload.ContextMetadata = $"O usuário já ingeriu {calsToday:F0} Kcal hoje. Restam apenas {remaining:F0} Kcal para atingir a meta de {currentUser.Tdee:F0}.{suggestionBoost}";
                    }

                    // 5. Multimodal AI Processing
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

                        // Alarme Rastreamento
                        if (!string.IsNullOrEmpty(analysis.AlarmName) && analysis.AlarmHour.HasValue && analysis.AlarmMinute.HasValue)
                        {
                            var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
                            var alarm = new MealAlarm(currentUser.Id, analysis.AlarmName, analysis.AlarmHour.Value, analysis.AlarmMinute.Value);
                            await alarmRepo.AddAsync(alarm, stoppingToken);
                            await alarmRepo.SaveChangesAsync(stoppingToken);
                            
                            responseMsg = string.IsNullOrWhiteSpace(analysis.Feedback) 
                                ? $"✅ Lembrete de '{analysis.AlarmName}' criado para as {analysis.AlarmHour:D2}:{analysis.AlarmMinute:D2}h!" 
                                : analysis.Feedback;
                        }
                        // Refeição rastreada
                        else if (analysis.Calories > 0 && !isSuggestionMode)
                        {
                            var meal = new Meal(currentUser.Id, payload.MediaUrl, payload.TextContent, analysis.Calories, analysis.Proteins, analysis.Carbs, analysis.Fats, analysis.Feedback);
                            await mealRepository.AddAsync(meal, stoppingToken);
                            await mealRepository.SaveChangesAsync(stoppingToken);

                            // Streak logic (Brasília timezone constraint)
                            currentUser.RegisterActivity(DateTime.UtcNow.AddHours(-3));
                            await userRepository.SaveChangesAsync(stoppingToken);

                            var displayFeedback = string.IsNullOrWhiteSpace(analysis.Feedback)
                                ? "Refeição gravada no seu diário com sucesso! Continue mantendo o foco!"
                                : analysis.Feedback;

                            var streakMsg = currentUser.CurrentStreak > 1 
                                ? $"🔥 *Ofensiva Diária*: {currentUser.CurrentStreak} dias sem errar o foco!" 
                                : $"🔥 *Ofensiva Iniciada*: 1° dia gravado com sucesso!";

                            responseMsg = $"🥘 *Refeição Registrada:*\n\n" +
                                          $"🔥 Calorias: {analysis.Calories} kcal\n" +
                                          $"🥩 Proteínas: {analysis.Proteins}g\n" +
                                          $"🥖 Carboidratos: {analysis.Carbs}g\n" +
                                          $"🥑 Gorduras: {analysis.Fats}g\n\n" +
                                          $"💡 {displayFeedback}\n\n" +
                                          streakMsg;
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
