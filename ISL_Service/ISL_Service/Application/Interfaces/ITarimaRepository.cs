using ISL_Service.Application.DTOs.Tarima;

namespace ISL_Service.Application.Interfaces;

public interface ITarimaRepository
{
    Task<List<TarimaDto>> ConsultarTarimasAsync(int? idStatus, string? busqueda, CancellationToken ct = default);
    Task<int> InsertarTarimaAsync(CreateTarimaRequest request, string usuarioCreacion, CancellationToken ct = default);
    Task ActualizarTarimaAsync(int idTarima, UpdateTarimaRequest request, string usuarioModificacion, CancellationToken ct = default);
    Task CambiarStatusTarimaAsync(int idTarima, int idStatus, string? usuarioModificacion, CancellationToken ct = default);
}
