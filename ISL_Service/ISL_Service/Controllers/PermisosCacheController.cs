using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// TIRAR LA CACHE DE PERMISOS DE UNA PERSONA, SIN REINICIAR EL POOL.
/// </summary>
/// <remarks>
/// EL DIA QUE LO HIZO FALTA
/// ------------------------
/// El 2026-11-04 se concedieron permisos con `INSERT` directo en
/// `WUsuarioPermiso`, porque la pantalla de permisos no podia hacerlo. La
/// invalidacion de la cache vivia unicamente al final de
/// `UpsertUserOverridesAsync` —dentro del camino de escritura del boton
/// Guardar—, asi que el backend siguio sirviendo su copia en memoria y hubo que
/// REINICIAR `pool-tauro` CUATRO VECES.
///
/// Reiniciar el pool arregla un permiso tirando todas las sesiones de la
/// empresa, vaciando todas las caches de la aplicacion y haciendo que el
/// siguiente que entre pague el arranque en frio. Es un mazo para un tornillo,
/// y encima en produccion.
///
/// POR QUE UN CONTROLADOR APARTE Y NO DOS METODOS EN PermisosWebController
/// -----------------------------------------------------------------------
/// Mismo criterio que `CopiaDeMac31Controller`: son operaciones de
/// MANTENIMIENTO, no de la pantalla de permisos. No leen ni escriben un solo
/// permiso —solo olvidan lo que el proceso recordaba—, y mezclarlas entre los
/// nueve endpoints de esa pantalla hace mas dificil ver que aqui no se concede
/// nada.
///
/// POR QUE EL PERMISO ES EL DE EDITAR AJUSTES Y NO UNO NUEVO
/// ---------------------------------------------------------
/// `permisosweb.overrides.editar` (y su pareja del movil) es la llave de quien
/// reparte permisos. Quien puede CONCEDER un permiso puede, con mas razon,
/// pedirle al servidor que relea lo que acaba de conceder: tirar una cache no
/// da acceso a nada ni ensena ningun dato.
///
/// Una llave propia —`permisosweb.cache.tirar`— sonaba mas fino y es peor:
/// habria que sembrarla, repartirla persona por persona, y hasta que eso
/// pasara el boton estaria apagado JUSTO para quien tiene el problema. Es el
/// mismo razonamiento que ya se escribio en `CopiaDeMac31Controller`.
///
/// LO QUE NO SE PUEDE PROMETER, Y SE DICE AQUI PARA QUE NADIE LO ADIVINE
/// --------------------------------------------------------------------
/// 1. ALCANZA A **ESTE** PROCESO. La cache es la memoria del proceso que
///    atiende. Con un pool de un solo proceso —como esta hoy produccion— eso es
///    todo el backend; si algun dia hay varios, a los demas los pone al dia el
///    latido (PermissionService.CacheAlDia.cs), que cada proceso mide solo.
/// 2. LA APP MOVIL PINTA CON LOS PERMISOS DE LA SESION ANTERIOR. Guarda el
///    cuerpo de `/api/me` y solo lo vuelve a pedir AL ENTRAR. Tirar la cache del
///    servidor no repinta el telefono: la persona tiene que CERRAR SESION Y
///    VOLVER A ENTRAR. Forzar el cierre de la app no basta. Esto ya hizo creer
///    tres veces en una tarde que un permiso no habia llegado cuando si.
/// 3. NO PROTEGE DEL GUARDAR DE LA PANTALLA. Ese boton REEMPLAZA los ajustes de
///    la persona por lo que la pantalla tenga marcado, asi que una fila metida
///    a mano que la pantalla no ensena se borra al siguiente guardado. Eso se
///    arregla dandole su casilla a la clave, no tirando caches.
/// </remarks>
[ApiController]
[Route("api/permisos-web/cache")]
[Authorize]
public class PermisosCacheController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<PermisosCacheController> _logger;

    public PermisosCacheController(
        IPermissionService permissions,
        ICurrentUserAccessor currentUser,
        ILogger<PermisosCacheController> logger)
    {
        _permissions = permissions;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Olvida los permisos que este proceso recordaba de UNA persona. La
    /// siguiente peticion suya los vuelve a calcular contra la base.
    /// </summary>
    [HttpPost("usuarios/{userId:guid}")]
    [HttpPost("/api/capacidades/cache/usuarios/{userId:guid}")]
    [Authorize(Policy = "perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar")]
    public IActionResult TirarDeUsuario([FromRoute] Guid userId)
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { ok = false, message = "Token invalido." });

        if (userId == Guid.Empty)
            return UnprocessableEntity(new { ok = false, message = "Falta el usuario." });

        _permissions.InvalidarCacheDeUsuario(empresaId, userId);

        /*
          SE AUDITA SIEMPRE, Y CON QUIEN LO PIDIO DELANTE.

          No porque tirar una cache sea peligroso —no lo es—, sino porque esto
          se usa en medio de un incidente: la pregunta que viene despues es
          siempre "¿y a que hora se hizo?". El dia 4 de noviembre esa pregunta
          se contesto con los reinicios del pool apuntados a mano en una tabla.
          Ahora queda en el registro del backend, con hora, empresa, usuario
          afectado y quien lo pidio.
        */
        _logger.LogInformation(
            "Permisos: cache tirada a mano. empresaId={EmpresaId} usuarioAfectado={UserId} loPidio={Quien}",
            empresaId, userId, _currentUser.GetUsername(User));

        return Ok(new
        {
            ok = true,
            empresaId,
            userId,
            /* El aviso viaja en la respuesta porque es la mitad que nadie
               adivina: sin volver a entrar, el telefono sigue pintando con los
               permisos de la sesion anterior. */
            aviso = "Pídele que cierre sesión y vuelva a entrar: la app guarda los permisos de la última sesión."
        });
    }

    /// <summary>
    /// Olvida los permisos de TODA la empresa. Para cuando se toco la base de
    /// varias personas de golpe: un script de reparto, un respaldo repuesto.
    /// </summary>
    /// <remarks>
    /// ES EL MARTILLO GRANDE Y SE OFRECE APARTE A PROPOSITO. Todos los usuarios
    /// activos recalculan sus permisos en su siguiente peticion, y calcular un
    /// snapshot ha llegado a tardar segundos: hacerlo para la empresa entera a
    /// la hora de mas trabajo es exactamente la estampida que ya se midio en
    /// produccion. Si se sabe a quien se le toco, se usa el de arriba.
    ///
    /// Sigue siendo MUCHO mas suave que reiniciar el pool: no tira sesiones, no
    /// vacia las demas caches de la aplicacion y no corta ninguna peticion en
    /// vuelo.
    /// </remarks>
    [HttpPost]
    [HttpPost("/api/capacidades/cache")]
    [Authorize(Policy = "perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar")]
    public IActionResult TirarDeLaEmpresa()
    {
        if (!TryGetEmpresaId(out var empresaId))
            return Unauthorized(new { ok = false, message = "Token invalido." });

        _permissions.InvalidarCacheDeEmpresa(empresaId);

        _logger.LogInformation(
            "Permisos: cache de la EMPRESA tirada a mano. empresaId={EmpresaId} loPidio={Quien}",
            empresaId, _currentUser.GetUsername(User));

        return Ok(new
        {
            ok = true,
            empresaId,
            aviso = "Quien tenga sesión abierta en la app seguirá viendo los permisos de su última sesión hasta volver a entrar."
        });
    }

    private bool TryGetEmpresaId(out int empresaId)
    {
        var value = _currentUser.GetCompanyId(User);
        empresaId = value ?? 0;
        return value is not null;
    }
}
