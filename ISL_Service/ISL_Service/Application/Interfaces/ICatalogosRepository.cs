using ISL_Service.Application.DTOs.Catalogos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Catalogo de solo lectura (TiposUsados, Repartidores, Tarimas).
/// </summary>
public interface ICatalogosRepository
{
    Task<List<TipoCascoItemDto>> ListTiposCascoAsync(int? idStatus, CancellationToken ct = default);
    Task<List<RepartidorItemDto>> ListRepartidoresAsync(int? idStatus, string? texto, CancellationToken ct = default);
    Task<List<TarimaCatalogItemDto>> ListTarimasAsync(int? idStatus, CancellationToken ct = default);
    /// <summary>
    /// Desplegable de clientes. PIDE EL ID DE USUARIO porque el listado se acota
    /// al agente de quien pregunta (ver FiltroDeAgente): un desplegable de
    /// clientes no puede ensenar los clientes de otro agente.
    /// </summary>
    Task<List<ClienteCatalogItemDto>> ListClientesAsync(int? idStatus, int idUsuario, CancellationToken ct = default);
    Task<List<BancoCatalogItemDto>> ListBancosAsync(int? idStatus, string? texto, CancellationToken ct = default);
}
