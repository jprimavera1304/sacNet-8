using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Recaudaciones de aviacion. Contra la base LOCAL de la caja: sus sp_n_ no
/// reciben servidor, porque la caja cobra sin red y sincroniza despues.
/// </summary>
public interface IAviacionRecaudacionesRepository
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionRecaudacionesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarRecurrentesAsync(AviacionRecaudacionesRecurrentesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(AviacionRecaudacionesCortesFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarSiguienteFolioAsync(int idCaja, CancellationToken ct = default);

    Task<ResultadoSp> InsertarAsync(CrearAviacionRecaudacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> CancelarAsync(int idRecaudacion, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> InsertarCorteAsync(CrearAviacionRecaudacionCorteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
