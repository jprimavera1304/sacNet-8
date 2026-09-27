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
        bool filtrarPorRegistro, CancellationToken ct = default);

    Task<List<DetalleCascoCambioDto>> ConsultarDetalleAsync(int idMovimiento, CancellationToken ct = default);

    /// <summary>
    /// El desglose por tipo de TODOS los movimientos de un periodo, en una sola
    /// llamada. Devuelve de mas a proposito —no filtra por estatus— porque quien
    /// lo pide ya sabe que movimientos va a enseñar y busca por idMovimiento.
    /// </summary>
    Task<List<DetallePeriodoCascoCambioDto>> ConsultarDetallePeriodoAsync(
        DateTime? fechaInicio, DateTime? fechaFin, bool filtrarPorRegistro, CancellationToken ct = default);

    /// La carpeta de imagenes, el nombre del archivo del logo y el nombre con el
    /// que la empresa se presenta hacia afuera. Sale de Constantes: cada empresa
    /// tiene lo suyo, asi que no se puede empaquetar en el backend.
    Task<(string PathImagenes, string Logo, string Empresa)> ConsultarMarcaEmpresaAsync(CancellationToken ct = default);

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
