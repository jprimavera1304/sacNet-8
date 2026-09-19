using ISL_Service.Application.DTOs.CascosCambio;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Acceso a "Cascos a cambio". Todo pasa por sp_w_*; de legacy solo se LEE
/// [Catalogo TiposUsados], que es de donde salen los precios.
/// </summary>
public interface ICascosCambioRepository
{
    Task<List<TipoCascoCambioDto>> ConsultarTiposAsync(CancellationToken ct = default);

    Task<List<MovimientoCascoCambioDto>> ConsultarMovimientosAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? tipoMovimiento, bool incluirCancelados,
        CancellationToken ct = default);

    Task<List<DetalleCascoCambioDto>> ConsultarDetalleAsync(int idMovimiento, CancellationToken ct = default);

    Task<List<ResumenTipoCascoCambioDto>> ConsultarResumenAsync(
        DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default);

    /// <summary>
    /// Devuelve (ok, mensaje, idMovimiento, advertencia). La advertencia no es
    /// un error: el movimiento se guardo, pero hay algo que la persona tiene
    /// derecho a saber (remision repetida, o falta el precio de la contraparte).
    /// </summary>
    Task<(bool Ok, string Mensaje, int IdMovimiento, string Advertencia)> InsertarAsync(
        CrearMovimientoCascoCambioRequest request, string usuario, CancellationToken ct = default);

    Task<(bool Ok, string Mensaje)> CancelarAsync(
        int idMovimiento, string motivo, string usuario, CancellationToken ct = default);

    Task<SincronizacionPreciosDto> SincronizarPreciosContraparteAsync(
        string baseContraparte, string empresaContraparte, CancellationToken ct = default);
}
