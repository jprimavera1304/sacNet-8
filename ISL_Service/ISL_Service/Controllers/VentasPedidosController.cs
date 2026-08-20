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
    //
    // Y ademas van separados POR CANAL: el del web y el del movil son permisos
    // distintos aunque hagan lo mismo. Se puede dar el acceso en la
    // computadora y no en el telefono, o al reves, sin que uno encienda al
    // otro. Cada pantalla mira el suyo (la app, app_movil.ventas.*; el web,
    // ventas.pendientes.*).
    //
    // Aqui basta con cualquiera de los dos porque el endpoint es el mismo para
    // los dos canales: quien no tiene el de su canal no llega a llamarlo,
    // porque su pantalla ni siquiera le muestra la opcion.
    private static readonly string[] PermisosVer =
        { "ventas.pendientes.ver", "app_movil.ventas.ver" };
    private static readonly string[] PermisosAutorizar =
        { "ventas.pendientes.autorizar", "app_movil.ventas.autorizar" };

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
    private async Task<IActionResult?> ExigirPermisoAsync(string[] permisos, CancellationToken ct)
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

        var tiene = snapshot.Permissions.Any(
            x => permisos.Any(p => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)));
        if (tiene)
            return null;

        return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, message = "No tienes permiso para esta accion." });
    }

    [HttpPost("pendientes-autorizar/consultar")]
    public async Task<IActionResult> ConsultarPendientesAutorizar([FromBody] ConsultaVentasPedidosRequest? request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosVer, ct);
        if (sinPermiso != null) return sinPermiso;

        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPendientesAutorizarAsync(request ?? new ConsultaVentasPedidosRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pendientes consultados.", data });
    }

    [HttpPost("autorizar")]
    public async Task<IActionResult> Autorizar([FromBody] AutorizarPedidosRequest? request, CancellationToken ct)
    {
        var sinPermiso = await ExigirPermisoAsync(PermisosAutorizar, ct);
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
        var sinPermiso = await ExigirPermisoAsync(PermisosAutorizar, ct);
        if (sinPermiso != null) return sinPermiso;

        var status = _asyncCoordinator.GetStatus(operationId);
        if (status == null)
            return NotFound(new { ok = false, message = "Operacion no encontrada." });
        return Ok(new { ok = true, message = "Estatus de autorizacion.", data = status });
    }
}
