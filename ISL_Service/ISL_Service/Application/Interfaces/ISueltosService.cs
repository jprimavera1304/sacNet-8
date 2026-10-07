using ISL_Service.Application.DTOs.Sueltos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Proyectos, recordatorios, servicios y sincronizacion, vistos desde los
/// controladores. El id del usuario y el equipo salen del token.
/// </summary>
public interface ISueltosService
{
    Task<List<Dictionary<string, object?>>> ConsultarRecordatoriosAsync(RecordatoriosFiltro filtro, CancellationToken ct);
    Task<SueltoEscrituraResponse> CrearRecordatorioAsync(CrearRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<SueltoEscrituraResponse> ActualizarRecordatorioAsync(int id, ActualizarRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct);

    Task<List<Dictionary<string, object?>>> ConsultarServiciosAsync(ServiciosFiltro filtro, CancellationToken ct);
    Task<(SueltoEscrituraResponse Resultado, List<Dictionary<string, object?>> Renglones)> CrearServicioAsync(CrearServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<SueltoEscrituraResponse> CancelarServicioAsync(int idServicio, int idUsuario, string equipo, CancellationToken ct);

    Task<List<Dictionary<string, object?>>> ConsultarSincronizacionAsync(SincronizacionFiltro filtro, CancellationToken ct);

    Task<List<Dictionary<string, object?>>> ConsultarProyectosAsync(ProyectosFiltro filtro, CancellationToken ct);
    Task<SueltoEscrituraResponse> CrearProyectoAsync(ProyectoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<SueltoEscrituraResponse> ActualizarProyectoAsync(int id, ActualizarProyectoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<SueltoEscrituraResponse> ActualizarFacturasProyectoAsync(int id, FacturasProyectoRequest r, CancellationToken ct);
}
