using ISL_Service.Application.DTOs.VentasPedidos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasPedidosService
{
    Task<ConsultaVentasPedidosResponse> ConsultarPendientesAutorizarAsync(ConsultaVentasPedidosRequest request, int idUsuarioToken, CancellationToken ct);
    Task<AutorizarPedidosResponse> AutorizarPedidosAsync(AutorizarPedidosRequest request, int idUsuarioToken, string equipoToken, CancellationToken ct);
}
