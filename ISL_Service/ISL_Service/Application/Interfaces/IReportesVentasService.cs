using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Application.Interfaces;

public interface IReportesVentasService
{
    Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default);

    Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default);

    Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default);
}
