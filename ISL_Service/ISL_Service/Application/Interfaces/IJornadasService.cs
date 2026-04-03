using ISL_Service.Application.DTOs.Jornadas;

namespace ISL_Service.Application.Interfaces;

public interface IJornadasService
{
    Task<List<JornadaDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<JornadaDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<JornadaDto> CrearAsync(CreateJornadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<JornadaDto> ActualizarAsync(Guid id, UpdateJornadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<JornadaDto> InhabilitarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default);
}
