using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Backend.Core.Abstractions;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/ventas/consulta")]
[Authorize]
public class VentasConsultaController : ControllerBase
{
    private readonly IVentasConsultaService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public VentasConsultaController(IVentasConsultaService service, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken ct)
    {
        var data = await _service.ConsultarCatalogosAsync(ct);
        return Ok(new { ok = true, message = "Catalogos consultados.", data });
    }

    [HttpPost("remisiones")]
    public async Task<IActionResult> Remisiones([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarRemisionesAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Remisiones consultadas.", data });
    }

    [HttpPost("pedidos")]
    public async Task<IActionResult> Pedidos([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPedidosAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pedidos consultados.", data });
    }

    [HttpPost("pendientes-imprimir")]
    public async Task<IActionResult> PendientesImprimir([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPendientesImprimirAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pendientes de imprimir consultados.", data });
    }

    [HttpPost("pagos")]
    public async Task<IActionResult> Pagos([FromBody] VentasConsultaRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.ConsultarPagosAsync(request ?? new VentasConsultaRequest(), idUsuario, ct);
        return Ok(new { ok = true, message = "Pagos consultados.", data });
    }
}
