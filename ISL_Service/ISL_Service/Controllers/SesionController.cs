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
          EL CANAL SE RESUELVE AQUI PORQUE AQUI ESTAN LAS CABECERAS.

          El canal del token (ver Application/Security/CanalDeAcceso.cs) se
          resuelve con tres senales, en este orden:

            1. `canal` en el cuerpo del login — lo que el cliente declara.
            2. `X-Cliente-App` — el identificador propio de la app
               ("mac-movil/<version>"). Es el mecanismo bueno: un contrato
               explicito, con la lista de valores fijada en el servidor.
            3. El User-Agent — RESPALDO, y solo eso: existe porque los APK ya
               instalados no mandan ninguna de las dos primeras y pasarlos a
               canal web les quitaria todas sus claves `app_movil.*`.

          Las tres son datos que el cliente escribe, y las tres son seguras por
          la misma razon: declarar un canal nunca da permisos, solo los quita.

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
                clienteApp: Request.Headers[CanalDeAcceso.CabeceraClienteApp].ToString(),
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
