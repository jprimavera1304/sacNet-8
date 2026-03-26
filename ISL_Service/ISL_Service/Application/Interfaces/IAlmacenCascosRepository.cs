using ISL_Service.Application.DTOs.AlmacenCascos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Operaciones del módulo Almacén de Cascos (movimientos, salidas, entradas, cancelación).
/// </summary>
public interface IAlmacenCascosRepository
{
    Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default);

    Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default);

    Task<MovimientoCascoDto?> GetMovimientoAsync(int idMovimiento, CancellationToken ct = default);

    Task<bool> ExisteNumeroTarimaDetalleAsync(int idMovimiento, int numeroTarima, CancellationToken ct = default);

    /// <summary>
    /// Crea cabecera SALIDA (SP), inserta detalle y actualiza totales. Transaccional.
    /// </summary>
    Task<int> InsertarSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta sp_w_AceptarEntradaCasco (crea ENTRADA, acepta SALIDA, TotalKilos = 0).
    /// </summary>
    Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una salida (cabecera + detalle). Recalcula totales.
    /// </summary>
    Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una entrada (repartidor recibe, observaciones).
    /// </summary>
    Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default);

    Task<MovimientoCascoTarimaKilosResultDto> UpsertMovimientoTarimaKilosAsync(int idMovimiento, int numeroTarima, decimal kilos, string usuario, CancellationToken ct = default);

    Task<List<MovimientoCascoTarimaKilosDto>> GetMovimientoTarimasKilosAsync(int idMovimiento, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta sp_w_CancelarMovimientoCasco.
    /// </summary>
    Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default);
}
