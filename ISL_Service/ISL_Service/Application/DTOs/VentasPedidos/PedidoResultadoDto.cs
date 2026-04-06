namespace ISL_Service.Application.DTOs.VentasPedidos;

public class PedidoResultadoDto
{
    public int Result { get; set; }
    public string? Mensaje { get; set; }
    public int IDPedido { get; set; }
    public int Pedido { get; set; }
    public int IDVenta { get; set; }
}
