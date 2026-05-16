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
        ApplyWorkbookHeader(ws, data);

        ws.Cell("A7").Value = "Indicadores acumulados";
        ws.Range("A7:H7").Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));

        ws.Cell("A8").Value = "Venta total";
        ws.Cell("B8").Value = data.KpisAcumulados.VentaTotal;
        ws.Cell("C8").Value = "Piezas";
        ws.Cell("D8").Value = data.KpisAcumulados.UnidadesVendidas;

        ws.Range("B8:B8").Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range("D8:D8").Style.NumberFormat.Format = "#,##0";

        ws.Cell("A10").Value = "KPIs por año";
        ws.Range("A10:H10").Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));

        var row = 11;
        ws.Cell(row, 1).Value = "Año";
        ws.Cell(row, 2).Value = "Venta";
        ws.Cell(row, 3).Value = "Piezas";
        ws.Range(row, 1, row, 3).Style.Font.SetBold();
        ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        row++;
        foreach (var year in data.Years)
        {
            ws.Cell(row, 1).Value = year.Year;
            ws.Cell(row, 2).Value = year.Kpis.VentaTotal;
            ws.Cell(row, 3).Value = year.Kpis.UnidadesVendidas;
            row++;
        }

        ws.Range(12, 2, row - 1, 2).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range(12, 3, row - 1, 3).Style.NumberFormat.Format = "#,##0";

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
            ApplySignColor(ws.Cell(row, 2), data.Comparativo.DeltaVenta);
            ApplySignColor(ws.Cell(row, 3), data.Comparativo.DeltaVentaPorcentaje);
            ApplySignColor(ws.Cell(row, 4), data.Comparativo.DeltaUnidades);
            ApplySignColor(ws.Cell(row, 5), data.Comparativo.DeltaUnidadesPorcentaje);
            row++;
            ws.Cell(row, 1).Value = "% Venta = ((Venta año actual - Venta año anterior) / Venta año anterior) x 100\n% Piezas = ((Piezas actual - Piezas anterior) / Piezas anterior) x 100";
            ws.Range(row, 1, row, 8).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#475569")).Font.SetItalic();
            ws.Range(row, 1, row, 8).Style.Alignment.WrapText = true;
            ws.Row(row).AdjustToContents();
        }

        ws.Columns(1, 8).AdjustToContents();
    }

    private static void BuildYearSheet(XLWorkbook workbook, DashboardVentasReporteYearData year)
    {
        var ws = workbook.Worksheets.Add($"Año {year.Year}");
        ApplyWorkbookHeader(ws, null, year.Year);

        ws.Cell("A7").Value = "Venta";
        ws.Cell("B7").Value = year.Kpis.VentaTotal;
        ws.Cell("C7").Value = "Piezas";
        ws.Cell("D7").Value = year.Kpis.UnidadesVendidas;
        ws.Range("B7:B7").Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range("D7:D7").Style.NumberFormat.Format = "#,##0";

        ws.Cell("A9").Value = "Evolucion mensual";
        ws.Range("A9:H9").Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
        ws.Cell("A10").Value = "Mes";
        ws.Cell("B10").Value = "Venta";
        ws.Cell("C10").Value = "Piezas";
        ws.Range("A10:C10").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        var row = 11;
        var monthlyStartRow = row;
        var monthlyRows = year.SerieMensual
            .Where(x => x.Mes >= 1 && x.Mes <= 12)
            .OrderBy(x => x.Mes)
            .ToList();
        foreach (var mes in monthlyRows)
        {
            ws.Cell(row, 1).Value = GetMonthLabel(mes);
            ws.Cell(row, 2).Value = mes.VentaTotal;
            ws.Cell(row, 3).Value = mes.Tickets;
            row++;
        }
        var monthlyDataEndRow = Math.Max(monthlyStartRow, row - 1);
        ws.Range(11, 2, monthlyDataEndRow, 2).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range(11, 3, monthlyDataEndRow, 3).Style.NumberFormat.Format = "#,##0";

        ws.Cell(row, 1).Value = "TOTAL: SUMA";
        ws.Cell(row, 2).Value = monthlyRows.Sum(x => x.VentaTotal);
        ws.Cell(row, 3).Value = monthlyRows.Sum(x => x.Tickets);
        ws.Range(row, 1, row, 3).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
        ws.Range(row, 2, row, 2).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Range(row, 3, row, 3).Style.NumberFormat.Format = "#,##0";
        row++;
        var monthlyEndRow = monthlyDataEndRow;
        if (monthlyEndRow >= monthlyStartRow)
        {
            var graphTitleRow = Math.Max(1, monthlyStartRow - 2);
            var graphHeaderRow = Math.Max(1, monthlyStartRow - 1);

            // Grafica separada (estilo front) para no pintar la columna numerica de venta.
            ws.Cell(graphTitleRow, 4).Value = "Grafica venta mensual";
            ws.Range(graphTitleRow, 4, graphTitleRow, 6).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            ws.Cell(graphHeaderRow, 4).Value = "Mes";
            ws.Cell(graphHeaderRow, 5).Value = "Barra";
            ws.Cell(graphHeaderRow, 6).Value = "Venta";
            ws.Range(graphHeaderRow, 4, graphHeaderRow, 6).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

            for (var r = monthlyStartRow; r <= monthlyEndRow; r++)
            {
                ws.Cell(r, 4).Value = ws.Cell(r, 1).GetString();
                ws.Cell(r, 5).Value = ws.Cell(r, 2).GetValue<decimal>();
                ws.Cell(r, 6).Value = ws.Cell(r, 2).GetValue<decimal>();
            }

            var graphBars = ws.Range(monthlyStartRow, 5, monthlyEndRow, 5);
            graphBars.Style.NumberFormat.Format = ";;;";
            graphBars.AddConditionalFormat().DataBar(XLColor.FromHtml("#1A2AA5"));
            ws.Range(monthlyStartRow, 6, monthlyEndRow, 6).Style.NumberFormat.Format = "$ #,##0.00";

        }
        var monthlyComparisons = BuildMonthlyComparisons(monthlyRows);
        if (monthlyComparisons.Any())
        {
            row += 1;
            ws.Cell(row, 1).Value = $"Comparacion mensual {year.Year} (mes vs mes anterior)";
            ws.Range(row, 1, row, 8).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            row++;
            ws.Cell(row, 1).Value = $"Mes actual {year.Year}";
            ws.Cell(row, 2).Value = $"Venta actual {year.Year}";
            ws.Cell(row, 3).Value = $"Mes anterior {year.Year}";
            ws.Cell(row, 4).Value = $"Venta anterior {year.Year}";
            ws.Cell(row, 5).Value = $"Cambio {year.Year}";
            ws.Cell(row, 6).Value = $"% {year.Year}";
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
                ApplySignColor(ws.Cell(row, 5), cmp.DeltaVenta);
                ApplySignColor(ws.Cell(row, 6), cmp.DeltaPorcentaje);
                row++;
            }
            ws.Range(row - monthlyComparisons.Count, 2, row - 1, 2).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row - monthlyComparisons.Count, 4, row - 1, 5).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row - monthlyComparisons.Count, 6, row - 1, 6).Style.NumberFormat.Format = "0.00%";

            var totalCurrent = monthlyComparisons.Sum(x => x.CurrentVenta);
            var totalPrevious = monthlyComparisons.Sum(x => x.PreviousVenta);
            var totalDelta = totalCurrent - totalPrevious;
            var totalPct = totalPrevious == 0 ? 0m : (totalDelta / totalPrevious);

            ws.Cell(row, 1).Value = "TOTAL SUMA";
            ws.Cell(row, 2).Value = totalCurrent;
            ws.Cell(row, 3).Value = "SUMA";
            ws.Cell(row, 4).Value = totalPrevious;
            ws.Cell(row, 5).Value = totalDelta;
            ws.Cell(row, 6).Value = totalPct;
            ws.Range(row, 1, row, 6).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
            ws.Range(row, 2, row, 2).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row, 4, row, 5).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Range(row, 6, row, 6).Style.NumberFormat.Format = "0.00%";
            ApplySignColor(ws.Cell(row, 5), totalDelta);
            ApplySignColor(ws.Cell(row, 6), totalPct * 100m);
            row++;
        }

        row += 1;
        ws.Cell(row, 1).Value = $"Top 10 productos (Año {year.Year})";
        ws.Range(row, 1, row, 8).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EAF1FF"));
        row++;
        ws.Cell(row, 1).Value = $"Producto {year.Year}";
        ws.Cell(row, 2).Value = $"Cantidad {year.Year}";
        ws.Cell(row, 3).Value = $"Venta {year.Year}";
        ws.Range(row, 1, row, 3).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE7FF"));

        row++;
        var topProductosStartRow = row;
        foreach (var item in year.TopProductos)
        {
            ws.Cell(row, 1).Value = item.Producto ?? "-";
            ws.Cell(row, 2).Value = item.CantidadVendida;
            ws.Cell(row, 3).Value = item.VentaTotal;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 3).Style.NumberFormat.Format = "$ #,##0.00";
            row++;
        }
        if (row > topProductosStartRow)
        {
            ws.Range(topProductosStartRow, 2, row - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws.Range(topProductosStartRow, 3, row - 1, 3).Style.NumberFormat.Format = "$ #,##0.00";
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

    private static void ApplyWorkbookHeader(IXLWorksheet ws, DashboardVentasReporteData? data = null, int? singleYear = null)
    {
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
        ws.Cell("B4").Value = data?.Tipo == "comparados" ? "Comparación de años" : "Año";
        ws.Cell("A5").Value = "Años";
        ws.Cell("B5").Value = data is not null
            ? string.Join(", ", data.Years.Select(x => x.Year))
            : (singleYear?.ToString() ?? "-");

        ws.Range("A3:A5").Style.Font.SetBold();
        ws.Range("A3:H5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFF"));
        ws.Range("A3:H5").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range("A3:H5").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
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

    private static void ApplySignColor(IXLCell cell, decimal value)
    {
        if (value > 0)
            cell.Style.Font.FontColor = XLColor.FromHtml("#B91C1C"); // positivo rojo
        else if (value < 0)
            cell.Style.Font.FontColor = XLColor.FromHtml("#166534"); // negativo verde
        else
            cell.Style.Font.FontColor = XLColor.Black;
    }

    private sealed record MonthlyComparisonRow(
        string CurrentLabel,
        decimal CurrentVenta,
        string PreviousLabel,
        decimal PreviousVenta,
        decimal DeltaVenta,
        decimal DeltaPorcentaje
    );

}






