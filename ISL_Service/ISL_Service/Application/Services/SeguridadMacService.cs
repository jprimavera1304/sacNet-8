using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.SeguridadMac;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de usuarios de Mac31, huellas, constantes y parametros.
///
/// Toma UNA decision propia: decodificar las plantillas de huella que llegan en
/// base64. Si el texto no es base64 valido devuelve null y el controlador contesta
/// 400, en vez de mandar bytes basura a la base: una plantilla invalida se guarda
/// sin protestar y el error aparece semanas despues, cuando alguien no puede
/// checar.
/// </summary>
public class SeguridadMacService : ISeguridadMacService
{
    private readonly ISeguridadMacRepository _repo;

    public SeguridadMacService(ISeguridadMacRepository repo)
    {
        _repo = repo;
    }

    private static SeguridadEscrituraResponse Traducir(ResultadoLegacy r, int id = 0)
        => new() { Guardo = r.Ok, Mensaje = r.Mensaje, Id = id };

    // --- Usuario de Mac31

    public async Task<SeguridadEscrituraResponse> CrearUsuarioMacAsync(CrearUsuarioMacRequest r, CancellationToken ct)
    {
        var (resultado, id) = await _repo.InsertarUsuarioMacAsync(r, ct);
        return Traducir(resultado, id);
    }

    public async Task<SeguridadEscrituraResponse> ActualizarUsuarioMacAsync(int id, ActualizarUsuarioMacRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarUsuarioMacAsync(id, r, ct), id);

    public async Task<SeguridadEscrituraResponse?> CambiarStatusUsuarioMacAsync(int id, int idStatus, CancellationToken ct)
    {
        var r = await _repo.CambiarStatusUsuarioMacAsync(id, idStatus, ct);
        return r is null ? null : Traducir(r, id);
    }

    public Task<LoteSeguridadResponse> GuardarAlmacenesDeUsuarioAsync(
        int idUsuario, AlmacenesDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct)
        => _repo.GuardarAlmacenesDeUsuarioAsync(idUsuario, r, idUsuarioMovimiento, equipo, ct);

    public Task<LoteSeguridadResponse> GuardarProcesosDeUsuarioAsync(
        int idUsuario, ProcesosDeUsuarioRequest r, int idUsuarioMovimiento, CancellationToken ct)
        => _repo.GuardarProcesosDeUsuarioAsync(idUsuario, r, idUsuarioMovimiento, ct);

    public Task<LoteSeguridadResponse> GuardarFechasDeUsuarioAsync(
        int idUsuario, FechasDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct)
        => _repo.GuardarFechasDeUsuarioAsync(idUsuario, r, idUsuarioMovimiento, equipo, ct);

    // --- Huellas

    /// <summary>
    /// Guarda la huella. Devuelve null cuando alguna de las dos plantillas no es
    /// base64 valido.
    /// </summary>
    public async Task<SeguridadEscrituraResponse?> GuardarHuellaAsync(
        GuardarHuellaRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct)
    {
        var huella = Decodificar(r.Huella);
        if (huella is null || huella.Length == 0) return null;

        byte[]? huella2 = null;
        if (!string.IsNullOrWhiteSpace(r.Huella2))
        {
            huella2 = Decodificar(r.Huella2);
            if (huella2 is null) return null;
        }

        return Traducir(await _repo.GuardarHuellaAsync(r, huella, huella2, idUsuarioMovimiento, equipo, ct));
    }

    /// <summary>Base64 -> bytes. null cuando el texto no es base64 valido.</summary>
    private static byte[]? Decodificar(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return Array.Empty<byte>();
        try
        {
            return Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // --- Bitacora de contrasenas

    public async Task<SeguridadEscrituraResponse> RegistrarContrasenaCapturadaAsync(
        RegistrarContrasenaCapturadaRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var (resultado, id) = await _repo.RegistrarContrasenaCapturadaAsync(r, idUsuario, equipo, ct);
        return Traducir(resultado, id);
    }

    // --- Constantes

    public Task<List<Dictionary<string, object?>>> ActualizarConstantesAsync(ActualizarConstantesRequest r, CancellationToken ct)
        => _repo.ActualizarConstantesAsync(r, ct);

    public Task<List<Dictionary<string, object?>>> ActualizarFechaOperacionPagosAsync(
        ActualizarFechaOperacionPagosRequest r, int idUsuario, string equipo, CancellationToken ct)
        => _repo.ActualizarFechaOperacionPagosAsync(r, idUsuario, equipo, ct);

    public Task<List<Dictionary<string, object?>>> AvanzarFechaOperacionVentasAsync(
        int idUsuario, string equipo, CancellationToken ct)
        => _repo.AvanzarFechaOperacionVentasAsync(idUsuario, equipo, ct);

    public Task<List<Dictionary<string, object?>>> RegistrarActividadAsync(int idUsuario, string equipo, CancellationToken ct)
        => _repo.RegistrarActividadAsync(idUsuario, equipo, ct);

    // --- Parametros

    public Task<List<Dictionary<string, object?>>> ConsultarParametrosAsync(int id, CancellationToken ct)
        => _repo.ConsultarParametrosAsync(id, ct);

    public Task<List<Dictionary<string, object?>>> GuardarParametrosAsync(
        GuardarParametrosRequest r, int idUsuario, string equipo, CancellationToken ct)
        => _repo.GuardarParametrosAsync(r, idUsuario, equipo, ct);
}
