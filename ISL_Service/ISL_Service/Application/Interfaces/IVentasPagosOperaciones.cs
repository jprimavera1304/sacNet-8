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

    /// <summary>
    /// LAS PARTIDAS PARA FACTURAR. Es la unica puerta que debe usar la
    /// preparacion del CFDI, y existe para que no se pueda entrar por la otra.
    /// </summary>
    /// <remarks>
    /// Siempre lee de `sp_n_VentasDetalleMultiple`, tambien cuando es UNA sola
    /// remision. No es una preferencia: los dos procedimientos llaman
    /// `PrecioListaConIvaRnd` a cosas distintas -el NETO por pieza en el
    /// multiple, el precio de LISTA en el simple- y
    /// `ReglasPreparacionFactura` esta escrita contra el multiple. Medido en
    /// MacZ, remision 154134: el simple dice 4,480.00 donde el neto es
    /// 2,388.68, asi que el concepto del CFDI salia con el precio de lista y
    /// el comprobante no sumaba lo que se cobra.
    ///
    /// ESTA COMO METODO Y NO COMO UNA BANDERA QUE HAYA QUE ACORDARSE DE PONER
    /// porque el defecto original fue exactamente eso: el camino correcto
    /// existia -con dos o mas remisiones las cifras salian bien- y el caso
    /// normal, UNA remision, se iba por el otro. Una bandera se olvida; una
    /// puerta distinta, no.
    ///
    /// Facturar no simula descuentos, asi que no pierde nada por no usar el
    /// simple (que es el unico que recibe `@IDDescuento`).
    ///
    /// Lo vigila `PrecioDelConceptoContraLaBaseTests`.
    /// </remarks>
    Task<FilasLegacyDto> ConsultarDetalleParaFacturarAsync(
        IReadOnlyCollection<int> idsVenta, CancellationToken ct);
}
