using System.Globalization;
using System.Net;
using System.Text;
using ISL_Service.Application.DTOs.CascosCambio;

namespace ISL_Service.Infrastructure.Reports;

/*
  EL REPORTE DE USADOS A CAMBIO, CON LA CARA DE LOS REPORTES DE SIEMPRE

  ---------------------------------------------------------------------------
  POR QUE EXISTE ESTE ARCHIVO SI YA HABIA UNO
  ---------------------------------------------------------------------------
  UsadosHtmlBuilder —el de al lado— sigue ahi, intacto y funcionando. No se
  toco ni una linea suya: hay pases firmados circulando que lo piden, y un
  reporte que la gente ya conoce no se cambia por debajo mientras alguien lo
  esta imprimiendo.

  Este es OTRO papel. Aquel se dibujo desde cero, con su propia tipografia y su
  franja azul. Este copia la estetica de los reportes de Mac31 —los que la
  oficina lleva quince años mirando— porque eso fue lo que se pidio: que el
  reporte nuevo no pareciera de otro sistema. Los valores no son de mi gusto,
  salen de la plantilla real: Template_Html id 43 de la base, que es el molde
  de los reportes de legacy.

      Tahoma en todo             titulo 14pt negritas
      empresa 12pt               "Pag: N de M" 8pt
      linea de fechas 11.5pt     "Impresion:" 8pt
      tabla 10pt                 rotulos de columna 11.5px negritas

  Si, "11.5px" en una tabla medida en puntos. Esta asi en la plantilla y se deja
  asi: el dia que alguien compare este papel con uno de legacy, tienen que
  medir lo mismo.

  ---------------------------------------------------------------------------
  LAS DOS VARIANTES
  ---------------------------------------------------------------------------
  SIN DETALLE  una fila por movimiento, con sus piezas, su importe y el saldo
               que lleva la cuenta.
  CON DETALLE  lo mismo, mas una columna por tipo de usado —MINI CHICO(1),
               CHICO(2), ... JUMBO(7)— con las piezas de cada tipo. Es el
               desglose que hoy solo se ve pulsando "Ver", movimiento a
               movimiento, y que hay que copiar a mano para cuadrar con la otra
               empresa.

  LAS COLUMNAS DE TIPO NO ESTAN ESCRITAS AQUI. Salen de los tipos que de verdad
  aparecen en el periodo. El catalogo tiene ocho —esta MOTO, que en cascos a
  cambio casi nunca se mueve— y puede crecer: una lista fija en el codigo seria
  una columna vacia para siempre y un tipo nuevo que no se ve.

  ---------------------------------------------------------------------------
  EL ENCABEZADO NO ESTA EN ESTE DOCUMENTO
  ---------------------------------------------------------------------------
  Se pidio que arriba de TODAS las hojas salgan el titulo, quien imprimio, la
  fecha y hora y "Pag: 1 de 2". Un encabezado escrito al principio del cuerpo
  sale una sola vez y ademas no tiene forma de saber en que pagina va.

  Por eso son dos documentos: Cuerpo() arma la tabla y Encabezado() arma la
  franja de arriba, que wkhtmltopdf repite en cada hoja y a la que le pasa el
  numero de pagina en la direccion. Ver EncabezadoHtml en el renderer.

  ---------------------------------------------------------------------------
  ESCRITO PARA wkhtmltopdf 0.12.4, QUE NO ES UN NAVEGADOR
  ---------------------------------------------------------------------------
  Ese binario corre sobre un WebKit de la epoca de Safari 5:

    * NADA DE VARIABLES CSS. `var(--x)` no se entiende y la propiedad entera se
      descarta: el color cae a negro y no hay error que lo avise. Colores
      literales, repetidos.
    * NADA DE FLEXBOX. El encabezado se maqueta con TABLAS, igual que la
      plantilla de legacy —que tambien las usa, por la misma razon quince años
      antes.
    * NADA DE object-fit. El logo se acota con width/height, como en la
      plantilla.

  Y la tipografia es Tahoma, no Century Gothic: Century Gothic viene con Office
  y el servidor no lo tiene. Tahoma viene con Windows desde siempre, asi que el
  papel mide lo mismo salga de donde salga — que es justo lo que se rompe si la
  fuente cambia: cambian las metricas, cambian los cortes de renglon y el mismo
  reporte sale de 10 hojas en una maquina y de 11 en otra.
*/
public static class UsadosLegacyHtmlBuilder
{
    /* Se fija aqui y no se toma de la maquina: un servidor en otra region
       imprimiria los miles y los decimales al reves. */
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    private const int EstatusCancelado = 2;

    /* ================================================================== */
    /*  EL ENCABEZADO QUE SE REPITE EN CADA HOJA                          */
    /* ================================================================== */

    /*
      COMO LLEGA EL NUMERO DE PAGINA

      wkhtmltopdf no sustituye [page] dentro de un --header-html: lo que hace es
      abrir este documento con los datos en la direccion
      (?page=3&topage=14&frompage=1...). Asi que el numero lo pone el propio
      documento al cargarse, leyendo su direccion y escribiendolo en los <span>
      que llevan la clase correspondiente.

      Es el unico camino. Con [page] a secas, el encabezado imprime los
      corchetes tal cual — se probo.
    */
    public static string Encabezado(
        string titulo,
        string empresa,
        string logoDataUri,
        DateTime? desde,
        DateTime? hasta,
        bool porRegistro,
        string estatus,
        string usuario)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<style>");
        html.Append("body{margin:0;font-family:Tahoma,Verdana,Arial,sans-serif;}");
        html.Append(".enc{width:98%;border-collapse:collapse;font-family:Tahoma,Verdana,Arial,sans-serif;font-size:11pt;margin:0 auto;}");
        html.Append(".enc td{vertical-align:top;padding:0;}");
        /* El ancho justo del logo de la plantilla: si la celda creciera,
           empujaria el titulo hacia la derecha. */
        html.Append(".celdaLogo{width:170px;}");
        html.Append(".tit{font-size:14pt;font-weight:bold;}");
        /* La celda de la derecha se encoge a su contenido y no se parte: asi el
           bloque del titulo se queda con todo el ancho que sobra. */
        html.Append(".copia{white-space:nowrap;vertical-align:top;width:1%;}");
        html.Append(".emp{font-size:12pt;font-weight:normal;}");
        html.Append(".oculto{display:none;}");
        html.Append(".pag{font-size:10pt;font-weight:normal;}");
        /* LA LINEA DE FECHAS, EN 11.5pt. Era lo primero que pidio el cliente:
           en el reporte anterior salia en letra chica y gris, y es la linea que
           dice de que periodo es el papel que tienes en la mano. */
        html.Append(".fec{font-size:11.5pt;font-weight:normal;}");
        html.Append(".imp{font-size:10pt;font-weight:normal;}");
        html.Append("hr{border:0;border-top:1px solid #000;margin:4px 0 0;}");
        html.Append("</style>");

        /*
          El script lee la direccion con la que wkhtmltopdf abrio este archivo y
          copia cada valor en los elementos que llevan su nombre como clase. Sin
          el, "Pag: de" sale vacio.
        */
        html.Append("<script>function subst(){var v={},p=document.location.search.substring(1).split('&');");
        html.Append("for(var i=0;i<p.length;i++){var z=p[i].split('=',2);v[z[0]]=decodeURIComponent(z[1]||'');}");
        html.Append("var n=['page','topage','frompage'];");
        html.Append("for(var j=0;j<n.length;j++){var e=document.getElementsByClassName(n[j]);");
        html.Append("for(var k=0;k<e.length;k++){e[k].textContent=v[n[j]]||'';}}}</script>");
        html.Append("</head><body onload=\"subst()\">");

        html.Append("<table class=\"enc\"><tr>");

        /*
          Sin logo si no se encontro el archivo, y sin celda tampoco: una celda
          de 170 px en blanco dejaria el titulo descolgado, como si faltara algo.
          Una imagen rota en un papel que se le entrega a la otra empresa se ve
          peor que no tener logo.
        */
        if (!string.IsNullOrWhiteSpace(logoDataUri))
        {
            /*
              EL LOGO CONSERVA SU PROPORCION.

              La plantilla de legacy lo fija en 160x20 y ahi el logo sale
              aplastado: el de Tauro es de 3015x775, o sea casi 4 a 1, y
              forzarlo a 8 a 1 lo estira al doble de ancho de lo que le toca.
              Es lo unico en lo que NO copiamos a legacy, y a proposito — ese
              achatamiento no es una decision de diseño suya, es que ahi pusieron
              dos numeros fijos.

              Solo se fija el ALTO y el ancho se deja en auto: asi cada empresa
              pone el suyo, con la forma que tenga, y ninguno se deforma.
            */
            html.Append("<td class=\"celdaLogo\"><img src=\"")
                .Append(logoDataUri)
                .Append("\" alt=\"\" height=\"30\"></td>");
        }

        /* Lo que identifica al REPORTE: que es y de que periodo. */
        html.Append("<td align=\"left\">");
        html.Append("<span class=\"tit\">").Append(Texto(titulo)).Append("</span>&nbsp;&nbsp;");
        /*
          EL NOMBRE DE LA EMPRESA SE OCULTA, NO SE QUITA.

          Al lado del logo no aporta: el logo ya dice de quien es el papel, y
          repetirlo en letra le roba sitio al titulo. Legacy lo lleva porque su
          logo va en 160x20 y ahi no siempre se distingue.

          Se deja pasando el dato y escondiendolo con CSS —la misma manera que
          las columnas guardadas de la pantalla— para que volver a enseñarlo sea
          borrar una clase. Y sigue haciendo falta en el papel de la otra
          empresa, si algun dia su logo no llega.
        */
        html.Append("<span class=\"emp oculto\">").Append(Texto(empresa)).Append("</span><br>");
        html.Append("<span class=\"fec\">")
            .Append(Texto(Periodo(desde, hasta, porRegistro, estatus)))
            .Append("</span>");
        html.Append("</td>");

        /*
          Y A LA DERECHA, LO QUE IDENTIFICA A LA COPIA: que hoja es, quien la
          saco y cuando.

          Son cosas distintas y por eso van en celdas distintas. Antes iban
          mezcladas con el titulo, y la pagina quedaba colgada detras del nombre
          de la empresa: para saber en que hoja estabas habia que leer toda la
          linea del titulo primero.

          Pegado a la derecha y con su propia celda, el sitio no se mueve aunque
          el titulo crezca —la variante con detalle lo alarga— asi que en las
          siete hojas esta siempre donde el ojo ya lo busca.

          Y en 10pt, no en los 8 de legacy: ahi eran letra pequeña de contrato.
          Se pidio verlo, y para eso hay que poder leerlo.
        */
        html.Append("<td align=\"right\" class=\"copia\">");
        html.Append("<span class=\"pag\">Página&nbsp;<b class=\"page\"></b>&nbsp;de&nbsp;<b class=\"topage\"></b></span><br>");
        html.Append("<span class=\"imp\">Impresión: <b>")
            .Append(Texto(DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura)))
            .Append("</b></span>");
        if (!string.IsNullOrWhiteSpace(usuario))
        {
            html.Append("<br><span class=\"imp\">Usuario: <b>")
                .Append(Texto(usuario))
                .Append("</b></span>");
        }
        html.Append("</td></tr></table><hr>");

        html.Append("</body></html>");
        return html.ToString();
    }

    /* ================================================================== */
    /*  EL CUERPO: LA TABLA                                               */
    /* ================================================================== */

    /// <param name="detallePorMovimiento">
    /// Las piezas por tipo de cada movimiento, o null para la variante sin
    /// detalle. Null y diccionario vacio NO son lo mismo: vacio significa "con
    /// detalle, pero este periodo no movio ni una pieza" y las columnas de tipo
    /// no se pintan por falta de tipos, no por falta de variante.
    /// </param>
    public static string Cuerpo(
        IReadOnlyList<MovimientoCascoCambioDto> movimientos,
        CorteCascosCambioDto? corte,
        IReadOnlyDictionary<int, List<DetallePeriodoCascoCambioDto>>? detallePorMovimiento)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<title>Usados a cambio</title><style>");
        html.Append(Estilos());
        html.Append("</style></head><body>");

        if (movimientos.Count == 0)
        {
            /* Se entrega la hoja igual, diciendolo. Un PDF en blanco se lee como
               un error del sistema; este dice que el periodo no tuvo nada. */
            html.Append("<p class=\"vacio\">No hubo movimientos en este periodo.</p>");
            html.Append("</body></html>");
            return html.ToString();
        }

        /*
          LAS COLUMNAS DE TIPO SALEN DE LOS DATOS, NO DE UNA LISTA EN EL CODIGO.

          Se recorre el desglose del periodo y se queda con los tipos que de
          verdad movieron piezas, en el orden del catalogo. Asi un periodo de
          puras entregas chicas no arrastra siete columnas de ceros, y el dia que
          entre un tipo nuevo al catalogo sale solo.
        */
        var tipos = TiposDelPeriodo(movimientos, detallePorMovimiento);

        html.Append(Tabla(movimientos, corte, detallePorMovimiento, tipos));
        html.Append("</body></html>");
        return html.ToString();
    }

    private static List<(int Id, string Rotulo)> TiposDelPeriodo(
        IReadOnlyList<MovimientoCascoCambioDto> movimientos,
        IReadOnlyDictionary<int, List<DetallePeriodoCascoCambioDto>>? detalle)
    {
        var tipos = new List<(int Id, string Rotulo)>();
        if (detalle is null) return tipos;

        var vistos = new Dictionary<int, (int Orden, string Rotulo)>();
        foreach (var m in movimientos)
        {
            if (!detalle.TryGetValue(m.idMovimiento, out var renglones)) continue;
            foreach (var r in renglones)
            {
                if (r.piezas == 0) continue;
                if (vistos.ContainsKey(r.idTipoUsado)) continue;
                /* La clave corta ('MINI CHICO(1)') es lo que cabe en un rotulo
                   de columna; el nombre largo lleva delante el codigo de
                   producto de legacy y no dice nada en un encabezado. */
                var rotulo = string.IsNullOrWhiteSpace(r.clave) ? (r.nombre ?? "") : r.clave!;
                vistos[r.idTipoUsado] = (r.orden, rotulo);
            }
        }

        foreach (var par in vistos.OrderBy(p => p.Value.Orden))
            tipos.Add((par.Key, par.Value.Rotulo));

        return tipos;
    }

    private static string Tabla(
        IReadOnlyList<MovimientoCascoCambioDto> movimientos,
        CorteCascosCambioDto? corte,
        IReadOnlyDictionary<int, List<DetallePeriodoCascoCambioDto>>? detalle,
        List<(int Id, string Rotulo)> tipos)
    {
        var html = new StringBuilder();
        var conDetalle = detalle != null;

        /* Cuantas columnas hay en total: hace falta para los colspan de los
           renglones de totales y de las notas de cancelacion. Contarlas a mano
           en cada sitio es como se descuadran las tablas. */
        var columnas = 4 + tipos.Count + 3;

        html.Append(conDetalle ? "<table class=\"datos apretada\">" : "<table class=\"datos\">");

        html.Append("<thead><tr>");
        html.Append("<th class=\"izq\">FECHA</th>");
        html.Append("<th class=\"izq\">TIPO</th>");
        html.Append("<th class=\"izq\">REMISIÓN</th>");
        html.Append("<th class=\"izq\">PERSONA</th>");
        foreach (var t in tipos)
            html.Append("<th class=\"der tipoCol\">").Append(Texto(t.Rotulo)).Append("</th>");
        html.Append("<th class=\"der\">USADOS</th>");
        html.Append("<th class=\"der\">IMPORTE</th>");
        /*
          EL SALDO NO SALE EN EL PAPEL, igual que en la pantalla, donde tambien
          se oculto. El saldo que importa es el del corte, que va al final; el
          corrido renglon a renglon solo sirve mientras se revisa en pantalla.

          Se deja el dato calculandose: volver a enseñarlo es descomentar esta
          linea y la de la celda.
        */
        html.Append("<th class=\"izq colCancel\">CANCELACIÓN</th>");
        html.Append("</tr></thead><tbody>");

        /*
          LOS TOTALES SE ACUMULAN AQUI MISMO, RECORRIENDO LO QUE SE IMPRIME.

          No se piden a la base ni se sacan del corte: tienen que ser la suma de
          lo que esta EN EL PAPEL, o el dia que el reporte recorte algo (solo
          cancelados, por ejemplo) la ultima fila diria un numero que no cuadra
          con ninguna columna de arriba. Un total que no se puede comprobar
          sumando a mano lo de encima no sirve para cuadrar nada.

          Los CANCELADOS no suman. No mueven el saldo —el servicio los salta al
          acumularlo— y meterlos en el total dejaria la ultima fila peleada con
          la columna SALDO.
        */
        var totalPiezas = 0;
        decimal totalImporte = 0;
        var totalPorTipo = new Dictionary<int, int>();

        var raya = false;

        foreach (var m in movimientos)
        {
            var cancelado = m.estatus == EstatusCancelado;

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
            html.Append("<td class=\"izq fuerte\">")
                .Append(Texto(cancelado ? "CANCELADA" : (m.tipoMovimientoNombre ?? "")))
                .Append("</td>");
            html.Append(Celda(m.remision ?? "—"));
            html.Append(Celda(m.persona ?? "—"));

            foreach (var t in tipos)
            {
                var piezas = PiezasDe(detalle, m.idMovimiento, t.Id);
                /* El cero no se escribe: en una tabla de ocho columnas de
                   piezas, una retícula de ceros esconde justo los numeros que
                   se vienen a leer. El hueco se lee igual de rapido. */
                html.Append(CeldaNum(piezas == 0 ? "" : piezas.ToString("N0", Cultura)));
                if (!cancelado && piezas != 0)
                    totalPorTipo[t.Id] = totalPorTipo.GetValueOrDefault(t.Id) + piezas;
            }

            html.Append(CeldaNum(m.totalPiezas > 0 ? m.totalPiezas.ToString("N0", Cultura) : "—"));
            html.Append(CeldaNum(Dinero(m.importeConSigno)));
            /*
              LA CANCELACION, EN SU COLUMNA.

              Estaba en un renglon aparte debajo del movimiento, y ahi partia la
              tabla: entre dos filas de datos aparecia una linea de texto que
              rompia la lectura de las columnas justo donde se esta comparando.

              En su propia columna, cada movimiento ocupa UNA fila y siempre la
              misma. Los que no estan cancelados llevan una raya, que dice "aqui
              no hay nada" mejor que un hueco — un hueco hace dudar de si falta
              el dato.
            */
            html.Append("<td class=\"izq colCancel\">")
                /*
                  El texto va SIN escapar porque lleva un <br> nuestro; lo que
                  viene del usuario —el motivo— se escapa por dentro, en
                  NotaDeCancelacion. La raya del caso normal no lleva nada que
                  escapar.
                */
                .Append(cancelado ? NotaDeCancelacion(m) : "—")
                .Append("</td>");
            html.Append("</tr>");

            if (!cancelado)
            {
                totalPiezas += m.totalPiezas;
                totalImporte += m.importeConSigno;
            }

        }

        /*
          LA ULTIMA FILA DE LA TABLA SON LOS TOTALES. Antes vivian en un bloque
          aparte, a la derecha y debajo: para comprobar una columna habia que
          mirar en otro sitio, y en un reporte de varias hojas ese sitio estaba
          en la ultima. Aqui la suma cae justo debajo de lo que suma.

          El SALDO no se suma —sumar saldos corridos no significa nada— sino que
          se enseña el ultimo, que es el saldo al corte: el mismo numero que
          cierra la columna de arriba.
        */
        html.Append("<tr class=\"totales\">");
        html.Append("<td class=\"izq\" colspan=\"4\">TOTALES</td>");
        foreach (var t in tipos)
        {
            var suma = totalPorTipo.GetValueOrDefault(t.Id);
            html.Append(CeldaNum(suma == 0 ? "" : suma.ToString("N0", Cultura)));
        }
        html.Append(CeldaNum(totalPiezas.ToString("N0", Cultura)));
        html.Append(CeldaNum(Dinero(totalImporte)));
        /* La columna de cancelacion no suma nada: se deja vacia para que la
           fila de totales tenga las mismas celdas que las de arriba. */
        html.Append("<td class=\"colCancel\"></td>");
        html.Append("</tr>");

        /*
          Y DEBAJO, EL CORTE — que no es lo mismo que los totales.

          Los totales de arriba suman COLUMNAS. Esto reparte el mismo dinero por
          tipo de movimiento y le pone delante el saldo con el que llegaba la
          cuenta: saldo anterior + lo de estos dias = saldo al corte. Sin el
          saldo anterior, los numeros del periodo se leen como si la cuenta
          empezara en cero ese dia.

          Va como UN SOLO RENGLON de la misma tabla, en el sitio del #Totales#
          de la plantilla de legacy.

          POR QUE UNA LINEA Y NO CINCO. Eran cinco renglones apilados, y cinco
          renglones al final de una tabla que ya viene llena caben o no caben
          segun donde haya quedado el ultimo movimiento. Cuando no caben se
          llevan una HOJA ENTERA para enseñar el corte, que es el peor cambio
          posible: una pagina de papel por cuatro cifras. En una linea eso deja
          de poder pasar.

          Y se lee igual de bien, porque la suma va en orden: lo que habia, lo
          que entro, lo que salio, y con que se queda.
        */
        if (corte != null)
            html.Append(RenglonCorte(columnas, corte));

        html.Append("</tbody></table>");
        return html.ToString();
    }

    private static int PiezasDe(
        IReadOnlyDictionary<int, List<DetallePeriodoCascoCambioDto>>? detalle,
        int idMovimiento,
        int idTipo)
    {
        if (detalle is null) return 0;
        if (!detalle.TryGetValue(idMovimiento, out var renglones)) return 0;

        var total = 0;
        foreach (var r in renglones)
            if (r.idTipoUsado == idTipo) total += r.piezas;
        return total;
    }

    private static bool TieneNotaDeCancelacion(MovimientoCascoCambioDto m)
        => !string.IsNullOrWhiteSpace(m.usuarioCancelacion)
           || m.fechaCancelacion.HasValue
           || !string.IsNullOrWhiteSpace(m.motivoCancelacion);

    /*
      "Cancelada por JUAN el 22/09/2026 14:30 — Remision equivocada"

      Las tres piezas son opcionales por separado: hay movimientos cancelados de
      antes de que se guardara el usuario, y la frase tiene que seguir teniendo
      sentido con lo que haya. Se arma con lo que exista en vez de imprimir
      "Cancelada por  el " con los huecos a la vista.
    */
    /*
      QUIEN Y CUANDO EN UN RENGLON, EL MOTIVO EN OTRO.

      Iba todo seguido y el motivo quedaba pegado detras de la hora, separado
      solo por una raya: "Cancelada por JUAN el 23/09/2026 01:29 — por que si".
      Al partirse la celda en dos lineas, esa raya caia a mitad de camino y ya no
      se sabia donde acababa el dato del sistema y empezaba lo que escribio la
      persona.

      Arriba quien y cuando, que es lo que se comprueba. Debajo "Motivo:" con
      su rotulo, que es lo que se lee.

      DEVUELVE HTML, no texto: lleva un <br> propio. Por eso el motivo —lo unico
      que escribe una persona— se escapa AQUI. Sin eso, un motivo con un "<"
      partiria la tabla.
    */
    private static string NotaDeCancelacion(MovimientoCascoCambioDto m)
    {
        var texto = new StringBuilder("Cancelada");

        if (!string.IsNullOrWhiteSpace(m.usuarioCancelacion))
            texto.Append(" por ").Append(Texto(m.usuarioCancelacion!.Trim()));

        if (m.fechaCancelacion.HasValue)
            texto.Append(" el ").Append(m.fechaCancelacion.Value.ToString("dd/MM/yyyy HH:mm", Cultura));

        if (!string.IsNullOrWhiteSpace(m.motivoCancelacion))
            texto.Append("<br><b>Motivo:</b> ").Append(Texto(m.motivoCancelacion!.Trim()));

        return texto.ToString();
    }

    /*
      EL CORTE, EN UN RENGLON.

      "Compras" y no "Pedidos": lo que se cuenta ahi son los usados que se
      mueven al COMPRARLE a Zaragoza, no pedidos a clientes. El rotulo decia
      otra cosa que la columna de la tabla, que ya se llama "compras".

      Se quito "Pagos". Estaba siempre en cero porque los movimientos de dinero
      no se capturan en esta pantalla: una cifra que solo puede valer cero no
      informa, y en una linea unica cada hueco cuenta.

      El saldo al corte va en negritas y al final, que es donde termina de leerse
      la suma.
    */
    private static string RenglonCorte(int columnas, CorteCascosCambioDto corte)
    {
        var linea = new StringBuilder();
        linea.Append("<tr class=\"corte fuerte\"><td class=\"der\" colspan=\"").Append(columnas).Append("\">");
        linea.Append("<span class=\"corteDato\">Saldo anterior: <b>")
             .Append(Texto(Dinero(corte.saldoAnterior))).Append("</b></span>");
        linea.Append("<span class=\"corteDato\">Entrega de usados: <b>")
             .Append(Texto(Dinero(corte.entregas))).Append("</b></span>");
        linea.Append("<span class=\"corteDato\">Compras: <b>")
             .Append(Texto(Dinero(corte.pedidos))).Append("</b></span>");
        linea.Append("<span class=\"corteSaldo\">Saldo al corte: <b>")
             .Append(Texto(Dinero(corte.saldo))).Append("</b></span>");
        linea.Append("</td></tr>");
        return linea.ToString();
    }

    /*
      DICE POR CUAL DE LAS DOS FECHAS SE FILTRO, Y CON QUE ESTATUS.

      Un movimiento tiene la que se teclea —cuando se entregaron los usados— y la
      de registro —cuando se capturo—, y el mismo periodo da listas distintas
      segun cual se use. El estatus hace lo mismo. Sin esta linea, dos reportes
      del mismo rango salen distintos y no hay nada en el papel que lo explique.

      Es la linea que el cliente pidio EN GRANDE: 11.5pt, como en legacy.
    */
    public static string Periodo(DateTime? desde, DateTime? hasta, bool porRegistro, string estatus)
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

    /* Sin simbolo de moneda, como en la pantalla: en este reporte toda cifra es
       dinero y el simbolo se repetiria en cada celda sin distinguir nada. */
    private static string Dinero(decimal valor) => valor.ToString("N2", Cultura);

    /* TODO lo que viene de la base pasa por aqui. Una remision o un nombre de
       chofer con un "<" partiria el documento en dos. */
    private static string Texto(string? texto) => WebUtility.HtmlEncode(texto ?? "");

    private static string Celda(string texto) => "<td class=\"izq\">" + Texto(texto) + "</td>";

    private static string CeldaNum(string texto) => "<td class=\"der\">" + Texto(texto) + "</td>";

    /*
      LOS TAMAÑOS SALEN DE Template_Html 43, LA PLANTILLA DE LOS REPORTES DE
      LEGACY. No se "mejoraron" al traerlos: si cada implementacion los redondea
      a su gusto, el papel deja de parecerse a los demas reportes del sistema y
      no hay forma de saber cual es el bueno.

      Nada de var(), nada de flex: ver la cabecera del archivo.
    */
    private static string Estilos() => @"
      body {
        margin: 0;
        font-family: Tahoma, Verdana, Arial, sans-serif;
        font-size: 10pt;
        color: #000;
      }

      /* 98% y centrada, como la plantilla: deja un respiro a los lados sin
         depender de los margenes de la hoja. */
      .datos {
        width: 98%;
        margin: 0 auto;
        border-collapse: collapse;
        font-family: Tahoma, Verdana, Arial, sans-serif;
        font-size: 10pt;
      }

      /*
        LA VARIANTE CON DETALLE APRIETA LA LETRA, Y NO POR CAPRICHO.

        Son siete columnas mas. A 10pt, una hoja apaisada no da para quince
        columnas: la remision y el nombre del chofer se parten en tres renglones
        y la tabla se vuelve un muro. A 8pt caben enteros. Es el unico sitio
        donde este papel se separa de la plantilla, y se separa por aritmetica.
      */
      .datos.apretada { font-size: 8pt; }
      .datos.apretada th, .datos.apretada td { padding: 2px 3px; }
      .datos.apretada th { font-size: 8.5px; }

      /*
        La columna de cancelacion se lleva lo que sobre y se parte en varios
        renglones si hace falta: es la unica que lleva texto libre —el motivo lo
        escribe quien cancela— y forzarla a una linea la cortaria justo donde
        esta la explicacion.
      */
      .colCancel { white-space: normal; font-size: 8.5pt; }

      /* Los rotulos en 11.5px y negritas, como la plantilla — si, px en una
         tabla en puntos; esta asi en el molde de legacy. */
      .datos th {
        font-size: 11.5px;
        font-weight: bold;
        padding: 3px 5px;
        border-bottom: 1px solid #000;
      }
      .datos td { padding: 3px 5px; }

      /*
        LINEAS VERTICALES ENTRE COLUMNAS

        Con quince columnas de numeros y solo el rayado horizontal, la vista se
        va de una columna a la de al lado sin darse cuenta: al leer el renglon
        de JUMBO se acaba mirando la cifra de EXTRA GRANDE. Una linea fina entre
        columnas ata cada numero a su rotulo.

        Fina y gris, no negra: tiene que guiar el ojo, no dibujar una reja. Y
        solo entre columnas —la primera y la ultima no llevan— para que la tabla
        no parezca encajonada.
      */
      .datos th + th,
      .datos td + td { border-left: 1px solid #b8b8b8; }

      .izq { text-align: left; }
      /* Los numeros a la derecha y sin partirse: es lo unico que permite
         comparar una columna de dinero de un vistazo. */
      .der { text-align: right; white-space: nowrap; }
      .fuerte { font-weight: bold; }

      /* El rotulo de un tipo ('EXTRA GRANDE(6)') es mas ancho que sus cifras:
         se le deja partir en dos renglones para que no ensanche la columna. */
      .tipoCol { white-space: normal; }

      /* Rayado apenas perceptible: con quince columnas, el ojo se salta de
         linea al seguir un saldo. Tiene que guiar, no rayar. */
      .datos tbody tr.raya td { background: #f2f2f2; }

      /*
        LOS CANCELADOS SALEN TACHADOS Y EN ROSA, no se omiten: el papel tiene que
        poder cotejarse renglon por renglon contra la pantalla, y un movimiento
        que desaparece del reporte pero sigue en el sistema es justo lo que hace
        que dos cuentas no cuadren sin que nadie sepa por que.
      */
      .datos tbody tr.cancelado td {
        background: #ffb6c1; color: #7a0b26; text-decoration: line-through;
      }

      /* La raya de arriba es la que dice 'aqui se acaba la lista y empieza la
         suma'. Sin ella los totales son una fila mas. */
      .datos tbody tr.totales td {
        font-weight: bold;
        border-top: 2px solid #000;
        border-bottom: 1px solid #000;
        padding-top: 4px; padding-bottom: 4px;
      }

      /*
        EL CORTE VA EN UN RENGLON, con sus cuatro cifras separadas por aire y no
        por una raya: en una tabla que ya esta llena de lineas, una mas partiria
        la linea del corte en cuatro celdas y se leeria como otro renglon de
        datos. El aire basta para separarlas y deja claro que es otra cosa.

        Una raya arriba, esa si, para despegarlo del ultimo movimiento.
      */
      .datos tbody tr.corte td {
        border: 0;
        border-top: 1.5px solid #000000;
        padding: 6px 5px 2px;
        font-size: 10.5pt;
      }
      /* La separacion entre cifras. Generosa a proposito: pegadas, el cero de
         una y el rotulo de la siguiente se leen como un solo dato.
         (Sin comillas en este comentario: va dentro de una cadena verbatim de
         C# y ahi cada comilla tendria que ir doblada.) */
      .corteDato { margin-right: 26px; white-space: nowrap; }
      /* El saldo al corte, mas grande y sin margen: cierra la linea. */
      .corteSaldo { font-size: 11.5pt; white-space: nowrap; }

      /* EL ENCABEZADO DE LA TABLA SE REPITE EN CADA HOJA. Una segunda pagina de
         numeros sin rotulo no se puede leer. */
      thead { display: table-header-group; }
      /* Y ningun renglon se parte por la mitad entre dos hojas: el 'SALDO AL
         CORTE' no puede quedar en una pagina y su numero en la siguiente. */
      tr { page-break-inside: avoid; }

      .vacio { margin: 24px 0; font-size: 11pt; }
    ";
}
