using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Empresa;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IUserAdminService _userAdminService;
    private readonly IEmpresaRepository _empresas;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICapacidadesEmpresaService _capacidadesEmpresa;

    public MeController(
        IUserRepository users,
        IUserAdminService userAdminService,
        IEmpresaRepository empresas,
        IPermissionService permissionService,
        ICurrentUserAccessor currentUserAccessor,
        ICapacidadesEmpresaService capacidadesEmpresa)
    {
        _users = users;
        _userAdminService = userAdminService;
        _empresas = empresas;
        _permissionService = permissionService;
        _currentUserAccessor = currentUserAccessor;
        _capacidadesEmpresa = capacidadesEmpresa;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = _currentUserAccessor.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { message = "Token invalido." });

        var user = await _users.GetByIdAsync(userId.Value, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        var permissionSnapshot = await _permissionService.GetPermissionsAsync(
            user.Id,
            user.EmpresaId,
            user.Rol,
            ct);

        var companyKey = await _empresas.GetCompanyKeyAsync(ct);

        /*
          LAS CAPACIDADES DE LA EMPRESA VIAJAN AQUI.

          Sin esto el front no sabe en que empresa esta parado y una pantalla nueva
          solo puede implementar una de las dos: Legacy ramifica en 430 sitios por
          seis valores de dbo.Constantes y ninguno llegaba al web ni al movil.

          Va dentro de /api/me porque es la llamada que los dos fronts ya hacen al
          autenticar y la que ya cachean; un endpoint propio habria sumado un tercer
          round-trip al arranque.

          Si la lectura falla, /api/me NO se cae: el usuario entra, el campo queda en
          null y el front lo trata como "todavia no se pueden dibujar reglas por
          empresa". Dejar sin sesion a todo el mundo porque dbo.Constantes no
          contesta seria peor que entrar con menos informacion.
        */
        CapacidadesEmpresaResponse? capacidades = null;
        try
        {
            capacidades = await _capacidadesEmpresa.ConsultarAsync(
                _currentUserAccessor.GetLegacyUserId(User), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            capacidades = null;
        }

        var response = new MeResponse
        {
            UserId = user.Id,
            IdUsuario = _currentUserAccessor.GetLegacyUserId(User),
            Id = user.Id,
            Usuario = user.UsuarioNombre,
            RolLegacy = user.Rol,
            Rol = user.Rol,
            EmpresaId = user.EmpresaId,
            PermissionsEnabled = permissionSnapshot.PermissionsEnabled,

            /*
              ESTO SE INFORMA **SIN FILTRAR POR CANAL**, Y ES DELIBERADO POR
              AHORA. NO ES UN OLVIDO. Si has llegado aqui buscando por que el
              movil y el web reciben la misma lista, esta es la respuesta.

              LO QUE PASA
              -----------
              Un token de canal WEB recibe aqui tambien sus claves
              `app_movil.*`. Medido el 2026-10-15 con el usuario DEMO por los
              dos canales: 398 permisos y 43 claves `app_movil.*` en los dos
              casos, identicos.

              POR QUE NO ES UN AGUJERO
              ------------------------
              Porque informar no es conceder, y el COBRO si filtra. Lo hacen
              los dos caminos que deciden, y los dos usan
              `CanalDeAcceso.Filtrar`:

                * `PermisoControllerBase.LeerPermisosAsync` — todos los
                  controladores que heredan de ahi;
                * `PermissionAuthorizationHandler` — todo lo que va por
                  `[Authorize(Policy = "perm:...")]`.

              O sea: quien presente una clave `app_movil.*` desde un token de
              canal web recibe 403 hoy, le diga esta respuesta lo que le diga.
              Hay pruebas de las dos mitades en `CanalDelTokenTests` y en
              `IntrusionSieteFuncionesMovilTests`.

              CUAL ES LA MEJORA PENDIENTE
              ---------------------------
              Filtrar tambien AL INFORMAR, aqui, con el mismo
              `CanalDeAcceso.Filtrar`. La razon no es seguridad: es que hoy la
              coherencia la impone una segunda verdad mantenida A MANO EN EL
              FRONT —`saleEnWeb()` en `nucleo/permisosWeb.ts`, que excluye el
              modulo `app_movil` entero— y mientras eso siga asi, un cliente
              de canal web que decida que dibujar leyendo esta lista pintaria
              botones muertos: botones que el servidor no va a honrar. Que el
              servidor informe lo mismo que cobra es lo correcto, y "informar
              una cosa y cobrar otra" es exactamente el patron que causo los
              fallos de botones de esta entrega (el de
              `app_movil.ventas.pedidos.modificar` el ultimo).

              POR QUE NO SE HIZO HOY: POR LA FECHA, NO POR EL CRITERIO
              --------------------------------------------------------
              Se decidio el 2026-10-15, a un paso de publicar la app. Cambiar
              lo que informa este endpoint mueve lo que dibujan LOS DOS
              fronts, el front del web lo estaba cambiando otro agente en
              paralelo, y la ganancia observable hoy es CERO: el movil no se
              entera (`Filtrar` solo recorta en canal web) y el web ya excluye
              `app_movil` por su cuenta. Aceptar un riesgo real por una mejora
              que no se ve, el dia del despliegue, no sale a cuenta.

              Fuera de esa ventana, el criterio es filtrar. Quien lo haga:
              envuelve este valor en `CanalDeAcceso.Filtrar(..., CanalDeAcceso.De(User))`
              y deja una prueba de que el mismo usuario por los dos canales
              recibe listas DISTINTAS y que la del movil es identica a la de
              hoy.
            */
            Permissions = permissionSnapshot.Permissions,
            PermissionsVersion = permissionSnapshot.PermissionsVersion,
            CompanyKey = companyKey,
            DebeCambiarContrasena = user.DebeCambiarContrasena,
            Empresa = capacidades
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
