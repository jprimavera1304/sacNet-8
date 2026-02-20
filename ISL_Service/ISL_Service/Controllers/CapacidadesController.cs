using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/capacidades")]
[Authorize]
public class CapacidadesController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPermissionService _permissionService;

    public CapacidadesController(IUserRepository users, IPermissionService permissionService)
    {
        _users = users;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        var snapshot = await _permissionService.GetPermissionsAsync(user.Id, user.EmpresaId, user.Rol, ct);
        if (!snapshot.PermissionsEnabled)
            return NotFound(new { message = "Capacidades no disponibles para este tenant/base." });

        return Ok(new CapabilitiesResponse
        {
            UserId = snapshot.UserId,
            EmpresaId = snapshot.EmpresaId,
            RolLegacy = snapshot.RolLegacy,
            PermissionsEnabled = snapshot.PermissionsEnabled,
            Permissions = snapshot.Permissions,
            PermissionsVersion = snapshot.PermissionsVersion
        });
    }
}
