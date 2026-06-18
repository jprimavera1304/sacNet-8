using ISL_Service.Application.DTOs.VentasConsulta;

namespace ISL_Service.Application.Interfaces;

public interface IVentasConsultaService
{
    Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct);
}
