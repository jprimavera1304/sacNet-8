using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de la operacion diaria.
///
/// Toma UNA decision de negocio, y es la de aplicar una transferencia: traduce
/// "dinero" o "cascos" al procedimiento que corresponde, y si el valor no es
/// ninguno de los dos devuelve null para que el controlador conteste 400. En
/// legacy eran dos banderas independientes con dos `if` sueltos: con las dos en
/// true ganaba cascos, y con las dos en false se armaba un SqlCommand con nombre
/// vacio y el usuario leia "[No se pudo procesar ] - ...". Decidir aqui, con un
/// valor obligatorio, elimina los dos estados imposibles.
/// </summary>
public class OperacionMacService : IOperacionMacService
{
    private readonly IOperacionMacRepository _repo;

    public OperacionMacService(IOperacionMacRepository repo)
    {
        _repo = repo;
    }

    private static OperacionEscrituraResponse Traducir(ResultadoLegacy r, int id = 0)
        => new() { Guardo = r.Ok, Mensaje = r.Mensaje, Id = id };

    // --- Compras

    public Task<List<Dictionary<string, object?>>> ConsultarComprasAsync(ComprasFiltro filtro, int idUsuarioActual, CancellationToken ct)
        => _repo.ConsultarComprasAsync(filtro, idUsuarioActual, ct);

    public async Task<OperacionEscrituraResponse> MarcarCompraFacturaAsync(int idCompra, bool esFactura, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.MarcarCompraFacturaAsync(idCompra, esFactura, idUsuario, equipo, ct), idCompra);

    public async Task<(OperacionEscrituraResponse, List<Dictionary<string, object?>>)> CancelarCompraAsync(
        int idCompra, string motivo, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, renglones) = await _repo.CancelarCompraAsync(idCompra, motivo, idUsuario, equipo, ct);
        return (Traducir(resultado, idCompra), renglones);
    }

    // --- Inventario en transito

    public async Task<OperacionEscrituraResponse> ProcesarTransitoAsync(ProcesarTransitoRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.ProcesarTransitoAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    // --- Cortes

    public Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(int idCorte, DateTime? desde, DateTime? hasta, CancellationToken ct)
        => _repo.ConsultarCortesAsync(idCorte, desde, hasta, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCorteTipoGastosAsync(string corteInfo, DateTime desde, DateTime hasta, CancellationToken ct)
        => _repo.ConsultarCorteTipoGastosAsync(corteInfo, desde, hasta, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCortesCentroServicioAsync(int idCorte, int folio, DateTime? desde, DateTime? hasta, int idTipoRetiro, CancellationToken ct)
        => _repo.ConsultarCortesCentroServicioAsync(idCorte, folio, desde, hasta, idTipoRetiro, ct);

    public async Task<OperacionEscrituraResponse> CrearCorteAsync(CrearCorteRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarCorteAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    public async Task<OperacionEscrituraResponse> CrearCorteCentroServicioAsync(CrearCorteCentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarCorteCentroServicioAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    public async Task<OperacionEscrituraResponse> CancelarCorteAsync(int idCorte, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.CancelarCorteAsync(idCorte, idUsuario, equipo, ct), idCorte);

    // --- Transferencias bancarias

    public Task<List<Dictionary<string, object?>>> ConsultarTransferenciasAsync(TransferenciasFiltro filtro, CancellationToken ct)
        => _repo.ConsultarTransferenciasAsync(filtro, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarMovimientosTransferenciaAsync(MovimientosTransferenciaFiltro filtro, CancellationToken ct)
        => _repo.ConsultarMovimientosTransferenciaAsync(filtro, ct);

    public async Task<(OperacionEscrituraResponse, int)> CargarTransferenciasAsync(
        CargarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, idCabecera, cargados) = await _repo.CargarTransferenciasAsync(r, idUsuario, equipo, ct);
        return (Traducir(resultado, idCabecera), cargados);
    }

    public async Task<OperacionEscrituraResponse> IdentificarTransferenciasAsync(IdentificarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.IdentificarTransferenciasAsync(r, idUsuario, equipo, ct), r.IdMovimientoPrincipal());

    public async Task<OperacionEscrituraResponse?> AplicarTransferenciaAsync(AplicarTransferenciaRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var sp = AplicacionTransferencia.SpDe(r.AplicarA);
        if (sp is null) return null;

        return Traducir(await _repo.AplicarTransferenciaAsync(r, sp, idUsuario, equipo, ct), r.IdMovimiento);
    }

    public async Task<OperacionEscrituraResponse> CancelarMovimientoTransferenciaAsync(int idMovimiento, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.CancelarMovimientoTransferenciaAsync(idMovimiento, idUsuario, equipo, ct), idMovimiento);

    // --- Cambios de vigencia

    public Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaAsync(CambiosVigenciaFiltro filtro, CancellationToken ct)
        => _repo.ConsultarCambiosVigenciaAsync(filtro, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaDetalleAsync(int idCambioVigencia, CancellationToken ct)
        => _repo.ConsultarCambiosVigenciaDetalleAsync(idCambioVigencia, ct);

    public async Task<OperacionEscrituraResponse> CrearCambioVigenciaAsync(CrearCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarCambioVigenciaAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    public async Task<OperacionEscrituraResponse> ActualizarCambioVigenciaAsync(int id, ActualizarCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarCambioVigenciaAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<OperacionEscrituraResponse> CancelarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.CancelarCambioVigenciaAsync(id, idUsuario, equipo, ct), id);

    public async Task<OperacionEscrituraResponse> RegresarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.RegresarCambioVigenciaAsync(id, idUsuario, equipo, ct), id);
}

/// <summary>Ayudante de lectura para la respuesta de identificar.</summary>
internal static class IdentificarTransferenciasExtensiones
{
    /// <summary>
    /// El primer movimiento de la lista, solo para que la respuesta traiga un id
    /// con el que la pantalla pueda refrescarse. La operacion es sobre varios y el
    /// id no identifica el conjunto: por eso no se usa para nada mas.
    /// </summary>
    public static int IdMovimientoPrincipal(this IdentificarTransferenciasRequest r)
        => r.IdsMovimiento.Count > 0 ? r.IdsMovimiento[0] : 0;
}
