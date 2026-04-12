using JiraFit.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JiraFit.API.BackgroundServices;

public class AlarmDispatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlarmDispatcherService> _logger;

    public AlarmDispatcherService(IServiceProvider serviceProvider, ILogger<AlarmDispatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlarmDispatcherService started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Horário de Brasília fixo (UTC-3)
                var brazilTime = DateTime.UtcNow.AddHours(-3);
                var currentHour = brazilTime.Hour;
                var currentMinute = brazilTime.Minute;

                using var scope = _serviceProvider.CreateScope();
                var alarmRepo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
                var messagingService = scope.ServiceProvider.GetRequiredService<IMessagingService>();

                // Busca alarmes que batem com a hora e minuto, e que não dispararam hoje
                var pendingAlarms = await alarmRepo.GetAlarmsToTriggerAsync(currentHour, currentMinute, stoppingToken);

                foreach (var alarm in pendingAlarms)
                {
                    _logger.LogInformation($"Triggering alarm '{alarm.Name}' for user {alarm.UserId}");

                    var msg = $"🚨 *Lembrete JiraFit!* 🚨\n\nEstá na hora do seu: *{alarm.Name}*!\n\nNão esqueça de registrar a refeição assim que comer para manter o diário em dia! 💪";

                    if (alarm.User != null && !string.IsNullOrEmpty(alarm.User.PhoneNumber))
                    {
                        await messagingService.SendMessageAsync(alarm.User.PhoneNumber, msg, stoppingToken);
                        // Marca como disparado hoje
                        alarm.MarkAsTriggered(DateTime.UtcNow);
                    }
                }

                if (pendingAlarms.Any())
                {
                    await alarmRepo.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alarms in dispatcher loop.");
            }
        }
    }
}
