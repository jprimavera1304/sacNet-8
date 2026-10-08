using ISL_Service.Application.DTOs.OperacionMac;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Operacion diaria de Mac31 vista desde los controladores.
/// <c>idUsuario</c> y <c>equipo</c> vienen del token, nunca del cuerpo.
/// </summary>
public interface IOperacionMacService
{
    // Compras
    Task<List<Dictionary<string, object?>>> ConsultarComprasAsync(ComprasFiltro filtro, int idUsuarioActual, CancellationToken ct);
    Task<OperacionEscrituraResponse> MarcarCompraFacturaAsync(int idCompra, bool esFactura, int idUsuario, string equipo, CancellationToken ct);
    Task<(OperacionEscrituraResponse Resultado, List<Dictionary<string, object?>> Renglones)> CancelarCompraAsync(int idCompra, string motivo, int idUsuario, string equipo, CancellationToken ct);

    // Inventario en transito
    Task<OperacionEscrituraResponse> ProcesarTransitoAsync(ProcesarTransitoRequest r, int idUsuario, string equipo, CancellationToken ct);

    // Cortes
    Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(int idCorte, DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCorteTipoGastosAsync(string corteInfo, DateTime desde, DateTime hasta, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCortesCentroServicioAsync(int idCorte, int folio, DateTime? desde, DateTime? hasta, int idTipoRetiro, CancellationToken ct);
    Task<OperacionEscrituraResponse> CrearCorteAsync(CrearCorteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> CrearCorteCentroServicioAsync(CrearCorteCentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> CancelarCorteAsync(int idCorte, int idUsuario, string equipo, CancellationToken ct);

    // Transferencias bancarias
    Task<List<Dictionary<string, object?>>> ConsultarTransferenciasAsync(TransferenciasFiltro filtro, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarMovimientosTransferenciaAsync(MovimientosTransferenciaFiltro filtro, CancellationToken ct);
    Task<(OperacionEscrituraResponse Resultado, int Cargados)> CargarTransferenciasAsync(CargarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> IdentificarTransferenciasAsync(IdentificarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>
    /// Aplica el movimiento. Devuelve null cuando <c>aplicarA</c> no es "dinero"
    /// ni "cascos": el controlador contesta 400. No se adivina un valor por
    /// omision porque son dos operaciones que mueven dinero distinto.
    /// </summary>
    Task<OperacionEscrituraResponse?> AplicarTransferenciaAsync(AplicarTransferenciaRequest r, int idUsuario, string equipo, CancellationToken ct);

    Task<OperacionEscrituraResponse> CancelarMovimientoTransferenciaAsync(int idMovimiento, int idUsuario, string equipo, CancellationToken ct);

    // Cambios de vigencia
    Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaAsync(CambiosVigenciaFiltro filtro, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaDetalleAsync(int idCambioVigencia, CancellationToken ct);
    Task<OperacionEscrituraResponse> CrearCambioVigenciaAsync(CrearCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> ActualizarCambioVigenciaAsync(int id, ActualizarCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> CancelarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct);
    Task<OperacionEscrituraResponse> RegresarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct);
}
