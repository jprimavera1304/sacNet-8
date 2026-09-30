using ISL_Service.Infrastructure.Reports;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// DE QUE CENTRO SALIO EL REPORTE, EN EL ENCABEZADO.
///
/// Legacy no lo trae: el logo del encabezado es el mismo (Logo_Mac.png) en
/// MacZ, MacZCS1 y MacZCS4 y no hay marca de base. Con un selector, el MISMO
/// reporte del mismo periodo sacado de dos centros trae numeros distintos y se
/// ve identico.
///
/// Los trozos de HTML de estas pruebas NO estan inventados: son los de la
/// plantilla de verdad (dbo.Template_Html, descripcion 'cuerpo_n'), con sus
/// entidades y su forma. Una prueba con HTML de mentira habria pasado igual y
/// no diria nada.
/// </summary>
public class SelloDeCentroTests
{
    /*
      EL ENCABEZADO COMO LLEGA AQUI: la plantilla de verdad (dbo.Template_Html,
      'cuerpo_n') pero YA RENDERIZADA. Importa que sea asi y no con los #Tags#
      puestos: este codigo corre DESPUES de armar el HTML, cuando los tags ya
      se sustituyeron. Probarlo con la plantilla cruda daria confianza falsa.
    */
    private const string HeaderReal =
        "<span style=\"font-size:12pt;font-weight:normal;\">FECHAS: 30-07-2026 AL 30-09-2026</span> &nbsp;&nbsp;&nbsp; " +
        "<span style=\"font-size:12pt;font-weight:normal;\">EMPRESA: TODOS</span>  &nbsp;&nbsp;&nbsp;  <br /> " +
        "<span style=\"font-size:11pt;\">Almacen: <b>TODOS</b></span> &nbsp;&nbsp; " +
        "<span style=\"font-size:9pt;\">Impresi&oacute;n: <b>30-09-2026 14:08:18</b></span>";

    [Fact]
    public void Sin_Centro_El_Reporte_Sale_Exactamente_Igual_Que_Hoy()
    {
        Assert.Equal(HeaderReal, SelloDeCentro.Html(HeaderReal, null));
        Assert.Equal(HeaderReal, SelloDeCentro.Html(HeaderReal, "   "));
        Assert.Equal("Folios", SelloDeCentro.Nombre("Folios", null));
    }

    [Fact]
    public void El_Centro_Va_En_EL_ENCABEZADO_Junto_A_Empresa_Y_Almacen()
    {
        var html = SelloDeCentro.Html(HeaderReal, "EJE 6");

        Assert.Contains("CENTRO: <b>EJE 6</b>", html);

        /* Entre los datos del encabezado: despues de EMPRESA y ALMACEN y antes
           de Impresion, que es el renglon donde uno ya mira. */
        Assert.True(html.IndexOf("EMPRESA: TODOS") < html.IndexOf("CENTRO:"));
        Assert.True(html.IndexOf("CENTRO:") < html.IndexOf("Impresi&oacute;n"));

        /* Y NO como aviso arriba de todo: eso es solo el respaldo. */
        Assert.DoesNotContain("Centro de servicio:", html);

        /* No se perdio nada de lo que ya decia. */
        Assert.Contains("FECHAS: 30-07-2026 AL 30-09-2026", html);
        Assert.Contains("EMPRESA: TODOS", html);
        Assert.Contains("Almacen: <b>TODOS</b>", html);
    }

    [Fact]
    public void Sin_Ninguno_De_Los_Dos_Se_Ve_Igual_Aunque_Sea_Arriba()
    {
        /* Vale mas verlo feo que no verlo. */
        var html = SelloDeCentro.Html("<html><body><table><tr><td>1</td></tr></table></body></html>", "XOLA");
        Assert.Contains("Centro de servicio: XOLA", html);
        Assert.True(html.IndexOf("Centro de servicio") > html.IndexOf("<body>"));
    }

    [Fact]
    public void El_Nombre_Del_Reporte_Tambien_Lo_Dice()
    {
        /* Se ve en la pestaña del navegador y en el nombre del archivo. */
        Assert.Equal("Folios - Centro EJE 6", SelloDeCentro.Nombre("Folios", "EJE 6"));
    }

    [Fact]
    public void Un_Nombre_Con_Html_Adentro_No_Se_Cuela()
    {
        var html = SelloDeCentro.Html(HeaderReal, "<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
