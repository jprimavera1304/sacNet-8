using ISL_Service.Application.DTOs.VentasConsultasLegacy;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Las cabeceras de pantalla de ventas que faltaban. Son siete procedimientos de
/// legacy, los mismos que llama Mac31, y ninguna de ellas llama ya a
/// sp_n_ConsultaConstantes.
/// </summary>
public interface IVentasConsultasLegacyRepository
{
    Task<FilasLegacyDto> ConsultarPedidoDetalleAsync(VentasPedidoDetalleConsultaRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarTransitoAsync(VentasTransitoConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPendientesAsync(VentasPendienteConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosAsync(VentasPagosConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarNuevosPagosAsync(VentasNuevosPagosConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosCentroServicioAsync(VentasPagosCentroServicioConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosLiquidacionAsync(VentasPagosLiquidacionConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarFoliosAsync(VentasFoliosConsultaRequest request, CancellationToken ct);
}

/// <summary>Las reglas de filtro de esas consultas.</summary>
public interface IVentasConsultasLegacyService
{
    Task<FilasLegacyDto> ConsultarPedidoDetalleAsync(VentasPedidoDetalleConsultaRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarTransitoAsync(VentasTransitoConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPendientesAsync(VentasPendienteConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosAsync(VentasPagosConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarNuevosPagosAsync(VentasNuevosPagosConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosCentroServicioAsync(VentasPagosCentroServicioConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarPagosLiquidacionAsync(VentasPagosLiquidacionConsultaRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarFoliosAsync(VentasFoliosConsultaRequest request, CancellationToken ct);
}
