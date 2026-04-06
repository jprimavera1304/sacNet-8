namespace ISL_Service.Application.DTOs.VentasPedidos;

public class AutorizarPedidosResponse
{
    public List<PedidoResultadoDto> Pedidos { get; set; } = new();
    public string? IdsVenta { get; set; }

    public AutorizarPedidosResponse()
    {
    }

    public AutorizarPedidosResponse(List<PedidoResultadoDto> pedidos, string? idsVenta)
    {
        Pedidos = pedidos ?? new List<PedidoResultadoDto>();
        IdsVenta = idsVenta;
    }
}
