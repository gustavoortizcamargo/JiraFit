using JiraFit.Domain.Entities;
using JiraFit.Domain.Common;

namespace JiraFit.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMealRepository
{
    Task AddAsync(Meal meal, CancellationToken cancellationToken = default);
    Task<List<Meal>> GetDailyMealsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);
    Task<bool> DeleteLatestMealAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAlarmRepository
{
    Task AddAsync(MealAlarm alarm, CancellationToken cancellationToken = default);
    Task<List<MealAlarm>> GetActiveAlarmsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<MealAlarm>> GetAlarmsToTriggerAsync(int currentHour, int currentMinute, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid alarmId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
