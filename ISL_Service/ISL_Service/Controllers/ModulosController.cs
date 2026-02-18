using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/modulos")]
[Authorize]
public class ModulosController : ControllerBase
{
    private readonly IPermissionService _permissions;

    public ModulosController(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    [HttpGet("disponibles")]
    public async Task<IActionResult> Disponibles([FromQuery] string? scope = "tenant", CancellationToken ct = default)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var companyKey = (User.FindFirstValue("companyKey") ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(companyKey))
            return Unauthorized(new { message = "Token invalido: companyKey faltante." });

        var rawScope = (scope ?? "tenant").Trim().ToLowerInvariant();
        var includeAll = string.Equals(rawScope, "all", StringComparison.OrdinalIgnoreCase);

        var rolLegacy = (User.FindFirstValue("roleLegacy")
            ?? User.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty).Trim();

        var isSuperAdmin = string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rolLegacy, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase);

        if (includeAll && !isSuperAdmin)
            return Forbid();

        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var snapshot = await _permissions.GetPermissionsAsync(userId, empresaId, rolLegacy, ct);
        var modules = await _permissions.GetAvailableModulesAsync(empresaId, companyKey, includeAll, ct);

        if (!includeAll && !isSuperAdmin)
        {
            if (snapshot.PermissionsEnabled)
            {
                var permissionSet = new HashSet<string>(snapshot.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                modules = modules
                    .Where(m => permissionSet.Contains(m.CapabilityVer))
                    .ToList();
            }
            else
            {
                modules = modules
                    .Where(m => _permissions.IsAllowedByLegacy(rolLegacy, m.CapabilityVer))
                    .ToList();
            }
        }

        return Ok(new
        {
            scope = includeAll ? "all" : "tenant",
            companyKey,
            modules
        });
    }

    private static bool TryGetEmpresaId(ClaimsPrincipal user, out int empresaId)
    {
        empresaId = 0;
        var raw = user.FindFirstValue("empresaId");
        return int.TryParse(raw, out empresaId);
    }
}
