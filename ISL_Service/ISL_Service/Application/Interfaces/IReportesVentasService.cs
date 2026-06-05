using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Application.Interfaces;

public interface IReportesVentasService
{
    Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default);
}
