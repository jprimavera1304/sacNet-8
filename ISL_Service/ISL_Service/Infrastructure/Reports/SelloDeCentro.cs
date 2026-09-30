using System.Text.RegularExpressions;

namespace ISL_Service.Infrastructure.Reports;

/// <summary>
/// Le pone al reporte de que centro salio.
/// </summary>
/// <remarks>
/// LEGACY NO LO TIENE Y AQUI SI HACE FALTA.
///
/// Se comprobo: el logo del encabezado es el mismo (Logo_Mac.png) en MacZ, en
/// MacZCS1 y en MacZCS4, y no hay ninguna marca de base ni de empresa. En Mac31
/// daba igual, porque cada centro tiene su propia instalacion apuntada a su
/// base — la aplicacion ES el centro. Aqui hay un selector, asi que dos PDF del
/// mismo reporte y del mismo periodo pueden traer numeros distintos y verse
/// identicos. Eso es exactamente "generé y me lo dio mal".
///
/// SOLO SE SELLA CUANDO NO ES LA MATRIZ. Los reportes de la principal siguen
/// saliendo byte por byte como hoy, que era la condicion: que no se vean
/// distintos de los de Mac31.
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

    /// <summary>
    /// Mete una linea visible al principio del cuerpo del reporte.
    /// </summary>
    /// <remarks>
    /// Se inserta despues de &lt;body&gt; y no se re-arma el HTML: el documento
    /// lo produce el renderizador de siempre y aqui solo se le agrega una linea.
    /// Si no hubiera &lt;body&gt; se pone al principio, que sigue siendo visible.
    /// </remarks>
    public static string Html(string html, string? nombreDelCentro)
    {
        var centro = (nombreDelCentro ?? "").Trim();
        if (centro.Length == 0 || string.IsNullOrEmpty(html)) return html;

        var linea =
            "<div style=\"font:600 13px Arial,sans-serif;color:#7a2e00;" +
            "background:#fff3e0;border:1px solid #ffb74d;border-radius:4px;" +
            "padding:6px 10px;margin:0 0 8px 0;text-align:center;\">" +
            "Centro de servicio: " + Escapar(centro) +
            "</div>";

        var m = Regex.Match(html, "<body[^>]*>", RegexOptions.IgnoreCase);
        if (m.Success)
            return html.Insert(m.Index + m.Length, linea);

        return linea + html;
    }

    private static string Escapar(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
