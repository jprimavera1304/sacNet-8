using ISL_Service.Application.DTOs.VentasSaldos;

namespace ISL_Service.Application.Interfaces;

public interface IVentasSaldosRepository
{
    Task<VentasSaldosResponse> ConsultarSaldosClienteAsync(int idCliente, CancellationToken ct);
}
