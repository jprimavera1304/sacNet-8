using System.Text.RegularExpressions;

namespace ISL_Service.Infrastructure.Reports;

/// <summary>
/// Le pone al reporte de que centro de servicio salio.
/// </summary>
/// <remarks>
/// LEGACY NO LO TIENE Y AQUI HACE FALTA.
///
/// Se comprobo: el logo del encabezado es el mismo (Logo_Mac.png) en MacZ, en
/// MacZCS1 y en MacZCS4, y no hay ninguna marca de base. En Mac31 daba igual,
/// porque cada centro tiene su instalacion apuntada a su base — la aplicacion
/// ES el centro. Con un selector no: el MISMO reporte, del mismo periodo,
/// sacado de dos centros trae numeros distintos y se ve identico.
///
/// DONDE SE PONE. En el encabezado, junto a FECHAS / EMPRESA / ALMACEN, que es
/// donde uno ya mira para saber de que es el reporte. En concreto justo antes
/// de "Impresion:", que es otro dato del mismo renglon.
///
/// POR QUE AHI Y NO EN EL HUECO #ParametrosTexto# DE LA PLANTILLA: porque esto
/// corre sobre el HTML YA ARMADO, y para entonces los #Tags# de la plantilla ya
/// se sustituyeron. Apuntar a ese hueco habria sido codigo que no se ejecuta
/// nunca — se ve razonable y no hace nada.
///
/// Si una plantilla no trae "Impresion:", se pone un aviso al principio: vale
/// mas verlo feo que no verlo.
///
/// SOLO SE SELLA CUANDO NO ES LA MATRIZ. Los reportes de la principal siguen
/// saliendo exactamente como hoy, que era la condicion.
/// </remarks>
public static class SelloDeCentro
{
    /// <summary>"Folios" + "EJE 6" -> "Folios - Centro EJE 6". Sin centro, igual.</summary>
    public static string Nombre(string nombreDelReporte, string? nombreDelCentro)
    {
        var centro = (nombreDelCentro ?? "").Trim();
        if (centro.Length == 0) return nombreDelReporte;
        return $"{nombreDelReporte} - Centro {centro}";
    }

    /// <summary>Mete "CENTRO: X" en el encabezado del reporte.</summary>
    public static string Html(string html, string? nombreDelCentro)
    {
        var centro = (nombreDelCentro ?? "").Trim();
        if (centro.Length == 0 || string.IsNullOrEmpty(html)) return html;

        var texto = Escapar(centro);

        /* Junto a "Impresion:", otro dato del mismo encabezado. Se busca con
           acento, sin el y como entidad HTML porque la plantilla lo guarda
           como &oacute;. */
        var impresion = Regex.Match(html, @"<span[^>]*>\s*Impresi(?:ó|&oacute;|o)n\s*:", RegexOptions.IgnoreCase);
        if (impresion.Success)
            return html.Insert(impresion.Index, $"{EnLinea(texto)} &nbsp;&nbsp;&nbsp; ");

        /* Sin ese ancla: que se vea igual, aunque sea arriba de todo. */
        var cuerpo = Regex.Match(html, "<body[^>]*>", RegexOptions.IgnoreCase);
        var aviso =
            "<div style=\"font:600 13px Arial,sans-serif;color:#7a2e00;" +
            "background:#fff3e0;border:1px solid #ffb74d;border-radius:4px;" +
            "padding:6px 10px;margin:0 0 8px 0;text-align:center;\">" +
            "Centro de servicio: " + texto + "</div>";

        return cuerpo.Success
            ? html.Insert(cuerpo.Index + cuerpo.Length, aviso)
            : aviso + html;
    }

    /// <summary>Como se ve dentro del encabezado, igual que EMPRESA o ALMACEN.</summary>
    private static string EnLinea(string centroYaEscapado) =>
        $"<span style=\"font-size:11.5pt;font-weight:normal;\">CENTRO: <b>{centroYaEscapado}</b></span>";

    private static string Escapar(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
