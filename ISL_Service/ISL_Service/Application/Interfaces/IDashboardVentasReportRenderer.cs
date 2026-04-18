using ISL_Service.Application.DTOs.DashboardVentas;

namespace ISL_Service.Application.Interfaces;

public interface IDashboardVentasReportRenderer
{
    string Formato { get; }
    Task<DashboardVentasReporteFile> RenderAsync(DashboardVentasReporteData data, DashboardVentasReporteRequest request, CancellationToken ct = default);
}
