using System.Data;
using System.Globalization;
using System.Text;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  EL CONCENTRADO DEL REPARTIDOR (1005 / 1008)
  ══════════════════════════════════════════════════════════════════════

  EL PAPEL QUE FALTABA, NO UNA DIFERENCIA DE FORMATO.

  El catalogo tenia `concentrados`, que es el 1185 "Concentrados Detalle": el
  LISTADO de los concentrados de un rango de fechas. El DOCUMENTO de UN
  concentrado -la hoja que se le da al repartidor con sus acumuladores, sus
  productos, sus cascos y su rejilla de folios- es el 1005, y no estaba en
  ninguna parte: ni en ReportesCatalogo, ni como id, ni como armador. O sea
  que desde el web no habia forma de imprimirlo.

  En Mac31 lo imprime el boton IMPRIMIR de ConsultarConcentrados
  (ConsultarConcentrados.cs:1239, con los IDConcentrado marcados separados por
  `~`) y el de la propia pantalla Concentrado (Concentrado.cs:908, con uno
  solo). Los dos entran por el camino de siempre -se guarda el renglon de
  parametros y se abre `URL_REPORTES_V2?psp=`-, y alla ReporteV2Service lo
  desvia a ReporteConcentradoService (ReporteV2Service.cs:1116-1119) porque la
  plantilla generica no sabe pintarlo: sus seis piezas se llaman
  `concentrado_*` y no `cuerpo_n` / `detalle` / `totales`.

  ESTO ES ESE SERVICIO, PORTADO. Mismo recorrido, mismos tags, mismos
  formatos, y las rarezas de legacy copiadas a proposito porque es el papel que
  el repartidor firma:

    * UNA HOJA POR CONCENTRADO, agrupando la primera tabla por IDConcentrado y
      tomando su renglon de menor ID, que es el que trae la cabecera.
    * LOS ROTULOS DE LAS COLUMNAS SON FIJOS: "LTH", "AMERICA", "CALE",
      "PRODUCTOS". Legacy los escribe a mano aunque el concentrado traiga otras
      marcas; no se "arreglan" por la misma razon que en la hoja de cobro.
    * `#Titulo5#` a `#Titulo8#` se vacian: legacy los deja en blanco.
    * EL RENGLON DE ACUMULADORES SOLO SALE SI ALGUNA DE SUS SEIS CELDAS TIENE
      ALGO, y el de productos solo si alguna de sus dos tiene algo. Pegando la
      plantilla siempre saldrian decenas de renglones vacios.
    * EL TOTAL DE ACUMULADORES es la SUMA de TotalCol1 + TotalCol2 + TotalCol3
      y se imprime con " PZ" pegado.
    * `#Productos#` lleva TotalColFtm4 (la columna de productos ya formateada) y
      `#CantidadTotal#` lleva la columna `Productos` con " PZ". Se ven
      cambiados de sitio y asi estan en legacy.
    * SI NO HAY CASCOS se escribe el "S I N   U S A D O S" espaciado, letra por
      letra, tal cual.
    * LOS TOTALES DE CASCOS Y SU IMPORTE SALEN VACIOS CUANDO SON CERO, no
      "0"/"0.00".
    * EL NOMBRE DEL CLIENTE SE CORTA A 40 CARACTERES.
    * LA REJILLA DE FOLIOS SE TOTALIZA EN C#, sumando renglon por renglon, no
      con lo que diga el procedimiento.

  UN DEFECTO QUE NO SE PORTA, Y SE DICE: legacy lee
  `dtConcentradoFoliosUsados.Rows[0]["htmlTauroDetalleFoliosA"]` sin comprobar
  que haya renglones, asi que un concentrado sin folios le revienta con
  "no rows" y su catch devuelve la excepcion en vez del papel. Aqui ese caso
  deja el tag en blanco y el resto de la hoja sale. Copiar un crash no imprime
  nada, y un concentrado sin folios es un concentrado recien abierto.
*/
public static class ConcentradoHtmlBuilder
{
    /* Los nombres de las tablas son los nombres de los procedimientos del
       `spV2`: ExecuteLegacySpAsync nombra cada DataTable con su SP, igual que
       hacia ConsultaSpRepository en legacy. Se buscan por nombre y no por
       posicion para que reordenar el `spV2` de la base no cambie el papel. */
    private const string TablaAcumuladores = "sp_n_rptConcentradoImprimirMultiple";
    private const string TablaUsados = "sp_n_rptConcentradoUsadosImprimirMultiple";
    private const string TablaFolios = "sp_n_rptConcentradoFoliosUsadosMultiple";

    /* La cultura de los papeles portados: es-MX con los patrones de fecha y
       hora de Mac31. Para los numeros da lo mismo que la es-MX de siempre, y
       asi no hay dos culturas sueltas en el mismo documento. */
    private static readonly CultureInfo Mx = FormatoFechaLegacy.Cultura;

    /* El formato de dinero de legacy: `###,##0.00`, no "N2". */
    private const string FormatoImporte = "###,##0.00";
    private const string FormatoEntero = "###,##0";

    public static string Construir(
        string logo,
        string reporteNombre,
        DataSet datos,
        DataTable plantillas)
    {
        var p = Plantillas.Desde(plantillas);

        var acumuladores = Tabla(datos, TablaAcumuladores);
        var usados = Tabla(datos, TablaUsados);
        var folios = Tabla(datos, TablaFolios);

        /*
          LA PRIMERA TABLA MANDA, Y SE AGRUPA POR IDConcentrado.

          Legacy usa `dsReporteDatos.Tables[0]`, que es la de acumuladores, con
          GroupBy(IDConcentrado) + OrderBy(ID).First(): un juego por
          concentrado, en el orden de primera aparicion, y el renglon
          representante es el de menor ID.
        */
        var html = new StringBuilder();

        foreach (var idConcentrado in HojasDeConcentrado(acumuladores))
        {
            /*
              El filtro de legacy es `IDconcentrado = <n>`, una IGUALDAD, asi
              que el `DataTable.Select` -que ordena por la columna del filtro-
              devuelve todas las claves iguales y ADO.NET desempata por la
              posicion del renglon: queda el orden del procedimiento. (Donde eso
              NO vale es en un filtro que casa valores distintos, como el
              `NOT LIKE` de la hoja de cobro.)
            */
            var filtro = $"IDconcentrado = {idConcentrado}";
            var filasAcumuladores = Filtrar(acumuladores, filtro);
            var filasFolios = Filtrar(folios, filtro);
            var filasUsados = usados.Rows.Count > 0 ? Filtrar(usados, filtro) : Array.Empty<DataRow>();

            if (filasAcumuladores.Length == 0)
                continue;

            html.Append(ArmarHoja(p, logo, reporteNombre, filasAcumuladores, filasUsados, filasFolios));
            html.Append("<p style='page-break-before: always'></p>");
        }

        return html.ToString();
    }

    private static string ArmarHoja(
        Plantillas p,
        string logo,
        string reporteNombre,
        DataRow[] acumuladores,
        DataRow[] usados,
        DataRow[] folios)
    {
        var cabecera = acumuladores[0];
        var cuerpo = p.Cuerpo;

        cuerpo = cuerpo.Replace("#Logo#", logo);
        cuerpo = cuerpo.Replace("#Titulo#", reporteNombre);
        /* Legacy escribe `DateTime.Parse(DateTime.Now.ToString()).ToString()`,
           o sea el ToString por omision de la cultura del servidor. Eso en
           Mac31 es `dd/MM/yyyy hh:mm:ss p. m.`; ver FormatoFechaLegacy. */
        cuerpo = cuerpo.Replace("#Impresion#", FormatoFechaLegacy.FechaHora(DateTime.Now));
        cuerpo = cuerpo.Replace("#Titulo1#", "FOLIO: " + Texto(cabecera, "Folio"));
        cuerpo = cuerpo.Replace("#Titulo2#", "FECHA: <b>" + Texto(cabecera, "Fecha") + "</b>");
        cuerpo = cuerpo.Replace("#Titulo3#", "REPARTIDOR: <b>" + Texto(cabecera, "Repartidor") + "</b>");

        var usuario = "CREADO: <b>" + Texto(cabecera, "Usuario") + "</b>";
        var usuarioModifico = Texto(cabecera, "UsuarioModifico");
        if (usuarioModifico != "")
        {
            /* El pegote de &nbsp; y el acento de MODIFICÓ son los de legacy. */
            usuario += " &nbsp;&nbsp;&nbsp;&nbsp;&nbsp; " + "MODIFICÓ: <b>" + usuarioModifico + "</b>" + "&nbsp;&nbsp;";
        }

        cuerpo = cuerpo.Replace("#Titulo4#", usuario);
        cuerpo = cuerpo.Replace("#Titulo5#", "");
        cuerpo = cuerpo.Replace("#Titulo6#", "");
        cuerpo = cuerpo.Replace("#Titulo7#", "");
        cuerpo = cuerpo.Replace("#Titulo8#", "");

        /* ROTULOS FIJOS. Son los de legacy, escritos a mano, aunque el
           concentrado traiga otras marcas. No se "arreglan": el papel bueno
           seria DISTINTO del que la gente ya tiene impreso. */
        cuerpo = cuerpo.Replace("#Encabezado1#", "LTH");
        cuerpo = cuerpo.Replace("#Encabezado2#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado3#", "AMERICA");
        cuerpo = cuerpo.Replace("#Encabezado4#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado5#", "CALE");
        cuerpo = cuerpo.Replace("#Encabezado6#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado7#", "PRODUCTOS");
        cuerpo = cuerpo.Replace("#Encabezado8#", "CANT.");

        cuerpo = cuerpo.Replace("#DetalleAcumuladores#", ArmarAcumuladores(p, acumuladores));
        cuerpo = cuerpo.Replace("#DetalleProductos#", ArmarProductos(p, acumuladores));

        /* Los cuatro subtotales de columna, ya formateados por el SP. */
        cuerpo = cuerpo.Replace("#Subtotal1#", Texto(cabecera, "TotalColFtm1"));
        cuerpo = cuerpo.Replace("#Subtotal2#", Texto(cabecera, "TotalColFtm2"));
        cuerpo = cuerpo.Replace("#Subtotal3#", Texto(cabecera, "TotalColFtm3"));
        cuerpo = cuerpo.Replace("#Subtotal4#", Texto(cabecera, "TotalColFtm4"));

        var piezasAcumuladores = Entero(cabecera, "TotalCol1")
                                 + Entero(cabecera, "TotalCol2")
                                 + Entero(cabecera, "TotalCol3");

        /* `ToString()` a secas, sin separador de miles: es lo que hace legacy
           con la suma de las tres columnas. */
        cuerpo = cuerpo.Replace("#Acumuladores#", piezasAcumuladores.ToString(CultureInfo.InvariantCulture) + " PZ");
        /* Si, `#Productos#` lleva la columna de importe y `#CantidadTotal#` la
           de piezas. Se ve cambiado y asi esta en legacy. */
        cuerpo = cuerpo.Replace("#Productos#", Texto(cabecera, "TotalColFtm4"));
        cuerpo = cuerpo.Replace("#CantidadTotal#", Texto(cabecera, "Productos") + " PZ");
        cuerpo = cuerpo.Replace("#MontoTotal#", Dinero(cabecera, "TotalPagar"));
        cuerpo = cuerpo.Replace("#NumFolios#", Texto(cabecera, "NumFolios"));
        cuerpo = cuerpo.Replace("#PesoKg#", Dinero(cabecera, "PesoKg"));
        cuerpo = cuerpo.Replace("#Observaciones#", "OBSERVACIONES: " + Texto(cabecera, "Observaciones"));

        var cancelado = p.Cancelado
            .Replace("#Cancelada#", Texto(cabecera, "Cancelada"))
            .Replace("#CanceladaImagen#", Texto(cabecera, "CanceladaImagen"));
        cuerpo = cuerpo.Replace("#CanceladaDetalle#", cancelado);

        cuerpo = ArmarUsados(p, cuerpo, usados);
        cuerpo = ArmarFolios(p, cuerpo, folios);

        return cuerpo;
    }

    /*
      EL RENGLON DE ACUMULADORES SOLO SALE SI TIENE ALGO.

      Legacy concatena las seis celdas y, si la cadena resultante no esta vacia,
      pega la plantilla. El concentrado trae tantos renglones como el mas largo
      de sus tres columnas, asi que sin esta guarda saldrian decenas de
      renglones en blanco.

      OJO CON EL ORDEN DE LOS REEMPLAZOS: legacy acumula primero y reemplaza
      despues sobre TODA la cadena acumulada, lo que significa que el
      `Replace("#Dato1#", ...)` del renglon 2 ya no encuentra nada en el renglon
      1 (sus tags ya se fueron). El resultado es el mismo que reemplazar por
      renglon, y asi se escribe aqui, que es lo que se lee.
    */
    private static string ArmarAcumuladores(Plantillas p, DataRow[] filas)
    {
        var html = new StringBuilder();

        foreach (var fila in filas)
        {
            var cadena = Texto(fila, "LTH") + Texto(fila, "LTHCantidad")
                       + Texto(fila, "AME") + Texto(fila, "AMECantidad")
                       + Texto(fila, "CALE") + Texto(fila, "CALECantidad");

            if (cadena == "")
                continue;

            html.Append(p.DetalleAcumuladores
                .Replace("#Dato1#", Texto(fila, "LTH"))
                .Replace("#Dato2#", Texto(fila, "LTHCantidad"))
                .Replace("#Dato3#", Texto(fila, "AME"))
                .Replace("#Dato4#", Texto(fila, "AMECantidad"))
                .Replace("#Dato5#", Texto(fila, "CALE"))
                .Replace("#Dato6#", Texto(fila, "CALECantidad")));
        }

        return html.ToString();
    }

    private static string ArmarProductos(Plantillas p, DataRow[] filas)
    {
        var html = new StringBuilder();

        foreach (var fila in filas)
        {
            if (Texto(fila, "PROD") + Texto(fila, "PRODCantidadFtm") == "")
                continue;

            html.Append(p.DetalleProductos
                .Replace("#Dato7#", Texto(fila, "PROD"))
                .Replace("#Dato8#", Texto(fila, "PRODCantidadFtm")));
        }

        return html.ToString();
    }

    private static string ArmarUsados(Plantillas p, string cuerpo, DataRow[] filas)
    {
        var html = new StringBuilder();
        var total = 0;
        var monto = 0m;

        foreach (var fila in filas)
        {
            html.Append(p.DetalleUsados
                /* La columna se llama "Tipo de Usado", CON espacios. */
                .Replace("#TipoUsado#", Texto(fila, "Tipo de Usado"))
                .Replace("#CantidadUsadoCredito#", Texto(fila, "CantidadUsadoCredito"))
                .Replace("#TotalUsadoCredito#", Dinero(fila, "TotalUsadoCredito")));

            total += Entero(fila, "CantidadUsadoCredito");
            monto += Decimal_(fila, "TotalUsadoCredito");
        }

        var detalle = html.ToString();
        if (detalle == "")
        {
            /* El letrero espaciado letra por letra, tal cual lo escribe legacy. */
            detalle = "<tr><td colspan='3'><b> &nbsp; S &nbsp; I &nbsp; N &nbsp; &nbsp; &nbsp; U &nbsp; S &nbsp; A &nbsp; D &nbsp; O &nbsp; S</b></td></tr>";
        }

        cuerpo = cuerpo.Replace("#DetalleUsados#", detalle);
        /* En cero salen VACIOS, no "0" ni "0.00". */
        cuerpo = cuerpo.Replace("#UsadosTotal#", total > 0 ? total.ToString(FormatoEntero, Mx) : "");
        cuerpo = cuerpo.Replace("#UsadosMontoTotal#", monto > 0 ? monto.ToString(FormatoImporte, Mx) : "");

        return cuerpo;
    }

    private static string ArmarFolios(Plantillas p, string cuerpo, DataRow[] filas)
    {
        var html = new StringBuilder();
        var num = 0;
        var productos = 0;
        var numUsados = 0;
        var pesoKg = 0m;
        var totalPagar = 0m;
        var montoUsados = 0m;

        foreach (var fila in filas)
        {
            var nombre = Texto(fila, "NombreCliente");
            /* A 40 caracteres, como legacy. */
            if (nombre.Length > 40)
                nombre = nombre.Substring(0, 40);

            html.Append(p.DetalleFolios
                .Replace("#DetalleEmpresa#", Texto(fila, "Empresa"))
                .Replace("#DetalleFolio#", Texto(fila, "FolioVentaFtm"))
                .Replace("#DetalleFechaEmision#", Texto(fila, "FechaEmisionCorto"))
                .Replace("#DetalleNombreCliente#", nombre)
                .Replace("#DetalleProductos#", Texto(fila, "Productos"))
                .Replace("#DetallePesoKg#", Dinero(fila, "PesoKg"))
                /* Las dos columnas con espacios en el nombre son de legacy. */
                .Replace("#DetalleTotalPagar#", Dinero(fila, "Total a Pagar"))
                .Replace("#DetalleMontoUsados#", Dinero(fila, "TotalUsadoCredito"))
                .Replace("#DetalleNumUsados#", Texto(fila, "CantidadUsadoCredito")));

            num += 1;
            productos += Entero(fila, "Productos");
            pesoKg += Decimal_(fila, "PesoKg");
            totalPagar += Decimal_(fila, "Total a Pagar");
            montoUsados += Decimal_(fila, "TotalUsadoCredito");
            numUsados += Entero(fila, "CantidadUsadoCredito");
        }

        cuerpo = cuerpo.Replace("#DetalleFolios#", html.ToString());
        /* `ToString()` a secas en los dos contadores, como legacy. */
        cuerpo = cuerpo.Replace("#FoliosNum#", num.ToString(CultureInfo.InvariantCulture));
        cuerpo = cuerpo.Replace("#FoliosProductos#", productos.ToString(CultureInfo.InvariantCulture));
        cuerpo = cuerpo.Replace("#FoliosPesoKg#", pesoKg.ToString(FormatoImporte, Mx));
        cuerpo = cuerpo.Replace("#FoliosTotalPagar#", totalPagar.ToString(FormatoImporte, Mx));
        cuerpo = cuerpo.Replace("#FoliosMontoUsados#", montoUsados.ToString(FormatoImporte, Mx));
        cuerpo = cuerpo.Replace("#FoliosNumUsados#", numUsados.ToString(FormatoEntero, Mx));

        /*
          EL BLOQUE DE TAURO, Y EL DEFECTO QUE NO SE PORTA.

          Legacy lo lee de `Rows[0]` sin comprobar que haya renglones, asi que un
          concentrado sin folios le lanza "no rows" y su catch devuelve la
          excepcion en lugar del papel. Aqui queda en blanco y el resto de la
          hoja sale: copiar un crash no imprime nada.
        */
        cuerpo = cuerpo.Replace(
            "#htmlTauroDetalleFoliosA#",
            filas.Length > 0 ? Texto(filas[0], "htmlTauroDetalleFoliosA") : "");

        return cuerpo;
    }

    /* ------------------------------------------------------------------ */
    /* Piezas sueltas                                                      */
    /* ------------------------------------------------------------------ */

    /* El orden de los grupos es el de primera aparicion y el representante es
       el de menor ID, como el GroupBy/OrderBy/First de legacy. */
    private static IEnumerable<string> HojasDeConcentrado(DataTable acumuladores)
    {
        if (acumuladores.Rows.Count == 0 || !acumuladores.Columns.Contains("IDConcentrado"))
            return Array.Empty<string>();

        return acumuladores.AsEnumerable()
            .GroupBy(fila => Texto(fila, "IDConcentrado"))
            .Select(grupo => Texto(grupo.OrderBy(fila => Entero(fila, "ID")).First(), "IDConcentrado"))
            .Where(id => id.Length > 0)
            .ToArray();
    }

    private static DataTable Tabla(DataSet datos, string nombre)
    {
        return datos.Tables.Contains(nombre) ? datos.Tables[nombre]! : new DataTable(nombre);
    }

    private static DataRow[] Filtrar(DataTable tabla, string filtro)
    {
        if (tabla.Rows.Count == 0)
            return Array.Empty<DataRow>();

        try
        {
            return tabla.Select(filtro);
        }
        catch
        {
            /* La tabla no tiene la columna del filtro. Legacy reventaria; aqui
               la hoja sale sin ese bloque. */
            return Array.Empty<DataRow>();
        }
    }

    private static string Texto(DataRow? fila, string columna)
    {
        if (fila is null || !fila.Table.Columns.Contains(columna))
            return "";
        /* Legacy escribe `fila[columna].ToString()` a pelo, asi que una columna
           datetime sale con la cultura del servidor. Ver FormatoFechaLegacy. */
        return FormatoFechaLegacy.Celda(fila[columna]);
    }

    private static int Entero(DataRow? fila, string columna)
    {
        return int.TryParse(Texto(fila, columna), NumberStyles.Any, Mx, out var n) ? n : 0;
    }

    private static decimal Decimal_(DataRow? fila, string columna)
    {
        return decimal.TryParse(Texto(fila, columna), NumberStyles.Any, Mx, out var n) ? n : 0m;
    }

    private static string Dinero(DataRow? fila, string columna)
    {
        return Decimal_(fila, columna).ToString(FormatoImporte, Mx);
    }

    /*
      LAS SEIS PIEZAS DEL CONCENTRADO EN `Template_Html`.

      Son las mismas seis que lee SetHtml de ReporteConcentradoService y por eso
      el motor generico no puede con este reporte: ninguna se llama `cuerpo_n`.
    */
    private sealed class Plantillas
    {
        public string Cuerpo { get; private init; } = "";
        public string DetalleAcumuladores { get; private init; } = "";
        public string DetalleProductos { get; private init; } = "";
        public string DetalleUsados { get; private init; } = "";
        public string DetalleFolios { get; private init; } = "";
        public string Cancelado { get; private init; } = "";

        public static Plantillas Desde(DataTable plantillas)
        {
            return new Plantillas
            {
                Cuerpo = Leer(plantillas, "concentrado_cuerpo"),
                DetalleAcumuladores = Leer(plantillas, "concentrado_detalle_acumuladores"),
                DetalleProductos = Leer(plantillas, "concentrado_detalle_productos"),
                DetalleUsados = Leer(plantillas, "concentrado_detalle_usados"),
                DetalleFolios = Leer(plantillas, "concentrado_detalle_folios"),
                Cancelado = Leer(plantillas, "concentrado_cancelado")
            };
        }

        private static string Leer(DataTable plantillas, string descripcion)
        {
            var filas = plantillas.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
            if (filas.Length == 0)
                throw new InvalidOperationException(
                    $"Falta la plantilla '{descripcion}' del concentrado (Template_Html, IDTipoTemplateHtml = 1005).");
            return Convert.ToString(filas[0]["html"]) ?? "";
        }
    }
}
