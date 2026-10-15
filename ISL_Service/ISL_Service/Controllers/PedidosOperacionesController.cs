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
    private static readonly string[] PermisoProcesar =
        { "ventas.pedidos.procesar", "app_movil.ventas.pedidos.procesar" };

    /*
      AUTORIZAR Y RECHAZAR SON DECISIONES APARTE, NO BANDERAS DEL CUERPO.

      Hasta el 2026-10-12 este endpoint solo pedia `ventas.pedidos.procesar`, y
      las banderas `Autorizar` y `Rechazar` del cuerpo no sumaban ninguna
      comprobacion. O sea que quien podia procesar un pedido podia tambien
      AUTORIZAR uno retenido por saldo vencido -que es la decision que la
      retencion existe para pedir- y RECHAZARLO, que es irreversible. El front
      si distinguia; la decision vivia en el navegador.

      Se piden las dos claves de autorizar porque hay dos pantallas para lo
      mismo y cada una uso la suya: la captura manda aqui con
      `ventas.pedidos.autorizar` y la de pendientes llama a otro endpoint con
      `ventas.pendientes.autorizar` (VentasPedidosController). Aceptar las dos
      evita elegir un perdedor entre gente que hoy autoriza de verdad, y el par
      del movil va incluido como en el resto del backend.

      Medido antes de ponerlo, en las dos empresas: todos los que tienen
      `ventas.pedidos.procesar` tienen ya `autorizar` y `rechazar` -JUAN y DEMO
      en MacZ, DEMO en Produccion_svr-, asi que esto no le quita la operacion a
      nadie.
    */
    private static readonly string[] PermisoAutorizar =
    {
        "ventas.pedidos.autorizar", "ventas.pendientes.autorizar", "app_movil.ventas.autorizar"
    };

    private static readonly string[] PermisoRechazar = { "ventas.pedidos.rechazar" };
    private static readonly string[] PermisoCancelar =
        { "ventas.pedidos.cancelar", "app_movil.ventas.pedidos.cancelar" };
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

        // Se mira el cuerpo DESPUES de comprobar que puede procesar: las dos
        // banderas amplian lo que se va a hacer, no lo sustituyen.
        if (request.Autorizar)
        {
            var vetoAutorizar = await ExigirPermisoAsync(PermisoAutorizar, ct);
            if (vetoAutorizar is not null) return vetoAutorizar;
        }

        if (request.Rechazar)
        {
            var vetoRechazar = await ExigirPermisoAsync(PermisoRechazar, ct);
            if (vetoRechazar is not null) return vetoRechazar;
        }

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
