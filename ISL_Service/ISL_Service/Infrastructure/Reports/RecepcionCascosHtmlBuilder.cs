using System.Globalization;
using System.Text;
using ISL_Service.Application.DTOs.RecepcionCascos;

namespace ISL_Service.Infrastructure.Reports;

/*
  EL PAPEL DE LA RECEPCION DE CASCOS USADOS

  Sustituye la hoja que hoy se llena a mano: "Relacion de cascos usados que
  entregan del dia [FECHA]". Una fila por recepcion, una columna por tamano de
  casco, y los totales abajo.

  POR QUE ESTE ARCHIVO SE PARECE TANTO A UsadosLegacyHtmlBuilder
  Porque tiene que salir igual. Los dos papeles conviven en el mismo escritorio
  y la gente los lee como si fueran del mismo sistema, que es lo que son. La
  tipografia, los tamanos, el encabezado que se repite y el pie salen de la
  misma plantilla de legacy (Template_Html id 43). Lo que NO se copio es la
  tabla, porque esta hoja no tiene remision ni importes: tiene tamanos.

  LAS RESTRICCIONES DE wkhtmltopdf 0.12.4 MANDAN (su WebKit es de la epoca de
  Safari 5), y no son opinables:

    * NADA de variables CSS. var(--x) se descarta en silencio y el color cae a
      negro, sin error y sin aviso.
    * NADA de flexbox. El encabezado se maqueta con <table>.
    * NADA de object-fit. El logo se acota con height y ancho automatico.
    * Tahoma y no Century Gothic: Century Gothic viene con Office y el servidor
      no lo tiene. Si la fuente cambia, cambian las metricas y los cortes de
      renglon, y el mismo reporte sale de 3 hojas en una maquina y de 4 en
      otra.
*/
public static class RecepcionCascosHtmlBuilder
{
    public const string Titulo = "RELACIÓN DE CASCOS USADOS QUE ENTREGAN";

    /* Se fija aqui y no se toma de la maquina: un servidor en otra region
       imprimiria los miles al reves. */
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    /* ================================================================== */
    /*  EL ENCABEZADO QUE SE REPITE EN CADA HOJA                          */
    /* ================================================================== */

    /*
      COMO LLEGA EL NUMERO DE PAGINA

      wkhtmltopdf no sustituye [page] dentro de un --header-html: abre este
      documento con los datos en la direccion (?page=3&topage=14&...). Asi que
      el numero lo pone el propio documento al cargarse, leyendo su direccion.
      Es el unico camino: con [page] a secas el encabezado imprime los
      corchetes tal cual.
    */
    public static string Encabezado(
        string empresa, string logoDataUri,
        DateTime desde, DateTime hasta, string repartidor, string usuario)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<style>");
        html.Append("body{margin:0;font-family:Tahoma,Verdana,Arial,sans-serif;}");
        html.Append(".enc{width:98%;border-collapse:collapse;font-family:Tahoma,Verdana,Arial,sans-serif;font-size:11pt;margin:0 auto;}");
        html.Append(".enc td{vertical-align:top;padding:0;}");
        /* El ancho justo del logo: si la celda creciera, empujaria el titulo. */
        html.Append(".celdaLogo{width:170px;}");
        html.Append(".tit{font-size:14pt;font-weight:bold;}");
        /* La celda de la derecha se encoge a su contenido y no se parte. */
        html.Append(".copia{white-space:nowrap;vertical-align:top;width:1%;}");
        html.Append(".emp{font-size:12pt;font-weight:normal;}");
        html.Append(".oculto{display:none;}");
        html.Append(".pag{font-size:10pt;font-weight:normal;}");
        /* La linea que dice de que periodo es el papel que tienes en la mano. */
        html.Append(".fec{font-size:11.5pt;font-weight:normal;}");
        html.Append(".imp{font-size:10pt;font-weight:normal;}");
        html.Append("hr{border:0;border-top:1px solid #000;margin:4px 0 0;}");
        html.Append("</style>");

        html.Append("<script>function subst(){var v={},p=document.location.search.substring(1).split('&');");
        html.Append("for(var i=0;i<p.length;i++){var z=p[i].split('=',2);v[z[0]]=decodeURIComponent(z[1]||'');}");
        html.Append("var n=['page','topage','frompage'];");
        html.Append("for(var j=0;j<n.length;j++){var e=document.getElementsByClassName(n[j]);");
        html.Append("for(var k=0;k<e.length;k++){e[k].textContent=v[n[j]]||'';}}}</script>");
        html.Append("</head><body onload=\"subst()\">");

        html.Append("<table class=\"enc\"><tr>");

        /*
          Sin logo si no se encontro el archivo, y sin celda tampoco: una celda
          de 170 px en blanco dejaria el titulo descolgado, como si faltara
          algo. Solo se fija el ALTO y el ancho queda en auto, para que el logo
          de cada empresa conserve su proporcion.
        */
        if (!string.IsNullOrWhiteSpace(logoDataUri))
        {
            html.Append("<td class=\"celdaLogo\"><img src=\"")
                .Append(logoDataUri)
                .Append("\" alt=\"\" height=\"30\"></td>");
        }

        html.Append("<td align=\"left\">");
        html.Append("<span class=\"tit\">").Append(Texto(Titulo)).Append("</span>&nbsp;&nbsp;");
        /* El nombre de la empresa se oculta, no se quita: el logo ya dice de
           quien es el papel, y repetirlo le roba sitio al titulo. Si algun dia
           el logo no llega, es borrar una clase. */
        html.Append("<span class=\"emp oculto\">").Append(Texto(empresa)).Append("</span><br>");
        html.Append("<span class=\"fec\">")
            .Append(Texto(Periodo(desde, hasta, repartidor)))
            .Append("</span>");
        html.Append("</td>");

        /* A la derecha, lo que identifica a la COPIA: que hoja es, quien la
           saco y cuando. En celda propia para que no se mueva aunque crezca el
           titulo. */
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
    /*  EL CUERPO                                                          */
    /* ================================================================== */

    /*
      LAS COLUMNAS SALEN DEL CATALOGO, NO DEL CODIGO.

      La lista de totales trae un renglon por tipo activo —los 8 de la hoja— y
      de ahi salen las columnas, en su orden. El dia que la empresa dé de alta
      un noveno tamano, la columna aparece sola. Escribirlas aqui obligaria a
      recompilar el backend por un renglon de catalogo.

      Y se pintan TODAS, incluso las que salen en cero, porque la hoja de papel
      tiene las ocho columnas siempre: quien la lee cuenta por posicion.
    */
    public static string Construir(DatosReporteRecepcionCascos datos)
    {
        var tipos = datos.Totales.OrderBy(t => t.orden).ThenBy(t => t.idTipoUsado).ToList();

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<style>");
        html.Append("body{margin:0;font-family:Tahoma,Verdana,Arial,sans-serif;font-size:10pt;}");
        html.Append("table.datos{width:100%;border-collapse:collapse;}");
        /* 11.5px dentro de una tabla medida en puntos: esta asi en la
           plantilla de legacy y se deja igual a proposito. */
        html.Append("table.datos th{font-size:11.5px;font-weight:bold;border-bottom:1px solid #000;padding:3px 4px;}");
        html.Append("table.datos td{padding:2px 4px;}");
        html.Append(".izq{text-align:left;}");
        html.Append(".der{text-align:right;}");
        html.Append(".cen{text-align:center;}");
        /* Las columnas de tamano son angostas y todas iguales: asi la rejilla
           se lee en vertical, que es como se suma a mano. */
        html.Append(".tipoCol{width:52px;}");
        html.Append(".colFecha{width:66px;}");
        html.Append(".colTotal{width:54px;}");
        html.Append(".grupo td{font-weight:bold;border-top:1px solid #000;padding-top:5px;}");
        html.Append(".sub td{font-weight:bold;border-top:1px solid #999;}");
        html.Append(".tot td{font-weight:bold;border-top:2px solid #000;border-bottom:2px solid #000;}");
        html.Append(".obs{font-size:9pt;}");
        /* El cero no se imprime: una rejilla con sesenta ceros estorba para
           encontrar el numero que si cuenta. Es lo que hace la hoja a mano. */
        html.Append(".vacio{color:#fff;}");
        html.Append(".nada{font-size:11pt;padding:14px 4px;}");

        /*
          LAS DOS REGLAS DE IMPRESION. Sin ellas el papel sale mal, y se vio:
          en la segunda hoja el rotulo de columnas se pintaba ENCIMA de los dos
          primeros renglones y no se podia leer ninguno de los tres.

          La primera repite el rotulo en cada hoja —una pagina de numeros sin
          rotulo no se puede leer— y la segunda es la que arregla el encime: sin
          ella el corte de pagina cae a media fila, la fila se parte y lo que
          queda arriba se cruza con el rotulo repetido.

          Las dos estan copiadas del reporte de usados a cambio, que ya habia
          pasado por esto.
        */
        html.Append("thead{display:table-header-group;}");
        html.Append("tr{page-break-inside:avoid;}");

        html.Append("</style></head><body>");

        html.Append("<table class=\"datos\">");

        /* ---- encabezado de columnas ---- */
        html.Append("<thead><tr>");
        html.Append("<th class=\"izq colFecha\">FECHA</th>");
        html.Append("<th class=\"izq\">QUIÉN ENTREGÓ</th>");
        foreach (var t in tipos)
            html.Append("<th class=\"der tipoCol\">").Append(Texto(Rotulo(t))).Append("</th>");
        html.Append("<th class=\"der colTotal\">TOTAL</th>");
        html.Append("<th class=\"izq obs\">OBSERVACIONES</th>");
        html.Append("</tr></thead><tbody>");

        if (datos.Renglones.Count == 0)
        {
            /*
              Un papel vacio tiene que DECIR que esta vacio. Una hoja con solo
              los rotulos de columna se lee como un reporte que no se imprimio
              bien, y alguien lo vuelve a sacar.
            */
            html.Append("<tr><td class=\"nada cen\" colspan=\"")
                .Append(tipos.Count + 4)
                .Append("\">No hubo cascos recibidos en este periodo.</td></tr>");
        }
        else
        {
            /*
              UN SOLO BLOQUE, ORDENADO POR REPARTIDOR.

              Antes esto se partia en tres secciones con su subtotal —agentes,
              tiendas, galleros—, que es como venia la hoja de papel. Esa
              separacion se cayo entera cuando el negocio corrigio quien
              entrega: no hay tres clases de origen, hay repartidores. Un
              subtotal por repartidor no se pidio y ademas repetiria el renglon
              en los dias de una sola entrega.
            */
            var porRecepcion = datos.Renglones
                .GroupBy(r => r.idRecepcion)
                .Select(g => new
                {
                    Id = g.Key,
                    Primero = g.First(),
                    Piezas = g.ToDictionary(x => x.idTipoUsado, x => x.piezas)
                })
                .OrderBy(x => x.Primero.quienEntrego, StringComparer.Create(Cultura, true))
                .ThenBy(x => x.Primero.fecha)
                .ThenBy(x => x.Id)
                .ToList();

            foreach (var r in porRecepcion)
            {
                html.Append("<tr>");
                html.Append("<td class=\"izq\">")
                    .Append(r.Primero.fecha.ToString("dd/MM/yyyy", Cultura)).Append("</td>");
                html.Append("<td class=\"izq\">").Append(Texto(r.Primero.quienEntrego)).Append("</td>");

                foreach (var t in tipos)
                {
                    r.Piezas.TryGetValue(t.idTipoUsado, out var piezas);
                    html.Append("<td class=\"der\">").Append(Numero(piezas)).Append("</td>");
                }

                html.Append("<td class=\"der\">")
                    .Append(piezasDeGrupo(r.Piezas).ToString("N0", Cultura)).Append("</td>");
                html.Append("<td class=\"izq obs\">").Append(Texto(r.Primero.observaciones)).Append("</td>");
                html.Append("</tr>");
            }

            /* ---- el total general, ultima fila de la tabla ---- */
            html.Append("<tr class=\"tot\">");
            html.Append("<td class=\"izq\" colspan=\"2\">TOTAL DE CASCOS RECIBIDOS</td>");
            var granTotal = 0;
            foreach (var t in tipos)
            {
                var suma = porRecepcion.Sum(x => x.Piezas.TryGetValue(t.idTipoUsado, out var p) ? p : 0);
                granTotal += suma;
                html.Append("<td class=\"der\">").Append(Numero(suma)).Append("</td>");
            }
            html.Append("<td class=\"der\">").Append(granTotal.ToString("N0", Cultura)).Append("</td>");
            html.Append("<td></td></tr>");
        }

        html.Append("</tbody></table>");
        html.Append("</body></html>");
        return html.ToString();
    }

    /* ---------------------------------------------------------------- */

    private static int piezasDeGrupo(Dictionary<int, int> piezas) => piezas.Values.Sum();

    /*
      El rotulo de la columna: el nombre corto si lo hay. El catalogo guarda
      'MINI CHICO(1)' en el corto y '11141608 MINI CHICO (1)' en el largo —ese
      numero es una clave de producto pegada al nombre y no cabe en una columna
      de 52 px.
    */
    private static string Rotulo(TotalPorTipoRecepcionCascoDto tipo)
    {
        var corto = (tipo.clave ?? "").Trim();
        if (corto.Length > 0) return corto;
        return (tipo.nombre ?? "").Trim();
    }

    /* El cero se imprime en blanco: ocupa su sitio en la rejilla pero no
       estorba al leer. Si se quitara del todo, la celda se colapsaria y las
       columnas dejarian de alinearse entre renglones. */
    private static string Numero(int valor)
        => valor == 0 ? "<span class=\"vacio\">0</span>" : valor.ToString("N0", Cultura);

    private static string Periodo(DateTime desde, DateTime hasta, string repartidor)
    {
        var texto = new StringBuilder();

        texto.Append(desde.Date == hasta.Date
            ? $"Del día {desde:dd/MM/yyyy}"
            : $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}");

        if (!string.IsNullOrWhiteSpace(repartidor))
            texto.Append("  ·  ").Append(repartidor.Trim());

        return texto.ToString();
    }

    /* Escapar es obligatorio y no decorativo: los nombres de repartidor y las
       observaciones los teclea una persona, y un "&" suelto rompe el
       documento sin que wkhtmltopdf diga nada. */
    private static string Texto(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        return valor
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
