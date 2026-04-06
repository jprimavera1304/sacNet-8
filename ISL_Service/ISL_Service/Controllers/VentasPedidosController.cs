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

    public VentasPedidosController(IVentasPedidosService service)
    {
        _service = service;
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
        var data = await _service.AutorizarPedidosAsync(request, idUsuario, equipo, ct);
        return Ok(new { ok = true, message = "Proceso de autorización finalizado.", data });
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
