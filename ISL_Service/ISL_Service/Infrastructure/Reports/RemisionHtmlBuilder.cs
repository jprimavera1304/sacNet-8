using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace ISL_Service.Infrastructure.Reports;

/// Los seis conjuntos de datos que alimentan UNA remision. Son exactamente los
/// que junta legacy antes de armar el html, cada uno de su propio sp_n_.
public sealed class RemisionDatosVenta
{
    public int IdVenta { get; init; }
    public DataTable Informacion { get; init; } = new();
    public DataTable Detalle { get; init; } = new();
    public DataTable UsadosCargo { get; init; } = new();
    public DataTable UsadosCreditoActual { get; init; } = new();
    public DataTable UsadosCreditoAnterior { get; init; } = new();
    public DataTable Devolucion { get; init; } = new();
}

/*
  EL ARMADO DEL HTML DE LA REMISION

  Port de VentasImprimirService.GeneraRemisionHtml (y de los metodos que
  llama: VentasInformacion, VentasPie, VentasDetalle_VentasCargos,
  VentasUsadoCreditoAnterior, VentasUsadoCreditoActual, VentasDevolucion) de
  Legacy/MacServicios2/Mac3Servicios/Models/Service/VentasServ/VentasImprimirServ/.

  COMO FUNCIONA, EN UNA LINEA: las plantillas traen marcadores #Columna# y
  cada conjunto de datos recorre SUS columnas sustituyendo "#" + nombre + "#"
  por el valor. No hay una lista de campos en ningun lado: lo que trae el
  procedimiento es lo que se puede pintar. Por eso el port respeta el orden de
  las sustituciones —quien pisa a quien importa— y no "ordena" nada.

  LO QUE SE PIERDE SI ESTO SE PORTA A MEDIAS: una remision bien formada pero
  con menos conceptos de los que el cliente se llevo. No truena, no se ve mal,
  y no lo nota nadie hasta que reclaman. De ahi que al final se verifique que
  no haya quedado ningun marcador sin sustituir.
*/
public static class RemisionHtmlBuilder
{
    /// Legacy corta a la segunda pagina pasados 35 renglones de detalle.
    private const int RenglonesPorPagina = 35;

    /*
      Un marcador es "#Nombre#" con letras, digitos y guion bajo. La forma
      importa: en las plantillas hay colores css (#ccc) y textos con "#" que NO
      son marcadores, y confundirlos convertiria una remision buena en un error.
    */
    private static readonly Regex MarcadorPendiente = new(@"#[A-Za-z_][A-Za-z0-9_]*#", RegexOptions.Compiled);

    /*
      Una sola llamada arma TODAS las ventas seleccionadas, que es como lo pide
      la pantalla de consulta cuando hay varias marcadas. El salto de pagina
      entre una y otra ya viene en el html de cada remision.
    */
    public static string Construir(IReadOnlyList<RemisionDatosVenta> ventas, DataTable dtTemplateHtml, int copias)
    {
        if (copias <= 0)
            copias = 1;

        var normales = RemisionPlantillas.Desde(dtTemplateHtml, "");
        var reimpresiones = RemisionPlantillas.Desde(dtTemplateHtml, "reimp_");

        var remisionesHtml = new StringBuilder();

        foreach (var venta in ventas)
        {
            /*
              Quien decide si es reimpresion es el procedimiento, no nosotros:
              si la venta ya tenia usuario de impresion, devuelve la columna
              Reimpresion con texto y el papel tiene que salir con la marca.
            */
            var reimpresion = LeerCelda(venta.Informacion.Rows[0], "Reimpresion");
            var plantillas = reimpresion != "" ? reimpresiones : normales;

            remisionesHtml.Append(GeneraRemisionHtml(venta, plantillas, copias));
        }

        var html = remisionesHtml.ToString();
        VerificarQueNoQuedenMarcadores(html);
        return html;
    }

    private static string GeneraRemisionHtml(RemisionDatosVenta venta, RemisionPlantillas plantillas, int copias)
    {
        var resultado = new StringBuilder();

        foreach (DataRow rowInformacion in venta.Informacion.Rows)
        {
            var idVenta = LeerCelda(rowInformacion, "idVenta");
            var pathImagenes = ResolverPathImagenes(rowInformacion);
            var nopagina = 1;
            var numDetalleRows = 0;

            /*
              El salto de pagina va ANTES de cada remision, incluida la primera.
              Es lo que hace legacy; wkhtmltopdf lo ignora al principio del
              documento, asi que no sale una hoja en blanco.
            */
            var htmlString = "<p style='page-break-before: always'></p>" + plantillas.Cuerpo;
            htmlString = htmlString.Replace("#encabezado#", plantillas.Encabezado);
            htmlString = htmlString.Replace("#nopagina#", nopagina.ToString());

            htmlString = VentasInformacion(venta.Informacion, rowInformacion, htmlString);
            htmlString = VentasPie(venta.Informacion, rowInformacion, htmlString, plantillas.Pie);
            htmlString = VentasDetalleVentasCargos(venta, idVenta, htmlString, ref numDetalleRows, plantillas);
            htmlString = VentasUsadoCreditoAnterior(venta, idVenta, htmlString, plantillas);
            htmlString = VentasUsadoCreditoActual(venta, idVenta, htmlString, plantillas);
            htmlString = VentasDevolucion(venta, idVenta, htmlString, plantillas);

            if (numDetalleRows > RenglonesPorPagina)
            {
                /*
                  Con mas de 35 renglones la remision se va a una segunda hoja y
                  hay que repetir el encabezado. Se vuelven a correr Informacion
                  y Detalle SOLO para llenar los marcadores de ese encabezado
                  recien insertado (folio, almacen, ticket...).
                */
                nopagina++;
                var saltoLinea = "<p style='page-break-before: always'></p>" + plantillas.Encabezado;
                saltoLinea = saltoLinea.Replace("#nopagina#", nopagina.ToString());
                htmlString = htmlString.Replace("#SaltoLinea#", saltoLinea);
                htmlString = htmlString.Replace("#ImagenReimpresion3#", LeerCelda(rowInformacion, "ImagenReimpresion1"));

                htmlString = VentasInformacion(venta.Informacion, rowInformacion, htmlString);
                htmlString = VentasDetalleVentasCargos(venta, idVenta, htmlString, ref numDetalleRows, plantillas);
            }
            else
            {
                htmlString = htmlString.Replace("#SaltoLinea#", "");
                htmlString = htmlString.Replace("#ImagenReimpresion3#", "");
            }

            htmlString = htmlString.Replace("#Version1#", "");
            htmlString = htmlString.Replace("#Version4#", "");
            htmlString = htmlString.Replace("#imagenFondo#", "");
            htmlString = htmlString.Replace("#dirImagenes#", pathImagenes);
            htmlString = htmlString.Replace("#totalpaginas#", nopagina.ToString());

            for (var i = 1; i <= copias; i++)
                resultado.Append(htmlString);
        }

        return resultado.ToString();
    }

    /*
      DE DONDE SALEN LAS IMAGENES (logo, sello de CANCELADA)

      Legacy siempre usa PathImagenes, que es una carpeta local del servidor
      (C:\Mac21\imagenes\) porque MacReportes corre en la misma maquina donde
      esta esa carpeta. Este backend no tiene por que correr ahi: si la carpeta
      no existe se usa PathImagenesServer, que es la MISMA carpeta publicada por
      http. Es la unica desviacion respecto de legacy y es para que el papel
      salga igual, no distinto: sin esto el logo sale como imagen rota.
    */
    private static string ResolverPathImagenes(DataRow rowInformacion)
    {
        var local = LeerCelda(rowInformacion, "PathImagenes");
        if (local != "" && Directory.Exists(local))
            return local;

        var servidor = LeerCelda(rowInformacion, "PathImagenesServer");
        return servidor != "" ? servidor : local;
    }

    /// Sustituye los campos de la venta SIN formato de numero (legacy tampoco lo aplica aqui).
    private static string VentasInformacion(DataTable dtInformacion, DataRow rowInformacion, string htmlString)
    {
        foreach (DataColumn column in dtInformacion.Columns)
        {
            var tag = "#" + column.ColumnName + "#";
            var valor = RemisionFormatoLegacy.ValorDeColumna(rowInformacion[column.ColumnName]);
            htmlString = htmlString.Replace(tag, valor);
        }

        return htmlString;
    }

    /// El pie si formatea numeros: ahi va el total con letra y los datos de pago.
    private static string VentasPie(DataTable dtInformacion, DataRow rowInformacion, string htmlString, string pieTemplate)
    {
        var pieHtmlString = pieTemplate;

        foreach (DataColumn column in dtInformacion.Columns)
        {
            var tag = "#" + column.ColumnName + "#";
            var valor = RemisionFormatoLegacy.ValorDeColumna(rowInformacion[column.ColumnName]);
            valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

            pieHtmlString = pieHtmlString.Replace(tag, valor);
        }

        return htmlString.Replace("#Pie#", pieHtmlString);
    }

    /*
      EL DETALLE Y LOS CARGOS POR USADOS

      El detalle repite la plantilla de renglon una vez por partida. Ademas —y
      esto es facil de perder al portar— cada renglon sustituye tambien sobre el
      CUERPO: los totales (#Importe#, #IVA#, #TotalPagarFtm#), la marca de
      cancelada (#CanceladaDetalle#) y datos del encabezado (#Folio#, #Ticket#,
      #Almacen#) son columnas del detalle, no de la informacion de la venta.
      Como Replace solo pega la primera vez, gana el primer renglon.
    */
    private static string VentasDetalleVentasCargos(
        RemisionDatosVenta venta,
        string idVenta,
        string htmlString,
        ref int numDetalleRows,
        RemisionPlantillas plantillas)
    {
        var detalleHtmlString = new StringBuilder();
        var usadosCargosHtmlString = new StringBuilder();

        var filasDetalle = Filtrar(venta.Detalle, idVenta);
        if (filasDetalle.Count == 0)
            throw new InvalidOperationException($"La venta {idVenta} no tiene renglones de detalle; no se puede imprimir.");

        numDetalleRows = filasDetalle.Count - 1;

        foreach (var rowDetalle in filasDetalle)
        {
            detalleHtmlString.Append(plantillas.Detalle);
            var renglon = detalleHtmlString.ToString();

            foreach (DataColumn column in venta.Detalle.Columns)
            {
                var tag = "#" + column.ColumnName + "#";
                var valor = RemisionFormatoLegacy.ValorDeColumna(rowDetalle[column.ColumnName]);
                valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

                renglon = renglon.Replace(tag, valor);
                htmlString = htmlString.Replace(tag, valor);
            }

            detalleHtmlString.Clear();
            detalleHtmlString.Append(renglon);

            htmlString = htmlString.Replace(
                "#TotalPagarLetra#",
                RemisionNumeroALetra.ConvertirNumero(RemisionFormatoLegacy.ValorDeColumna(filasDetalle[0]["TotalPagar"])));
        }

        /*
          Los cargos por usados (cascos que el cliente NO entrego y se le
          cobran) pueden no existir; legacy lo resuelve dejando la seccion
          vacia, no fallando.
        */
        var filasCargo = Filtrar(venta.UsadosCargo, idVenta);
        foreach (var rowUsadoCargo in filasCargo)
        {
            usadosCargosHtmlString.Append(plantillas.UsadoCargo);
            var renglon = usadosCargosHtmlString.ToString();

            foreach (DataColumn column in venta.UsadosCargo.Columns)
            {
                var tag = "#" + column.ColumnName + "#";
                var valor = RemisionFormatoLegacy.ValorDeColumna(rowUsadoCargo[column.ColumnName]);
                valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

                renglon = renglon.Replace(tag, valor);
            }

            usadosCargosHtmlString.Clear();
            usadosCargosHtmlString.Append(renglon);
        }

        htmlString = htmlString.Replace("#Detalle#", detalleHtmlString.ToString());
        htmlString = htmlString.Replace("#UsadosCargos#", usadosCargosHtmlString.ToString());
        return htmlString;
    }

    /// Cascos que el cliente ya habia dejado a deber de remisiones anteriores.
    private static string VentasUsadoCreditoAnterior(RemisionDatosVenta venta, string idVenta, string htmlString, RemisionPlantillas plantillas)
    {
        var filas = Filtrar(venta.UsadosCreditoAnterior, idVenta);
        if (filas.Count == 0)
            return htmlString.Replace("#UsadosCreditosAnterior#", "");

        var bloque = new StringBuilder();

        foreach (var row in filas)
        {
            bloque.Append(plantillas.UsadoCreditoAnteriorDetalle);
            var renglon = bloque.ToString();

            foreach (DataColumn column in venta.UsadosCreditoAnterior.Columns)
            {
                var tag = "#" + column.ColumnName + "#";
                var valor = RemisionFormatoLegacy.ValorDeColumna(row[column.ColumnName]);
                valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

                renglon = renglon.Replace(tag, valor);
            }

            bloque.Clear();
            bloque.Append(renglon);
        }

        return htmlString.Replace("#UsadosCreditosAnterior#", plantillas.UsadoCreditoAnteriorEncabezado + bloque);
    }

    /// Cascos que el cliente entrega en ESTA remision.
    private static string VentasUsadoCreditoActual(RemisionDatosVenta venta, string idVenta, string htmlString, RemisionPlantillas plantillas)
    {
        var filas = Filtrar(venta.UsadosCreditoActual, idVenta);

        if (filas.Count == 0)
        {
            /*
              Sin credito de usados hay que limpiar tambien los campos sueltos
              que viven en el cuerpo (fecha, usuario, los dos textos), porque
              esos los llena esta seccion y si no se quedan escritos en el papel.
            */
            htmlString = htmlString.Replace("#UsadosCreditosActual#", "");
            htmlString = htmlString.Replace("#FechaUsadoCredito#", "");
            htmlString = htmlString.Replace("#NombreUsuarioUsadoCreditoActual#", "");
            htmlString = htmlString.Replace("#TextoTipoUsadosCreditosActual#", "");
            htmlString = htmlString.Replace("#TextoCantidadUsadosCreditosActual#", "");
            return htmlString;
        }

        var bloque = new StringBuilder();

        foreach (var row in filas)
        {
            bloque.Append(plantillas.UsadoCreditoActual);
            var renglon = bloque.ToString();

            foreach (DataColumn column in venta.UsadosCreditoActual.Columns)
            {
                var tag = "#" + column.ColumnName + "#";
                var valor = RemisionFormatoLegacy.ValorDeColumna(row[column.ColumnName]);
                valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

                renglon = renglon.Replace(tag, valor);
                htmlString = htmlString.Replace(tag, valor);
            }

            bloque.Clear();
            bloque.Append(renglon);
        }

        return htmlString.Replace("#UsadosCreditosActual#", bloque.ToString());
    }

    /// Lo que el cliente devolvio de esta remision.
    private static string VentasDevolucion(RemisionDatosVenta venta, string idVenta, string htmlString, RemisionPlantillas plantillas)
    {
        var filas = Filtrar(venta.Devolucion, idVenta);
        if (filas.Count == 0)
            return htmlString.Replace("#Devoluciones#", "");

        var bloque = new StringBuilder();

        foreach (var row in filas)
        {
            bloque.Append(plantillas.DevolucionDetalle);
            var renglon = bloque.ToString();

            foreach (DataColumn column in venta.Devolucion.Columns)
            {
                var tag = "#" + column.ColumnName + "#";
                var valor = RemisionFormatoLegacy.ValorDeColumna(row[column.ColumnName]);
                valor = RemisionFormatoLegacy.FormateaNumero(column.DataType.Name, valor);

                renglon = renglon.Replace(tag, valor);
            }

            bloque.Clear();
            bloque.Append(renglon);
        }

        var textoDevoluciones = plantillas.DevolucionEncabezado.Replace("#DevolucionesDetalle#", bloque.ToString());
        return htmlString.Replace("#Devoluciones#", textoDevoluciones);
    }

    /*
      Legacy filtra cada tabla por "idVenta = N" con DataTable.Select. Se hace
      igual, pero tolerando que la tabla venga vacia o sin esa columna (que es
      justo el caso "esta venta no tuvo devoluciones"): legacy lo resolvia
      dejando que Select tronara y atrapando la excepcion.
    */
    private static List<DataRow> Filtrar(DataTable tabla, string idVenta)
    {
        if (tabla.Rows.Count == 0)
            return new List<DataRow>();

        var columna = tabla.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => string.Equals(c.ColumnName, "IDVenta", StringComparison.OrdinalIgnoreCase));

        if (columna is null)
            return tabla.Rows.Cast<DataRow>().ToList();

        return tabla.Rows.Cast<DataRow>()
            .Where(r => RemisionFormatoLegacy.ValorDeColumna(r[columna]).Trim() == idVenta.Trim())
            .ToList();
    }

    private static string LeerCelda(DataRow row, string columna)
    {
        foreach (DataColumn c in row.Table.Columns)
        {
            if (string.Equals(c.ColumnName, columna, StringComparison.OrdinalIgnoreCase))
                return RemisionFormatoLegacy.ValorDeColumna(row[c]);
        }

        return "";
    }

    /*
      LA RED DE SEGURIDAD

      Un marcador que sobrevive al armado significa que un pedazo del papel no
      se lleno: o falto portar una seccion, o el procedimiento dejo de devolver
      una columna. En legacy eso se imprimia tal cual —"#Observaciones#" en
      medio de la remision— o, peor, la seccion salia vacia y nadie se enteraba.

      Aqui se truena con los nombres. Vale mas no entregar el papel que
      entregar uno con menos conceptos de los que el cliente se llevo.
    */
    private static void VerificarQueNoQuedenMarcadores(string html)
    {
        var pendientes = MarcadorPendiente.Matches(html)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pendientes.Count == 0)
            return;

        throw new InvalidOperationException(
            "La remision quedo con marcadores sin sustituir: " + string.Join(", ", pendientes) +
            ". Es senal de que falta un dato o una plantilla, asi que no se entrega el PDF.");
    }
}
