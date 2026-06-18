using System.Data;
using System.Globalization;
using ClosedXML.Excel;
using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Infrastructure.Reports;

public static class LegacyPlainXlsxReportRenderer
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly CultureInfo EsMx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly XLColor BrandBlue = XLColor.FromHtml("#08158A");
    private static readonly XLColor HeaderBlue = XLColor.FromHtml("#101C9A");
    private static readonly XLColor SoftBlue = XLColor.FromHtml("#EAF1FF");
    private static readonly XLColor SoftGray = XLColor.FromHtml("#F6F8FC");
    private static readonly XLColor BorderGray = XLColor.FromHtml("#D6DCE8");
    private static readonly XLColor TextMuted = XLColor.FromHtml("#475569");

    public static ReportesVentasFileResponse Render(
        DataTable table,
        string nombreReporte,
        ReportesVentasAcumuladoresProductosRequest? request = null)
    {
        using var workbook = new XLWorkbook();
        var worksheetName = string.IsNullOrWhiteSpace(table.TableName)
            ? SanitizeSheetName(nombreReporte)
            : SanitizeSheetName(table.TableName);
        var worksheet = workbook.Worksheets.Add(worksheetName);

        var columnCount = Math.Max(1, table.Columns.Count);
        var tableHeaderRow = BuildReportHeader(worksheet, columnCount, nombreReporte, request);
        WriteTable(worksheet, table, tableHeaderRow);
        ApplySheetLayout(worksheet, table, columnCount, tableHeaderRow);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        return new ReportesVentasFileResponse
        {
            Content = ms.ToArray(),
            ContentType = ContentType,
            FileName = $"{SanitizeFileName(nombreReporte)}_{DateTime.Now:ddMMyyyy_HHmmss}.xlsx"
        };
    }

    private static int BuildReportHeader(
        IXLWorksheet worksheet,
        int columnCount,
        string nombreReporte,
        ReportesVentasAcumuladoresProductosRequest? request)
    {
        worksheet.Cell(1, 1).Value = string.IsNullOrWhiteSpace(nombreReporte)
            ? "REPORTE"
            : nombreReporte.Trim().ToUpperInvariant();
        var titleRange = worksheet.Range(1, 1, 1, columnCount);
        titleRange.Merge();
        titleRange.Style
            .Font.SetBold()
            .Font.SetFontSize(15)
            .Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(BrandBlue)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        worksheet.Row(1).Height = 24;

        var filterRows = BuildFilterRows(request).ToList();
        var currentRow = 3;
        worksheet.Cell(currentRow, 1).Value = "Generado";
        worksheet.Cell(currentRow, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsMx);
        currentRow++;

        foreach (var (label, value) in filterRows)
        {
            worksheet.Cell(currentRow, 1).Value = label;
            worksheet.Cell(currentRow, 2).Value = value;
            currentRow++;
        }

        var lastMetaRow = currentRow - 1;
        if (lastMetaRow >= 3)
        {
            var metaRange = worksheet.Range(3, 1, lastMetaRow, Math.Max(2, Math.Min(columnCount, 6)));
            metaRange.Style
                .Fill.SetBackgroundColor(SoftGray)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin)
                .Border.SetOutsideBorderColor(BorderGray)
                .Border.SetInsideBorderColor(BorderGray)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            worksheet.Range(3, 1, lastMetaRow, 1).Style
                .Font.SetBold()
                .Font.SetFontColor(TextMuted);

            if (columnCount > 2)
            {
                for (var row = 3; row <= lastMetaRow; row++)
                    worksheet.Range(row, 2, row, Math.Min(columnCount, 6)).Merge();
            }
        }

        var tableHeaderRow = Math.Max(5, currentRow + 1);
        worksheet.Cell(tableHeaderRow - 1, 1).Value = "Datos";
        worksheet.Range(tableHeaderRow - 1, 1, tableHeaderRow - 1, columnCount).Merge().Style
            .Font.SetBold()
            .Font.SetFontColor(BrandBlue)
            .Fill.SetBackgroundColor(SoftBlue);

        return tableHeaderRow;
    }

    private static IEnumerable<(string Label, string Value)> BuildFilterRows(ReportesVentasAcumuladoresProductosRequest? request)
    {
        var rows = new List<(string Label, string Value)>();
        if (request is null)
            return rows;

        if (request.FechaInicial != default)
            rows.Add(("Fecha inicial", request.FechaInicial.ToString("dd/MM/yyyy", EsMx)));
        if (request.FechaFinal != default)
            rows.Add(("Fecha final", request.FechaFinal.ToString("dd/MM/yyyy", EsMx)));

        rows.Add(("Tipo reporte", NormalizeText(request.TipoReporte)));
        rows.Add(("Categoria", !string.IsNullOrWhiteSpace(request.Categoria)
            ? request.Categoria.Trim().ToUpperInvariant()
            : request.IDGrupoCategoria.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(request.Documento))
            rows.Add(("Documento", NormalizeText(request.Documento)));

        AddIds("Empresas", request.IDEmpresas);
        AddIds("Almacenes", request.IDAlmacenes);
        AddIds("Agentes", request.IDAgentes);
        AddIds("Clientes", request.IDClientes);
        AddIds("Subcategorias", request.IDSubcategorias);
        AddIds("Marcas", request.IDMarcas);

        if (request is ReportesVentasLegacyRequest legacy)
        {
            if (!string.IsNullOrWhiteSpace(legacy.ReporteKey))
                rows.Add(("Reporte", NormalizeText(legacy.ReporteKey)));
            if (!string.IsNullOrWhiteSpace(legacy.Formato))
                rows.Add(("Formato", NormalizeText(legacy.Formato)));
            if (legacy.IDProveedor > 0)
                rows.Add(("Proveedor", legacy.IDProveedor.ToString(CultureInfo.InvariantCulture)));
            if (legacy.IDTipoGasto > 0)
                rows.Add(("Tipo gasto", legacy.IDTipoGasto.ToString(CultureInfo.InvariantCulture)));
            AddIds("Usuarios", legacy.IDUsuarios);
            AddIds("Repartidores", legacy.IDRepartidores);
            AddIds("Autos", legacy.IDAutos);
            AddIds("Estatus garantias", legacy.IDStatusGarantias);

            var opciones = BuildLegacyOptions(legacy);
            if (!string.IsNullOrWhiteSpace(opciones))
                rows.Add(("Opciones", opciones));
        }

        return rows;

        void AddIds(string label, IReadOnlyCollection<int>? values)
        {
            if (values is null || values.Count == 0)
                return;

            rows.Add((label, values.Count > 10
                ? $"{values.Count} seleccionados"
                : string.Join(", ", values)));
        }
    }

    private static string BuildLegacyOptions(ReportesVentasLegacyRequest request)
    {
        var options = new List<string>();
        if (request.SoloServiciosDomicilio) options.Add("Solo servicios a domicilio");
        if (request.InventarioConCostos) options.Add("Incluir costos");
        if (request.InventarioHistorico) options.Add("Inventario historico");
        if (request.InventarioGenerarPedido) options.Add("Generar pedido");
        if (request.Redondear) options.Add("Redondear 5 a 10 pesos");
        if (!request.IncluirDescuentos) options.Add("Sin descuentos");
        if (request.PagosPorDia) options.Add("Pagos por dia");
        if (request.PagosExcedentes) options.Add("Solo pagos excedentes");
        if (request.FiltrarFechas) options.Add("Filtrar fechas");
        if (!string.IsNullOrWhiteSpace(request.Comentarios)) options.Add("Con comentarios");
        return string.Join(" | ", options);
    }

    private static void WriteTable(IXLWorksheet worksheet, DataTable table, int headerRow)
    {
        if (table.Columns.Count == 0)
        {
            worksheet.Cell(headerRow, 1).Value = "Sin columnas";
            worksheet.Cell(headerRow + 1, 1).Value = "El reporte no regreso columnas para mostrar.";
            return;
        }

        for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            worksheet.Cell(headerRow, columnIndex + 1).Value = table.Columns[columnIndex].ColumnName;

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var value = row[columnIndex];
                if (value == DBNull.Value)
                    continue;

                worksheet.Cell(rowIndex + headerRow + 1, columnIndex + 1).Value =
                    XLCellValue.FromObject(value, CultureInfo.InvariantCulture);
            }
        }

        if (table.Rows.Count == 0)
        {
            worksheet.Cell(headerRow + 1, 1).Value = "Sin informacion";
            worksheet.Range(headerRow + 1, 1, headerRow + 1, table.Columns.Count).Merge().Style
                .Font.SetItalic()
                .Font.SetFontColor(TextMuted)
                .Fill.SetBackgroundColor(SoftGray);
        }
    }

    private static void ApplySheetLayout(IXLWorksheet worksheet, DataTable table, int columnCount, int headerRow)
    {
        var lastRow = Math.Max(headerRow + Math.Max(table.Rows.Count, 1), headerRow);
        var usedRange = worksheet.Range(1, 1, lastRow, columnCount);
        usedRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        usedRange.Style.Font.SetFontName("Segoe UI");

        var header = worksheet.Range(headerRow, 1, headerRow, columnCount);
        header.Style
            .Font.SetBold()
            .Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(HeaderBlue)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);

        var dataRange = worksheet.Range(headerRow, 1, lastRow, columnCount);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = BorderGray;
        dataRange.Style.Border.InsideBorderColor = BorderGray;

        if (table.Rows.Count > 0)
        {
            for (var row = headerRow + 1; row <= lastRow; row++)
            {
                if ((row - headerRow) % 2 == 0)
                    worksheet.Range(row, 1, row, columnCount).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFF"));
            }

            worksheet.Range(headerRow, 1, lastRow, columnCount).AsTable("DatosReporte");
        }

        ApplyColumnFormats(worksheet, table, headerRow, lastRow);
        ApplyColumnSizing(worksheet, columnCount, lastRow);

        worksheet.SheetView.FreezeRows(headerRow);
    }

    private static void ApplyColumnFormats(IXLWorksheet worksheet, DataTable table, int headerRow, int lastRow)
    {
        if (table.Rows.Count == 0)
            return;

        for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
        {
            var column = table.Columns[columnIndex];
            var excelColumn = worksheet.Column(columnIndex + 1);
            var dataRange = worksheet.Range(headerRow + 1, columnIndex + 1, lastRow, columnIndex + 1);

            if (column.DataType == typeof(DateTime))
            {
                dataRange.Style.DateFormat.Format = "dd/MM/yyyy";
                continue;
            }

            if (column.DataType == typeof(decimal) || column.DataType == typeof(double) || column.DataType == typeof(float))
            {
                dataRange.Style.NumberFormat.Format = IsCurrencyColumn(column.ColumnName)
                    ? "$ #,##0.00"
                    : "#,##0.00";
                dataRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                continue;
            }

            if (column.DataType == typeof(short) || column.DataType == typeof(int) || column.DataType == typeof(long))
            {
                dataRange.Style.NumberFormat.Format = IsCurrencyColumn(column.ColumnName)
                    ? "$ #,##0"
                    : "#,##0";
                dataRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                continue;
            }

            excelColumn.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
        }
    }

    private static bool IsCurrencyColumn(string columnName)
    {
        var normalized = columnName.ToLowerInvariant();
        return normalized.Contains("importe", StringComparison.Ordinal)
            || normalized.Contains("total", StringComparison.Ordinal)
            || normalized.Contains("saldo", StringComparison.Ordinal)
            || normalized.Contains("precio", StringComparison.Ordinal)
            || normalized.Contains("costo", StringComparison.Ordinal)
            || normalized.Contains("venta", StringComparison.Ordinal)
            || normalized.Contains("pago", StringComparison.Ordinal)
            || normalized.Contains("abono", StringComparison.Ordinal);
    }

    private static void ApplyColumnSizing(IXLWorksheet worksheet, int columnCount, int lastRow)
    {
        if (columnCount <= 0)
            return;

        var scanUntilRow = Math.Min(Math.Max(1, lastRow), 300);
        worksheet.Columns(1, columnCount).AdjustToContents(1, scanUntilRow);

        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            var column = worksheet.Column(columnIndex);
            if (column.Width < 11)
                column.Width = 11;
            if (column.Width > 48)
                column.Width = 48;
        }

        worksheet.Rows(1, Math.Min(lastRow, 40)).AdjustToContents();
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim().Replace("_", " ", StringComparison.Ordinal).ToUpperInvariant();
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = string.Join("", (value ?? "").Select(ch => invalidChars.Contains(ch) ? '_' : ch));
        if (string.IsNullOrWhiteSpace(clean))
            return "Reporte";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private static string SanitizeFileName(string value)
    {
        var clean = new string((value ?? "")
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        while (clean.Contains("__", StringComparison.Ordinal))
            clean = clean.Replace("__", "_", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(clean) ? "Reporte" : clean.Trim('_');
    }
}
