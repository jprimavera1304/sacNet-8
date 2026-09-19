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
    /*
      Las banderas son las de Mac31 (ConsultarVentas.cs:4213 y :4221) y llegan
      desde el pase firmado. En Zaragoza se ignoran, porque alla legacy tampoco
      las manda: ver VentasImpresionReglas.Banderas.
    */
    Task<RemisionPdf> GenerarAsync(
        IReadOnlyList<int> idsVenta,
        int idUsuarioImpresion,
        int primerImpresion,
        int reimpresion,
        string equipoImpresion,
        CancellationToken ct);
}
