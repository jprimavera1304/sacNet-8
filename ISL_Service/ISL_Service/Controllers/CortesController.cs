using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Cortes de caja: detalle de un corte, siguiente fecha de corte, y los dos
/// listados de corte de centro de servicio.
///
/// FALTAN TRES ENDPOINTS DE LEGACY, Y ES A PROPOSITO
/// -------------------------------------------------
/// La lista de cortes, los conceptos de gasto y la lista de cortes de centro de
/// servicio piden `sp_n_ConsultaConstantes` antes de consultar, solo para leer
/// `formatoFechaDB` y voltear el dia y el mes del filtro. Ese procedimiento, con
/// la fecha de operacion atrasada, hace el CIERRE DEL DIA: borra y rehace el
/// congelado de existencias del dia anterior y adelanta la fecha de operacion del
/// negocio. Abrir una pantalla de consulta no puede cerrar el dia, asi que esos
/// tres no se migran como lecturas: van en la entrega que los trate como
/// escrituras, dentro del ciclo de instantanea.
///
/// SOLO LECTURA: `sp_n_InsertarCorte`, `sp_n_InsertarCorteCS` y
/// `sp_n_CancelarCorte` existen y no se exponen.
/// </summary>
[ApiController]
[Route("api/cortes")]
[Authorize]
public class CortesController : ControllerBase
{
    private readonly ICortesService _service;

    public CortesController(ICortesService service)
    {
        _service = service;
    }

    /// <summary>
    /// Detalle de un corte: sus totales mas sus gastos.
    /// </summary>
    /// <remarks>
    /// Devuelve la cabecera del corte REPETIDA en cada renglon de gasto, porque
    /// es la forma en la que el SP la entrega y es la que pinta la rejilla de
    /// Mac31. Un corte sin gastos devuelve un solo renglon con
    /// `idCorteDetalle = 0`: eso no es un gasto de cero pesos, es "no hay
    /// gastos".
    /// </remarks>
    /// <response code="400">idCorte requerido</response>
    [HttpGet("{idCorte:int}/detalle")]
    [Authorize(Policy = "perm:cortes.ver|app_movil.cortes.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Detalle([FromRoute] int idCorte, CancellationToken ct)
    {
        // Con 0 el SP devuelve vacio. Se contesta 400 porque una lista vacia aqui
        // se leeria como "ese corte no tiene nada", que es otra cosa.
        if (idCorte <= 0)
            return BadRequest(new { message = "idCorte requerido." });

        var data = await _service.ConsultarDetalleAsync(idCorte, ct);
        return Ok(new { ok = true, message = "Detalle del corte consultado.", data });
    }

    /// <summary>
    /// La siguiente fecha de corte.
    /// </summary>
    /// <remarks>
    /// En legacy este endpoint se llamaba `/consultar/actual` y recibia un
    /// `IDCorte` que el SP pisaba en su primera linea. Aqui no recibe nada,
    /// porque no depende de nada: es el ultimo corte NO CANCELADO mas un dia, y
    /// si ese dia cae en domingo, mas otro (el negocio no corta en domingo).
    ///
    /// El `idCorte` que viene en la respuesta es el del ULTIMO corte, no el de uno
    /// nuevo: nada se reserva aqui.
    ///
    /// Puede venir vacio si no hay ni un corte en la base.
    /// </remarks>
    /// <response code="200">Siguiente fecha</response>
    /// <response code="404">No hay ningun corte del que partir</response>
    [HttpGet("siguiente-fecha")]
    [Authorize(Policy = "perm:cortes.ver|app_movil.cortes.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SiguienteFecha(CancellationToken ct)
    {
        var data = await _service.ConsultarSiguienteFechaAsync(ct);
        if (data is null || data.FechaCorte is null)
            return NotFound(new { message = "No hay cortes registrados." });

        return Ok(new { ok = true, message = "Siguiente fecha de corte consultada.", data });
    }

    /// <summary>
    /// Cortes de centro de servicio con sus saldos por forma de pago.
    /// </summary>
    /// <param name="idCorte">Un corte. 0 = todos.</param>
    /// <param name="idCobroDineroUsado">
    /// CUIDADO: aqui el 0 NO significa "todos". El SP, cuando llega 0, filtra
    /// `ISNULL(IDCobroDineroUsado,0) = 0`, o sea los cortes que TODAVIA NO estan
    /// asociados a un cobro. Es exactamente al contrario que en el resto del
    /// sistema, y es lo que hace Mac31: la pantalla sirve para encontrar lo que
    /// falta cobrar.
    /// </param>
    /// <param name="idTipoRetiro">1 efectivo, 2 cheque, 5 transferencia, 9 tarjeta, 10 deposito. 0 = todos.</param>
    /// <remarks>
    /// TARDA, y puede fallar por una base ajena: el SP recorre con un cursor
    /// todos los centros de servicio activos y consulta la base de cada uno. Si
    /// una base de centro no responde, falla la llamada completa. No se filtra por
    /// centro porque el SP no lo permite — siempre trae todos.
    ///
    /// No devuelve status ni datos de cancelacion: ese SP, al contrario que el de
    /// la lista de cortes CS, no los proyecta.
    /// </remarks>
    [HttpGet("centros-servicio/cobros")]
    [Authorize(Policy = "perm:cortes.cs.ver|app_movil.cortes.cs.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CobrosCs(
        [FromQuery] int idCorte = 0,
        [FromQuery] int idCobroDineroUsado = 0,
        [FromQuery] int idTipoRetiro = 0,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarCobrosCsAsync(idCorte, idCobroDineroUsado, idTipoRetiro, ct);
        return Ok(new { ok = true, message = "Cobros de corte consultados.", data, total = data.Count });
    }

    /// <summary>
    /// Ventas canceladas que salieron por un corte de centro de servicio, con su
    /// total.
    /// </summary>
    /// <param name="idCorte">
    /// CUIDADO: el 0 NO significa "todas". El SP con 0 filtra
    /// `venCan.IDCorte IS NULL`, o sea las cancelaciones que aun no se han
    /// llevado a ningun corte. Asi funciona en Mac31.
    /// </param>
    /// <remarks>
    /// `sumaAbonos` va una sola vez, fuera de los renglones. El SP lo devuelve
    /// repetido en cada fila y legacy lo perdia entero porque su DTO no declaraba
    /// la columna: el total que la base calculaba no llegaba nunca a la pantalla.
    ///
    /// El `motivo` de cada renglon lo ARMA el SP y pisa lo que haya guardado en la
    /// base, asi que no se puede confiar en el como texto capturado por nadie.
    /// </remarks>
    [HttpGet("centros-servicio/ventas-canceladas")]
    [Authorize(Policy = "perm:cortes.cs.ver|app_movil.cortes.cs.ver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VentasCanceladasCs(
        [FromQuery] int idCorte = 0,
        [FromQuery] int idTipoRetiro = 0,
        CancellationToken ct = default)
    {
        var data = await _service.ConsultarVentasCanceladasCsAsync(idCorte, idTipoRetiro, ct);
        return Ok(new
        {
            ok = true,
            message = "Ventas canceladas del corte consultadas.",
            data = data.Renglones,
            sumaAbonos = data.SumaAbonos,
            total = data.Renglones.Count
        });
    }
}
