using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Sueltos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de los cuatro sueltos. Delgada: traduce el par
/// result/mensaje de legacy y nada mas.
/// </summary>
public class SueltosService : ISueltosService
{
    private readonly ISueltosRepository _repo;

    public SueltosService(ISueltosRepository repo)
    {
        _repo = repo;
    }

    private static SueltoEscrituraResponse Traducir(ResultadoLegacy r, int id = 0)
        => new() { Guardo = r.Ok, Mensaje = r.Mensaje, Id = id };

    // --- Recordatorios

    public Task<List<Dictionary<string, object?>>> ConsultarRecordatoriosAsync(RecordatoriosFiltro filtro, CancellationToken ct)
        => _repo.ConsultarRecordatoriosAsync(filtro, ct);

    public async Task<SueltoEscrituraResponse> CrearRecordatorioAsync(CrearRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarRecordatorioAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    public async Task<SueltoEscrituraResponse> ActualizarRecordatorioAsync(int id, ActualizarRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarRecordatorioAsync(id, r, idUsuario, equipo, ct), id);

    // --- Servicios

    public Task<List<Dictionary<string, object?>>> ConsultarServiciosAsync(ServiciosFiltro filtro, CancellationToken ct)
        => _repo.ConsultarServiciosAsync(filtro, ct);

    public async Task<(SueltoEscrituraResponse, List<Dictionary<string, object?>>)> CrearServicioAsync(
        CrearServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, renglones) = await _repo.InsertarServicioAsync(r, idUsuario, equipo, ct);
        return (Traducir(resultado), renglones);
    }

    public async Task<SueltoEscrituraResponse> CancelarServicioAsync(int idServicio, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.CancelarServicioAsync(idServicio, idUsuario, equipo, ct), idServicio);

    // --- Sincronizacion

    public Task<List<Dictionary<string, object?>>> ConsultarSincronizacionAsync(SincronizacionFiltro filtro, CancellationToken ct)
        => _repo.ConsultarSincronizacionAsync(filtro, ct);

    // --- Proyectos

    public Task<List<Dictionary<string, object?>>> ConsultarProyectosAsync(ProyectosFiltro filtro, CancellationToken ct)
        => _repo.ConsultarProyectosAsync(filtro, ct);

    public async Task<SueltoEscrituraResponse> CrearProyectoAsync(ProyectoRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarProyectoAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    public async Task<SueltoEscrituraResponse> ActualizarProyectoAsync(int id, ActualizarProyectoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarProyectoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<SueltoEscrituraResponse> ActualizarFacturasProyectoAsync(int id, FacturasProyectoRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarFacturasProyectoAsync(id, r, ct), id);
}
