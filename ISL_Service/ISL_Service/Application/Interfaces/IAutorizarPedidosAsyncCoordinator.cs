using ISL_Service.Application.DTOs.VentasPedidos;

namespace ISL_Service.Application.Interfaces;

public interface IAutorizarPedidosAsyncCoordinator
{
    AutorizarPedidosAsyncStartResponse Start(AutorizarPedidosRequest request, int idUsuarioToken, string equipoToken);
    AutorizarPedidosAsyncStatusResponse? GetStatus(string operationId);
}
