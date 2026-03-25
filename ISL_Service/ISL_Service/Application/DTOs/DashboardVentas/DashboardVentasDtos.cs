namespace ISL_Service.Application.DTOs.DashboardVentas;

public class DashboardVentasKpisDto
{
    public decimal VentaTotal { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal DescuentoTotal { get; set; }
    public decimal Tickets { get; set; }
    public decimal UnidadesVendidas { get; set; }
    public decimal TicketPromedio { get; set; }
    public decimal MargenPorcentaje { get; set; }
}

public class DashboardVentasSerieMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string? MesNombre { get; set; }
    public decimal VentaTotal { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal Tickets { get; set; }
}

public class DashboardTopProductoDto
{
    public int IDProducto { get; set; }
    public string? Codigo { get; set; }
    public string? Producto { get; set; }
    public decimal CantidadVendida { get; set; }
    public decimal VentaTotal { get; set; }
    public decimal GananciaTotal { get; set; }
}

public class DashboardTopClienteDto
{
    public int IDCliente { get; set; }
    public string? Cliente { get; set; }
    public decimal VentaTotal { get; set; }
    public decimal Tickets { get; set; }
}

public class DashboardVentasAlmacenDto
{
    public int IDAlmacen { get; set; }
    public string? Almacen { get; set; }
    public decimal VentaTotal { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal Tickets { get; set; }
}

public class DashboardVentasAgenteDto
{
    public int IDAgente { get; set; }
    public string? Nombre { get; set; }
    public decimal VentaTotal { get; set; }
    public decimal GananciaTotal { get; set; }
    public decimal Tickets { get; set; }
}

public class DashboardVentasDetalleDto
{
    public int IDVenta { get; set; }
    public DateTime FechaEmision { get; set; }
    public string? Folio { get; set; }
    public string? Empresa { get; set; }
    public string? Almacen { get; set; }
    public string? Agente { get; set; }
    public string? Documento { get; set; }
    public string? Cliente { get; set; }
    public string? Codigo { get; set; }
    public string? Producto { get; set; }
    public string? Categoria { get; set; }
    public string? Marca { get; set; }
    public decimal Cantidad { get; set; }
    public decimal ImporteDetalle { get; set; }
    public decimal GananciaDetalle { get; set; }
    public decimal DescuentoDetalle { get; set; }
    public decimal ImporteVenta { get; set; }
    public decimal GananciaVenta { get; set; }
}
