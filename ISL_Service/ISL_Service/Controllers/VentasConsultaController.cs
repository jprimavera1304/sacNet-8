using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

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

    /*
      Prepara el reporte de una o varias remisiones y devuelve la direccion a la
      que hay que ir. NO devuelve el reporte: lo pinta MacReportes, la misma
      aplicacion que abre Mac31, y por eso sale identico.

      El id de usuario sale del TOKEN y no del cuerpo de la peticion. En Mac31 lo
      manda el cliente porque el cliente es de confianza; aqui no lo es, y ese id
      es el que queda escrito como quien pidio el reporte.
    */
    [HttpPost("reporte")]
    public async Task<IActionResult> Reporte([FromBody] VentasReporteRequest? request, CancellationToken ct)
    {
        var idUsuario = _currentUserAccessor.GetLegacyUserId(User);
        var data = await _service.PrepararReporteAsync(request ?? new VentasReporteRequest(), idUsuario, ct);

        if (!data.Ok)
            return BadRequest(new { ok = false, message = data.Message });

        return Ok(new { ok = true, message = "Reporte listo.", data });
    }
}
