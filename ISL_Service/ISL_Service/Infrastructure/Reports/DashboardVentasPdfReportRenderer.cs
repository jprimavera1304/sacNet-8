using System.Globalization;
using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ISL_Service.Infrastructure.Reports;

public class DashboardVentasPdfReportRenderer : IDashboardVentasReportRenderer
{
    private static readonly CultureInfo EsMx = CultureInfo.GetCultureInfo("es-MX");
    public string Formato => "pdf";

    public Task<DashboardVentasReporteFile> RenderAsync(DashboardVentasReporteData data, DashboardVentasReporteRequest request, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("REPORTE DASHBOARD DE VENTAS").Bold().FontSize(16).FontColor("0B3D91");
                            c.Item().Text($"Generado: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsMx)}").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Tipo: {(data.Tipo == "comparados" ? "Comparacion de años" : "Año")}  |  Años: {string.Join(", ", data.Years.Select(y => y.Year))}")
                                .FontColor(Colors.Grey.Darken1);
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Element(Card).Column(c =>
                    {
                        c.Item().Text("Indicadores acumulados").Bold().FontColor("1A2AA5");
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Element(KpiBox).Column(k =>
                            {
                                k.Item().Text("Venta total").FontSize(9).FontColor(Colors.Grey.Darken1);
                                k.Item().Text(ToCurrency(data.KpisAcumulados.VentaTotal)).Bold().FontSize(12);
                            });
                            r.RelativeItem().Element(KpiBox).Column(k =>
                            {
                                k.Item().Text("Unidades").FontSize(9).FontColor(Colors.Grey.Darken1);
                                k.Item().Text(ToNumber(data.KpisAcumulados.UnidadesVendidas)).Bold().FontSize(12);
                            });
                            r.RelativeItem().Element(KpiBox).Column(k =>
                            {
                                k.Item().Text("Tickets").FontSize(9).FontColor(Colors.Grey.Darken1);
                                k.Item().Text(ToNumber(data.KpisAcumulados.Tickets)).Bold().FontSize(12);
                            });
                        });
                    });

                    if (data.Comparativo is not null)
                    {
                        col.Item().Element(Card).Column(c =>
                        {
                            c.Item().Text($"Comparativo {data.Comparativo.FromYear} vs {data.Comparativo.ToYear}").Bold().FontColor("1A2AA5");
                            c.Item().PaddingTop(4).Text($"Venta: {ToCurrency(data.Comparativo.DeltaVenta)} ({ToPercent(data.Comparativo.DeltaVentaPorcentaje)})");
                            c.Item().Text($"Unidades: {ToNumber(data.Comparativo.DeltaUnidades)} ({ToPercent(data.Comparativo.DeltaUnidadesPorcentaje)})");
                        });
                    }

                    foreach (var year in data.Years)
                    {
                        col.Item().Element(Card).Column(c =>
                        {
                            c.Spacing(6);
                            c.Item().Text($"Año {year.Year}").Bold().FontSize(13).FontColor("0B3D91");

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Element(KpiBox).Text($"Venta: {ToCurrency(year.Kpis.VentaTotal)}");
                                r.RelativeItem().Element(KpiBox).Text($"Unidades: {ToNumber(year.Kpis.UnidadesVendidas)}");
                                r.RelativeItem().Element(KpiBox).Text($"Tickets: {ToNumber(year.Kpis.Tickets)}");
                            });

                            c.Item().Text("Evolucion mensual").Bold().FontColor("1A2AA5");
                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(1);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(TableHeader).Text("Mes");
                                    h.Cell().Element(TableHeader).Text("Venta");
                                    h.Cell().Element(TableHeader).Text("Ganancia");
                                    h.Cell().Element(TableHeader).Text("Tickets");
                                });

                                foreach (var mes in year.SerieMensual.OrderBy(x => x.Mes))
                                {
                                    table.Cell().Element(TableCell).Text(ToSpanishMonthName(mes.Mes, mes.MesNombre));
                                    table.Cell().Element(TableCell).Text(ToCurrency(mes.VentaTotal));
                                    table.Cell().Element(TableCell).Text(ToCurrency(mes.GananciaTotal));
                                    table.Cell().Element(TableCell).Text(ToNumber(mes.Tickets));
                                }
                            });

                            var monthlyComparisons = BuildMonthlyComparisons(year.SerieMensual);
                            if (monthlyComparisons.Any())
                            {
                                c.Item().Text("Comparacion mensual (mes vs mes anterior)").Bold().FontColor("1A2AA5");
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1.5f);
                                        cols.RelativeColumn(1.8f);
                                        cols.RelativeColumn(1.5f);
                                        cols.RelativeColumn(1.8f);
                                        cols.RelativeColumn(1.8f);
                                        cols.RelativeColumn(1.2f);
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().Element(TableHeader).Text("Mes actual");
                                        h.Cell().Element(TableHeader).Text("Venta actual");
                                        h.Cell().Element(TableHeader).Text("Mes anterior");
                                        h.Cell().Element(TableHeader).Text("Venta anterior");
                                        h.Cell().Element(TableHeader).Text("Cambio");
                                        h.Cell().Element(TableHeader).Text("%");
                                    });

                                    foreach (var cmp in monthlyComparisons)
                                    {
                                        table.Cell().Element(TableCell).Text(ToSpanishMonthName(cmp.CurrentMonth, null));
                                        table.Cell().Element(TableCell).Text(ToCurrency(cmp.CurrentVenta));
                                        table.Cell().Element(TableCell).Text(ToSpanishMonthName(cmp.PreviousMonth, null));
                                        table.Cell().Element(TableCell).Text(ToCurrency(cmp.PreviousVenta));
                                        table.Cell().Element(cmp.DeltaVenta > 0 ? TableCellUp : cmp.DeltaVenta < 0 ? TableCellDown : TableCellNeutral)
                                            .Text(ToSignedCurrencyWithArrow(cmp.DeltaVenta));
                                        table.Cell().Element(cmp.DeltaPorcentaje > 0 ? TableCellUp : cmp.DeltaPorcentaje < 0 ? TableCellDown : TableCellNeutral)
                                            .Text(ToSignedPercent(cmp.DeltaPorcentaje));
                                    }
                                });
                            }

                            c.Item().Text("Top productos").Bold().FontColor("1A2AA5");
                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(TableHeader).Text("Producto");
                                    h.Cell().Element(TableHeader).Text("Cantidad");
                                    h.Cell().Element(TableHeader).Text("Venta");
                                });

                                foreach (var p in year.TopProductos.Take(10))
                                {
                                    table.Cell().Element(TableCell).Text(p.Producto ?? "-");
                                    table.Cell().Element(TableCell).Text(ToNumber(p.CantidadVendida));
                                    table.Cell().Element(TableCell).Text(ToCurrency(p.VentaTotal));
                                }
                            });

                            if (year.Detalle.Any())
                            {
                                c.Item().Text("Detalle (muestra)").Bold().FontColor("1A2AA5");
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1.4f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(2.4f);
                                        cols.RelativeColumn(2.6f);
                                        cols.RelativeColumn(1.2f);
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().Element(TableHeader).Text("Fecha");
                                        h.Cell().Element(TableHeader).Text("Folio");
                                        h.Cell().Element(TableHeader).Text("Cliente");
                                        h.Cell().Element(TableHeader).Text("Producto");
                                        h.Cell().Element(TableHeader).Text("Importe");
                                    });

                                    foreach (var d in year.Detalle.Take(30))
                                    {
                                        table.Cell().Element(TableCell).Text(d.FechaEmision.ToString("dd/MM/yyyy", EsMx));
                                        table.Cell().Element(TableCell).Text(d.Folio ?? "-");
                                        table.Cell().Element(TableCell).Text(d.Cliente ?? "-");
                                        table.Cell().Element(TableCell).Text(d.Producto ?? "-");
                                        table.Cell().Element(TableCell).Text(ToCurrency(d.ImporteDetalle));
                                    }
                                });

                                if (year.DetalleTruncado)
                                    c.Item().Text($"Nota: detalle truncado. Mostrando {year.Detalle.Count} de {year.DetalleTotalRegistros} registros.")
                                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                            }
                            var monthlySales = year.SerieMensual.OrderBy(x => x.Mes).ToList();
                            if (monthlySales.Any())
                            {
                                c.Item().Text("Grafica de venta mensual").Bold().FontColor("1A2AA5");
                                var maxVenta = monthlySales.Max(x => x.VentaTotal);
                                foreach (var mes in monthlySales)
                                {
                                    var ratio = maxVenta <= 0 ? 0m : (mes.VentaTotal / maxVenta);
                                    c.Item().Row(r =>
                                    {
                                        r.ConstantItem(84).Text(ToSpanishMonthName(mes.Mes, mes.MesNombre)).FontSize(8).FontColor(Colors.Grey.Darken1);
                                        r.RelativeItem().PaddingVertical(2).Text(ToAsciiBar(ratio)).FontSize(8).FontColor("1A2AA5");
                                        r.ConstantItem(95).AlignRight().Text(ToCurrency(mes.VentaTotal)).FontSize(8);
                                    });
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Pagina ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        var yearsLabel = string.Join("-", data.Years.Select(x => x.Year));
        return Task.FromResult(new DashboardVentasReporteFile
        {
            Content = bytes,
            ContentType = "application/pdf",
            FileName = $"dashboard-ventas-{yearsLabel}-{DateTime.Now:yyyyMMdd-HHmm}.pdf"
        });
    }

    private static IContainer Card(IContainer container)
        => container.Border(1).BorderColor("DCE3F5").Background("F8FAFF") .Padding(10);

    private static IContainer KpiBox(IContainer container)
        => container.Background("EEF3FF").Border(1).BorderColor("DCE3F5") .Padding(8);

    private static IContainer TableHeader(IContainer container)
        => container.Background("E1E9FF").BorderBottom(1).BorderColor("C8D5F4").PaddingVertical(4).PaddingHorizontal(6);

    private static IContainer TableCell(IContainer container)
        => container.BorderBottom(1).BorderColor("E9EDF8").PaddingVertical(3).PaddingHorizontal(6);
    private static IContainer TableCellUp(IContainer container)
        => container.BorderBottom(1).BorderColor("D1FAE5").Background("ECFDF3").PaddingVertical(3).PaddingHorizontal(6).DefaultTextStyle(x => x.FontColor("166534").SemiBold());
    private static IContainer TableCellDown(IContainer container)
        => container.BorderBottom(1).BorderColor("FECACA").Background("FEF2F2").PaddingVertical(3).PaddingHorizontal(6).DefaultTextStyle(x => x.FontColor("B91C1C").SemiBold());
    private static IContainer TableCellNeutral(IContainer container)
        => container.BorderBottom(1).BorderColor("FDE68A").Background("FFFBEB").PaddingVertical(3).PaddingHorizontal(6).DefaultTextStyle(x => x.FontColor("B45309").SemiBold());

    private static string ToCurrency(decimal value) => string.Format(EsMx, "{0:C2}", value);
    private static string ToNumber(decimal value) => string.Format(EsMx, "{0:N0}", value);
    private static string ToPercent(decimal value) => string.Format(EsMx, "{0:N2}%", value);
    private static string ToSignedPercent(decimal value) => $"{(value >= 0 ? "+" : "")}{value:N2}%";
    private static string ToSignedCurrencyWithArrow(decimal value)
    {
        var icon = value >= 0 ? "^" : "v";
        var sign = value >= 0 ? "+" : "-";
        return $"{icon} {sign}{ToCurrency(Math.Abs(value))}";
    }

    private static string ToAsciiBar(decimal ratio)
    {
        const int width = 26;
        var safe = Math.Max(0m, Math.Min(1m, ratio));
        var filled = (int)Math.Round(safe * width, MidpointRounding.AwayFromZero);
        return new string('#', filled).PadRight(width, '-');
    }
    private static string ToSpanishMonthName(int month, string? fallbackName)
    {
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
            rows.Add(new MonthlyComparisonRow(curr.Mes, curr.VentaTotal, prev.Mes, prev.VentaTotal, delta, pct));
        }
        return rows;
    }

    private sealed record MonthlyComparisonRow(
        int CurrentMonth,
        decimal CurrentVenta,
        int PreviousMonth,
        decimal PreviousVenta,
        decimal DeltaVenta,
        decimal DeltaPorcentaje
    );

}
