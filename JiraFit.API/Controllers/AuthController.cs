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
    /// Step 1: Register and send SMS verification code.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email e senha são obrigatórios." });

        if (request.Password.Length < 6)
            return BadRequest(new { message = "A senha precisa ter pelo menos 6 caracteres." });

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { message = "Número de telefone é obrigatório para verificação SMS." });

        try
        {
            var dashUser = await _dashboardService.RegisterAsync(request.Email, request.Password, request.PhoneNumber, ct);
            
            // Send SMS verification code
            await _dashboardService.SendVerificationCodeAsync(dashUser.Id, ct);

            return Created("", new RegisterPendingResponseDto
            {
                DashboardUserId = dashUser.Id,
                Message = "Cadastro criado! Enviamos um código de verificação por SMS para o número informado."
            });
        }
        catch (Exception)
        {
            return Conflict(new { message = "Este email já está cadastrado." });
        }
    }

    /// <summary>
    /// Step 2: Verify SMS code and receive JWT token.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Código de verificação é obrigatório." });

        var dashUser = await _dashboardService.VerifyCodeAsync(request.DashboardUserId, request.Code, ct);
        if (dashUser == null)
            return Unauthorized(new { message = "Código inválido ou expirado. Solicite um novo código." });

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

    /// <summary>
    /// Resend SMS verification code.
    /// </summary>
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequestDto request, CancellationToken ct)
    {
        var result = await _dashboardService.ResendVerificationCodeAsync(request.DashboardUserId, ct);
        if (result == null)
            return NotFound(new { message = "Usuário não encontrado." });

        return Ok(new { message = "Novo código enviado por SMS." });
    }

    /// <summary>
    /// Authenticate with email and password (only works after phone verification).
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email e senha são obrigatórios." });

        var dashUser = await _dashboardService.AuthenticateAsync(request.Email, request.Password, ct);
        if (dashUser == null)
            return Unauthorized(new { message = "Email/senha incorretos ou telefone ainda não verificado." });

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
