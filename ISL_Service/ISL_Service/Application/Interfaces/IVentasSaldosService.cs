using ISL_Service.Application.DTOs.VentasSaldos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasSaldosService
{
    Task<VentasSaldosResponse> ConsultarSaldosClienteAsync(VentasSaldosRequest request, CancellationToken ct);
}
