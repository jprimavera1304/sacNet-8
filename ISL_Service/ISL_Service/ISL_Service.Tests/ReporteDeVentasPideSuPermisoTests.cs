using System.Reflection;
using ISL_Service.Controllers;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// QUE CADA ACCION DEL REPORTE DE VENTAS PIDA SU CLAVE, Y QUE LO DESCONOCIDO
/// CAIGA DEL LADO CERRADO.
/// </summary>
/// <remarks>
/// La accion llega en el CUERPO de la peticion, asi que la elige quien llama.
/// Eso hace que el reparto de claves sea una decision de seguridad y no un
/// detalle: si una accion que nadie mapeo acabara en la clave mas barata -o en
/// ninguna-, bastaria con inventarse un nombre de accion para imprimir sin
/// permiso. Por eso lo no reconocido cae en `ventas.reimprimir`, que es la mas
/// restrictiva de las tres, y por eso hay una prueba que lo fija.
///
/// Se llega al metodo por reflexion a proposito: es privado y debe seguirlo
/// siendo. Hacerlo publico para probarlo seria ensanchar la superficie del
/// controlador por comodidad de la prueba.
/// </remarks>
public class ReporteDeVentasPideSuPermisoTests
{
    private static string[] Claves(string? accion) =>
        (string[])typeof(VentasConsultaController)
            .GetMethod("PermisoDeLaAccion", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object?[] { accion })!;

    [Theory]
    [InlineData("pantalla", "ventas.pantalla")]
    [InlineData("PANTALLA", "ventas.pantalla")]
    [InlineData("  imprimir  ", "ventas.imprimir")]
    [InlineData("reimprimir", "ventas.reimprimir")]
    public void Cada_Accion_Pide_Su_Clave(string accion, string esperada)
    {
        Assert.Contains(esperada, Claves(accion));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_Accion_Se_Trata_Como_Pantalla(string? accion)
    {
        // Es el valor por omision del DTO, y es la accion mas inofensiva de las
        // tres: ver el PDF en la pantalla.
        Assert.Contains("ventas.pantalla", Claves(accion));
    }

    [Theory]
    [InlineData("algo_que_nadie_mapeo")]
    [InlineData("descargar")]
    [InlineData("exportar")]
    public void Lo_Desconocido_Cae_En_La_Mas_Restrictiva(string accion)
    {
        var claves = Claves(accion);
        Assert.Contains("ventas.reimprimir", claves);
        Assert.DoesNotContain("ventas.pantalla", claves);
        Assert.DoesNotContain("ventas.imprimir", claves);
    }

    [Fact]
    public void Ninguna_Accion_Se_Queda_Sin_Clave()
    {
        foreach (var accion in new[] { "pantalla", "imprimir", "reimprimir", "", "raro", null })
            Assert.NotEmpty(Claves(accion));
    }
}
