using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Reports;

/// <summary>
/// El disco donde el 1003 deja el PDF y el XML del CFDI.
/// </summary>
/// <remarks>
/// LA RUTA NO SE ELIGE AQUI. La dicta `sp_n_rptFacturaElectronica`, que la arma
/// con `Constantes.PathMac` + "Facturas\" + mes-anio del IssueDate + el nombre
/// del documento. Esta clase solo escribe donde le digan: si manana cambian la
/// carpeta en Constantes, los archivos se mueven solos y Mac31 y el web siguen
/// mirando el mismo sitio.
///
/// GUARDAR NO PUEDE TUMBAR LA DESCARGA. En produccion la ruta es un disco DE LA
/// OFICINA (C:\macZ\Facturas\...) que en el servidor no existe, asi que el
/// guardado va a fallar ahi SIEMPRE. Eso no puede impedir que el usuario baje su
/// factura —el contenido ya lo tenemos en memoria—, asi que el fallo se devuelve
/// como motivo y quien llama decide. Es la diferencia entre "no pude dejar la
/// copia" y "no tienes factura".
/// </remarks>
public class FacturaDocumentoAlmacen : IFacturaDocumentoAlmacen
{
    private readonly ILogger<FacturaDocumentoAlmacen> _logger;

    public FacturaDocumentoAlmacen(ILogger<FacturaDocumentoAlmacen> logger)
    {
        _logger = logger;
    }

    public bool Existe(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return false;

        /*
          Un File.Exists sobre una ruta mal formada (un PathMac vacio deja
          "Facturas\10-2026\...") lanza en algunos casos en vez de devolver
          false. Aqui una ruta imposible es simplemente "no esta".
        */
        try
        {
            return File.Exists(ruta);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo comprobar si existe {Ruta}", ruta);
            return false;
        }
    }

    public async Task<byte[]> LeerAsync(string ruta, CancellationToken ct)
        => await File.ReadAllBytesAsync(ruta, ct);

    public async Task<string?> GuardarAsync(string ruta, byte[] contenido, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return "El procedimiento no devolvio ruta para el archivo (revisa Constantes.PathMac).";

        if (contenido.Length == 0)
            return "No habia nada que guardar.";

        try
        {
            /* La carpeta del mes la crea el primer comprobante del mes, igual
               que hace legacy antes de escribir (ReporteV2Service.cs:1166). */
            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrWhiteSpace(carpeta) && !Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);

            /*
              SE ESCRIBE A UN TEMPORAL Y SE MUEVE.

              Legacy escribe directo sobre el destino. Si el proceso muere a
              medias —o si dos peticiones del mismo comprobante caen a la vez,
              que en una API pasa y en Mac31 no— queda un PDF truncado en disco,
              y a partir de ahi `Existe` dice que si y todo el mundo adjunta un
              archivo roto. Con el temporal, el destino solo aparece completo.
            */
            var temporal = ruta + ".parcial";
            await File.WriteAllBytesAsync(temporal, contenido, ct);
            File.Move(temporal, ruta, overwrite: true);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el documento del CFDI en {Ruta}", ruta);
            return ex.Message;
        }
    }
}
