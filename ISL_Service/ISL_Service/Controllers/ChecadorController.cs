using ISL_Service.Application.DTOs.Checador;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

/// <summary>
/// Checador: hora de comida de los empleados (salida a comer y regreso).
/// Usa los SP sp_w_* sobre la tabla WEmpleadoChecada. Los eventos no se borran:
/// se cancelan con motivo.
/// </summary>
[ApiController]
[Route("api/checador")]
[Authorize]
public class ChecadorController : ControllerBase
{
    private readonly IChecadorService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChecadorController(IChecadorService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// Registra una checada de comida. Si no se manda tipo, el SP deduce si toca
    /// COMIDA_INICIO o COMIDA_FIN segun la ultima checada del empleado ese dia.
    /// </summary>
    /// <response code="200">Checada registrada</response>
    /// <response code="400">Datos invalidos o regla de negocio (empleado inactivo)</response>
    /// <response code="409">Secuencia invalida (por ejemplo, cerrar una comida que nunca inicio)</response>
    [HttpPost("comida")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarComida([FromBody] RegistrarChecadaComidaRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.RegistrarComidaAsync(request, idUsuario, ResolveEquipo(), ct);

        var message = data.Tipo == ChecadaComidaTipo.Fin
            ? "Regreso de comida registrado."
            : "Salida a comida registrada.";
        return Ok(new { ok = true, message, data });
    }

    /// <summary>
    /// Consulta las comidas de un rango de fechas. Sin idEmpleado (o con 0) trae
    /// las de todos los empleados.
    /// </summary>
    /// <param name="idEmpleado">Opcional. 0 o ausente = todos.</param>
    /// <param name="fechaInicial">Requerida (solo la fecha, la hora se ignora).</param>
    /// <param name="fechaFinal">Requerida (solo la fecha, la hora se ignora).</param>
    /// <param name="incluirCanceladas">Opcional, default false.</param>
    [HttpGet("comida")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarComida(
        [FromQuery] int idEmpleado,
        [FromQuery] DateTime? fechaInicial,
        [FromQuery] DateTime? fechaFinal,
        [FromQuery] bool incluirCanceladas,
        CancellationToken ct)
    {
        var data = await _service.ConsultarComidasAsync(idEmpleado, fechaInicial, fechaFinal, incluirCanceladas, ct);
        return Ok(new { ok = true, message = "Checadas de comida consultadas.", data });
    }

    /// <summary>
    /// Cancela una checada de comida. No se borra el renglon: queda marcado como
    /// cancelado con el motivo, el usuario y el equipo.
    /// </summary>
    /// <response code="200">Checada cancelada</response>
    /// <response code="400">Motivo vacio o regla de negocio</response>
    /// <response code="404">La checada no existe</response>
    /// <response code="409">La checada ya estaba cancelada</response>
    [HttpPost("comida/{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarComida([FromRoute] int id, [FromBody] CancelarChecadaComidaRequest? request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Motivo))
            return BadRequest(new { ok = false, message = "motivo es requerido." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        await _service.CancelarComidaAsync(id, request.Motivo, idUsuario, ResolveEquipo(), ct);
        return Ok(new { ok = true, message = "Checada cancelada." });
    }

    /// <summary>
    /// Registra la ENTRADA o la SALIDA del empleado que puso el dedo.
    ///
    /// No se manda el tipo: el SP mira si ya tiene hora de entrada hoy y decide.
    /// Es a proposito, porque el que checa no elige nada, nada mas pone el dedo.
    /// La checada queda tambien en EmpleadoAsistencias, asi que se ve en el
    /// sistema viejo igual que si hubiera usado el checador de siempre.
    /// </summary>
    /// <response code="200">Movimiento registrado</response>
    /// <response code="400">Datos invalidos o empleado inactivo</response>
    /// <response code="409">No hay periodo de nomina generado para hoy</response>
    [HttpPost("movimientos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarMovimiento([FromBody] RegistrarChecadaMovimientoRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.RegistrarMovimientoAsync(request, idUsuario, ResolveEquipo(), ct);

        var message = data.Tipo == ChecadaMovimientoTipo.Salida
            ? "Salida registrada."
            : "Entrada registrada.";
        return Ok(new { ok = true, message, data });
    }

    /// <summary>
    /// Todo lo checado en un dia: entradas, salidas y comidas, en una sola lista.
    /// Incluye lo que se marco en el checador viejo, no solo lo del web.
    /// </summary>
    /// <param name="fecha">Opcional. Sin ella, hoy.</param>
    /// <param name="idEmpleado">Opcional. 0 o ausente = todos.</param>
    [HttpGet("movimientos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarMovimientos(
        [FromQuery] DateTime? fecha,
        [FromQuery] int idEmpleado,
        CancellationToken ct)
    {
        var data = await _service.ConsultarMovimientosDiaAsync(fecha, idEmpleado, ct);
        return Ok(new { ok = true, message = "Movimientos consultados.", data = data.Rows });
    }

    /// <summary>
    /// El horario que debe mostrar la pantalla (entrada, salida, jornada) y la
    /// hora a partir de la cual se propone SALIDA en vez de ENTRADA.
    ///
    /// Sale de la misma configuracion que lee el checador viejo, para que los
    /// dos digan lo mismo.
    /// </summary>
    /// <summary>
    /// La asistencia de un dia: un renglon por empleado con su entrada, su
    /// salida y su retardo, igual que la muestra el checador viejo.
    /// </summary>
    [HttpGet("asistencia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarAsistencia(
        [FromQuery] DateTime? fecha,
        [FromQuery] int idEmpleado,
        CancellationToken ct)
    {
        var data = await _service.ConsultarAsistenciaDiaAsync(fecha, idEmpleado, ct);
        return Ok(new { ok = true, message = "Asistencia consultada.", data = data.Rows });
    }

    [HttpGet("configuracion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarConfiguracion(CancellationToken ct)
    {
        var data = await _service.ConsultarConfiguracionAsync(ct);
        return Ok(new { ok = true, message = "Configuracion consultada.", data = data.Rows.FirstOrDefault() });
    }

    /// <summary>
    /// Empleados activos con cuantos dedos tienen registrados, para el alta de
    /// huella.
    /// </summary>
    [HttpGet("empleados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarEmpleados([FromQuery] string? filtro, CancellationToken ct)
    {
        var data = await _service.ConsultarEmpleadosAsync(filtro, ct);
        return Ok(new { ok = true, message = "Empleados consultados.", data = data.Rows });
    }

    /// <summary>
    /// Que dedos tiene registrados un empleado. No devuelve la huella misma:
    /// la pantalla solo necesita saber cuales hay, y un dato biometrico no tiene
    /// por que salir a un navegador.
    /// </summary>
    [HttpGet("empleados/{idEmpleado:int}/huellas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarHuellas([FromRoute] int idEmpleado, CancellationToken ct)
    {
        var data = await _service.ConsultarHuellasAsync(idEmpleado, ct);
        return Ok(new { ok = true, message = "Huellas consultadas.", data = data.Rows });
    }

    /// <summary>
    /// Da de alta o reemplaza la huella de un dedo. Se guarda donde ya las tiene
    /// el sistema, para que la persona pueda checar tambien en el aparato viejo.
    ///
    /// Van dos capturas del mismo dedo: es como las guarda el sistema y es lo
    /// que hace que reconozca aunque el dedo quede un poco corrido.
    /// </summary>
    /// <response code="200">Huella guardada</response>
    /// <response code="400">Faltan capturas, o mano/dedo invalidos</response>
    [HttpPost("huellas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarHuella([FromBody] GuardarHuellaEmpleadoRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.GuardarHuellaAsync(request, idUsuario, ResolveEquipo(), ct);
        return Ok(new { ok = true, message = "Huella guardada.", data = data.Rows });
    }

    /// <summary>
    /// Quita la huella de un dedo. Se borra de donde la lee el checador (si no,
    /// seguiria abriendo la puerta), pero antes se guarda copia por si la baja
    /// fue un error.
    /// </summary>
    /// <response code="200">Huella dada de baja</response>
    /// <response code="404">La huella no existe</response>
    [HttpPost("huellas/{idEmpleadoHuella:int}/baja")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BajaHuella([FromRoute] int idEmpleadoHuella, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        await _service.BajaHuellaAsync(idEmpleadoHuella, idUsuario, ResolveEquipo(), ct);
        return Ok(new { ok = true, message = "Huella dada de baja." });
    }

    // Quien y desde donde: el cliente NUNCA los manda, para que no pueda firmar
    // una checada a nombre de otro. El IDUsuario sale del token (claim legacy) y
    // el equipo del nombre de usuario del token; si el token no lo trae, se usa la
    // maquina del servidor, que es lo mismo que hace la captura de pedidos.
    private string ResolveEquipo()
        => _currentUserAccessor.GetUsername(User, Environment.MachineName);
}
