using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Facturacion;

namespace ISL_Service.Application.Services;

/*
  EL 1003 EN CASA: DEJAR EN DISCO EL PDF Y EL XML DEL CFDI

  El porque de que esto no sea una plantilla esta en IFacturaDocumento.cs. Aqui
  esta el COMO, que es el mismo camino de legacy en el mismo orden
  (Legacy/MacServicios2/.../ReporteV2Service.cs:1132-1214):

      1. sp_n_rptFacturaElectronica -> la ruta y el IDFacturapi
      2. el candado del key (FacturapiSecretKey tiene que llevar el AppMac)
      3. si falta en disco, bajar de Facturapi
      4. escribirlo en esa ruta, creando la carpeta del mes

  TRES DIFERENCIAS CON LEGACY, Y SE DICEN:

  a) NO hay Thread.Sleep(4000). Legacy, cuando no encuentra el PDF, duerme
     cuatro segundos a ver si aparece (VentasFacturaService.cs:956). En una API
     eso es un hilo bloqueado por peticion apostando a que otro proceso acabe;
     aqui, si falta, se baja —que es la solucion de verdad— y si no se puede, se
     dice por que.

  b) NO se baja el .zip. Legacy guarda zip + xml + pdf. El zip no es un
     documento: es el pdf y el xml juntos, y nadie lo pide —ni
     `/documento?formato=`, que solo acepta pdf y xml, ni el correo, que adjunta
     los dos sueltos—. Para bajarlo haria falta una linea en
     `FacturapiCliente.DescargarAsync`, que es de otro agente; esta anotado en
     el informe. Mientras no exista, el unico que echa de menos el zip es Mac31
     cuando le piden Presentacion = 5, y Mac31 lo genera por su cuenta.

  c) Si no se puede ESCRIBIR, se entrega igual. Ver FacturaDocumentoAlmacen.
*/
public class FacturaDocumentoService : IFacturaDocumentoService
{
    private readonly IVentasFacturacionRepository _repository;
    private readonly FacturapiCliente _facturapi;
    private readonly IFacturaDocumentoAlmacen _almacen;
    private readonly ILogger<FacturaDocumentoService> _logger;

    public FacturaDocumentoService(
        IVentasFacturacionRepository repository,
        FacturapiCliente facturapi,
        IFacturaDocumentoAlmacen almacen,
        ILogger<FacturaDocumentoService> logger)
    {
        _repository = repository;
        _facturapi = facturapi;
        _almacen = almacen;
        _logger = logger;
    }

    /// <summary>Los dos formatos que son un documento. El zip no lo es (ver arriba).</summary>
    private static readonly string[] Formatos = { "pdf", "xml" };

    public async Task<ComprobanteGenerado> GenerarAsync(
        int idVentaFactura, bool rehacer, CancellationToken ct)
    {
        var (datos, rutas) = await ResolverAsync(idVentaFactura, ct);

        var resultado = new ComprobanteGenerado
        {
            IdVentaFactura = idVentaFactura,
            IdFacturapi = datos.IdFacturapi,
            Cancelada = EsCancelada(rutas["pdf"])
        };

        /*
          EL SECRETO SE PIDE UNA VEZ PARA LOS DOS ARCHIVOS, y solo si hace falta
          bajar alguno: generar un comprobante que ya esta completo en disco no
          tiene por que leer la llave de la empresa.
        */
        string? secreto = null;

        foreach (var formato in Formatos)
        {
            var ruta = rutas[formato];

            if (!rehacer && _almacen.Existe(ruta))
            {
                resultado.Archivos.Add(new ArchivoDeComprobante
                {
                    Formato = formato,
                    Ruta = ruta,
                    Bytes = new FileInfo(ruta).Length,
                    Origen = "disco",
                    EnDisco = true
                });
                continue;
            }

            secreto ??= await SecretoAsync(ct);
            var contenido = await BajarAsync(secreto, datos.IdFacturapi, formato, ruta, ct);
            var aviso = await _almacen.GuardarAsync(ruta, contenido, ct);

            resultado.Archivos.Add(new ArchivoDeComprobante
            {
                Formato = formato,
                Ruta = ruta,
                Bytes = contenido.Length,
                Origen = "facturapi",
                EnDisco = aviso is null,
                Aviso = aviso
            });
        }

        _logger.LogInformation(
            "Documentos del CFDI {Id} ({Facturapi}): {Resumen}",
            idVentaFactura,
            resultado.IdFacturapi,
            string.Join(", ", resultado.Archivos.Select(x => $"{x.Formato}={x.Origen}/{x.Bytes}b")));

        return resultado;
    }

    public async Task<DocumentoDeFactura> LeerAsync(
        int idVentaFactura, string formato, CancellationToken ct)
    {
        var tipo = (formato ?? string.Empty).Trim().ToLowerInvariant();
        if (!Formatos.Contains(tipo))
            throw new ArgumentException("El formato tiene que ser pdf o xml.");

        var (datos, rutas) = await ResolverAsync(idVentaFactura, ct);
        var ruta = rutas[tipo];

        /*
          PRIMERO EL DISCO. Cuando la API corre en la oficina el archivo SI esta,
          y leerlo es gratis frente a una llamada por internet. Es lo que ya
          hacia el endpoint; lo que faltaba era el else, que ahora GUARDA.
        */
        var deDisco = _almacen.Existe(ruta);

        byte[] contenido;
        if (deDisco)
        {
            contenido = await _almacen.LeerAsync(ruta, ct);
        }
        else
        {
            var secreto = await SecretoAsync(ct);
            contenido = await BajarAsync(secreto, datos.IdFacturapi, tipo, ruta, ct);

            /* La copia en disco es el 1003: que la SIGUIENTE lectura y, sobre
               todo, el correo —que adjunta desde disco— ya lo encuentren. */
            await _almacen.GuardarAsync(ruta, contenido, ct);
        }

        return new DocumentoDeFactura
        {
            Contenido = contenido,
            /* Si la ruta venia vacia, Path.GetFileName devuelve vacio y el
               navegador bautizaria el archivo "descarga". */
            NombreArchivo = string.IsNullOrWhiteSpace(Path.GetFileName(ruta))
                ? $"{datos.IdFacturapi}.{tipo}"
                : Path.GetFileName(ruta),
            TipoContenido = tipo == "pdf" ? "application/pdf" : "application/xml"
        };
    }

    /* ---------------------------------------------------------------- */

    /// <summary>
    /// Lee el 1003 y resuelve las dos rutas.
    /// </summary>
    /// <remarks>
    /// EL PROCEDIMIENTO DEVUELVE DOS COLUMNAS DE RUTA Y SE USA LA SEGUNDA.
    /// `Archivo` trae ya una extension elegida con el parametro `Presentacion`,
    /// que desde aqui va siempre en 0 y por tanto dice ".zip";
    /// `FacturaArchivo` trae el mismo nombre TERMINADO EN PUNTO, sin extension
    /// —"C:\macZ\Facturas\10-2026\Factura 0 80660 ."—, que es exactamente lo
    /// que legacy usa para pegarle "pdf" o "xml" (ReporteV2Service.cs:1136).
    /// Partir de esa evita el `Replace(".zip", ".pdf")` del otro camino, que es
    /// una suposicion sobre la extension que el SP decidio.
    /// </remarks>
    private async Task<(CorreoFactura Datos, Dictionary<string, string> Rutas)>
        ResolverAsync(int idVentaFactura, CancellationToken ct)
    {
        if (idVentaFactura <= 0)
            throw new ArgumentException("La factura es obligatoria.");

        var datos = await _repository.LeerDatosCorreoAsync(idVentaFactura, ct)
            ?? throw new NotFoundException("No se encontro la factura.");

        /*
          EL PROCEDIMIENTO NO SABE DECIR "NO EXISTE", Y HAY QUE DISTINGUIRLO.

          `sp_n_rptFacturaElectronica` SIEMPRE devuelve un renglon, porque los
          nueve campos del correo salen de `Constantes` y no de la factura.
          Medido con '@99999999' en Produccion_svr: IssueDate, FolioFtm, Archivo
          e IDFacturapi vienen NULL y el correo de la empresa viene completo. O
          sea que "hay renglon" no significa "hay factura".

          Las dos situaciones se separan porque mandan a mirar sitios distintos:

          - sin ruta Y sin IDFacturapi -> ese IDVentaFactura NO EXISTE (404).
          - con ruta y sin IDFacturapi -> la factura existe pero NO SE TIMBRO,
            asi que no tiene UUID ni CFDI y no hay PDF que Facturapi pueda dar
            (409). Es tambien el caso de los renglones anteriores a Facturapi.

          Sin esta separacion las dos contestaban lo mismo —un 409 hablando de
          una ruta de disco—, que manda a revisar carpetas cuando lo que falta
          es la factura.
        */
        var pdf = datos.Archivo;
        var sinRuta = string.IsNullOrWhiteSpace(pdf);
        var sinFolio = string.IsNullOrWhiteSpace(datos.IdFacturapi);

        if (sinRuta && sinFolio)
            throw new NotFoundException($"No existe el comprobante {idVentaFactura}.");

        if (sinFolio)
            throw new ConflictException(
                $"El comprobante {idVentaFactura} no esta timbrado: no tiene folio de Facturapi "
                + "ni UUID, asi que no hay PDF ni XML que entregar.");

        if (sinRuta)
            throw new ConflictException(
                $"El comprobante {idVentaFactura} esta timbrado pero el procedimiento no devolvio "
                + "la ruta del archivo. Suele ser Constantes.PathMac vacio en esta base.");

        /* El XML es el mismo nombre con otra extension: es la unica regla de
           extensiones del modulo y vive en un solo sitio. */
        var xml = pdf.Replace(".pdf", ".xml", StringComparison.OrdinalIgnoreCase);

        return (datos, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pdf"] = pdf,
            ["xml"] = xml
        });
    }

    private async Task<string> SecretoAsync(CancellationToken ct)
    {
        var (secretKey, appMac) = await _repository.LeerLlaveFacturapiAsync(ct);

        /*
          EL MISMO CANDADO QUE AL TIMBRAR, y por la misma razon: el key solo
          sirve si lleva dentro el nombre de la aplicacion de ESTA empresa
          (ReglasFacturacion.SecretoReal). Aqui solo se BAJA un comprobante ya
          emitido, pero si el candado no cuadra el mensaje tiene que ser el que
          ya conoce quien factura, no uno nuevo.
        */
        return ReglasFacturacion.SecretoReal(secretKey, appMac)
            ?? throw new ConflictException(
                "NO SE PUEDE OBTENER EL COMPROBANTE." + Environment.NewLine
                + "Esta base no tiene configurada la llave de Facturapi (o no coincide con "
                + "la aplicacion), asi que no se puede bajar el archivo de Facturapi.");
    }

    private async Task<byte[]> BajarAsync(
        string secreto, string idFacturapi, string tipo, string ruta, CancellationToken ct)
    {
        var (contenido, error) = await _facturapi.DescargarAsync(secreto, idFacturapi, tipo, ct);

        if (contenido is null || contenido.Length == 0)
            throw new ConflictException(
                $"NO SE PUDO OBTENER EL {tipo.ToUpperInvariant()}.{Environment.NewLine}"
                + $"No esta en disco ({ruta}) y Facturapi contesto: {error}");

        return contenido;
    }

    /// <summary>
    /// Si la ruta que armo el procedimiento dice que el comprobante esta
    /// cancelado. Ver el porque en <see cref="ComprobanteGenerado.Cancelada"/>.
    /// </summary>
    private static bool EsCancelada(string ruta)
        => Path.GetFileNameWithoutExtension(ruta)
            .TrimEnd('.', ' ')
            .EndsWith("CANCELADA", StringComparison.OrdinalIgnoreCase);
}
