using ISL_Service.Application.Reportes;

namespace ISL_Service.Tests;

/// <summary>
/// EL REPORTE DE AUTOS USA EL GEMELO, Y LA FILA DE Reportes NO SE TOCA.
///
/// `sp_n_rptAutos` pide `KmPorLitroKmMaximo` y la columna de `[Catalogo Autos]`
/// se llama `KmPorLitroMaximo`. Comprobado en MacZ y en Produccion_svr: la
/// misma errata en las dos, asi que el boton IMPRIMIR de Mac31 tampoco funciona.
///
/// El cambio se hace en codigo y no cambiando el `spV2` de la fila de
/// `Reportes`, porque esa fila la lee tambien Mac31.
/// </summary>
public class ProcedimientosRotosTests
{
    [Fact]
    public void El_De_Autos_Se_Sustituye_Por_Su_Gemelo()
    {
        Assert.Equal("sp_w_rptAutos", ProcedimientosRotosEnLaBase.Bueno("sp_n_rptAutos"));
        Assert.True(ProcedimientosRotosEnLaBase.EstaRoto("sp_n_rptAutos"));
    }

    [Fact]
    public void Un_Procedimiento_Sano_Se_Devuelve_Tal_Cual()
    {
        Assert.Equal("sp_n_rptVentasSaldos", ProcedimientosRotosEnLaBase.Bueno("sp_n_rptVentasSaldos"));
        Assert.False(ProcedimientosRotosEnLaBase.EstaRoto("sp_n_rptVentasSaldos"));
    }

    [Theory]
    [InlineData("SP_N_RPTAUTOS")]
    [InlineData("  sp_n_rptAutos  ")]
    public void El_Nombre_Se_Reconoce_Sin_Importar_Mayusculas_Ni_Espacios(string comoVino)
    {
        /* `sp_ConsultaReportes` devuelve lo que alguien escribio a mano en la
           fila: viene con espacios y con la caja que sea. */
        Assert.Equal("sp_w_rptAutos", ProcedimientosRotosEnLaBase.Bueno(comoVino));
    }

    [Fact]
    public void Nada_Devuelve_Nada_Y_No_Revienta()
    {
        Assert.Equal("", ProcedimientosRotosEnLaBase.Bueno(null));
        Assert.Equal("", ProcedimientosRotosEnLaBase.Bueno("   "));
    }
}
