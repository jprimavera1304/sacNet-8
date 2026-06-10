using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private sealed record LegacyExcelColumn(string Name, Type Type);

    private sealed class LegacyReportParams
    {
        public string NombreEquipo { get; set; } = "WEB";
        public int IDUsuario { get; set; }
        public string Param1 { get; set; } = "";
        public string Param2 { get; set; } = "";
        public string Param3 { get; set; } = "";
        public string Param4 { get; set; } = "";
        public string Param5 { get; set; } = "";
        public string Param6 { get; set; } = "";
        public string Param7 { get; set; } = "";
        public string Param8 { get; set; } = "";
        public string Param9 { get; set; } = "";
        public string Param10 { get; set; } = "";
        public string Param11 { get; set; } = "";
        public string Param12 { get; set; } = "";
        public string Param13 { get; set; } = "";
        public string Param14 { get; set; } = "";
    }

    private sealed class LegacyStoredParams
    {
        public int IDReporte { get; set; }
        public ReportesVentasAcumuladoresProductosRequest Request { get; set; } = new();
    }

    private sealed class LegacyReportConfig
    {
        public string NombreReporte { get; set; } = "";
        public string StoredProcedure { get; set; } = "";
        public string Aspx { get; set; } = "";
        public int IDReporteMaster { get; set; }
    }

    private sealed class LegacyConstants
    {
        public string FechaOperacion { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public string Logo { get; set; } = "";
        public string LogoWatermark { get; set; } = "";
        public int IDDescuentoCompra { get; set; }
    }

    private sealed class LegacyTemplate
    {
        public string Orientacion { get; set; } = "Landscape";
        public int Ancho { get; set; }
        public int Alto { get; set; }
        public int MarginTop { get; set; }
        public int MarginBottom { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public int MaxLineas { get; set; } = 40;
    }
}
