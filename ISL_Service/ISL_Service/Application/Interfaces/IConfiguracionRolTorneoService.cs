using ISL_Service.Application.DTOs.ConfiguracionRolTorneo;

namespace ISL_Service.Application.Interfaces;

public interface IConfiguracionRolTorneoService
{
    Task<List<ConfiguracionRolTorneoDto>> ConsultarAsync(string? texto, Guid? temporadaId, byte? estado, CancellationToken ct = default);
    Task<ConfiguracionRolTorneoDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ConfiguracionRolTorneoDto?> ObtenerActivaPorTorneoAsync(Guid torneoId, CancellationToken ct = default);
    Task<ConfiguracionRolTorneoDto> CrearAsync(CreateConfiguracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<ConfiguracionRolTorneoDto> ActualizarAsync(Guid id, UpdateConfiguracionRolTorneoRequest request, Guid usuarioId, CancellationToken ct = default);
    Task<ConfiguracionRolTorneoDto> InhabilitarAsync(Guid id, string motivo, Guid usuarioId, CancellationToken ct = default);
}
