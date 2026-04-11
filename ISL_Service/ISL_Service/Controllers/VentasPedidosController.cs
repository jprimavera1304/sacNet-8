using System.Security.Claims;
using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/pedidos")]
[Authorize]
public class VentasPedidosController : ControllerBase
{
    private readonly IVentasPedidosService _service;
    private readonly IAutorizarPedidosAsyncCoordinator _asyncCoordinator;

    public VentasPedidosController(IVentasPedidosService service, IAutorizarPedidosAsyncCoordinator asyncCoordinator)
    {
        _service = service;
        _asyncCoordinator = asyncCoordinator;
    }

    [HttpPost("pendientes-autorizar/consultar")]
    public async Task<IActionResult> ConsultarPendientesAutorizar([FromBody] ConsultaVentasPedidosRequest? request, CancellationToken ct)
    {
        var idUsuario = ParseLegacyUserIdClaim(User);
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

        var idUsuario = ParseLegacyUserIdClaim(User);
        var equipo = ResolveEquipo(User);

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

    private static int ParseLegacyUserIdClaim(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("idUsuario");
        return int.TryParse(raw, out var idUsuario) && idUsuario > 0 ? idUsuario : 0;
    }

    private static string ResolveEquipo(ClaimsPrincipal principal)
    {
        var u = principal.FindFirstValue("username")
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("name")
            ?? principal.FindFirstValue("unique_name")
            ?? principal.Identity?.Name;
        return string.IsNullOrWhiteSpace(u) ? Environment.MachineName : u.Trim();
    }
}
