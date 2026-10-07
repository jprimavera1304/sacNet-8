using ISL_Service.Application.DTOs.SeguridadMac;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Usuarios de Mac31, huellas, constantes y parametros, vistos desde los
/// controladores. <c>idUsuarioMovimiento</c> y <c>equipo</c> vienen del token.
/// </summary>
public interface ISeguridadMacService
{
    Task<SeguridadEscrituraResponse> CrearUsuarioMacAsync(CrearUsuarioMacRequest r, CancellationToken ct);
    Task<SeguridadEscrituraResponse> ActualizarUsuarioMacAsync(int id, ActualizarUsuarioMacRequest r, CancellationToken ct);

    /// <summary>null = el usuario no existe (404).</summary>
    Task<SeguridadEscrituraResponse?> CambiarStatusUsuarioMacAsync(int id, int idStatus, CancellationToken ct);

    Task<LoteSeguridadResponse> GuardarAlmacenesDeUsuarioAsync(int idUsuario, AlmacenesDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct);
    Task<LoteSeguridadResponse> GuardarProcesosDeUsuarioAsync(int idUsuario, ProcesosDeUsuarioRequest r, int idUsuarioMovimiento, CancellationToken ct);
    Task<LoteSeguridadResponse> GuardarFechasDeUsuarioAsync(int idUsuario, FechasDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct);

    /// <summary>null = alguna plantilla no es base64 valido (400).</summary>
    Task<SeguridadEscrituraResponse?> GuardarHuellaAsync(GuardarHuellaRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct);

    Task<SeguridadEscrituraResponse> RegistrarContrasenaCapturadaAsync(RegistrarContrasenaCapturadaRequest r, int idUsuario, string equipo, CancellationToken ct);

    Task<List<Dictionary<string, object?>>> ActualizarConstantesAsync(ActualizarConstantesRequest r, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ActualizarFechaOperacionPagosAsync(ActualizarFechaOperacionPagosRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> AvanzarFechaOperacionVentasAsync(int idUsuario, string equipo, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> RegistrarActividadAsync(int idUsuario, string equipo, CancellationToken ct);

    Task<List<Dictionary<string, object?>>> ConsultarParametrosAsync(int id, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> GuardarParametrosAsync(GuardarParametrosRequest r, int idUsuario, string equipo, CancellationToken ct);
}
