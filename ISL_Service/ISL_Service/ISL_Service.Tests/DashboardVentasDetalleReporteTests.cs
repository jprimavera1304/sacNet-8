using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Tests;

/// <summary>
/// EL DETALLE DEL REPORTE DE DASHBOARD.
///
/// Estaba apagado con un comentario ("desactivado para reducir tiempo de
/// generacion") y LoadDetalleAsync era codigo muerto, asi que IncluirDetalle y
/// MaxDetalleFilas se recibian del cliente y se tiraban: pedir detalle devolvia
/// 200 con el detalle vacio y sin avisar.
///
/// Estas pruebas fijan las tres cosas que importan de haberlo terminado: que el
/// detalle salga cuando se pide, que NO se pague cuando no se pide, y que el tope
/// de filas se respete y se declare.
/// </summary>
public class DashboardVentasDetalleReporteTests
{
    [Fact]
    public async Task SinPedirDetalleNoSeConsultaElDetalle()
    {
        var dashboard = new DashboardFalso(totalDetalle: 1000);
        var renderer = new RendererEspia();
        var service = NuevoServicio(dashboard, renderer);

        await service.GenerarReporteAsync(new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = false
        });

        Assert.Equal(0, dashboard.LlamadasDetalle);
        Assert.Empty(renderer.Ultima!.Years.Single().Detalle);
    }

    [Fact]
    public async Task PidiendoDetalleElReporteLoTrae()
    {
        var dashboard = new DashboardFalso(totalDetalle: 30);
        var renderer = new RendererEspia();
        var service = NuevoServicio(dashboard, renderer);

        await service.GenerarReporteAsync(new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = true
        });

        var year = renderer.Ultima!.Years.Single();
        Assert.True(dashboard.LlamadasDetalle > 0);
        Assert.Equal(30, year.Detalle.Count);
        Assert.Equal(30, year.DetalleTotalRegistros);
        Assert.False(year.DetalleTruncado);
    }

    [Fact]
    public async Task ElTopeDeFilasSeRespetaYSeDeclaraElRecorte()
    {
        var dashboard = new DashboardFalso(totalDetalle: 900);
        var renderer = new RendererEspia();
        var service = NuevoServicio(dashboard, renderer);

        await service.GenerarReporteAsync(new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = true,
            MaxDetalleFilas = 250
        });

        var year = renderer.Ultima!.Years.Single();
        Assert.Equal(250, year.Detalle.Count);
        Assert.Equal(900, year.DetalleTotalRegistros);
        Assert.True(year.DetalleTruncado);
    }

    /// <summary>
    /// Un MaxDetalleFilas absurdo no es "sin limite": se acota al tope duro de
    /// 5000 por ano. Si no, un cliente podria pedir la base entera por HTTP.
    /// </summary>
    [Fact]
    public async Task UnTopeAbsurdoSeAcota()
    {
        var dashboard = new DashboardFalso(totalDetalle: 6000);
        var renderer = new RendererEspia();
        var service = NuevoServicio(dashboard, renderer);

        await service.GenerarReporteAsync(new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = true,
            MaxDetalleFilas = 999999
        });

        var year = renderer.Ultima!.Years.Single();
        Assert.Equal(5000, year.Detalle.Count);
        Assert.True(year.DetalleTruncado);
    }

    /// <summary>
    /// LA TRAMPA DE LA CACHE.
    ///
    /// El reporte se cachea por una llave armada con los filtros. Si la decision
    /// de incluir detalle no entrara en esa llave, el primer reporte sin detalle
    /// se le serviria tambien a quien SI lo pidio — y al contrario.
    /// </summary>
    [Fact]
    public async Task PedirDetalleNoSeSirveDeLaCacheDelQueNoLoPidio()
    {
        var dashboard = new DashboardFalso(totalDetalle: 10);
        var renderer = new RendererEspia();
        var service = NuevoServicio(dashboard, renderer);

        var sinDetalle = new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = false
        };
        await service.GenerarReporteAsync(sinDetalle);
        Assert.Empty(renderer.Ultima!.Years.Single().Detalle);

        await service.GenerarReporteAsync(new DashboardVentasReporteRequest
        {
            Formato = "pdf",
            Anios = new List<int> { 2026 },
            IncluirDetalle = true
        });

        Assert.Equal(10, renderer.Ultima!.Years.Single().Detalle.Count);
    }

    private static DashboardVentasReportService NuevoServicio(
        IDashboardVentasService dashboard,
        IDashboardVentasReportRenderer renderer)
        => new(dashboard, new MemoryCache(new MemoryCacheOptions()), new[] { renderer });

    /// <summary>Se queda con los datos que le pasaron a renderizar.</summary>
    private sealed class RendererEspia : IDashboardVentasReportRenderer
    {
        public string Formato => "pdf";
        public DashboardVentasReporteData? Ultima { get; private set; }

        public Task<DashboardVentasReporteFile> RenderAsync(
            DashboardVentasReporteData data,
            DashboardVentasReporteRequest request,
            CancellationToken ct = default)
        {
            Ultima = data;
            return Task.FromResult(new DashboardVentasReporteFile { Content = new byte[] { 1 } });
        }
    }

    /// <summary>
    /// Paginador en memoria, igual que el servicio real: la consulta completa se
    /// hace una vez y las paginas se cortan de ahi.
    /// </summary>
    private sealed class DashboardFalso : IDashboardVentasService
    {
        private readonly List<DashboardVentasDetalleDto> _detalle;

        public DashboardFalso(int totalDetalle)
        {
            _detalle = Enumerable.Range(1, totalDetalle)
                .Select(i => new DashboardVentasDetalleDto { ImporteDetalle = i })
                .ToList();
        }

        public int LlamadasDetalle { get; private set; }

        public Task<DashboardVentasDetallePagedResponse> ConsultarVentasDetalleAsync(DashboardVentasDetalleRequest request, CancellationToken ct = default)
        {
            LlamadasDetalle++;
            var page = request.Page ?? 1;
            var pageSize = Math.Min(request.PageSize ?? 50, 200);

            return Task.FromResult(new DashboardVentasDetallePagedResponse
            {
                Page = page,
                PageSize = pageSize,
                Total = _detalle.Count,
                Items = _detalle.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            });
        }

        public Task<DashboardVentasKpisDto?> ConsultarKpisAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => Task.FromResult<DashboardVentasKpisDto?>(new DashboardVentasKpisDto());

        public Task<List<DashboardVentasSerieMensualDto>> ConsultarSerieMensualAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => Task.FromResult(new List<DashboardVentasSerieMensualDto>());

        public Task<List<DashboardTopProductoDto>> ConsultarTopProductosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => Task.FromResult(new List<DashboardTopProductoDto>());

        public Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardVentasSerieSemanalDto>> ConsultarSerieSemanalAsync(DashboardVentasSerieSemanalRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardTopClienteDto>> ConsultarTopClientesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardTopCategoriaDto>> ConsultarTopCategoriasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardTopMarcaDto>> ConsultarTopMarcasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardVentasAlmacenDto>> ConsultarVentasAlmacenesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<DashboardVentasAgenteDto>> ConsultarVentasAgentesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DashboardVentasOverviewResponse> ConsultarOverviewAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
