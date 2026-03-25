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
}
