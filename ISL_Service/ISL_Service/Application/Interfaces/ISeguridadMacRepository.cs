using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.SeguridadMac;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Usuarios de Mac31, huellas, constantes de empresa y parametros de reporte.
///
/// Los tres metodos de lote (almacenes, procesos y fechas del usuario) ejecutan el
/// procedimiento una vez por renglon DENTRO DE UNA TRANSACCION: si uno falla, no
/// se aplica ninguno. En legacy ese ciclo vivia sin transaccion y una falla a
/// medias dejaba al usuario con parte de sus permisos cambiados.
/// </summary>
public interface ISeguridadMacRepository
{
    // --- Usuario de Mac31
    Task<(ResultadoLegacy Resultado, int Id)> InsertarUsuarioMacAsync(CrearUsuarioMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarUsuarioMacAsync(int id, ActualizarUsuarioMacRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusUsuarioMacAsync(int id, int idStatus, CancellationToken ct);

    Task<LoteSeguridadResponse> GuardarAlmacenesDeUsuarioAsync(int idUsuario, AlmacenesDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct);
    Task<LoteSeguridadResponse> GuardarProcesosDeUsuarioAsync(int idUsuario, ProcesosDeUsuarioRequest r, int idUsuarioMovimiento, CancellationToken ct);
    Task<LoteSeguridadResponse> GuardarFechasDeUsuarioAsync(int idUsuario, FechasDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct);

    // --- Huellas
    Task<ResultadoLegacy> GuardarHuellaAsync(GuardarHuellaRequest r, byte[] huella, byte[]? huella2, int idUsuarioMovimiento, string equipo, CancellationToken ct);

    // --- Bitacora de contrasenas de autorizacion
    Task<(ResultadoLegacy Resultado, int Id)> RegistrarContrasenaCapturadaAsync(RegistrarContrasenaCapturadaRequest r, int idUsuario, string equipo, CancellationToken ct);

    // --- Constantes
    Task<List<Dictionary<string, object?>>> ActualizarConstantesAsync(ActualizarConstantesRequest r, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ActualizarFechaOperacionPagosAsync(ActualizarFechaOperacionPagosRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> RegistrarActividadAsync(int idUsuario, string equipo, CancellationToken ct);

    // --- Parametros de reporte
    Task<List<Dictionary<string, object?>>> ConsultarParametrosAsync(int id, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> GuardarParametrosAsync(GuardarParametrosRequest r, int idUsuario, string equipo, CancellationToken ct);
}
