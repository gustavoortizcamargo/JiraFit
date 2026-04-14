using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Domain.Entities;
using JiraFit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly ISmsService _smsService;

    public DashboardService(AppDbContext context, ISmsService smsService)
    {
        _context = context;
        _smsService = smsService;
    }

    // ─── AUTH ────────────────────────
    public async Task<DashboardUser?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _context.DashboardUsers
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        // Block login if phone not verified
        if (!user.IsVerified)
            return null;

        user.RecordLogin();
        await _context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<DashboardUser> RegisterAsync(string email, string password, string phoneNumber, CancellationToken ct = default)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var dashUser = new DashboardUser(email, hash, phoneNumber);

        // Auto-link if a WhatsApp user with this phone already exists
        var whatsappUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, ct);
        if (whatsappUser != null)
            dashUser.LinkToWhatsAppUser(whatsappUser.Id);

        await _context.DashboardUsers.AddAsync(dashUser, ct);
        await _context.SaveChangesAsync(ct);
        return dashUser;
    }

    public async Task<string> SendVerificationCodeAsync(Guid dashboardUserId, CancellationToken ct = default)
    {
        var user = await _context.DashboardUsers.FindAsync(new object[] { dashboardUserId }, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        var code = new Random().Next(100000, 999999).ToString();
        user.SetVerificationCode(code, expirationMinutes: 10);
        await _context.SaveChangesAsync(ct);

        await _smsService.SendSmsAsync(
            user.PhoneNumber,
            $"🔐 JiraFit: Seu código de verificação é {code}. Válido por 10 minutos.",
            ct);

        return code;
    }

    public async Task<DashboardUser?> VerifyCodeAsync(Guid dashboardUserId, string code, CancellationToken ct = default)
    {
        var user = await _context.DashboardUsers.FindAsync(new object[] { dashboardUserId }, ct);
        if (user == null) return null;

        var isValid = user.ValidateVerificationCode(code);
        if (!isValid) return null;

        // Auto-link WhatsApp user on successful verification
        var whatsappUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == user.PhoneNumber, ct);
        if (whatsappUser != null)
            user.LinkToWhatsAppUser(whatsappUser.Id);

        await _context.SaveChangesAsync(ct);
        return user;
    }

    public async Task<string?> ResendVerificationCodeAsync(Guid dashboardUserId, CancellationToken ct = default)
    {
        try
        {
            return await SendVerificationCodeAsync(dashboardUserId, ct);
        }
        catch
        {
            return null;
        }
    }

    // ─── USERS ────────────────────────────
    public async Task<(int Total, List<UserSummaryDto> Data)> GetUsersAsync(int page, int pageSize, bool? isPro, CancellationToken ct = default)
    {
        var query = _context.Users.AsQueryable();
        if (isPro.HasValue) query = query.Where(u => u.IsPro == isPro.Value);

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id, PhoneNumber = u.PhoneNumber, Name = u.Name,
                Weight = u.Weight, Height = u.Height, Tdee = u.Tdee,
                IsPro = u.IsPro, CurrentStreak = u.CurrentStreak, CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return (total, data);
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Meals).Include(u => u.Alarms)
            .Where(u => u.Id == id)
            .Select(u => new UserDetailDto
            {
                Id = u.Id, PhoneNumber = u.PhoneNumber, Name = u.Name,
                Weight = u.Weight, Height = u.Height, Age = u.Age,
                Gender = u.Gender.ToString(), Objective = u.Objective.ToString(),
                Bmr = u.Bmr, Tdee = u.Tdee, IsPro = u.IsPro,
                CurrentStreak = u.CurrentStreak, CreatedAt = u.CreatedAt,
                MessagesSentToday = u.MessagesSentToday,
                LastActivityDate = u.LastActivityDate,
                LastMessageTrackedDate = u.LastMessageTrackedDate,
                MealCount = u.Meals.Count, AlarmCount = u.Alarms.Count
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserSummaryDto?> GetUserByPhoneAsync(string phone, CancellationToken ct = default)
    {
        return await _context.Users
            .Where(u => u.PhoneNumber == phone)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id, PhoneNumber = u.PhoneNumber, Name = u.Name,
                Weight = u.Weight, Height = u.Height, Tdee = u.Tdee,
                IsPro = u.IsPro, CurrentStreak = u.CurrentStreak, CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UpgradeToProAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, ct);
        if (user == null) return false;
        user.UpgradeToPro();
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, ct);
        if (user == null) return false;
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DashboardStatsDto> GetUserStatsAsync(CancellationToken ct = default)
    {
        var total = await _context.Users.CountAsync(ct);
        var pro = await _context.Users.CountAsync(u => u.IsPro, ct);
        var today = DateTime.UtcNow.AddHours(-3).Date;
        var activeToday = await _context.Users.CountAsync(
            u => u.LastMessageTrackedDate != null && u.LastMessageTrackedDate.Value.Date == today, ct);

        return new DashboardStatsDto
        {
            TotalUsers = total, ProUsers = pro, FreeUsers = total - pro,
            ActiveToday = activeToday,
            ConversionRate = total > 0 ? Math.Round((double)pro / total * 100, 1) : 0
        };
    }

    // ─── MEALS ────────────────────────────
    public async Task<(int Total, List<MealSummaryDto> Data)> GetMealsAsync(
        Guid? userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Meals.Include(m => m.User).AsQueryable();
        if (userId.HasValue) query = query.Where(m => m.UserId == userId.Value);
        if (from.HasValue) query = query.Where(m => m.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(m => m.Timestamp <= to.Value);

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MealSummaryDto
            {
                Id = m.Id, UserId = m.UserId,
                UserName = m.User != null ? m.User.Name : null,
                Calories = m.Calories, Proteins = m.Proteins,
                Carbs = m.Carbs, Fats = m.Fats,
                RawText = m.RawText, Timestamp = m.Timestamp
            })
            .ToListAsync(ct);

        return (total, data);
    }

    public async Task<MealDetailDto?> GetMealByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Meals.Include(m => m.User)
            .Where(m => m.Id == id)
            .Select(m => new MealDetailDto
            {
                Id = m.Id, UserId = m.UserId,
                UserName = m.User != null ? m.User.Name : null,
                UserPhone = m.User != null ? m.User.PhoneNumber : null,
                Calories = m.Calories, Proteins = m.Proteins,
                Carbs = m.Carbs, Fats = m.Fats,
                AiFeedback = m.AiFeedback, RawText = m.RawText,
                ImageUrl = m.ImageUrl, Timestamp = m.Timestamp
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DailySummaryDto?> GetDailySummaryAsync(Guid userId, DateTime? date, CancellationToken ct = default)
    {
        var targetDate = (date ?? DateTime.UtcNow.AddHours(-3)).Date;
        var nextDay = targetDate.AddDays(1);

        var meals = await _context.Meals
            .Where(m => m.UserId == userId && m.Timestamp >= targetDate && m.Timestamp < nextDay)
            .OrderBy(m => m.Timestamp)
            .Select(m => new MealSummaryDto
            {
                Id = m.Id, UserId = m.UserId, Calories = m.Calories,
                Proteins = m.Proteins, Carbs = m.Carbs, Fats = m.Fats,
                RawText = m.RawText, Timestamp = m.Timestamp
            })
            .ToListAsync(ct);

        var user = await _context.Users.Where(u => u.Id == userId)
            .Select(u => new { u.Tdee, u.Name }).FirstOrDefaultAsync(ct);

        var totalCals = meals.Sum(m => m.Calories);
        return new DailySummaryDto
        {
            UserId = userId, UserName = user?.Name,
            Date = targetDate.ToString("yyyy-MM-dd"),
            TotalCalories = totalCals, TotalProteins = meals.Sum(m => m.Proteins),
            TotalCarbs = meals.Sum(m => m.Carbs), TotalFats = meals.Sum(m => m.Fats),
            CalorieGoal = user?.Tdee ?? 0, RemainingCalories = (user?.Tdee ?? 0) - totalCals,
            MealCount = meals.Count, Meals = meals
        };
    }

    public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var endDate = DateTime.UtcNow.AddHours(-3).Date.AddDays(1);
        var startDate = endDate.AddDays(-7);

        var meals = await _context.Meals
            .Where(m => m.UserId == userId && m.Timestamp >= startDate && m.Timestamp < endDate)
            .ToListAsync(ct);

        var days = meals.GroupBy(m => m.Timestamp.Date)
            .Select(g => new DayBreakdownDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                TotalCalories = g.Sum(m => m.Calories), TotalProteins = g.Sum(m => m.Proteins),
                TotalCarbs = g.Sum(m => m.Carbs), TotalFats = g.Sum(m => m.Fats),
                MealCount = g.Count()
            })
            .OrderBy(d => d.Date).ToList();

        return new WeeklySummaryDto { UserId = userId, Days = days };
    }

    public async Task<bool> DeleteMealAsync(Guid id, CancellationToken ct = default)
    {
        var meal = await _context.Meals.FindAsync(new object[] { id }, ct);
        if (meal == null) return false;
        _context.Meals.Remove(meal);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MealStatsDto> GetMealStatsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.AddHours(-3).Date;
        var todayEnd = today.AddDays(1);

        return new MealStatsDto
        {
            TotalMeals = await _context.Meals.CountAsync(ct),
            MealsToday = await _context.Meals.CountAsync(m => m.Timestamp >= today && m.Timestamp < todayEnd, ct),
            AvgCaloriesPerMeal = Math.Round(await _context.Meals.AverageAsync(m => (double?)m.Calories, ct) ?? 0, 1)
        };
    }

    // ─── ALARMS ────────────────────────────
    public async Task<(int Total, List<AlarmSummaryDto> Data)> GetAlarmsAsync(
        Guid? userId, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.MealAlarms.Include(a => a.User).AsQueryable();
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (isActive.HasValue) query = query.Where(a => a.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderBy(a => a.Hour).ThenBy(a => a.Minute)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AlarmSummaryDto
            {
                Id = a.Id, UserId = a.UserId,
                UserName = a.User != null ? a.User.Name : null,
                UserPhone = a.User != null ? a.User.PhoneNumber : null,
                Name = a.Name, Hour = a.Hour, Minute = a.Minute,
                IsActive = a.IsActive, LastTriggeredAt = a.LastTriggeredAt
            })
            .ToListAsync(ct);

        return (total, data);
    }

    public async Task<AlarmSummaryDto?> GetAlarmByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MealAlarms.Include(a => a.User)
            .Where(a => a.Id == id)
            .Select(a => new AlarmSummaryDto
            {
                Id = a.Id, UserId = a.UserId,
                UserName = a.User != null ? a.User.Name : null,
                UserPhone = a.User != null ? a.User.PhoneNumber : null,
                Name = a.Name, Hour = a.Hour, Minute = a.Minute,
                IsActive = a.IsActive, LastTriggeredAt = a.LastTriggeredAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(int Total, List<AlarmSummaryDto> Data)> GetAlarmsByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var data = await _context.MealAlarms
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Hour).ThenBy(a => a.Minute)
            .Select(a => new AlarmSummaryDto
            {
                Id = a.Id, UserId = a.UserId, Name = a.Name,
                Hour = a.Hour, Minute = a.Minute,
                IsActive = a.IsActive, LastTriggeredAt = a.LastTriggeredAt
            })
            .ToListAsync(ct);

        return (data.Count, data);
    }

    public async Task<bool> ToggleAlarmAsync(Guid id, CancellationToken ct = default)
    {
        var alarm = await _context.MealAlarms.FindAsync(new object[] { id }, ct);
        if (alarm == null) return false;

        // Using the domain method instead of reflection
        alarm.Toggle(!alarm.IsActive);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAlarmAsync(Guid id, CancellationToken ct = default)
    {
        var alarm = await _context.MealAlarms.FindAsync(new object[] { id }, ct);
        if (alarm == null) return false;
        _context.MealAlarms.Remove(alarm);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
