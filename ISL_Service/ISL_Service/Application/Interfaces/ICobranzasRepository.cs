using ISL_Service.Application.DTOs.Cobranzas;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>Cobranzas: el fajo de remisiones que se lleva un repartidor.</summary>
public interface ICobranzasRepository
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(CobranzasFiltro filtro, int idUsuarioActual, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(CobranzaDetalleFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ValidarFolioAsync(CobranzaValidarFolioFiltro filtro, int idUsuarioActual, CancellationToken ct = default);

    Task<ResultadoSp> InsertarAsync(CrearCobranzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarDetalleAsync(int idCobranza, ActualizarCobranzaDetalleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> CancelarAsync(CancelarCobranzasRequest request, int idUsuario, string equipo, CancellationToken ct = default);

    /// <summary>
    /// La fecha de operacion del negocio, leida con un SELECT TOP 1 que NO
    /// escribe. Legacy la pedia en el cuerpo de la peticion, y el reporte de
    /// cobranza pendiente la necesita para decidir que esta vencido.
    /// </summary>
    Task<DateOnly> LeerFechaDeOperacionAsync(CancellationToken ct = default);

    Task<List<Dictionary<string, object?>>> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosFiltro filtro, DateOnly fechaOperacion, CancellationToken ct = default);
}

/// <summary>Reglas de las cobranzas.</summary>
public interface ICobranzasService
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(CobranzasFiltro filtro, int idUsuarioActual, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(CobranzaDetalleFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ValidarFolioAsync(CobranzaValidarFolioFiltro filtro, int idUsuarioActual, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarPendientesVencidosAsync(CobranzaPendientesVencidosFiltro filtro, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearAsync(CrearCobranzaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarDetalleAsync(int idCobranza, ActualizarCobranzaDetalleRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CancelarAsync(CancelarCobranzasRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
