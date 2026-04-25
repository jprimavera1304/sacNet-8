using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/modulos")]
[Authorize]
public class ModulosController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ModulosController(IPermissionService permissions, ICurrentUserAccessor currentUserAccessor)
    {
        _permissions = permissions;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("disponibles")]
    public async Task<IActionResult> Disponibles([FromQuery] string? scope = "tenant", CancellationToken ct = default)
    {
        var empresaId = _currentUserAccessor.GetCompanyId(User);
        if (empresaId is null)
            return Unauthorized(new { message = "Token invalido." });

        var companyKey = _currentUserAccessor.GetCompanyKey(User);
        if (string.IsNullOrWhiteSpace(companyKey))
            return Unauthorized(new { message = "Token invalido: companyKey faltante." });

        var rawScope = (scope ?? "tenant").Trim().ToLowerInvariant();
        var includeAll = string.Equals(rawScope, "all", StringComparison.OrdinalIgnoreCase);

        var rolLegacy = _currentUserAccessor.GetRole(User);
        var isSuperAdmin = string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rolLegacy, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase);

        if (includeAll && !isSuperAdmin)
            return Forbid();

        var userId = _currentUserAccessor.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { message = "Token invalido." });

        var snapshot = await _permissions.GetPermissionsAsync(userId.Value, empresaId.Value, rolLegacy, ct);
        var modules = await _permissions.GetAvailableModulesAsync(empresaId.Value, companyKey, includeAll, ct);

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
}
