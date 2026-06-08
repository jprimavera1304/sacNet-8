using System.Data;
using System.Globalization;
using ClosedXML.Excel;
using ISL_Service.Application.DTOs.Reportes;

namespace ISL_Service.Infrastructure.Reports;

public static class LegacyPlainXlsxReportRenderer
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static ReportesVentasFileResponse Render(DataTable table, string nombreReporte)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(table.TableName) ? "Sheet1" : SanitizeSheetName(table.TableName));

        for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            worksheet.Cell(1, columnIndex + 1).Value = table.Columns[columnIndex].ColumnName;

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var value = row[columnIndex];
                if (value == DBNull.Value)
                    continue;

                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = XLCellValue.FromObject(value, CultureInfo.InvariantCulture);
            }
        }

        ApplyColumnSizing(worksheet, table.Columns.Count, table.Rows.Count + 1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        return new ReportesVentasFileResponse
        {
            Content = ms.ToArray(),
            ContentType = ContentType,
            FileName = $"{SanitizeFileName(nombreReporte)}_{DateTime.Now:ddMMyyyy_HHmmss}.xlsx"
        };
    }

    private static void ApplyColumnSizing(IXLWorksheet worksheet, int columnCount, int lastRow)
    {
        if (columnCount <= 0)
            return;

        var scanUntilRow = Math.Max(1, lastRow);
        worksheet.Columns(1, columnCount).AdjustToContents(1, scanUntilRow);

        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            var column = worksheet.Column(columnIndex);
            if (column.Width < 10)
                column.Width = 10;
            if (column.Width > 64)
                column.Width = 64;
        }
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = string.Join("", value.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
        if (string.IsNullOrWhiteSpace(clean))
            return "Sheet1";
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
