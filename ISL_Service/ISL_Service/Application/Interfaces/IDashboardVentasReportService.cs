using ISL_Service.Application.DTOs.DashboardVentas;

namespace ISL_Service.Application.Interfaces;

public interface IDashboardVentasReportService
{
    Task<DashboardVentasReporteFile> GenerarReporteAsync(DashboardVentasReporteRequest request, CancellationToken ct = default);
}
