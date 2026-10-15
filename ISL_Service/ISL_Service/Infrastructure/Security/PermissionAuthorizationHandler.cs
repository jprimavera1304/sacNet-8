using System.Security.Claims;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace ISL_Service.Infrastructure.Security;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        var rolLegacy = context.User.FindFirstValue("rolLegacy")
            ?? context.User.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty;

        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        var sub = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var empresaIdRaw = context.User.FindFirstValue("empresaId");
        if (!Guid.TryParse(sub, out var userId) || !int.TryParse(empresaIdRaw, out var empresaId))
            return;

        var snapshot = await _permissionService.GetPermissionsAsync(
            userId,
            empresaId,
            rolLegacy,
            CancellationToken.None);

        // Con cualquiera de los permisos aceptados alcanza (ver PermissionRequirement).
        if (snapshot.PermissionsEnabled)
        {
            bool Tiene(string clave) =>
                snapshot.Permissions.Any(p => string.Equals(p, clave, StringComparison.OrdinalIgnoreCase));

            /*
              "modulo:<clave>": PUEDE ABRIR LA PANTALLA DEL MODULO.

              No es un permiso: es la pregunta "en Mac31 esta forma se le
              abriria". Alla el permiso no decide si la pantalla aparece, decide
              si el BOTON se puede pulsar; quien tiene NUEVO y no tiene VER
              entra a la forma y ve el boton VER en gris. El web usaba la clave
              de lectura como candado de la pantalla entera y dejaba fuera a esa
              misma persona: medido el 2026-10-12, le pasaba a XOCHITL y JAZMIN
              con proveedores y a NAOLANY con productos, entre otras.

              SE EXIGEN LAS DOS COSAS, la entrada al modulo y algun permiso de
              trabajo suyo. Solo con la entrada entraria DEMO, que tiene las 34
              puertas de plataforma y ni un permiso de Mac31.

              Y NO CONCEDE LA CLAVE DE LECTURA: las rutas de detalle y las de
              escritura siguen pidiendo la suya, como los botones grises.
            */
            bool PuedeAbrirElModulo(string clave)
            {
                if (!Tiene($"{clave}.ver_modulo")) return false;
                var familia = clave + ".";
                return snapshot.Permissions.Any(p =>
                    !p.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase)
                    && p.StartsWith(familia, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var required in requirement.Permissions)
            {
                if (required.StartsWith("modulo:", StringComparison.OrdinalIgnoreCase))
                {
                    if (PuedeAbrirElModulo(required.Substring("modulo:".Length)))
                    {
                        context.Succeed(requirement);
                        return;
                    }

                    continue;
                }

                // SIN RAMPA: la clave pedida se tiene o no se tiene.
                //
                // Aqui habia un CanUseLegacyVerAsModuleView: si el endpoint
                // pedia "X.ver_modulo" y la persona tenia "X.ver", pasaba. Se
                // quito junto con su gemela de EntradasDeModulo.PuedeEntrar —
                // las dos en el mismo cambio, porque cerrar una sola dejaba el
                // modulo abierto por el otro camino de la API.
                //
                // Nadie pierde acceso: cuando Mac31 concede el ".ver" de una
                // familia puenteada, el snapshot ya trae su ".ver_modulo"
                // derivado (ver PermissionService.FamiliasLegacy.cs). La rampa
                // solo seguia tapando el caso que se queria cerrar: un permiso
                // fino suelto, sin entrada al modulo, en familias como
                // "usuarios." que nunca fueron de legacy.
                var tieneLaClave = Tiene(required);

                // ADEMAS del permiso fino, la ENTRADA al modulo.
                //
                // La regla es "entrada al modulo + permiso de la operacion", y
                // hasta ahora esta mitad solo la aplicaba el front. Se pone
                // aqui Y en PermisoControllerBase porque la API tiene dos
                // caminos para decidir lo mismo: si solo se cerrara uno, el
                // modulo quedaria abierto por el otro y nadie lo notaria.
                if (tieneLaClave && EntradasDeModulo.PuedeEntrar(required, Tiene))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
            return;
        }

        if (requirement.Permissions.Any(required => _permissionService.IsAllowedByLegacy(rolLegacy, required)))
            context.Succeed(requirement);
    }
}
