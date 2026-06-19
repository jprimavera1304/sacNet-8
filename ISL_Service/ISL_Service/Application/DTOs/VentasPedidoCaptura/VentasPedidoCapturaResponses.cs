namespace ISL_Service.Application.DTOs.VentasPedidoCaptura;

public class PedidoCatalogoItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Clave { get; set; } = "";
}

public class PedidoFechaOperacionDto
{
    public string FechaOperacion { get; set; } = "";
    public string FechaOperacionFtm { get; set; } = "";
    public string FechaOperacionTexto { get; set; } = "";
}

public class PedidoSnapshotDto
{
    public int IDPedido { get; set; }
    public List<Dictionary<string, object?>> Detalles { get; set; } = new();
    public List<Dictionary<string, object?>> CascosCargo { get; set; } = new();
    public List<Dictionary<string, object?>> CascosCredito { get; set; } = new();
    public Dictionary<string, object?> Totales { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class PedidoBootstrapResponse
{
    public List<PedidoCatalogoItemDto> Empresas { get; set; } = new();
    public List<PedidoCatalogoItemDto> Almacenes { get; set; } = new();
    public List<PedidoCatalogoItemDto> Agentes { get; set; } = new();
    public PedidoFechaOperacionDto FechaOperacion { get; set; } = new();
    public PedidoSnapshotDto Pedido { get; set; } = new();
}

public class PedidoClienteContextResponse
{
    public List<Dictionary<string, object?>> Clientes { get; set; } = new();
    public List<Dictionary<string, object?>> Domicilios { get; set; } = new();
    public List<Dictionary<string, object?>> Saldos { get; set; } = new();
}

public class PedidoRowsResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total => Rows.Count;
}
