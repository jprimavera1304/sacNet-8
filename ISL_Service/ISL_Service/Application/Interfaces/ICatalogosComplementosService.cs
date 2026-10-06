using ISL_Service.Application.DTOs.CatalogosComplementos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Normaliza los filtros de los catalogos complementarios.</summary>
public interface ICatalogosComplementosService
{
    Task<List<TipoDomicilioDto>> ConsultarTiposDomicilioAsync(int id, int idStatus, string? texto, CancellationToken ct);
    Task<List<CondicionCreditoDto>> ConsultarCondicionesCreditoAsync(int id, int idStatus, string? texto, bool exacto, CancellationToken ct);
    Task<List<EmpresaSerieDto>> ConsultarEmpresasSeriesAsync(int id, int idEmpresa, int idStatus, string? texto, bool exacto, CancellationToken ct);
    Task<List<CatalogoFeDto>> ConsultarCatalogoFeAsync(CatalogoFe cual, int id, string? clave, bool exacto, CancellationToken ct);

    /// <summary>
    /// Marcas. <paramref name="id"/> gana sobre <paramref name="ids"/>, igual
    /// que en Mac31.
    /// </summary>
    Task<List<MarcaCatalogoDto>> ConsultarMarcasAsync(int id, IEnumerable<int>? ids, int idStatus, string? texto, bool exacto, CancellationToken ct);
}
