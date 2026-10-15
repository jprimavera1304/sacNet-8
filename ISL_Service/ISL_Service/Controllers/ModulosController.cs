using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

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

        /*
          `scope=empresa`: QUE TIENE LA EMPRESA, SIN RECORTAR POR EL USUARIO.

          POR QUE HIZO FALTA. Con `tenant` esta lista pasa por tres filtros: la
          empresa lo tiene encendido, existe la clave `<modulo>.ver_modulo`, y
          EL USUARIO la tiene concedida. Los dos primeros son de la empresa; el
          tercero no, y ahi estaba el problema: la app usa esta lista para
          preguntar "¿esta empresa tiene este modulo?" y recibia una respuesta
          que dependia de los permisos WEB de quien pregunta.

          Consecuencia medida: un usuario con el canal movil completo
          -`app_movil.ventas.consulta.ver` y compania- NO veia el mosaico, porque
          Consulta de ventas cuelga de `ventas` y el no tenia `ventas.ver_modulo`,
          que es del canal web. Lo mismo con Facturas, Productos, Clientes,
          Reportes y Almacen de cascos. El canal movil quedaba decorativo en el
          ultimo paso.

          QUE NO SE AFLOJA. Esto no concede nada: el recorte por usuario lo sigue
          haciendo la app, con el permiso de cada mosaico -que es donde ahora
          vive la llave del telefono- y sobre todo lo sigue haciendo CADA
          ENDPOINT, que valida su permiso aunque el mosaico se pinte. Lo unico
          que se sabe de mas es que modulos compro la empresa, que no es un dato
          de nadie en particular.

          `tenant` se queda igual para quien ya lo usa.
        */
        var soloEmpresa = string.Equals(rawScope, "empresa", StringComparison.OrdinalIgnoreCase);

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

        if (!includeAll && !soloEmpresa && !isSuperAdmin)
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
            scope = includeAll ? "all" : (soloEmpresa ? "empresa" : "tenant"),
            companyKey,
            modules
        });
    }
}
