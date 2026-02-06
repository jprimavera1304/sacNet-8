using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;

    public MeController(IUserRepository users)
    {
        _users = users;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        return Ok(new
        {
            id = user.Id,
            usuario = user.UsuarioNombre,
            rol = user.Rol,
            empresaId = user.EmpresaId,
            debeCambiarContrasena = user.DebeCambiarContrasena
        });
    }
}
