using ISL_Service.Application.DTOs.Profesores;

namespace ISL_Service.Application.Interfaces;

public interface IProfesoresService
{
    Task<List<ProfesorDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default);
    Task<ProfesorDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProfesorDto> CrearAsync(CreateProfesorRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<ProfesorDto> ActualizarAsync(Guid id, UpdateProfesorRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<ProfesorDto> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
}
