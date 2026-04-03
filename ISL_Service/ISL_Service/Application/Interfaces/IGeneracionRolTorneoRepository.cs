using ISL_Service.Application.DTOs.GeneracionRolTorneo;

namespace ISL_Service.Application.Interfaces;

public interface IGeneracionRolTorneoRepository
{
    Task<List<GeneracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? torneoId, Guid? jornadaId, DateTime? fechaJuego, byte? diaJuego, byte? estado, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto?> InsertarAsync(CreateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto?> ActualizarAsync(Guid id, UpdateGeneracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GeneracionRolTorneoDto?> CancelarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default);
    Task<List<GeneracionRolCategoriaDto>> ConsultarCategoriasAsync(Guid generacionId, CancellationToken ct = default);
    Task<List<GeneracionRolCanchaDto>> GuardarCanchasAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<GeneracionRolCanchaItemRequest> canchas, CancellationToken ct = default);
    Task<List<GeneracionRolCanchaDto>> ConsultarCanchasAsync(Guid generacionId, CancellationToken ct = default);
    Task<List<GeneracionRolEquipoDto>> CargarEquiposAsync(Guid generacionId, Guid usuarioId, CancellationToken ct = default);
    Task<List<GeneracionRolEquipoDto>> ConsultarEquiposAsync(Guid generacionId, CancellationToken ct = default);
    Task<GeneracionRolEquipoDto?> ActualizarParticipacionEquipoAsync(Guid id, UpdateParticipacionEquipoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<GenerarPartidosGeneracionRolTorneoResponse> GenerarPartidosAsync(Guid generacionId, Guid usuarioId, bool confirmarEstado = true, bool soloConfirmarEstado = false, CancellationToken ct = default);
    Task<List<PartidoGeneracionRolTorneoDto>> ConsultarPartidosAsync(Guid generacionId, CancellationToken ct = default);
    Task<List<PartidoGeneracionRolTorneoDto>> ActualizarOrdenPartidosAsync(Guid generacionId, Guid usuarioId, IReadOnlyList<OrdenPartidoItemRequest> partidos, CancellationToken ct = default);
    Task<PartidoGeneracionRolTorneoDto?> ActualizarObservacionPartidoAsync(Guid partidoId, string? observaciones, Guid usuarioId, CancellationToken ct = default);
}
