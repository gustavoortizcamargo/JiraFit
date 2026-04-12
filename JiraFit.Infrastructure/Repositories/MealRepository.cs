using JiraFit.Application.Interfaces;
using JiraFit.Domain.Entities;
using JiraFit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.Infrastructure.Repositories;

public class MealRepository : IMealRepository
{
    private readonly AppDbContext _context;

    public MealRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Meal meal, CancellationToken cancellationToken = default)
    {
        await _context.Meals.AddAsync(meal, cancellationToken);
    }

    public async Task<List<Meal>> GetDailyMealsAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);
        
        return await _context.Meals
            .Where(m => m.UserId == userId && m.Timestamp >= startOfDay && m.Timestamp < endOfDay)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
