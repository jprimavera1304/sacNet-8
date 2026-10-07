using ISL_Service.Application.DTOs.DashboardVentas;

namespace ISL_Service.Application.Interfaces;

public interface IDashboardVentasRepository
{
    /// <summary>
    /// Los catalogos que llenan los desplegables del tablero. NO recibe filtros:
    /// <c>sp_w_ConsultarDashboardVentasFiltros</c> no declara ni un parametro
    /// (ver <c>Querrys/Dashboard Ventas Zara/02_...sql</c>) y devuelve los ocho
    /// catalogos completos. Antes la firma pedia los 11 filtros del tablero y el
    /// cuerpo del metodo no los usaba en ninguna linea.
    /// </summary>
    Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(CancellationToken ct = default);
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
