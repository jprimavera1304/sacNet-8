using ISL_Service.Application.DTOs.ObraContratos;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Contratos de obra (MC) y todo lo que les cuelga, contra los mismos sp_n_ que
/// llamaba MacServicios2.
/// </summary>
public interface IObraContratosRepository
{
    // --- El contrato ---
    Task<List<Dictionary<string, object?>>> ConsultarContratosAsync(ObraContratosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarContratoAsync(CrearObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarContratoAsync(int id, ActualizarObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarContratoPartidasIndividualesAsync(int id, bool partidasIndividuales, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarContratoStatusAsync(int id, bool contratoCerrado, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Convenios ---
    Task<List<Dictionary<string, object?>>> ConsultarConveniosAsync(ObraConveniosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarConvenioAsync(CrearObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarConvenioAsync(int id, ActualizarObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarConvenioStatusAsync(int id, bool convenioCerrado, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Grupos ---
    Task<ResultadoSp> InsertarGrupoAsync(CrearObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarGrupoAsync(int id, ActualizarObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ReordenarGrupoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Conceptos ---
    Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(ObraContratoConceptosFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarConceptosEstimacionesAsync(ObraConceptosEstimacionesFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarConceptoAsync(CrearObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarConceptoAsync(int id, ActualizarObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ReordenarConceptoAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> CargarConceptosDeArchivoAsync(CargarObraContratoConceptosRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarConceptoEstimacionAsync(ActualizarObraConceptoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Partidas del contrato ---
    Task<List<Dictionary<string, object?>>> ConsultarContratoPartidasAsync(ObraContratoPartidasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarContratoPartidaAsync(CrearObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarContratoPartidaAsync(int id, ActualizarObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ReordenarContratoPartidaAsync(int id, int idContrato, int delta, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Periodos ---
    Task<List<Dictionary<string, object?>>> ConsultarPeriodosAsync(ObraContratoPeriodosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarPeriodoAsync(CrearObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarPeriodoAsync(int id, ActualizarObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Estimaciones ---
    Task<ResultadoSp> InsertarEstimacionAsync(CrearObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarEstimacionAsync(int id, ActualizarObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
