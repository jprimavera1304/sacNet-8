using ISL_Service.Application.DTOs.GeneracionRolTorneo;

namespace ISL_Service.Application.Interfaces;

public interface IGeneracionRolTorneoService
{
    Task<List<GeneracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? torneoId, Guid? jornadaId, DateTime? fechaJuego, byte? diaJuego, byte? estado, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto> CrearAsync(CreateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto> ActualizarAsync(Guid id, UpdateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto> CancelarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default);
    Task<List<GeneracionRolEquipoDto>> CargarEquiposAsync(Guid generacionId, Guid usuarioId, CancellationToken ct = default);
    Task<List<GeneracionRolEquipoDto>> ConsultarEquiposAsync(Guid generacionId, CancellationToken ct = default);
    Task<GeneracionRolEquipoDto> ActualizarParticipacionEquipoAsync(Guid id, UpdateParticipacionEquipoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GenerarPartidosGeneracionRolTorneoResponse> GenerarPartidosAsync(Guid generacionId, Guid usuarioId, CancellationToken ct = default);
    Task<List<PartidoGeneracionRolTorneoDto>> ConsultarPartidosAsync(Guid generacionId, CancellationToken ct = default);
}
