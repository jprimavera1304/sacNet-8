using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardVentasController : ControllerBase
{
    private readonly IDashboardVentasService _service;
    private readonly IDashboardVentasReportService _reportService;

    public DashboardVentasController(IDashboardVentasService service, IDashboardVentasReportService reportService)
    {
        _service = service;
        _reportService = reportService;
    }

    [HttpPost("filtros/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(DashboardVentasFiltrosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarFiltros([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarFiltrosAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("kpis/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(DashboardVentasKpisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarKpis([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarKpisAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("ventas/serie-mensual/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardVentasSerieMensualDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarSerieMensual([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarSerieMensualAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    /// <summary>
    /// Serie semanal por mes. Semanas fijas: 1-7, 8-14, 15-21, 22-28, 29-fin.
    /// </summary>
    [HttpPost("ventas/serie-semanal/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardVentasSerieSemanalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarSerieSemanal([FromBody] DashboardVentasSerieSemanalRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarSerieSemanalAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("top-productos/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardTopProductoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarTopProductos([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarTopProductosAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("top-clientes/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardTopClienteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarTopClientes([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarTopClientesAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("top-categorias/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardTopCategoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarTopCategorias([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarTopCategoriasAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("top-marcas/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardTopMarcaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarTopMarcas([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarTopMarcasAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("almacenes/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardVentasAlmacenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarVentasAlmacenes([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarVentasAlmacenesAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("agentes/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(List<DashboardVentasAgenteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarVentasAgentes([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarVentasAgentesAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }

    [HttpPost("detalle/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(DashboardVentasDetallePagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarVentasDetalle([FromBody] DashboardVentasDetalleRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarVentasDetalleAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }


    [HttpPost("reporte/generar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarReporte([FromBody] DashboardVentasReporteRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var file = await _reportService.GenerarReporteAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
    [HttpPost("overview/consultar")]
    [Authorize(Policy = "perm:dashboardventas.ver")]
    [ProducesResponseType(typeof(DashboardVentasOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarOverview([FromBody] DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        var result = await _service.ConsultarOverviewAsync(request, ct);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }
}


