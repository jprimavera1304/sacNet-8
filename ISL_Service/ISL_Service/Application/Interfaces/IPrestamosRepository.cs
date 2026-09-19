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

    /// <summary>
    /// La FechaFinal del periodo de NOMINA que esta abierto, o null si no hay
    /// ninguno.
    ///
    /// Es el tope de la regla de PAGO INMEDIATO de Mac31
    /// (Legacy/Mac31/Mac31/Forms/Nomina/Prestamos.cs:398-409): un prestamo que
    /// se cobra de golpe se descuenta en el periodo que esta corriendo, asi que
    /// fecharlo despues del cierre de ese periodo hace que el descuento no se
    /// aplique nunca. El prestamo se queda vivo y nadie lo cobra.
    ///
    /// Mac31 lo lee igual: ConsultarPeriodosTipoSueldo con IDPeriodoStatus = 1,
    /// IDTipoSueldo = Nomina (1) y orden descendente, y se queda con Rows[0].
    /// </summary>
    Task<DateTime?> FinDelPeriodoNominaAbiertoAsync(CancellationToken ct = default);
}
