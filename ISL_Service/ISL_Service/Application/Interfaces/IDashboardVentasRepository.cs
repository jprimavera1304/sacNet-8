using ISL_Service.Application.DTOs.DashboardVentas;

namespace ISL_Service.Application.Interfaces;

public interface IDashboardVentasRepository
{
    Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<DashboardVentasKpisDto?> ConsultarKpisAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardVentasSerieMensualDto>> ConsultarSerieMensualAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardVentasSerieSemanalRawDto>> ConsultarSerieSemanalAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardTopProductoDto>> ConsultarTopProductosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardTopClienteDto>> ConsultarTopClientesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardTopCategoriaDto>> ConsultarTopCategoriasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardTopMarcaDto>> ConsultarTopMarcasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardVentasAlmacenDto>> ConsultarVentasAlmacenesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardVentasAgenteDto>> ConsultarVentasAgentesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
    Task<List<DashboardVentasDetalleDto>> ConsultarVentasDetalleAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default);
}
