using ISL_Service.Application.DTOs.RecepcionCascos;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Recepcion de cascos usados. TODO lo que escribe pasa por sp_w_*.
/// De legacy solo se LEE, y la lectura ocurre dentro de los procedimientos.
/// </summary>
public interface IRecepcionCascosRepository
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

    /// <summary>Existe y en que estatus esta. 0 = no existe.</summary>
    Task<int> EstatusDeAsync(int idRecepcion, CancellationToken ct = default);

    Task<ResultadoRecepcionCascoDto> InsertarAsync(
        CrearRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<ResultadoRecepcionCascoDto> ActualizarAsync(
        ActualizarRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<ResultadoCancelacionRecepcionCascoDto> CancelarAsync(
        IEnumerable<int> ids, string motivo, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default);

    Task<DatosReporteRecepcionCascos> ConsultarDatosDeReporteAsync(
        DateTime fechaInicio, DateTime fechaFin, bool incluirCanceladas = false,
        CancellationToken ct = default);

    /// <summary>
    /// La marca de la empresa para el papel: carpeta de imagenes, archivo del
    /// logo y nombre con el que la empresa se presenta. Sale de Constantes,
    /// igual que los reportes de legacy, porque cada empresa tiene el suyo.
    /// </summary>
    Task<(string PathImagenes, string Logo, string Empresa)> ConsultarMarcaEmpresaAsync(
        CancellationToken ct = default);
}
