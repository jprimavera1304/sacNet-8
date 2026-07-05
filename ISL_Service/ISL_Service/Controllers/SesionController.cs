using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Controllers;

[ApiController]
public class SesionController : ControllerBase
{
    private readonly IUserService _users;

    public SesionController(IUserService users)
    {
        _users = users;
    }

    // RUTA ABSOLUTA (no choca con api/[controller])
    [HttpPost("/api/auth/login")]
    [HttpPost("/api/sesion/login")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _users.LoginAsync(request, ct);
        return Ok(result);
    }

    // Canjea el refresh token por un nuevo access token (solo app movil).
    [HttpPost("/api/auth/refresh")]
    [HttpPost("/api/sesion/refresh")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _users.RefreshAsync(request?.RefreshToken ?? string.Empty, ct);
            return Ok(result);
        }
        catch (System.Security.Authentication.AuthenticationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // Revoca el refresh token (logout de la app).
    [HttpPost("/api/auth/logout")]
    [HttpPost("/api/sesion/logout")]
    [Consumes("application/json")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _users.LogoutAsync(request?.RefreshToken ?? string.Empty, ct);
        return Ok(new { ok = true });
    }
}
