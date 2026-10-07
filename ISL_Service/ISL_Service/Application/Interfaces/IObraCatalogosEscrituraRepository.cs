using ISL_Service.Application.DTOs.ObraCatalogos;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Altas y modificaciones de los catalogos del modulo de obra (MC), contra los
/// mismos sp_n_ que llamaba MacServicios2. Ninguno se toca.
/// </summary>
public interface IObraCatalogosEscrituraRepository
{
    // --- Los cuatro catalogos de un solo nombre ---
    Task<ResultadoSp> InsertarTipoContratacionAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarTipoContratacionAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarTipoDocumentoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarTipoDocumentoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarTipoObraAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarTipoObraAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarTipoRecursoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarTipoRecursoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Los que tienen banderas propias ---
    Task<ResultadoSp> InsertarPuestoAsync(CrearObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarPuestoAsync(int id, ActualizarObraPuestoRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarResidenteAsync(CrearObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarResidenteAsync(int id, ActualizarObraResidenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarGerenteAsync(CrearObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarGerenteAsync(int id, ActualizarObraGerenteRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    Task<ResultadoSp> InsertarUnidadMedidaAsync(CrearObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarUnidadMedidaAsync(int id, ActualizarObraUnidadMedidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Partidas presupuestales ---
    Task<List<Dictionary<string, object?>>> ConsultarPartidasAsync(ObraPartidasFiltro filtro, CancellationToken ct = default);
    Task<ResultadoSp> InsertarPartidaAsync(CrearObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarPartidaAsync(int id, ActualizarObraPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    // --- Padron de proveedores de obra ---
    Task<ResultadoSp> InsertarProveedorAsync(CrearObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarProveedorAsync(int id, ActualizarObraProveedorRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
