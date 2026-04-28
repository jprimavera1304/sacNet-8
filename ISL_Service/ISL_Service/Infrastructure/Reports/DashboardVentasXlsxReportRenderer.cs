using System.Globalization;
using ClosedXML.Excel;
using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Reports;

public class DashboardVentasXlsxReportRenderer : IDashboardVentasReportRenderer
{
    private static readonly CultureInfo EsMx = CultureInfo.GetCultureInfo("es-MX");
    public string Formato => "xlsx";

    public Task<DashboardVentasReporteFile> RenderAsync(DashboardVentasReporteData data, DashboardVentasReporteRequest request, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        foreach (var year in data.Years)
            BuildYearSheet(workbook, year);
        BuildResumenSheet(workbook, data);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var yearsLabel = string.Join("-", data.Years.Select(x => x.Year));
        return Task.FromResult(new DashboardVentasReporteFile
        {
            Content = ms.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"dashboard-ventas-{yearsLabel}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        });
    }

    private static void BuildResumenSheet(XLWorkbook workbook, DashboardVentasReporteData data)
    {
        var ws = workbook.Worksheets.Add("Resumen");
        ws.Cell("A1").Value = "REPORTE DASHBOARD DE VENTAS";
        ws.Range("A1:H1").Merge().Style
            .Font.SetBold()
            .Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#0B3D91"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        ws.Cell("A3").Value = "Generado";
        ws.Cell("B3").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsMx);
        ws.Cell("A4").Value = "Tipo";
        ws.Cell("B4").Value = data.Tipo == "comparados" ? "Comparación de años" : "Año";
        ws.Cell("A5").Value = "años";
        ws.Cell("B5").Value = string.Join(", ", data.Years.Select(x => x.Year));

        ws.Cell("A7").Value = "Indicadores acumulados";
        ws.Range("A7:H7").Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));

        ws.Cell("A8").Value = "Venta total";
        ws.Cell("B8").Value = data.KpisAcumulados.VentaTotal;
        ws.Cell("C8").Value = "Piezas";
        ws.Cell("D8").Value = data.KpisAcumulados.UnidadesVendidas;
        ws.Cell("E8").Value = "Margen %";
        ws.Cell("F8").Value = data.KpisAcumulados.MargenPorcentaje / 100m;

        ws.Range("B8:B8").Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range("D8:D8").Style.NumberFormat.Format = "#,##0";
        ws.Range("F8:F8").Style.NumberFormat.Format = "0.00%";

        ws.Cell("A10").Value = "KPIs por año";
        ws.Range("A10:H10").Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));

        var row = 11;
        ws.Cell(row, 1).Value = "Año";
        ws.Cell(row, 2).Value = "Venta";
        ws.Cell(row, 3).Value = "Piezas";
        ws.Cell(row, 4).Value = "Margen %";
        ws.Range(row, 1, row, 4).Style.Font.SetBold();
        ws.Range(row, 1, row, 4).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        row++;
        foreach (var year in data.Years)
        {
            ws.Cell(row, 1).Value = year.Year;
            ws.Cell(row, 2).Value = year.Kpis.VentaTotal;
            ws.Cell(row, 3).Value = year.Kpis.UnidadesVendidas;
            ws.Cell(row, 4).Value = year.Kpis.MargenPorcentaje / 100m;
            row++;
        }

        ws.Range(12, 2, row - 1, 2).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range(12, 3, row - 1, 3).Style.NumberFormat.Format = "#,##0";
        ws.Range(12, 4, row - 1, 4).Style.NumberFormat.Format = "0.00%";

        if (data.Comparativo is not null)
        {
            row += 1;
            ws.Cell(row, 1).Value = "Comparativo";
            ws.Range(row, 1, row, 8).Merge().Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));

            row++;
            ws.Cell(row, 1).Value = $"{data.Comparativo.FromYear} vs {data.Comparativo.ToYear}";
            ws.Cell(row, 2).Value = data.Comparativo.DeltaVenta;
            ws.Cell(row, 3).Value = data.Comparativo.DeltaVentaPorcentaje / 100m;
            ws.Cell(row, 4).Value = data.Comparativo.DeltaUnidades;
            ws.Cell(row, 5).Value = data.Comparativo.DeltaUnidadesPorcentaje / 100m;
            ws.Range(row, 2, row, 2).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row, 3, row, 3).Style.NumberFormat.Format = "0.00%";
            ws.Range(row, 4, row, 4).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 5, row, 5).Style.NumberFormat.Format = "0.00%";
        }

        ws.Columns(1, 8).AdjustToContents();
    }

    private static void BuildYearSheet(XLWorkbook workbook, DashboardVentasReporteYearData year)
    {
        var ws = workbook.Worksheets.Add($"Año {year.Year}");
        ws.Cell("A1").Value = $"Dashboard Ventas {year.Year}";
        ws.Range("A1:H1").Merge().Style
            .Font.SetBold()
            .Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#1A2AA5"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell("A3").Value = "Venta";
        ws.Cell("B3").Value = year.Kpis.VentaTotal;
        ws.Cell("C3").Value = "Piezas";
        ws.Cell("D3").Value = year.Kpis.UnidadesVendidas;
        ws.Range("B3:B3").Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range("D3:D3").Style.NumberFormat.Format = "#,##0";

        ws.Cell("A5").Value = "Evolucion mensual";
        ws.Range("A5:H5").Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
        ws.Cell("A6").Value = "Mes";
        ws.Cell("B6").Value = "Venta";
        ws.Range("A6:B6").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        var row = 7;
        foreach (var mes in year.SerieMensual.OrderBy(x => x.Mes))
        {
            ws.Cell(row, 1).Value = GetMonthLabel(mes);
            ws.Cell(row, 2).Value = mes.VentaTotal;
            row++;
        }
        ws.Range(7, 2, Math.Max(7, row - 1), 2).Style.NumberFormat.Format = "$ #,##0.00";
        var monthlyStartRow = 7;
        var monthlyEndRow = Math.Max(monthlyStartRow, row - 1);
        if (monthlyEndRow >= monthlyStartRow)
        {
            // Grafica separada (estilo front) para no pintar la columna numerica de venta.
            ws.Cell("F5").Value = "Grafica venta mensual";
            ws.Range("F5:H5").Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            ws.Cell("F6").Value = "Mes";
            ws.Cell("G6").Value = "Barra";
            ws.Cell("H6").Value = "Venta";
            ws.Range("F6:H6").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

            for (var r = monthlyStartRow; r <= monthlyEndRow; r++)
            {
                ws.Cell(r, 6).Value = ws.Cell(r, 1).GetString();
                ws.Cell(r, 7).Value = ws.Cell(r, 2).GetValue<decimal>();
                ws.Cell(r, 8).Value = ws.Cell(r, 2).GetValue<decimal>();
            }

            var graphBars = ws.Range(monthlyStartRow, 7, monthlyEndRow, 7);
            graphBars.Style.NumberFormat.Format = ";;;";
            graphBars.AddConditionalFormat().DataBar(XLColor.FromHtml("#1A2AA5"));
            ws.Range(monthlyStartRow, 8, monthlyEndRow, 8).Style.NumberFormat.Format = "$ #,##0.00";
        }
        var monthlyComparisons = BuildMonthlyComparisons(year.SerieMensual);
        if (monthlyComparisons.Any())
        {
            row += 1;
            ws.Cell(row, 1).Value = "Comparacion mensual (mes vs mes anterior)";
            ws.Range(row, 1, row, 8).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            row++;
            ws.Cell(row, 1).Value = "Mes actual";
            ws.Cell(row, 2).Value = "Venta actual";
            ws.Cell(row, 3).Value = "Mes anterior";
            ws.Cell(row, 4).Value = "Venta anterior";
            ws.Cell(row, 5).Value = "Cambio";
            ws.Cell(row, 6).Value = "%";
            ws.Range(row, 1, row, 6).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));
            row++;

            foreach (var cmp in monthlyComparisons)
            {
                ws.Cell(row, 1).Value = cmp.CurrentLabel;
                ws.Cell(row, 2).Value = cmp.CurrentVenta;
                ws.Cell(row, 3).Value = cmp.PreviousLabel;
                ws.Cell(row, 4).Value = cmp.PreviousVenta;
                ws.Cell(row, 5).Value = cmp.DeltaVenta;
                ws.Cell(row, 6).Value = cmp.DeltaPorcentaje / 100m;
                row++;
            }
            ws.Range(row - monthlyComparisons.Count, 2, row - 1, 2).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row - monthlyComparisons.Count, 4, row - 1, 5).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row - monthlyComparisons.Count, 6, row - 1, 6).Style.NumberFormat.Format = "0.00%";
        }

        row += 1;
        ws.Cell(row, 1).Value = "Top productos";
        ws.Range(row, 1, row, 8).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
        row++;
        ws.Cell(row, 1).Value = "Producto";
        ws.Cell(row, 2).Value = "Cantidad";
        ws.Cell(row, 3).Value = "Venta";
        ws.Range(row, 1, row, 3).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        row++;
        foreach (var item in year.TopProductos)
        {
            ws.Cell(row, 1).Value = item.Producto ?? "-";
            ws.Cell(row, 2).Value = item.CantidadVendida;
            ws.Cell(row, 3).Value = item.VentaTotal;
            row++;
        }
        ws.Range(1, 1, Math.Max(1, row), 8).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Range(1, 1, Math.Max(1, row), 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, Math.Max(1, row), 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (year.Detalle.Any())
        {
            row += 1;
            ws.Cell(row, 1).Value = "Detalle (muestra)";
            ws.Range(row, 1, row, 10).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            row++;
            ws.Cell(row, 1).Value = "Fecha";
            ws.Cell(row, 2).Value = "Folio";
            ws.Cell(row, 3).Value = "Cliente";
            ws.Cell(row, 4).Value = "Producto";
            ws.Cell(row, 5).Value = "Cantidad";
            ws.Cell(row, 6).Value = "Importe";
            ws.Range(row, 1, row, 6).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));
            row++;

            foreach (var d in year.Detalle)
            {
                ws.Cell(row, 1).Value = d.FechaEmision;
                ws.Cell(row, 2).Value = d.Folio ?? "-";
                ws.Cell(row, 3).Value = d.Cliente ?? "-";
                ws.Cell(row, 4).Value = d.Producto ?? "-";
                ws.Cell(row, 5).Value = d.Cantidad;
                ws.Cell(row, 6).Value = d.ImporteDetalle;
                row++;
            }

            ws.Range(1, 1, Math.Max(1, row), 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, Math.Max(1, row), 10).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, Math.Max(1, row), 10).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Range(1, 1, Math.Max(1, row), 10).Style.Alignment.SetWrapText(false);
            ws.Range(1, 1, Math.Max(1, row), 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

            ws.Column(1).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Range(1, 5, Math.Max(1, row), 5).Style.NumberFormat.Format = "#,##0";
            ws.Range(1, 6, Math.Max(1, row), 6).Style.NumberFormat.Format = "$ #,##0.00";
        }

        var lastDataRow = Math.Max(1, row);
        var scanUntilRow = Math.Min(lastDataRow, 300);
        ws.Columns(1, 10).AdjustToContents(1, scanUntilRow);
    }

    private static string ToSpanishMonthName(int month, string? fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName.Trim();

        if (month >= 1 && month <= 12)
        {
            var name = EsMx.DateTimeFormat.GetMonthName(month);
            if (!string.IsNullOrWhiteSpace(name))
                return char.ToUpper(name[0], EsMx) + name[1..];
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? month.ToString() : fallbackName.Trim();
    }


    private static List<MonthlyComparisonRow> BuildMonthlyComparisons(IEnumerable<DashboardVentasSerieMensualDto> serie)
    {
        var months = serie.OrderBy(x => x.Mes).ToList();
        var rows = new List<MonthlyComparisonRow>();
        for (var i = 1; i < months.Count; i++)
        {
            var prev = months[i - 1];
            var curr = months[i];
            var delta = curr.VentaTotal - prev.VentaTotal;
            var pct = prev.VentaTotal == 0 ? 0m : (delta / prev.VentaTotal) * 100m;
            rows.Add(new MonthlyComparisonRow(GetMonthLabel(curr), curr.VentaTotal, GetMonthLabel(prev), prev.VentaTotal, delta, pct));
        }
        return rows;
    }

    private static string GetMonthLabel(DashboardVentasSerieMensualDto month)
        => ToSpanishMonthName(month.Mes, month.MesNombre);

    private sealed record MonthlyComparisonRow(
        string CurrentLabel,
        decimal CurrentVenta,
        string PreviousLabel,
        decimal PreviousVenta,
        decimal DeltaVenta,
        decimal DeltaPorcentaje
    );

}






