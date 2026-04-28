using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace ISL_Service.Application.Services;

public class DashboardVentasService : IDashboardVentasService
{
    private static readonly CultureInfo EsMx = CultureInfo.GetCultureInfo("es-MX");
    private readonly IDashboardVentasRepository _repository;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public DashboardVentasService(IDashboardVentasRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarFiltrosAsync(request, ct);
    }

    public Task<DashboardVentasKpisDto?> ConsultarKpisAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("kpis", request), () => _repository.ConsultarKpisAsync(request, ct));
    }

    public Task<List<DashboardVentasSerieMensualDto>> ConsultarSerieMensualAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("serie_mensual", request), async () =>
        {
            var data = await _repository.ConsultarSerieMensualAsync(request, ct);
            foreach (var item in data)
            {
                item.MesNombre = BuildMonthYearLabel(item.Anio, item.Mes, item.MesNombre);
            }
            return data;
        });
    }

    public async Task<List<DashboardVentasSerieSemanalDto>> ConsultarSerieSemanalAsync(DashboardVentasSerieSemanalRequest request, CancellationToken ct = default)
    {
        ValidateSemanalRequest(request);

        var firstDay = new DateTime(request.Year, request.Month, 1);
        var lastDay = new DateTime(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month));

        var filtro = new DashboardVentasFiltroRequest
        {
            FechaInicial = firstDay,
            FechaFinal = lastDay,
            IDEmpresa = request.IDEmpresa,
            IDAlmacen = request.IDAlmacen,
            IDAgente = request.IDAgente,
            IDCliente = request.IDCliente,
            IDProducto = request.IDProducto,
            IDCategoria = request.IDCategoria,
            IDMarca = request.IDMarca,
            IDTipoDocumento = request.IDTipoDocumento
        };

        var raw = await GetOrCreateAsync(CacheKey("serie_semanal", filtro), () => _repository.ConsultarSerieSemanalAsync(filtro, ct));
        var semanas = BuildSemanasIso(firstDay, lastDay);

        foreach (var semana in semanas)
        {
            var match = raw.FirstOrDefault(x => x.LunesSemana.Date == semana.FechaInicioSemana.Date);
            if (match is null)
                continue;

            semana.VentaTotal = match.VentaTotal;
            semana.GananciaTotal = match.GananciaTotal;
            semana.CantidadVendida = 0;
        }

        return semanas;
    }

    public Task<List<DashboardTopProductoDto>> ConsultarTopProductosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("top_productos", request), () => _repository.ConsultarTopProductosAsync(request, ct));
    }

    public Task<List<DashboardTopClienteDto>> ConsultarTopClientesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("top_clientes", request), () => _repository.ConsultarTopClientesAsync(request, ct));
    }


    public Task<List<DashboardVentasAlmacenDto>> ConsultarVentasAlmacenesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("almacenes", request), () => _repository.ConsultarVentasAlmacenesAsync(request, ct));
    }

    public Task<List<DashboardVentasAgenteDto>> ConsultarVentasAgentesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("agentes", request), () => _repository.ConsultarVentasAgentesAsync(request, ct));
    }

    public async Task<DashboardVentasDetallePagedResponse> ConsultarVentasDetalleAsync(DashboardVentasDetalleRequest request, CancellationToken ct = default)
    {
        ValidateDetalleRequest(request);

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 50;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var filtro = new DashboardVentasFiltroRequest
        {
            FechaInicial = request.FechaInicial,
            FechaFinal = request.FechaFinal,
            IDEmpresa = request.IDEmpresa,
            IDAlmacen = request.IDAlmacen,
            IDAgente = request.IDAgente,
            IDCliente = request.IDCliente,
            IDProducto = request.IDProducto,
            IDCategoria = request.IDCategoria,
            IDMarca = request.IDMarca,
            IDTipoDocumento = request.IDTipoDocumento
        };

        var all = await GetOrCreateAsync(CacheKey("detalle", filtro), () => _repository.ConsultarVentasDetalleAsync(filtro, ct));
        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new DashboardVentasDetallePagedResponse
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    public async Task<DashboardVentasOverviewResponse> ConsultarOverviewAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var kpisTask = ConsultarKpisAsync(request, ct);
        var serieTask = ConsultarSerieMensualAsync(request, ct);
        var topProdTask = ConsultarTopProductosAsync(request, ct);
        var topCliTask = ConsultarTopClientesAsync(request, ct);
        var topCatTask = ConsultarTopCategoriasAsync(request, ct);
        var topMarTask = ConsultarTopMarcasAsync(request, ct);
        var almacenesTask = ConsultarVentasAlmacenesAsync(request, ct);
        var agentesTask = ConsultarVentasAgentesAsync(request, ct);

        await Task.WhenAll(kpisTask, serieTask, topProdTask, topCliTask, topCatTask, topMarTask, almacenesTask, agentesTask);

        return new DashboardVentasOverviewResponse
        {
            Kpis = kpisTask.Result,
            SerieMensual = serieTask.Result,
            TopProductos = topProdTask.Result,
            TopClientes = topCliTask.Result,
            TopCategorias = topCatTask.Result,
            TopMarcas = topMarTask.Result,
            Almacenes = almacenesTask.Result,
            Agentes = agentesTask.Result
        };
    }

    public Task<List<DashboardTopCategoriaDto>> ConsultarTopCategoriasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("top_categorias", request), () => _repository.ConsultarTopCategoriasAsync(request, ct));
    }

    public Task<List<DashboardTopMarcaDto>> ConsultarTopMarcasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return GetOrCreateAsync(CacheKey("top_marcas", request), () => _repository.ConsultarTopMarcasAsync(request, ct));
    }

    private static void ValidateRequest(DashboardVentasFiltroRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.FechaInicial == default)
            throw new ArgumentException("fechaInicial es requerida.");
        if (request.FechaFinal == default)
            throw new ArgumentException("fechaFinal es requerida.");
        if (request.FechaFinal < request.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser menor a fechaInicial.");
    }

    private static void ValidateDetalleRequest(DashboardVentasDetalleRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.FechaInicial == default)
            throw new ArgumentException("fechaInicial es requerida.");
        if (request.FechaFinal == default)
            throw new ArgumentException("fechaFinal es requerida.");
        if (request.FechaFinal < request.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser menor a fechaInicial.");
    }

    private string CacheKey(string prefix, DashboardVentasFiltroRequest request)
    {
        return $"{prefix}|{request.FechaInicial:yyyy-MM-dd}|{request.FechaFinal:yyyy-MM-dd}|{request.IDEmpresa}|{request.IDAlmacen}|{request.IDAgente}|{request.IDCliente}|{request.IDProducto}|{request.IDCategoria}|{request.IDMarca}|{request.IDTipoDocumento}|{request.Top}";
    }

    private Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return Task.FromResult(cached);

        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await factory();
        })!;
    }

    private static void ValidateSemanalRequest(DashboardVentasSerieSemanalRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.Year < 2000 || request.Year > 2100)
            throw new ArgumentException("year debe estar entre 2000 y 2100.");
        if (request.Month < 1 || request.Month > 12)
            throw new ArgumentException("month debe estar entre 1 y 12.");
    }

    private static List<DashboardVentasSerieSemanalDto> BuildSemanasIso(DateTime firstDay, DateTime lastDay)
    {
        var semanas = new List<DashboardVentasSerieSemanalDto>();

        var startMonday = firstDay.AddDays(-((int)firstDay.DayOfWeek + 6) % 7);
        var endMonday = lastDay.AddDays(-((int)lastDay.DayOfWeek + 6) % 7);

        var semanaNumero = 1;
        for (var monday = startMonday; monday <= endMonday; monday = monday.AddDays(7))
        {
            semanas.Add(new DashboardVentasSerieSemanalDto
            {
                SemanaNumero = semanaNumero++,
                FechaInicioSemana = monday.Date,
                FechaFinSemana = monday.AddDays(6).Date,
                VentaTotal = 0,
                GananciaTotal = 0,
                CantidadVendida = 0
            });
        }

        return semanas;
    }

    private static string BuildMonthYearLabel(int year, int month, string? fallback)
    {
        if (month is >= 1 and <= 12)
        {
            var monthName = EsMx.DateTimeFormat.GetMonthName(month);
            if (!string.IsNullOrWhiteSpace(monthName))
                return $"{char.ToUpper(monthName[0], EsMx)}{monthName[1..]} {year}";
        }

        if (!string.IsNullOrWhiteSpace(fallback))
            return $"{fallback.Trim()} {year}";

        return $"{month} {year}";
    }
}
