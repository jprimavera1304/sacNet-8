using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los catalogos del modulo de aviacion, contra los mismos sp_n_ que llamaba
/// MacServicios2. `central` elige el origen: false = la replica local de la
/// caja, true = el servidor de aviacion de Constantes.AviacionDBServer.
/// </summary>
public interface IAviacionCatalogosRepository
{
    // --- Cajas (siempre local: su SP no recibe servidor) ---
    Task<ResultadoSp> ActualizarCajaAsync(int idCaja, ActualizarAviacionCajaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Conceptos (siempre local) ---
    Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(AviacionConceptosFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarConceptoSiguienteCuentaAsync(int idConcepto, CancellationToken ct = default);
    Task<ResultadoSp> InsertarConceptoAsync(CrearAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarConceptoAsync(int idConcepto, ActualizarAviacionConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Cuentas ---
    Task<List<Dictionary<string, object?>>> ConsultarCuentasAsync(AviacionCuentasFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarCuentaSiguienteNumeroAsync(int idCuenta, CancellationToken ct = default);
    Task<ResultadoSp> InsertarCuentaAsync(CrearAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarCuentaAsync(int idCuenta, ActualizarAviacionCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> AceptarCuentaAsync(int idCuenta, int idUsuario, string equipo, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarTarjetaPorcentajesAsync(int idCuenta, int idStatus, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarTarjetaPorcentajesAsync(ActualizarAviacionTarjetaPorcentajesRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Economicos ---
    Task<List<Dictionary<string, object?>>> ConsultarEconomicosAsync(AviacionEconomicosFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteIdAsync(int idPermisionario, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteNumeroCuentaAsync(int idPermisionario, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEconomicosAuxiliaresAsync(AviacionEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarEconomicoAsync(CrearAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarEconomicoAsync(int idEconomico, ActualizarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarEconomicoAmortizacionAsync(int idEconomico, ActualizarAviacionEconomicoAmortizacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ReasignarEconomicoAsync(int idEconomico, ReasignarAviacionEconomicoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> AceptarEconomicoAsync(int idEconomico, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Motivos de fianza ---
    Task<List<Dictionary<string, object?>>> ConsultarMotivosFianzaAsync(AviacionMotivosFianzaFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarMotivoFianzaSiguienteIdAsync(int idMotivoFianza, CancellationToken ct = default);
    Task<ResultadoSp> InsertarMotivoFianzaAsync(CrearAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarMotivoFianzaAsync(int idMotivoFianza, ActualizarAviacionMotivoFianzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Operadores ---
    Task<List<Dictionary<string, object?>>> ConsultarOperadoresAsync(AviacionOperadoresFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarOperadorSiguienteNumeroCuentaAsync(int idOperador, CancellationToken ct = default);
    Task<ResultadoSp> InsertarOperadorAsync(CrearAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarOperadorAsync(int idOperador, ActualizarAviacionOperadorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> AceptarOperadorAsync(int idOperador, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarOperadorFianzaAsync(int idOperador, decimal importeFianza, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarOperadorCuentaAsync(int idOperador, ActualizarAviacionOperadorCuentaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Permisionarios ---
    Task<List<Dictionary<string, object?>>> ConsultarPermisionariosAsync(AviacionPermisionariosFiltro filtro, bool central, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct = default);
    Task<ResultadoSp> InsertarPermisionarioAsync(CrearAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarPermisionarioAsync(int idPermisionario, ActualizarAviacionPermisionarioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> AceptarPermisionarioAsync(int idPermisionario, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Impresoras (siempre local) ---
    Task<List<Dictionary<string, object?>>> ConsultarImpresorasAsync(AviacionImpresorasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarImpresoraAsync(ActualizarAviacionImpresoraRequest request, int idUsuario, CancellationToken ct = default);

    // --- Replicacion ---
    /// <summary>
    /// sp_n_SincronizarDownload para una tabla de aviacion. Corre contra la base
    /// LOCAL (su SP no recibe servidor) y solo tiene sentido si esta instalacion
    /// es una caja.
    /// </summary>
    Task<ResultadoSp> SincronizarDescargaAsync(int idTabla, int idUsuario, string equipo, CancellationToken ct = default);
}
