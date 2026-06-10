using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class ReportesVentasService : IReportesVentasService
{
    private readonly IReportesVentasRepository _repository;

    public ReportesVentasService(IReportesVentasRepository repository)
    {
        _repository = repository;
    }

    public Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default)
    {
        return _repository.ConsultarCatalogosAcumuladoresProductosAsync(idGrupoCategoria, idCategorias, ct);
    }

    public Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        int? numero,
        int? idCliente,
        CancellationToken ct = default)
    {
        if (numero.GetValueOrDefault() <= 0 && idCliente.GetValueOrDefault() <= 0)
            throw new ArgumentException("numero o idCliente requerido.");

        return _repository.ConsultarClientesAsync(numero, idCliente, ct);
    }

    public Task<ReportesVentasCatalogosResponse> ConsultarCatalogosRemisionesAsync(CancellationToken ct = default)
    {
        return _repository.ConsultarCatalogosRemisionesAsync(ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.GenerarAcumuladoresProductosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarRemisionesAsync(
        ReportesVentasRemisionesRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarRemisionesAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarFoliosAsync(
        ReportesVentasFoliosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarFoliosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarFacturasAsync(
        ReportesVentasFacturasRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        ValidateUsuarios(request);

        return _repository.GenerarFacturasAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarConcentradosAsync(
        ReportesVentasConcentradosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        if ((request.IDRepartidores ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un repartidor.");

        return _repository.GenerarConcentradosAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarCobranzaAsync(
        ReportesVentasCobranzaRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.GenerarCobranzaAsync(request, ct);
    }

    public Task<ReportesVentasGenerateResponse> GenerarLegacyVentasAsync(
        ReportesVentasLegacyRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.ReporteKey) && request.IDReporte <= 0)
            throw new ArgumentException("reporteKey o idReporte requerido.");

        return _repository.GenerarLegacyVentasAsync(request, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.ConsultarAcumuladoresProductosAsync(request, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.ConsultarAcumuladoresProductosPorParametrosAsync(parametrosLegacy, ct);
    }

    public Task<ReportesVentasFileResponse> GenerarAcumuladoresProductosExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.GenerarAcumuladoresProductosExcelPorParametrosAsync(parametrosLegacy, ct);
    }

    public Task<ReportesVentasPreviewResponse> ConsultarReporteVentasPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.ConsultarReporteVentasPorParametrosAsync(parametrosLegacy, ct);
    }

    public Task<ReportesVentasFileResponse> GenerarReporteVentasExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        if (parametrosLegacy <= 0)
            throw new ArgumentException("psp requerido.");

        return _repository.GenerarReporteVentasExcelPorParametrosAsync(parametrosLegacy, ct);
    }

    private static void Validate(ReportesVentasAcumuladoresProductosRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.FechaInicial == default)
            throw new ArgumentException("fechaInicial es requerida.");
        if (request.FechaFinal == default)
            throw new ArgumentException("fechaFinal es requerida.");
        if (request.FechaFinal < request.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser menor a fechaInicial.");

        var salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant();
        if (salida != "pantalla" && salida != "excel")
            throw new ArgumentException("salida debe ser 'pantalla' o 'excel'.");

        var tipoReporte = (request.TipoReporte ?? "empresa").Trim().ToLowerInvariant();
        if (tipoReporte == "agente" && (request.IDAgentes ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un agente.");
        if (tipoReporte == "cliente" && (request.IDClientes ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione el cliente.");
    }

    private static void ValidateUsuarios(ReportesVentasRemisionesRequest request)
    {
        if ((request.IDUsuarios ?? new List<int>()).Where(id => id > 0).Distinct().Count() == 0)
            throw new ArgumentException("seleccione al menos un usuario.");
    }
}
