using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[Route("api/[controller]")]
public class AlarmsController : ScopedControllerBase
{
    private readonly IDashboardService _service;

    public AlarmsController(IDashboardService service, AppDbContext context) : base(context)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/alarms — List MY alarms.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyAlarms(CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var (total, data) = await _service.GetAlarmsByUserAsync(userId.Value, ct);
        return Ok(new { total, data });
    }

    /// <summary>
    /// GET /api/alarms/{id} — Get a specific alarm (only if mine).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var alarm = await _service.GetAlarmByIdAsync(id, ct);
        if (alarm is null || alarm.UserId != userId.Value)
            return NotFound(new { message = "Alarme não encontrado." });

        return Ok(alarm);
    }

    /// <summary>
    /// PATCH /api/alarms/{id}/toggle — Toggle one of MY alarms.
    /// </summary>
    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var alarm = await _service.GetAlarmByIdAsync(id, ct);
        if (alarm is null || alarm.UserId != userId.Value)
            return NotFound(new { message = "Alarme não encontrado." });

        var ok = await _service.ToggleAlarmAsync(id, ct);
        return ok ? Ok(new { message = "Status do alarme alterado." }) : NotFound(new { message = "Alarme não encontrado." });
    }

    /// <summary>
    /// DELETE /api/alarms/{id} — Delete one of MY alarms.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var alarm = await _service.GetAlarmByIdAsync(id, ct);
        if (alarm is null || alarm.UserId != userId.Value)
            return NotFound(new { message = "Alarme não encontrado." });

        var ok = await _service.DeleteAlarmAsync(id, ct);
        return ok ? Ok(new { message = "Alarme removido." }) : NotFound(new { message = "Alarme não encontrado." });
    }

    /// <summary>
    /// POST /api/alarms — Create a new alarm for the logged-in user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlarmRequestDto request, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var alarm = await _service.CreateAlarmAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = alarm.Id }, alarm);
    }

    /// <summary>
    /// PATCH /api/alarms/{id} — Update an existing alarm (Name, Time, Active state).
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAlarmRequestDto request, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        // Verify ownership
        var alarm = await _service.GetAlarmByIdAsync(id, ct);
        if (alarm is null || alarm.UserId != userId.Value)
            return NotFound(new { message = "Alarme não encontrado." });

        var ok = await _service.UpdateAlarmAsync(id, request, ct);
        return ok ? NoContent() : NotFound();
    }
}
