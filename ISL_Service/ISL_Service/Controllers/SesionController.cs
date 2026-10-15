using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
public class SesionController : ControllerBase
{
    private readonly IUserService _users;

    public SesionController(IUserService users)
    {
        _users = users;
    }

    // RUTA ABSOLUTA (no choca con api/[controller])
    [HttpPost("/api/auth/login")]
    [HttpPost("/api/sesion/login")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        /*
          EL CANAL SE RESUELVE AQUI PORQUE AQUI ESTA EL USER-AGENT.

          El canal del token (ver Application/Security/CanalDeAcceso.cs) sale de
          lo que el cliente declare en `canal`; cuando no declara nada —la app
          que ya esta instalada en los telefonos no lo manda— se cae a una
          heuristica sobre el User-Agent, y ese dato solo se ve desde el
          controlador.

          Se deja YA NORMALIZADO en el request ("movil" o "web") para que el
          servicio no tenga que conocer la peticion HTTP y para que el valor que
          se firma sea siempre una de las dos constantes, nunca el texto que
          mando el cliente.

          No se valida aqui la llave maestra: el token se emite igual, y el
          candado esta en AccesoMovilMiddleware. Hace falta que se emita para
          que la app pueda leer /api/me y pintar "Sin acceso a la app" en vez de
          un error de red.
        */
        if (request is not null)
        {
            var canal = CanalDeAcceso.Normalizar(
                request.Canal,
                agenteDeUsuario: Request.Headers.UserAgent.ToString());
            request.Canal = CanalDeAcceso.AClaim(canal);
        }

        var result = await _users.LoginAsync(request!, ct);
        return Ok(result);
    }

    // Canjea el refresh token por un nuevo access token (solo app movil).
    [HttpPost("/api/auth/refresh")]
    [HttpPost("/api/sesion/refresh")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _users.RefreshAsync(request?.RefreshToken ?? string.Empty, ct);
            return Ok(result);
        }
        catch (System.Security.Authentication.AuthenticationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // Revoca el refresh token (logout de la app).
    [HttpPost("/api/auth/logout")]
    [HttpPost("/api/sesion/logout")]
    [Consumes("application/json")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _users.LogoutAsync(request?.RefreshToken ?? string.Empty, ct);
        return Ok(new { ok = true });
    }
}
