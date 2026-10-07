using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.Gasolinas;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>Recibos de gasolina de los repartidores.</summary>
public interface IGasolinasRepository
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(GasolinasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarAsync(CrearGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarAsync(int idGasolina, ActualizarGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    /// <summary>
    /// Devuelve la tabla que arma el SP: un renglon por recibo con su resultado.
    /// No es el (result, mensaje) del resto de la familia; ver GasolinasDtos.
    /// </summary>
    Task<List<Dictionary<string, object?>>> CancelarAsync(CancelarGasolinasRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}

/// <summary>Reglas de los recibos de gasolina.</summary>
public interface IGasolinasService
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(GasolinasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearAsync(CrearGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarAsync(int idGasolina, ActualizarGasolinaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoCancelacionGasolinasDto> CancelarAsync(CancelarGasolinasRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
