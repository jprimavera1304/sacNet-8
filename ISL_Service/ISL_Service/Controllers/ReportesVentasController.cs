using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Reports;
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

    [HttpGet("acumuladores-productos/clientes")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(List<ReportesVentasClienteItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarClientes(
        [FromQuery] int? numero,
        [FromQuery] int? idCliente,
        CancellationToken ct)
    {
        var result = await _service.ConsultarClientesAsync(numero, idCliente, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpGet("remisiones/catalogos")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasCatalogosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCatalogosRemisiones(CancellationToken ct)
    {
        var result = await _service.ConsultarCatalogosRemisionesAsync(ct);
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
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("remisiones/generar")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarRemisiones(
        [FromBody] ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.GenerarRemisionesAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("folios/generar")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFolios(
        [FromBody] ReportesVentasFoliosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.GenerarFoliosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("facturas/generar")]
    [Authorize(Policy = "perm:reportes_acumuladores_productos.ver")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFacturas(
        [FromBody] ReportesVentasFacturasRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.GenerarFacturasAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

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

    [HttpGet("ReportesV3W")]
    [HttpGet("ReportesV2")]
    [HttpGet("acumuladores-productos/pantalla")]
    [AllowAnonymous]
    public async Task<IActionResult> VerAcumuladoresProductosPantalla([FromQuery] int psp, CancellationToken ct)
    {
        if (psp <= 0)
            return BadRequest("psp requerido.");

        try
        {
            var excel = await _service.GenerarReporteVentasExcelPorParametrosAsync(psp, ct);
            Response.Headers["Cache-Control"] = "no-store";
            return File(excel.Content, excel.ContentType, excel.FileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("salida Excel", StringComparison.OrdinalIgnoreCase))
        {
        }

        var result = await _service.ConsultarReporteVentasPorParametrosAsync(psp, ct);
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(result.Html, result.Orientacion, ct);
        Response.Headers["Cache-Control"] = "no-store";
        return File(pdf, "application/pdf");
    }

    private string BuildReportesV3WUrl(string parametrosLegacy)
    {
        var psp = Uri.EscapeDataString(parametrosLegacy);
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        return $"{Request.Scheme}://{Request.Host}{pathBase}/api/reportes/ventas/ReportesV3W?psp={psp}";
    }
}
