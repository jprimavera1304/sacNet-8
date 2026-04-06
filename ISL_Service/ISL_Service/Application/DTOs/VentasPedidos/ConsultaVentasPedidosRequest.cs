namespace ISL_Service.Application.DTOs.VentasPedidos;

public class ConsultaVentasPedidosRequest
{
    public int IDPedido { get; set; }
    public int IDVenta { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDUsuario { get; set; }
    public int IDUsuarioActual { get; set; }
    public int IDAgente { get; set; }
    public int IDTipoDocumento { get; set; }
    public int IDStatusPedido { get; set; }
    public string? FolioInicial { get; set; }
    public string? FolioFinal { get; set; }
    public string? FechaInicial { get; set; }
    public string? FechaFinal { get; set; }
    public string? FechaCancelInicial { get; set; }
    public string? FechaCancelFinal { get; set; }
    public int Formato { get; set; }
}
