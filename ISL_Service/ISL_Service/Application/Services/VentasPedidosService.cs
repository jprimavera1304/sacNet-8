using ISL_Service.Application.DTOs.VentasPedidos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasPedidosService : IVentasPedidosService
{
    private readonly IVentasPedidosRepository _repo;

    public VentasPedidosService(IVentasPedidosRepository repo)
    {
        _repo = repo;
    }

    public async Task<ConsultaVentasPedidosResponse> ConsultarPendientesAutorizarAsync(
        ConsultaVentasPedidosRequest request,
        int idUsuarioToken,
        CancellationToken ct)
    {
        var safe = request ?? new ConsultaVentasPedidosRequest();
        if (safe.IDUsuarioActual <= 0 && idUsuarioToken > 0)
            safe.IDUsuarioActual = idUsuarioToken;
        if (safe.IDStatusPedido <= 0)
            safe.IDStatusPedido = 1; // PendientesAutorizarRechazar

        var list = await _repo.ConsultarPendientesAutorizarAsync(safe, ct);
        return new ConsultaVentasPedidosResponse(list);
    }

    public Task<AutorizarPedidosResponse> AutorizarPedidosAsync(
        AutorizarPedidosRequest request,
        int idUsuarioToken,
        string equipoToken,
        CancellationToken ct)
    {
        return _repo.AutorizarPedidosAsync(request, idUsuarioToken, equipoToken, ct);
    }
}
