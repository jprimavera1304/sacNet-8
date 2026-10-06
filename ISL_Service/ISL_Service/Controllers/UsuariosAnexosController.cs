using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Las tres lecturas sueltas de la pantalla de usuarios de Mac31: el inventario
/// de botones con el acceso de un usuario, las fechas de operacion que tiene
/// abiertas, y el siguiente numero de usuario libre.
///
/// POR QUE UN CONTROLADOR APARTE Y NO DENTRO DE UsuariosController
/// ---------------------------------------------------------------
/// `UsuariosController` es el CRUD de `dbo.UsuarioWeb` y uno de sus endpoints
/// devuelve contrasenas en claro; por eso tiene su propia comprobacion de
/// permisos a mano, mas estricta que la del resto (cuando el tenant no tiene el
/// modelo de permisos, solo deja pasar a Admin y SuperAdmin). Esto otro es
/// lectura de tablas de Mac31 y no tiene nada que ver con esa decision. Mezclarlo
/// obligaria a que la regla de las contrasenas aplicara tambien aqui, o —peor—
/// invitaria a relajarla.
///
/// SOLO LECTURA: `sp_n_ActualizarUsuarioFecha` y
/// `sp_n_ActualizarUsuarioFormaProceso` existen. El segundo lo llama ya
/// PermissionService al guardar permisos desde el web, y ahi se queda: dos
/// caminos para escribir los permisos de Mac31 es como no tener ninguno.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosAnexosController : ControllerBase
{
    private readonly IUsuariosAnexosService _service;

    public UsuariosAnexosController(IUsuariosAnexosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Los botones (procesos) de las formas de Mac31 y si este usuario los tiene.
    /// </summary>
    /// <param name="idUsuario">
    /// IDUsuario de `dbo.Usuarios` (el de Mac31, no el Guid del web). 0 devuelve
    /// el catalogo completo con `acceso` en false, que es como se pregunta "que
    /// botones existen".
    /// </param>
    /// <remarks>
    /// EL PERMISO ES EL DE ADMINISTRAR PERMISOS, no `usuarios.ver`. Esto es el
    /// mapa de seguridad de Mac31: dice que pantallas hay y quien entra a cada
    /// boton. Quien puede leerlo puede planear por donde entrar, asi que pide lo
    /// mismo que la pantalla de permisos (`permisosweb.bootstrap`).
    ///
    /// El numero de formas que devuelve CAMBIA con la empresa: el SP ajusta su
    /// filtro segun `Constantes.EsAlcaldiaMC`, `AviacionIDCaja` y
    /// `EsAviacionServer`. No es un parametro, es la configuracion de la base.
    /// </remarks>
    [HttpGet("formas-procesos")]
    [Authorize(Policy = "perm:permisosweb.bootstrap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> FormasProcesos([FromQuery] int idUsuario = 0, CancellationToken ct = default)
    {
        var data = await _service.ConsultarFormasProcesosAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Formas y procesos consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Fechas de operacion abiertas para un usuario.
    /// </summary>
    /// <remarks>
    /// `idUsuario` es obligatorio de hecho: el WHERE del SP siempre compara
    /// contra el, no hay rama de "todos", asi que con 0 devuelve vacio. Se
    /// contesta 400 en vez de una lista vacia porque una lista vacia aqui se lee
    /// como "este usuario no tiene fechas abiertas", que es otra cosa.
    ///
    /// Nota de datos: la tabla es `UsuariosFechas`, SIN espacio. No es la
    /// `[Usuarios Fechas]` (con espacio) que toca sp_n_ConsultaConstantes al
    /// rodar la fecha de operacion. Son dos tablas distintas en la misma base.
    /// </remarks>
    /// <response code="400">idUsuario requerido</response>
    [HttpGet("fechas")]
    [Authorize(Policy = "perm:usuarios.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Fechas([FromQuery] int idUsuario, CancellationToken ct)
    {
        if (idUsuario <= 0)
            return BadRequest(new { message = "idUsuario requerido." });

        var data = await _service.ConsultarFechasAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Fechas del usuario consultadas.", data });
    }

    /// <summary>
    /// El siguiente numero de usuario libre, para la pantalla de alta.
    /// </summary>
    /// <remarks>
    /// Pide `usuarios.crear` y no `usuarios.ver`: es para quien va a dar de alta.
    ///
    /// NO RESERVA NADA. Es MAX(NumUsuario)+1 entre los activos, calculado en el
    /// momento: dos altas simultaneas reciben el mismo numero. Es exactamente lo
    /// que hace Mac31, y por eso quien de verdad asigna el numero tiene que ser
    /// el alta, no esta consulta.
    /// </remarks>
    [HttpGet("siguiente-numero")]
    [Authorize(Policy = "perm:usuarios.crear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SiguienteNumero(CancellationToken ct)
    {
        var numUsuario = await _service.ConsultarSiguienteNumeroAsync(ct);
        return Ok(new { ok = true, message = "Siguiente numero consultado.", data = new { numUsuario } });
    }
}
