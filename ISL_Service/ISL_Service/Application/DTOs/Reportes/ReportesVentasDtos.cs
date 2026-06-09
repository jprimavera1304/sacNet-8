namespace ISL_Service.Application.DTOs.Reportes;

public class ReportesVentasAcumuladoresProductosRequest
{
    public DateTime FechaInicial { get; set; }
    public DateTime FechaFinal { get; set; }
    public string Categoria { get; set; } = "acumuladores";
    public int IDGrupoCategoria { get; set; } = 1;
    public string TipoReporte { get; set; } = "empresa";
    public string Documento { get; set; } = "ventas";
    public bool SoloServiciosDomicilio { get; set; }
    public List<int> IDEmpresas { get; set; } = new();
    public List<int> IDAlmacenes { get; set; } = new();
    public List<int> IDSubcategorias { get; set; } = new();
    public List<int> IDMarcas { get; set; } = new();
    public List<int> IDAgentes { get; set; } = new();
    public List<int> IDClientes { get; set; } = new();
    public string Salida { get; set; } = "pantalla";
}

public class ReportesVentasRemisionesRequest : ReportesVentasAcumuladoresProductosRequest
{
    public string EstatusFolio { get; set; } = "todos";
    public List<int> IDUsuarios { get; set; } = new();
}

public class ReportesVentasPreviewResponse
{
    public int IDReporte { get; set; }
    public string NombreReporte { get; set; } = "";
    public string StoredProcedure { get; set; } = "";
    public string ParametrosLegacy { get; set; } = "";
    public string Html { get; set; } = "";
    public string Orientacion { get; set; } = "Landscape";
    public int Ancho { get; set; }
    public int Alto { get; set; }
    public int MarginTop { get; set; }
    public int MarginBottom { get; set; }
    public int MarginLeft { get; set; }
    public int MarginRight { get; set; }
    public List<ReportesVentasColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total { get; set; }
}

public class ReportesVentasGenerateResponse
{
    public int IDReporte { get; set; }
    public string NombreReporte { get; set; } = "";
    public string StoredProcedure { get; set; } = "";
    public string ParametrosLegacy { get; set; } = "";
    public string Url { get; set; } = "";
    public string Salida { get; set; } = "pantalla";
}

public class ReportesVentasFileResponse
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
}

public class ReportesVentasColumnDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
}

public class ReportesVentasCatalogosResponse
{
    public string FechaOperacion { get; set; } = "";
    public List<ReportesVentasCatalogoItem> Empresas { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Almacenes { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Agentes { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Categorias { get; set; } = new();
    public List<ReportesVentasProductoItem> Subcategorias { get; set; } = new();
    public List<ReportesVentasProductoItem> Marcas { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Documentos { get; set; } = new();
    public List<ReportesVentasCatalogoItem> StatusFolios { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Usuarios { get; set; } = new();
}

public class ReportesVentasCatalogoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Clave { get; set; }
}

public class ReportesVentasClienteItem
{
    public int IDCliente { get; set; }
    public int Numero { get; set; }
    public string NombreCliente { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public class ReportesVentasProductoItem
{
    public int IDGrupoCategoria { get; set; }
    public string GrupoCategoria { get; set; } = "";
    public int IDCategoria { get; set; }
    public string Categoria { get; set; } = "";
    public int IDMarca { get; set; }
    public string Marca { get; set; } = "";
}
