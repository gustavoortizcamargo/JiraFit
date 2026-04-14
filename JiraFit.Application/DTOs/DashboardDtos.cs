namespace JiraFit.Application.DTOs;

// ─── User DTOs ────────────────────────────
public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Name { get; set; }
    public double Weight { get; set; }
    public double Height { get; set; }
    public double Tdee { get; set; }
    public bool IsPro { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserDetailDto : UserSummaryDto
{
    public int Age { get; set; }
    public string? Gender { get; set; }
    public string? Objective { get; set; }
    public double Bmr { get; set; }
    public int MessagesSentToday { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime? LastMessageTrackedDate { get; set; }
    public int MealCount { get; set; }
    public int AlarmCount { get; set; }
}

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int ProUsers { get; set; }
    public int FreeUsers { get; set; }
    public int ActiveToday { get; set; }
    public double ConversionRate { get; set; }
}

// ─── Meal DTOs ────────────────────────────
public class MealSummaryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public double Calories { get; set; }
    public double Proteins { get; set; }
    public double Carbs { get; set; }
    public double Fats { get; set; }
    public string? RawText { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MealDetailDto : MealSummaryDto
{
    public string? UserPhone { get; set; }
    public string? AiFeedback { get; set; }
    public string? ImageUrl { get; set; }
}

public class DailySummaryDto
{
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string Date { get; set; } = string.Empty;
    public double TotalCalories { get; set; }
    public double TotalProteins { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFats { get; set; }
    public double CalorieGoal { get; set; }
    public double RemainingCalories { get; set; }
    public int MealCount { get; set; }
    public List<MealSummaryDto> Meals { get; set; } = new();
}

public class WeeklySummaryDto
{
    public Guid UserId { get; set; }
    public List<DayBreakdownDto> Days { get; set; } = new();
}

public class DayBreakdownDto
{
    public string Date { get; set; } = string.Empty;
    public double TotalCalories { get; set; }
    public double TotalProteins { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFats { get; set; }
    public int MealCount { get; set; }
}

public class MealStatsDto
{
    public int TotalMeals { get; set; }
    public int MealsToday { get; set; }
    public double AvgCaloriesPerMeal { get; set; }
}

// ─── Alarm DTOs ────────────────────────────
public class AlarmSummaryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserPhone { get; set; }
    public string? Name { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
}

// ─── Auth DTOs ────────────────────────────
public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? LinkedUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class RegisterPendingResponseDto
{
    public Guid DashboardUserId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class VerifyCodeRequestDto
{
    public Guid DashboardUserId { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class ResendCodeRequestDto
{
    public Guid DashboardUserId { get; set; }
}
