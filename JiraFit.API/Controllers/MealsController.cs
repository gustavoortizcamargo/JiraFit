using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MealsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/meals?page=1&pageSize=20&userId=...&from=2025-01-01&to=2025-12-31
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Meals.AsQueryable();

        if (userId.HasValue)
            query = query.Where(m => m.UserId == userId.Value);
        if (from.HasValue)
            query = query.Where(m => m.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.Timestamp <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var meals = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                UserName = m.User != null ? m.User.Name : null,
                m.Calories,
                m.Proteins,
                m.Carbs,
                m.Fats,
                m.AiFeedback,
                m.RawText,
                m.ImageUrl,
                m.Timestamp
            })
            .ToListAsync(cancellationToken);

        return Ok(new { total, page, pageSize, data = meals });
    }

    // GET /api/meals/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var meal = await _context.Meals
            .Include(m => m.User)
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                UserName = m.User != null ? m.User.Name : null,
                UserPhone = m.User != null ? m.User.PhoneNumber : null,
                m.Calories,
                m.Proteins,
                m.Carbs,
                m.Fats,
                m.AiFeedback,
                m.RawText,
                m.ImageUrl,
                m.Timestamp
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (meal == null)
            return NotFound(new { message = "Refeição não encontrada." });

        return Ok(meal);
    }

    // GET /api/meals/user/{userId}/daily-summary?date=2025-04-13
    [HttpGet("user/{userId:guid}/daily-summary")]
    public async Task<IActionResult> GetDailySummary(
        Guid userId,
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = (date ?? DateTime.UtcNow.AddHours(-3)).Date;
        var nextDay = targetDate.AddDays(1);

        var meals = await _context.Meals
            .Where(m => m.UserId == userId && m.Timestamp >= targetDate && m.Timestamp < nextDay)
            .OrderBy(m => m.Timestamp)
            .Select(m => new
            {
                m.Id,
                m.Calories,
                m.Proteins,
                m.Carbs,
                m.Fats,
                m.RawText,
                m.Timestamp
            })
            .ToListAsync(cancellationToken);

        var totalCals = meals.Sum(m => m.Calories);
        var totalProt = meals.Sum(m => m.Proteins);
        var totalCarbs = meals.Sum(m => m.Carbs);
        var totalFats = meals.Sum(m => m.Fats);

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Tdee, u.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            userId,
            userName = user?.Name,
            date = targetDate.ToString("yyyy-MM-dd"),
            totalCalories = totalCals,
            totalProteins = totalProt,
            totalCarbs,
            totalFats,
            calorieGoal = user?.Tdee ?? 0,
            remainingCalories = (user?.Tdee ?? 0) - totalCals,
            mealCount = meals.Count,
            meals
        });
    }

    // GET /api/meals/user/{userId}/weekly-summary
    [HttpGet("user/{userId:guid}/weekly-summary")]
    public async Task<IActionResult> GetWeeklySummary(Guid userId, CancellationToken cancellationToken)
    {
        var endDate = DateTime.UtcNow.AddHours(-3).Date.AddDays(1);
        var startDate = endDate.AddDays(-7);

        var meals = await _context.Meals
            .Where(m => m.UserId == userId && m.Timestamp >= startDate && m.Timestamp < endDate)
            .ToListAsync(cancellationToken);

        var grouped = meals
            .GroupBy(m => m.Timestamp.Date)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                totalCalories = g.Sum(m => m.Calories),
                totalProteins = g.Sum(m => m.Proteins),
                totalCarbs = g.Sum(m => m.Carbs),
                totalFats = g.Sum(m => m.Fats),
                mealCount = g.Count()
            })
            .OrderBy(g => g.date)
            .ToList();

        return Ok(new { userId, days = grouped });
    }

    // DELETE /api/meals/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var meal = await _context.Meals.FindAsync(new object[] { id }, cancellationToken);
        if (meal == null)
            return NotFound(new { message = "Refeição não encontrada." });

        _context.Meals.Remove(meal);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Refeição removida com sucesso." });
    }

    // GET /api/meals/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.AddHours(-3).Date;
        var todayEnd = today.AddDays(1);

        var totalMeals = await _context.Meals.CountAsync(cancellationToken);
        var mealsToday = await _context.Meals.CountAsync(m => m.Timestamp >= today && m.Timestamp < todayEnd, cancellationToken);
        var avgCaloriesPerMeal = await _context.Meals.AverageAsync(m => (double?)m.Calories, cancellationToken) ?? 0;

        return Ok(new { totalMeals, mealsToday, avgCaloriesPerMeal = Math.Round(avgCaloriesPerMeal, 1) });
    }
}
