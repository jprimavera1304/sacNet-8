using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Polizas de cheques de aviacion. Todo contra el servidor central
/// (Constantes.AviacionDBServer).
/// </summary>
public interface IAviacionPolizasRepository
{
    Task<List<Dictionary<string, object?>>> ConsultarAsync(AviacionPolizasFiltro filtro, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDetalleAsync(int idPoliza, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarChequesAsync(int idPoliza, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarEncabezadosAsync(int idPolizaEncabezado, int idStatus, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarIngresosAsync(string parametros, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarFianzasAsync(string parametros, CancellationToken ct = default);
    Task<List<Dictionary<string, object?>>> ConsultarDocumentosAsync(string parametros, CancellationToken ct = default);

    Task<ResultadoSp> InsertarAsync(CrearAviacionPolizaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> CancelarAsync(int idPoliza, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarEncabezadoAsync(ActualizarAviacionPolizaEncabezadoRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task<ResultadoSp> ActualizarChequeAsync(int idPolizaCheque, ActualizarAviacionPolizaChequeRequest request, int idUsuario, string equipo, CancellationToken ct = default);
}
