using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/reportes/ventas")]
[Authorize]
public class ReportesVentasController : ControllerBase
{
    private readonly IReportesVentasService _service;

    public ReportesVentasController(IReportesVentasService service)
    {
        _service = service;
    }

    [HttpPost("acumuladores-productos/vista-previa")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarAcumuladoresProductosAsync(request, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }
}
