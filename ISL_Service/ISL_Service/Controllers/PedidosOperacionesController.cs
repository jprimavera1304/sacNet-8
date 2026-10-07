using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  PROCESAR, CANCELAR Y AJUSTAR PEDIDOS

  Aqui SI se reusan las claves de Mac31, y es lo correcto: `ventas.pedidos.procesar`
  y `ventas.pedidos.cancelar` son botones de verdad de la forma CONSULTA DE
  VENTAS (btnProcesarPedido y btnCancelarPedido), ya mapeados en el puente de
  permisos. Quien puede procesar un pedido en Mac31 es exactamente quien debe
  poder procesarlo desde el web, y asi hay UNA sola verdad en vez de dos
  interruptores que hay que acordarse de encender juntos.

  Las compras son el caso raro: en Mac31 esa pantalla no cuelga de la forma de
  ventas, asi que no hay boton que mapear. Se reusa `ventas.pedidos.procesar`
  porque es la misma accion sobre el mismo pedido —procesarlo— y separar una
  clave nueva en la familia `ventas.*` la haria desaparecer en el siguiente
  recalculo del snapshot.
*/
[ApiController]
[Route("api/ventas/pedidos")]
[Authorize]
public class PedidosOperacionesController : PermisoControllerBase
{
    private static readonly string[] PermisoProcesar = { "ventas.pedidos.procesar" };
    private static readonly string[] PermisoCancelar = { "ventas.pedidos.cancelar" };
    private static readonly string[] PermisoModificar = { "ventas.pedidos.modificar" };

    private readonly IPedidosOperacionesService _service;

    public PedidosOperacionesController(
        IPedidosOperacionesService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _service = service;
    }

    /// <summary>
    /// Procesa pedidos de venta y los convierte en remisiones.
    ///
    /// NO es atomico, igual que en Mac31: cada pedido se procesa por separado y
    /// un fallo a mitad deja hechos los anteriores. La respuesta trae un renglon
    /// por pedido con su resultado y su folio, asi que se ve exactamente que
    /// quedo hecho y que no — legacy devolvia solo los fallos.
    /// </summary>
    /// <response code="200">Un renglon por pedido, hechos y fallidos.</response>
    [HttpPost("ventas/procesar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ProcesarVentasAsync(
        [FromBody] PedidosProcesarVentaRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoProcesar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ProcesarVentasAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new
        {
            ok = true,
            message = $"{data.Hechos} procesados, {data.Fallidos} con problema.",
            data
        });
    }

    /// <summary>Procesa un pedido de compra y lo convierte en una compra.</summary>
    [HttpPost("compras/procesar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ProcesarCompraAsync(
        [FromBody] PedidoProcesarCompraRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoProcesar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.ProcesarCompraAsync(request, Equipo(), ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Compra procesada."), data });
    }

    /// <summary>
    /// Cancela uno o varios pedidos. Tampoco es atomico; la respuesta trae un
    /// renglon por pedido.
    /// </summary>
    [HttpPost("cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelarAsync(
        [FromBody] PedidosCancelarRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCancelar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.CancelarAsync(request, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new
        {
            ok = true,
            message = $"{data.Hechos} cancelados, {data.Fallidos} con problema.",
            data
        });
    }

    /// <summary>
    /// Ajusta cuantos cascos de un tipo lleva el pedido a credito. Devuelve el
    /// renglon ajustado y, en `tablasExtra`, el catalogo con las cantidades al
    /// dia.
    /// </summary>
    [HttpPost("usados-credito")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AjustarUsadosCreditoAsync(
        [FromBody] PedidoUsadoCreditoRequest? request, CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoModificar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { message = "Body requerido." });

        var data = await _service.AjustarUsadosCreditoAsync(request, ct);
        return Ok(new { ok = true, message = Mensaje(data.Hecho, "Cascos a credito ajustados."), data });
    }

    private static string Mensaje(bool hecho, string siSeHizo)
        => hecho ? siSeHizo : "El procedimiento rechazo la operacion.";
}
