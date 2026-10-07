using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Middleware;

/*
  UN TOKEN DE UNA EMPRESA VALIA EN OTRA.

  QUE PASABA
  ----------
  El token trae un claim `companyKey` con la clave de la instalacion que lo
  emitio (JwtTokenGenerator.cs). Ese claim NO se comparaba contra nada en el
  pipeline: su unico consumidor era ModulosController, y solo para armar la
  lista de modulos. Ni PermisoControllerBase ni el handler de autorizacion lo
  miraban.

  La firma, el emisor y la audiencia que SI se validan (Program.cs) son las tres
  constantes COMPARTIDAS: salen de la misma seccion Jwt de appsettings y, salvo
  que Azure las sobreescriba con un valor distinto por sitio, son iguales en los
  cuatro despliegues. Resultado: un token emitido al entrar en Tauro era un token
  perfectamente valido para pedirle datos a Zaragoza. Y como el rol viaja en el
  token, el `sub` ni se comprobaba contra la base de destino.

  Es decir: el aislamiento entre empresas era una convencion de despliegue, no un
  control.

  QUE HACE ESTO
  -------------
  Compara el `companyKey` del token con el de ESTA instalacion
  (dbo.EmpresaWeb(Id=1).Clave, lo mismo que se usa al emitirlo) y, si no cuadran,
  contesta 401 y no deja pasar la peticion. Se registra del lado del servidor con
  las dos claves, porque un choque aqui casi siempre significa una de dos cosas:
  o alguien esta reusando un token de otra instalacion, o un despliegue quedo
  apuntando a la base equivocada. Las dos hay que verlas.

  LO QUE NO HACE, Y HAY QUE DECIRLO
  ---------------------------------
  No arregla que `Jwt:Key` este en claro en el repo e igual para todos. Eso no se
  cierra con codigo: se cierra ROTANDO la llave y poniendo una distinta por
  despliegue, y es del usuario. Con este candado puesto, aun con la llave
  compartida, un token ajeno ya no sirve: se rechaza por empresa.

  DECISIONES
  ----------
  - Solo mira peticiones AUTENTICADAS. Lo anonimo a proposito -el PDF de la
    remision, el login- no trae token, asi que pasa de largo: ahi lo que autoriza
    es el pase firmado y eso no se toca.
  - Token SIN el claim: se rechaza. El generador no emite ninguno sin el (lanza
    si viene vacio), asi que un token sin claim es un token que no salio de aqui.
  - Si no se puede resolver la clave de la instalacion (la base no contesta), se
    FALLA CERRADO con 503. Un candado de aislamiento que se abre cuando la base
    se cae no es un candado; y sin base esta API no puede contestar nada util de
    todas formas.
  - La clave de la instalacion se cachea 5 minutos, y en la MISMA entrada
    "companyKey" que ya usa UserService al emitir el token: asi las dos mitades
    -la que firma y la que comprueba- no pueden opinar distinto.
*/
public class CompanyKeyMiddleware
{
    /// <summary>La misma entrada de cache que usa UserService al emitir el token.</summary>
    private const string CacheKey = "companyKey";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly ILogger<CompanyKeyMiddleware> _logger;

    public CompanyKeyMiddleware(RequestDelegate next, ILogger<CompanyKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(
        HttpContext context,
        ICurrentUserAccessor currentUser,
        IEmpresaRepository empresas,
        IMemoryCache cache)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var claveDelToken = currentUser.GetCompanyKey(context.User);
        if (string.IsNullOrWhiteSpace(claveDelToken))
        {
            _logger.LogWarning(
                "Token autenticado SIN claim companyKey en {Ruta}. No lo emitio este backend; se rechaza.",
                context.Request.Path);
            await ResponderAsync(context, StatusCodes.Status401Unauthorized,
                "Token sin empresa. Vuelve a iniciar sesion.");
            return;
        }

        string claveDeLaInstalacion;
        try
        {
            claveDeLaInstalacion = await ClaveDeLaInstalacionAsync(empresas, cache, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudo resolver la empresa de esta instalacion, asi que NO se puede comprobar el aislamiento del token. Se rechaza la peticion a {Ruta}.",
                context.Request.Path);
            await ResponderAsync(context, StatusCodes.Status503ServiceUnavailable,
                "No se puede verificar la empresa de esta instalacion. Intenta de nuevo.");
            return;
        }

        if (!string.Equals(claveDelToken, claveDeLaInstalacion, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Token de otra empresa: el token dice companyKey={ClaveDelToken} y esta instalacion es {ClaveDeLaInstalacion}. Peticion a {Ruta} rechazada.",
                claveDelToken, claveDeLaInstalacion, context.Request.Path);
            await ResponderAsync(context, StatusCodes.Status401Unauthorized,
                "Tu sesion es de otra empresa. Inicia sesion en esta.");
            return;
        }

        await _next(context);
    }

    private static async Task<string> ClaveDeLaInstalacionAsync(
        IEmpresaRepository empresas, IMemoryCache cache, CancellationToken ct)
    {
        if (cache.TryGetValue<string>(CacheKey, out var cacheada) && !string.IsNullOrWhiteSpace(cacheada))
            return cacheada!;

        var clave = await empresas.GetCompanyKeyAsync(ct);
        cache.Set(CacheKey, clave, CacheTtl);
        return clave;
    }

    private static Task ResponderAsync(HttpContext context, int statusCode, string mensaje)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { ok = false, message = mensaje }));
    }
}
