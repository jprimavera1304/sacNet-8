namespace ISL_Service.Application.DTOs.DashboardVentas;

public class DashboardVentasReporteRequest : DashboardVentasFiltroRequest
{
    public string? Formato { get; set; } = "pdf";
    public string? Modo { get; set; } = "current";
    public string? Tipo { get; set; } = "actual";
    public List<int>? Anios { get; set; }
    public bool IncluirDetalle { get; set; }
    public int? MaxDetalleFilas { get; set; }
}

public class DashboardVentasReporteData
{
    public DateTime GeneratedAtUtc { get; set; }
    public string Tipo { get; set; } = "actual";
    public DateTime FechaReferenciaInicial { get; set; }
    public DateTime FechaReferenciaFinal { get; set; }
    public DashboardVentasKpisDto KpisAcumulados { get; set; } = new();
    public List<DashboardVentasReporteYearData> Years { get; set; } = new();
    public DashboardVentasReporteComparativoDto? Comparativo { get; set; }
}

public class DashboardVentasReporteYearData
{
    public int Year { get; set; }
    public DashboardVentasKpisDto Kpis { get; set; } = new();
    public List<DashboardVentasSerieMensualDto> SerieMensual { get; set; } = new();
    public List<DashboardTopProductoDto> TopProductos { get; set; } = new();
    public List<DashboardVentasDetalleDto> Detalle { get; set; } = new();
    public bool DetalleTruncado { get; set; }
    public int DetalleTotalRegistros { get; set; }
}

public class DashboardVentasReporteComparativoDto
{
    public int FromYear { get; set; }
    public int ToYear { get; set; }
    public decimal DeltaVenta { get; set; }
    public decimal DeltaUnidades { get; set; }
    public decimal DeltaVentaPorcentaje { get; set; }
    public decimal DeltaUnidadesPorcentaje { get; set; }
}

public class DashboardVentasReporteFile
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "reporte";
}
