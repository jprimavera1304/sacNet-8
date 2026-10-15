using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Infrastructure.Middleware;

/*
  LA LLAVE MAESTRA DE LA APP LA APLICABA FLUTTER, NO EL SERVIDOR.

  QUE PASABA
  ----------
  `app_movil.acceso` es la llave de entrada a la app movil: sin ella no se
  entra. Pero el UNICO sitio que la miraba era `lib/app/mobile_access_gate.dart`
  —una pantalla de Flutter—. Medido el 2026-10-15 con un grep de
  "app_movil.acceso" sobre todo el backend: cero resultados en Controllers/,
  Infrastructure/ y Application/. Ni un `[Authorize]` la nombra.

  O sea: a quien le quitaras la llave maestra se le cerraba la APP, no la API.
  Con su token -que el servidor le sigue dando, porque sus credenciales son
  validas- podia llamar a los endpoints a mano y seguir trabajando: capturar
  pedidos, consultar clientes, mover cascos. Ocultar no es cerrar.

  QUE HACE ESTO
  -------------
  Si el token es de CANAL MOVIL (el claim `canal`, firmado por el servidor al
  iniciar sesion; ver CanalDeAcceso), exige `app_movil.acceso` para TODO lo que
  no este en la lista de abajo. Sin ella: 403.

  POR QUE UN MIDDLEWARE Y NO SOLO LAS POLITICAS
  ---------------------------------------------
  Porque la llave maestra no es de un modulo, es de la puerta. Muchos endpoints
  llevan `[Authorize]` a secas, sin `perm:`, y esos no pasan por
  PermissionAuthorizationHandler: cerrar solo las politicas dejaria la mitad de
  la API abierta para un token movil sin llave. Se hace EN LOS DOS SITIOS a
  proposito (el handler tambien la cobra), por la misma razon que ya obligo a
  duplicar la entrada de modulo: la API tiene dos caminos para decidir lo mismo
  y cerrar uno solo deja la puerta abierta por el otro sin que nadie lo note.

  LO QUE SE DEJA PASAR, Y POR QUE
  -------------------------------
  Las rutas de SESION e IDENTIDAD. La app necesita poder entrar (login),
  renovar, salir y leer /api/me con un token sin llave maestra: de /api/me sale
  justamente la lista de permisos con la que el gate pinta "Tu usuario no tiene
  el permiso de acceso a la aplicacion movil. Pide a tu administrador que te lo
  asigne." Si esto tambien contestara 403, la app caeria en "No se pudo
  verificar tu acceso" con un boton de Reintentar que no arregla nada — el
  mismo callejon sin salida que ya se corrigio una vez en mobile_access_gate.
  /api/me no deja HACER nada: es lectura de quien eres.

  Tambien pasan las rutas de version/actualizacion, que la app consulta antes de
  tener sesion util y que no exponen datos del negocio.

  DOS CASOS QUE NO SE CIERRAN, A PROPOSITO
  ----------------------------------------
  - SuperAdmin pasa, como en todo el resto de la API (PermissionAuthorizationHandler,
    PermisoControllerBase, y el front). Si no, el switch de "Inicio de
    repartidor" del perfil superadmin dejaria de funcionar.
  - Instalacion sin modelo de permisos (`PermissionsEnabled = false`): pasa. Ahi
    no existe ninguna clave `app_movil.*` contra la que comparar, asi que exigir
    la llave maestra dejaria la app muerta para toda la base. Es la misma regla
    que el resto del modelo: lo que no se puede comprobar no se cierra.
*/
public class AccesoMovilMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccesoMovilMiddleware> _logger;

    public AccesoMovilMiddleware(RequestDelegate next, ILogger<AccesoMovilMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Rutas que un token de canal movil puede usar SIN la llave maestra.
    /// Se compara por prefijo, en minusculas.
    /// </summary>
    private static readonly string[] RutasDeSesion =
    {
        "/api/auth/",            // login, refresh, logout
        "/api/sesion/",          // los mismos, con el otro nombre de ruta
        "/api/me",               // quien soy + mis permisos -> lo que lee el gate
        "/api/appversion",       // version minima de la app
        "/api/actualizaciones"   // descarga/aviso de actualizacion
    };

    public async Task Invoke(
        HttpContext context,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
    {
        // Lo anonimo pasa de largo: ahi no hay canal que comprobar (y el login
        // tiene que poder llegar para que exista un token).
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var canal = CanalDeAcceso.De(context.User);
        if (!CanalDeAcceso.ExigeLlaveMaestra(canal))
        {
            // Canal web, o token de antes de este cambio (NoDeclarado): se
            // comporta exactamente como ayer. Lo que el canal web pierde -las
            // claves `app_movil.*`- se lo quita el filtro de
            // PermissionAuthorizationHandler, no esta puerta.
            await _next(context);
            return;
        }

        if (EsRutaDeSesion(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (Application.Security.CurrentUser.IsSuperAdmin(context.User))
        {
            await _next(context);
            return;
        }

        var userId = currentUser.GetUserId(context.User);
        if (userId is null)
        {
            // Token autenticado del que no se sabe quien es: no se puede
            // resolver su juego de permisos, asi que no pasa. CompanyKeyMiddleware
            // ya rechaza antes los tokens que no salieron de aqui.
            await ResponderAsync(context, StatusCodes.Status401Unauthorized,
                "Token sin usuario. Vuelve a iniciar sesion.");
            return;
        }

        var empresaId = currentUser.GetCompanyId(context.User) ?? 0;
        var rol = currentUser.GetRole(context.User);

        // El snapshot esta cacheado en memoria por usuario (PermissionService),
        // y la politica del endpoint lo va a pedir igual un momento despues: no
        // son dos viajes a SQL, es el mismo renglon de cache leido dos veces.
        var snapshot = await permissionService.GetPermissionsAsync(userId.Value, empresaId, rol, context.RequestAborted);

        if (!snapshot.PermissionsEnabled)
        {
            await _next(context);
            return;
        }

        if (!CanalDeAcceso.TieneLlaveMaestra(snapshot.Permissions))
        {
            _logger.LogWarning(
                "Token de canal movil SIN {Llave}: usuario {UserId} (empresa {EmpresaId}) intento {Metodo} {Ruta}. Se rechaza con 403.",
                CanalDeAcceso.LlaveMaestraMovil, userId, empresaId, context.Request.Method, context.Request.Path);

            await ResponderAsync(context, StatusCodes.Status403Forbidden,
                "Tu usuario no tiene permiso de acceso a la aplicacion movil.");
            return;
        }

        await _next(context);
    }

    private static bool EsRutaDeSesion(PathString ruta)
    {
        var valor = ruta.HasValue ? ruta.Value! : string.Empty;
        return RutasDeSesion.Any(r => valor.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    private static Task ResponderAsync(HttpContext context, int statusCode, string mensaje)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { ok = false, message = mensaje }));
    }
}
