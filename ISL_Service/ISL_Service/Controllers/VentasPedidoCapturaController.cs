using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
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
    private readonly IPermissionService _permissionService;

    public VentasPedidoCapturaController(IVentasPedidoCapturaService service, ICurrentUserAccessor currentUserAccessor, IConfiguration configuration, IPermissionService permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
        _permissionService = permissionService;
    }

    // Blindaje de propiedad para usuarios "solo movil" (repartidores).
    //
    // Los sp_n_ de captura NO validan que el pedido sea del usuario del token:
    // cualquiera con token puede leer, editar y hasta apropiarse del pedido de
    // otro pasando un idPedido ajeno (el PUT reescribe IDUsuario). Como esos SP
    // son legacy e intocables, la validacion se hace aqui.
    //
    // Solo se restringe a repartidores. Oficina (ventas.pedidos.crear) y
    // SuperAdmin conservan su comportamiento actual, para no destabilizar el web.
    // Un repartidor solo puede tocar pedidos SUYOS, y solo en borrador (0) o
    // pendiente (1): no un pedido ya procesado.
    //
    // Devuelve un IActionResult de error si hay que rechazar; null si esta bien.
    // Canal/equipo que queda registrado en el pedido. Si el cliente lo declara
    // (MOVIL / WEB) se usa eso; si no, se conserva el comportamiento anterior:
    // el nombre de usuario/maquina. Se recorta a 100 (tamaño de la columna).
    private string ResolveEquipo(string? requestEquipo)
    {
        var equipo = string.IsNullOrWhiteSpace(requestEquipo)
            ? _currentUserAccessor.GetUsername(User, Environment.MachineName)
            : requestEquipo.Trim();
        return equipo.Length > 100 ? equipo[..100] : equipo;
    }

    private async Task<IActionResult?> ValidarPropiedadPedidoAsync(int idPedido, CancellationToken ct)
    {
        if (idPedido <= 0) return null; // pedido nuevo, aun no tiene dueño
        if (!await EsSoloMovilAsync(ct)) return null; // oficina / super / legacy

        var propiedad = await _service.ObtenerPropiedadPedidoAsync(idPedido, ct);
        if (propiedad == null)
            return NotFound(new { ok = false, message = "Pedido no encontrado." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        if (propiedad.IdUsuario != idUsuario)
            return StatusCode(403, new { ok = false, message = "Este pedido no es tuyo." });

        if (propiedad.IdStatus != 0 && propiedad.IdStatus != 1)
            return StatusCode(403, new { ok = false, message = "Este pedido ya no se puede modificar." });

        return null;
    }

    // True si quien llama es un usuario "solo movil": no SuperAdmin y sin el
    // permiso de oficina ventas.pedidos.crear. En modo legacy (capacidades
    // apagadas) no se restringe, para no cambiar el comportamiento actual.
    private async Task<bool> EsSoloMovilAsync(CancellationToken ct)
    {
        if (CurrentUser.IsSuperAdmin(User)) return false;

        var userId = CurrentUser.GetUserId(User);
        var empresaId = CurrentUser.GetEmpresaId(User);
        var rolLegacy = User.FindFirst("rolLegacy")?.Value ?? CurrentUser.GetRol(User);

        var snapshot = await _permissionService.GetPermissionsAsync(userId, empresaId, rolLegacy, ct);
        if (!snapshot.PermissionsEnabled) return false;

        var esOficina = snapshot.Permissions.Any(p =>
            string.Equals(p, "ventas.pedidos.crear", StringComparison.OrdinalIgnoreCase));
        return !esOficina;
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
        var rechazo = await ValidarPropiedadPedidoAsync(idPedido, ct);
        if (rechazo != null) return rechazo;

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

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct);
        if (rechazo != null) return rechazo;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = ResolveEquipo(request.Equipo);
        var data = await _service.AgregarDetalleAsync(request, idUsuario, equipo, ct);
        return Ok(new { ok = true, message = "Producto agregado.", data });
    }

    [HttpDelete("detalle")]
    [Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]
    public async Task<IActionResult> EliminarDetalle([FromBody] PedidoEliminarDetalleRequest? request, CancellationToken ct)
    {
        if (request == null || request.IDPedidoDetalle <= 0)
            return BadRequest(new { ok = false, message = "IDPedidoDetalle requerido." });

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct);
        if (rechazo != null) return rechazo;

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

        var rechazo = await ValidarPropiedadPedidoAsync(request.IDPedido, ct);
        if (rechazo != null) return rechazo;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = ResolveEquipo(request.Equipo);
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
