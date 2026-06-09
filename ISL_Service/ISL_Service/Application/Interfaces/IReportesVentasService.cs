using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Application.Interfaces;

public interface IReportesVentasService
{
    Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default);

    Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        int? numero,
        int? idCliente,
        CancellationToken ct = default);

    Task<ReportesVentasCatalogosResponse> ConsultarCatalogosRemisionesAsync(CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarRemisionesAsync(
        ReportesVentasRemisionesRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarFoliosAsync(
        ReportesVentasFoliosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarFacturasAsync(
        ReportesVentasFacturasRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarConcentradosAsync(
        ReportesVentasConcentradosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarCobranzaAsync(
        ReportesVentasCobranzaRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default);

    Task<ReportesVentasFileResponse> GenerarAcumuladoresProductosExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default);

    Task<ReportesVentasPreviewResponse> ConsultarReporteVentasPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default);

    Task<ReportesVentasFileResponse> GenerarReporteVentasExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default);
}
