using ISL_Service.Application.DTOs.Prestamos;

namespace ISL_Service.Application.Interfaces;

public interface IPrestamosService
{
    Task<PrestamosResponse> ConsultarAsync(int idEmpleado, string? estado, CancellationToken ct = default);
    Task<Dictionary<string, object?>?> ObtenerAsync(int idEmpleadoPrestamo, CancellationToken ct = default);
    Task<PrestamoCreadoDto> CrearAsync(CrearPrestamoRequest request, int idUsuario, CancellationToken ct = default);
    Task CambiarMontoPagosAsync(int idEmpleadoPrestamo, decimal montoPagos, int idUsuario, CancellationToken ct = default);
    Task CancelarAsync(int idEmpleadoPrestamo, int idUsuario, CancellationToken ct = default);
}
