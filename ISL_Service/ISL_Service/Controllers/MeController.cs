using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IUserAdminService _userAdminService;
    private readonly IEmpresaRepository _empresas;

    public MeController(IUserRepository users, IUserAdminService userAdminService, IEmpresaRepository empresas)
    {
        _users = users;
        _userAdminService = userAdminService;
        _empresas = empresas;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        var companyKey = await _empresas.GetCompanyKeyAsync(ct);

        var response = new MeResponse
        {
            Id = user.Id,
            Usuario = user.UsuarioNombre,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            CompanyKey = companyKey,
            DebeCambiarContrasena = user.DebeCambiarContrasena
        };

        return Ok(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        await _userAdminService.ChangeMyPasswordAsync(req, User, ct);
        return NoContent();
    }
}
