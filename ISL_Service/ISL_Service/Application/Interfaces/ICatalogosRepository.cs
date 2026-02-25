using ISL_Service.Application.DTOs.Catalogos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Catalogo de solo lectura (TiposUsados, Repartidores, Tarimas).
/// </summary>
public interface ICatalogosRepository
{
    Task<List<TipoCascoItemDto>> ListTiposCascoAsync(int? idStatus, CancellationToken ct = default);
    Task<List<RepartidorItemDto>> ListRepartidoresAsync(int? idStatus, CancellationToken ct = default);
    Task<List<TarimaCatalogItemDto>> ListTarimasAsync(int? idStatus, CancellationToken ct = default);
}
