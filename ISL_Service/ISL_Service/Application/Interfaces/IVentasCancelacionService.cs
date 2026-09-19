using ISL_Service.Application.DTOs.VentasCancelacion;

namespace ISL_Service.Application.Interfaces;

public interface IVentasCancelacionService
{
    /// El primer recorrido de Mac31: valida TODO lo marcado y no toca nada.
    Task<VentasCancelacionVerificarResponse> VerificarAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct);

    /// El segundo: vuelve a validar y, si todo sigue en pie, cancela.
    Task<VentasCancelacionResponse> CancelarAsync(VentasCancelacionRequest request, int idUsuario, string equipo, CancellationToken ct);
}
