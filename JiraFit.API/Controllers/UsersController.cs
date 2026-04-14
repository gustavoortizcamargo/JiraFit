using JiraFit.Application.Interfaces;
using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[Route("api/[controller]")]
public class UsersController : ScopedControllerBase
{
    private readonly IDashboardService _service;

    public UsersController(IDashboardService service, AppDbContext context) : base(context)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/users/me — Get the logged-in user's own profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = await GetLinkedUserIdAsync(ct);
        if (userId == null)
            return NotFound(new { message = "Sua conta ainda não está vinculada a um perfil WhatsApp." });

        var user = await _service.GetUserByIdAsync(userId.Value, ct);
        return user is null ? NotFound(new { message = "Perfil não encontrado." }) : Ok(user);
    }
}
