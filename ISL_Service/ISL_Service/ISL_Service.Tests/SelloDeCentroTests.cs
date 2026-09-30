using ISL_Service.Infrastructure.Reports;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// De que centro salio el reporte.
///
/// Legacy NO lo trae: se comprobo que el logo del encabezado es el mismo
/// (Logo_Mac.png) en MacZ, MacZCS1 y MacZCS4, y no hay marca de base. En Mac31
/// no hacia falta porque cada centro tiene su instalacion; con un selector si,
/// o dos PDF con numeros distintos se ven identicos.
///
/// La condicion era que los de la MATRIZ se sigan viendo igual que en Mac31.
/// De eso tratan la mitad de estas pruebas.
/// </summary>
public class SelloDeCentroTests
{
    [Fact]
    public void Sin_Centro_El_Reporte_Sale_Exactamente_Igual_Que_Hoy()
    {
        const string html = "<html><body><h1>Folios</h1></body></html>";
        Assert.Equal(html, SelloDeCentro.Html(html, null));
        Assert.Equal(html, SelloDeCentro.Html(html, "   "));
        Assert.Equal("Folios", SelloDeCentro.Nombre("Folios", null));
        Assert.Equal("Folios", SelloDeCentro.Nombre("Folios", ""));
    }

    [Fact]
    public void Con_Centro_Se_Nota_En_El_Nombre_Y_En_El_Cuerpo()
    {
        Assert.Equal("Folios - Centro EJE 6", SelloDeCentro.Nombre("Folios", "EJE 6"));

        var html = SelloDeCentro.Html("<html><body><h1>Folios</h1></body></html>", "EJE 6");
        Assert.Contains("Centro de servicio: EJE 6", html);
        /* Y va DENTRO del cuerpo, no antes: fuera de <body> el navegador lo
           reacomoda y en el PDF puede no salir. */
        Assert.True(html.IndexOf("Centro de servicio") > html.IndexOf("<body>"));
        Assert.True(html.IndexOf("Centro de servicio") < html.IndexOf("<h1>"));
    }

    [Fact]
    public void Si_El_Html_No_Trae_Body_Igual_Se_Ve()
    {
        var html = SelloDeCentro.Html("<table><tr><td>1</td></tr></table>", "XOLA");
        Assert.StartsWith("<div", html);
        Assert.Contains("Centro de servicio: XOLA", html);
    }

    [Fact]
    public void Un_Nombre_Con_Html_Adentro_No_Se_Cuela()
    {
        /* El nombre sale de la base, pero lo captura una persona. */
        var html = SelloDeCentro.Html("<html><body>x</body></html>", "<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
