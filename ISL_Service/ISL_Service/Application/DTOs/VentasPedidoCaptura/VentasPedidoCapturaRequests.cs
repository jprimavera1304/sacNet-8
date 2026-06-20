namespace ISL_Service.Application.DTOs.VentasPedidoCaptura;

public class PedidoClienteBuscarRequest
{
    public int IDCliente { get; set; }
    public string? Numero { get; set; }
    public int IDEmpresaCS { get; set; }
}

public class PedidoProductoBuscarRequest
{
    public int IDAlmacen { get; set; }
    public int IDProducto { get; set; }
    public int IDCliente { get; set; }
    public string? Clave { get; set; }
    public int IDGrupoCategoria { get; set; }
    public int IDEmpresaCS { get; set; }
}

public class PedidoAgregarDetalleRequest
{
    public int IDPedido { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDAgenteAutoriza { get; set; }
    public int IDDomicilio { get; set; }
    public int IDTipoDocumento { get; set; } = 5;
    public string? Fecha { get; set; }
    public int IDAlmacen { get; set; }
    public int IDProducto { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal SaldoVencido { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal Disponible { get; set; }
    public int MaxDiasVencidos { get; set; }
    public int MaxDiasVencidosAcumuladores { get; set; }
    public int MaxDiasVencidosAceites { get; set; }
    public int MaxDiasVencidosCascos { get; set; }
    public string? Observaciones { get; set; }
    public string? ObservacionesDetalle { get; set; }
    public decimal ServicioPrecioConIva { get; set; }
    public string? ServicioNombre { get; set; }
    public string? ServicioDireccion1 { get; set; }
    public string? ServicioDireccion2 { get; set; }
    public string? ServicioReferencia { get; set; }
    public string? ServicioTelefono { get; set; }
    public int SoloAceites { get; set; }
    public int SoloServicios { get; set; }
    public int SoloLogistica { get; set; }
    public int Facturar { get; set; }
    public int Simular { get; set; }
    public int IDDescuentoSimular { get; set; }
    public int PedidoSinCascosCambio { get; set; }
    public int Tipo { get; set; }
    public int IDEmpresaCS { get; set; }
    public int FacturaMayorista { get; set; }
    public int RemisionMayorista { get; set; }
    public decimal DescuentoAdicional { get; set; }
}

public class PedidoEliminarDetalleRequest
{
    public int IDPedido { get; set; }
    public int IDPedidoDetalle { get; set; }
    public string? Observaciones { get; set; }
    public int SinModificarObservaciones { get; set; }
}

public class PedidoGuardarRequest
{
    public int IDPedido { get; set; }
    public int IDDomicilio { get; set; }
    public int Productos { get; set; }
    public decimal TotalPagar { get; set; }
    public string? Observaciones { get; set; }
    public int IDAgenteAutoriza { get; set; }
    public string? ServicioNombre { get; set; }
    public string? ServicioDireccion1 { get; set; }
    public string? ServicioDireccion2 { get; set; }
    public string? ServicioReferencia { get; set; }
    public string? ServicioTelefono { get; set; }
    public int SoloLogistica { get; set; }
    public int PedidoSinCascosCambio { get; set; }
}
