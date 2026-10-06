using ISL_Service.Application.DTOs.ProveedoresAnexos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lectura de los anexos del proveedor: apoderados, domicilios y documentos.</summary>
public interface IProveedoresAnexosRepository
{
    Task<List<ProveedorApoderadoDto>> ConsultarApoderadosAsync(
        int id, int idProveedor, int idStatus, string texto,
        int esRepresentanteLegal, int esRepresentanteTecnico, bool exacto, CancellationToken ct);

    Task<List<ProveedorDomicilioDto>> ConsultarDomiciliosAsync(
        int id, int idProveedor, int idTipoDomicilio, int idStatus, string texto, bool exacto, CancellationToken ct);

    Task<List<ProveedorDocumentoDto>> ConsultarDocumentosAsync(
        int id, int idProveedor, int idStatus, string texto, bool exacto, CancellationToken ct);
}

/// <summary>Normaliza los filtros de los anexos del proveedor.</summary>
public interface IProveedoresAnexosService
{
    Task<List<ProveedorApoderadoDto>> ConsultarApoderadosAsync(
        int id, int idProveedor, int idStatus, string? texto,
        bool? esRepresentanteLegal, bool? esRepresentanteTecnico, bool exacto, CancellationToken ct);

    Task<List<ProveedorDomicilioDto>> ConsultarDomiciliosAsync(
        int id, int idProveedor, int idTipoDomicilio, int idStatus, string? texto, bool exacto, CancellationToken ct);

    Task<List<ProveedorDocumentoDto>> ConsultarDocumentosAsync(
        int id, int idProveedor, int idStatus, string? texto, bool exacto, CancellationToken ct);
}
