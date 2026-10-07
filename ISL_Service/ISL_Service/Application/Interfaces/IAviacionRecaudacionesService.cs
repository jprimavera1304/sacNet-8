using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;

namespace ISL_Service.Application.Interfaces;

/// <summary>Reglas de las recaudaciones de aviacion.</summary>
public interface IAviacionRecaudacionesService
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionRecaudacionesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarRecurrentesAsync(AviacionRecaudacionesRecurrentesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(AviacionRecaudacionesCortesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarSiguienteFolioAsync(int idCaja, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearAsync(CrearAviacionRecaudacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CancelarAsync(int idRecaudacion, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearCorteAsync(CrearAviacionRecaudacionCorteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
