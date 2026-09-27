using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Reportes;
using ISL_Service.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISL_Service.Application.Security;

namespace ISL_Service.Controllers;

[ApiController]
[Route("api/reportes/ventas")]
[Authorize]
public class ReportesVentasController : PermisoControllerBase
{
    private readonly IReportesVentasService _service;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ReportesVentasController(
        IReportesVentasService service,
        ICurrentUserAccessor currentUserAccessor,
        IPermissionService permissionService)
        : base(currentUserAccessor, permissionService)
    {
        _service = service;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("acumuladores-productos/catalogos")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
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
    [Authorize(Policy = "perm:reportes.ver_modulo")]
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
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasCatalogosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCatalogosRemisiones(CancellationToken ct)
    {
        var result = await _service.ConsultarCatalogosRemisionesAsync(ct);
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("acumuladores-productos/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("acumuladores_y_productos", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarAcumuladoresProductosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("remisiones/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarRemisiones(
        [FromBody] ReportesVentasRemisionesRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("remisiones", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarRemisionesAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("folios/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFolios(
        [FromBody] ReportesVentasFoliosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("folios", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarFoliosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("facturas/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarFacturas(
        [FromBody] ReportesVentasFacturasRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("facturas", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarFacturasAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("concentrados/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarConcentrados(
        [FromBody] ReportesVentasConcentradosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("concentrados", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarConcentradosAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("cobranza/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarCobranza(
        [FromBody] ReportesVentasCobranzaRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("cobranza", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarCobranzaAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("legacy/generar")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasGenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarLegacyVentas(
        [FromBody] ReportesVentasLegacyRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        // EL REPORTE SE PIDE POR reporteKey Y NADA MAS.
        //
        // El repositorio tambien aceptaba un IDReporte crudo del cliente, y con
        // permisos por reporte eso es una puerta falsa: mandas la clave de un
        // reporte que si puedes y el id del que no, y el permiso que se revisa
        // no es el del reporte que sale. Ni el web ni la app mandan idReporte.
        if (request.IDReporte != 0)
            return BadRequest(new { message = "No mandes idReporte: el reporte se pide por reporteKey." });

        if (ReportesCatalogo.Buscar(request.ReporteKey) is null)
            return BadRequest(new { message = $"reporteKey desconocido: \"{request.ReporteKey}\"." });

        if (await ExigirPermisoReporteAsync(request.ReporteKey, ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
        var result = await _service.GenerarLegacyVentasAsync(request, ct);
        result.Url = BuildReportesV3WUrl(result.ParametrosLegacy);

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }

    [HttpPost("acumuladores-productos/vista-previa")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesVentasPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConsultarAcumuladoresProductos(
        [FromBody] ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "Body requerido." });

        if (await ExigirPermisoReporteAsync("acumuladores_y_productos", ct) is { } negado)
            return negado;

        HydrateLegacyContext(request);
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
        /* El nombre del reporte es lo que se lee en la pestaña; sin el decia "pdf",
           que es el ultimo pedazo de la direccion. Ver RenderAsync. */
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            result.Html,
            result.Orientacion,
            result.NombreReporte,
            ct);
        Response.Headers["Cache-Control"] = "no-store";
        return File(pdf, "application/pdf");
    }

    /// <summary>
    /// Que reportes puede ver quien pregunta. El menu del web y el de la app se
    /// pintan con esto, asi que ofrecen exactamente lo que la API va a aceptar.
    /// </summary>
    /// <remarks>
    /// Antes cada front traia su propio catalogo y "reportes.ver_modulo" era el
    /// unico candado: quien entraba al modulo podia generar los 53 reportes por
    /// mas que le hubieran quitado permisos. El mapeo reporte->permiso vive
    /// ahora en <see cref="ReportesCatalogo"/> y se publica aqui.
    /// </remarks>
    [HttpGet("permisos")]
    [Authorize(Policy = "perm:reportes.ver_modulo")]
    [ProducesResponseType(typeof(ReportesPermisosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarPermisosReportes(CancellationToken ct)
    {
        // Un solo snapshot para los 53. Ver PermisosDelUsuario.
        var puede = await LeerPermisosAsync(ct);
        if (!puede.TokenValido)
            return Unauthorized(new { ok = false, message = "Token invalido." });

        var reportes = ReportesCatalogo.Reportes
            .Select(x => new ReportesPermisoItem
            {
                Clave = x.Clave,
                Grupo = x.Grupo,
                Etiqueta = x.Etiqueta,
                Permiso = x.Permiso,
                // Sin permiso conocido el reporte sigue abierto: bloquear por una
                // duda deja a alguien sin su reporte sin saber por que.
                Permitido = x.Permiso is null || puede.Tiene(x.Permiso)
            })
            .ToList();

        Response.Headers["Cache-Control"] = "no-store";
        return Ok(new ReportesPermisosResponse
        {
            PermisoModulo = ReportesCatalogo.PermisoModulo,
            Reportes = reportes,
            ClavesPermitidas = reportes.Where(x => x.Permitido).Select(x => x.Clave).ToList()
        });
    }

    /// <summary>
    /// El 403 a regresar si el usuario no puede ese reporte, o null si puede.
    /// Un reporte sin permiso en el catalogo NO se bloquea.
    /// </summary>
    private async Task<IActionResult?> ExigirPermisoReporteAsync(string? clave, CancellationToken ct)
    {
        var reporte = ReportesCatalogo.Buscar(clave);

        // Clave desconocida: no se inventa un candado. El repositorio ya rebota
        // las claves que no sabe resolver.
        if (reporte?.Permiso is null)
            return null;

        if (await ExigirPermisoAsync(new[] { reporte.Permiso }, ct) is null)
            return null;

        // Se dice CUAL reporte y CUAL permiso falta: un "no tienes permiso" a
        // secas, en una pantalla con 53 reportes, no le sirve a nadie para
        // pedirlo.
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            ok = false,
            message = $"No tienes permiso para el reporte \"{reporte.Etiqueta}\".",
            reporte = reporte.Clave,
            permiso = reporte.Permiso
        });
    }

    private string BuildReportesV3WUrl(string parametrosLegacy)
    {
        var psp = Uri.EscapeDataString(parametrosLegacy);
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : "";
        return $"{Request.Scheme}://{Request.Host}{pathBase}/api/reportes/ventas/ReportesV3W?psp={psp}";
    }

    private void HydrateLegacyContext(ReportesVentasAcumuladoresProductosRequest request)
    {
        request.LegacyIDUsuario = _currentUserAccessor.GetLegacyUserId(User);
        request.LegacyNombreEquipo = "WEB";
    }
}
