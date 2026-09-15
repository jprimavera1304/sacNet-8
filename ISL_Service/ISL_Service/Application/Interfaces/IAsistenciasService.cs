using ISL_Service.Application.DTOs.Asistencias;

namespace ISL_Service.Application.Interfaces;

public interface IAsistenciasService
{
    Task<List<PeriodoNominaDto>> ConsultarPeriodosAsync(int idTipoSueldo, int top, CancellationToken ct = default);
    Task<List<DiaPeriodoDto>> ConsultarDiasAsync(int idPeriodoTipoSueldo, CancellationToken ct = default);
    Task<RejillaAsistenciaResponse> ConsultarRejillaAsync(int idPeriodoTipoSueldo, int idEmpleado, int idStatus, CancellationToken ct = default);
    Task GuardarDiaAsync(GuardarAsistenciaDiaRequest request, int idUsuario, string equipo, CancellationToken ct = default);
    Task EliminarDiaAsync(int idEmpleado, DateTime? fecha, int idUsuario, string equipo, CancellationToken ct = default);
}
