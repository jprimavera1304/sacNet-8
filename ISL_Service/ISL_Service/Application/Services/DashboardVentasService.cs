using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class DashboardVentasService : IDashboardVentasService
{
    private readonly IDashboardVentasRepository _repository;

    public DashboardVentasService(IDashboardVentasRepository repository)
    {
        _repository = repository;
    }

    public Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarFiltrosAsync(request, ct);
    }

    public Task<DashboardVentasKpisDto?> ConsultarKpisAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarKpisAsync(request, ct);
    }

    public Task<List<DashboardVentasSerieMensualDto>> ConsultarSerieMensualAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarSerieMensualAsync(request, ct);
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

        var detalle = await _repository.ConsultarVentasDetalleAsync(filtro, ct);

        var semanas = BuildSemanasPorRango(firstDay, lastDay);
        foreach (var semana in semanas)
        {
            var items = detalle
                .Where(d => d.FechaEmision.Date >= semana.FechaInicioSemana.Date && d.FechaEmision.Date <= semana.FechaFinSemana.Date)
                .ToList();

            semana.VentaTotal = items.Sum(x => x.ImporteVenta);
            semana.GananciaTotal = items.Sum(x => x.GananciaVenta);
            semana.CantidadVendida = items.Sum(x => x.Cantidad);
        }

        return semanas;
    }

    public Task<List<DashboardTopProductoDto>> ConsultarTopProductosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarTopProductosAsync(request, ct);
    }

    public Task<List<DashboardTopClienteDto>> ConsultarTopClientesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarTopClientesAsync(request, ct);
    }

    public Task<List<DashboardTopCategoriaDto>> ConsultarTopCategoriasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarTopCategoriasAsync(request, ct);
    }

    public Task<List<DashboardTopMarcaDto>> ConsultarTopMarcasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarTopMarcasAsync(request, ct);
    }

    public Task<List<DashboardVentasAlmacenDto>> ConsultarVentasAlmacenesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarVentasAlmacenesAsync(request, ct);
    }

    public Task<List<DashboardVentasAgenteDto>> ConsultarVentasAgentesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarVentasAgentesAsync(request, ct);
    }

    public Task<List<DashboardVentasDetalleDto>> ConsultarVentasDetalleAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);
        return _repository.ConsultarVentasDetalleAsync(request, ct);
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

    private static void ValidateSemanalRequest(DashboardVentasSerieSemanalRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.Year < 2000 || request.Year > 2100)
            throw new ArgumentException("year debe estar entre 2000 y 2100.");
        if (request.Month < 1 || request.Month > 12)
            throw new ArgumentException("month debe estar entre 1 y 12.");
    }

    private static List<DashboardVentasSerieSemanalDto> BuildSemanasPorRango(DateTime firstDay, DateTime lastDay)
    {
        var semanas = new List<DashboardVentasSerieSemanalDto>();
        var year = firstDay.Year;
        var month = firstDay.Month;
        var last = lastDay.Day;

        var ranges = new List<(int start, int end)>
        {
            (1, Math.Min(7, last)),
            (8, Math.Min(14, last)),
            (15, Math.Min(21, last)),
            (22, Math.Min(28, last))
        };

        if (last >= 29)
            ranges.Add((29, last));

        var semanaNumero = 1;
        foreach (var (start, end) in ranges)
        {
            var inicio = new DateTime(year, month, start);
            var fin = new DateTime(year, month, end);
            semanas.Add(new DashboardVentasSerieSemanalDto
            {
                SemanaNumero = semanaNumero++,
                FechaInicioSemana = inicio,
                FechaFinSemana = fin,
                VentaTotal = 0,
                GananciaTotal = 0,
                CantidadVendida = 0
            });
        }

        return semanas;
    }
}
