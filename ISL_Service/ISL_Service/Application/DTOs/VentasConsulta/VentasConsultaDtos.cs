namespace ISL_Service.Application.DTOs.VentasConsulta;

public class VentasConsultaCatalogoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
}

public class VentasConsultaCatalogosResponse
{
    public List<VentasConsultaCatalogoItem> Empresas { get; set; } = new();
    public List<VentasConsultaCatalogoItem> Almacenes { get; set; } = new();
    public List<VentasConsultaCatalogoItem> Agentes { get; set; } = new();
    public List<VentasConsultaCatalogoItem> TiposDocumento { get; set; } = new();
    public List<VentasConsultaCatalogoItem> EstatusVenta { get; set; } = new();
    public VentasConsultaFechasOperacion FechasOperacion { get; set; } = new();
}

public class VentasConsultaFechasOperacion
{
    public string FechaOperacion { get; set; } = string.Empty;
    public string FechaOperacionFtm { get; set; } = string.Empty;
    public string FechaOperacionPagos { get; set; } = string.Empty;
    public string FechaOperacionPagosFtm { get; set; } = string.Empty;
}

public class VentasConsultaRequest
{
    public List<int> IDsVentaLst { get; set; } = new();
    public List<int> IDsClientesLst { get; set; } = new();
    public List<int> IDsProductoLst { get; set; } = new();
    public List<int> FoliosLst { get; set; } = new();
    public List<int> NumerosClienteLst { get; set; } = new();
    public List<string> ClavesProductoLst { get; set; } = new();
    public string? IDsVenta { get; set; }
    public string? IDsClientes { get; set; }
    public string? IDsProducto { get; set; }
    public string? ClaveProducto { get; set; }
    public int IDVenta { get; set; }
    public int IDEmpresa { get; set; }
    public int IDCliente { get; set; }
    public int IDUsuario { get; set; }
    public int IDAgente { get; set; }
    public int IDTipoDocumento { get; set; }
    public int IDStatusPedido { get; set; }
    public int IDUsuarioActual { get; set; }
    public int IDProducto { get; set; }
    public int TipoDocumento { get; set; }
    public string? FolioInicial { get; set; }
    public string? FolioFinal { get; set; }
    public string? FechaEmisionInicial { get; set; }
    public string? FechaEmisionFinal { get; set; }
    public string? FechaCancelInicial { get; set; }
    public string? FechaCancelFinal { get; set; }
    public string? FechaPagoInicial { get; set; }
    public string? FechaPagoFinal { get; set; }
    public string? FechaInicial { get; set; }
    public string? FechaFinal { get; set; }
    public int Formato { get; set; }
    public bool DiferenciaUsados { get; set; }
    public int IDCobro { get; set; }
    public bool DineroExcedente { get; set; }
    // Cuando es true (lo usa la app movil "Mis pedidos") se filtra por el
    // usuario del token, el MISMO que se guarda al crear el pedido. Asi el
    // usuario solo ve los pedidos que el hizo. El web no lo manda -> ve todos.
    public bool SoloMisPedidos { get; set; }
}

public class VentasConsultaRowsResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total { get; set; }
}
