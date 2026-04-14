using JiraFit.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly IDashboardService _service;
    private const int MaxPageSize = 100;

    public MealsController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (total, data) = await _service.GetMealsAsync(userId, from, to, page, pageSize, ct);
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var meal = await _service.GetMealByIdAsync(id, ct);
        return meal is null ? NotFound(new { message = "Refeição não encontrada." }) : Ok(meal);
    }

    [HttpGet("user/{userId:guid}/daily-summary")]
    public async Task<IActionResult> GetDailySummary(Guid userId, [FromQuery] DateTime? date = null, CancellationToken ct = default)
    {
        var summary = await _service.GetDailySummaryAsync(userId, date, ct);
        return summary is null ? NotFound(new { message = "Sem dados." }) : Ok(summary);
    }

    [HttpGet("user/{userId:guid}/weekly-summary")]
    public async Task<IActionResult> GetWeeklySummary(Guid userId, CancellationToken ct)
    {
        return Ok(await _service.GetWeeklySummaryAsync(userId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeleteMealAsync(id, ct);
        return ok ? Ok(new { message = "Refeição removida." }) : NotFound(new { message = "Refeição não encontrada." });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        return Ok(await _service.GetMealStatsAsync(ct));
    }
}
