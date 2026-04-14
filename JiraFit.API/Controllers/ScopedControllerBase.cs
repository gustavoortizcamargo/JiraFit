using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JiraFit.Application.Interfaces;
using JiraFit.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraFit.API.Controllers;

/// <summary>
/// Base controller that extracts the current user's LinkedUserId from their JWT.
/// All dashboard controllers inherit from this to auto-scope data to the logged-in user.
/// </summary>
[ApiController]
[Authorize]
public abstract class ScopedControllerBase : ControllerBase
{
    private readonly AppDbContext _context;

    protected ScopedControllerBase(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the WhatsApp User ID linked to the currently authenticated dashboard user.
    /// Returns null if the dashboard user has no linked WhatsApp account.
    /// </summary>
    protected async Task<Guid?> GetLinkedUserIdAsync(CancellationToken ct)
    {
        var dashboardUserIdStr = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(dashboardUserIdStr) || !Guid.TryParse(dashboardUserIdStr, out var dashboardUserId))
            return null;

        var dashUser = await _context.DashboardUsers
            .AsNoTracking()
            .Where(d => d.Id == dashboardUserId)
            .Select(d => d.LinkedUserId)
            .FirstOrDefaultAsync(ct);

        return dashUser;
    }
}
