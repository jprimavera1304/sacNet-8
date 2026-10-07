using ISL_Service.Application.Reportes;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// EL NODO "Cobranza" IMPRIMIA CON LA PLANTILLA DE LA OTRA EMPRESA.
///
/// Medido contra Zaragoza (base MacZ) con el mismo renglon de dbo.Parametros que
/// Legacy: el backend nuevo entregaba 1 298 importes y Legacy 6 734, y 46 de los
/// nuestros eran numeros que Legacy no tiene. No era el dato ni el procedimiento:
/// era el id, y por lo tanto la plantilla. Legacy cambia el 1160 por el 1161
/// cuando la funcionalidad es "ZARA" (ReporteV2Service.cs:783) y eso no estaba
/// copiado. Ver migracion/zaragoza-estado.md.
///
/// Estas pruebas fijan las dos direcciones -que Zaragoza reciba el 1161 y que
/// Tauro SIGA recibiendo el 1160- y, sobre todo, que la condicion no se afloje a
/// un "no es Tauro": una tercera empresa tiene que quedarse con el 1160, que es
/// lo que hace Legacy.
/// </summary>
public class ReporteCobranzaPorEmpresaTests
{
    [Theory]
    [InlineData("ZARA")]
    [InlineData("zara")]
    [InlineData("  ZARA  ")]
    public void Zaragoza_Recibe_El_1161(string funcionalidad)
    {
        Assert.Equal(1161, ReporteCobranzaPorEmpresa.Resolver(funcionalidad));
    }

    [Theory]
    [InlineData("TAU")]
    [InlineData("tau")]
    [InlineData(" TAU ")]
    public void Tauro_Sigue_Recibiendo_El_1160(string funcionalidad)
    {
        Assert.Equal(1160, ReporteCobranzaPorEmpresa.Resolver(funcionalidad));
    }

    /// <summary>
    /// La guarda NO puede ser "si no es Tauro, es Zaragoza". Legacy pregunta por
    /// igualdad exacta con "ZARA", asi que cualquier otra empresa se queda con el
    /// 1160. Si alguien cambia esto por un EsTauro() negado, esta prueba lo
    /// detiene.
    /// </summary>
    [Theory]
    [InlineData("LUBRI")]
    [InlineData("MACL")]
    [InlineData("ZARAGOZA")]
    [InlineData("AVIACION")]
    public void Cualquier_Otra_Empresa_Se_Queda_Con_El_1160(string funcionalidad)
    {
        Assert.Equal(1160, ReporteCobranzaPorEmpresa.Resolver(funcionalidad));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_Funcionalidad_Se_Queda_Con_El_1160(string? funcionalidad)
    {
        Assert.Equal(1160, ReporteCobranzaPorEmpresa.Resolver(funcionalidad));
    }

    [Fact]
    public void Los_Dos_Ids_Son_Los_Del_Enum_De_Legacy()
    {
        // enumReportes.cs:239 y :242 de MacServicios2.
        Assert.Equal(1160, ReporteCobranzaPorEmpresa.Tauro);
        Assert.Equal(1161, ReporteCobranzaPorEmpresa.Zaragoza);
    }
}
