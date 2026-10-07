using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;

namespace ISL_Service.Application.Interfaces;

/// <summary>Reglas de los catalogos de aviacion, incluida la replicacion.</summary>
public interface IAviacionCatalogosService
{
    // --- Cajas ---
    Task<ResultadoEscrituraDto> ActualizarCajaAsync(int idCaja, ActualizarAviacionCajaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Conceptos ---
    Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(AviacionConceptosFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarConceptoSiguienteCuentaAsync(int idConcepto, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearConceptoAsync(CrearAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarConceptoAsync(int idConcepto, ActualizarAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Cuentas ---
    Task<List<Dictionary<string, object?>>> ConsultarCuentasAsync(AviacionCuentasFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarCuentaSiguienteNumeroAsync(int idCuenta, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearCuentaAsync(CrearAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarCuentaAsync(int idCuenta, ActualizarAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> AceptarCuentaAsync(int idCuenta, int idUsuario, string equipo, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarTarjetaPorcentajesAsync(int idCuenta, int idStatus, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarTarjetaPorcentajesAsync(ActualizarAviacionTarjetaPorcentajesRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Economicos ---
    Task<List<Dictionary<string, object?>>> ConsultarEconomicosAsync(AviacionEconomicosFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteIdAsync(int idPermisionario, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteNumeroCuentaAsync(int idPermisionario, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicosAuxiliaresAsync(AviacionEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearEconomicoAsync(CrearAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarEconomicoAsync(int idEconomico, ActualizarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarEconomicoAmortizacionAsync(int idEconomico, ActualizarAviacionEconomicoAmortizacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ReasignarEconomicoAsync(int idEconomico, ReasignarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> AceptarEconomicoAsync(int idEconomico, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Motivos de fianza ---
    Task<List<Dictionary<string, object?>>> ConsultarMotivosFianzaAsync(AviacionMotivosFianzaFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarMotivoFianzaSiguienteIdAsync(int idMotivoFianza, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearMotivoFianzaAsync(CrearAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarMotivoFianzaAsync(int idMotivoFianza, ActualizarAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Operadores ---
    Task<List<Dictionary<string, object?>>> ConsultarOperadoresAsync(AviacionOperadoresFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarOperadorSiguienteNumeroCuentaAsync(int idOperador, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearOperadorAsync(CrearAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarOperadorAsync(int idOperador, ActualizarAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> AceptarOperadorAsync(int idOperador, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarOperadorFianzaAsync(int idOperador, ActualizarAviacionOperadorFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarOperadorCuentaAsync(int idOperador, ActualizarAviacionOperadorCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Permisionarios ---
    Task<List<Dictionary<string, object?>>> ConsultarPermisionariosAsync(AviacionPermisionariosFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearPermisionarioAsync(CrearAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarPermisionarioAsync(int idPermisionario, ActualizarAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> AceptarPermisionarioAsync(int idPermisionario, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Impresoras ---
    Task<List<Dictionary<string, object?>>> ConsultarImpresorasAsync(AviacionImpresorasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarImpresoraAsync(ActualizarAviacionImpresoraRequest request, int idUsuario, CancellationToken ct = default);
}
