using ISL_Service.Application.DTOs.VentasConsulta;

namespace ISL_Service.Application.Interfaces;

public interface IVentasConsultaRepository
{
    Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, CancellationToken ct);

    /// Lo que se pidio hoy y no se pudo surtir. Mac31: btnPedidosFaltantes_Click
    /// (ConsultarVentas.cs:3472) -> ConsultaPedidoFaltante() (:3503).
    Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct);
}
