using JiraFit.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlarmsController : ControllerBase
{
    private readonly IDashboardService _service;
    private const int MaxPageSize = 100;

    public AlarmsController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (total, data) = await _service.GetAlarmsAsync(userId, isActive, page, pageSize, ct);
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var alarm = await _service.GetAlarmByIdAsync(id, ct);
        return alarm is null ? NotFound(new { message = "Alarme não encontrado." }) : Ok(alarm);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var (total, data) = await _service.GetAlarmsByUserAsync(userId, ct);
        return Ok(new { userId, total, data });
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var ok = await _service.ToggleAlarmAsync(id, ct);
        return ok ? Ok(new { message = "Status do alarme alterado." }) : NotFound(new { message = "Alarme não encontrado." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeleteAlarmAsync(id, ct);
        return ok ? Ok(new { message = "Alarme removido." }) : NotFound(new { message = "Alarme não encontrado." });
    }
}
