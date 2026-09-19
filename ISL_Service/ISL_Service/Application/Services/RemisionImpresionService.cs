using System.Text.RegularExpressions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Reports;

namespace ISL_Service.Application.Services;

/*
  EL REPORTE DE REMISION, AHORA EN CASA

  Antes este papel lo pintaba MacReportes: el web dejaba parametros en una
  tabla, abria una direccion de ese servidor viejo, ese servidor le pedia el
  html a MacServicios2 y lo convertia a PDF. Tres saltos y dos aplicaciones
  legacy para entregar una hoja.

  Ahora se arma aqui, pero con las MISMAS piezas: las mismas plantillas de
  Template_Html y los mismos sp_n_. Eso es lo que garantiza que el papel salga
  igual y siga igual: si manana ajustan una plantilla, el web se entera solo.

  El unico pedazo que no se reusa es el motor de PDF —alla NReco, aca
  wkhtmltopdf—, pero por dentro los dos son wkhtmltopdf y se le pasan los
  mismos margenes y la misma orientacion que usa MacReportes para la remision.
*/
public class RemisionImpresionService : IRemisionImpresionService
{
    /*
      MacReportes imprime la remision PARADA y con estos margenes
      (ReportesController.MostrarPdf en Legacy/MacReportes2). Se repiten tal
      cual: son los que hacen que el corte de hoja caiga donde debe.
    */
    private const string Orientacion = "portrait";

    /// Pantalla = 1 copia. Las copias son cosa de la impresora de mostrador.
    private const int Copias = 1;

    private readonly IRemisionImpresionRepository _repository;

    public RemisionImpresionService(IRemisionImpresionRepository repository)
    {
        _repository = repository;
    }

    public async Task<RemisionPdf> GenerarAsync(IReadOnlyList<int> idsVenta, int idUsuarioImpresion, CancellationToken ct)
    {
        if (idsVenta.Count == 0)
            throw new InvalidOperationException("No se indico ninguna remision.");

        /*
          ZARAGOZA IMPRIME OTRO PAPEL, NO ESTE CON OTRO LOGO.

          Es el mismo desvio que hace MacServicios2 (ReporteV2Service ~813):
          cuando le piden la remision y la empresa es Zaragoza, se va por la
          "Remision Zaragoza Generico". Sin esto el web de Zaragoza entregaba la
          remision de Tauro, que no se parece a la que sale de Mac31.
        */
        var funcionalidad = await _repository.ConsultarFuncionalidadAsync(ct);
        if (funcionalidad.ToUpperInvariant().Contains("ZARA"))
            return await GenerarZaragozaAsync(idsVenta, idUsuarioImpresion, ct);

        var plantillas = await _repository.ConsultarPlantillasAsync(ct);

        var ventas = new List<RemisionDatosVenta>();
        var rechazadas = new List<string>();

        foreach (var idVenta in idsVenta)
        {
            var resultado = await _repository.ConsultarDatosVentaAsync(idVenta, idUsuarioImpresion, 0, ct);

            if (resultado.Datos is null)
            {
                rechazadas.Add(resultado.Mensaje);
                continue;
            }

            ventas.Add(resultado.Datos);
        }

        /*
          Si NINGUNA se pudo imprimir hay que decir por que, no entregar un PDF
          en blanco. Si algunas si, se entregan esas: es lo que hace legacy.
        */
        if (ventas.Count == 0)
        {
            throw new InvalidOperationException(rechazadas.Count > 0
                ? string.Join(" ", rechazadas)
                : "No se pudo obtener la informacion de las remisiones.");
        }

        var html = RemisionHtmlBuilder.Construir(ventas, plantillas, Copias);
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(html, Orientacion, ct);

        return new RemisionPdf
        {
            Contenido = pdf,
            NombreArchivo = NombreArchivo(ventas[0])
        };
    }

    /*
      EL PAPEL DE ZARAGOZA.

      Mismo esqueleto que el de Tauro —se piden las plantillas, se recorren las
      ventas, las que no se puedan imprimir aportan su motivo— pero con el
      procedimiento y el constructor de alla. Ver RemisionZaragozaHtmlBuilder.
    */
    private async Task<RemisionPdf> GenerarZaragozaAsync(
        IReadOnlyList<int> idsVenta,
        int idUsuarioImpresion,
        CancellationToken ct)
    {
        var plantillas = await _repository.ConsultarPlantillasZaragozaAsync(ct);
        if (plantillas.Rows.Count == 0)
            throw new InvalidOperationException("No estan cargadas las plantillas de la remision de Zaragoza.");

        var logos = await _repository.ConsultarLogosAsync(ct);

        var ventas = new List<System.Data.DataTable>();
        var rechazadas = new List<string>();

        foreach (var idVenta in idsVenta)
        {
            var resultado = await _repository.ConsultarDatosVentaZaragozaAsync(idVenta, idUsuarioImpresion, ct);

            if (resultado.Datos is null)
            {
                rechazadas.Add(resultado.Mensaje);
                continue;
            }

            ventas.Add(resultado.Datos);
        }

        if (ventas.Count == 0)
        {
            throw new InvalidOperationException(rechazadas.Count > 0
                ? string.Join(" ", rechazadas)
                : "No se pudo obtener la informacion de las remisiones.");
        }

        var html = RemisionZaragozaHtmlBuilder.Construir(ventas, plantillas, logos.Logo, logos.MarcaDeAgua);
        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(html, Orientacion, ct);

        var primera = ventas[0].Rows[0];

        return new RemisionPdf
        {
            Contenido = pdf,
            NombreArchivo = NombreArchivoDe("zaragoza", primera)
        };
    }

    /*
      El mismo nombre que arma MacReportes al descargar: "tauro_<folio>_<cliente>",
      recortado a 20 caracteres de nombre y con todo lo que no sea letra o
      numero convertido en guion bajo.
    */
    private static string NombreArchivo(RemisionDatosVenta venta)
    {
        var folio = Valor(venta.Detalle.Rows.Count > 0 ? venta.Detalle.Rows[0] : null, "folio");
        return NombreArchivoDe("tauro", venta.Informacion.Rows[0], folio);
    }

    /*
      En Zaragoza el folio y el cliente vienen en la MISMA tabla que el detalle,
      porque su procedimiento devuelve un solo conjunto. Por eso el folio se
      puede omitir y se toma de ahi.
    */
    private static string NombreArchivoDe(string empresa, System.Data.DataRow informacion, string? folio = null)
    {
        folio ??= Valor(informacion, "folio");

        var nombreCompleto = (Valor(informacion, "ClienteApellidoPaterno") + " " +
                              Valor(informacion, "ClienteApellidoMaterno") + " " +
                              Valor(informacion, "ClienteNombre")).Trim();

        if (nombreCompleto.Length > 21)
            nombreCompleto = nombreCompleto.Substring(0, 20);

        var archivo = empresa + "_" + folio + "_" + nombreCompleto;
        return Regex.Replace(archivo, "[^0-9a-zA-Z]+", "_") + ".pdf";
    }

    private static string Valor(System.Data.DataRow? row, string columna)
    {
        if (row is null || !row.Table.Columns.Contains(columna))
            return "";

        return RemisionFormatoLegacy.ValorDeColumna(row[columna]);
    }
}
