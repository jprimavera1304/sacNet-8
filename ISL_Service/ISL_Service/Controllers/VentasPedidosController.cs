using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/pedidos")]
[Authorize]
public class VentasPedidosController : ControllerBase
{
    // Ver la lista y soltar el pedido son permisos SEPARADOS: hay quien
    // necesita vigilar que no se acumulen pendientes sin poder autorizarlos.
    private const string PermisoVer = "ventas.pendientes.ver";
    private const string PermisoAutorizar = "ventas.pendientes.autorizar";

    private readonly IVentasPedidosService _service;
    private readonly IAutorizarPedidosAsyncCoordinator _asyncCoordinator;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPermissionService _permissionService;

    public VentasPedidosController(
        IVentasPedidosService service,
        IAutorizarPedidosAsyncCoordinator asyncCoordinator,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService)
    {
        _service = service;
        _asyncCoordinator = asyncCoordinator;
        _currentUserAccessor = currentUserAccessor;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Devuelve el 403 a regresar si al usuario le falta el permiso, o null si
    /// puede pasar.
    ///
    /// Hasta antes de esto, autorizar solo pedia estar logueado: el candado
    /// vivia unicamente en la pantalla, asi que cualquiera con sesion podia
    /// soltar pedidos llamando la API directo. Autorizar suelta credito y
    /// genera la venta; no puede depender de que la app esconda un boton.
    ///
    /// Se resuelve contra el token y no con [Authorize(Policy=...)] porque los
    /// permisos son por empresa y viven en la base: una politica estatica no
    /// sabe si el tenant siquiera lo tiene prendido.
    /// </summary>
    private async Task<IActionResult?> ExigirPermisoAsync(string permiso, CancellationToken ct)
    {
        var userId = _currentUserAccessor.GetUserId(User);
        if (userId is null)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        var empresaId = _currentUserAccessor.GetCompanyId(User) ?? 0;
        var rol = _currentUserAccessor.GetRole(User);
        var snapshot = await _permissionService.GetPermissionsAsync(userId.Value, empresaId, rol, ct);

        // Instalacion sin modelo de permisos: no hay nada contra que comparar
        // y negar dejaria sin autorizar a bases que hoy funcionan. Pasa, igual
        // que antes de que este control existiera.
        if (!snapshot.PermissionsEnabled)
            return null;

        var tiene = snapshot.Permissions.Any(x => string.Equals(x, permiso, StringComparison.OrdinalIgnoreCase));
        if (tiene)
            return null;

        return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, message = "No tienes permiso para esta accion." });
    }

    [HttpPost("pendientes-autorizar/consultar")]
    public async Task<IActionResult> ConsultarPendientesAutorizar([FromBody] ConsultaVentasPedidosRequest? request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisoVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPendientesAutorizarAsync(request ?? new ConsultaVentasPedidosRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pendientes consultados.", data });
    }

    [HttpPost("autorizar")]
    public async Task<IActionResult> Autorizar([FromBody] AutorizarPedidosRequest? request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisoAutorizar, ct);
        if (sinPermiso != null) return sinPermiso;

        if (request == null)
            return BadRequest(new { message = "Body requerido." });
        if (request.IdsPedido == null || request.IdsPedido.Count == 0)
            return BadRequest(new { message = "idsPedido es requerido." });

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var equipo = _currentUserAccessor.GetUsername(User, Environment.MachineName);

        if (request.AsyncMode)
        {
            var op = _asyncCoordinator.Start(request, idUsuario, equipo);
            return Accepted(new { ok = true, message = "Autorizacion en proceso.", data = op });
        }

        var data = await _service.AutorizarPedidosAsync(request, idUsuario, equipo, ct);
        var hasErrors = (data.Pedidos?.Count ?? 0) > 0;
        return Ok(new
        {
            ok = !hasErrors,
            message = hasErrors ? "Autorizacion finalizada con errores." : "Autorizacion finalizada correctamente.",
            data
        });
    }

    [HttpGet("autorizar/status/{operationId}")]
    public async Task<IActionResult> AutorizarStatus([FromRoute] string operationId, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisoAutorizar, ct);
        if (sinPermiso != null) return sinPermiso;

        var status = _asyncCoordinator.GetStatus(operationId);
        if (status == null)
            return NotFound(new { ok = false, message = "Operacion no encontrada." });
        return Ok(new { ok = true, message = "Estatus de autorizacion.", data = status });
    }
}
