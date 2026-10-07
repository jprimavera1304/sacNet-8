using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraCatalogos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Reglas de negocio de las altas y modificaciones de los catalogos de obra.
/// </summary>
public interface IObraCatalogosEscrituraService
{
    Task<ResultadoEscrituraDto> CrearTipoContratacionAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarTipoContratacionAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearTipoDocumentoAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarTipoDocumentoAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearTipoObraAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarTipoObraAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearTipoRecursoAsync(CrearObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarTipoRecursoAsync(int id, ActualizarObraCatalogoSimpleRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearPuestoAsync(CrearObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarPuestoAsync(int id, ActualizarObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearResidenteAsync(CrearObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarResidenteAsync(int id, ActualizarObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearGerenteAsync(CrearObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarGerenteAsync(int id, ActualizarObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearUnidadMedidaAsync(CrearObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarUnidadMedidaAsync(int id, ActualizarObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<List<Dictionary<string, object?>>> ConsultarPartidasAsync(ObraPartidasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CrearPartidaAsync(CrearObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarPartidaAsync(int id, ActualizarObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearProveedorAsync(CrearObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarProveedorAsync(int id, ActualizarObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
