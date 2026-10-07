using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;

namespace ISL_Service.Application.Interfaces;

/// <summary>Reglas de las polizas de cheques de aviacion.</summary>
public interface IAviacionPolizasService
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionPolizasFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(int idPoliza, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarChequesAsync(int idPoliza, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEncabezadosAsync(int idPolizaEncabezado, int idStatus, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarIngresosAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarFianzasAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDocumentosAsync(AviacionPolizaReporteFiltro filtro, CancellationToken ct = default);

    Task<ResultadoEscrituraDto> CrearAsync(CrearAviacionPolizaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> CancelarAsync(int idPoliza, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarEncabezadoAsync(ActualizarAviacionPolizaEncabezadoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoEscrituraDto> ActualizarChequeAsync(int idPolizaCheque, ActualizarAviacionPolizaChequeRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
