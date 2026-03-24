using ISL_Service.Application.DTOs.InscripcionesTorneo;

namespace ISL_Service.Application.Interfaces;

public interface IInscripcionesTorneoService
{
    Task<List<InscripcionTorneoDto>> ConsultarAsync(Guid? torneoId, Guid? categoriaId, byte? estado, string? texto, CancellationToken ct = default);
    Task<InscripcionTorneoDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InscripcionTorneoDto> CrearAsync(CreateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<InscripcionTorneoDto> ActualizarAsync(Guid id, UpdateInscripcionTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<InscripcionTorneoDto> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
    Task<InscripcionTorneoDto> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
}