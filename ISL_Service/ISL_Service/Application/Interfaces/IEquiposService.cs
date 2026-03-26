using ISL_Service.Application.DTOs.Equipos;

namespace ISL_Service.Application.Interfaces;

public interface IEquiposService
{
    Task<List<EquipoDto>> ConsultarAsync(byte? estado, Guid? categoriaId, byte? diaJuego, string? texto, CancellationToken ct = default);
    Task<EquipoDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EquipoDto> CrearAsync(CreateEquipoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<EquipoDto> ActualizarAsync(Guid id, UpdateEquipoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<EquipoDto> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default);
    Task<EquipoDto> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
}

