using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de las tres fichas maestras.
///
/// Delgada: traduce el par result/mensaje de legacy y nada mas. Las validaciones
/// de forma estan en el controlador y las reglas de negocio (el RFC repetido, el
/// codigo ya usado) viven en los sp_n_ y llegan como el texto que la persona
/// tiene que leer, con las mismas palabras que ve en Mac31.
/// </summary>
public class MaestrosService : IMaestrosService
{
    private readonly IMaestrosRepository _repo;

    public MaestrosService(IMaestrosRepository repo)
    {
        _repo = repo;
    }

    private static MaestroEscrituraResponse Traducir(ResultadoLegacy r, int id = 0)
        => new() { Guardo = r.Ok, Mensaje = r.Mensaje, Id = id };

    private static MaestroEscrituraResponse Traducir((ResultadoLegacy Resultado, int Id) x)
        => Traducir(x.Resultado, x.Id);

    /// <summary>
    /// Para las bajas: null se queda null porque significa "ese renglon no
    /// existe", y eso es un 404.
    /// </summary>
    private static MaestroEscrituraResponse? TraducirOpcional(ResultadoLegacy? r)
        => r is null ? null : Traducir(r);

    // --- Cliente

    public async Task<MaestroEscrituraResponse> CrearClienteAsync(CrearClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarClienteAsync(r, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> ActualizarClienteAsync(int id, ActualizarClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarClienteAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse> CambiarFacturamaClienteAsync(int id, CambiarFacturamaClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.CambiarFacturamaClienteAsync(id, r.IdFacturama, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusClienteAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusClienteAsync(id, idStatus, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> GuardarBonificacionesAsync(int idCliente, BonificacionesClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.GuardarBonificacionesAsync(idCliente, r, idUsuario, equipo, ct), idCliente);

    public async Task<MaestroEscrituraResponse> GuardarCentrosServicioAsync(int idCliente, CentrosServicioClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.GuardarCentrosServicioAsync(idCliente, r, idUsuario, equipo, ct), idCliente);

    public async Task<MaestroEscrituraResponse> CrearClienteFacturacionAsync(CrearClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarClienteFacturacionAsync(r, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> ActualizarClienteFacturacionAsync(int id, ActualizarClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarClienteFacturacionAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse> MarcarFacturacionMostradorAsync(int idCliente, CancellationToken ct)
        => Traducir(await _repo.MarcarFacturacionMostradorAsync(idCliente, ct), idCliente);

    // --- Producto

    public async Task<MaestroEscrituraResponse> CrearProductoAsync(CrearProductoRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarProductoAsync(r, ct));

    public async Task<MaestroEscrituraResponse> ActualizarProductoAsync(int id, ActualizarProductoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarProductoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusProductoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusProductoAsync(id, idStatus, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse?> GuardarArchivoProductoAsync(int idProducto, ArchivoProductoRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var x = await _repo.GuardarArchivoProductoAsync(idProducto, r, idUsuario, equipo, ct);
        return x is null ? null : Traducir(x.Value.Resultado, x.Value.IdArchivo);
    }

    // --- Proveedor

    public async Task<MaestroEscrituraResponse> CrearProveedorAsync(CrearProveedorRequest r, CancellationToken ct)
        => Traducir(await _repo.InsertarProveedorAsync(r, ct));

    public async Task<MaestroEscrituraResponse> ActualizarProveedorAsync(int id, ActualizarProveedorRequest r, CancellationToken ct)
        => Traducir(await _repo.ActualizarProveedorAsync(id, r, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusProveedorAsync(int id, int idStatus, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusProveedorAsync(id, idStatus, ct));

    public async Task<MaestroEscrituraResponse> CrearApoderadoAsync(CrearProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarApoderadoAsync(r, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> ActualizarApoderadoAsync(int id, ActualizarProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarApoderadoAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusApoderadoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusApoderadoAsync(id, idStatus, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> CrearDomicilioAsync(CrearProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarDomicilioAsync(r, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> ActualizarDomicilioAsync(int id, ActualizarProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarDomicilioAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusDomicilioAsync(id, idStatus, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> CrearDocumentoProveedorAsync(CrearProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.InsertarDocumentoProveedorAsync(r, idUsuario, equipo, ct));

    public async Task<MaestroEscrituraResponse> ActualizarDocumentoProveedorAsync(int id, ActualizarProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => Traducir(await _repo.ActualizarDocumentoProveedorAsync(id, r, idUsuario, equipo, ct), id);

    public async Task<MaestroEscrituraResponse?> CambiarStatusDocumentoProveedorAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => TraducirOpcional(await _repo.CambiarStatusDocumentoProveedorAsync(id, idStatus, idUsuario, equipo, ct));
}
