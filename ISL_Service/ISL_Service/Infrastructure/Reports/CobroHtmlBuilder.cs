using System.Data;
using System.Globalization;
using System.Text;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  LA HOJA DE COBRO (REPORTE 1006)
  ══════════════════════════════════════════════════════════════════════

  Es `enumReportes.Cobro` de Mac31: el papel que imprime ConsultarCobros con el
  boton IMPRIMIR COBRO sobre las hojas marcadas. Hasta ahora no se podia pedir
  desde el web y quedaba anotado como hueco en dos sitios del front
  (nucleo/reportesDeCobranza.ts y nucleo/dineroUsados.ts).

  ──────────────────────────────────────────────────────────────────────
  POR QUE NECESITA ARMADOR PROPIO
  ──────────────────────────────────────────────────────────────────────

  El motor generico de reportes pinta UNA tabla con la plantilla `rptTemplate`:
  cuerpo, detalle, totales, y un `#Columna#` por cada columna del SELECT. Este
  reporte no es eso. Son TRES procedimientos (lo dice el `spV2` de su renglon en
  `Reportes`, separados por tuberias):

      sp_n_rptConcentradoCobroImprimirMultiple   los acumuladores y los productos
      sp_n_rptConcentradoUsadosImprimirMultiple  los cascos por grupo (G1..G7)
      sp_n_rptConcentradoFoliosUsadosMultiple    la rejilla de folios y el pie

  y ONCE plantillas (`cobro_*` en `Template_Html` con IDTipoTemplateHtml = 1006),
  que se combinan en un orden que ninguna plantilla generica sabe expresar: el
  bloque de acumuladores esta PIVOTEADO en catorce columnas (Marca1..Marca14), y
  la rejilla de folios se recorre TRES veces sobre la misma tabla con tres
  filtros distintos.

  ──────────────────────────────────────────────────────────────────────
  ES UN PORTE, NO UN DISEÑO NUEVO, Y ESO ES LO QUE LO HACE CORRECTO
  ──────────────────────────────────────────────────────────────────────

  La fuente es
  Legacy/MacServicios2/Mac3Servicios/Models/Service/ReportesServ/ReporteCobroService.cs,
  que es el armador que hoy saca el papel de verdad cuando se imprime desde
  Mac31. Se porta paso por paso, con los MISMOS formatos de numero y las MISMAS
  plantillas leidas de la MISMA base.

  No se "mejora" nada a proposito, y hay dos sitios donde la tentacion es fuerte
  (el orden de las marcas y los renglones en blanco): van anotados abajo. El
  reporte tiene que salir IDENTICO al de Mac31 porque el mismo papel se imprime
  desde los dos programas y el repartidor firma encima. Un reporte "mejor" que no
  coincide con el de legacy es un reporte que nadie puede comparar.
*/
public static class CobroHtmlBuilder
{
    /* Los nombres de las tres tablas son los nombres de los procedimientos:
       ExecuteLegacySpAsync nombra cada DataTable con su SP, igual que hacia
       ConsultaSpRepository en legacy. Por eso se buscan por nombre y no por
       posicion: si alguien reordena el `spV2` de la base, esto sigue bien. */
    private const string TablaAcumuladores = "sp_n_rptConcentradoCobroImprimirMultiple";
    private const string TablaUsados = "sp_n_rptConcentradoUsadosImprimirMultiple";
    private const string TablaFolios = "sp_n_rptConcentradoFoliosUsadosMultiple";

    /* El relleno con el que se ordenan las marcas vacias. Ver OrdenarPorMarca. */
    private const string TextoZ = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";

    /* El formato de dinero de legacy. No es "N2": `###,##0.00` deja el cero como
       "0.00" y no como "0", y es el que esta en las cuarenta llamadas de
       ReporteCobroService. La cultura va fija a es-MX porque el servidor puede
       levantarse con la de la maquina y entonces el punto decimal cambia de
       sitio: el mismo reporte diria 9,353.00 aqui y 9.353,00 alla.

       Se toma de FormatoFechaLegacy, que es la misma es-MX pero con los
       patrones de fecha y hora clavados a los de Mac31. Para los numeros da
       exactamente lo mismo; se comparte para que no haya dos culturas sueltas
       en el mismo papel. */
    private static readonly CultureInfo Mx = FormatoFechaLegacy.Cultura;

    public static string Construir(
        string logo,
        string logoMarcaDeAgua,
        string reporteNombre,
        DataSet datos,
        DataTable plantillas)
    {
        var p = Plantillas.Desde(plantillas);

        var acumuladores = Tabla(datos, TablaAcumuladores);
        var usados = Tabla(datos, TablaUsados);
        var folios = Tabla(datos, TablaFolios);

        var acumuladoresOrdenados = OrdenarPorMarca(acumuladores);

        var html = new StringBuilder();

        /*
          UNA HOJA POR CONCENTRADO.

          Se agrupa la tabla de folios por IDConcentrado y se toma su primer
          renglon (el de menor ID), que es el que trae los datos de cabecera de
          esa hoja: repartidor, folio del cobro, usuario, observaciones. Es lo
          que hace legacy con el GroupBy/OrderBy/First de LINQ.
        */
        foreach (var cabecera in HojasDeCobro(folios))
        {
            var idConcentrado = Texto(cabecera, "IDConcentrado");
            var idCobranza = Texto(cabecera, "IDCobranza");

            /*
              DE QUE HOJA SON LOS RENGLONES.

              Normalmente se filtra por IDConcentrado. Pero una hoja puede no
              tener concentrado —cuando todo lo que trae son folios de cobranza
              ya asignada— y entonces se filtra por IDCobranza. Es la misma
              condicion de legacy, literal.
            */
            var filtro = !string.IsNullOrEmpty(idConcentrado)
                ? $"IDConcentrado = {idConcentrado}"
                : idCobranza != "0" ? $"IDCobranza = {idCobranza}" : "";

            var foliosDeLaHoja = Filtrar(folios, filtro);
            if (foliosDeLaHoja.Length == 0)
                continue;

            /*
              La tabla de acumuladores NO tiene columna IDCobranza (su #Concentrado
              temporal solo lleva IDConcentrado), asi que cuando el filtro es por
              cobranza el Select revienta. Legacy se protege solo en la tabla de
              usados y deja reventar esta; aqui se protegen las dos: una hoja sin
              concentrado tiene cero acumuladores, que es justo lo que devuelve un
              filtro fallido vacio, y no hay motivo para contestar un 500.
            */
            var acumuladoresDeLaHoja = Filtrar(acumuladoresOrdenados, filtro);
            var usadosDeLaHoja = Filtrar(usados, filtro);

            html.Append(ArmarHoja(
                logo,
                logoMarcaDeAgua,
                reporteNombre,
                p,
                cabecera,
                foliosDeLaHoja,
                acumuladoresDeLaHoja,
                usadosDeLaHoja));

            /* El salto entre hojas, como legacy: cada cobro empieza en papel
               nuevo aunque el anterior haya ocupado tres renglones. */
            html.Append("<p style='page-break-before: always'></p>");
        }

        return html.ToString();
    }

    /* ------------------------------------------------------------------ */
    /* Una hoja                                                            */
    /* ------------------------------------------------------------------ */

    private static string ArmarHoja(
        string logo,
        string logoMarcaDeAgua,
        string reporteNombre,
        Plantillas p,
        DataRow cabecera,
        DataRow[] folios,
        DataRow[] acumuladores,
        DataRow[] usados)
    {
        var cuerpo = p.Cuerpo;

        /* ---------------------------------------------------------- */
        /* El encabezado                                               */
        /* ---------------------------------------------------------- */

        cuerpo = cuerpo.Replace("#Logo#", logo);
        cuerpo = cuerpo.Replace("#Titulo#", reporteNombre);
        /* La fecha de impresion es la de AHORA, no la del cobro: sirve para
           distinguir dos copias del mismo folio sacadas en dias distintos. */
        cuerpo = cuerpo.Replace("#Impresion#", FormatoFechaLegacy.FechaHora(DateTime.Now));
        cuerpo = cuerpo.Replace("#FolioCobro#", Texto(cabecera, "FolioCobro"));
        cuerpo = cuerpo.Replace("#FechaCobroFtm#", Texto(cabecera, "FechaCobro"));
        cuerpo = cuerpo.Replace("#Titulo3#", $"REPARTIDOR: <b>{Texto(cabecera, "Repartidor")}</b>");

        /* CREADO POR, y MODIFICO solo si alguien la modifico. Un rotulo
           "MODIFICÓ:" con nada al lado se lee como si la hubieran tocado. */
        var quien = $"CREADO POR: <b>{Texto(cabecera, "Usuario")}</b>";
        var modifico = Texto(cabecera, "UsuarioModifico");
        if (modifico != "")
        {
            quien += " &nbsp;&nbsp;&nbsp;&nbsp;&nbsp; " +
                     $"MODIFICÓ: <b>{modifico}&nbsp;&nbsp;&nbsp;{Texto(cabecera, "FechaModificacion")}</b>";
        }
        cuerpo = cuerpo.Replace("#Titulo4#", quien);

        cuerpo = cuerpo.Replace("#Titulo5#", "");
        cuerpo = cuerpo.Replace("#Titulo6#", "");
        cuerpo = cuerpo.Replace("#Titulo7#", "");
        cuerpo = cuerpo.Replace("#Titulo8#", "");

        /*
          LOS SEIS ROTULOS DE LAS COLUMNAS DE ACUMULADORES VAN FIJOS.

          Y son fijos en legacy tambien, que es lo que sorprende: la plantilla
          declara seis encabezados y el armador les mete "LTH / CANT. / AMERICA /
          CANT. / CALE / CANT." sin mirar los datos. Las marcas de verdad
          (AMERICA, FULL-POWER, LTH, MOTO AMERICA…) salen DENTRO de cada tabla
          del bloque, de la columna Marca de cada renglon, y por eso la hoja se
          lee bien aunque el rotulo de arriba diga otra cosa.

          Se copia tal cual. Cambiarlo por las marcas reales daria un papel mejor
          pero DISTINTO al de Mac31, y este es el documento que se compara folio
          a folio con el que trae el repartidor.
        */
        cuerpo = cuerpo.Replace("#Encabezado1#", "LTH");
        cuerpo = cuerpo.Replace("#Encabezado2#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado3#", "AMERICA");
        cuerpo = cuerpo.Replace("#Encabezado4#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado5#", "CALE");
        cuerpo = cuerpo.Replace("#Encabezado6#", "CANT.");
        cuerpo = cuerpo.Replace("#Encabezado7#", "PRODUCTOS");
        cuerpo = cuerpo.Replace("#Encabezado8#", "CANT.");

        /* ---------------------------------------------------------- */
        /* TOTAL DE ACUMULADORES: una tabla por marca                  */
        /* ---------------------------------------------------------- */

        var totalColumnas = Entero(acumuladores.FirstOrDefault(), "Columnas");
        var totalAcum = Entero(acumuladores.FirstOrDefault(), "TotalAcum");

        /* El ancho del bloque crece con el numero de marcas y se topa en 100%:
           con siete marcas, 15% cada una ya se sale del papel. */
        var ancho = Math.Min(15 * totalColumnas, 100);

        cuerpo = cuerpo.Replace("#DetalleAcumuladores_n2#",
            ArmarAcumuladores(p, acumuladores, totalColumnas, totalAcum));
        cuerpo = cuerpo.Replace("#htmlWidthAcum#", ancho.ToString(CultureInfo.InvariantCulture));

        /* ---------------------------------------------------------- */
        /* La columna de PRODUCTOS                                     */
        /* ---------------------------------------------------------- */

        var totalProductos = Entero(acumuladores.FirstOrDefault(), "TotalProd");

        /* El encabezado y los totales de productos solo se pintan si hay
           productos: si no, la hoja saldria con una columna rotulada y vacia. */
        cuerpo = cuerpo.Replace("#EncabezadoProductos#", totalProductos > 0 ? p.EncabezadoProductos : "");
        cuerpo = cuerpo.Replace("#TotalesProductos#", totalProductos > 0 ? p.TotalesProductos : "");

        var renglonesProducto = new StringBuilder();
        for (var i = 0; i < acumuladores.Length; i++)
        {
            var fila = acumuladores[i];
            if (Texto(fila, "PROD") + Texto(fila, "PRODCantidadFtm") == "")
                continue;

            var renglon = p.DetalleProductos
                .Replace("#Dato7#", Texto(fila, "PROD"))
                .Replace("#Dato8#", Texto(fila, "PRODCantidadFtm"));

            /* El total de productos se imprime UNA vez, pegado al ultimo
               renglon: la plantilla del renglon lo lleva adentro. */
            renglon = renglon.Replace("#TotalProdFtm#",
                i == acumuladores.Length - 1 ? Texto(fila, "TotalProdFtm") : "");

            renglonesProducto.Append(renglon);
        }
        cuerpo = cuerpo.Replace("#DetalleProductos#", renglonesProducto.ToString());

        var primeroAcum = acumuladores.FirstOrDefault();
        var totalProdFtm = Texto(primeroAcum, "TotalProdFtm");
        cuerpo = cuerpo.Replace("#TotalProdFtm#", totalProdFtm);
        cuerpo = cuerpo.Replace("#Subtotal4#", totalProdFtm);
        cuerpo = cuerpo.Replace("#Acumuladores#", $"{totalAcum} PZ");
        cuerpo = cuerpo.Replace("#Productos#", totalProdFtm);
        cuerpo = cuerpo.Replace("#CantidadTotal#", $"{Texto(primeroAcum, "Productos")} PZ");
        cuerpo = cuerpo.Replace("#NumFolios#", Texto(cabecera, "NumFolios"));
        cuerpo = cuerpo.Replace("#PesoKg#", Dinero(cabecera, "PesoKg"));
        cuerpo = cuerpo.Replace("#Observaciones#", "OBSERVACIONES: " + Texto(cabecera, "Observaciones"));

        /* La banda de CANCELADA: el texto y el sello vienen armados desde el
           procedimiento (incluyen usuario y fecha), y con la hoja viva los dos
           llegan vacios, asi que la banda no pinta nada. */
        cuerpo = cuerpo.Replace("#CanceladaDetalle#", p.Cancelado
            .Replace("#Cancelada#", Texto(cabecera, "Cancelada"))
            .Replace("#CanceladaImagen#", Texto(cabecera, "CanceladaImagen")));

        /* ---------------------------------------------------------- */
        /* Los cascos por grupo (G1..G7 y MOTO)                        */
        /* ---------------------------------------------------------- */

        var renglonesUsados = new StringBuilder();
        var piezasUsados = 0;
        decimal montoUsados = 0;
        foreach (var fila in usados)
        {
            renglonesUsados.Append(p.DetalleUsados
                .Replace("#TipoUsado#", Texto(fila, "Tipo de Usado"))
                .Replace("#CantidadUsadoCredito#", Texto(fila, "CantidadUsadoCredito"))
                .Replace("#TotalUsadoCredito#", Dinero(fila, "TotalUsadoCredito")));

            piezasUsados += Entero(fila, "CantidadUsadoCredito");
            montoUsados += Decimal_(fila, "TotalUsadoCredito");
        }

        /* "S I N   U S A D O S", separado letra por letra: es el texto literal
           de legacy, y el espaciado es lo que lo hace legible dentro de una
           celda de tres columnas. */
        cuerpo = cuerpo.Replace("#DetalleUsados#", renglonesUsados.Length > 0
            ? renglonesUsados.ToString()
            : "<tr><td colspan='3'><b> &nbsp; S &nbsp; I &nbsp; N &nbsp; &nbsp; &nbsp; U &nbsp; S &nbsp; A &nbsp; D &nbsp; O &nbsp; S</b></td></tr>");

        /* En cero NO se imprime el cero: el hueco en blanco es donde se escribe
           a mano, y un "0" impreso ahi no se puede corregir. */
        cuerpo = cuerpo.Replace("#UsadosTotal#", piezasUsados > 0 ? piezasUsados.ToString("###,##0", Mx) : "");
        cuerpo = cuerpo.Replace("#UsadosMontoTotal#", montoUsados > 0 ? montoUsados.ToString("###,##0.00", Mx) : "");

        /* ---------------------------------------------------------- */
        /* La rejilla de folios: TRES pasadas sobre la misma tabla      */
        /* ---------------------------------------------------------- */

        var pie = new PieDeLaHoja();

        cuerpo = cuerpo.Replace("#DetalleFolios#", ArmarFoliosDelConcentrado(p, folios, pie));
        cuerpo = cuerpo.Replace("#DetalleFoliosAConCosto#", ArmarFoliosA(p, folios, pie));

        var deCobranza = ArmarFoliosDeCobranza(p, folios, pie);
        /* `#LeyendaCobranza#` va en blanco en los dos casos. Lo hace legacy
           tambien: el `if` que lo distingue pone "&nbsp;" en las dos ramas. Se
           deja la rama porque es donde habria que escribir el rotulo el dia que
           se quiera, y porque borrarla haria pensar que nunca existio. */
        cuerpo = cuerpo.Replace("#LeyendaCobranza#", "&nbsp;");
        cuerpo = cuerpo.Replace("#DetalleFoliosCobranza#", deCobranza);

        /* ---------------------------------------------------------- */
        /* El renglon de totales de la rejilla                         */
        /* ---------------------------------------------------------- */

        cuerpo = cuerpo.Replace("#FoliosNum#", pie.NumFolios.ToString(CultureInfo.InvariantCulture));

        /*
          UNA HOJA DE CLIENTE MANUAL NO LLEVA TOTALES.

          Cuando el primer renglon trae NumeroCliente = 0 la hoja no es de
          folios de venta: es la que se imprime en blanco para llenarse a mano.
          Ahi los totales se dejan vacios —igual que las columnas de cada
          renglon— porque lo que va a haber debajo lo va a escribir una persona.
        */
        if (Texto(folios[0], "NumeroCliente") == "0")
        {
            cuerpo = cuerpo.Replace("#FoliosProductos#", "");
            cuerpo = cuerpo.Replace("#FoliosPesoKg#", "");
            cuerpo = cuerpo.Replace("#FoliosTotalImporte#", "");
            cuerpo = cuerpo.Replace("#FoliosTotalCascos#", "");
        }
        else
        {
            cuerpo = cuerpo.Replace("#FoliosProductos#", pie.Productos.ToString(CultureInfo.InvariantCulture));
            cuerpo = cuerpo.Replace("#FoliosPesoKg#", pie.PesoKg.ToString("###,##0.00", Mx));
            cuerpo = cuerpo.Replace("#FoliosTotalImporte#", pie.Importe.ToString("###,##0.00", Mx));
            cuerpo = cuerpo.Replace("#FoliosTotalCascos#", pie.Cascos.ToString("###,##0.00", Mx));
        }

        /* ---------------------------------------------------------- */
        /* Los renglones en blanco hasta el pie                        */
        /* ---------------------------------------------------------- */

        /*
          LA HOJA SE IMPRIME PARA ESCRIBIR ENCIMA, y por eso se rellena.

          El repartidor anota a mano lo que cobro de cada folio, asi que la
          rejilla tiene que llegar hasta abajo con renglones rayados aunque la
          hoja traiga cinco folios. Cuantos faltan lo dice el procedimiento
          (MaxLineasHoja) menos lo que ya se ocupo.

          Quien calcula el relleno es el SP y no esto: MaxLineasHoja depende del
          tamaño de papel y del alto de renglon de la plantilla, y ya esta medido
          alla para que la raya caiga justo arriba del pie.
        */
        var ocupadas = Entero(primeroAcum, "LineasDetalleProductos") + pie.LineasDetalleRemisiones;
        cuerpo = cuerpo.Replace("#LineasBlanco#", RenglonesEnBlanco(ocupadas, pie.MaxLineasHoja));

        cuerpo = cuerpo.Replace("#TotalAcum#", totalAcum.ToString("###,##0", Mx));
        cuerpo = cuerpo.Replace("#FechaCierreFtm#", pie.FechaCierreFtm);

        /* ---------------------------------------------------------- */
        /* La marca de agua                                            */
        /* ---------------------------------------------------------- */

        /*
          Es Logo_Mac_WM.png (el circulo rojo con "AZ"), y su POSICION la calcula
          el procedimiento: `topWM` sale de la base porque depende de cuantos
          renglones trae la hoja. Centrarla aqui por cuenta propia la dejaria
          encima de la rejilla en las hojas cortas.
        */
        cuerpo = cuerpo.Replace("#CanceladaWM#", pie.CanceladaWM);
        cuerpo = cuerpo.Replace("#Cancelada2WM#", pie.Cancelada2WM);
        cuerpo = cuerpo.Replace("#topWM#", pie.TopWM.ToString(CultureInfo.InvariantCulture));
        cuerpo = cuerpo.Replace("#LogoWM#", logoMarcaDeAgua);

        return cuerpo;
    }

    /* ------------------------------------------------------------------ */
    /* El bloque de acumuladores                                           */
    /* ------------------------------------------------------------------ */

    /*
      EL BLOQUE ESTA PIVOTEADO, Y ESTE ES EL RECORRIDO QUE LO DESPIVOTA.

      El procedimiento devuelve catorce juegos de columnas
      (Marca1/Codigo1/Cantidad1/Total1 … Marca14/…): cada juego es UNA marca con
      sus claves una debajo de otra, y los renglones que le sobran a una marca
      vienen con la marca VACIA. O sea que el cambio de columna no se anuncia:
      se descubre al encontrar un hueco.

      El recorrido es el de legacy, literal: se va leyendo la columna `columna`
      hasta que la marca llega vacia; ahi se cierra la tabla de esa marca, se
      agrega un `<td>` separador y se pasa a la siguiente. Reescribirlo "bien"
      —agrupando por marca con LINQ— cambiaria el orden de las claves dentro de
      cada marca, y ese orden es el que trae el papel que se compara.
    */
    private static string ArmarAcumuladores(
        Plantillas p,
        DataRow[] filas,
        int totalColumnas,
        int totalAcum)
    {
        /* Sin acumuladores no hay bloque: la hoja es solo de productos. */
        if (totalAcum == 0 || filas.Length == 0)
            return "";

        var tablas = new StringBuilder();
        var tablaDeLaMarca = p.DetalleAcumuladoresNuevo;
        var renglones = new StringBuilder();
        var marcaAnterior = "";
        var marcaAgregada = "";
        var totalDeLaMarca = "";
        var columna = 1;
        var linea = 1;

        foreach (var fila in filas)
        {
            var marca = Texto(fila, "Marca" + columna);

            if (marca == "")
            {
                /* Se acabo esta marca: se cierra su tabla con su total. */
                marcaAgregada = marcaAnterior;
                tablaDeLaMarca = tablaDeLaMarca
                    .Replace("#acumuladores_detalle_nuevo#", renglones.ToString())
                    .Replace("#Total#", totalDeLaMarca)
                    .Replace("#Marca#", marcaAnterior);

                if (totalColumnas == columna)
                {
                    tablas.Append(tablaDeLaMarca);
                    break;
                }

                /* El separador entre dos marcas. Va fuera de la plantilla porque
                   la ultima marca NO lo lleva (si no, sobraria una columna
                   vacia al final y el bloque quedaria descentrado). */
                tablas.Append(tablaDeLaMarca).Append("<td>&nbsp;</td>");

                linea = 1;
                columna++;
            }

            if (linea == 1)
            {
                tablaDeLaMarca = p.DetalleAcumuladoresNuevo;
                renglones.Clear();
            }

            totalDeLaMarca = Texto(fila, "Total" + columna);

            renglones.Append(p.DetalleAcumuladoresDetalleNuevo
                .Replace("#Clave#", Texto(fila, "Codigo" + columna))
                .Replace("#Cantidad#", Texto(fila, "Cantidad" + columna)));

            marcaAnterior = Texto(fila, "Marca" + columna);
            linea++;
        }

        /*
          LA ULTIMA MARCA, SI NO LA CERRO EL BUCLE.

          Cuando la tabla de datos se acaba sin un hueco final, la marca que se
          estaba llenando se quedaria sin cerrar y sin aparecer. `marcaAgregada`
          recuerda la ultima que si se cerro; si no es esta, se cierra aqui.
        */
        if (renglones.Length > 0 && marcaAgregada != marcaAnterior)
        {
            tablas.Append(tablaDeLaMarca
                    .Replace("#acumuladores_detalle_nuevo#", renglones.ToString())
                    .Replace("#Total#", totalDeLaMarca)
                    .Replace("#Marca#", marcaAnterior))
                .Append("<td>&nbsp;</td>");
        }

        return tablas.ToString();
    }

    /* ------------------------------------------------------------------ */
    /* Las tres pasadas de la rejilla                                      */
    /* ------------------------------------------------------------------ */

    /*
      PASADA 1: LOS FOLIOS DEL CONCENTRADO, SIN LAS "A".

      Solo los vivos de un concentrado cerrado y que no son de cobranza:
      Fecha Cancelacion vacia, IDStatusConcentrado = 1 e IDCobranza = 0. Esta es
      la unica pasada que SUMA el pie: las "A" son garantias y las de cobranza ya
      venian de antes, y meterlas en los totales del dia contaria dos veces.
    */
    private static string ArmarFoliosDelConcentrado(Plantillas p, DataRow[] folios, PieDeLaHoja pie)
    {
        var html = new StringBuilder();

        foreach (var fila in PorFolio(folios, conA: false))
        {
            if (!EsFolioDelConcentrado(fila)) continue;

            pie.Tomar(fila);

            var renglon = p.DetalleFolios
                .Replace("#DetalleEmpresa#", Texto(fila, "Empresa"))
                .Replace("#DetalleFolio#", Texto(fila, "FolioVentaFtm"))
                .Replace("#DetalleFechaEmision#", Texto(fila, "FechaEmisionCorto"));

            /*
              EL RENGLON SIN NUMERO DE CLIENTE SALE SIN CIFRAS.

              NumeroCliente = 0 es el renglon que se imprime para llenarse a
              mano. Pintarle un 0.00 en importe y en cascos seria imprimir un
              dato donde hay que escribir uno.
            */
            if (Texto(fila, "NumeroCliente") == "0")
            {
                renglon = renglon
                    .Replace("#DetalleNumeroCliente#", "")
                    .Replace("#DetalleProductos#", "")
                    .Replace("#DetallePesoKg#", "")
                    .Replace("#DetalleImporte#", "")
                    .Replace("#DetalleCascos#", "");
            }
            else
            {
                renglon = renglon
                    .Replace("#DetalleNumeroCliente#", Texto(fila, "NumeroCliente"))
                    .Replace("#DetalleProductos#", Texto(fila, "Productos"))
                    .Replace("#DetallePesoKg#", Dinero(fila, "PesoKg"))
                    .Replace("#DetalleImporte#", Dinero(fila, "Importe"))
                    .Replace("#DetalleCascos#", Dinero(fila, "Cascos"));

                pie.Sumar(fila);
            }

            renglon = renglon
                .Replace("#DetalleNombreCliente#", Texto(fila, "NombreClienteCorto"))
                .Replace("#DetalleNumUsados#", Texto(fila, "CantidadUsadoCredito"));

            pie.SumarImporteYCascos(fila);
            html.Append(renglon);
        }

        return html.ToString();
    }

    /*
      PASADA 2: LAS "A" CON COSTO.

      Los folios A- son garantias, y llevan su propia plantilla porque enseñan
      MARCA y CLAVE del acumulador en vez de peso y cascos: la pregunta de un
      renglon de garantia es "que pieza", no "cuanto pesaba".

      No suman el pie a proposito: ya estan dentro del importe del folio que las
      origino.
    */
    private static string ArmarFoliosA(Plantillas p, DataRow[] folios, PieDeLaHoja pie)
    {
        var html = new StringBuilder();

        foreach (var fila in PorFolio(folios, conA: true))
        {
            if (!EsFolioDelConcentrado(fila)) continue;

            pie.Tomar(fila);

            html.Append(p.DetalleFoliosA
                .Replace("#DetalleFolio#", Texto(fila, "FolioVentaFtm"))
                .Replace("#DetalleNumeroCliente#", Texto(fila, "NumeroCliente"))
                .Replace("#DetalleNombreCliente#", Texto(fila, "NombreClienteCorto"))
                .Replace("#DetalleMarca#", Texto(fila, "Marca"))
                .Replace("#DetalleCodigo#", Texto(fila, "Codigo"))
                .Replace("#DetalleFolioA#", Texto(fila, "DetalleFolioA"))
                .Replace("#DetalleImporte#", Dinero(fila, "Total a Pagar")));
        }

        return html.ToString();
    }

    /*
      PASADA 3: LOS FOLIOS DE COBRANZA.

      Lo que ya estaba asignado al repartidor de dias anteriores. Van ordenados
      por NOMBRE DE CLIENTE y no por folio: son visitas de cobro, y quien los
      recorre va cliente por cliente.

      Y aqui las columnas de dinero cambian de significado: se imprime el SALDO
      (SaldoImporte / SaldoCascos) y no el importe, porque de una remision vieja
      lo que se va a cobrar es lo que falta, no lo que valio.
    */
    private static string ArmarFoliosDeCobranza(Plantillas p, DataRow[] folios, PieDeLaHoja pie)
    {
        var html = new StringBuilder();

        var ordenados = folios
            .Where(fila => Texto(fila, "IDCobranza") != "0")
            .OrderBy(fila => Texto(fila, "NombreCliente"), StringComparer.Ordinal)
            .ToArray();

        foreach (var fila in ordenados)
        {
            pie.Tomar(fila);

            html.Append(p.DetalleFolios
                .Replace("#DetalleEmpresa#", Texto(fila, "Empresa"))
                .Replace("#DetalleFolio#", Texto(fila, "FolioVentaFtm"))
                .Replace("#DetalleFechaEmision#", Texto(fila, "FechaEmisionCorto"))
                .Replace("#DetalleNumeroCliente#", Texto(fila, "NumeroCliente"))
                .Replace("#DetalleProductos#", Texto(fila, "Productos"))
                .Replace("#DetallePesoKg#", Dinero(fila, "PesoKg"))
                .Replace("#DetalleImporte#", Dinero(fila, "SaldoImporte"))
                .Replace("#DetalleCascos#", Dinero(fila, "SaldoCascos"))
                .Replace("#DetalleNombreCliente#", Texto(fila, "NombreClienteCorto"))
                .Replace("#DetalleNumUsados#", Texto(fila, "CantidadUsadoCredito")));
        }

        return html.ToString();
    }

    /*
      LAS DOS PRIMERAS PASADAS VAN ORDENADAS POR FOLIO, Y NO ES UN CAPRICHO.

      El `sp_n_` devuelve los renglones con `ORDER BY IDConcentrado, FolioVenta,
      Orden`, o sea por el folio NUMERICO: en el cobro 27861 eso pone el
      G-8569 (folio 8569) delante de los 125349..125369.

      Mac31 lo imprime al reves, el G-8569 AL FINAL, y no porque ordene nada a
      mano: porque separa las dos pasadas con

          dtCobroFoliosUsados.Select("FolioVentaFtm NOT LIKE 'A-%'")
          dtCobroFoliosUsados.Select("FolioVentaFtm LIKE 'A-%'")

      y `DataTable.Select(filtro)` NO conserva el orden de la tabla: construye
      un indice sobre la columna del filtro y devuelve las filas por ese indice.
      El filtro mira `FolioVentaFtm`, que es TEXTO, asi que el resultado sale
      ordenado como texto —"125349" < … < "125369" < "G-8569"— y el renglon de
      garantia cae al final. Es un efecto secundario, no una decision, pero es
      el que esta impreso en el papel que firma el repartidor.

      Nosotros filtramos con LINQ sobre un DataRow[], que SI conserva el orden,
      y por eso el G-8569 nos salia arriba. Se reproduce el orden de legacy
      ordenando explicitamente por FolioVentaFtm. Ordinal porque es lo que hace
      el indice sobre una columna de folios (digitos y prefijos A-/G-, sin
      acentos) y porque asi el resultado no depende de la maquina.

      La tercera pasada, la de cobranza, no pasa por aqui: esa si la ordena
      legacy a mano por NombreCliente.
    */
    private static IEnumerable<DataRow> PorFolio(DataRow[] folios, bool conA)
    {
        return folios
            .Where(fila => Texto(fila, "FolioVentaFtm").StartsWith("A-", StringComparison.Ordinal) == conA)
            .OrderBy(fila => Texto(fila, "FolioVentaFtm"), StringComparer.Ordinal);
    }

    private static bool EsFolioDelConcentrado(DataRow fila)
    {
        return Texto(fila, "Fecha Cancelacion") == ""
               && Texto(fila, "IDStatusConcentrado") == "1"
               && Texto(fila, "IDCobranza") == "0";
    }

    /* ------------------------------------------------------------------ */
    /* El relleno                                                          */
    /* ------------------------------------------------------------------ */

    private static string RenglonesEnBlanco(int ocupadas, int maxLineas)
    {
        /* El estilo va incrustado y repetido en cada celda porque esto no es una
           pagina con hoja de estilos: es un fragmento que se le da a
           wkhtmltopdf, que es un WebKit viejo, y la plantilla de la base no trae
           clases a las que colgarse. Es el mismo HTML que emite legacy. */
        const string BordeAbajo = "font-size:11.0pt; border-bottom: 3px solid #ccc; line-height: 1.3em;";
        const string ConBordeIzquierdo = BordeAbajo + "  border-left: 3px solid #ccc; line-height: 1.3em;";
        const string Ultima = ConBordeIzquierdo + " border-right: 3px solid #ccc; line-height: 1.3em;";

        var html = new StringBuilder();
        for (var linea = ocupadas; linea <= maxLineas; linea++)
        {
            html.Append("<tr>");
            /* Las nueve primeras columnas (folio, fecha, cliente, importes…) van
               juntas: ahi no se escribe a mano, se raya. */
            html.Append($"<td colspan='9' style='{BordeAbajo}'> &nbsp; </td>");
            /* Y las SIETE de la derecha son G1..G7: cada una con su raya
               vertical, porque es ahi donde se anota casco por casco. */
            for (var g = 1; g <= 6; g++)
                html.Append($"<td style='{ConBordeIzquierdo}'> &nbsp; </td>");
            html.Append($"<td style='{Ultima}'> &nbsp; </td>");
            html.Append("</tr>");
        }

        return html.ToString();
    }

    /* ------------------------------------------------------------------ */
    /* El orden de las marcas                                              */
    /* ------------------------------------------------------------------ */

    /*
      PARA ORDENAR LAS MARCAS HAY QUE RELLENAR LOS HUECOS PRIMERO.

      Las marcas vacias son los renglones que le sobran a una marca con menos
      claves que otra, y al ordenar ascendente se irian ARRIBA: la tabla de cada
      marca quedaria con sus huecos antes que sus claves, y el recorrido de
      ArmarAcumuladores —que usa el hueco como señal de "cambia de columna"—
      cerraria la marca antes de empezarla.

      Legacy lo resuelve rellenando los vacios con treinta zetas, ordenando, y
      quitando las zetas. Se copia, con dos cosas que conviene saber:

      1) El `Sort` de legacy repite Marca4 y se olvida de Marca12 y Marca14. Se
         copia TAL CUAL. Es un defecto real, pero es el defecto con el que esta
         impreso el papel que se compara: "arreglarlo" cambiaria el orden de las
         claves en las hojas de doce o mas marcas.
      2) Se trabaja sobre una COPIA. Legacy escribe las zetas sobre el DataTable
         que acaba de traer de la base y las quita despues; si algo lanza en
         medio, la tabla se queda con las zetas dentro. Aqui la tabla original no
         se toca.
    */
    private static DataTable OrdenarPorMarca(DataTable acumuladores)
    {
        if (acumuladores.Rows.Count == 0)
            return acumuladores;

        var copia = acumuladores.Copy();

        foreach (DataRow fila in copia.Rows)
        {
            for (var i = 1; i <= 14; i++)
            {
                var columna = "Marca" + i;
                if (!copia.Columns.Contains(columna)) continue;
                if (Convert.ToString(fila[columna]) == "")
                    fila[columna] = TextoZ;
            }
        }

        var vista = copia.DefaultView;
        vista.Sort = "IDCobro ASC, Marca1 ASC, Marca2 ASC, Marca3 ASC, Marca4 ASC, Marca5 ASC, " +
                     "Marca6 ASC, Marca7 ASC, Marca8 ASC, Marca9 ASC, Marca10 ASC, Marca11 ASC, " +
                     "Marca13 ASC, Marca4 ASC";
        var ordenada = vista.ToTable();

        foreach (DataRow fila in ordenada.Rows)
        {
            for (var i = 1; i <= 14; i++)
            {
                var columna = "Marca" + i;
                if (!ordenada.Columns.Contains(columna)) continue;
                fila[columna] = (Convert.ToString(fila[columna]) ?? "").Replace(TextoZ, "");
            }
        }

        return ordenada;
    }

    /* ------------------------------------------------------------------ */
    /* El pie, que se va llenando al recorrer la rejilla                   */
    /* ------------------------------------------------------------------ */

    /*
      POR QUE ES UNA CLASE Y NO DIEZ VARIABLES SUELTAS.

      Las tres pasadas de la rejilla leen y escriben los mismos acumulados, y en
      legacy son diez variables locales que se pasan de un bloque a otro por
      estar en el mismo metodo de 700 lineas. Juntarlas aqui es lo que permite
      partir el recorrido en tres metodos sin que ninguno se quede con una copia
      vieja.

      MaxLineasHoja, topWM y las dos de la marca de agua se TOMAN del renglon, no
      se suman: vienen repetidas en todos y el procedimiento ya las calculo.
    */
    private sealed class PieDeLaHoja
    {
        public int NumFolios;
        public int Productos;
        public decimal PesoKg;
        public decimal Importe;
        public decimal Cascos;

        public int MaxLineasHoja;
        public int TopWM;
        public string CanceladaWM = "";
        public string Cancelada2WM = "";
        public string FechaCierreFtm = "";
        public int LineasDetalleRemisiones;

        /* Los datos de hoja que vienen repetidos en cada renglon. */
        public void Tomar(DataRow fila)
        {
            FechaCierreFtm = Texto(fila, "FechaCierreFtm");
            MaxLineasHoja = Entero(fila, "MaxLineasHoja");
            TopWM = Entero(fila, "topWM");
            CanceladaWM = Texto(fila, "CanceladaWM");
            Cancelada2WM = Texto(fila, "Cancelada2WM");
            /* Cuantos renglones ocupa la rejilla. Se toma en las tres pasadas
               porque una hoja puede traer folios en cualquiera de ellas y la
               cifra es la misma en todas. */
            LineasDetalleRemisiones = Entero(fila, "LineasDetalleRemisiones");
        }

        public void Sumar(DataRow fila)
        {
            NumFolios += 1;
            Productos += Entero(fila, "Productos");
            PesoKg += Decimal_(fila, "PesoKg");
        }

        /* Importe y cascos se suman TAMBIEN en el renglon sin numero de cliente,
           y no es un descuido: legacy lo hace fuera del if. El renglon manual
           trae ceros, asi que no cambia la suma, pero el dia que traiga algo el
           total tiene que reflejarlo. */
        public void SumarImporteYCascos(DataRow fila)
        {
            Importe += Decimal_(fila, "Importe");
            Cascos += Decimal_(fila, "Cascos");
        }
    }

    /* ------------------------------------------------------------------ */
    /* Las once plantillas                                                 */
    /* ------------------------------------------------------------------ */

    /*
      SE LEEN TODAS DE GOLPE Y AL PRINCIPIO.

      Si falta una, se sabe antes de haber armado media hoja: el mensaje dice
      QUE plantilla falta, que es lo unico que hace falta para arreglarlo (se
      agrega el renglon a Template_Html con IDTipoTemplateHtml = 1006).
    */
    private sealed class Plantillas
    {
        public string Cuerpo = "";
        public string DetalleAcumuladoresNuevo = "";
        public string DetalleAcumuladoresDetalleNuevo = "";
        public string DetalleProductos = "";
        public string DetalleUsados = "";
        public string DetalleFolios = "";
        public string DetalleFoliosA = "";
        public string Cancelado = "";
        public string EncabezadoProductos = "";
        public string TotalesProductos = "";

        public static Plantillas Desde(DataTable plantillas)
        {
            return new Plantillas
            {
                Cuerpo = Leer(plantillas, "cobro_cuerpo"),
                DetalleAcumuladoresNuevo = Leer(plantillas, "cobro_detalle_acumuladores_nuevo"),
                DetalleAcumuladoresDetalleNuevo = Leer(plantillas, "cobro_detalle_acumuladores_detalle_nuevo"),
                DetalleProductos = Leer(plantillas, "cobro_detalle_productos"),
                DetalleUsados = Leer(plantillas, "cobro_detalle_usados"),
                DetalleFolios = Leer(plantillas, "cobro_detalle_folios"),
                DetalleFoliosA = Leer(plantillas, "cobro_detalle_folios_A"),
                Cancelado = Leer(plantillas, "cobro_cancelado"),
                EncabezadoProductos = Leer(plantillas, "cobro_encabezado_productos"),
                TotalesProductos = Leer(plantillas, "cobro_totales_productos")
                /* `cobro_detalle_acumuladores` existe en la base y NO se usa: es
                   la version vieja del bloque, reemplazada por la "_nuevo". Se
                   deja sin leer para que no parezca que hace algo. */
            };
        }

        private static string Leer(DataTable plantillas, string descripcion)
        {
            var filas = plantillas.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
            if (filas.Length == 0)
                throw new InvalidOperationException(
                    $"Falta la plantilla '{descripcion}' de la hoja de cobro (Template_Html, IDTipoTemplateHtml = 1006).");
            return Convert.ToString(filas[0]["html"]) ?? "";
        }
    }

    /* ------------------------------------------------------------------ */
    /* Piezas sueltas                                                      */
    /* ------------------------------------------------------------------ */

    private static DataTable Tabla(DataSet datos, string nombre)
    {
        return datos.Tables.Contains(nombre) ? datos.Tables[nombre]! : new DataTable(nombre);
    }

    private static DataRow[] Filtrar(DataTable tabla, string filtro)
    {
        if (tabla.Rows.Count == 0 || string.IsNullOrEmpty(filtro))
            return Array.Empty<DataRow>();

        try
        {
            return tabla.Select(filtro);
        }
        catch
        {
            /* La tabla no tiene esa columna. Pasa con los acumuladores cuando la
               hoja se filtra por cobranza; ver el comentario de Construir. */
            return Array.Empty<DataRow>();
        }
    }

    private static IEnumerable<DataRow> HojasDeCobro(DataTable folios)
    {
        if (folios.Rows.Count == 0)
            return Array.Empty<DataRow>();

        return folios.AsEnumerable()
            .GroupBy(fila => Texto(fila, "IDConcentrado"))
            .Select(grupo => grupo.OrderBy(fila => Entero(fila, "ID")).First())
            .ToArray();
    }

    private static string Texto(DataRow? fila, string columna)
    {
        if (fila is null || !fila.Table.Columns.Contains(columna))
            return "";
        /* Legacy escribe `fila[columna].ToString()` a pelo, asi que las columnas
           datetime (FechaCobro, FechaModificacion) salen con el formato de la
           cultura. Se pasan por FormatoFechaLegacy para que esa cultura sea la
           de Mac31 y no la que traiga la maquina. Ver FormatoFechaLegacy. */
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
        return Decimal_(fila, columna).ToString("###,##0.00", Mx);
    }
}
