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
    private readonly ILogger<SesionController> _logger;

    public SesionController(IUserService users, ILogger<SesionController> logger)
    {
        _users = users;
        _logger = logger;
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
        var canalResuelto = Canal.Web;

        if (request is not null)
        {
            var declarado = request.Canal;
            var clienteApp = Request.Headers[CanalDeAcceso.CabeceraClienteApp].ToString();
            var agente = Request.Headers.UserAgent.ToString();

            canalResuelto = CanalDeAcceso.Normalizar(declarado, clienteApp, agente);
            request.Canal = CanalDeAcceso.AClaim(canalResuelto);

            /*
              LA MEDICION QUE HACE FALTA PARA PODER RETIRAR EL RESPALDO.

              CanalDeAcceso dice que el User-Agent se podra borrar "cuando no
              quede ninguna sesion llegando sin X-Cliente-App". Sin esta linea
              esa semana limpia no se puede medir, y lo que no se puede medir
              no se retira nunca: el respaldo se quedaria para siempre por
              falta de un dato, no por falta de decision.

              Se registra SOLO cuando el respaldo fue el que decidio —ni el
              cuerpo ni la cabecera dijeron nada y el User-Agent mando al canal
              movil—, que es exactamente el caso que tiene que llegar a cero.
              Los logins normales no escriben nada: esto no es un log de
              accesos.

              Va el nombre de usuario para poder avisar al que le falta
              actualizar la app. NO va la contrasena ni el token, obviamente, y
              el User-Agent se recorta: es un dato que escribe el cliente y no
              tiene por que caber entero en el log.
            */
            var decidioElRespaldo =
                CanalDeAcceso.DeTexto(declarado) == Canal.NoDeclarado
                && CanalDeAcceso.DeClienteApp(clienteApp) is null
                && canalResuelto == Canal.Movil;

            if (decidioElRespaldo)
            {
                _logger.LogInformation(
                    "Canal movil resuelto por el RESPALDO del User-Agent (sin {Cabecera}): usuario={Usuario} agente={Agente}. "
                    + "Mientras esto siga apareciendo, el respaldo de CanalDeAcceso no se puede retirar.",
                    CanalDeAcceso.CabeceraClienteApp,
                    request.Usuario,
                    agente.Length > 120 ? agente[..120] : agente);
            }
        }

        var result = await _users.LoginAsync(request!, canalResuelto, ct);
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
