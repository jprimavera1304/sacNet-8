using System.Globalization;
using System.Net;
using System.Text;
using ISL_Service.Application.DTOs.CascosCambio;

namespace ISL_Service.Infrastructure.Reports;

/*
  EL REPORTE DE USADOS A CAMBIO, EN PAPEL

  Lo que habia antes era window.print(): el navegador fotografiaba la pantalla.
  Salia con los filtros, la barra de botones, las columnas ocultas a medio
  recortar y el ancho de la ventana de quien imprimia, asi que el mismo periodo
  daba una hoja distinta en cada maquina. Un papel que se le entrega a la otra
  empresa para cuadrar una cuenta no puede depender de eso.

  Esto arma el HTML y lo manda a wkhtmltopdf, igual que la remision de Ventas:
  mismo camino y mismo visor, asi que se imprime, se guarda y se manda por
  correo como cualquier otro documento del sistema.

  ---------------------------------------------------------------------------
  LOS VALORES SALEN DEL DISEÑO, NO DE MI CRITERIO
  ---------------------------------------------------------------------------
  Colores, tamaños, pesos y sangrias estan copiados del documento de diseño
  ("Reporte usados a cambio - muchas filas"). No se "mejoraron" al traerlos: si
  cada implementacion los redondea a su gusto, el papel deja de parecerse al
  diseño y no hay forma de saber cual de los dos es el bueno.

  Las dos unicas desviaciones, y por que:

    1. EL NOMBRE DE LA EMPRESA del pie sale de la base (Constantes), no escrito
       a mano. El diseño dice "Acumuladores Tauro" porque se dibujo con Tauro
       delante; escribirlo fijo haria que el papel de Zaragoza mintiera.
    2. LA TIPOGRAFIA cae a las del sistema. El diseño pide Century Gothic con
       Questrial de respaldo, pero Questrial se descarga de Google Fonts y
       wkhtmltopdf corre en otro proceso, sin garantia de red: una fuente que a
       veces llega y a veces no da dos papeles distintos. Se deja la misma lista
       para que donde SI este Century Gothic salga igual.

  ---------------------------------------------------------------------------
  EL ESTILO VA INCRUSTADO
  ---------------------------------------------------------------------------
  wkhtmltopdf no comparte el servidor de la aplicacion, asi que una hoja de
  estilo enlazada llegaria vacia y el PDF saldria sin formato. Por lo mismo el
  logo va como data URI y no como direccion.

  ---------------------------------------------------------------------------
  EL REPORTE SE AJUSTA SOLO AL NUMERO DE RENGLONES
  ---------------------------------------------------------------------------
  Un periodo puede traer cuatro movimientos o cuatrocientos y el papel tiene que
  quedar bien en los dos casos sin que nadie lo acomode. Tres reglas lo
  resuelven, y ninguna depende de cuantas filas haya — no hay alturas fijas ni
  cuentas de cuantos renglones caben por hoja, que es lo que se rompe en cuanto
  cambia el tamaño de la letra o entra un chofer con el nombre largo:

    1. EL SALDO AL CORTE VA ARRIBA. Es la cifra que se viene a buscar; si
       viviera solo al final, en un reporte de catorce hojas habria que irse
       hasta la ultima para leerla.
    2. EL ENCABEZADO DE LA TABLA SE REPITE en cada hoja
       (display: table-header-group). Verificado con 300 renglones: 9 paginas,
       las 9 con los siete rotulos.
    3. NI LOS RENGLONES NI EL CORTE SE PARTEN (page-break-inside: avoid). Sin
       eso, el "Saldo al corte" puede quedar en una pagina y su numero en la
       siguiente.
*/
public static class UsadosHtmlBuilder
{
    /* Se fija aqui y no se toma de la maquina: un servidor en otra region
       imprimiria los miles y los decimales al reves. */
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public static string Construir(
        IReadOnlyList<MovimientoCascoCambioDto> movimientos,
        CorteCascosCambioDto? corte,
        DateTime? desde,
        DateTime? hasta,
        bool porRegistro,
        string estatus,
        string logoDataUri)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<title>Usados a cambio</title><style>");
        html.Append(Estilos());
        html.Append("</style></head><body>");

        html.Append(Encabezado(corte, desde, hasta, porRegistro, estatus, logoDataUri));

        if (movimientos.Count == 0)
        {
            /* Se entrega la hoja igual, diciendolo. Un PDF en blanco se lee como
               un error del sistema; este dice que el periodo no tuvo nada. */
            html.Append("<p class=\"vacio\">No hubo movimientos en este periodo.</p>");
            html.Append("</body></html>");
            return html.ToString();
        }

        html.Append(Tabla(movimientos));
        if (corte != null) html.Append(Corte(corte));
        html.Append("</body></html>");
        return html.ToString();
    }

    /*
      EL ENCABEZADO: logo, que es, de que periodo, y el saldo.

      El saldo al corte va arriba y no solo al final por una razon practica: es
      LA cifra del reporte, y en un documento de catorce hojas obligar a irse a
      la ultima para leerla es esconderla.
    */
    private static string Encabezado(
        CorteCascosCambioDto? corte,
        DateTime? desde,
        DateTime? hasta,
        bool porRegistro,
        string estatus,
        string logoDataUri)
    {
        var html = new StringBuilder();

        /*
          EL ENCABEZADO VA EN UNA TABLA, NO EN UN FLEX.

          El diseño lo resuelve con flexbox, pero wkhtmltopdf 0.12.4 corre sobre
          un WebKit de la epoca de Safari 5: `display:flex` no existe ahi, asi
          que el logo, el titulo y el saldo se apilaban como bloques y el
          encabezado salia descolocado. Una tabla de tres celdas da exactamente
          la misma reparticion y la entiende cualquier motor.
        */
        html.Append("<table class=\"cabecera\"><tr>");

        /*
          Sin logo si no se encontro el archivo: una imagen rota en un documento
          que se le entrega a la otra empresa se ve peor que nada.

          Y en ese caso la celda NO se pinta, no se pinta vacia: una celda de
          164 px en blanco dejaria el titulo descolgado hacia la derecha, como
          si faltara algo. Sin ella, el encabezado se cierra solo.
        */
        if (!string.IsNullOrWhiteSpace(logoDataUri))
        {
            html.Append("<td class=\"celdaLogo\"><img class=\"logo\" src=\"")
                .Append(logoDataUri)
                .Append("\" alt=\"\"></td>");
        }

        html.Append("<td class=\"celdaTitulo\">");
        html.Append("<p class=\"titulo\">Usados a cambio</p>");
        html.Append("<p class=\"periodo\">")
            .Append(WebUtility.HtmlEncode(Periodo(desde, hasta, porRegistro, estatus)))
            .Append("</p></td>");

        html.Append("<td class=\"celdaSaldo\">");
        if (corte != null)
        {
            html.Append("<p class=\"corteArribaRotulo\">SALDO AL CORTE</p>");
            html.Append("<p class=\"corteArribaCifra\">")
                .Append(WebUtility.HtmlEncode(Dinero(corte.saldo)))
                .Append("</p>");
        }
        html.Append("</td></tr></table>");

        return html.ToString();
    }

    private static string Tabla(IReadOnlyList<MovimientoCascoCambioDto> movimientos)
    {
        var html = new StringBuilder();
        html.Append("<table class=\"movimientos\"><thead><tr>");
        html.Append("<th>FECHA</th><th>TIPO</th><th>REMISIÓN</th><th>CHOFER</th>");
        html.Append("<th class=\"num\">USADOS</th><th class=\"num\">IMPORTE</th><th class=\"num\">SALDO</th>");
        html.Append("</tr></thead><tbody>");

        /*
          El rayado se cuenta a mano y no con :nth-child(even) porque los
          renglones cancelados llevan su propio fondo: dejandoselo al selector,
          un cancelado en medio "gasta" su turno y el rayado se descuadra a
          partir de ahi. Contando solo los que sí se rayan, la alternancia sigue
          siendo pareja aunque haya cancelados salteados.
        */
        var raya = false;

        foreach (var m in movimientos)
        {
            var cancelado = m.estatus == 2;

            /*
              Los cancelados salen TACHADOS y en rosa, no se omiten: el papel
              tiene que poder cotejarse renglon por renglon contra la pantalla, y
              un movimiento que desaparece del reporte pero sigue en el sistema
              es justo lo que hace que dos cuentas no cuadren sin que nadie sepa
              por que. El color los separa antes de leerlos.
            */
            if (cancelado)
            {
                html.Append("<tr class=\"cancelado\">");
            }
            else
            {
                html.Append(raya ? "<tr class=\"raya\">" : "<tr>");
                raya = !raya;
            }

            html.Append(Celda(m.fecha.ToString("dd/MM/yyyy", Cultura)));
            /* El tipo va destacado: en la pantalla es una pastilla de color, y
               en papel esto es lo mismo sin gastar tinta en un fondo por
               renglon. */
            html.Append("<td class=\"tipo\">")
                .Append(WebUtility.HtmlEncode(cancelado ? "CANCELADA" : (m.tipoMovimientoNombre ?? "")))
                .Append("</td>");
            html.Append(Celda(m.remision ?? "—"));
            html.Append(Celda(m.persona ?? "—"));
            html.Append(CeldaNum(m.totalPiezas > 0 ? m.totalPiezas.ToString("N0", Cultura) : "—"));
            html.Append(CeldaNum(Dinero(m.importeConSigno)));
            /* El saldo es la columna que se sigue de arriba a abajo: destacada,
               igual que la cifra del encabezado. */
            html.Append("<td class=\"num saldo\">")
                .Append(WebUtility.HtmlEncode(Dinero(m.saldo)))
                .Append("</td>");
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
        return html.ToString();
    }

    /*
      EL CORTE, CON EL SALDO ANTERIOR DELANTE.

      Sin el, los numeros del periodo se leen como si la cuenta empezara en cero
      ese dia: saldo anterior + lo de estos dias = saldo, y las tres cifras
      tienen que estar para que la suma se pueda seguir.
    */
    private static string Corte(CorteCascosCambioDto corte)
    {
        var html = new StringBuilder();
        /* Mismo motivo que el encabezado: nada de flex. Una tabla de dos
           celdas, la primera vacia, empuja el corte a la derecha. */
        html.Append("<table class=\"corteCaja\"><tr><td></td><td class=\"corteHueco\">");
        html.Append("<table class=\"corte\"><tbody>");
        html.Append(Renglon("Saldo anterior", Dinero(corte.saldoAnterior)));
        html.Append(Renglon("Entregas", Dinero(corte.entregas)));
        html.Append(Renglon("Pedidos", Dinero(corte.pedidos)));
        html.Append(Renglon("Pagos", Dinero(corte.pagos)));
        html.Append(Renglon("Saldo al corte", Dinero(corte.saldo), destacado: true));
        html.Append("</tbody></table></td></tr></table>");
        return html.ToString();
    }

    /*
      DICE POR CUAL DE LAS DOS FECHAS SE FILTRO, Y CON QUE ESTATUS.

      Un movimiento tiene la que se teclea —cuando se entregaron los usados— y
      la de registro —cuando se capturo—, y el mismo periodo da listas distintas
      segun cual se use. El estatus hace lo mismo: un reporte de solo cancelados
      y uno de todos se parecen lo bastante como para confundirlos. Sin esta
      linea, dos reportes del mismo rango salen distintos y no hay nada en el
      papel que lo explique.
    */
    private static string Periodo(DateTime? desde, DateTime? hasta, bool porRegistro, string estatus)
    {
        var cual = porRegistro ? "Por fecha de registro" : "Por fecha del movimiento";

        var rango =
            desde == null && hasta == null ? "Todo el historial"
            : desde != null && hasta != null
                ? "Del " + desde.Value.ToString("dd/MM/yyyy", Cultura) +
                  " al " + hasta.Value.ToString("dd/MM/yyyy", Cultura)
            : desde != null
                ? "Desde el " + desde.Value.ToString("dd/MM/yyyy", Cultura)
                : "Hasta el " + hasta!.Value.ToString("dd/MM/yyyy", Cultura);

        /* "Todos" no se escribe: es lo que se espera de un reporte, y ponerlo
           gasta renglon para no decir nada. Los otros dos SI, porque son una
           lista recortada y eso hay que advertirlo. */
        var cualEstatus = estatus switch
        {
            "activos" => " · Solo activos",
            "cancelados" => " · Solo cancelados",
            _ => ""
        };

        return cual + " · " + rango + cualEstatus;
    }

    /*
      EL TEXTO DEL PIE — QUE YA NO SE PINTA EN EL CUERPO

      Lleva el nombre de la empresa y la fecha de impresion. La fecha no es
      adorno: dos copias del mismo periodo sacadas en dias distintos pueden
      diferir si entremedias se cancelo algo, y sin ella no hay forma de saber
      cual de los dos papeles es el nuevo.

      Devuelve TEXTO PLANO, no HTML, porque ya no va dentro del documento: se le
      pasa a wkhtmltopdf para que lo escriba en la franja de pie de cada hoja.
      Cuando era un parrafo al final del cuerpo, su margen de 26px podia no
      caber en lo que quedaba de hoja y se llevaba una pagina entera en blanco
      solo para enseñar esa linea.
    */
    public static string TextoDePie(string empresa)
    {
        var sello = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura);
        return string.IsNullOrWhiteSpace(empresa)
            ? "Impreso el " + sello
            : empresa + " · Impreso el " + sello;
    }

    /* Sin simbolo de moneda, como en la pantalla: en este reporte toda cifra es
       dinero y el simbolo se repetiria en cada celda sin distinguir nada. */
    private static string Dinero(decimal valor) => valor.ToString("N2", Cultura);

    /* TODO lo que viene de la base pasa por aqui. Una remision o un nombre de
       chofer con un "<" partiria el documento en dos. */
    private static string Celda(string texto)
        => "<td>" + WebUtility.HtmlEncode(texto) + "</td>";

    private static string CeldaNum(string texto)
        => "<td class=\"num\">" + WebUtility.HtmlEncode(texto) + "</td>";

    private static string Renglon(string rotulo, string valor, bool destacado = false)
        => (destacado ? "<tr class=\"total\">" : "<tr>") +
           "<td class=\"corteRotulo\">" + WebUtility.HtmlEncode(rotulo) + "</td>" +
           "<td class=\"corteCifra\">" + WebUtility.HtmlEncode(valor) + "</td></tr>";

    /*
      EL CSS ESTA ESCRITO PARA wkhtmltopdf 0.12.4, NO PARA UN NAVEGADOR

      Ese binario corre sobre un WebKit de la epoca de Safari 5, y eso obliga a
      dos cosas que de otro modo pareceran anticuadas:

        * NADA DE VARIABLES CSS. `var(--azul)` no existe ahi: la propiedad se
          descarta entera y el color cae al valor por omision. Asi salio el
          primer intento — el encabezado sin su franja azul y todo en negro,
          que es exactamente lo que se veia en el PDF. Los colores van escritos
          literales, repetidos, y el catalogo queda en este comentario:

              #000C7B  el azul del sistema (franja, saldos, remates)
              #1b2a8f  el de la columna TIPO, un punto mas claro
              #ffb6c1  el fondo de un renglon cancelado
              #7a0b26  su texto
              rgba(10,15,30,.028)  el rayado, apenas perceptible

        * NADA DE FLEXBOX. Por eso el encabezado y el corte van en tablas: dan
          la misma reparticion y no dependen de una propiedad que el motor no
          entiende.

      `object-fit` tampoco existe; el logo se limita con max-width/max-height.
    */
    private static string Estilos() => @"
      @page { margin: 0.6in; }

      /*
        La tipografia del diseño. Questrial se queda en la lista aunque no se
        descargue —wkhtmltopdf corre sin garantia de red y una fuente que a
        veces llega da dos papeles distintos—: si algun dia esta instalada,
        entra sola.
      */
      body {
        margin: 0;
        font-family: 'Century Gothic', Questrial, 'Segoe UI', Roboto, Arial, sans-serif;
        color: rgba(10,15,30,.92);
        font-size: 10.5px;
      }

      /* EL ENCABEZADO, EN TABLA. Tres celdas: logo, titulo, saldo. */
      .cabecera {
        width: 100%;
        border-collapse: collapse;
        padding-bottom: 9px;
        border-bottom: 2px solid #000C7B;
        page-break-inside: avoid;
      }
      .cabecera td { vertical-align: middle; padding: 0 0 9px; }
      /* El ancho justo del logo: si la celda creciera, empujaria el titulo. */
      .celdaLogo { width: 164px; }
      .celdaTitulo { width: auto; }
      /* Se encoge a su contenido y la cifra crece hacia adentro. */
      .celdaSaldo { width: 1%; white-space: nowrap; text-align: right; }

      /*
        EL LOGO, MAS GRANDE QUE EN EL DISEÑO. Y A PROPOSITO.

        El diseño lo encierra en una caja de 104x50, pero el logo real es muy
        apaisado (3.9 a 1): dentro de esa caja sale de 104x27, o sea usando
        media altura, y a ese tamaño los contornos blancos que llevan las letras
        por dentro se pierden y la marca se ve lavada — parecia que tuviera
        transparencia. A 152 de ancho ocupa los 39 de alto que le tocan y se
        lee, sin descuadrar el encabezado.

        Sin object-fit, que tampoco existe en este motor: acotando por los dos
        lados se conserva la proporcion sola, y eso vale para cualquier logo,
        que cada empresa tiene el suyo con otra forma.
      */
      .logo { max-width: 152px; max-height: 50px; }

      .titulo { margin: 0; font-size: 16px; font-weight: 800; letter-spacing: -.01em; }
      .periodo { margin: 2px 0 0; font-size: 10.5px; color: rgba(10,15,30,.62); }

      /* El saldo al corte: rotulo chico y espaciado, cifra grande y azul. */
      .corteArribaRotulo { margin: 0; font-size: 9px; letter-spacing: .14em; color: rgba(10,15,30,.5); }
      .corteArribaCifra { margin: 1px 0 0; font-size: 19px; font-weight: 800; color: #000C7B; }

      .movimientos { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 10.5px; }
      .movimientos thead tr { background: #000C7B; }
      .movimientos th {
        text-align: left; padding: 7px 9px; font-size: 9px;
        letter-spacing: .07em; font-weight: 700; color: #fff;
      }
      .movimientos th.num { text-align: right; }
      /* Las esquinas redondeadas solo en los extremos de la franja azul. */
      .movimientos th:first-child { border-top-left-radius: 7px; }
      .movimientos th:last-child  { border-top-right-radius: 7px; }

      .movimientos td {
        padding: 6px 9px; border-bottom: 1px solid rgba(10,15,30,.1);
        color: rgba(10,15,30,.85);
      }

      /* Rayado apenas perceptible: con siete columnas y cifras a la derecha, el
         ojo se salta de linea al seguir un saldo. Tiene que guiar, no rayar. */
      .movimientos tbody tr.raya td { background: rgba(10,15,30,.028); }

      /*
        VAN CON `td` DELANTE POR ESPECIFICIDAD, NO POR GUSTO.

        La regla general de la tabla es `.movimientos td`, que pesa (0,1,1): una
        clase suelta como `.tipo` pesa (0,1,0) y PIERDE, asi que el color se lo
        seguia poniendo la regla general y las dos columnas salian en gris
        aunque aqui dijera azul. Se vio en el PDF: el azul aparecia solo en la
        franja del encabezado.
      */
      .movimientos td.tipo { font-weight: 700; letter-spacing: .03em; color: #1b2a8f; }

      /* Los numeros a la derecha y sin partirse: es lo unico que permite
         comparar una columna de dinero de un vistazo. */
      .num { text-align: right; white-space: nowrap; }
      .movimientos td.saldo { font-weight: 700; color: #000C7B; }

      /* EL ENCABEZADO SE REPITE EN CADA HOJA. Una segunda pagina de numeros sin
         rotulo no se puede leer. */
      thead { display: table-header-group; }
      /* Y ningun renglon se parte por la mitad entre dos hojas. */
      tr { page-break-inside: avoid; }

      .movimientos tr.cancelado td {
        background: #ffb6c1; color: #7a0b26; text-decoration: line-through;
      }
      /* Y estas tienen que pesar mas que las dos de arriba, que ya llevan `td`:
         en un cancelado manda el vino, no el azul. */
      .movimientos tr.cancelado td.tipo,
      .movimientos tr.cancelado td.saldo { color: #7a0b26; }

      /* EL CORTE DEL FINAL NO SE PARTE: si cayera en el filo de la hoja, el
         rotulo quedaria en una pagina y su numero en la siguiente. */
      .corteCaja { width: 100%; border-collapse: collapse; margin-top: 14px; page-break-inside: avoid; }
      /* La celda de la derecha se encoge a su contenido; la vacia de al lado se
         queda todo el hueco y empuja el corte a la orilla. */
      .corteHueco { width: 1%; white-space: nowrap; }
      .corte { border-collapse: collapse; font-size: 11px; }
      .corte td { border-bottom: none; }
      .corteRotulo { padding: 3px 10px; color: rgba(10,15,30,.66); }
      .corteCifra { padding: 3px 10px; text-align: right; font-weight: 700; }
      .corte .total .corteRotulo {
        padding: 8px 10px 0; font-size: 12.5px; font-weight: 800; border-top: 2px solid #000C7B;
      }
      .corte .total .corteCifra {
        padding: 8px 10px 0; font-size: 15px; font-weight: 800;
        color: #000C7B; border-top: 2px solid #000C7B;
      }

      .vacio { margin: 24px 0; color: rgba(10,15,30,.66); }
      /* El pie ya no esta aqui: lo escribe wkhtmltopdf en la franja de cada
         hoja. Ver TextoDePie. */
    ";
}
