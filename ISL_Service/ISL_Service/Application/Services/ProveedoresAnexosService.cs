using ISL_Service.Application.DTOs.ProveedoresAnexos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros de los anexos del proveedor.
///
/// Los tres SP concatenan el filtro de texto en su EXEC(@Sql) y legacy no lo
/// saneaba en la rama de consulta: el apostrofo se quita aqui.
///
/// Ojo con el tamano: los tres declaran `@Sql varchar(4000)`. Con filtros muy
/// largos la cadena se trunca EN SILENCIO y el EXEC corre una consulta
/// incompleta. Por eso el controlador recorta el texto a una longitud sensata en
/// vez de dejar pasar lo que llegue.
/// </summary>
public class ProveedoresAnexosService : IProveedoresAnexosService
{
    /// <summary>
    /// Tope del filtro de texto. Los SP arman `@Sql varchar(4000)` y el resto de
    /// la consulta ya ocupa casi todo; 200 caracteres es mas largo que cualquier
    /// nombre de apoderado o de documento real y deja la cadena a salvo.
    /// </summary>
    private const int MaxTexto = 200;

    private readonly IProveedoresAnexosRepository _repository;

    public ProveedoresAnexosService(IProveedoresAnexosRepository repository)
    {
        _repository = repository;
    }

    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var limpio = valor.Replace("'", string.Empty).Trim();
        return limpio.Length <= MaxTexto ? limpio : limpio[..MaxTexto];
    }

    private static int Bandera(bool? valor) => valor == true ? 1 : 0;

    public Task<List<ProveedorApoderadoDto>> ConsultarApoderadosAsync(
        int id, int idProveedor, int idStatus, string? texto,
        bool? esRepresentanteLegal, bool? esRepresentanteTecnico, bool exacto, CancellationToken ct)
        => _repository.ConsultarApoderadosAsync(
            id, idProveedor, idStatus, Texto(texto),
            Bandera(esRepresentanteLegal), Bandera(esRepresentanteTecnico), exacto, ct);

    public Task<List<ProveedorDomicilioDto>> ConsultarDomiciliosAsync(
        int id, int idProveedor, int idTipoDomicilio, int idStatus, string? texto, bool exacto, CancellationToken ct)
        => _repository.ConsultarDomiciliosAsync(id, idProveedor, idTipoDomicilio, idStatus, Texto(texto), exacto, ct);

    public Task<List<ProveedorDocumentoDto>> ConsultarDocumentosAsync(
        int id, int idProveedor, int idStatus, string? texto, bool exacto, CancellationToken ct)
        => _repository.ConsultarDocumentosAsync(id, idProveedor, idStatus, Texto(texto), exacto, ct);
}
