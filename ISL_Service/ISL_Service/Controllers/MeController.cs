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
    private readonly IPermissionService _permissionService;

    public MeController(
        IUserRepository users,
        IUserAdminService userAdminService,
        IEmpresaRepository empresas,
        IPermissionService permissionService)
    {
        _users = users;
        _userAdminService = userAdminService;
        _empresas = empresas;
        _permissionService = permissionService;
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

        var permissionSnapshot = await _permissionService.GetPermissionsAsync(
            user.Id,
            user.EmpresaId,
            user.Rol,
            ct);

        var companyKey = await _empresas.GetCompanyKeyAsync(ct);

        var response = new MeResponse
        {
            UserId = user.Id,
            Id = user.Id,
            Usuario = user.UsuarioNombre,
            RolLegacy = user.Rol,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            PermissionsEnabled = permissionSnapshot.PermissionsEnabled,
            Permissions = permissionSnapshot.Permissions,
            PermissionsVersion = permissionSnapshot.PermissionsVersion,
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
