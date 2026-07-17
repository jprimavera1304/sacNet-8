using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

// Captura de pedidos. La usan DOS superficies con catalogos de permiso
// distintos, y por eso las policies son compuestas ("a|b" = basta con una):
//
//   - Oficina, desde el web  -> ventas.pedidos.crear   (Web\QR_Web ... pedido.api.js
//                                                       pega a estos mismos endpoints)
//   - Repartidor, desde el movil -> app_movil.pedidos
//
// Exigir solo uno deja fuera al otro: con solo ventas.pedidos.crear el
// repartidor recibia 403 en todo y la app se quedaba en blanco; con solo
// app_movil.pedidos se rompia la captura del web.
//
// NO se resolvio dandole ventas.pedidos.crear al repartidor porque ese permiso,
// por si solo, tambien le abre la pantalla de captura del web (pedido.main.js
// entra con un OR) y ademas lo lee el Mac31 de produccion (btnNuevoPedido).
[ApiController]
[Route("api/ventas/pedidos/captura")]
[Authorize]
public class VentasPedidoCapturaController : ControllerBase
{
    private readonly IVentasPedidoCapturaService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IConfiguration _configuration;

    public VentasPedidoCapturaController(IVentasPedidoCapturaService service, ICurrentUserAccessor currentUserAccessor, IConfiguration configuration)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
    }

    [HttpGet("bootstrap")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.BootstrapAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Nuevo pedido inicializado.", data });
    }

    [HttpGet("{idPedido:int}")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> Obtener([FromRoute] int idPedido, CancellationToken ct)
    {
        var data = await _service.ConsultarPedidoAsync(idPedido, ct);
        return Ok(new { ok = true, message = "Pedido consultado.", data });
    }

    [HttpPost("clientes/buscar")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> BuscarCliente([FromBody] PedidoClienteBuscarRequest? request, CancellationToken ct)
    {
        var data = await _service.BuscarClienteAsync(request ?? new PedidoClienteBuscarRequest(), ct);
        return Ok(new { ok = true, message = "Cliente consultado.", data });
    }

    [HttpPost("productos/buscar")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> BuscarProducto([FromBody] PedidoProductoBuscarRequest? request, CancellationToken ct)
    {
        var safe = request ?? new PedidoProductoBuscarRequest();
        if (safe.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Seleccione almacen." });

        var data = await _service.BuscarProductoAsync(safe, ct);
        return Ok(new { ok = true, message = "Producto consultado.", data });
    }

    // Version paginada de productos/buscar: devuelve solo productos con existencia
    // y solo las columnas que usa la app. productos/buscar sigue vivo para la app
    // publicada.
    [HttpPost("productos/pagina")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> BuscarProductoPagina([FromBody] PedidoProductoPaginaRequest? request, CancellationToken ct)
    {
        var safe = request ?? new PedidoProductoPaginaRequest();
        if (safe.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Seleccione almacen." });

        var modo = PedidoModo.Normalizar(safe.Modo);
        if (modo == null)
            return BadRequest(new { ok = false, message = "Modo invalido. Use 'normal' o 'aceites'." });

        // El server manda: aunque el bootstrap no ofrezca "aceites", el cliente
        // podria pedirlo de todos modos.
        if (modo == PedidoModo.Aceites && !_configuration.GetValue<bool>(PedidoModo.ConfigAceitesHabilitado))
            return BadRequest(new { ok = false, message = "El modo aceites no esta habilitado." });

        safe.Modo = modo;
        var data = await _service.BuscarProductoPaginaAsync(safe, ct);
        return Ok(new { ok = true, message = "Productos consultados.", data });
    }

    [HttpPost("detalle")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> AgregarDetalle([FromBody] PedidoAgregarDetalleRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });
        if (request.IDCliente <= 0 || request.IDDomicilio <= 0 || request.IDProducto <= 0 || request.IDAlmacen <= 0)
            return BadRequest(new { ok = false, message = "Cliente, domicilio, almacen y producto son requeridos." });
        if (request.Cantidad <= 0)
            return BadRequest(new { ok = false, message = "Cantidad invalida." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = _currentUserAccessor.GetUsername(User, Environment.MachineName);
        var data = await _service.AgregarDetalleAsync(request, idUsuario, equipo, ct);
        return Ok(new { ok = true, message = "Producto agregado.", data });
    }

    [HttpDelete("detalle")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> EliminarDetalle([FromBody] PedidoEliminarDetalleRequest? request, CancellationToken ct)
    {
        if (request == null || request.IDPedidoDetalle <= 0)
            return BadRequest(new { ok = false, message = "IDPedidoDetalle requerido." });

        var data = await _service.EliminarDetalleAsync(request, ct);
        return Ok(new { ok = true, message = "Producto eliminado.", data });
    }

    [HttpPut("{idPedido:int}")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> Guardar([FromRoute] int idPedido, [FromBody] PedidoGuardarRequest? request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { ok = false, message = "Body requerido." });
        request.IDPedido = idPedido;
        if (request.IDPedido <= 0 || request.IDDomicilio <= 0)
            return BadRequest(new { ok = false, message = "Pedido y domicilio son requeridos." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = _currentUserAccessor.GetUsername(User, Environment.MachineName);
        var data = await _service.GuardarAsync(request, idUsuario, equipo, ct);
        return Ok(new { ok = true, message = "Pedido guardado.", data });
    }

    [HttpDelete("borrador")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> EliminarBorrador(CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.EliminarBorradorAsync(idUsuario, ct);
        return Ok(new { ok = true, message = "Borrador eliminado.", data });
    }
}
