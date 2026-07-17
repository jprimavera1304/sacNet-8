using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasConsultaService : IVentasConsultaService
{
    private readonly IVentasConsultaRepository _repository;

    public VentasConsultaService(IVentasConsultaRepository repository)
    {
        _repository = repository;
    }

    public Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
    {
        return _repository.ConsultarCatalogosAsync(ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarRemisionesAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        safe.Formato = 0;
        // "Solo mis pedidos" (app movil): filtra por el usuario del token, el
        // mismo que se guarda al crear el pedido -> garantiza que empaten.
        if (safe.SoloMisPedidos && idUsuarioToken > 0)
            safe.IDUsuario = idUsuarioToken;
        return _repository.ConsultarPedidosAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarPendientesImprimirAsync(safe, ct);
    }

    public Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
    {
        var safe = Prepare(request, idUsuarioToken);
        return _repository.ConsultarPagosAsync(safe, ct);
    }

    private static VentasConsultaRequest Prepare(VentasConsultaRequest? request, int idUsuarioToken)
    {
        var safe = request ?? new VentasConsultaRequest();
        if (safe.IDUsuarioActual <= 0 && idUsuarioToken > 0)
            safe.IDUsuarioActual = idUsuarioToken;
        return safe;
    }
}
