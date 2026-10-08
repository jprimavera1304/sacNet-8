using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  ══════════════════════════════════════════════════════════════════════
  LA CAPTURA DE PARTIDAS DE UNA NOTA DE CREDITO
  ══════════════════════════════════════════════════════════════════════

  Archivo nuevo. Va en:
      sacNet-8/ISL_Service/ISL_Service/Controllers/VentasNotaCreditoCapturaController.cs
  No toca ningun fichero existente.

  POR QUE EXISTE ESTE CONTROLADOR, SI YA HAY UNA CAPTURA DE PEDIDOS
  -----------------------------------------------------------------

  En Mac31, la nota de credito captura sus partidas en un PEDIDO de verdad:
  `VentasNotaCredito.cs:1782` inserta el detalle con el mismo servicio que la
  venta normal y `:2864` manda ese `IDPedido` al timbrado. No es un detalle de
  implementacion: el pedido SE IMPRIME en el comprobante. Comprobado abriendo
  una nota de credito real de MacZ (IDVentaFactura 52615, folio NCZ2136) por
  `GET /api/ventas/facturas/52615/documento?formato=pdf`: su ultimo renglon
  dice

      APLICA A PEDIDOS 116379

  Asi que la nota de credito tiene que capturar en un pedido, con el mismo
  procedimiento, o el documento sale incompleto.

  Lo natural seria reusar `/api/ventas/pedidos/captura`, y de hecho ESTE
  controlador reusa su servicio entero —no se duplica ni una regla de negocio—
  pero NO se pueden reusar sus endpoints, por dos motivos medidos:

  1. EL DOMICILIO. `VentasPedidoCapturaController.cs:178` exige
     `IDDomicilio > 0`. La nota de credito manda `IDDomicilio = 0`
     (`VentasNotaCredito.cs:1729`), porque una nota no se entrega en ningun
     sitio: no hay domicilio que elegir. Comprobado contra el endpoint de
     pedidos con el cuerpo de una nota:

         POST /api/ventas/pedidos/captura/detalle  {"idDomicilio":0,...}
         -> 400 {"ok":false,"message":"Cliente, domicilio, almacen y producto son requeridos."}

     Esa guardia esta BIEN para un pedido —una venta sin domicilio no se puede
     entregar— asi que no se relaja: se deja en paz y la nota pide por su lado.

  2. EL PERMISO. Toda la captura de pedidos va detras de
     `ventas.pedidos.crear|app_movil.pedidos`. Quien hace notas de credito
     tiene `facturacion.timbrar`, y no tiene por que poder levantar pedidos de
     venta: son dos trabajos distintos. Colgar la nota de ese permiso obligaria
     a dar de alta vendedores para poder acreditar una factura, que es
     exactamente al reves de lo que el permiso quiere decir.

  De ahi este controlador: las mismas operaciones, con el permiso de
  facturacion y sin la guardia de domicilio. El trabajo lo hace
  `IVentasPedidoCapturaService`, que es el mismo de siempre.

  VA EN SU PROPIO ARCHIVO, y no dentro de `VentasFacturacionController`, porque
  ese fichero lo estan tocando otros agentes a la vez; un archivo nuevo no
  choca con nadie.

  ──────────────────────────────────────────────────────────────────────
  EL `Tipo` NO LO ELIGE EL CLIENTE
  ──────────────────────────────────────────────────────────────────────

  `@Tipo` del procedimiento vale 1 (nota de credito) o 2 (nota de credito de
  cascos), y lo decide `cboTipoProducto` en Mac31 (`:1765-1770`). Aqui llega
  como `cascos: true|false` y el numero se pone en el servidor. Si el cliente
  mandara el `tipo` crudo podria colar un 0, y lo que se simularia seria una
  VENTA —con su apartado de inventario— desde una pantalla de facturacion.
*/
[ApiController]
[Route("api/ventas/facturas/nota-credito")]
[Authorize]
public class VentasNotaCreditoCapturaController : PermisoControllerBase
{
    /*
      Las mismas dos claves que el timbrado: quien puede timbrar la nota puede
      capturar sus partidas, y no hace falta una clave nueva (en una familia que
      viene de Mac31 las claves inventadas se borran en el siguiente recalculo
      del snapshot de permisos).
    */
    private static readonly string[] PermisoCapturar = { "facturacion.timbrar", "ventas.facturar" };

    /* El tipo de documento que manda Mac31: `enumTipoDocumento.Remision`. */
    private const int IdTipoDocumentoRemision = 5;

    /* `enumTipoFactura`: 1 nota de credito, 2 nota de credito de cascos. */
    private const int TipoNotaCredito = 1;
    private const int TipoNotaCreditoCascos = 2;

    private readonly IVentasPedidoCapturaService _captura;

    public VentasNotaCreditoCapturaController(
        IVentasPedidoCapturaService captura,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        _captura = captura;
    }

    /// <summary>
    /// Los catalogos de la captura (empresas, almacenes) y el borrador que el
    /// usuario tenga abierto.
    /// </summary>
    [HttpGet("captura")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CapturaAsync(CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCapturar, ct);
        if (veto is not null) return veto;

        var data = await _captura.BootstrapAsync(IdUsuarioLegacy(), ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Captura de la nota de credito.", data });
    }

    /// <summary>
    /// Tira el borrador de pedido que haya quedado colgado.
    ///
    /// Mac31 lo hace al ABRIR la pantalla (`VentasNotaCredito.cs:1653`,
    /// `DELETE api/pedidos`), no al cerrarla, y eso deja un pedido huerfano cada
    /// vez que alguien cierra la ventana a medias. Se mantiene el momento —al
    /// abrir— porque es el que garantiza empezar en limpio, y ademas la pantalla
    /// nueva lo llama tambien al salir, que es lo que Mac31 no hace.
    /// </summary>
    [HttpDelete("borrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TirarBorradorAsync(CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCapturar, ct);
        if (veto is not null) return veto;

        var data = await _captura.EliminarBorradorAsync(IdUsuarioLegacy(), ct);
        return Ok(new { ok = true, message = "Borrador descartado.", data });
    }

    /// <summary>
    /// Busca productos para capturar una partida, por pagina.
    /// </summary>
    [HttpPost("productos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ProductosAsync(
        [FromBody] PedidoProductoPaginaRequest? request,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCapturar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        /*
          `modo` decide el filtro por categoria y fuera de ZARA no filtra nada
          (VentasPedidoCapturaRepository.ResolverIDGrupoCategoria). La nota de
          credito es como la venta normal: Mac31 abre el buscador de productos
          con `SoloAceites = 0` (`VentasNotaCredito.cs:1086`).
        */
        request.Modo = "normal";

        var data = await _captura.BuscarProductoPaginaAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new { ok = true, message = "Productos consultados.", data });
    }

    /// <summary>
    /// Agrega una partida a la nota de credito.
    ///
    /// Devuelve el pedido entero recalculado por el procedimiento: las
    /// partidas, las dos rejillas de usados y todos los totales. El cliente no
    /// suma nada.
    /// </summary>
    [HttpPost("partidas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AgregarPartidaAsync(
        [FromBody] NotaCreditoPartidaRequest? request,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCapturar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });

        if (request.IDCliente <= 0)
            return BadRequest(new { ok = false, message = "El cliente es requerido." });
        if (request.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "El almacen es requerido." });
        /*
          Con cascos, `IDProducto` NO es un producto: es el `IDTipoUsado` del
          casco (`VentasNotaCredito.cs:1766`). El mismo campo con otro
          significado, asi que la comprobacion es la misma pero el mensaje
          nombra lo que el usuario eligio.
        */
        if (request.IDProducto <= 0)
            return BadRequest(new
            {
                ok = false,
                message = request.Cascos ? "El tipo de casco es requerido." : "El producto es requerido."
            });
        if (request.Cantidad <= 0)
            return BadRequest(new { ok = false, message = "Cantidad invalida." });
        /*
          LA FECHA ES OBLIGATORIA, Y NO ES UN FORMALISMO.

          Sin ella, el procedimiento escribe el pedido con `Fecha` en
          **1900-01-01**. Medido llamando a este endpoint sin fecha: quedo el
          renglon 198708 de `dbo.Pedidos` fechado en 1900. No revienta, no avisa,
          y la partida se puede timbrar: una nota de credito fechada en 1900.

          Mac31 siempre la manda (`VentasNotaCredito.cs:1738`, con
          `FechaOperacionFtm`) y la pantalla nueva tambien, asi que vacia solo
          puede llegar de una llamada a mano o de un error del cliente. En los
          dos casos la respuesta correcta es un 400, no un renglon de 1900.
        */
        if (string.IsNullOrWhiteSpace(request.Fecha))
            return BadRequest(new { ok = false, message = "La fecha de operacion es requerida." });

        var paraElSp = new PedidoAgregarDetalleRequest
        {
            IDPedido = request.IDPedido,
            IDEmpresa = request.IDEmpresa,
            IDCliente = request.IDCliente,
            IDAlmacen = request.IDAlmacen,
            IDProducto = request.IDProducto,
            Cantidad = request.Cantidad,
            Observaciones = request.Observaciones ?? string.Empty,
            ObservacionesDetalle = string.Empty,
            ServicioPrecioConIva = request.ServicioPrecioConIva,
            IDEmpresaCS = request.IDEmpresaCS,
            Fecha = request.Fecha ?? string.Empty,

            /*
              Lo que Mac31 manda fijo en esta pantalla (`:1727-1775`):
              sin domicilio, sin agente que autorice, documento de remision, y
              SIEMPRE simulando — de ahi salen los precios y los descuentos que
              la pantalla ensena.
            */
            IDDomicilio = 0,
            IDAgenteAutoriza = 0,
            IDTipoDocumento = IdTipoDocumentoRemision,
            Simular = 1,
            SoloAceites = 0,
            PedidoSinCascosCambio = 0,

            /* El tipo lo pone el servidor; ver el encabezado del archivo. */
            Tipo = request.Cascos ? TipoNotaCreditoCascos : TipoNotaCredito
        };

        var data = await _captura.AgregarDetalleAsync(paraElSp, IdUsuarioLegacy(), Equipo(), ct);
        return Ok(new { ok = true, message = "Partida agregada.", data });
    }

    /// <summary>
    /// Quita una partida de la nota de credito.
    /// </summary>
    [HttpDelete("partidas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EliminarPartidaAsync(
        [FromBody] PedidoEliminarDetalleRequest? request,
        CancellationToken ct)
    {
        var veto = await ExigirPermisoAsync(PermisoCapturar, ct);
        if (veto is not null) return veto;
        if (request is null) return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IDPedido <= 0 || request.IDPedidoDetalle <= 0)
            return BadRequest(new { ok = false, message = "El pedido y la partida son requeridos." });

        /*
          Mac31 TIRA el resultado de este borrado (`:2343`) y sigue como si
          hubiera funcionado, asi que una partida que no se pudo quitar se
          timbra igual. Aqui el fallo del procedimiento sube como excepcion y
          llega a la pantalla: en una nota de credito, una partida de mas es un
          CFDI de mas.
        */
        request.SinModificarObservaciones = 1;

        var data = await _captura.EliminarDetalleAsync(request, ct);
        return Ok(new { ok = true, message = "Partida eliminada.", data });
    }
}

/// <summary>
/// Lo que la pantalla de nota de credito manda para agregar una partida.
///
/// Es un subconjunto de <see cref="PedidoAgregarDetalleRequest"/> a proposito:
/// los campos que la nota no usa (saldos, dias vencidos, logistica, facturar,
/// mayorista, descuento adicional) no se exponen, para que no se puedan mandar
/// desde fuera. Y `Tipo` no esta: lo decide el servidor con <see cref="Cascos"/>.
/// </summary>
public class NotaCreditoPartidaRequest
{
    /// <summary>0 la primera vez: el procedimiento crea el pedido y lo devuelve.</summary>
    public int IDPedido { get; set; }

    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDAlmacen { get; set; }

    /// <summary>El producto, o el IDTipoUsado del casco cuando <see cref="Cascos"/>.</summary>
    public int IDProducto { get; set; }

    public int Cantidad { get; set; } = 1;

    /// <summary>true = nota de credito de CASCOS (tipo 2) en vez de productos (tipo 1).</summary>
    public bool Cascos { get; set; }

    /// <summary>Solo para las partidas de servicio, que piden su precio con IVA.</summary>
    public decimal ServicioPrecioConIva { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>La fecha de operacion, como la manda Mac31 (`FechaOperacionFtm`).</summary>
    public string? Fecha { get; set; }

    public int IDEmpresaCS { get; set; }
}
