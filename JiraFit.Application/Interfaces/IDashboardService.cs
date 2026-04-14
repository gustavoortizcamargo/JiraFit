using JiraFit.Application.DTOs;
using JiraFit.Domain.Entities;

namespace JiraFit.Application.Interfaces;

public interface IDashboardService
{
    // Auth
    Task<DashboardUser?> AuthenticateAsync(string email, string password, CancellationToken ct = default);
    Task<DashboardUser> RegisterAsync(string email, string password, string phoneNumber, CancellationToken ct = default);
    Task<string> SendVerificationCodeAsync(Guid dashboardUserId, CancellationToken ct = default);
    Task<DashboardUser?> VerifyCodeAsync(Guid dashboardUserId, string code, CancellationToken ct = default);
    Task<string?> ResendVerificationCodeAsync(Guid dashboardUserId, CancellationToken ct = default);

    // Users
    Task<(int Total, List<UserSummaryDto> Data)> GetUsersAsync(int page, int pageSize, bool? isPro, CancellationToken ct = default);
    Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserSummaryDto?> GetUserByPhoneAsync(string phone, CancellationToken ct = default);
    Task<bool> UpgradeToProAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<DashboardStatsDto> GetUserStatsAsync(CancellationToken ct = default);

    // Meals
    Task<(int Total, List<MealSummaryDto> Data)> GetMealsAsync(Guid? userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<MealDetailDto?> GetMealByIdAsync(Guid id, CancellationToken ct = default);
    Task<DailySummaryDto?> GetDailySummaryAsync(Guid userId, DateTime? date, CancellationToken ct = default);
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, CancellationToken ct = default);
    Task<bool> DeleteMealAsync(Guid id, CancellationToken ct = default);
    Task<MealStatsDto> GetMealStatsAsync(CancellationToken ct = default);

    // Alarms
    Task<(int Total, List<AlarmSummaryDto> Data)> GetAlarmsAsync(Guid? userId, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<AlarmSummaryDto?> GetAlarmByIdAsync(Guid id, CancellationToken ct = default);
    Task<(int Total, List<AlarmSummaryDto> Data)> GetAlarmsByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ToggleAlarmAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAlarmAsync(Guid id, CancellationToken ct = default);
}
