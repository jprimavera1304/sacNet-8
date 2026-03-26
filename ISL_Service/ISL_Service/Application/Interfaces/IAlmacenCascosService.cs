using ISL_Service.Application.DTOs.AlmacenCascos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Servicio de negocio para Almacén de Cascos (validaciones + llamadas al repositorio).
/// </summary>
public interface IAlmacenCascosService
{
    Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default);

    Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default);

    Task<List<MovimientoCascoTarimaKilosDto>> GetMovimientoTarimasKilosAsync(int idMovimiento, CancellationToken ct = default);

    Task<int> CrearSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default);

    Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default);

    Task<MovimientoCascoTarimaKilosResultDto> GuardarKilosTarimaAsync(int idMovimiento, int numeroTarima, decimal kilos, string usuario, CancellationToken ct = default);

    Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default);

    Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default);

    Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default);
}
