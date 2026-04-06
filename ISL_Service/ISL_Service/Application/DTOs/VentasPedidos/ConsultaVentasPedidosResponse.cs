namespace ISL_Service.Application.DTOs.VentasPedidos;

public class ConsultaVentasPedidosResponse
{
    public List<VentaPedidoDto> VentasPedidos { get; set; } = new();

    public ConsultaVentasPedidosResponse()
    {
    }

    public ConsultaVentasPedidosResponse(List<VentaPedidoDto> ventasPedidos)
    {
        VentasPedidos = ventasPedidos ?? new List<VentaPedidoDto>();
    }
}
