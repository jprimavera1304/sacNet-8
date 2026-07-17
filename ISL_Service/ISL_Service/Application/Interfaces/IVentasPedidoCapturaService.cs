using ISL_Service.Application.DTOs.VentasPedidoCaptura;

namespace ISL_Service.Application.Interfaces;

public interface IVentasPedidoCapturaService
{
    Task<PedidoBootstrapResponse> BootstrapAsync(int idUsuario, CancellationToken ct);
    Task<PedidoSnapshotDto> ConsultarPedidoAsync(int idPedido, CancellationToken ct);
    Task<PedidoClienteContextResponse> BuscarClienteAsync(PedidoClienteBuscarRequest request, CancellationToken ct);
    Task<PedidoRowsResponse> BuscarProductoAsync(PedidoProductoBuscarRequest request, CancellationToken ct);
    Task<PedidoProductoPaginaResponse> BuscarProductoPaginaAsync(PedidoProductoPaginaRequest request, CancellationToken ct);
    Task<PedidoSnapshotDto> AgregarDetalleAsync(PedidoAgregarDetalleRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<PedidoSnapshotDto> EliminarDetalleAsync(PedidoEliminarDetalleRequest request, CancellationToken ct);
    Task<PedidoSnapshotDto> GuardarAsync(PedidoGuardarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<PedidoRowsResponse> EliminarBorradorAsync(int idUsuario, CancellationToken ct);
    Task<PedidoPropiedad?> ObtenerPropiedadPedidoAsync(int idPedido, CancellationToken ct);
}
