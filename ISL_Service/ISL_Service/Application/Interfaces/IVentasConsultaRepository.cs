using ISL_Service.Application.DTOs.VentasConsulta;

namespace ISL_Service.Application.Interfaces;

public interface IVentasConsultaRepository
{
    Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, CancellationToken ct);
    Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, CancellationToken ct);

    /// Deja los parametros del reporte en la tabla Parametros y devuelve su id,
    /// que es lo que la pagina de reportes usa para recuperarlos.
    Task<int> RegistrarParametrosReporteAsync(string nombreEquipo, int idUsuario, int idReporte, string idsVenta, CancellationToken ct);
}
