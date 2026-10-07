using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Maestros;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Escrituras de las tres fichas maestras de Mac31: cliente, producto y
/// proveedor. <c>null</c> en los cambios de estatus significa "ese renglon no
/// existe": el controlador contesta 404.
/// </summary>
public interface IMaestrosRepository
{
    // --- Cliente
    Task<(ResultadoLegacy Resultado, int Id)> InsertarClienteAsync(CrearClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarClienteAsync(int id, ActualizarClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> CambiarFacturamaClienteAsync(int id, string? idFacturama, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusClienteAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> GuardarBonificacionesAsync(int idCliente, BonificacionesClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> GuardarCentrosServicioAsync(int idCliente, CentrosServicioClienteRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> InsertarClienteFacturacionAsync(CrearClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarClienteFacturacionAsync(int id, ActualizarClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> MarcarFacturacionMostradorAsync(int idCliente, CancellationToken ct);

    // --- Producto
    Task<(ResultadoLegacy Resultado, int Id)> InsertarProductoAsync(CrearProductoRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarProductoAsync(int id, ActualizarProductoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusProductoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>
    /// Adjunta el archivo. Devuelve null cuando el producto no existe: el precio
    /// actual se lee de ahi y sin renglon no hay precio que conservar.
    /// </summary>
    Task<(ResultadoLegacy Resultado, int IdArchivo)?> GuardarArchivoProductoAsync(int idProducto, ArchivoProductoRequest r, int idUsuario, string equipo, CancellationToken ct);

    // --- Proveedor
    Task<(ResultadoLegacy Resultado, int Id)> InsertarProveedorAsync(CrearProveedorRequest r, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarProveedorAsync(int id, ActualizarProveedorRequest r, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusProveedorAsync(int id, int idStatus, CancellationToken ct);

    Task<(ResultadoLegacy Resultado, int Id)> InsertarApoderadoAsync(CrearProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarApoderadoAsync(int id, ActualizarProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusApoderadoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    Task<(ResultadoLegacy Resultado, int Id)> InsertarDomicilioAsync(CrearProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarDomicilioAsync(int id, ActualizarProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);

    Task<(ResultadoLegacy Resultado, int Id)> InsertarDocumentoProveedorAsync(CrearProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy> ActualizarDocumentoProveedorAsync(int id, ActualizarProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacy?> CambiarStatusDocumentoProveedorAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct);
}
