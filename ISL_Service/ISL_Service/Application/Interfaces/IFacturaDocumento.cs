namespace ISL_Service.Application.Interfaces;

/*
  EL REPORTE 1003, QUE NO ES UN REPORTE: ES EL ARCHIVO DEL CFDI

  QUE ES EL 1003 EN LEGACY. `enumReportes.FacturaElectronica = 1003`
  (Legacy/Mac31/Mac31/Utils/enumReportes.cs:288, "Impresion de Factura
  Electronica"). Lo disparan seis pantallas de Mac31 (Ventas.cs:6281,
  VentasFacturacion.cs:1886, VentasNotaCredito.cs:3098,
  VentasComplementoPago.cs:935, VentasFacturacionCancelacion.cs:393,
  ConsultarVentasFacturas.cs:1723).

  LO QUE HACE DE VERDAD, MEDIDO. Su rama en
  Legacy/MacServicios2/.../ReportesV2Serv/ReporteV2Service.cs:1132-1214 NO
  arma ni una linea de HTML. Lo que hace es:

      1. leer `sp_n_rptFacturaElectronica` para saber DONDE va el archivo,
      2. si no esta en disco, bajar de Facturapi el zip, el xml y el pdf
         (`FacturapiService.downloadFactura`, FacturapiService.cs:2042: los tres
         con `DownloadZipAsync` / `DownloadXmlAsync` / `DownloadPdfAsync`),
      3. ESCRIBIRLOS en esa ruta, creando la carpeta del mes si falta,
      4. devolver la ruta del que se pidio segun `Presentacion`
         (2 = pdf, 4 = xml, 5 = zip).

  O sea que el 1003 es un DESCARGADOR CON CACHE EN DISCO, y por eso no tiene
  plantilla que copiar de MacReportes2. Comprobado tambien por el lado de la
  base: `Reportes` tiene la fila 1003 ("Factura", rptTemplate,
  sp_n_rptFacturaElectronica) en MacZ y en Produccion_svr, pero `Template_Html`
  solo tiene TRES plantillas del tipo 1003 (`detalle`, `totales`, `cuerpo_n`)
  frente a las 32 del 1001 y las 10 del 1175: no hay encabezado ni pie, y las
  62 almohadillas de `cuerpo_n` no las llena ningun procedimiento porque
  `sp_n_rptFacturaElectronica` no devuelve ni emisor ni receptor ni conceptos
  —devuelve IssueDate, FolioFtm, Presentacion, Archivo, FacturaArchivo,
  IDFacturapi y los nueve campos del correo, y nada mas.

  POR ESO NO ENTRA EN ReportesCatalogo y por eso esto vive aqui, al lado de la
  facturacion, y no en el canal de reportes: el papel del CFDI es el PDF que
  SELLA Facturapi, no un dibujo nuestro. Dibujarlo seria inventar un
  comprobante fiscal distinto del que tiene el SAT.

  LO QUE FALTABA EN EL BACKEND. `GET /api/ventas/facturas/{id}/documento` ya
  bajaba de Facturapi cuando el disco no tenia el archivo, pero NO guardaba lo
  bajado: el paso 3 del 1003 no existia. Consecuencia medida: un comprobante
  timbrado desde el web o el movil se timbra bien y nunca deja archivo en
  disco, asi que `POST /api/ventas/facturas/enviar` —que adjunta DESDE DISCO,
  igual que legacy (VentasFacturaService.cs:956-980, donde la descarga esta
  comentada)— contestaba 409 para siempre.
*/

/// <summary>Uno de los archivos del comprobante, ya resuelto en disco.</summary>
public class ArchivoDeComprobante
{
    /// <summary>"pdf" o "xml".</summary>
    public string Formato { get; set; } = string.Empty;

    /// <summary>La ruta que dicta <c>sp_n_rptFacturaElectronica</c>.</summary>
    public string Ruta { get; set; } = string.Empty;

    public long Bytes { get; set; }

    /// <summary>"disco" si ya estaba, "facturapi" si hubo que bajarlo.</summary>
    public string Origen { get; set; } = string.Empty;

    /// <summary>
    /// Si quedo escrito en disco. Un false con <c>Origen = "facturapi"</c>
    /// significa que el archivo SE ENTREGO pero no se pudo guardar (carpeta de
    /// la oficina que no existe en el servidor, disco de solo lectura...), que
    /// es un aviso y no un fallo: lo que se pidio salio.
    /// </summary>
    public bool EnDisco { get; set; }

    /// <summary>El motivo, cuando <see cref="EnDisco"/> es false.</summary>
    public string? Aviso { get; set; }
}

/// <summary>El resultado de generar los archivos de un comprobante (el 1003).</summary>
public class ComprobanteGenerado
{
    public int IdVentaFactura { get; set; }

    public string IdFacturapi { get; set; } = string.Empty;

    /// <summary>
    /// Si el comprobante esta cancelado.
    ///
    /// NO sale de una columna: sale del NOMBRE del archivo. El procedimiento
    /// mete la palabra en la ruta (<c>CASE WHEN IDStatus = 2 THEN 'CANCELADA'</c>)
    /// y no devuelve el estado por separado, asi que es el unico dato de
    /// cancelacion que da esta lectura. Importa decirlo porque la ruta cambia:
    /// al cancelar, el PDF viejo ("Factura 131754 29168 .pdf") se queda
    /// huerfano y el nuevo se llama "Factura 131754 29168 CANCELADA.pdf" —las
    /// dos parejas estan en C:\mac21\Facturas\11-2025 de esta maquina—. Quien
    /// cancele tiene que volver a generar, y para eso esta `rehacer`.
    /// </summary>
    public bool Cancelada { get; set; }

    public List<ArchivoDeComprobante> Archivos { get; set; } = new();
}

/// <summary>
/// El 1003: deja en disco el PDF y el XML de un comprobante ya timbrado.
/// </summary>
public interface IFacturaDocumentoService
{
    /// <summary>
    /// Asegura los dos archivos del comprobante y dice como quedaron.
    /// </summary>
    /// <param name="rehacer">
    /// true vuelve a bajar de Facturapi aunque el archivo ya este en disco. Es
    /// para despues de cancelar (la ruta cambia de nombre) o cuando lo que hay
    /// en disco quedo a medias.
    /// </param>
    Task<ComprobanteGenerado> GenerarAsync(int idVentaFactura, bool rehacer, CancellationToken ct);

    /// <summary>
    /// El PDF o el XML listo para entregar, dejandolo en disco por el camino.
    /// </summary>
    Task<DocumentoDeFactura> LeerAsync(int idVentaFactura, string formato, CancellationToken ct);
}

/// <summary>
/// Donde se guardan los archivos del comprobante. Es una interfaz y no
/// <c>File</c> a pelo para que las pruebas puedan comprobar QUE se escribe sin
/// tocar el disco de nadie.
/// </summary>
public interface IFacturaDocumentoAlmacen
{
    bool Existe(string ruta);

    Task<byte[]> LeerAsync(string ruta, CancellationToken ct);

    /// <summary>
    /// Escribe el archivo creando la carpeta del mes si falta (igual que
    /// ReporteV2Service.cs:1166-1167). Devuelve el motivo si no se pudo, y
    /// <c>null</c> si quedo guardado: NO lanza, porque no poder guardar la
    /// copia no es razon para no entregar el documento que se pidio.
    /// </summary>
    Task<string?> GuardarAsync(string ruta, byte[] contenido, CancellationToken ct);
}
