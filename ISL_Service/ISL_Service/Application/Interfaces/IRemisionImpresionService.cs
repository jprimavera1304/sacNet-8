namespace ISL_Service.Application.Interfaces;

public sealed class RemisionPdf
{
    public byte[] Contenido { get; init; } = Array.Empty<byte>();

    /// Con el mismo nombre que le pone MacReportes al descargarla, para que al
    /// usuario no le cambie el archivo de nombre por haberlo bajado del web.
    public string NombreArchivo { get; init; } = "remision.pdf";
}

public interface IRemisionImpresionService
{
    Task<RemisionPdf> GenerarAsync(IReadOnlyList<int> idsVenta, int idUsuarioImpresion, CancellationToken ct);
}
