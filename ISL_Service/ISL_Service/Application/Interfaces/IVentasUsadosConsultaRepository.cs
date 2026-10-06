using ISL_Service.Application.DTOs.ConsultasLegacy;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los detalles de usados (cascos) de Mac31. Todo de solo lectura.
/// </summary>
public interface IVentasUsadosConsultaRepository
{
    Task<ConsultaFilasResponse> ConsultarMovimientoDetalleAsync(int idUsadoMovimiento, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCobroUsadoDetalleAsync(int idVentaCobroUsado, int idCobro, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCobroDineroUsadoDetalleAsync(int idCobroDineroUsado, int idCobro, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarExcedenteDetalleAsync(
        int idVentaUsadoExcedente, int idCobro, int idCliente, int idPagoVenta, CancellationToken ct);

    Task<ConsultaFilasResponse> ConsultarCreditoLogAsync(int idVenta, CancellationToken ct);
}
