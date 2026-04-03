using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using ISL_Service.Application.Exceptions;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SqlException: {Message}", ex.Message);
            // RAISERROR con severity 16 suele usar error number 50001-99999
            var isUserError = ex.Number >= 50000 && ex.Number <= 99999;
            context.Response.StatusCode = isUserError ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = isUserError ? ex.Message : "Error de base de datos.", detail = ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Error interno.", detail = ex.Message }));
        }
    }

    private static bool IsConnectionStringError(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("Keyword not supported", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Connection string", StringComparison.OrdinalIgnoreCase);
    }
}
