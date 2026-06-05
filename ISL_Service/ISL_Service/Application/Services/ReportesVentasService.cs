using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class ReportesVentasService : IReportesVentasService
{
    private readonly IReportesVentasRepository _repository;

    public ReportesVentasService(IReportesVentasRepository repository)
    {
        _repository = repository;
    }

    public Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        Validate(request);
        return _repository.ConsultarAcumuladoresProductosAsync(request, ct);
    }

    private static void Validate(ReportesVentasAcumuladoresProductosRequest request)
    {
        if (request is null)
            throw new ArgumentException("Body requerido.");
        if (request.FechaInicial == default)
            throw new ArgumentException("fechaInicial es requerida.");
        if (request.FechaFinal == default)
            throw new ArgumentException("fechaFinal es requerida.");
        if (request.FechaFinal < request.FechaInicial)
            throw new ArgumentException("fechaFinal no puede ser menor a fechaInicial.");

        var salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant();
        if (salida != "pantalla" && salida != "excel")
            throw new ArgumentException("salida debe ser 'pantalla' o 'excel'.");
    }
}
