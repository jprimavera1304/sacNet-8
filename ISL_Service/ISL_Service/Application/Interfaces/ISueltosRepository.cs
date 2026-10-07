using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Sueltos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Proyectos de obra por consulta ciudadana, recordatorios, servicios y la cola de
/// sincronizacion de cajas. Ocho de sus nueve procedimientos no estan instalados en
/// Produccion_svr; recordatorios si.
/// </summary>
public interface ISueltosRepository
{
    // Recordatorios
    Task<List<Dictionary<string, object?>>> ConsultarRecordatoriosAsync(RecordatoriosFiltro filtro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarRecordatorioAsync(CrearRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarRecordatorioAsync(int id, ActualizarRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);

    // Servicios
    Task<List<Dictionary<string, object?>>> ConsultarServiciosAsync(ServiciosFiltro filtro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, List<Dictionary<string, object?>> Renglones)> InsertarServicioAsync(CrearServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> CancelarServicioAsync(int idServicio, int idUsuario, string equipo, CancellationToken ct);

    // Sincronizacion
    Task<List<Dictionary<string, object?>>> ConsultarSincronizacionAsync(SincronizacionFiltro filtro, CancellationToken ct);

    // Proyectos
    Task<List<Dictionary<string, object?>>> ConsultarProyectosAsync(ProyectosFiltro filtro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarProyectoAsync(ProyectoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarProyectoAsync(int id, ActualizarProyectoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarFacturasProyectoAsync(int id, FacturasProyectoRequest r, CancellationToken ct);
}
