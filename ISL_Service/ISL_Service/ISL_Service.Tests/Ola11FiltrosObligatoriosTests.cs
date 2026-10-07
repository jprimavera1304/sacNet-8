using ISL_Service.Application.Reportes;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// DOS REPORTES QUE PARECIAN ROTOS EN LA BASE Y SOLO ESTABAN MAL PEDIDOS.
///
/// El 106 (Clientes CON Compra Por DIA) y el 1245 (Listas de Precios de
/// productos) contestaban 500 "Error de base de datos" y entraron a la auditoria
/// como defecto heredado. No lo eran: su SP arma SQL dinamico con la lista que
/// le llega y se rompe solo si llega vacia, y Mac31 lo impide desde la forma.
/// Con el filtro puesto los dos entregan PDF e igualan a Legacy renglon por
/// renglon (migracion/ola-11-reportes-restantes.md).
///
/// Estas pruebas son el seguro de que la guarda no se afloje y, sobre todo, de
/// que no se extienda a los reportes de la misma familia que SI saben contestar
/// con la lista vacia: el 1240 y las cuatro listas personalizadas de Tauro.
/// </summary>
public class Ola11FiltrosObligatoriosTests
{
    private const int Acumuladores = 1;
    private const int Productos = 3;

    [Fact]
    public void El_106_Sin_Dias_Se_Rebota_Antes_De_Llegar_Al_SP()
    {
        var falta = FiltrosObligatoriosDeReporte.QueFalta(
            "clientes_con_compra_por_dia", Acumuladores, diasSemana: 0, subcategorias: 0);

        Assert.NotNull(falta);
        Assert.Contains("idDiasSemana", falta);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void El_106_Con_Al_Menos_Un_Dia_Pasa(int dias)
    {
        Assert.Null(FiltrosObligatoriosDeReporte.QueFalta(
            "clientes_con_compra_por_dia", Acumuladores, dias, subcategorias: 0));
    }

    [Theory]
    [InlineData("listas_de_precios")]
    [InlineData("listas_de_precios_imp")]
    public void El_1245_Sin_Subcategorias_Se_Rebota(string clave)
    {
        var falta = FiltrosObligatoriosDeReporte.QueFalta(
            clave, Productos, diasSemana: 0, subcategorias: 0);

        Assert.NotNull(falta);
        Assert.Contains("idSubcategorias", falta);
    }

    [Theory]
    [InlineData("listas_de_precios")]
    [InlineData("listas_de_precios_imp")]
    public void El_1245_Con_Subcategorias_Pasa(string clave)
    {
        Assert.Null(FiltrosObligatoriosDeReporte.QueFalta(
            clave, Productos, diasSemana: 0, subcategorias: 15));
    }

    [Theory]
    [InlineData("listas_de_precios")]
    [InlineData("listas_de_precios_imp")]
    public void Las_Listas_De_Acumuladores_NO_Exigen_Subcategoria(string clave)
    {
        /*
          Aqui el resolver elige el 1240 o una de las cuatro de Tauro
          (1243/1244/1246/1247), y las cinco entregan documento con Param11
          vacio: esta comprobado en la ola 7 y en la ola 11. Si la guarda se
          extendiera a ellas, se romperian cinco reportes que hoy funcionan.
        */
        Assert.Null(FiltrosObligatoriosDeReporte.QueFalta(
            clave, Acumuladores, diasSemana: 0, subcategorias: 0));
    }

    [Theory]
    [InlineData("acumuladores_y_productos")]
    [InlineData("clientes_con_compra")]
    [InlineData("utilidad")]
    [InlineData("inventario_actual")]
    [InlineData("")]
    [InlineData(null)]
    public void Ninguna_Otra_Clave_Se_Toca(string? clave)
    {
        Assert.Null(FiltrosObligatoriosDeReporte.QueFalta(
            clave, Productos, diasSemana: 0, subcategorias: 0));
    }

    [Fact]
    public void La_Clave_Se_Reconoce_Sin_Importar_Mayusculas_Ni_Espacios()
    {
        Assert.NotNull(FiltrosObligatoriosDeReporte.QueFalta(
            "  Clientes_Con_Compra_Por_Dia  ", Acumuladores, 0, 0));
    }
}
