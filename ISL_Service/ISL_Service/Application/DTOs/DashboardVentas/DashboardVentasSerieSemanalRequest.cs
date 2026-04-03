namespace ISL_Service.Application.DTOs.DashboardVentas;

public class DashboardVentasSerieSemanalRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? IDEmpresa { get; set; }
    public int? IDAlmacen { get; set; }
    public int? IDAgente { get; set; }
    public int? IDCliente { get; set; }
    public int? IDProducto { get; set; }
    public int? IDCategoria { get; set; }
    public int? IDMarca { get; set; }
    public int? IDTipoDocumento { get; set; }
}
