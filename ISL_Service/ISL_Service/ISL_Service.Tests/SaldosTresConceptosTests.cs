using System.Runtime.CompilerServices;

namespace ISL_Service.Tests;

/// <summary>
/// EN ZARAGOZA LOS DIAS VENCIDOS VIENEN EN TRES, Y EL TOTAL NO ES UNA CADENA.
///
/// `sp_n_rptVentasSaldos` devuelve DOS cosas parecidas y de tipo distinto:
/// `DiasVencimiento` es `int` y `DiasVencimientoMostrar` es `varchar(1000)`,
/// porque con la capacidad `creditoPorTresConceptos` junta los dias de
/// acumuladores, aceites y cascos en "91/91/0".
///
/// El repositorio leia el entero DE la columna de texto. En Tauro pasaba
/// -un solo concepto, la cadena es un numero suelto- y en Zaragoza era un 500
/// para cualquier cliente que debiera algo:
///
///     The input string '91/91/0' was not in a correct format.
///
/// Esta prueba mira el fuente porque el defecto no esta en la logica, esta en
/// QUE COLUMNA se le pide al lector: con un doble de base de datos que
/// devolviera enteros, la prueba pasaria y el bug seguiria ahi.
/// </summary>
public class SaldosTresConceptosTests
{
    private static string Fuente()
        => LeerFuente("Infrastructure/Repositories/VentasSaldosRepository.cs");

    [Fact]
    public void El_Entero_De_Dias_Vencidos_No_Se_Lee_De_La_Columna_De_Texto()
    {
        Assert.DoesNotContain("Entero(reader, \"DiasVencimientoMostrar\")", Fuente());
    }

    [Fact]
    public void El_Entero_Sale_De_La_Columna_Que_Es_Entera()
    {
        Assert.Contains("Entero(reader, \"DiasVencimiento\")", Fuente());
    }

    [Fact]
    public void El_Desglose_Por_Concepto_No_Se_Pierde_Y_Se_Lee_Como_Texto()
    {
        Assert.Contains("Texto(reader, \"DiasVencimientoMostrar\")", Fuente());
    }

    private static string LeerFuente(string relativa, [CallerFilePath] string estaPrueba = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(estaPrueba)!);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ISL_Service.csproj")))
                return File.ReadAllText(Path.Combine(dir.FullName, relativa.Replace('/', Path.DirectorySeparatorChar)));
            dir = dir.Parent;
        }

        throw new FileNotFoundException("No se encontro la raiz del proyecto ISL_Service para leer " + relativa);
    }
}
