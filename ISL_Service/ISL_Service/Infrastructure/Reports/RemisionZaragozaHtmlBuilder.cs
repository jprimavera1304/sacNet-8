using System.Data;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ISL_Service.Infrastructure.Reports;

/*
  LA REMISION DE ZARAGOZA ES OTRO PAPEL, NO EL MISMO CON OTRO LOGO

  Esto se descubrio comparando el PDF del web contra el de Mac31 en Zaragoza: no
  se parecian. La causa no estaba en las plantillas —las de la remision de Tauro
  son identicas en las dos bases— sino en que Zaragoza NO USA ESA REMISION.

  El desvio esta en Legacy/MacServicios2 (ReporteV2Service, ~linea 813): cuando
  le piden el reporte 1001 "Remision", si la aplicacion es MACZ lo cambia por la
  remision generica de Zaragoza y se va por el motor de plantillas. Solo cuando
  es MAC31 entra al constructor de remision que nosotros ya portamos.

  Y en la base de Zaragoza el 1001 ni siquiera es una remision: ahi ese numero es
  "Reporte de Cascos Excedentes". El papel de Zaragoza es el 1202, "Remision
  Zaragoza Generico", que en la base de Tauro no existe.

  POR ESO ESTE ARCHIVO. Arma el papel de Zaragoza con sus cinco plantillas
  (cuerpo_n, detalle, totales, pie, hoja4) y su procedimiento
  sp_n_rptVentasRemisionSinPrecio, respetando lo que hace el motor viejo:

    1. O R I G I N A L         sin precios (con los valores "Fake")
    2. COPIA CLIENTE           lo mismo
    3. Credito y Cobranza      aqui SI van los precios y los totales reales
    4. Hoja 4                  el talon con folio, cliente y referencia

  Esas son las cuatro hojas que se ven en el PDF de Mac31.

  SE REPRODUCE LA FORMA, NO SE MEJORA. Los espacios de mas antes de los numeros,
  el "0" que se vuelve cadena vacia fuera de los totales, el truncado a dos
  decimales en vez de redondeo: todo eso esta copiado a proposito. Es un papel
  que la gente compara con el que ya tiene en la mano.
*/
public static class RemisionZaragozaHtmlBuilder
{
    /*
      Misma cultura que el otro formato: .NET 8 saca es-MX de ICU y ahi la hora
      es de 24 horas, mientras que el papel de Mac31 dice "a. m.".
    */
    private static readonly CultureInfo Cultura = RemisionFormatoLegacy.Cultura;

    /* Los formatos de numero del motor viejo, tal cual. */
    private const string FormatoEntero = "###,##0";
    private const string FormatoDecimal = "###,##0.00";
    private const string FormatoImporte = "###,###,##0.00";

    /*
      La marca de agua se va recorriendo hacia abajo hoja tras hoja. Estos dos
      numeros salen del motor viejo y son los que la dejan donde debe en cada
      una; no son para ajustar a ojo.
    */
    private const int TopInicialMarcaAgua = 500;
    private const int SaltoMarcaAgua = 1590;

    public static string Construir(
        IReadOnlyList<DataTable> ventas,
        DataTable plantillas,
        string logo,
        string logoMarcaAgua)
    {
        var html = new StringBuilder();

        var htmlHoja4Base = Plantilla(plantillas, "hoja4");
        var htmlPieBase = Plantilla(plantillas, "pie");
        var maxLineas = MaxLineas(plantillas);

        /*
          La marca de agua NO se reinicia con cada venta: el contador sigue
          corriendo entre remisiones porque todas terminan en el mismo PDF.
        */
        var numPaginaRemisiones = 0;

        foreach (var datos in ventas)
        {
            if (datos.Rows.Count == 0)
                continue;

            /*
              DataTable.Load marca como SOLO LECTURA las columnas que el
              procedimiento calcula, y este papel necesita escribir encima de
              varias de ellas. Legacy no se topaba con esto porque llenaba el
              DataSet por otro camino. Se desbloquean antes de empezar.
            */
            Desbloquear(datos);

            var fila = datos.Rows[0];

            /*
              PRIMERO SIN PRECIOS. El procedimiento trae cada importe dos veces:
              el real y uno terminado en "Fake", que es el que va en el papel del
              cliente. Se copia el Fake encima del real, que es lo que permite
              usar UNA sola plantilla para las hojas con precio y sin precio.
            */
            AplicarPreciosFake(datos);

            var cuerpo = ComponerCuerpo(datos, plantillas, logo, logoMarcaAgua, maxLineas);

            var totalCostoUsadoCargoConIva = Importe(fila, "TotalCostoUsadoCargoConIva");
            var ganancia = Importe(fila, "Ganancia");

            /*
              En las dos primeras hojas el costo del casco y la ganancia se
              borran: son numeros de la casa, no del cliente.
            */
            EscribirSiExiste(fila, "TotalCostoUsadoCargoConIva", "0.00");
            EscribirSiExiste(fila, "Ganancia", "");

            var pieSinPrecios = ReemplazaValores(datos, fila, htmlPieBase.Replace("#Devoluciones#", ""));

            html.Append(Hoja(cuerpo, "O R I G I N A L", pieSinPrecios, logoMarcaAgua, ref numPaginaRemisiones));
            html.Append(Hoja(cuerpo, "COPIA CLIENTE", pieSinPrecios, logoMarcaAgua, ref numPaginaRemisiones));

            /*
              AHORA SI CON PRECIOS. La hoja de Credito y Cobranza usa los mismos
              renglones pero deshaciendo el "Fake", por eso se vuelve a componer
              el cuerpo desde cero en vez de reusar el de arriba.
            */
            AplicarPreciosReales(datos);

            var cuerpoConPrecios = ComponerCuerpo(datos, plantillas, logo, logoMarcaAgua, maxLineas);

            EscribirSiExiste(fila, "TotalPagarPrimeraParte", Texto(fila, "TotalPagarZ"));
            EscribirSiExiste(fila, "TotalPagarPrimeraParteFtm", " " + FormatoImporteDe(Texto(fila, "TotalPagarZ")));

            var pieConPrecios = htmlPieBase.Replace(
                "#TotalCostoUsadoCargoConIva#",
                totalCostoUsadoCargoConIva == "0.00" ? "0.00" : totalCostoUsadoCargoConIva);

            EscribirSiExiste(fila, "Ganancia", "$ " + ganancia);
            pieConPrecios = ReemplazaValores(datos, fila, pieConPrecios);

            var totalPagarZ = Importe(fila, "TotalPagarZ");
            var totalImporteConIva = Importe(fila, "TotalImporteConIva");
            var totalImpuestos = Importe(fila, "TotalImpuestos");

            html.Append(Hoja(
                cuerpoConPrecios,
                "Crédito Cobranza",
                pieConPrecios,
                logoMarcaAgua,
                ref numPaginaRemisiones,
                textoGanancia: "IMPUESTOS: ",
                textoImporte: "IMPORTE: $",
                textoCargoCascos: "CARGO CASCOS: $",
                textoTotal: "TOTAL: $",
                valorGanancia: ganancia,
                valorImportes: totalImporteConIva + "/" + totalImpuestos,
                valorCargoCascos: totalCostoUsadoCargoConIva,
                valorTotal: totalPagarZ));

            /* La hoja 4 es un talon suelto: no lleva cuerpo ni paginado. */
            numPaginaRemisiones++;

            html.Append(htmlHoja4Base
                .Replace("#FolioFtm#", Texto(fila, "Folio"))
                .Replace("#Importes#", totalImporteConIva)
                .Replace("#TotalCostoUsadoCargoConIva#", totalCostoUsadoCargoConIva)
                .Replace("#Total#", totalPagarZ)
                .Replace("#ClienteNumero#", Texto(fila, "ClienteNumero"))
                .Replace("#ClienteApellidoPaterno#", Texto(fila, "ClienteApellidoPaterno"))
                .Replace("#ClienteApellidoMaterno#", Texto(fila, "ClienteApellidoMaterno"))
                .Replace("#ClienteNombre#", Texto(fila, "ClienteNombre"))
                .Replace("#ClienteReferencia#", Texto(fila, "ClienteReferencia")));
        }

        return html.ToString();
    }

    /*
      UNA HOJA = el cuerpo ya compuesto + el tipo de copia + el pie.

      El paginado se hace sobre las marcas #pie# que dejo el compositor: hay una
      por hoja, la ultima se queda como pie de verdad y a las anteriores se les
      mete el "Página: n de N". Asi lo hace el motor viejo, y por eso el numero
      de pagina solo aparece cuando la remision ocupa mas de una.
    */
    private static string Hoja(
        string cuerpo,
        string tipoTexto,
        string pie,
        string logoMarcaAgua,
        ref int numPaginaRemisiones,
        string textoGanancia = "",
        string textoImporte = "",
        string textoCargoCascos = "",
        string textoTotal = "",
        string valorGanancia = "",
        string valorImportes = "",
        string valorCargoCascos = "",
        string valorTotal = "")
    {
        var html = cuerpo.Replace("#TipoTexto#", tipoTexto);

        var totalPaginas = Regex.Matches(html, Regex.Escape("#pie#")).Count;

        var numPagina = 1;
        while (Regex.Matches(html, Regex.Escape("#pie#")).Count > 1)
        {
            var inicio = html.IndexOf("#pie#", StringComparison.Ordinal);
            html = html.Remove(inicio, 5)
                       .Insert(inicio, "<br /> <span style='font - size:18pt; font - weight:normal; '><b>Página: " +
                                       numPagina + " de " + totalPaginas + "</b>");
            numPagina++;
        }

        for (var i = 1; i <= totalPaginas; i++)
        {
            var inicio = html.IndexOf("#topWM#", StringComparison.Ordinal);
            if (inicio < 0)
                break;

            var top = (SaltoMarcaAgua * numPaginaRemisiones) + TopInicialMarcaAgua;
            html = html.Remove(inicio, 7).Insert(inicio, top.ToString(CultureInfo.InvariantCulture));
            numPaginaRemisiones++;
        }

        return html
            .Replace("#pie#", pie)
            .Replace("#TextoGanancia#", textoGanancia)
            .Replace("#TextoImporte#", textoImporte)
            .Replace("#TextoCargoCascos#", textoCargoCascos)
            .Replace("#TextoTotal#", textoTotal)
            .Replace("#Ganancia#", valorGanancia)
            .Replace("#Importes#", valorImportes)
            .Replace("#TotalCostoUsadoCargoConIva#", valorCargoCascos)
            .Replace("#Total#", valorTotal)
            .Replace("#LogoWM#", logoMarcaAgua)
            .Replace("#CanceladaWM#", "");
    }

    /*
      EL COMPOSITOR (RepFormatoNormalService en legacy).

      Reparte los renglones en hojas de maxLineas y en cada salto arranca otra
      con su encabezado. Los totales van UNA sola vez, despues del ultimo
      renglon.

      El motor viejo agrupa por Agente porque este mismo compositor sirve a
      reportes de varios agentes; en una remision siempre es uno, asi que aqui se
      recorre derecho. Agrupar daria el mismo resultado con mas partes moviles.
    */
    private static string ComponerCuerpo(
        DataTable datos,
        DataTable plantillas,
        string logo,
        string logoMarcaAgua,
        int maxLineas)
    {
        var plantillaCuerpo = Plantilla(plantillas, "cuerpo_n");
        var plantillaDetalle = Plantilla(plantillas, "detalle");
        var plantillaTotales = Plantilla(plantillas, "totales");
        var nombreReporte = NombreReporte(plantillas);

        if (maxLineas <= 0)
            maxLineas = datos.Rows.Count > 0 ? datos.Rows.Count : 1;

        var totalPaginas = datos.Rows.Count / maxLineas;
        if (datos.Rows.Count % maxLineas != 0)
            totalPaginas++;

        var html = new StringBuilder();
        var htmlCuerpo = "";
        var htmlDetalle = new StringBuilder();
        var linea = 0;
        var numPagina = 1;
        var numRegistro = 0;
        var totalesPuestos = false;

        foreach (DataRow fila in datos.Rows)
        {
            numRegistro++;

            if (linea == 0)
            {
                htmlCuerpo = "<p style='page-break-before: always'></p>" +
                             Encabezado(plantillaCuerpo, logo, nombreReporte, numPagina, totalPaginas, datos, numRegistro);
            }

            htmlDetalle.Append(ReemplazaValores(datos, fila, plantillaDetalle));
            linea++;

            if (linea < maxLineas)
                continue;

            if (numRegistro == datos.Rows.Count)
            {
                htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReemplazaValores(datos, datos.Rows[0], plantillaTotales, esTotales: true));
                totalesPuestos = true;
            }

            linea = 0;
            numPagina++;
            htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle.ToString()).Replace("#Totales#", "");
            html.Append(htmlCuerpo);
            htmlDetalle.Clear();
        }

        if (!totalesPuestos)
        {
            htmlCuerpo = htmlCuerpo
                .Replace("#Detalle#", htmlDetalle.ToString())
                .Replace("#Totales#", datos.Rows.Count > 0
                    ? ReemplazaValores(datos, datos.Rows[0], plantillaTotales, esTotales: true)
                    : "");
            html.Append(htmlCuerpo);
        }

        html.Append("<p style='page-break-before: always'></p>");

        return html.ToString().Replace("#LogoWM#", logoMarcaAgua);
    }

    private static string Encabezado(
        string plantillaCuerpo,
        string logo,
        string nombreReporte,
        int numPagina,
        int totalPaginas,
        DataTable datos,
        int numRegistro)
    {
        var html = plantillaCuerpo
            .Replace("#Logo#", logo)
            .Replace("#Titulo#", nombreReporte)
            .Replace("#NumPagina#", numPagina.ToString(CultureInfo.InvariantCulture))
            .Replace("#TotalPaginas#", totalPaginas.ToString(CultureInfo.InvariantCulture));

        /*
          Si el procedimiento trae su propia columna topWM manda ella, y la marca
          de agua no se recorre desde aqui.
        */
        if (datos.Columns.Contains("topWM") && numRegistro - 1 < datos.Rows.Count)
            html = html.Replace("#topWM#", Texto(datos.Rows[numRegistro - 1], "topWM"));

        var fila = datos.Rows[0];
        foreach (DataColumn columna in datos.Columns)
        {
            html = html.Replace(
                "#" + columna.ColumnName + "#",
                FormateaNumero(columna.DataType.Name, Texto(fila, columna.ColumnName), esTotales: false));
        }

        return html;
    }

    /*
      Sustituye #Columna# por su valor en toda la plantilla, tambien en
      minusculas: las plantillas de legacy escriben unas veces #Folio# y otras
      #folio#, y el motor viejo prueba las dos.
    */
    private static string ReemplazaValores(DataTable datos, DataRow fila, string html, bool esTotales = false)
    {
        foreach (DataColumn columna in datos.Columns)
        {
            var tag = "#" + columna.ColumnName + "#";
            var valor = Texto(fila, columna.ColumnName);

            if (valor != "")
                valor = FormateaNumero(columna.DataType.Name, valor, esTotales);

            html = html.Replace(tag, valor).Replace(tag.ToLowerInvariant(), valor);
        }

        return html;
    }

    /*
      El formato del motor viejo, con sus rarezas intactas:

      - -99999 es el "no aplica" de los procedimientos y sale como cero.
      - Un cero se imprime VACIO salvo en el renglon de totales. Deja el papel
        limpio de ceros sueltos, que es como lo lee la gente de mostrador.
      - Los decimales se TRUNCAN a dos, no se redondean. Redondear moveria
        centavos contra el papel que ya tienen impreso.
    */
    private static string FormateaNumero(string tipo, string valor, bool esTotales)
    {
        if (tipo == "Int32" && valor == "") return "0";
        if (tipo == "Decimal" && valor == "") return "0.00";

        if (valor == "-99999") return "0";
        if (valor == "-99999.00") return "0.00";

        if (valor == "0") return esTotales ? valor : "";
        if (valor is "0.00" or "0.0000") return esTotales ? valor : "";

        if (tipo == "Int32" && int.TryParse(valor, NumberStyles.Any, Cultura, out var entero))
            return entero.ToString(FormatoEntero, Cultura);

        if (tipo == "Single" && double.TryParse(valor, NumberStyles.Any, Cultura, out var flotante))
            return flotante.ToString(FormatoDecimal, Cultura);

        if (tipo == "Decimal" && decimal.TryParse(valor, NumberStyles.Any, Cultura, out var decimalValor))
            return Truncar(decimalValor, 2).ToString(FormatoDecimal, Cultura);

        return valor;
    }

    private static decimal Truncar(decimal valor, int precision)
    {
        var paso = (decimal)Math.Pow(10, precision);
        return Math.Truncate(paso * valor) / paso;
    }

    private static void AplicarPreciosFake(DataTable datos)
    {
        foreach (DataRow fila in datos.Rows)
        {
            if (Texto(fila, "SubTotalPagar") == "")
                continue;

            EscribirSiExiste(fila, "PrecioListaConUsadoConIvaFtm", " " + FormatoImporteDe(Texto(fila, "PrecioRemisionSinIvaFake")));
            EscribirSiExiste(fila, "SubTotalConUsadoConIvaFtm", " " + FormatoImporteDe(Texto(fila, "ImporteRemisionSinIvaFake")));
            EscribirSiExiste(fila, "SubTotalPagarMostrar", " " + FormatoImporteDe(Texto(fila, "SubTotalPagarFake")));
            EscribirSiExiste(fila, "IvaPagarMostrar", " " + FormatoImporteDe(Texto(fila, "IvaPagarFake")));
            EscribirSiExiste(fila, "TotalPagarMostrar", " " + FormatoImporteDe(Texto(fila, "TotalPagarFake")));
        }
    }

    /*
      Y de vuelta a los de verdad para la hoja de Credito y Cobranza.

      El renglon 99999999 es el marcador de "esto no es un producto" (el acarreo
      y la linea de cascos); a ese no se le recalcula el precio unitario porque
      no tiene cantidad que dividir.
    */
    private static void AplicarPreciosReales(DataTable datos)
    {
        var totalPagarZ = datos.Rows.Count > 0 ? Texto(datos.Rows[0], "TotalPagarZ") : "";

        foreach (DataRow fila in datos.Rows)
        {
            if (Texto(fila, "IDVentaDetalle") != "99999999")
            {
                var importe = Texto(fila, "ImporteConUsadoConIva");
                EscribirSiExiste(fila, "SubTotalConUsadoConIva", importe);
                EscribirSiExiste(fila, "SubTotalConUsadoConIvaFtm", " " + FormatoImporteDe(importe));

                var cantidad = Numero(Texto(fila, "Cantidad"));
                if (cantidad != 0)
                {
                    var unitario = Numero(importe) / cantidad;
                    EscribirSiExiste(fila, "PrecioListaConUsadoConIvaFtm", " " + unitario.ToString(FormatoImporte, Cultura));
                }
            }

            EscribirSiExiste(fila, "TotalPagarPrimeraParte", totalPagarZ);
        }

        foreach (DataRow fila in datos.Rows)
        {
            if (Texto(fila, "SubTotalPagar") == "")
                continue;

            EscribirSiExiste(fila, "PrecioListaConUsadoConIvaFtm", " " + FormatoImporteDe(Texto(fila, "PrecioRemisionSinIva")));
            EscribirSiExiste(fila, "SubTotalConUsadoConIvaFtm", " " + FormatoImporteDe(Texto(fila, "ImporteRemisionSinIva")));
            EscribirSiExiste(fila, "SubTotalPagarMostrar", " " + FormatoImporteDe(Texto(fila, "SubTotalPagar")));
            EscribirSiExiste(fila, "IvaPagarMostrar", " " + FormatoImporteDe(Texto(fila, "IvaPagar")));
            EscribirSiExiste(fila, "TotalPagarMostrar", " " + FormatoImporteDe(Texto(fila, "TotalPagar")));
        }
    }

    /*
      Escribir una columna que el procedimiento quiza ya no traiga tumbaria el
      reporte entero por una diferencia de esquema entre empresas. Se comprueba
      antes: si no esta, no habia nada que pintar con ella.
    */
    private static void Desbloquear(DataTable datos)
    {
        /*
          Se saca la lista primero: ConvertirATexto agrega y quita columnas, y no
          se puede recorrer la coleccion mientras cambia.
        */
        var columnas = datos.Columns.Cast<DataColumn>().ToList();

        foreach (var columna in columnas)
        {
            columna.ReadOnly = false;

            /*
              Ademas se pasan a texto: en el papel estas columnas llevan el
              numero YA formateado (" 1,234.00"), y eso no cabe en una columna
              decimal. Legacy las recibia como cadena.
            */
            if (columna.DataType != typeof(string) && EsColumnaQueSeReescribe(columna.ColumnName))
                ConvertirATexto(datos, columna);
        }
    }

    /* Las que este archivo sobreescribe con el numero ya formateado. */
    private static bool EsColumnaQueSeReescribe(string nombre)
        => nombre is "PrecioListaConUsadoConIvaFtm"
            or "SubTotalConUsadoConIvaFtm"
            or "SubTotalConUsadoConIva"
            or "SubTotalPagarMostrar"
            or "IvaPagarMostrar"
            or "TotalPagarMostrar"
            or "TotalPagarPrimeraParte"
            or "TotalPagarPrimeraParteFtm"
            or "TotalCostoUsadoCargoConIva"
            or "Ganancia";

    /*
      DataTable no deja cambiarle el tipo a una columna que ya tiene datos, asi
      que se agrega una columna de texto con los mismos valores, se borra la
      original y la nueva toma su nombre y su lugar.
    */
    private static void ConvertirATexto(DataTable datos, DataColumn columna)
    {
        var nombre = columna.ColumnName;
        var posicion = columna.Ordinal;
        var temporal = new DataColumn(nombre + "__texto", typeof(string));

        datos.Columns.Add(temporal);
        foreach (DataRow fila in datos.Rows)
            fila[temporal] = fila[columna] == DBNull.Value ? "" : RemisionFormatoLegacy.ValorDeColumna(fila[columna]);

        datos.Columns.Remove(columna);
        temporal.ColumnName = nombre;
        temporal.SetOrdinal(posicion);
    }

    private static void EscribirSiExiste(DataRow fila, string columna, string valor)
    {
        if (fila.Table.Columns.Contains(columna))
            fila[columna] = valor;
    }

    private static string Texto(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna))
            return "";

        var valor = fila[columna];
        return valor == DBNull.Value ? "" : RemisionFormatoLegacy.ValorDeColumna(valor);
    }

    private static decimal Numero(string valor)
        => decimal.TryParse(valor, NumberStyles.Any, Cultura, out var n) ? n : 0m;

    private static string Importe(DataRow fila, string columna)
        => FormatoImporteDe(Texto(fila, columna));

    private static string FormatoImporteDe(string valor)
        => Numero(valor).ToString(FormatoImporte, Cultura);

    private static string Plantilla(DataTable plantillas, string descripcion)
    {
        foreach (DataRow fila in plantillas.Rows)
        {
            if (string.Equals(Texto(fila, "Descripcion"), descripcion, StringComparison.OrdinalIgnoreCase))
                return Texto(fila, "Html");
        }

        throw new InvalidOperationException(
            $"Falta la plantilla '{descripcion}' de la remision de Zaragoza en Template_Html.");
    }

    private static int MaxLineas(DataTable plantillas)
        => plantillas.Rows.Count > 0 && int.TryParse(Texto(plantillas.Rows[0], "MaxLineas"), out var n) ? n : 0;

    private static string NombreReporte(DataTable plantillas)
        => plantillas.Rows.Count > 0 ? Texto(plantillas.Rows[0], "NombreReporte") : "";
}
