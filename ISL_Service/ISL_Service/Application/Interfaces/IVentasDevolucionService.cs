using ISL_Service.Application.DTOs.VentasDevolucion;

namespace ISL_Service.Application.Interfaces;

public interface IVentasDevolucionService
{
    /// Todo lo que la pantalla necesita para abrirse, en una sola ida.
    Task<VentasDevolucionVistaResponse> VistaAsync(int idVenta, CancellationToken ct);

    /// Devuelve. Quien decide es sp_n_DevolucionVenta.
    Task<VentasDevolucionResponse> DevolverAsync(
        VentasDevolucionRequest request, int idUsuario, string equipo, CancellationToken ct);
}
