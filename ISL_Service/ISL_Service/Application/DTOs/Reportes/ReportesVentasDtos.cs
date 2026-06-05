namespace ISL_Service.Application.DTOs.Reportes;

public class ReportesVentasAcumuladoresProductosRequest
{
    public DateTime FechaInicial { get; set; }
    public DateTime FechaFinal { get; set; }
    public string Categoria { get; set; } = "acumuladores";
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

public class ReportesVentasColumnDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
}
