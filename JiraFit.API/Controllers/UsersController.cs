using JiraFit.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IDashboardService _service;
    private const int MaxPageSize = 100;

    public UsersController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isPro = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (total, data) = await _service.GetUsersAsync(page, pageSize, isPro, ct);
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _service.GetUserByIdAsync(id, ct);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }

    [HttpGet("phone/{phone}")]
    public async Task<IActionResult> GetByPhone(string phone, CancellationToken ct)
    {
        var user = await _service.GetUserByPhoneAsync(phone, ct);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }

    [HttpPatch("{id:guid}/upgrade-pro")]
    public async Task<IActionResult> UpgradeToPro(Guid id, CancellationToken ct)
    {
        var ok = await _service.UpgradeToProAsync(id, ct);
        return ok ? Ok(new { message = "Usuário promovido ao Plano Pro!" }) : NotFound(new { message = "Usuário não encontrado." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeleteUserAsync(id, ct);
        return ok ? Ok(new { message = "Usuário removido." }) : NotFound(new { message = "Usuário não encontrado." });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        return Ok(await _service.GetUserStatsAsync(ct));
    }
}
