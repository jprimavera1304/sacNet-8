using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/*
  ══════════════════════════════════════════════════════════════════════
  LA CAPTURA DE UNA COMPRA (NUEVA / MODIFICAR)
  ══════════════════════════════════════════════════════════════════════

  ES LA FORMA `Compras.cs` DE MAC31, que abren los botones NUEVA (F3) y
  MODIFICAR (F5) de CONSULTA DE COMPRAS.

  UNA COMPRA ES UN PEDIDO PROCESADO, Y ESO EXPLICA TODO LO DEMAS.

  En Mac31 la captura no escribe en `Compras`: va metiendo renglones en un
  PEDIDO con `sp_n_InsertarPedidoDetalle` -el pedido se crea solo con el primer
  renglon- y al guardar llama a `sp_n_ProcesarCompra`, que es quien convierte
  ese pedido en la compra, mueve el inventario y calcula los totales. Por eso
  MODIFICAR solo se ofrece cuando la compra VIENE DE UN PEDIDO
  (`IDPedido != 0`, ConsultarCompras.cs:646): si no hay pedido detras, no hay
  nada que volver a procesar.

  POR QUE UN CONTROLADOR PROPIO Y NO EL DE LA CAPTURA DE VENTAS
  ------------------------------------------------------------
  Los PROCEDIMIENTOS son los mismos -`sp_n_InsertarPedidoDetalle`,
  `sp_n_EliminarPedidoDetalle`, `sp_n_ConsultaPedido`-, pero los PERMISOS no.
  `api/ventas/pedidos/captura` pide `ventas.pedidos.crear` o
  `app_movil.pedidos`, que son el vendedor de oficina y el repartidor. Quien
  captura compras tiene `compras.editar`, que es como se hereda el proceso
  `btnModificar` de la forma CONSULTA DE COMPRAS, y no tiene por que tener
  ninguno de los otros dos. Colgar la captura de compras de aquella puerta
  habria obligado a darle a los compradores un permiso de ventas: justo lo que
  no se hace.

  Lo que cambia en los datos es poco y esta en un solo sitio, el renglon:
  Mac31 manda el PROVEEDOR y la factura con sus dos fechas, y no manda cliente
  (Compras.cs:1105, :1115-1117). El domicilio es el 1, que en esa forma esta
  escrito como "Domicilio Dummy" (Compras.cs:40), y el tipo de documento es 11
  (`enumTipoDocumento.Compra`).
*/
[ApiController]
[Route("api/compras/captura")]
[Authorize]
public class ComprasCapturaController : ControllerBase
{
    private readonly IVentasPedidoCapturaService _captura;
    private readonly IPedidosOperacionesService _operaciones;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ComprasCapturaController(
        IVentasPedidoCapturaService captura,
        IPedidosOperacionesService operaciones,
        ICurrentUserAccessor currentUserAccessor)
    {
        _captura = captura;
        _operaciones = operaciones;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>El tipo de documento de una compra en `dbo.TiposDocumento`.</summary>
    private const int TipoDocumentoCompra = 11;

    /// <summary>El domicilio que Mac31 llama "dummy" en la captura de compras.</summary>
    private const int DomicilioDeCompra = 1;

    private string Equipo(string? delCliente)
    {
        var equipo = string.IsNullOrWhiteSpace(delCliente)
            ? _currentUserAccessor.GetUsername(User, Environment.MachineName)
            : delCliente.Trim();
        return equipo.Length > 100 ? equipo[..100] : equipo;
    }

    /// <summary>Empresas, almacenes del usuario y la fecha de operacion.</summary>
    [HttpGet("bootstrap")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _captura.BootstrapAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Captura de compra inicializada.", data });
    }

    /// <summary>El pedido que hay detras de la compra, con sus renglones y totales.</summary>
    [HttpGet("{idPedido:int}")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> Obtener([FromRoute] int idPedido, CancellationToken ct)
    {
        if (idPedido <= 0)
            return BadRequest(new { ok = false, message = "El pedido es requerido." });

        var data = await _captura.ConsultarPedidoAsync(idPedido, ct);
        return Ok(new { ok = true, message = "Pedido consultado.", data });
    }

    /// <summary>Busca el producto por clave dentro de un almacen.</summary>
    [HttpPost("productos/buscar")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> BuscarProducto(
        [FromBody] PedidoProductoBuscarRequest? request, CancellationToken ct)
    {
        var safe = request ?? new PedidoProductoBuscarRequest();
        if (safe.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Seleccione almacen." });

        var data = await _captura.BuscarProductoAsync(safe, ct);
        return Ok(new { ok = true, message = "Producto consultado.", data });
    }

    /*
      AGREGA UN RENGLON, Y SI NO HABIA PEDIDO LO CREA.

      Es exactamente lo que hace Mac31: `cur_idPedido` empieza en 0 y el
      procedimiento devuelve el id del pedido que acaba de abrir
      (Compras.cs:1135). Por eso `idPedido = 0` NO es un error aqui.

      El cliente, el domicilio y el tipo de documento NO se aceptan del cuerpo:
      los fija el servidor. Son lo que distingue un renglon de compra de uno de
      venta, y dejarlos entrar seria dejar capturar una VENTA con el permiso de
      compras.
    */
    [HttpPost("detalle")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> AgregarDetalle(
        [FromBody] ComprasCapturaDetalleRequest? request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IDProveedor <= 0)
            return BadRequest(new { ok = false, message = "Seleccione el proveedor." });
        if (request.IDAlmacen <= 0 || request.IDProducto <= 0)
            return BadRequest(new { ok = false, message = "Almacen y producto son requeridos." });
        if (request.Cantidad <= 0)
            return BadRequest(new { ok = false, message = "Cantidad invalida." });

        var paraElSp = new PedidoAgregarDetalleRequest
        {
            IDPedido = request.IDPedido,
            IDEmpresa = request.IDEmpresa,
            IDAlmacen = request.IDAlmacen,
            IDProducto = request.IDProducto,
            Cantidad = request.Cantidad,
            Fecha = request.Fecha,
            Observaciones = request.Observaciones,
            ObservacionesDetalle = request.ObservacionesDetalle,
            IDEmpresaCS = request.IDEmpresaCS,

            // Lo que hace que el renglon sea de una compra.
            IDProveedor = request.IDProveedor,
            Factura = request.Factura,
            FechaEmision = request.FechaEmision,
            FechaVencimiento = request.FechaVencimiento,
            IDCliente = 0,
            IDDomicilio = DomicilioDeCompra,
            IDTipoDocumento = TipoDocumentoCompra
        };

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _captura.AgregarDetalleAsync(paraElSp, idUsuario, Equipo(request.Equipo), ct);
        return Ok(new { ok = true, message = "Producto agregado.", data });
    }

    /// <summary>Quita un renglon del pedido de la compra.</summary>
    [HttpDelete("detalle")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> EliminarDetalle(
        [FromBody] PedidoEliminarDetalleRequest? request, CancellationToken ct)
    {
        if (request is null || request.IDPedidoDetalle <= 0)
            return BadRequest(new { ok = false, message = "IDPedidoDetalle requerido." });

        var data = await _captura.EliminarDetalleAsync(request, ct);
        return Ok(new { ok = true, message = "Producto eliminado.", data });
    }

    /*
      GUARDA: convierte el pedido en la compra.

      Es el `btnGuardar_Click` de Mac31 entero: primero `sp_n_ProcesarCompra`
      (ProcesarCompra, Compras.cs:1272) y despues la marca de FACTURA o NOTA
      (ActualizarCompra, :1318). Los dos pasos van juntos porque en la forma
      tambien van juntos: el radio de factura/nota se guarda siempre, con el
      mismo boton.

      La marca se manda al endpoint que ya existia para ella
      (`compras/{id}/factura`), no se duplica aqui: es la misma operacion que
      hace el boton "Marcar como factura" de la consulta.
    */
    [HttpPost("procesar")]
    [Authorize(Policy = "perm:compras.editar")]
    public async Task<IActionResult> Procesar(
        [FromBody] PedidoProcesarCompraRequest? request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { ok = false, message = "Body requerido." });

        var data = await _operaciones.ProcesarCompraAsync(request, Equipo(null), ct);
        return Ok(new
        {
            ok = true,
            message = string.IsNullOrWhiteSpace(data.Mensaje) ? "Compra procesada." : data.Mensaje,
            data
        });
    }
}

/*
  EL RENGLON QUE MANDA LA PANTALLA DE CAPTURA DE COMPRAS.

  Es un DTO propio y no el de la captura de ventas a proposito: aquel tiene
  cuarenta campos -saldos del cliente, dias vencidos, servicios, mayorista- que
  en una compra no significan nada, y aceptarlos seria ofrecer una puerta para
  capturar otra cosa con este permiso. Aqui estan los que Mac31 llena en
  `AgregarPedidoDetalle` de Compras.cs y ninguno mas.
*/
public class ComprasCapturaDetalleRequest
{
    /// <summary>0 la primera vez: el procedimiento abre el pedido y devuelve su id.</summary>
    public int IDPedido { get; set; }

    public int IDEmpresa { get; set; }
    public int IDProveedor { get; set; }
    public int IDAlmacen { get; set; }
    public int IDProducto { get; set; }
    public int Cantidad { get; set; } = 1;

    /// <summary>La fecha de operacion de la empresa, como la manda Mac31.</summary>
    public string? Fecha { get; set; }

    public string? Factura { get; set; }
    public string? FechaEmision { get; set; }
    public string? FechaVencimiento { get; set; }
    public string? Observaciones { get; set; }
    public string? ObservacionesDetalle { get; set; }
    public int IDEmpresaCS { get; set; }
    public string? Equipo { get; set; }
}
