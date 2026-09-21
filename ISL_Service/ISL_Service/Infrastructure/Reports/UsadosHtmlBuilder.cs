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

  Esto arma el HTML del reporte y lo manda a wkhtmltopdf, igual que la remision
  de Ventas: mismo camino y mismo visor, asi que se imprime, se guarda y se
  manda por correo como cualquier otro documento del sistema.

  NO USA LAS PLANTILLAS DE LEGACY. La remision las usa porque tiene que salir
  identica a la de Mac31 —la ve el cliente—; este reporte no existe en el
  sistema viejo, asi que copiar una plantilla de alla seria heredarle las
  limitaciones a un documento nuevo sin ganar parecido con nada.

  EL ESTILO VA INCRUSTADO y no en un archivo aparte: wkhtmltopdf corre en otro
  proceso y no comparte el servidor de la aplicacion, asi que cualquier hoja de
  estilo enlazada llegaria vacia y el PDF saldria sin formato.
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
        bool porRegistro)
    {
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<title>Usados a cambio</title><style>");
        html.Append(Estilos());
        html.Append("</style></head><body>");

        html.Append("<h1>Usados a cambio</h1>");
        html.Append("<p class=\"periodo\">")
            .Append(WebUtility.HtmlEncode(Periodo(desde, hasta, porRegistro)))
            .Append("</p>");

        if (movimientos.Count == 0)
        {
            /* Se entrega la hoja igual, diciendolo. Un PDF en blanco se lee como
               un error del sistema; este dice que el periodo no tuvo nada. */
            html.Append("<p class=\"vacio\">No hubo movimientos en este periodo.</p>");
            html.Append(Pie());
            html.Append("</body></html>");
            return html.ToString();
        }

        html.Append("<table><thead><tr>");
        html.Append("<th>Fecha</th><th>Tipo</th><th>Remisión</th><th>Chofer</th>");
        html.Append("<th class=\"num\">Usados</th><th class=\"num\">Importe</th><th class=\"num\">Saldo</th>");
        html.Append("</tr></thead><tbody>");

        foreach (var m in movimientos)
        {
            var cancelado = m.estatus == 2;
            /*
              Los cancelados salen TACHADOS, no se omiten: el papel tiene que
              poder cotejarse renglon por renglon contra la pantalla, y un
              movimiento que desaparece del reporte pero sigue en el sistema es
              justo lo que hace que dos cuentas no cuadren y nadie sepa por que.
            */
            html.Append(cancelado ? "<tr class=\"cancelado\">" : "<tr>");
            html.Append(Celda(m.fecha.ToString("dd/MM/yyyy", Cultura)));
            html.Append(Celda(cancelado ? "CANCELADA" : (m.tipoMovimientoNombre ?? "")));
            html.Append(Celda(m.remision ?? "—"));
            html.Append(Celda(m.persona ?? "—"));
            html.Append(CeldaNum(m.totalPiezas > 0 ? m.totalPiezas.ToString("N0", Cultura) : "—"));
            html.Append(CeldaNum(Dinero(m.importeConSigno)));
            html.Append(CeldaNum(Dinero(m.saldo)));
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");

        if (corte != null)
        {
            /*
              EL CORTE, CON EL SALDO ANTERIOR DELANTE.

              Sin el, los numeros del periodo se leen como si la cuenta empezara
              en cero ese dia: saldo anterior + lo de estos dias = saldo, y las
              tres cifras tienen que estar para que la suma se pueda seguir.
            */
            html.Append("<table class=\"corte\"><tbody>");
            html.Append(Renglon("Saldo anterior", Dinero(corte.saldoAnterior)));
            html.Append(Renglon("Entregas", Dinero(corte.entregas)));
            html.Append(Renglon("Pedidos", Dinero(corte.pedidos)));
            html.Append(Renglon("Pagos", Dinero(corte.pagos)));
            html.Append(Renglon("Saldo", Dinero(corte.saldo), destacado: true));
            html.Append("</tbody></table>");
        }

        html.Append(Pie());
        html.Append("</body></html>");
        return html.ToString();
    }

    /*
      DICE POR CUAL DE LAS DOS FECHAS SE FILTRO.

      Un movimiento tiene la que se teclea —cuando se entregaron los usados— y
      la de registro —cuando se capturo—, y el mismo periodo da listas distintas
      segun cual se use. Sin esta linea, dos reportes del mismo rango salen con
      renglones distintos y no hay nada en el papel que lo explique.
    */
    private static string Periodo(DateTime? desde, DateTime? hasta, bool porRegistro)
    {
        var cual = porRegistro ? "Por fecha de registro" : "Por fecha del movimiento";

        if (desde == null && hasta == null) return cual + " · Todo el historial";
        if (desde != null && hasta != null)
            return cual + " · Del " + desde.Value.ToString("dd/MM/yyyy", Cultura) +
                   " al " + hasta.Value.ToString("dd/MM/yyyy", Cultura);
        if (desde != null)
            return cual + " · Desde el " + desde.Value.ToString("dd/MM/yyyy", Cultura);
        return cual + " · Hasta el " + hasta!.Value.ToString("dd/MM/yyyy", Cultura);
    }

    /* La fecha de impresion: dos copias del mismo periodo sacadas en dias
       distintos pueden diferir si entremedias se cancelo algo. */
    private static string Pie()
        => "<p class=\"pie\">Impreso el " +
           WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura)) +
           "</p>";

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
           "<td>" + WebUtility.HtmlEncode(rotulo) + "</td>" +
           "<td class=\"num\">" + WebUtility.HtmlEncode(valor) + "</td></tr>";

    private static string Estilos() => @"
      body { font-family: Arial, Helvetica, sans-serif; color: #1a1a1a; margin: 0; font-size: 11px; }
      h1 { font-size: 16px; margin: 0 0 2px; }
      .periodo { margin: 0 0 12px; font-size: 11px; color: #555; }
      table { width: 100%; border-collapse: collapse; }
      th { background: #eef1f8; text-align: left; padding: 5px 6px; border-bottom: 1px solid #c7cddd; font-size: 10px; text-transform: uppercase; letter-spacing: .04em; }
      td { padding: 4px 6px; border-bottom: 1px solid #e6e6e6; }
      /* Los numeros a la derecha y con cifras del mismo ancho: es lo unico que
         permite comparar una columna de dinero de un vistazo. */
      .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
      /* Los encabezados se repiten en cada hoja: un reporte de varias paginas
         cuya segunda hoja son numeros sin rotulo no se puede leer. */
      thead { display: table-header-group; }
      tr { page-break-inside: avoid; }
      .cancelado td { text-decoration: line-through; color: #8a8a8a; }
      .corte { width: 240px; margin: 14px 0 0 auto; }
      .corte td { border-bottom: none; padding: 2px 6px; }
      .corte .total td { border-top: 1px solid #1a1a1a; font-weight: bold; font-size: 12px; }
      .vacio { margin: 24px 0; color: #555; }
      .pie { margin-top: 18px; font-size: 9px; color: #777; }
    ";
}
