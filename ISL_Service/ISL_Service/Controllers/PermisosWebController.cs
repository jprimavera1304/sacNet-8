using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/permisos-web")]
[Authorize]
public class PermisosWebController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    /*
      EL GUARDIA QUE DECIDE LO QUE LA POLITICA NO PUEDE DECIDIR.

      Los [Authorize(Policy = "perm:...")] de abajo contestan "¿tiene la llave
      de esta pantalla?" y se quedan ahi. La pregunta que faltaba depende del
      CUERPO de la peticion: "¿lo que esta intentando guardar cabe dentro de lo
      que el mismo tiene, y deja a alguien capaz de deshacerlo?".

      Se midio con curl contra el backend local (MacZ) ANTES de ponerlo: un
      usuario de prueba con 11 claves, TODAS de administracion de permisos y
      ninguna de negocio, conseguia 200 en los cuatro ataques — concederse
      "empleados.sueldo.ver" y los "usuarios.*", degradar el rol de sistema
      ADMIN de 17 claves a 1, firmarse ajustes individuales, y apagar los
      modulos "permisos_roles" y "permisos_modulos", tras lo cual su propio
      /api/me ya no traia ninguna clave "permisos*" y volver a prenderlos
      contestaba 403. Sin vuelta por API.

      Los frenos existian en el front (ModalPermisosDeRol.tsx no te deja
      quitarte a ti mismo; nucleo/permisosWeb.ts marca los modulos del sistema
      como no apagables), o sea que no existian.
    */
    private readonly IGuardiaDePermisos _guardia;

    public PermisosWebController(
        IPermissionService permissions,
        ICurrentUserAccessor currentUserAccessor,
        IGuardiaDePermisos guardia)
    {
        _permissions = permissions;
        _currentUserAccessor = currentUserAccessor;
        _guardia = guardia;
    }

    /// <summary>
    /// El 403 del guardia, con SU mensaje tal cual.
    /// </summary>
    /// <remarks>
    /// No se resume ni se cambia por un "No tienes permiso": el mensaje nombra
    /// las claves que faltan o a quien hay que darle la llave antes de
    /// reintentar, y eso es la diferencia entre que la persona lo arregle sola
    /// y que levante el telefono. El formato { ok, message } es el mismo que ya
    /// devuelve PermisoControllerBase, para que el front no tenga que
    /// distinguir de donde salio el 403.
    /// </remarks>
    private IActionResult Prohibido(EscaladaDePermisosException ex) =>
        StatusCode(StatusCodes.Status403Forbidden, new { ok = false, message = ex.Message });

    [HttpGet("bootstrap")]
    [HttpGet("/api/capacidades/bootstrap")]
    [Authorize(Policy = "perm:permisosweb.bootstrap|app_movil.permisosweb.bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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
    [Authorize(Policy = "perm:permisos_roles.ver_modulo|app_movil.permisos_roles.ver")]
    public async Task<IActionResult> RolesBootstrap(CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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
    [Authorize(Policy = "perm:permisos_modulos.ver_modulo|app_movil.permisos_modulos.ver")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var items = await _permissions.GetPermissionCatalogAsync(empresaId, ct);
        var modules = await _permissions.GetModuleCatalogAsync(empresaId, ct);
        return Ok(new { permissionsEnabled = true, permissions = items, modules });
    }

    [HttpPut("modulos/{moduleKey}/status")]
    [HttpPut("catalogo/modulos/{moduleKey}/status")]
    [Authorize(Policy = "perm:permisos_modulos.activar|app_movil.permisos_modulos.activar")]
    public async Task<IActionResult> SetModuleStatusByRoute(
        [FromRoute] string moduleKey,
        [FromBody] PermisosWebSetModuleStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        var bodyKey = request?.ModuloClave?.Trim() ?? string.Empty;
        var routeKey = moduleKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(routeKey))
            return UnprocessableEntity(new { message = "Modulo invalido." });
        if (!string.IsNullOrWhiteSpace(bodyKey) && !string.Equals(bodyKey, routeKey, StringComparison.OrdinalIgnoreCase))
            return UnprocessableEntity(new { message = "Modulo de ruta y body no coinciden." });

        try
        {
            var idStatus = request?.IdStatus ?? 0;
            await _guardia.ExigirPuedeCambiarEstatusDeModuloAsync(User, empresaId, routeKey, idStatus, ct);
            var updated = await _permissions.SetModuleStatusAsync(empresaId, routeKey, idStatus, ct);
            return Ok(new { ok = true, updated });
        }
        catch (EscaladaDePermisosException ex)
        {
            return Prohibido(ex);
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
    [Authorize(Policy = "perm:permisos_modulos.activar|app_movil.permisos_modulos.activar")]
    public async Task<IActionResult> SetModuleStatus(
        [FromBody] PermisosWebSetModuleStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null || string.IsNullOrWhiteSpace(request.ModuloClave))
            return UnprocessableEntity(new { message = "Payload invalido." });

        try
        {
            // La MISMA comprobacion que en la variante con la clave en la ruta.
            // Son dos puertas al mismo cambio: cerrar una sola dejaria el
            // apagado abierto por la otra y nadie lo notaria.
            await _guardia.ExigirPuedeCambiarEstatusDeModuloAsync(User, empresaId, request.ModuloClave, request.IdStatus, ct);
            var updated = await _permissions.SetModuleStatusAsync(empresaId, request.ModuloClave, request.IdStatus, ct);
            return Ok(new { ok = true, updated });
        }
        catch (EscaladaDePermisosException ex)
        {
            return Prohibido(ex);
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
    [Authorize(Policy = "perm:permisosweb.roles.editar|app_movil.permisos_roles.editar")]
    public async Task<IActionResult> SaveRolePermissions(
        [FromRoute] string roleCode,
        [FromBody] PermisosWebUpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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
            await _guardia.ExigirPuedeEditarRolAsync(User, empresaId, roleCode, normalizedPermissions, ct);
            await _permissions.UpsertRolePermissionsAsync(empresaId, roleCode, normalizedPermissions, ct);
        }
        catch (EscaladaDePermisosException ex)
        {
            return Prohibido(ex);
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
    [Authorize(Policy = "perm:permisosweb.roles.editar|app_movil.permisos_roles.editar")]
    public async Task<IActionResult> CreateRole(
        [FromBody] PermisosWebCreateRoleRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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
    [Authorize(Policy = "perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar")]
    public async Task<IActionResult> SaveUserOverrides(
        [FromRoute] Guid userId,
        [FromBody] PermisosWebUpdateUserOverridesRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { message = "Token invalido." });

        if (request is null)
            return UnprocessableEntity(new { message = "Payload invalido." });

        if (request.UserId != Guid.Empty && request.UserId != userId)
            return UnprocessableEntity(new { message = "userId de ruta y body no coinciden." });

        var allow = request.Allow ?? new List<string>();
        var deny = request.Deny ?? new List<string>();

        try
        {
            await _guardia.ExigirPuedeEditarOverridesAsync(User, empresaId, userId, allow, deny, ct);
            // true: el administrador acaba de decidir el estado completo del
            // usuario en la pantalla de permisos, asi que lo que desmarco
            // tambien tiene que revocarse en Mac31.
            await _permissions.UpsertUserOverridesAsync(empresaId, userId, allow, deny, sincronizarConLegacy: true, ct);
        }
        catch (EscaladaDePermisosException ex)
        {
            return Prohibido(ex);
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
    [Authorize(Policy = "perm:permisosweb.catalogo.editar|app_movil.permisos_modulos.activar")]
    public async Task<IActionResult> SyncCatalog(
        [FromBody] PermisosWebSyncCatalogRequest? request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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
    [Authorize(Policy = "perm:permisosweb.catalogo.editar|app_movil.permisos_modulos.activar")]
    public async Task<IActionResult> CreatePermission(
        [FromBody] PermisosWebCreatePermissionRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmpresaId(out var empresaId))
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

    private bool TryGetEmpresaId(out int empresaId)
    {
        var value = _currentUserAccessor.GetCompanyId(User);
        empresaId = value ?? 0;
        return value is not null;
    }
}
