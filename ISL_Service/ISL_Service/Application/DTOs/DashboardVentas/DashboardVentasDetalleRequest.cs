namespace ISL_Service.Application.DTOs.DashboardVentas;

public class DashboardVentasDetalleRequest
{
    public DateTime FechaInicial { get; set; }
    public DateTime FechaFinal { get; set; }
    public int? IDEmpresa { get; set; }
    public int? IDAlmacen { get; set; }
    public int? IDAgente { get; set; }
    public int? IDCliente { get; set; }
    public int? IDProducto { get; set; }
    public int? IDCategoria { get; set; }
    public int? IDMarca { get; set; }
    public int? IDTipoDocumento { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
