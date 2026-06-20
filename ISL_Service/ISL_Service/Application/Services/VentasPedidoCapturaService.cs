using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

public class VentasPedidoCapturaService : IVentasPedidoCapturaService
{
    private readonly IVentasPedidoCapturaRepository _repository;

    public VentasPedidoCapturaService(IVentasPedidoCapturaRepository repository)
    {
        _repository = repository;
    }

    public Task<PedidoBootstrapResponse> BootstrapAsync(int idUsuario, CancellationToken ct)
        => _repository.BootstrapAsync(idUsuario, ct);

    public Task<PedidoSnapshotDto> ConsultarPedidoAsync(int idPedido, CancellationToken ct)
        => _repository.ConsultarPedidoAsync(idPedido, ct);

    public Task<PedidoClienteContextResponse> BuscarClienteAsync(PedidoClienteBuscarRequest request, CancellationToken ct)
        => _repository.BuscarClienteAsync(request, ct);

    public Task<PedidoRowsResponse> BuscarProductoAsync(PedidoProductoBuscarRequest request, CancellationToken ct)
        => _repository.BuscarProductoAsync(request, ct);

    public Task<PedidoSnapshotDto> AgregarDetalleAsync(PedidoAgregarDetalleRequest request, int idUsuario, string equipo, CancellationToken ct)
        => _repository.AgregarDetalleAsync(request, idUsuario, equipo, ct);

    public Task<PedidoSnapshotDto> EliminarDetalleAsync(PedidoEliminarDetalleRequest request, CancellationToken ct)
        => _repository.EliminarDetalleAsync(request, ct);

    public Task<PedidoSnapshotDto> GuardarAsync(PedidoGuardarRequest request, int idUsuario, string equipo, CancellationToken ct)
        => _repository.GuardarAsync(request, idUsuario, equipo, ct);

    public Task<PedidoRowsResponse> EliminarBorradorAsync(int idUsuario, CancellationToken ct)
        => _repository.EliminarBorradorAsync(idUsuario, ct);
}
