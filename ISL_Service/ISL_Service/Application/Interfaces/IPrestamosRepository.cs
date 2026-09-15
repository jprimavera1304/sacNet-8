using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Prestamos;

namespace ISL_Service.Application.Interfaces;

public interface IPrestamosRepository
{
    Task<PrestamosResponse> ConsultarAsync(
        int idEmpleadoPrestamo, int idEmpleado, int pagados, CancellationToken ct = default);

    Task<(ResultadoLegacy Resultado, int IdEmpleadoPrestamo)> InsertarAsync(
        CrearPrestamoRequest request, int idUsuario, CancellationToken ct = default);

    Task<ResultadoLegacy> ActualizarMontoPagosAsync(
        int idEmpleadoPrestamo, int idEmpleado, decimal montoPagos, int idUsuario, CancellationToken ct = default);

    Task<ResultadoLegacy> CancelarAsync(int idEmpleadoPrestamo, int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Si el empleado ya tiene un prestamo abierto (no cancelado, no pagado y
    /// que no sea de pago inmediato).
    ///
    /// Se pregunta ANTES del alta porque en ese caso legacy no actualiza el
    /// descuento semanal del empleado con el monto que se mando: hay que poder
    /// avisarlo. Despues del alta ya no se distingue.
    /// </summary>
    Task<bool> TienePrestamoAbiertoAsync(int idEmpleado, CancellationToken ct = default);
}
