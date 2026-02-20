using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/permisos-web")]
[Authorize]
public class PermisosWebController : ControllerBase
{
    private readonly IPermissionService _permissions;

    public PermisosWebController(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    [HttpGet("bootstrap")]
    [HttpGet("/api/capacidades/bootstrap")]
    [Authorize(Policy = "perm:permisosweb.bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var payload = await _permissions.GetPermisosWebBootstrapAsync(empresaId, ct);
        if (payload is null)
            return Ok(new
            {
                permissionsEnabled = false,
                roles = Array.Empty<object>(),
                permissions = Array.Empty<object>(),
                rolePermissions = Array.Empty<object>(),
                users = Array.Empty<object>(),
                userOverrides = Array.Empty<object>(),
                message = "Capacidades no disponibles para este tenant/base."
            });

        return Ok(payload);
    }

    [HttpGet("roles/bootstrap")]
    [Authorize(Policy = "perm:permisos_roles.ver_modulo")]
    public async Task<IActionResult> RolesBootstrap(CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var payload = await _permissions.GetPermisosRolesBootstrapAsync(empresaId, ct);
        if (payload is null)
            return Ok(new
            {
                permissionsEnabled = false,
                roles = Array.Empty<object>(),
                permissions = Array.Empty<object>(),
                rolePermissions = Array.Empty<object>(),
                message = "Capacidades no disponibles para este tenant/base."
            });

        return Ok(payload);
    }

    [HttpGet("catalogo")]
    [Authorize(Policy = "perm:permisos_modulos.ver_modulo")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var items = await _permissions.GetPermissionCatalogAsync(empresaId, ct);
        var modules = await _permissions.GetModuleCatalogAsync(empresaId, ct);
        return Ok(new { permissionsEnabled = true, permissions = items, modules });
    }

    [HttpPut("modulos/{moduleKey}/status")]
    [HttpPut("catalogo/modulos/{moduleKey}/status")]
    [Authorize(Policy = "perm:permisos_modulos.activar")]
    public async Task<IActionResult> SetModuleStatusByRoute(
        [FromRoute] string moduleKey,
        [FromBody] PermisosWebSetModuleStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var bodyKey = request?.ModuloClave?.Trim() ?? string.Empty;
        var routeKey = moduleKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(routeKey))
            return UnprocessableEntity(new { message = "Modulo invalido." });
        if (!string.IsNullOrWhiteSpace(bodyKey) && !string.Equals(bodyKey, routeKey, StringComparison.OrdinalIgnoreCase))
            return UnprocessableEntity(new { message = "Modulo de ruta y body no coinciden." });

        try
        {
            var updated = await _permissions.SetModuleStatusAsync(empresaId, routeKey, request?.IdStatus ?? 0, ct);
            return Ok(new { ok = true, updated });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("modulos/status")]
    [Authorize(Policy = "perm:permisos_modulos.activar")]
    public async Task<IActionResult> SetModuleStatus(
        [FromBody] PermisosWebSetModuleStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null || string.IsNullOrWhiteSpace(request.ModuloClave))
            return UnprocessableEntity(new { message = "Payload invalido." });

        try
        {
            var updated = await _permissions.SetModuleStatusAsync(empresaId, request.ModuloClave, request.IdStatus, ct);
            return Ok(new { ok = true, updated });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("roles/{roleCode}/permissions")]
    [HttpPut("/api/capacidades/roles/{roleCode}/permissions")]
    [Authorize(Policy = "perm:permisosweb.roles.editar")]
    public async Task<IActionResult> SaveRolePermissions(
        [FromRoute] string roleCode,
        [FromBody] PermisosWebUpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null || string.IsNullOrWhiteSpace(roleCode))
            return UnprocessableEntity(new { message = "Payload invalido." });

        if (!string.IsNullOrWhiteSpace(request.RoleCode) &&
            !string.Equals(request.RoleCode.Trim(), roleCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return UnprocessableEntity(new { message = "roleCode de ruta y body no coinciden." });
        }

        var normalizedPermissions = request.Permissions ?? new List<string>();
        try
        {
            await _permissions.UpsertRolePermissionsAsync(empresaId, roleCode, normalizedPermissions, ct);
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        return Ok(new { ok = true });
    }

    [HttpPost("roles")]
    [Authorize(Policy = "perm:permisosweb.roles.editar")]
    public async Task<IActionResult> CreateRole(
        [FromBody] PermisosWebCreateRoleRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null)
            return UnprocessableEntity(new { message = "Payload invalido." });

        try
        {
            var created = await _permissions.CreateRoleAsync(empresaId, request.RoleCode, request.Name, ct);
            return Ok(new { ok = true, created });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("usuarios/{userId:guid}/overrides")]
    [HttpPut("/api/capacidades/usuarios/{userId:guid}/overrides")]
    [Authorize(Policy = "perm:permisosweb.overrides.editar")]
    public async Task<IActionResult> SaveUserOverrides(
        [FromRoute] Guid userId,
        [FromBody] PermisosWebUpdateUserOverridesRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null)
            return UnprocessableEntity(new { message = "Payload invalido." });

        if (request.UserId != Guid.Empty && request.UserId != userId)
            return UnprocessableEntity(new { message = "userId de ruta y body no coinciden." });

        var allow = request.Allow ?? new List<string>();
        var deny = request.Deny ?? new List<string>();

        try
        {
            await _permissions.UpsertUserOverridesAsync(empresaId, userId, allow, deny, ct);
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        return Ok(new { ok = true });
    }

    [HttpPost("catalogo/sync")]
    [Authorize(Policy = "perm:permisosweb.catalogo.editar")]
    public async Task<IActionResult> SyncCatalog(
        [FromBody] PermisosWebSyncCatalogRequest? request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        try
        {
            var result = await _permissions.SyncPermissionCatalogAsync(empresaId, request?.Modules, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("catalogo/permissions")]
    [Authorize(Policy = "perm:permisosweb.catalogo.editar")]
    public async Task<IActionResult> CreatePermission(
        [FromBody] PermisosWebCreatePermissionRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(User, out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null)
            return UnprocessableEntity(new { message = "Payload invalido." });

        try
        {
            var created = await _permissions.CreatePermissionAsync(empresaId, request.Key, request.Name, request.Description, ct);
            return Ok(new { ok = true, created });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private static bool TryGetEmpresaId(ClaimsPrincipal user, out int empresaId)
    {
        empresaId = 0;
        var raw = user.FindFirstValue("empresaId");
        return int.TryParse(raw, out empresaId);
    }
}
