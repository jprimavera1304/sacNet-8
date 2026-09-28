using System.Reflection;
using ISL_Service.Infrastructure.Security;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// PARA 'reportes.*' MANDA MAC31, PERO 'reportes.ver_modulo' ES DEL WEB.
///
/// Al leer los permisos, el servidor borra los de reportes que puso el web y
/// pone los de Mac31. El borrado se llevaba tambien 'reportes.ver_modulo', que
/// NO existe en Mac31: es la llave del web, la que decide si sale la tarjeta de
/// Reportes. Se guardaba en la base y desaparecia en la siguiente lectura, y
/// desde la pantalla se veia como "guardo, dice que si, y al volver no esta".
///
/// Comprobado en produccion de Zaragoza: RICARDO tenia 'reportes.ver_modulo'
/// con Tipo=allow en WUsuarioPermiso, cero reportes encendidos en Mac31, y la
/// casilla salia apagada.
///
/// Ventas, Empleados y Prestamos ya exceptuaban sus llaves del web. Reportes
/// era la unica que no. Esta prueba es el seguro de que siga igualada.
/// </summary>
public class ReportesVerModuloNoSeBorraTests
{
    private static bool MandaLegacy(string clave)
    {
        var metodo = typeof(PermissionService).GetMethod(
            "EsReporteQueMandaLegacy",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(metodo);
        return (bool)metodo!.Invoke(null, new object[] { clave })!;
    }

    [Fact]
    public void Ver_Modulo_De_Reportes_Lo_Manda_El_Web_Y_No_Se_Borra()
    {
        Assert.False(MandaLegacy("reportes.ver_modulo"));
        /* Y da igual como venga escrita: la base no promete mayusculas. */
        Assert.False(MandaLegacy("Reportes.Ver_Modulo"));
    }

    [Theory]
    [InlineData("reportes.ventas.folios.ver")]
    [InlineData("reportes.administracion.cortes.ver")]
    [InlineData("reportes.compras.facturas.ver")]
    public void Los_Reportes_De_Verdad_Los_Sigue_Mandando_Mac31(string clave)
    {
        Assert.True(MandaLegacy(clave));
    }

    [Theory]
    [InlineData("app_movil.reportes")]
    [InlineData("ventas.ver_modulo")]
    [InlineData("empleados.ver")]
    public void Lo_Que_No_Es_De_Reportes_Ni_Se_Toca(string clave)
    {
        Assert.False(MandaLegacy(clave));
    }
}
