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
    private static readonly System.Text.UTF8Encoding HtmlEncoding = new(false);

    public ReportesVentasController(IReportesVentasService service)
    {
        _service = service;
    }

    [HttpGet("acumuladores-productos/catalogos")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasCatalogosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCatalogosAcumuladoresProductos(
        [FromQuery] int? idGrupoCategoria,
        [FromQuery] int[]? idCategorias,
        CancellationToken ct)
    {
        var result = await _service.ConsultarCatalogosAcumuladoresProductosAsync(
            idGrupoCategoria,
            idCategorias ?? Array.Empty<int>(),
            ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("acumuladores-productos/generar")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.GenerarAcumuladoresProductosAsync(request, ct);
        result.Url = Url.ActionLink(
            nameof(VerAcumuladoresProductosPantalla),
            values: new { psp = result.ParametrosLegacy }) ?? $"/api/reportes/ventas/acumuladores-productos/pantalla?psp={Uri.EscapeDataString(result.ParametrosLegacy)}";

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
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

    [HttpGet("acumuladores-productos/pantalla")]
    [AllowAnonymous]
    [Produces("text/html")]
    public async Task<IActionResult> VerAcumuladoresProductosPantalla([FromQuery] int psp, CancellationToken ct)
    {
        if (psp <= 0)
            return BadRequest("psp requerido.");

        var result = await _service.ConsultarAcumuladoresProductosPorParametrosAsync(psp, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Content(result.Html, "text/html", HtmlEncoding);
    }
}
