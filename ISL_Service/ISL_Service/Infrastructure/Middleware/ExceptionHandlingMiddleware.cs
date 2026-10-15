using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using ISL_Service.Application.Exceptions;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Middleware;

/*
  EL `detail` CRUDO DEL MOTOR NO SALE EN PRODUCTION.

  Sintoma que se vio: con el backend corriendo en entorno Production, pedir el
  dashboard de ventas (cuyo sp_w_ no esta desplegado en Tauro) contestaba

      {"message":"Error de base de datos.",
       "detail":"Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasKpis'."}

  Es decir: el nombre del procedimiento. Con otros errores salen nombres de
  columna y, en fallos de red, el nombre del servidor y de la base. Eso es un
  mapa del esquema regalado a cualquiera que sepa provocar un 500.

  Que se hace: el `detail` tecnico solo se escribe cuando el entorno es
  Development. En Production el cliente recibe un mensaje generico mas un
  `traceId` (el TraceIdentifier de la peticion), y el mensaje completo del motor
  se registra del lado del servidor con ese mismo traceId para poder cruzarlo.

  Que NO se toca:
  - `AppException.Detail` si sale siempre: lo redacta el propio backend para el
    usuario (p. ej. "falta el script sp_w_ConsultaAgenteUsuario"), no viene del
    motor.
  - Los `RAISERROR` de negocio (numeros 50000-99999) siguen llegando al usuario
    como 400 con su texto: son mensajes escritos a proposito en los SP.
*/
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, "AppException: {Message}", ex.Message);
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = ex.Code,
                message = ex.Message,
                detail = ex.Detail
            }));
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning(ex, "AuthenticationException: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "UnauthorizedAccessException: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "KeyNotFoundException: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (ArgumentException ex)
        {
            if (IsConnectionStringError(ex))
            {
                _logger.LogError(ex, "Connection string error: {Message}", ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    message = "Error de configuraci\u00f3n. Comun\u00edcate con soporte."
                }));
                return;
            }

            _logger.LogWarning(ex, "ArgumentException: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        /*
          EL CLIENTE SE FUE. NO ES UN ERROR.

          Tiene que ir ANTES de las ramas de SqlException y Exception, porque la
          cancelacion llega disfrazada de las dos.

          Cuando el agente cierra la app o pierde la señal a media peticion,
          ASP.NET Core dispara RequestAborted. Si la espera estaba en el `await`,
          sale una OperationCanceledException. Pero si estaba leyendo el
          resultado, SqlClient levanta una SqlException con "Operation cancelled
          by user" y Number = 0, y esa caia en la rama de abajo: se anotaba
          LogError como si la base hubiera fallado y se contestaba 500 a alguien
          que ya no estaba escuchando.

          El resultado practico era que el visor de eventos se llenaba de
          errores de base que no eran de base, y encontrar las causas reales
          obligaba a filtrarlos a mano.

          499 es lo que usa todo el mundo para "el cliente cerro". Nadie lo lee
          -no hay cliente-, pero deja el registro honesto.
        */
        catch (Exception ex) when (EsClienteQueSeFue(ex, context))
        {
            _logger.LogInformation(
                "La peticion se cancelo porque el cliente se fue: {Ruta}", context.Request.Path);

            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (SqlException ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "SqlException (traceId {TraceId}): {Message}", traceId, ex.Message);
            // RAISERROR con severity 16 suele usar error number 50001-99999
            var isUserError = ex.Number >= 50000 && ex.Number <= 99999;
            context.Response.StatusCode = isUserError ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = isUserError ? ex.Message : "Error de base de datos.",
                detail = DetalleTecnico(ex, isUserError),
                traceId
            }));
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception (traceId {TraceId}): {Message}", traceId, ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = "Error interno.",
                detail = DetalleTecnico(ex, false),
                traceId
            }));
        }
    }

    /// <summary>
    /// El mensaje del motor solo viaja al cliente en Development. En cualquier
    /// otro entorno devuelve null (y `detail` sale null en el JSON), salvo que el
    /// propio mensaje ya se este entregando como texto de negocio (RAISERROR).
    /// </summary>
    private string? DetalleTecnico(Exception ex, bool yaEsMensajeDeNegocio)
    {
        if (yaEsMensajeDeNegocio) return ex.Message;
        return _env.IsDevelopment() ? ex.Message : null;
    }

    /*
      ¿ES ESTO "EL CLIENTE SE FUE" O UN ERROR DE VERDAD?

      Se exige que el token de la peticion este disparado. Sin esa condicion,
      una SqlException con Number = 0 por otra causa -un corte de red con la
      base, por ejemplo- se escondería como si fuera una cancelacion, y eso si
      seria tapar un problema.
    */
    private static bool EsClienteQueSeFue(Exception ex, HttpContext context)
    {
        if (!context.RequestAborted.IsCancellationRequested)
            return false;

        if (ex is OperationCanceledException)
            return true;

        // El disfraz de SqlClient cuando le cancelan a media lectura.
        return ex is SqlException sql
            && sql.Number == 0
            && (sql.Message ?? string.Empty).Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConnectionStringError(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("Keyword not supported", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Connection string", StringComparison.OrdinalIgnoreCase);
    }
}
