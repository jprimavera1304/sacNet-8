using System.Data;
using System.Globalization;
using System.Text;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  EL DOCUMENTO DE UNA COMPRA (1015)
  ══════════════════════════════════════════════════════════════════════

  ES EL PAPEL DEL BOTON IMPRIMIR Y DEL DOBLE CLIC DE "CONSULTAR COMPRAS".

  En Mac31 los dos abren lo mismo: `btnImprimir_Click` arma
  `IDReporte=1015&StoredProcParam=<IDCompra>||&MostrarReporte=0` y
  `dataGridView1_CellDoubleClick` llama a ese mismo boton cuando el doble clic
  cae en una columna que no es la primera -la del check-
  (ConsultarCompras.cs:687 y :729). Imprime LA FILA EN LA QUE ESTAS
  (`CurrentRow`), no las marcadas; eso lo hace la comprobacion (1017), que es
  otro reporte.

  POR QUE NO LO PINTA LA PLANTILLA GENERICA
  -----------------------------------------
  Porque el 1015 es de la generacion vieja y se nota en tres cosas:

    * `spV2` esta VACIO en las dos bases. Los datos los trae la columna `SP`,
      y no con un procedimiento sino con TRES:
      `sp_rptCompraDetalle|sp_rptComprasUsadosCargoMultiple|sp_rptComprasUsadosCreditoMultiple`.
      Por eso ConsultarConfiguracionAsync cae a `SP` cuando `spV2` esta vacio.
    * Sus procedimientos NO reciben el `@Params` generico: reciben
      `@IDCompra1, @IDCompra2, @IDCompra3` posicionales, y legacy les manda
      `<IDCompra>||`, o sea el id en el primero y los otros dos vacios. Eso lo
      resuelve ArgumentosDelProcedimiento.
    * Sus cuatro piezas de `Template_Html` se llaman `cuerpo`, `detalle`,
      `usados_credito_encabezado` y `usados_credito_detalle`, y el formato
      generico busca `cuerpo_n` / `detalle` / `totales`. Ademas necesita
      cruzar tres tablas en una hoja, y el generico pinta un renglon por fila
      de UNA.

  ESTO ES CompraService.cs PORTADO -el del servidor de reportes de legacy-,
  con su mismo recorrido, sus mismos tags y sus mismos formatos. Las rarezas
  que se copian a proposito:

    * UNA SOLA HOJA. `sp_rptCompraDetalle` filtra con `WHERE C.IDCompra =
      @IDCompra1`, una igualdad, asi que aunque el parametro sea varchar solo
      sale una compra. Las otras dos tablas si saben de listas separadas por
      `~`, pero nadie les manda una.
    * EL TITULO DE LOS USADOS DEPENDE DEL PEDIDO: si la compra NO viene de un
      pedido (`IDPedido = 0`) el bloque se llama "U S A D O S   C R E D I T O S"
      y su total "CREDITOS"; si viene de un pedido se llama
      "B O N I F I C A C I O N". Lo decide la primera fila del detalle.
    * LAS OBSERVACIONES SE ABREN EN NEGRITA Y NO SE CIERRAN: legacy escribe
      `"Observaciones: <b>" + texto` sin `</b>`. Se copia porque el `<b>` sin
      cerrar es lo que pone en negrita el resto del pie en el papel que la
      gente conoce.
    * EL RENGLON DE TOTAL DE LOS USADOS SALE SIEMPRE, aunque no haya ni un
      usado: es una fila con las dos celdas en negrita a cero.
    * `#Impresion#` va en VEINTICUATRO horas (`dd/MM/yyyy HH:mm:ss`), armada a
      mano con `Hours/Minutes/Seconds`, no con el `ToString()` de la cultura.
      Por eso aqui no se usa FormatoFechaLegacy.FechaHora para ese tag.
    * LA HOJA EMPIEZA CON UN SALTO DE PAGINA y termina con otro, igual que en
      legacy.

  DOS DEFECTOS DE LEGACY QUE NO SE PORTAN, Y SE DICEN
  --------------------------------------------------
    1. LOS CARGOS DE USADOS SALEN CON LOS TAGS EN CRUDO. Legacy reusa la
       plantilla `detalle` para esas filas pero sustituye `#Concepto#`,
       `#Cantidad#` y nada mas; la plantilla dice `#ConceptoFtm#` y
       `#CantidadFtm#`, que NO contienen esos textos, asi que en el papel
       quedan escritos "#ConceptoFtm#" y "#CantidadFtm#" tal cual. Aqui se
       sustituyen los tags de verdad. Imprimir un tag no es un formato, es un
       papel roto.
    2. UN PESO NULO TIRABA EL REPORTE ENTERO. `prod.PesoKg` no lleva ISNULL en
       el procedimiento, y legacy hace `decimal.Parse(dr["PesoKg"].ToString())`:
       con un producto sin peso capturado eso es una FormatException y su catch
       devuelve el texto de la excepcion en vez del documento. Aqui un nulo
       vale cero.
*/
public static class CompraHtmlBuilder
{
    /* Los nombres de las tablas son los nombres de los procedimientos de la
       columna `SP`: ExecuteLegacySpAsync nombra cada DataTable con el suyo,
       igual que hacia ConsultaSpRepository en legacy. Se buscan por nombre y
       no por posicion para que reordenar esa columna no cambie el papel. */
    private const string TablaDetalle = "sp_rptCompraDetalle";
    private const string TablaUsadosCargo = "sp_rptComprasUsadosCargoMultiple";
    private const string TablaUsadosCredito = "sp_rptComprasUsadosCreditoMultiple";

    private const string FormatoImporte = "###,###,##0.00";
    private const string FormatoEntero = "###,###,##0";
    private const string FormatoPeso = "###,###,##0.0000";

    private static readonly CultureInfo Mx = FormatoFechaLegacy.Cultura;

    public static string Construir(
        string logo,
        string reporteNombre,
        DataSet datos,
        DataTable plantillas)
    {
        var p = Plantillas.Desde(plantillas);
        var detalle = Tabla(datos, TablaDetalle);

        /*
          SIN PARTIDAS, LA HOJA DE "NO SE ENCONTRO INFORMACION".

          Legacy pinta el encabezado con la fila en null -o sea sin factura ni
          proveedor- y mete ese cartel en `#Detalle#`. Pasa cuando el IDCompra
          no existe. Se copia el cartel, pero el encabezado se arma sin fila
          para no reventar leyendo columnas de una fila que no hay.
        */
        if (detalle.Rows.Count == 0)
        {
            var vacio = Encabezado(p.Cuerpo, logo, reporteNombre, null);
            vacio = vacio.Replace(
                "#Detalle#",
                "<center> <br /><br /> <u> <h1> No se encontro información </h1> </u> </center>");
            return LimpiarTagsSobrantes(vacio);
        }

        var cabecera = detalle.Rows[0];

        var html = new StringBuilder();
        html.Append("<p style='page-break-before: always'></p>");
        var cuerpo = Encabezado(p.Cuerpo, logo, reporteNombre, cabecera);

        // ── Partidas de la compra ────────────────────────────────────────
        var filas = new StringBuilder();
        foreach (DataRow fila in detalle.Rows)
        {
            var renglon = p.Detalle;
            renglon = renglon.Replace("#Codigo#", Texto(fila, "Codigo"));
            renglon = renglon.Replace("#ConceptoFtm#", Texto(fila, "ConceptoFtm"));
            renglon = renglon.Replace("#PrecioLista#", Importe(fila, "PrecioLista"));
            renglon = renglon.Replace("#PesoKg#", Importe(fila, "PesoKg"));
            renglon = renglon.Replace("#CantidadFtm#", Texto(fila, "CantidadFtm"));
            renglon = renglon.Replace("#Descuento#", Importe(fila, "Descuento"));
            renglon = renglon.Replace("#UnidadMedida#", Texto(fila, "UnidadMedida"));
            renglon = renglon.Replace("#ImportePartida#", Importe(fila, "ImportePartida"));
            renglon = renglon.Replace("#ProductosPesoKg#", Importe(fila, "ProductosPesoKg"));
            filas.Append(renglon);
        }

        /*
          ── Cargos de usados, pegados debajo de las partidas ──────────────

          Van en la MISMA plantilla `detalle` que los productos, con el codigo
          en blanco y sin peso: es una partida mas de la factura. El orden es
          por IDCompraUsadoCargo, como lo ordena legacy antes de recorrerlos.
        */
        foreach (var fila in OrdenadoPor(Tabla(datos, TablaUsadosCargo), "IDCompraUsadoCargo"))
        {
            var renglon = p.Detalle;
            renglon = renglon.Replace("#Codigo#", "");
            renglon = renglon.Replace("#ConceptoFtm#", Texto(fila, "TipoUsadoCargo"));
            renglon = renglon.Replace("#PrecioLista#", Importe(fila, "CostoUsadoCargo"));
            renglon = renglon.Replace("#PesoKg#", "");
            renglon = renglon.Replace("#CantidadFtm#", Entero(fila, "CantidadUsadoCargo"));
            renglon = renglon.Replace("#Descuento#", "");
            renglon = renglon.Replace("#UnidadMedida#", "");
            renglon = renglon.Replace("#ImportePartida#", Importe(fila, "ImporteUsadoCargo"));
            renglon = renglon.Replace("#ProductosPesoKg#", "");
            filas.Append(renglon);
        }

        cuerpo = cuerpo.Replace("#Detalle#", filas.ToString());

        var observaciones = Texto(cabecera, "observaciones");
        cuerpo = cuerpo.Replace(
            "#Observaciones#",
            observaciones.Length == 0 ? "" : "Observaciones: <b>" + observaciones);

        // ── Creditos (o bonificacion) de usados ──────────────────────────
        cuerpo = cuerpo.Replace("#UsadosCredito#", UsadosCredito(p, datos));

        /*
          EL ROTULO DEL BLOQUE DE USADOS LO DECIDE EL PEDIDO.

          Se sustituye DESPUES de pegar `#UsadosCredito#` porque el
          `#TituloUsados#` vive dentro de esa plantilla, no en el cuerpo.
        */
        var deUnPedido = Numero(cabecera, "IDPedido") != 0;
        cuerpo = cuerpo.Replace(
            "#TituloUsados#",
            deUnPedido
                ? "B O N I F I C A C I Ó N"
                : "U S A D O S  &nbsp;&nbsp; C R É D I T O S");
        cuerpo = cuerpo.Replace("#TituloTotalUsados#", deUnPedido ? "BONIFICACIÓN" : "CRÉDITOS");

        cuerpo = cuerpo.Replace("#CanceladaDetalle#", Texto(cabecera, "CanceladaDetalle"));
        cuerpo = cuerpo.Replace("#PesoKgTotal#", Decimal(cabecera, "PesoKgTotal", FormatoPeso));
        cuerpo = cuerpo.Replace("#CantidadTotalFtm#", Texto(cabecera, "CantidadTotalFtm"));

        html.Append(LimpiarTagsSobrantes(cuerpo));
        html.Append("<p style='page-break-before: always'></p>");
        return html.ToString();
    }

    private static string UsadosCredito(Plantillas p, DataSet datos)
    {
        var filas = new StringBuilder();
        var cantidad = 0;
        var total = 0m;

        foreach (var fila in OrdenadoPor(Tabla(datos, TablaUsadosCredito), "IDCompraUsadoCredito"))
        {
            var renglon = p.UsadosCreditoDetalle;
            renglon = renglon.Replace("#TipoUsadoCredito#", Texto(fila, "TipoUsadoCredito"));
            renglon = renglon.Replace("#CantidadUsadoCredito#", Entero(fila, "CantidadUsadoCredito"));
            renglon = renglon.Replace("#TotalUsadoCredito#", Importe(fila, "TotalUsadoCredito"));
            filas.Append(renglon);

            cantidad += (int)Numero(fila, "CantidadUsadoCredito");
            total += Decimal(fila, "TotalUsadoCredito");
        }

        /* El renglon de totales: siempre, aunque la tabla venga vacia. */
        var suma = p.UsadosCreditoDetalle;
        suma = suma.Replace("#TipoUsadoCredito#", "");
        suma = suma.Replace("#CantidadUsadoCredito#", "<b>" + cantidad.ToString(FormatoEntero, Mx) + "</b>");
        suma = suma.Replace("#TotalUsadoCredito#", "<b>" + total.ToString(FormatoImporte, Mx) + "</b>");
        filas.Append(suma);

        return p.UsadosCreditoEncabezado.Replace("#DetalleUsadosCredito#", filas.ToString());
    }

    private static string Encabezado(string plantilla, string logo, string reporteNombre, DataRow? fila)
    {
        var cuerpo = plantilla;
        cuerpo = cuerpo.Replace("#Logo#", logo);
        cuerpo = cuerpo.Replace("#Titulo#", reporteNombre);

        /* Legacy arma esta en 24 horas a mano, celda por celda. Ver la nota de
           arriba: aqui no vale la cultura de los otros papeles. */
        var ahora = DateTime.Now;
        cuerpo = cuerpo.Replace(
            "#Impresion#",
            ahora.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));

        if (fila is null)
            return cuerpo;

        cuerpo = cuerpo.Replace("#Factura#", "Factura:&nbsp;<b>" + Texto(fila, "Factura") + "</b>");
        cuerpo = cuerpo.Replace("#FechaCaptura#", "Fecha de Captura:&nbsp;<b>" + Texto(fila, "FechaCaptura") + "</b>");
        cuerpo = cuerpo.Replace("#FechaEmision#", "Fecha de Emision:&nbsp;<b>" + SoloFecha(fila, "FechaEmision") + "</b>");
        cuerpo = cuerpo.Replace("#FechaVencimiento#", "Fecha de Vencimiento:&nbsp;<b>" + SoloFecha(fila, "FechaVencimiento") + "</b>");
        cuerpo = cuerpo.Replace("#Proveedor#", "Proveedor:&nbsp;<b>" + Texto(fila, "Proveedor") + "</b>");
        cuerpo = cuerpo.Replace("#Almacen#", "Almacen:&nbsp;<b>" + Texto(fila, "Almacen") + "</b>");
        cuerpo = cuerpo.Replace("#Empresa#", "Empresa:&nbsp;<b>" + Texto(fila, "RazonSocial") + "</b>");

        cuerpo = cuerpo.Replace("#Subtotal#", Importe(fila, "SubTotal"));
        cuerpo = cuerpo.Replace("#IVA#", Importe(fila, "IVA"));
        cuerpo = cuerpo.Replace("#Importe#", Importe(fila, "Importe"));
        cuerpo = cuerpo.Replace("#Creditos#", Importe(fila, "Creditos"));
        cuerpo = cuerpo.Replace("#TotalPagar#", "<b>" + Importe(fila, "TotalPagar") + "</b>");

        return cuerpo;
    }

    /*
      LOS TAGS QUE LA PLANTILLA TRAE Y EL RECORRIDO NO TOCA.

      La hoja de "no se encontro informacion" deja sin sustituir los nueve del
      encabezado, y el `#Observaciones#` del pie se sustituye solo cuando hay
      partidas. Dejarlos escritos imprimiria "#Factura#" en el papel, que es
      exactamente el defecto que se arreglo en los cargos de usados.
    */
    private static string LimpiarTagsSobrantes(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "#[A-Za-z][A-Za-z0-9_]*#", "");
    }

    private static DataTable Tabla(DataSet datos, string nombre)
    {
        return datos.Tables.Contains(nombre) ? datos.Tables[nombre]! : new DataTable(nombre);
    }

    /* El `DefaultView.Sort` de legacy, sin tocar la vista de la tabla. */
    private static DataRow[] OrdenadoPor(DataTable tabla, string columna)
    {
        if (tabla.Rows.Count == 0 || !tabla.Columns.Contains(columna))
            return tabla.Rows.Cast<DataRow>().ToArray();

        return tabla.Rows.Cast<DataRow>()
            .OrderBy(f => Numero(f, columna))
            .ToArray();
    }

    private static string Texto(DataRow fila, string columna)
    {
        return fila.Table.Columns.Contains(columna)
            ? FormatoFechaLegacy.Celda(fila[columna])
            : "";
    }

    /* `dr["FechaEmision"].ToString().Split(' ')[0]` de legacy. */
    private static string SoloFecha(DataRow fila, string columna)
    {
        var valor = Texto(fila, columna);
        var corte = valor.IndexOf(' ');
        return corte < 0 ? valor : valor.Substring(0, corte);
    }

    private static decimal Decimal(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value)
            return 0m;

        return decimal.TryParse(
            Convert.ToString(fila[columna], CultureInfo.InvariantCulture),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var valor)
            ? valor
            : 0m;
    }

    private static string Decimal(DataRow fila, string columna, string formato)
    {
        return Decimal(fila, columna).ToString(formato, Mx);
    }

    private static string Importe(DataRow fila, string columna)
    {
        return Decimal(fila, columna).ToString(FormatoImporte, Mx);
    }

    private static string Entero(DataRow fila, string columna)
    {
        return Numero(fila, columna).ToString(FormatoEntero, Mx);
    }

    private static long Numero(DataRow fila, string columna)
    {
        return (long)decimal.Truncate(Decimal(fila, columna));
    }

    private sealed class Plantillas
    {
        public string Cuerpo { get; private init; } = "";
        public string Detalle { get; private init; } = "";
        public string UsadosCreditoEncabezado { get; private init; } = "";
        public string UsadosCreditoDetalle { get; private init; } = "";

        public static Plantillas Desde(DataTable plantillas)
        {
            return new Plantillas
            {
                Cuerpo = Leer(plantillas, "cuerpo"),
                Detalle = Leer(plantillas, "detalle"),
                UsadosCreditoEncabezado = Leer(plantillas, "usados_credito_encabezado"),
                UsadosCreditoDetalle = Leer(plantillas, "usados_credito_detalle")
            };
        }

        private static string Leer(DataTable plantillas, string descripcion)
        {
            var filas = plantillas.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
            if (filas.Length == 0)
                throw new InvalidOperationException(
                    $"Falta la plantilla '{descripcion}' de la compra (Template_Html, IDTipoTemplateHtml = 1015).");
            return Convert.ToString(filas[0]["html"]) ?? "";
        }
    }
}
