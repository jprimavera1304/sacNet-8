using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasPagosOperaciones;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los trece procedimientos de pagos y de operaciones sueltas de la remision que
/// faltaban. Ninguno es nuevo.
/// </summary>
public interface IVentasPagosOperacionesRepository
{
    Task<ResultadoLegacyDto> DeclararIncobrableAsync(VentaIncobrableRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> AbonarKilosAsync(VentaPagoKilosRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAbonoKilosAsync(VentaPagoKilosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>
    /// La limpieza de los pagos de centro de servicio. Recibe los dos ids,
    /// aunque legacy los mandaba siempre en cero (ver el metodo).
    /// </summary>
    Task<ResultadoLegacyDto> LimpiarPagosCentroServicioAsync(int idPagoVenta, int idVenta, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearNuevoPagoAsync(NuevoPagoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> AplicarNuevoPagoAsync(NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarNuevoPagoAsync(NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarTransferenciaAsync(TransferenciaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarTransferenciaPagoAsync(TransferenciaPagoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> ReasignarAsync(VentasReasignarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarVentaAsync(VentaActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> MarcarPagoPorKiloAsync(VentasPagoPorKiloRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarDetalleAsync(VentasDetalleConsultaRequest request, CancellationToken ct);
}

/// <summary>Las reglas de pagos que no son del procedimiento.</summary>
public interface IVentasPagosOperacionesService
{
    Task<ResultadoLegacyDto> DeclararIncobrableAsync(VentaIncobrableRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> AbonarKilosAsync(VentaPagoKilosRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarAbonoKilosAsync(VentaPagoKilosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> LimpiarPagosCentroServicioAsync(CancellationToken ct);

    Task<ResultadoLegacyDto> CrearNuevoPagoAsync(NuevoPagoCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> AplicarNuevoPagoAsync(NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarNuevoPagoAsync(NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarTransferenciaAsync(TransferenciaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> CancelarTransferenciaPagoAsync(TransferenciaPagoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> ReasignarAsync(VentasReasignarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarVentaAsync(VentaActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> MarcarPagoPorKiloAsync(VentasPagoPorKiloRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarDetalleAsync(VentasDetalleConsultaRequest request, CancellationToken ct);
}
