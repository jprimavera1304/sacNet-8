using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los doce procedimientos de descuentos: catalogo, detalle y ajustes de venta.
/// Cuatro del catalogo NO existen en esta base; estan implementados igual, con su
/// nota, porque legacy falla igual al llamarlos.
/// </summary>
public interface IVentasDescuentosRepository
{
    Task<ResultadoLegacyDto> CrearDescuentoAsync(CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDescuentoAsync(DescuentoActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDescuentoAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarDescuentosAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> SimularDescuentoAsync(DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearDetalleAsync(DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDetalleAsync(DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDetalleAsync(DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearAjusteAsync(VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct);
    Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct);
    Task<FilasLegacyDto> SimularAjusteAsync(VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    /// <summary>
    /// El abono de un descuento ya autorizado: sp_n_InsertarVentasPagos con su
    /// `@IDVentasDescuentosAjustes` de verdad, que es lo que marca la liquidacion
    /// como aplicada. El importe lo decide el servicio desde el simulador, no el
    /// cliente.
    /// </summary>
    Task<ResultadoLegacyDto> InsertarPagoDeDescuentoAsync(
        int idVentasDescuentosAjustes,
        int idVenta,
        int idTipoPago,
        decimal abono,
        string? observaciones,
        int idUsuario,
        string equipo,
        CancellationToken ct);
}

/// <summary>Las reglas de descuentos que no son del procedimiento.</summary>
public interface IVentasDescuentosService
{
    Task<ResultadoLegacyDto> CrearDescuentoAsync(DescuentoCrearRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDescuentoAsync(DescuentoActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDescuentoAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarDescuentosAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> SimularDescuentoAsync(DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearDetalleAsync(DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDetalleAsync(DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDetalleAsync(DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearAjusteAsync(VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct);
    Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct);
    Task<FilasLegacyDto> SimularAjusteAsync(VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    /// <summary>
    /// Aplica una liquidacion de descuento autorizada: es el paso que mete el
    /// abono y baja el saldo de la remision. El cuerpo solo trae el id; el
    /// importe, la remision y el tipo de pago salen del ajuste guardado y del
    /// simulador, y el usuario y el equipo del token.
    /// </summary>
    Task<VentaDescuentoAjusteAplicarResponse> AplicarAjusteAsync(
        VentaDescuentoAjusteAplicarRequest request, int idUsuario, string equipo, CancellationToken ct);
}
