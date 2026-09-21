using ISL_Service.Application.DTOs.CascosCambio;

namespace ISL_Service.Application.Interfaces;

public interface ICascosCambioService
{
    Task<List<TipoCascoCambioDto>> ConsultarTiposAsync(CancellationToken ct = default);

    /// <summary>
    /// Movimientos con su saldo corrido y el corte del periodo ya calculados.
    /// </summary>
    Task<MovimientosCascosCambioResponse> ConsultarMovimientosAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? tipoMovimiento, bool incluirCancelados,
        bool filtrarPorRegistro, CancellationToken ct = default);

    Task<List<DetalleCascoCambioDto>> ConsultarDetalleAsync(int idMovimiento, CancellationToken ct = default);

    Task<List<ResumenTipoCascoCambioDto>> ConsultarResumenAsync(
        DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default);

    Task<MovimientoCascoCambioCreadoDto> CrearAsync(
        CrearMovimientoCascoCambioRequest request, string usuario, CancellationToken ct = default);

    Task CancelarAsync(int idMovimiento, string? motivo, string usuario, CancellationToken ct = default);

    Task<SincronizacionPreciosDto> SincronizarPreciosContraparteAsync(CancellationToken ct = default);
}
