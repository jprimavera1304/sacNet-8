using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private static async Task<LegacyReportConfig> ConsultarConfiguracionAsync(
        SqlConnection conn,
        int idReporte,
        CancellationToken ct)
    {
        var ds = await ExecuteLegacySpAsync(conn, "sp_ConsultaReportes", idReporte.ToString(), ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("Reporte no encontrado en sp_ConsultaReportes.");

        var row = ds.Tables[0].Rows[0];
        var nombre = Convert.ToString(row["NombreReporte"]) ?? "";
        var sp = Convert.ToString(row["SPV2"]) ?? "";
        if (string.IsNullOrWhiteSpace(sp))
            throw new InvalidOperationException("El reporte no tiene SPV2 configurado.");

        return new LegacyReportConfig
        {
            NombreReporte = nombre,
            StoredProcedure = sp,
            Aspx = Convert.ToString(row["aspx"]) ?? "",
            IDReporteMaster = ReadInt(row, "IDReporteMaster")
        };
    }

    private static async Task<LegacyConstants> ConsultarConstantesAsync(
        SqlConnection conn,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaConstantes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@ValidaActividad", 0);
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@Equipo", "");

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            return new LegacyConstants();

        var row = ds.Tables[0].Rows[0];
        var pathImagenes = Convert.ToString(row["PathImagenes"]) ?? "";
        return new LegacyConstants
        {
            FechaOperacion = DateTime.TryParse(ReadString(row, "FechaOperacion"), out var fechaOperacion)
                ? fechaOperacion.ToString("yyyy-MM-dd")
                : DateTime.Today.ToString("yyyy-MM-dd"),
            Logo = pathImagenes + (Convert.ToString(row["LogoMacReportes"]) ?? ""),
            LogoWatermark = pathImagenes + (Convert.ToString(row["LogoMacWM"]) ?? "")
        };
    }

    private static string ResolveLogoForHtml(string logo)
    {
        var trimmed = (logo ?? "").Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            var localLogo = TryImageFileToDataUri(trimmed);
            if (!string.IsNullOrWhiteSpace(localLogo))
                return localLogo;
        }

        return FallbackLogoDataUri.Value;
    }

    private static string? TryImageFileToDataUri(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            return $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }
        catch
        {
            return null;
        }
    }

    private static async Task<DataTable> ConsultarTemplatesAsync(
        SqlConnection conn,
        int idTipoTemplateHtml,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaTemplateHtml", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDTemplateHtml", 0);
        cmd.Parameters.AddWithValue("@IDTipoTemplateHtml", idTipoTemplateHtml);
        cmd.Parameters.AddWithValue("@IDAplicacion", 1);
        cmd.Parameters.AddWithValue("@IDStatus", 1);

        var ds = await FillDataSetAsync(cmd, ct);
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new InvalidOperationException("No se encontraron templates HTML para el reporte.");

        return ds.Tables[0];
    }

    private static LegacyTemplate BuildTemplateInfo(DataTable templates)
    {
        var row = templates.Rows[0];
        return new LegacyTemplate
        {
            Orientacion = Convert.ToString(row["orientacion"]) ?? "Landscape",
            Ancho = ReadInt(row, "ancho"),
            Alto = ReadInt(row, "alto"),
            MarginTop = ReadInt(row, "marginTop"),
            MarginBottom = ReadInt(row, "marginBottom"),
            MarginLeft = ReadInt(row, "marginLeft"),
            MarginRight = ReadInt(row, "marginRight"),
            MaxLineas = Math.Max(1, ReadInt(row, "maxLineas"))
        };
    }

    private static string GenerateFormatoNormalHtml(
        string logo,
        string logoWatermark,
        string reporteNombre,
        DataSet data,
        DataTable templates,
        LegacyTemplate template,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        if (table.Rows.Count == 0)
            return GenerateEmptyFormatoNormalHtml(logo, templates, reporteNombre, request);

        var cuerpoTemplate = ReadTemplateHtml(templates, "cuerpo_n");
        var detalleTemplate = ReadTemplateHtml(templates, "detalle");
        var totalesTemplate = ReadTemplateHtml(templates, "totales");
        var htmlFinal = "";

        var groupColumn = ResolveAgentGroupColumn(table, request);
        var groups = groupColumn is null
            ? new[] { "" }
            : table.AsEnumerable()
                .Select(row => Convert.ToString(row[groupColumn]) ?? "")
                .Distinct()
                .ToArray();

        foreach (var group in groups)
        {
            var rows = groupColumn is null
                ? table.AsEnumerable().ToArray()
                : table.AsEnumerable()
                    .Where(row => string.Equals(Convert.ToString(row[groupColumn]) ?? "", group, StringComparison.Ordinal))
                    .ToArray();
            if (rows.Length == 0) continue;

            var totalPaginas = rows.Length / template.MaxLineas;
            if (rows.Length % template.MaxLineas != 0) totalPaginas++;

            var numPagina = 1;
            var lineas = 0;
            var numRegistro = 0;
            var htmlCuerpo = "";
            var htmlDetalle = "";
            var registroTotales = false;

            foreach (var row in rows)
            {
                numRegistro++;

                if (lineas == 0)
                {
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += BuildFormatoNormalHeader(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, row, numRegistro, request);
                }

                htmlDetalle += ReplaceValues(table, row, detalleTemplate);
                lineas++;

                if (lineas >= template.MaxLineas)
                {
                    if (numRegistro == rows.Length)
                    {
                        htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReplaceValues(table, rows[0], totalesTemplate, true));
                        registroTotales = true;
                    }

                    lineas = 0;
                    numPagina++;
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
                    htmlCuerpo = htmlCuerpo.Replace("#Totales#", "");
                    htmlFinal += htmlCuerpo;
                    htmlDetalle = "";
                }
            }

            if (!registroTotales)
            {
                htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
                htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReplaceValues(table, rows[0], totalesTemplate, true));
                htmlFinal += htmlCuerpo;
            }

            htmlFinal += "<p style='page-break-before: always'></p>";
            htmlFinal = htmlFinal.Replace("#LogoWM#", logoWatermark);
        }

        return htmlFinal;
    }

    private static string GenerateFormatoColumnas3Html(
        string logo,
        string reporteNombre,
        DataSet data,
        DataTable templates,
        LegacyTemplate template,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        if (table.Rows.Count == 0)
            return GenerateEmptyColumnas3Html(logo, templates, reporteNombre, request);

        var cuerpoTemplate = ReadTemplateHtml(templates, "cuerpo_n");
        var detalle1Template = ReadTemplateHtml(templates, "detalle1");
        var detalle2Template = ReadTemplateHtml(templates, "detalle2");
        var detalle3Template = ReadTemplateHtml(templates, "detalle3");
        var totalesTemplate = ReadTemplateHtml(templates, "totales");
        var htmlFinal = "";

        var groupColumn = ResolveAgentGroupColumn(table, request);
        var groups = groupColumn is null
            ? new[] { "" }
            : table.AsEnumerable()
                .Select(row => Convert.ToString(row[groupColumn]) ?? "")
                .Distinct()
                .ToArray();

        foreach (var group in groups)
        {
            var rows = groupColumn is null
                ? table.AsEnumerable().ToArray()
                : table.AsEnumerable()
                    .Where(row => string.Equals(Convert.ToString(row[groupColumn]) ?? "", group, StringComparison.Ordinal))
                    .ToArray();
            if (rows.Length == 0) continue;

            var totalPaginas = rows.Length / template.MaxLineas;
            if (rows.Length % template.MaxLineas != 0) totalPaginas++;

            var numPagina = 1;
            var lineas = 0;
            var numRegistro = 0;
            var htmlCuerpo = "";
            var htmlDetalle1 = "";
            var htmlDetalle2 = "";
            var htmlDetalle3 = "";
            var registroTotales = false;

            foreach (var row in rows)
            {
                numRegistro++;

                if (lineas == 0)
                {
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += BuildColumnas3Header(cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas, table, row, request);
                }

                htmlDetalle1 += ReplaceValues(table, row, detalle1Template);
                htmlDetalle2 += ReplaceValues(table, row, detalle2Template);
                htmlDetalle3 += ReplaceValues(table, row, detalle3Template);
                lineas++;

                if (lineas >= template.MaxLineas)
                {
                    if (numRegistro == rows.Length)
                    {
                        htmlCuerpo += ReplaceValues(table, rows[0], totalesTemplate, true);
                        registroTotales = true;
                    }

                    lineas = 0;
                    numPagina++;
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle1#", htmlDetalle1);
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle2#", htmlDetalle2);
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle3#", htmlDetalle3);
                    htmlFinal += htmlCuerpo;
                    htmlDetalle1 = "";
                    htmlDetalle2 = "";
                    htmlDetalle3 = "";
                }
            }

            if (!registroTotales)
            {
                htmlCuerpo += ReplaceValues(table, rows[0], totalesTemplate, true);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle1#", htmlDetalle1);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle2#", htmlDetalle2);
                htmlCuerpo = htmlCuerpo.Replace("#Detalle3#", htmlDetalle3);
                htmlFinal += htmlCuerpo;
            }

            htmlFinal += "<p style='page-break-before: always'></p>";
        }

        return htmlFinal;
    }

    private static string GenerateEmptyColumnas3Html(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = BuildColumnas3Header(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, 1, 1, null, null, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro informacion</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", mensaje);
        html = html.Replace("#Detalle2#", "");
        html = html.Replace("#Detalle3#", "");
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string BuildColumnas3Header(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable? table,
        DataRow? row,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", numPagina.ToString());
        html = html.Replace("#TotalPaginas#", totalPaginas.ToString());

        if (table is null || row is null)
        {
            html = html.Replace("#almacen#", "----");
            html = html.Replace("#categorias#", "----");
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            html = html.Replace("#fechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
            html = html.Replace("#fechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
            html = html.Replace("#ParametrosTexto#", "");
            html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            return html;
        }

        html = ReplaceAgentHeader(html, table, row, request);
        html = ReplaceClientHeader(html, table, row);
        return ReplaceValues(table, row, html);
    }

    private static string GenerateEmptyFormatoNormalHtml(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = BuildEmptyHeader(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, request);
        var mensaje = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>No se encontro informacion</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", mensaje);
        html = html.Replace("#Detalle#", mensaje);
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string GenerateSinInformacionHtml(
        string logo,
        DataTable templates,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request,
        string mensaje)
    {
        var html = BuildEmptyHeader(ReadTemplateHtml(templates, "cuerpo_n"), logo, reporteNombre, request);
        var detail = "<tr><td colspan='16' align='center'><br /><br /> <u> <h1>" + mensaje + "</h1> </u></td></tr>";
        html = html.Replace("#Detalle1#", detail);
        html = html.Replace("#Detalle#", detail);
        html = html.Replace("#Detalle2#", "");
        html = html.Replace("#Detalle3#", "");
        html = html.Replace("#Totales#", "");
        return ReplaceRemainingTags(html);
    }

    private static string BuildEmptyHeader(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", "1");
        html = html.Replace("#TotalPaginas#", "1");
        html = html.Replace("#Agente#", "");
        html = html.Replace("#agente#", "");
        html = html.Replace("#Cliente#", "");
        html = html.Replace("#cliente#", "");
        html = html.Replace("#Totales#", "");
        html = html.Replace("#FormatoTexto#", "");
        html = html.Replace("#ParametrosTexto#", "");
        html = html.Replace("#fechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
        html = html.Replace("#fechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
        html = html.Replace("#FechaInicial#", request.FechaInicial.ToString("dd/MM/yyyy"));
        html = html.Replace("#FechaFinal#", request.FechaFinal.ToString("dd/MM/yyyy"));
        html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        return html;
    }

    private static string BuildFormatoNormalHeader(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable? table,
        DataRow? row,
        int numRegistro,
        ReportesVentasAcumuladoresProductosRequest request)
    {
        var html = cuerpoTemplate;
        html = html.Replace("#Logo#", logo);
        html = html.Replace("#Titulo#", reporteNombre);
        html = html.Replace("#NumPagina#", numPagina.ToString());
        html = html.Replace("#TotalPaginas#", totalPaginas.ToString());

        if (table is null || row is null)
        {
            html = html.Replace("#Agente#", "");
            html = html.Replace("#agente#", "");
            html = html.Replace("#Cliente#", "");
            html = html.Replace("#cliente#", "");
            html = html.Replace("#Totales#", "");
            html = html.Replace("#FormatoTexto#", "");
            html = html.Replace("#ParametrosTexto#", "");
            html = html.Replace("#fechaImpresion#", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            return ReplaceRemainingTags(html);
        }

        if (table.Columns.Contains("topWM") && numRegistro > 0)
            html = html.Replace("#topWM#", Convert.ToString(row["topWM"]) ?? "");

        html = ReplaceAgentHeader(html, table, row, request);
        html = ReplaceClientHeader(html, table, row);
        return ReplaceValues(table, row, html);
    }

    private static string ReadTemplateHtml(DataTable templates, string descripcion)
    {
        var rows = templates.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
        if (rows.Length == 0)
            throw new InvalidOperationException($"Template HTML '{descripcion}' no encontrado.");
        return Convert.ToString(rows[0]["html"]) ?? "";
    }

    private static string ReplaceValues(DataTable table, DataRow row, string html, bool esTotales = false)
    {
        foreach (DataColumn column in table.Columns)
        {
            var tag = "#" + column.ColumnName + "#";
            var value = Convert.ToString(row[column.ColumnName]) ?? "";
            if (value != "")
                value = FormatLegacyNumber(column.DataType, value, esTotales);

            html = html.Replace(tag, value);
            html = html.Replace(tag.ToLowerInvariant(), value);
        }

        return html;
    }

    private static string ReplaceRemainingTags(string html)
    {
        html = html.Replace("#ccc", "@@@@");
        while (true)
        {
            var start = html.IndexOf('#');
            if (start == -1) break;
            var end = html.IndexOf('#', start + 1);
            if (end == -1) break;
            var tag = html.Substring(start, end - start + 1);
            html = html.Replace(tag, "----");
        }
        return html.Replace("@@@@", "#ccc");
    }

    private static string FormatLegacyNumber(Type dataType, string value, bool esTotales)
    {
        if (dataType == typeof(int) && value == "") return "0";
        if (dataType == typeof(decimal) && value == "") return "0.00";
        if (value == "-99999") return "0";
        if (value == "-99999.00") return "0.00";
        if (value == "0") return esTotales ? value : "";
        if (value is "0.00" or "0.0000") return esTotales ? value : "";

        if (dataType == typeof(int) && int.TryParse(value, out var intValue))
            return intValue.ToString("###,##0");
        if ((dataType == typeof(float) || dataType == typeof(double)) && double.TryParse(value, out var doubleValue))
            return doubleValue.ToString("###,##0.00");
        if (dataType == typeof(decimal) && decimal.TryParse(value, out var decimalValue))
            return Math.Truncate(decimalValue * 100m / 1m) / 100m == 0
                ? decimalValue.ToString("###,##0.00")
                : (Math.Truncate(decimalValue * 100m) / 100m).ToString("###,##0.00");

        return value;
    }
}
