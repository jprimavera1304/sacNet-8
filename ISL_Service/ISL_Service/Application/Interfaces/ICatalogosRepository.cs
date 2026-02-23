using ISL_Service.Application.DTOs.Catalogos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Catalogo de solo lectura (ej. TiposUsados para tipos de casco).
/// </summary>
public interface ICatalogosRepository
{
    Task<List<TipoCascoItemDto>> ListTiposCascoAsync(int? idStatus, CancellationToken ct = default);
}
