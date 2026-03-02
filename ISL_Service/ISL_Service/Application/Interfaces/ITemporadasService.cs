using ISL_Service.Application.DTOs.Temporadas;

namespace ISL_Service.Application.Interfaces;

public interface ITemporadasService
{
    Task<List<TemporadaDto>> ConsultarTemporadasAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<List<TemporadaDto>> ConsultarTemporadasListadoAsync(byte? estado, string? texto, Guid usuarioSistemaId, CancellationToken ct = default);
    Task<TemporadaDto?> GetTemporadaByIdAsync(Guid id, CancellationToken ct = default);
    Task<TemporadaDto> CrearTemporadaAsync(CreateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto> ActualizarTemporadaAsync(Guid id, UpdateTemporadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto> CancelarTemporadaAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
    Task<TemporadaDto> ReactivarTemporadaAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<List<TorneoDto>> ConsultarTorneosAsync(Guid? temporadaId, byte? estado, string? texto, CancellationToken ct = default);
    Task<TorneoDto?> GetTorneoByIdAsync(Guid id, CancellationToken ct = default);
    Task<TorneoDto> CrearTorneoAsync(CreateTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto> ActualizarTorneoAsync(Guid id, UpdateTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<TorneoDto> CancelarTorneoAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
}
