using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los cuatro procedimientos de pedidos que faltaban: procesar una venta,
/// procesar una compra, cancelar y ajustar los cascos a credito.
/// </summary>
public interface IPedidosOperacionesRepository
{
    /// <summary>
    /// Procesa UN pedido de venta. El lote se arma arriba, a proposito: legacy
    /// tampoco lo hace en una transaccion y envolverlo cambiaria el efecto en la
    /// base ante un fallo a mitad.
    /// </summary>
    Task<PedidoResultadoLote> ProcesarVentaAsync(
        int idPedido, PedidosProcesarVentaRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> ProcesarCompraAsync(
        PedidoProcesarCompraRequest request, string equipo, CancellationToken ct);

    /// <summary>Cancela UN pedido.</summary>
    Task<PedidoResultadoLote> CancelarAsync(
        int idPedido, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> AjustarUsadosCreditoAsync(
        PedidoUsadoCreditoRequest request, CancellationToken ct);
}

/// <summary>Las reglas de pedidos que no son del procedimiento.</summary>
public interface IPedidosOperacionesService
{
    Task<PedidosLoteResponse> ProcesarVentasAsync(
        PedidosProcesarVentaRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> ProcesarCompraAsync(
        PedidoProcesarCompraRequest request, string equipo, CancellationToken ct);

    Task<PedidosLoteResponse> CancelarAsync(
        PedidosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> AjustarUsadosCreditoAsync(
        PedidoUsadoCreditoRequest request, CancellationToken ct);
}
