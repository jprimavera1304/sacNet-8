using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraContratos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Reglas de los contratos de obra (MC) y de lo que les cuelga.</summary>
public interface IObraContratosService
{
    // --- El contrato ---
    Task<List<Dictionary<string, object?>>> ConsultarContratosAsync(ObraContratosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearContratoAsync(CrearObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarContratoAsync(int id, ActualizarObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarContratoPartidasIndividualesAsync(int id, ActualizarObraContratoPartidasIndividualesRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarContratoStatusAsync(int id, ActualizarObraContratoStatusRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Convenios ---
    Task<List<Dictionary<string, object?>>> ConsultarConveniosAsync(ObraConveniosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearConvenioAsync(CrearObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarConvenioAsync(int id, ActualizarObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarConvenioStatusAsync(int id, ActualizarObraConvenioStatusRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Grupos ---
    Task<ResultadoEscrituraDto> CrearGrupoAsync(CrearObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarGrupoAsync(int id, ActualizarObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ReordenarGrupoAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Conceptos ---
    Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(ObraContratoConceptosFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarConceptosEstimacionesAsync(ObraConceptosEstimacionesFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearConceptoAsync(CrearObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarConceptoAsync(int id, ActualizarObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ReordenarConceptoAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> EliminarConceptoAsync(int id, int idContrato, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CargarConceptosDeArchivoAsync(CargarObraContratoConceptosRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarConceptoEstimacionAsync(ActualizarObraConceptoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Partidas del contrato ---
    Task<List<Dictionary<string, object?>>> ConsultarContratoPartidasAsync(ObraContratoPartidasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearContratoPartidaAsync(CrearObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarContratoPartidaAsync(int id, ActualizarObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ReordenarContratoPartidaAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Periodos ---
    Task<List<Dictionary<string, object?>>> ConsultarPeriodosAsync(ObraContratoPeriodosFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearPeriodoAsync(CrearObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarPeriodoAsync(int id, ActualizarObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Estimaciones ---
    Task<ResultadoEscrituraDto> CrearEstimacionAsync(CrearObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarEstimacionAsync(int id, ActualizarObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
