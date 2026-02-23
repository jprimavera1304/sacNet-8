using ISL_Service.Application.DTOs.Tarima;

namespace ISL_Service.Application.Interfaces;

public interface ITarimaService
{
    Task<List<TarimaDto>> ConsultarTarimasAsync(int? idStatus, string? busqueda, CancellationToken ct = default);
    Task<TarimaDto?> GetByIdAsync(int idTarima, CancellationToken ct = default);
    Task<int> CrearAsync(CreateTarimaRequest request, string usuarioCreacion, CancellationToken ct = default);
    Task ActualizarAsync(int idTarima, UpdateTarimaRequest request, string usuarioModificacion, CancellationToken ct = default);
    Task CambiarStatusAsync(int idTarima, int idStatus, CancellationToken ct = default);
}
