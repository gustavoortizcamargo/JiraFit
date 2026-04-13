using JiraFit.Application.Interfaces;
using JiraFit.Domain.Entities;
using JiraFit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.Infrastructure.Repositories;

public class AlarmRepository : IAlarmRepository
{
    private readonly AppDbContext _context;

    public AlarmRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MealAlarm alarm, CancellationToken cancellationToken = default)
    {
        await _context.MealAlarms.AddAsync(alarm, cancellationToken);
    }



    public async Task<List<MealAlarm>> GetActiveAlarmsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.MealAlarms
            .Where(a => a.UserId == userId && a.IsActive)
            .OrderBy(a => a.Hour).ThenBy(a => a.Minute)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MealAlarm>> GetAlarmsToTriggerAsync(int currentHour, int currentMinute, CancellationToken cancellationToken = default)
    {
        // Must consider alarms that match the time AND weren't triggered today.
        var today = DateTime.UtcNow.AddHours(-3).Date;

        return await _context.MealAlarms
            .Include(a => a.User) // We need the user to send the message
            .Where(a => a.IsActive && 
                        a.Hour == currentHour && 
                        a.Minute == currentMinute &&
                        (a.LastTriggeredAt == null || a.LastTriggeredAt.Value.Date != today))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid alarmId, CancellationToken cancellationToken = default)
    {
        var alarm = await _context.MealAlarms.FindAsync(new object[] { alarmId }, cancellationToken);
        if (alarm != null)
        {
            _context.MealAlarms.Remove(alarm);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
