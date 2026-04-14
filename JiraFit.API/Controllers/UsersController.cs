using JiraFit.Domain.Entities;
using JiraFit.Domain.Enums;
using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/users?page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isPro = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsQueryable();

        if (isPro.HasValue)
            query = query.Where(u => u.IsPro == isPro.Value);

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.PhoneNumber,
                u.Name,
                u.Weight,
                u.Height,
                u.Age,
                u.Gender,
                u.Objective,
                u.Bmr,
                u.Tdee,
                u.IsPro,
                u.CurrentStreak,
                u.MessagesSentToday,
                u.CreatedAt,
                u.LastActivityDate,
                u.LastMessageTrackedDate
            })
            .ToListAsync(cancellationToken);

        return Ok(new { total, page, pageSize, data = users });
    }

    // GET /api/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Meals)
            .Include(u => u.Alarms)
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.PhoneNumber,
                u.Name,
                u.Weight,
                u.Height,
                u.Age,
                u.Gender,
                u.Objective,
                u.Bmr,
                u.Tdee,
                u.IsPro,
                u.CurrentStreak,
                u.MessagesSentToday,
                u.CreatedAt,
                u.LastActivityDate,
                u.LastMessageTrackedDate,
                MealCount = u.Meals.Count,
                AlarmCount = u.Alarms.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        return Ok(user);
    }

    // GET /api/users/phone/{phone}
    [HttpGet("phone/{phone}")]
    public async Task<IActionResult> GetByPhone(string phone, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Where(u => u.PhoneNumber == phone)
            .Select(u => new
            {
                u.Id,
                u.PhoneNumber,
                u.Name,
                u.Weight,
                u.Height,
                u.Tdee,
                u.IsPro,
                u.CurrentStreak,
                u.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        return Ok(user);
    }

    // PATCH /api/users/{id}/upgrade-pro
    [HttpPatch("{id:guid}/upgrade-pro")]
    public async Task<IActionResult> UpgradeToPro(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        user.UpgradeToPro();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Usuário {user.Name ?? user.PhoneNumber} promovido ao Plano Pro com sucesso!" });
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Usuário removido com sucesso." });
    }

    // GET /api/users/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var proUsers = await _context.Users.CountAsync(u => u.IsPro, cancellationToken);
        var freeUsers = totalUsers - proUsers;
        var activeToday = await _context.Users
            .CountAsync(u => u.LastMessageTrackedDate != null &&
                u.LastMessageTrackedDate.Value.Date == DateTime.UtcNow.AddHours(-3).Date,
                cancellationToken);

        return Ok(new
        {
            totalUsers,
            proUsers,
            freeUsers,
            activeToday,
            conversionRate = totalUsers > 0 ? Math.Round((double)proUsers / totalUsers * 100, 1) : 0
        });
    }
}
