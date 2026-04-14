using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlarmsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AlarmsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/alarms?userId=...&isActive=true
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MealAlarms.Include(a => a.User).AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);
        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var alarms = await query
            .OrderBy(a => a.Hour).ThenBy(a => a.Minute)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                UserName = a.User != null ? a.User.Name : null,
                UserPhone = a.User != null ? a.User.PhoneNumber : null,
                a.Name,
                a.Hour,
                a.Minute,
                a.IsActive,
                a.LastTriggeredAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { total, page, pageSize, data = alarms });
    }

    // GET /api/alarms/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var alarm = await _context.MealAlarms
            .Include(a => a.User)
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                UserName = a.User != null ? a.User.Name : null,
                UserPhone = a.User != null ? a.User.PhoneNumber : null,
                a.Name,
                a.Hour,
                a.Minute,
                a.IsActive,
                a.LastTriggeredAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (alarm == null)
            return NotFound(new { message = "Alarme não encontrado." });

        return Ok(alarm);
    }

    // GET /api/alarms/user/{userId}
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken cancellationToken)
    {
        var alarms = await _context.MealAlarms
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Hour).ThenBy(a => a.Minute)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hour,
                a.Minute,
                a.IsActive,
                a.LastTriggeredAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { userId, total = alarms.Count, data = alarms });
    }

    // PATCH /api/alarms/{id}/toggle
    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var alarm = await _context.MealAlarms.FindAsync(new object[] { id }, cancellationToken);
        if (alarm == null)
            return NotFound(new { message = "Alarme não encontrado." });

        // Toggle IsActive via reflection-safe workaround
        var prop = typeof(JiraFit.Domain.Entities.MealAlarm).GetProperty("IsActive");
        prop?.SetValue(alarm, !alarm.IsActive);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Alarme '{alarm.Name}' está agora {(alarm.IsActive ? "ativo" : "inativo")}.", isActive = alarm.IsActive });
    }

    // DELETE /api/alarms/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var alarm = await _context.MealAlarms.FindAsync(new object[] { id }, cancellationToken);
        if (alarm == null)
            return NotFound(new { message = "Alarme não encontrado." });

        _context.MealAlarms.Remove(alarm);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Alarme '{alarm.Name}' removido com sucesso." });
    }
}
