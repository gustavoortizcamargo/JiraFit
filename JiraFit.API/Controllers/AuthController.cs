using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IConfiguration _configuration;

    public AuthController(IDashboardService dashboardService, IConfiguration configuration)
    {
        _dashboardService = dashboardService;
        _configuration = configuration;
    }

    /// <summary>
    /// Register a new dashboard user linked to a WhatsApp phone number.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email e senha são obrigatórios." });

        if (request.Password.Length < 6)
            return BadRequest(new { message = "A senha precisa ter pelo menos 6 caracteres." });

        try
        {
            var dashUser = await _dashboardService.RegisterAsync(request.Email, request.Password, request.PhoneNumber, ct);
            var token = GenerateJwtToken(dashUser.Id, dashUser.Email, dashUser.Role);

            return Created("", new AuthResponseDto
            {
                Token = token,
                Email = dashUser.Email,
                Role = dashUser.Role,
                LinkedUserId = dashUser.LinkedUserId,
                ExpiresAt = DateTime.UtcNow.AddHours(-3).AddHours(24)
            });
        }
        catch (Exception)
        {
            return Conflict(new { message = "Este email já está cadastrado." });
        }
    }

    /// <summary>
    /// Authenticate and receive a JWT token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email e senha são obrigatórios." });

        var dashUser = await _dashboardService.AuthenticateAsync(request.Email, request.Password, ct);
        if (dashUser == null)
            return Unauthorized(new { message = "Email ou senha incorretos." });

        var token = GenerateJwtToken(dashUser.Id, dashUser.Email, dashUser.Role);

        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = dashUser.Email,
            Role = dashUser.Role,
            LinkedUserId = dashUser.LinkedUserId,
            ExpiresAt = DateTime.UtcNow.AddHours(-3).AddHours(24)
        });
    }

    private string GenerateJwtToken(Guid userId, string email, string role)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "JiraFit",
            audience: _configuration["Jwt:Audience"] ?? "JiraFitUsers",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
