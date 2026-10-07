using ISL_Service.Application.DTOs.VentasConsultasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  LAS CABECERAS DE PANTALLA DE VENTAS QUE FALTABAN

  Son lecturas, y usan las claves que manda Mac31 porque existen y significan
  exactamente esto: `ventas.consultar` es btnBuscar de la forma CONSULTA DE
  VENTAS, y `ventas.pagos.ver` es btnPagos. Reusarlas es lo correcto aqui:
  quien puede pulsar Buscar en Mac31 es quien debe poder pedir estas listas, y
  hay UNA sola verdad en vez de dos interruptores.

  Los pendientes de autorizar usan `ventas.pendientes.ver`, que es del WEB y esta
  en la lista blanca (VentasWebPermissionSeeds), asi que esa si se conserva al
  recalcular el snapshot. Tiene su propia clave desde antes de esta ola, y por
  una razon escrita en PermissionService: ver la pantalla y soltar el pedido son
  permisos distintos a proposito.

  TODAS van con `no-store`: son saldos, pagos y folios. Un saldo en la cache del
  navegador es un saldo equivocado.
*/
[ApiController]
[Route("api/ventas/consultas")]
[Authorize]
public class VentasConsultasLegacyController : PermisoControllerBase
{
    private static readonly string[] PermisoConsultar = { "ventas.consultar" };
    private static readonly string[] PermisoPagos = { "ventas.pagos.ver", "cobros.pagos.editar" };
    private static readonly string[] PermisoPendientes = { "ventas.pendientes.ver" };
    private static readonly string[] PermisoFolios = { "cobranza.ver", "ventas.consultar" };

    private readonly IVentasConsultasLegacyService _service;

    public VentasConsultasLegacyController(
        IVentasConsultasLegacyService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>El detalle de pedidos de la consulta de ventas.</summary>
    [HttpPost("pedidos-detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PedidoDetalleAsync(
        [FromBody] VentasPedidoDetalleConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoConsultar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPedidoDetalleAsync(request, IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Detalle de pedidos.", data });
    }

    /// <summary>Las remisiones en transito.</summary>
    [HttpPost("transito")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TransitoAsync(
        [FromBody] VentasTransitoConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoConsultar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarTransitoAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Remisiones en transito.", data });
    }

    /// <summary>Las ventas pendientes de autorizar o rechazar.</summary>
    [HttpPost("pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PendientesAsync(
        [FromBody] VentasPendienteConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPendientes, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPendientesAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Ventas pendientes.", data });
    }

    /// <summary>Los pagos de una remision (esquema viejo).</summary>
    [HttpPost("pagos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PagosAsync(
        [FromBody] VentasPagosConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPagos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPagosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos de la remision.", data });
    }

    /// <summary>
    /// Los pagos del esquema nuevo. Es un endpoint aparte y no una bandera del
    /// anterior: en legacy la bandera `NuevoEsquemaPagos` del cuerpo decidia
    /// contra que tabla se consultaba, y con cualquier valor que no fuera 0 ni 1
    /// no se ejecutaba NINGUN procedimiento y contestaba una lista vacia como si
    /// no hubiera datos.
    /// </summary>
    [HttpPost("pagos/nuevos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NuevosPagosAsync(
        [FromBody] VentasNuevosPagosConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPagos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarNuevosPagosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos del esquema nuevo.", data });
    }

    /// <summary>Los pagos de centro de servicio.</summary>
    [HttpPost("pagos/centro-servicio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PagosCentroServicioAsync(
        [FromBody] VentasPagosCentroServicioConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPagos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPagosCentroServicioAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos de centro de servicio.", data });
    }

    /// <summary>Los pagos de una fecha de liquidacion.</summary>
    [HttpPost("pagos/liquidacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PagosLiquidacionAsync(
        [FromBody] VentasPagosLiquidacionConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoPagos, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarPagosLiquidacionAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Pagos de la liquidacion.", data });
    }

    /// <summary>El reporte de folios de venta.</summary>
    [HttpPost("folios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FoliosAsync(
        [FromBody] VentasFoliosConsultaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoFolios, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ConsultarFoliosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Folios de venta.", data });
    }
}
