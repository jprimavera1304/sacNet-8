using ISL_Service.Application.DTOs.RecepcionCascos;

namespace ISL_Service.Application.Interfaces;

public interface IRecepcionCascosService
{
    Task<List<TipoRecepcionCascoDto>> ConsultarTiposAsync(CancellationToken ct = default);

    Task<List<RepartidorRecepcionCascoDto>> ConsultarRepartidoresAsync(CancellationToken ct = default);

    Task<List<RecepcionCascoDto>> ConsultarAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? idRepartidor,
        int? estatus, bool incluirCanceladas, CancellationToken ct = default);

    Task<List<DetalleRecepcionCascoDto>> ConsultarDetalleAsync(
        int idRecepcion, CancellationToken ct = default);

    Task<List<BitacoraRecepcionCascoDto>> ConsultarBitacoraAsync(
        int idRecepcion, CancellationToken ct = default);

    Task<int> EstatusDeAsync(int idRecepcion, CancellationToken ct = default);

    Task<ResultadoRecepcionCascoDto> CrearAsync(
        CrearRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<ResultadoRecepcionCascoDto> ActualizarAsync(
        ActualizarRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<ResultadoCancelacionRecepcionCascoDto> CancelarAsync(
        CancelarRecepcionesCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<DatosReporteRecepcionCascos> ConsultarDatosDeReporteAsync(
        DateTime fechaInicio, DateTime fechaFin, bool incluirCanceladas = false,
        CancellationToken ct = default);

    /// <summary>
    /// El logo ya como data URI y el nombre con el que la empresa se presenta.
    /// Devuelve logo vacio si el archivo no esta: el papel sale sin logo antes
    /// que con una imagen rota.
    /// </summary>
    Task<(string Logo, string Empresa)> ConsultarMarcaParaReporteAsync(
        CancellationToken ct = default);
}
