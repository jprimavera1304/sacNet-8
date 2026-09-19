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

/*
  ABRIR EL REPORTE DE UNA REMISION

  El reporte lo genera ESTE backend: lee las mismas plantillas de Template_Html
  y llama los mismos sp_n_ que usa Mac31, y arma el PDF. Antes se delegaba en
  MacReportes —el servidor de reportes viejo— y eso obligaba a tener esa
  aplicacion viva y alcanzable desde el navegador del usuario.

  Lo que se conserva de aquel flujo es lo que de verdad importaba: las
  plantillas y los procedimientos. Por eso el papel sigue saliendo igual al de
  Mac31 y se sigue actualizando solo si alguien ajusta una plantilla.

  El POST no devuelve el PDF: devuelve la direccion donde esta. La pestana ya
  se abrio en el navegador antes de pedir (si no, la bloquea), asi que lo unico
  que falta es a donde mandarla.
*/
public class VentasReporteRequest
{
    /// Las ventas a incluir. Mac31 manda varias cuando hay varias marcadas, y
    /// salen todas en el mismo PDF, una por hoja.
    public List<int> IdsVenta { get; set; } = new();

    /// 0 = ver en pantalla, 1 = descargar el archivo.
    public int Descargar { get; set; }
}

public class VentasReporteResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    /// La direccion completa, lista para abrirse en otra pestana.
    public string Url { get; set; } = string.Empty;
}
