using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.OperacionMac;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Operacion diaria de Mac31: compras, inventario en transito, cortes de caja,
/// transferencias bancarias y cambios de vigencia.
///
/// Las lecturas devuelven los renglones tal cual los da el procedimiento (lista
/// de diccionarios) por la misma razon que Prestamos: los sp_n_ ya devuelven sus
/// columnas en camelCase y copiarlas a un DTO tipado solo agrega un lugar donde
/// equivocarse — si legacy renombra una columna, un DTO la deja en cero
/// calladito, y en dinero un cero silencioso es peor que un campo faltante.
/// </summary>
public interface IOperacionMacRepository
{
    // --- Compras
    Task<List<Dictionary<string, object?>>> ConsultarComprasAsync(ComprasFiltro filtro, int idUsuarioActual, CancellationToken ct);
    Task<ResultadoLegacy> MarcarCompraFacturaAsync(int idCompra, bool esFactura, int idUsuario, string equipo, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, List<Dictionary<string, object?>> Renglones)> CancelarCompraAsync(int idCompra, string motivo, int idUsuario, string equipo, CancellationToken ct);

    // --- Inventario en transito
    Task<(ResultadoLegacy Resultado, int Id)> ProcesarTransitoAsync(ProcesarTransitoRequest r, int idUsuario, string equipo, CancellationToken ct);

    // --- Cortes
    Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(int idCorte, DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCorteTipoGastosAsync(string corteInfo, DateTime desde, DateTime hasta, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCortesCentroServicioAsync(int idCorte, int folio, DateTime? desde, DateTime? hasta, int idTipoRetiro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarCorteAsync(CrearCorteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarCorteCentroServicioAsync(CrearCorteCentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> CancelarCorteAsync(int idCorte, int idUsuario, string equipo, CancellationToken ct);

    // --- Transferencias bancarias
    Task<List<Dictionary<string, object?>>> ConsultarTransferenciasAsync(TransferenciasFiltro filtro, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarMovimientosTransferenciaAsync(MovimientosTransferenciaFiltro filtro, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int IdBancoTransferencia, int Cargados)> CargarTransferenciasAsync(CargarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> IdentificarTransferenciasAsync(IdentificarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> AplicarTransferenciaAsync(AplicarTransferenciaRequest r, string sp, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>
    /// El estado real de UN movimiento, para la guarda del servidor. Lo lee
    /// sp_w_EstadoBancoTransferenciaMovimiento (solo lectura).
    /// </summary>
    Task<EstadoMovimientoTransferencia> LeerEstadoMovimientoTransferenciaAsync(int idMovimiento, CancellationToken ct);
    Task<ResultadoLegacy> CancelarMovimientoTransferenciaAsync(int idMovimiento, int idUsuario, string equipo, CancellationToken ct);

    // --- Cambios de vigencia
    Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaAsync(CambiosVigenciaFiltro filtro, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaDetalleAsync(int idCambioVigencia, CancellationToken ct);
    Task<(ResultadoLegacy Resultado, int Id)> InsertarCambioVigenciaAsync(CrearCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarCambioVigenciaAsync(int id, ActualizarCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> CancelarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> RegresarCambioVigenciaAsync(int id, int idUsuario, string equipo, CancellationToken ct);
}
