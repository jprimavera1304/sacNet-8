using ISL_Service.Application.DTOs.Temporadas;

namespace ISL_Service.Application.Interfaces;

public interface ITemporadasRepository
{
    Task<List<TemporadaDto>> ConsultarTemporadasAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<List<TemporadaDto>> ConsultarTemporadasListadoAsync(byte? estado, string? texto, Guid usuarioSistemaId, CancellationToken ct = default);
    Task<TemporadaDto?> InsertarTemporadaAsync(CreateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto?> ActualizarTemporadaAsync(Guid id, UpdateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto?> CancelarTemporadaAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto?> ReactivarTemporadaAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<List<TorneoDto>> ConsultarTorneosAsync(Guid? temporadaId, byte? estado, string? texto, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default);
    Task<TorneoDto?> InsertarTorneoAsync(CreateTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto?> ActualizarTorneoAsync(Guid id, UpdateTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto?> CancelarTorneoAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto?> ActivarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto?> CerrarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto?> ReactivarTorneoAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<int> CerrarTorneosVencidosAsync(Guid usuarioSistemaId, DateTime? fechaCorte, CancellationToken ct = default);
}
