using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace ISL_Service.Application.Services;

public class DashboardVentasReportService : IDashboardVentasReportService
{
    private readonly IDashboardVentasService _dashboardService;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, IDashboardVentasReportRenderer> _renderers;
    private static readonly TimeSpan ReportDataCacheTtl = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ReportFileCacheTtl = TimeSpan.FromMinutes(2);
    private const int MaxYearParallelism = 3;

    public DashboardVentasReportService(
        IDashboardVentasService dashboardService,
        IMemoryCache cache,
        IEnumerable<IDashboardVentasReportRenderer> renderers)
    {
        _dashboardService = dashboardService;
        _cache = cache;
        _renderers = renderers.ToDictionary(x => x.Formato.ToLowerInvariant(), x => x);
    }

    public async Task<DashboardVentasReporteFile> GenerarReporteAsync(DashboardVentasReporteRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var formato = NormalizeFormato(request.Formato);
        if (!_renderers.TryGetValue(formato, out var renderer))
            throw new ArgumentException("Formato de reporte no soportado.");

        var years = ResolveYears(request);
        var tipo = NormalizeTipo(request.Tipo, years.Count);

        var requestCacheKey = BuildRequestCacheKey(request, tipo, years);
        var fileCacheKey = $"dashboard_ventas:reporte:file:{formato}:{requestCacheKey}";

        if (_cache.TryGetValue(fileCacheKey, out DashboardVentasReporteFile? cachedFile) && cachedFile is not null)
            return CloneFile(cachedFile);

        var dataCacheKey = $"dashboard_ventas:reporte:data:{requestCacheKey}";
        var data = await _cache.GetOrCreateAsync(dataCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReportDataCacheTtl;
            return await BuildReportDataAsync(request, years, tipo, ct);
        }) ?? throw new InvalidOperationException("No se pudo preparar los datos del reporte.");

        var file = await renderer.RenderAsync(data, request, ct);
        _cache.Set(fileCacheKey, file, ReportFileCacheTtl);
        return CloneFile(file);
    }

    private async Task<DashboardVentasReporteData> BuildReportDataAsync(
        DashboardVentasReporteRequest request,
        List<int> years,
        string tipo,
        CancellationToken ct)
    {
        var data = new DashboardVentasReporteData
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Tipo = tipo,
            FechaReferenciaInicial = years.Min(y => new DateTime(y, 1, 1)),
            FechaReferenciaFinal = years.Max(y => new DateTime(y, 12, 31))
        };

        var yearResults = new DashboardVentasReporteYearData[years.Count];
        var yearsWithIndex = years.Select((year, index) => (year, index));
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Min(MaxYearParallelism, Math.Max(1, years.Count))
        };

        await Parallel.ForEachAsync(yearsWithIndex, parallelOptions, async (item, token) =>
        {
            var filtro = BuildYearFilter(request, item.year);
            var kpisTask = _dashboardService.ConsultarKpisAsync(filtro, token);
            var serieTask = _dashboardService.ConsultarSerieMensualAsync(filtro, token);
            var topTask = _dashboardService.ConsultarTopProductosAsync(filtro, token);

            // Detalle (muestra) desactivado temporalmente para reducir tiempo de generacion.
            // Para reactivar: usar LoadDetalleAsync(...) como estaba antes.
            var detalleTask = Task.FromResult((new List<DashboardVentasDetalleDto>(), false, 0));

            await Task.WhenAll(kpisTask, serieTask, topTask, detalleTask);

            var detalleResult = detalleTask.Result;
            yearResults[item.index] = new DashboardVentasReporteYearData
            {
                Year = item.year,
                Kpis = kpisTask.Result ?? new DashboardVentasKpisDto(),
                SerieMensual = serieTask.Result.OrderBy(x => x.Mes).ToList(),
                TopProductos = topTask.Result.OrderByDescending(x => x.VentaTotal).Take(filtro.Top ?? 10).ToList(),
                Detalle = detalleResult.Item1,
                DetalleTruncado = detalleResult.Item2,
                DetalleTotalRegistros = detalleResult.Item3
            };
        });

        data.Years = yearResults.OrderBy(x => x.Year).ToList();
        data.KpisAcumulados = BuildAcumulado(data.Years);
        data.Comparativo = BuildComparativo(data.Years);

        return data;
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
                throw new ArgumentException("Los anos deben estar entre 2000 y 2100.");
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

    private static DashboardVentasReporteFile CloneFile(DashboardVentasReporteFile src)
    {
        return new DashboardVentasReporteFile
        {
            Content = src.Content.ToArray(),
            ContentType = src.ContentType,
            FileName = src.FileName
        };
    }

    private static string BuildRequestCacheKey(DashboardVentasReporteRequest request, string tipo, IReadOnlyCollection<int> years)
    {
        var yearsKey = string.Join(",", years.OrderBy(x => x));
        return string.Join("|", new[]
        {
            tipo,
            yearsKey,
            (request.IDEmpresa ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDAlmacen ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDAgente ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDCliente ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDProducto ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDCategoria ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDMarca ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.IDTipoDocumento ?? 0).ToString(CultureInfo.InvariantCulture),
            (request.Top ?? 10).ToString(CultureInfo.InvariantCulture),
            request.FechaInicial == default ? "-" : request.FechaInicial.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.FechaFinal == default ? "-" : request.FechaFinal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }
}
