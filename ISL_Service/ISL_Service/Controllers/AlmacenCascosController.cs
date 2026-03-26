using System.Security.Claims;
using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// API Almacén de Cascos: movimientos (salidas/entradas), detalle y cancelación. Usa SP sp_w_* y tablas WMovimientoCasco / WMovimientoCascoDetalle.
/// </summary>
[ApiController]
[Route("api/almacen-cascos")]
[Authorize]
public class AlmacenCascosController : ControllerBase
{
    private readonly IAlmacenCascosService _service;

    public AlmacenCascosController(IAlmacenCascosService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lista movimientos con filtros opcionales. TipoMovimiento: 1=SALIDA, 2=ENTRADA. Estatus: 1=REGISTRADA, 2=ACEPTADA, 3=CANCELADA.
    /// </summary>
    /// <param name="estatus">NULL = todos, 1/2/3 para filtrar</param>
    /// <param name="tipoMovimiento">Opcional: 1=SALIDA, 2=ENTRADA</param>
    /// <param name="fechaInicio">Opcional: filtrar desde fecha</param>
    /// <param name="fechaFin">Opcional: filtrar hasta fecha</param>
    [HttpGet("movimientos")]
    [ProducesResponseType(typeof(List<MovimientoCascoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovimientos(
        [FromQuery] int? estatus,
        [FromQuery] int? tipoMovimiento,
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        CancellationToken ct)
    {
        var list = await _service.ConsultarMovimientosAsync(estatus, tipoMovimiento, fechaInicio, fechaFin, ct);
        return Ok(list);
    }

    /// <summary>
    /// Obtiene el detalle (líneas de tarimas/piezas) de un movimiento.
    /// </summary>
    [HttpGet("movimientos/{idMovimiento:int}/detalle")]
    [ProducesResponseType(typeof(List<MovimientoCascoDetalleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetalle(int idMovimiento, CancellationToken ct)
    {
        var list = await _service.GetDetalleMovimientoAsync(idMovimiento, ct);
        return Ok(list);
    }

    /// <summary>
    /// Obtiene las tarimas con kilos capturados para una ENTRADA.
    /// </summary>
    [HttpGet("movimientos/{idMovimiento:int}/tarimas")]
    [ProducesResponseType(typeof(List<MovimientoCascoTarimaKilosDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTarimasKilos(int idMovimiento, CancellationToken ct)
    {
        var list = await _service.GetMovimientoTarimasKilosAsync(idMovimiento, ct);
        return Ok(list);
    }

    /// <summary>
    /// Obtiene detalle agrupado por tarima logica (numeroTarima) y lineas por tipo de casco.
    /// </summary>
    [HttpGet("movimientos/{idMovimiento:int}/detalle-agrupado")]
    [ProducesResponseType(typeof(MovimientoCascoDetalleAgrupadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetalleAgrupado(int idMovimiento, CancellationToken ct)
    {
        var list = await _service.GetDetalleMovimientoAsync(idMovimiento, ct);
        var grouped = new MovimientoCascoDetalleAgrupadoDto
        {
            IdMovimiento = idMovimiento,
            Tarimas = list
                .GroupBy(x => x.NumeroTarima)
                .OrderBy(g => g.Key)
                .Select(g => new TarimaDetalleAgrupadoDto
                {
                    NumeroTarima = g.Key,
                    Lineas = g
                        .OrderBy(x => x.IdDetalle)
                        .Select(x => new TarimaDetalleLineaDto
                        {
                            IdDetalle = x.IdDetalle,
                            IdTarima = x.IdTarima,
                            IdTipoCasco = x.IdTipoCasco,
                            TipoCascoDescripcion = x.TipoCascoDescripcion,
                            Piezas = x.Piezas
                        })
                        .ToList()
                })
                .ToList()
        };

        return Ok(grouped);
    }

    /// <summary>
    /// Crea una SALIDA (cabecera + detalle). TotalTarimas/TotalPiezas se calculan en backend; TotalKilos = 0.
    /// </summary>
    /// <response code="201">Salida creada (devuelve idMovimiento)</response>
    /// <response code="400">Validación fallida o repartidor/tarimas no activos</response>
    [HttpPost("salidas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearSalida([FromBody] CreateSalidaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = GetUsuarioFromToken();
        var idMovimiento = await _service.CrearSalidaAsync(request, usuario, ct);
        return CreatedAtAction(nameof(GetMovimientos), new { idMovimiento }, new { idMovimiento });
    }

    /// <summary>
    /// Acepta una ENTRADA desde una SALIDA registrada. El SP crea la entrada, marca la salida como aceptada y deja TotalKilos = 0.
    /// </summary>
    /// <response code="200">Entrada aceptada</response>
    /// <response code="400">Validación o salida sin detalle / no registrada</response>
    /// <response code="409">Ya existe entrada para esta salida (índice único)</response>
    [HttpPost("entradas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarEntrada([FromBody] CreateEntradaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = GetUsuarioFromToken();
        await _service.AceptarEntradaAsync(request, usuario, ct);
        return Ok(new { message = "Entrada aceptada." });
    }

    /// <summary>
    /// Guarda kilos por tarima para una ENTRADA (inserta o actualiza).
    /// </summary>
    [HttpPost("movimientos/{idMovimiento:int}/tarimas/{numeroTarima:int}/kilos")]
    [ProducesResponseType(typeof(MovimientoCascoTarimaKilosResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarKilosTarima(int idMovimiento, int numeroTarima, [FromBody] GuardarKilosTarimaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = GetUsuarioFromToken();
        var result = await _service.GuardarKilosTarimaAsync(idMovimiento, numeroTarima, request.Kilos, usuario, ct);
        return Ok(result);
    }

    /// <summary>
    /// Actualiza una SALIDA (repartidor, tarima, cantidad de tarimas, piezas, observaciones).
    /// </summary>
    [HttpPut("salidas/{idMovimiento:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarSalida(int idMovimiento, [FromBody] UpdateSalidaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = GetUsuarioFromToken();
        await _service.ActualizarSalidaAsync(idMovimiento, request, usuario, ct);
        return Ok(new { message = "Salida actualizada." });
    }

    /// <summary>
    /// Actualiza una ENTRADA (repartidor recibe, observaciones).
    /// </summary>
    [HttpPut("entradas/{idMovimiento:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarEntrada(int idMovimiento, [FromBody] UpdateEntradaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = GetUsuarioFromToken();
        await _service.ActualizarEntradaAsync(idMovimiento, request, usuario, ct);
        return Ok(new { message = "Entrada actualizada." });
    }

    /// <summary>
    /// Cancela un movimiento (entrada o salida). Reglas validadas en SP (no cancelar ya cancelado; no cancelar salida si tiene entrada aceptada).
    /// </summary>
    /// <response code="200">Movimiento cancelado</response>
    /// <response code="400">Motivo vacío o regla de negocio</response>
    /// <response code="409">Ya cancelado o no se puede cancelar</response>
    [HttpPost("movimientos/{idMovimiento:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(int idMovimiento, [FromBody] CancelarMovimientoRequest body, CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.MotivoCancelacion))
            return BadRequest(new { message = "motivoCancelacion es requerido." });

        var usuario = GetUsuarioFromToken();
        await _service.CancelarMovimientoAsync(idMovimiento, body.MotivoCancelacion.Trim(), usuario, ct);
        return Ok(new { message = "Movimiento cancelado." });
    }

    private static string GetUsuarioFromToken(ClaimsPrincipal user)
    {
        var u = user.FindFirstValue("username")
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue("name")
            ?? user.FindFirstValue("unique_name")
            ?? user.Identity?.Name;
        return string.IsNullOrWhiteSpace(u) ? "Sistema" : u.Trim();
    }

    private string GetUsuarioFromToken() => GetUsuarioFromToken(User);
}
