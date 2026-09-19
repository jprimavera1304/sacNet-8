using ISL_Service.Application.DTOs.VentasMultiPago;

namespace ISL_Service.Application.Interfaces;

public interface IVentasMultiPagoService
{
    Task<VentasMultiPagoPantallaResponse> PantallaAsync(VentasMultiPagoPantallaRequest request, CancellationToken ct);

    Task<VentasMultiPagoGuardarResponse> GuardarAsync(
        VentasMultiPagoGuardarRequest request,
        int idUsuarioLegacy,
        string equipo,
        CancellationToken ct);
}
