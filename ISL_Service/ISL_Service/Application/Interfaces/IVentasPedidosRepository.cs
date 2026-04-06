using ISL_Service.Application.DTOs.VentasPedidos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasPedidosRepository
{
    Task<List<VentaPedidoDto>> ConsultarPendientesAutorizarAsync(ConsultaVentasPedidosRequest request, CancellationToken ct);
    Task<AutorizarPedidosResponse> AutorizarPedidosAsync(AutorizarPedidosRequest request, int idUsuarioToken, string equipoToken, CancellationToken ct);
}
