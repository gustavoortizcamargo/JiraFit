using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[Route("api/[controller]")]
public class MealsController : ScopedControllerBase
{
    private readonly IDashboardService _service;
    private const int MaxPageSize = 100;

    public MealsController(IDashboardService service, AppDbContext context) : base(context)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/meals — List MY meals with optional date filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyMeals(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (total, data) = await _service.GetMealsAsync(userId, from, to, page, pageSize, ct);
        return Ok(new { total, page, pageSize, data });
    }

    /// <summary>
    /// GET /api/meals/{id} — Get a specific meal (only if it belongs to the user).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var meal = await _service.GetMealByIdAsync(id, ct);
        if (meal is null || meal.UserId != userId.Value)
            return NotFound(new { message = "Refeição não encontrada." });

        return Ok(meal);
    }

    /// <summary>
    /// GET /api/meals/daily-summary — My daily summary for a given date.
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> GetDailySummary([FromQuery] DateTime? date = null, CancellationToken ct = default)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var summary = await _service.GetDailySummaryAsync(userId.Value, date, ct);
        return summary is null ? NotFound(new { message = "Sem dados." }) : Ok(summary);
    }

    /// <summary>
    /// GET /api/meals/weekly-summary — My last 7 days breakdown.
    /// </summary>
    [HttpGet("weekly-summary")]
    public async Task<IActionResult> GetWeeklySummary(CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        return Ok(await _service.GetWeeklySummaryAsync(userId.Value, ct));
    }

    /// <summary>
    /// DELETE /api/meals/{id} — Delete one of MY meals.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        // Verify ownership before deleting
        var meal = await _service.GetMealByIdAsync(id, ct);
        if (meal is null || meal.UserId != userId.Value)
            return NotFound(new { message = "Refeição não encontrada." });

        var ok = await _service.DeleteMealAsync(id, ct);
        return ok ? Ok(new { message = "Refeição removida." }) : NotFound(new { message = "Refeição não encontrada." });
    }
    /// <summary>
    /// POST /api/meals — Manually register a meal (scoped to the logged-in user).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateMeal([FromBody] CreateMealRequestDto request, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        var meal = await _service.CreateMealAsync(userId.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = meal.Id }, meal);
    }

    /// <summary>
    /// PATCH /api/meals/{id} — Update facts of one of MY meals.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateMeal(Guid id, [FromBody] UpdateMealRequestDto request, CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Conta não vinculada a perfil WhatsApp." });

        // Verify ownership
        var meal = await _service.GetMealByIdAsync(id, ct);
        if (meal is null || meal.UserId != userId.Value)
            return NotFound(new { message = "Refeição não encontrada." });

        var ok = await _service.UpdateMealAsync(id, request, ct);
        return ok ? NoContent() : NotFound();
    }
}
