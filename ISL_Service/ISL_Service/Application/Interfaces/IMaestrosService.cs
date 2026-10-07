using ISL_Service.Application.DTOs.Maestros;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Escrituras de cliente, producto y proveedor vistas desde el controlador.
/// <c>null</c> = el renglon no existe (404). <c>idUsuario</c> y <c>equipo</c>
/// vienen del token, nunca del cuerpo.
/// </summary>
public interface IMaestrosService
{
    // Cliente
    Task<MaestroEscrituraResponse> CrearClienteAsync(CrearClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarClienteAsync(int id, ActualizarClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> CambiarFacturamaClienteAsync(int id, CambiarFacturamaClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusClienteAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> GuardarBonificacionesAsync(int idCliente, BonificacionesClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> GuardarCentrosServicioAsync(int idCliente, CentrosServicioClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> CrearClienteFacturacionAsync(CrearClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarClienteFacturacionAsync(int id, ActualizarClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> MarcarFacturacionMostradorAsync(int idCliente, CancellationToken ct);

    // Producto
    Task<MaestroEscrituraResponse> CrearProductoAsync(CrearProductoRequest r, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarProductoAsync(int id, ActualizarProductoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusProductoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> GuardarArchivoProductoAsync(int idProducto, ArchivoProductoRequest r, int idUsuario, string equipo, CancellationToken ct);

    // Proveedor
    Task<MaestroEscrituraResponse> CrearProveedorAsync(CrearProveedorRequest r, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarProveedorAsync(int id, ActualizarProveedorRequest r, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusProveedorAsync(int id, int idStatus, CancellationToken ct);

    Task<MaestroEscrituraResponse> CrearApoderadoAsync(CrearProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarApoderadoAsync(int id, ActualizarProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusApoderadoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    Task<MaestroEscrituraResponse> CrearDomicilioAsync(CrearProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarDomicilioAsync(int id, ActualizarProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    Task<MaestroEscrituraResponse> CrearDocumentoProveedorAsync(CrearProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse> ActualizarDocumentoProveedorAsync(int id, ActualizarProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<MaestroEscrituraResponse?> CambiarStatusDocumentoProveedorAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
}
