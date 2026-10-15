using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

/// <summary>
/// API Almacén de Cascos: movimientos (salidas/entradas), detalle y cancelación. Usa SP sp_w_* y tablas WMovimientoCasco / WMovimientoCascoDetalle.
///
/// PERMISOS
/// --------
/// Las lecturas piden solo token, como antes. Las SEIS escrituras llevan ahora
/// permiso propio: antes bastaba con tener un token de cualquier rol para crear,
/// modificar o CANCELAR un movimiento de casco.
///
///   crear    -> POST salidas, POST entradas
///   editar   -> POST .../kilos, PUT salidas/{id}, PUT entradas/{id}
///   cancelar -> POST movimientos/{id}/cancelar
///
/// Las claves son las que ya pide el front en
/// shared/js/core/module-action-flags.js (almacen_cascos.*); se declaran en
/// PermissionService.PermissionCatalogSeeds.
/// </summary>
/*
  DOS CANALES, LA MISMA RUTA: `almacen_cascos.X` (web y Mac31) o
  `app_movil.almacen_cascos.X` (telefono). Basta con tener UNA.

  Antes solo existia la del web, y por eso quitarle el modulo a alguien en el
  telefono se lo quitaba tambien en la computadora: era el mismo permiso, no
  dos parecidos. Con el par se puede negar un lado sin tocar el otro, como ya
  hacen Empleados, Prestamos y Cambio de Cascos.

  El canal movil NO pide entrada de modulo (EntradasDeModulo deja pasar las
  claves `app_movil.`), asi que una concesion de solo-telefono alcanza por si
  sola: el telefono no tiene mosaicos de inicio que abrir.
*/
[ApiController]
[Route("api/almacen-cascos")]
[Authorize]
public class AlmacenCascosController : ControllerBase
{
    private readonly IAlmacenCascosService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AlmacenCascosController(IAlmacenCascosService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// Lista movimientos con filtros opcionales. TipoMovimiento: 1=SALIDA, 2=ENTRADA. Estatus: 1=REGISTRADA, 2=ACEPTADA, 3=CANCELADA.
    /// </summary>
    /// <param name="estatus">NULL = todos, 1/2/3 para filtrar</param>
    /// <param name="tipoMovimiento">Opcional: 1=SALIDA, 2=ENTRADA</param>
    /// <param name="fechaInicio">Opcional: filtrar desde fecha</param>
    /// <param name="fechaFin">Opcional: filtrar hasta fecha</param>
    // LAS LECTURAS TAMBIEN PIDEN PERMISO.
    //
    // La cabecera de arriba decia "las lecturas piden solo token, como
    // antes", y se midio el 2026-10-12 con una cuenta de auditoria sin un
    // solo permiso: estos cuatro contestaban 200 y entregaban los
    // movimientos de cascos, su detalle y sus tarimas. De los 220 GET del
    // servicio eran cuatro de los pocos que dejaban pasar.
    //
    // La clave no es nueva: es la que ya exige GET /existencias en este
    // mismo controlador, y la que el front pide para pintar la pantalla.
    [Authorize(Policy = "perm:almacen_cascos.ver|app_movil.almacen_cascos.ver")]
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
    [Authorize(Policy = "perm:almacen_cascos.ver|app_movil.almacen_cascos.ver")]
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
    [Authorize(Policy = "perm:almacen_cascos.ver|app_movil.almacen_cascos.ver")]
    [HttpGet("movimientos/{idMovimiento:int}/tarimas")]
    [ProducesResponseType(typeof(List<MovimientoCascoTarimaKilosDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTarimasKilos(int idMovimiento, CancellationToken ct)
    {
        var list = await _service.GetMovimientoTarimasKilosAsync(idMovimiento, ct);
        return Ok(list);
    }

    /// <summary>
    /// Obtiene detalle agrupado por tarima lógica (numeroTarima) y líneas por tipo de casco.
    /// </summary>
    [Authorize(Policy = "perm:almacen_cascos.ver|app_movil.almacen_cascos.ver")]
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
    /// La existencia de cascos por tipo: el panel "CASCOS ACTUAL" de Mac31.
    /// </summary>
    /// <remarks>
    /// POR QUE ESTE SI PIDE PERMISO Y LAS OTRAS LECTURAS NO.
    ///
    /// Las cuatro lecturas de arriba se quedan con solo token porque ya las
    /// consumen el front viejo y el movil, y apretarles el permiso hoy les
    /// contestaria 403 a usuarios que llevan meses entrando. Esta nace nueva y
    /// no tiene consumidores que romper, asi que nace bien: con
    /// `almacen_cascos.ver`, que es el permiso que el catalogo ya declara para
    /// mirar este modulo y que la pantalla web exige para dibujarse.
    ///
    /// NO ENVUELVE EN `{ok, data}`, y tampoco es un descuido: las otras cuatro
    /// lecturas de este controlador devuelven la lista DESNUDA, y los dos
    /// clientes que ya existen (el front viejo de /almacen-cascos y
    /// `almacen_cascos.dart`) la leen asi. Un endpoint nuevo con otra forma
    /// obligaria a cada pantalla a saber cual de las dos le toca segun la ruta,
    /// que es exactamente la clase de detalle que se olvida. La forma de este
    /// modulo es la lista pelada; se respeta.
    /// </remarks>
    /// <response code="200">Existencia por tipo de casco</response>
    [HttpGet("existencias")]
    [Authorize(Policy = "perm:almacen_cascos.ver|app_movil.almacen_cascos.ver")]
    [ProducesResponseType(typeof(List<ExistenciaDeCascoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExistencias(CancellationToken ct)
    {
        var list = await _service.ConsultarExistenciasAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Crea una SALIDA (cabecera + detalle). TotalTarimas/TotalPiezas se calculan en backend; TotalKilos = 0.
    /// </summary>
    /// <response code="201">Salida creada (devuelve idMovimiento)</response>
    /// <response code="400">Validación fallida o repartidor/tarimas no activos</response>
    [HttpPost("salidas")]
    [Authorize(Policy = "perm:almacen_cascos.crear|app_movil.almacen_cascos.crear")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearSalida([FromBody] CreateSalidaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
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
    [Authorize(Policy = "perm:almacen_cascos.crear|app_movil.almacen_cascos.crear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceptarEntrada([FromBody] CreateEntradaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
        await _service.AceptarEntradaAsync(request, usuario, ct);
        return Ok(new { message = "Entrada aceptada." });
    }

    /// <summary>
    /// Guarda kilos por tarima para una ENTRADA (inserta o actualiza).
    /// </summary>
    [HttpPost("movimientos/{idMovimiento:int}/tarimas/{numeroTarima:int}/kilos")]
    [Authorize(Policy = "perm:almacen_cascos.editar|app_movil.almacen_cascos.editar")]
    [ProducesResponseType(typeof(MovimientoCascoTarimaKilosResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarKilosTarima(int idMovimiento, int numeroTarima, [FromBody] GuardarKilosTarimaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
        var result = await _service.GuardarKilosTarimaAsync(idMovimiento, numeroTarima, request.Kilos, usuario, ct);
        return Ok(result);
    }

    /// <summary>
    /// Actualiza una SALIDA (repartidor, tarima, cantidad de tarimas, piezas, observaciones).
    /// </summary>
    [HttpPut("salidas/{idMovimiento:int}")]
    [Authorize(Policy = "perm:almacen_cascos.editar|app_movil.almacen_cascos.editar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarSalida(int idMovimiento, [FromBody] UpdateSalidaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
        await _service.ActualizarSalidaAsync(idMovimiento, request, usuario, ct);
        return Ok(new { message = "Salida actualizada." });
    }

    /// <summary>
    /// Actualiza una ENTRADA (repartidor recibe, observaciones).
    /// </summary>
    [HttpPut("entradas/{idMovimiento:int}")]
    [Authorize(Policy = "perm:almacen_cascos.editar|app_movil.almacen_cascos.editar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActualizarEntrada(int idMovimiento, [FromBody] UpdateEntradaRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { message = "Body requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
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
    [Authorize(Policy = "perm:almacen_cascos.cancelar|app_movil.almacen_cascos.cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(int idMovimiento, [FromBody] CancelarMovimientoRequest body, CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.MotivoCancelacion))
            return BadRequest(new { message = "motivoCancelacion es requerido." });

        var usuario = _currentUserAccessor.GetUsername(User);
        await _service.CancelarMovimientoAsync(idMovimiento, body.MotivoCancelacion.Trim(), usuario, ct);
        return Ok(new { message = "Movimiento cancelado." });
    }

}
