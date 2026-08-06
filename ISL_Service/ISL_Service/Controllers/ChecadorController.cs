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

    // Quien y desde donde: el cliente NUNCA los manda, para que no pueda firmar
    // una checada a nombre de otro. El IDUsuario sale del token (claim legacy) y
    // el equipo del nombre de usuario del token; si el token no lo trae, se usa la
    // maquina del servidor, que es lo mismo que hace la captura de pedidos.
    private string ResolveEquipo()
        => _currentUserAccessor.GetUsername(User, Environment.MachineName);
}
