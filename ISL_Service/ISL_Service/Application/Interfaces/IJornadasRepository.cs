using ISL_Service.Application.DTOs.Jornadas;

namespace ISL_Service.Application.Interfaces;

public interface IJornadasRepository
{
    Task<List<JornadaDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<JornadaDto?> InsertarAsync(CreateJornadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<JornadaDto?> ActualizarAsync(Guid id, UpdateJornadaRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<JornadaDto?> InhabilitarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default);
}
