using ISL_Service.Application.DTOs.Checador;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Acceso a los SP sp_w_* de checadas de comida (tabla WEmpleadoChecada).
/// El repositorio NO valida ni normaliza: recibe los valores ya listos para el SP.
/// </summary>
public interface IChecadorRepository
{
    Task<ChecadaComidaRegistradaDto> RegistrarComidaAsync(
        int idEmpleado,
        string tipo,
        int? idEmpleadoHuella,
        string origen,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);

    Task<ChecadaComidaRowsResponse> ConsultarComidasAsync(
        int idEmpleado,
        DateTime fechaInicial,
        DateTime fechaFinal,
        bool incluirCanceladas,
        CancellationToken ct = default);

    Task CancelarComidaAsync(
        int idEmpleadoChecada,
        string motivo,
        int idUsuario,
        string equipo,
        CancellationToken ct = default);
}
