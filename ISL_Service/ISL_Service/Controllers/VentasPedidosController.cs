using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/pedidos")]
[Authorize]
public class VentasPedidosController : ControllerBase
{
    private readonly IVentasPedidosService _service;
    private readonly IAutorizarPedidosAsyncCoordinator _asyncCoordinator;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public VentasPedidosController(IVentasPedidosService service, IAutorizarPedidosAsyncCoordinator asyncCoordinator, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _asyncCoordinator = asyncCoordinator;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("pendientes-autorizar/consultar")]
    public async Task<IActionResult> ConsultarPendientesAutorizar([FromBody] ConsultaVentasPedidosRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPendientesAutorizarAsync(request ?? new ConsultaVentasPedidosRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pendientes consultados.", data });
    }

    [HttpPost("autorizar")]
    public async Task<IActionResult> Autorizar([FromBody] AutorizarPedidosRequest? request, CancellationToken ct)
    {
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
    public IActionResult AutorizarStatus([FromRoute] string operationId)
    {
        var status = _asyncCoordinator.GetStatus(operationId);
        if (status == null)
            return NotFound(new { ok = false, message = "Operacion no encontrada." });
        return Ok(new { ok = true, message = "Estatus de autorizacion.", data = status });
    }
}
