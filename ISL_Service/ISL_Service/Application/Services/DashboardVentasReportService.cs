using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class DashboardVentasReportService : IDashboardVentasReportService
{
    private readonly IDashboardVentasService _dashboardService;
    private readonly Dictionary<string, IDashboardVentasReportRenderer> _renderers;

    public DashboardVentasReportService(
        IDashboardVentasService dashboardService,
        IEnumerable<IDashboardVentasReportRenderer> renderers)
    {
        _dashboardService = dashboardService;
        _renderers = renderers.ToDictionary(x => x.Formato.ToLowerInvariant(), x => x);
    }

    public async Task<DashboardVentasReporteFile> GenerarReporteAsync(DashboardVentasReporteRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var formato = NormalizeFormato(request.Formato);
        if (!_renderers.TryGetValue(formato, out var renderer))
            throw new ArgumentException("Formato de reporte no soportado.");

        var years = ResolveYears(request);
        var data = new DashboardVentasReporteData
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Tipo = NormalizeTipo(request.Tipo, years.Count),
            FechaReferenciaInicial = years.Min(y => new DateTime(y, 1, 1)),
            FechaReferenciaFinal = years.Max(y => new DateTime(y, 12, 31))
        };

        foreach (var year in years)
        {
            var filtro = BuildYearFilter(request, year);
            var kpisTask = _dashboardService.ConsultarKpisAsync(filtro, ct);
            var serieTask = _dashboardService.ConsultarSerieMensualAsync(filtro, ct);
            var topTask = _dashboardService.ConsultarTopProductosAsync(filtro, ct);
            // Detalle (muestra) desactivado temporalmente para reducir tiempo de generacion.
            // Para reactivar: usar LoadDetalleAsync(...) como estaba antes.
            var detalleTask = Task.FromResult((new List<DashboardVentasDetalleDto>(), false, 0));

            await Task.WhenAll(kpisTask, serieTask, topTask, detalleTask);

            var detalleResult = detalleTask.Result;
            data.Years.Add(new DashboardVentasReporteYearData
            {
                Year = year,
                Kpis = kpisTask.Result ?? new DashboardVentasKpisDto(),
                SerieMensual = serieTask.Result.OrderBy(x => x.Mes).ToList(),
                TopProductos = topTask.Result.OrderByDescending(x => x.VentaTotal).Take(filtro.Top ?? 10).ToList(),
                Detalle = detalleResult.Item1,
                DetalleTruncado = detalleResult.Item2,
                DetalleTotalRegistros = detalleResult.Item3
            });
        }

        data.Years = data.Years.OrderBy(x => x.Year).ToList();
        data.KpisAcumulados = BuildAcumulado(data.Years);
        data.Comparativo = BuildComparativo(data.Years);

        return await renderer.RenderAsync(data, request, ct);
    }

    private static void ValidateRequest(DashboardVentasReporteRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");

        var formato = NormalizeFormato(request.Formato);
        if (formato != "pdf" && formato != "xlsx")
            throw new ArgumentException("Formato invalido. Usa 'pdf' o 'xlsx'.");

        var years = request.Anios?.Distinct().ToList() ?? new List<int>();
        foreach (var year in years)
        {
            if (year < 2000 || year > 2100)
                throw new ArgumentException("Los años deben estar entre 2000 y 2100.");
        }
    }

    private static string NormalizeFormato(string? formato)
    {
        var value = (formato ?? "pdf").Trim().ToLowerInvariant();
        return value switch
        {
            "excel" => "xlsx",
            _ => value
        };
    }

    private static string NormalizeTipo(string? tipo, int yearsCount)
    {
        var value = (tipo ?? "actual").Trim().ToLowerInvariant();
        if (yearsCount > 1) return "comparados";
        return value == "comparados" ? "comparados" : "actual";
    }

    private static List<int> ResolveYears(DashboardVentasReporteRequest request)
    {
        var years = (request.Anios ?? new List<int>())
            .Where(y => y >= 2000 && y <= 2100)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        if (!years.Any())
        {
            if (request.FechaInicial != default)
                years.Add(request.FechaInicial.Year);
            else
                years.Add(DateTime.UtcNow.Year);
        }

        var tipo = NormalizeTipo(request.Tipo, years.Count);
        if (tipo != "comparados" && years.Count > 1)
            years = new List<int> { years.Last() };

        return years;
    }

    private static DashboardVentasFiltroRequest BuildYearFilter(DashboardVentasReporteRequest request, int year)
    {
        return new DashboardVentasFiltroRequest
        {
            FechaInicial = new DateTime(year, 1, 1),
            FechaFinal = new DateTime(year, 12, 31),
            IDEmpresa = request.IDEmpresa,
            IDAlmacen = request.IDAlmacen,
            IDAgente = request.IDAgente,
            IDCliente = request.IDCliente,
            IDProducto = request.IDProducto,
            IDCategoria = request.IDCategoria,
            IDMarca = request.IDMarca,
            IDTipoDocumento = request.IDTipoDocumento,
            Top = request.Top ?? 10
        };
    }

    private async Task<(List<DashboardVentasDetalleDto>, bool, int)> LoadDetalleAsync(
        DashboardVentasFiltroRequest filtro,
        int maxRows,
        CancellationToken ct)
    {
        const int pageSize = 200;
        var page = 1;
        var total = 0;
        var rows = new List<DashboardVentasDetalleDto>();

        while (rows.Count < maxRows)
        {
            var pageResult = await _dashboardService.ConsultarVentasDetalleAsync(new DashboardVentasDetalleRequest
            {
                FechaInicial = filtro.FechaInicial,
                FechaFinal = filtro.FechaFinal,
                IDEmpresa = filtro.IDEmpresa,
                IDAlmacen = filtro.IDAlmacen,
                IDAgente = filtro.IDAgente,
                IDCliente = filtro.IDCliente,
                IDProducto = filtro.IDProducto,
                IDCategoria = filtro.IDCategoria,
                IDMarca = filtro.IDMarca,
                IDTipoDocumento = filtro.IDTipoDocumento,
                Page = page,
                PageSize = pageSize
            }, ct);

            total = pageResult.Total;
            if (!pageResult.Items.Any())
                break;

            foreach (var item in pageResult.Items)
            {
                if (rows.Count >= maxRows)
                    break;
                rows.Add(item);
            }

            if (rows.Count >= total)
                break;

            page++;
        }

        var truncated = total > rows.Count;
        return (rows, truncated, total);
    }

    private static DashboardVentasKpisDto BuildAcumulado(IEnumerable<DashboardVentasReporteYearData> years)
    {
        var list = years.ToList();
        if (!list.Any()) return new DashboardVentasKpisDto();

        var ticketsTotal = list.Sum(y => y.Kpis.Tickets);
        var ventaTotal = list.Sum(y => y.Kpis.VentaTotal);
        var gananciaTotal = list.Sum(y => y.Kpis.GananciaTotal);
        var descuentoTotal = list.Sum(y => y.Kpis.DescuentoTotal);
        var unidades = list.Sum(y => y.Kpis.UnidadesVendidas);

        return new DashboardVentasKpisDto
        {
            VentaTotal = ventaTotal,
            GananciaTotal = gananciaTotal,
            DescuentoTotal = descuentoTotal,
            Tickets = ticketsTotal,
            UnidadesVendidas = unidades,
            TicketPromedio = ticketsTotal == 0 ? 0 : ventaTotal / ticketsTotal,
            MargenPorcentaje = ventaTotal == 0 ? 0 : (gananciaTotal / ventaTotal) * 100m
        };
    }

    private static DashboardVentasReporteComparativoDto? BuildComparativo(IReadOnlyList<DashboardVentasReporteYearData> years)
    {
        if (years.Count < 2) return null;

        var first = years.First();
        var last = years.Last();

        var deltaVenta = last.Kpis.VentaTotal - first.Kpis.VentaTotal;
        var deltaUnidades = last.Kpis.UnidadesVendidas - first.Kpis.UnidadesVendidas;

        return new DashboardVentasReporteComparativoDto
        {
            FromYear = first.Year,
            ToYear = last.Year,
            DeltaVenta = deltaVenta,
            DeltaUnidades = deltaUnidades,
            DeltaVentaPorcentaje = first.Kpis.VentaTotal == 0 ? 0 : (deltaVenta / first.Kpis.VentaTotal) * 100m,
            DeltaUnidadesPorcentaje = first.Kpis.UnidadesVendidas == 0 ? 0 : (deltaUnidades / first.Kpis.UnidadesVendidas) * 100m
        };
    }
}
