namespace ISL_Service.Application.Reportes;

/// <summary>
/// LOS PROCEDIMIENTOS DE REPORTE QUE ESTAN ROTOS EN LA BASE, Y SU GEMELO BUENO.
///
/// Hay reportes que no salen porque su `sp_n_` pide algo que no existe. No es
/// algo que rompiera el backend nuevo: llevan roto donde se miren, y el boton
/// de Mac31 tampoco funciona.
///
/// La salida NO es editar el `sp_n_` —eso es legacy y no se toca— ni cambiar el
/// `spV2` de la fila de `Reportes`, porque esa fila la lee tambien Mac31 y
/// cambiarla le cambia el comportamiento a un programa que no estamos migrando.
/// La salida es un gemelo `sp_w_` con el defecto corregido y esta tabla, que es
/// la unica que lo sabe.
///
/// Es una lista de reparaciones, no una capa de indireccion: cada renglon lleva
/// el error exacto y se BORRA el dia que el procedimiento original se arregle.
/// Si esta lista empieza a crecer sin que nadie borre renglones, el problema ya
/// no es esta tabla.
/// </summary>
public static class ProcedimientosRotosEnLaBase
{
    /// <summary>
    /// 1300, Reporte de Autos.
    ///
    /// `sp_n_rptAutos` pide `KmPorLitroKmMaximo` y en `[Catalogo Autos]` la
    /// columna se llama `KmPorLitroMaximo`, sin el "Km" de mas:
    ///
    ///     Invalid column name 'KmPorLitroKmMaximo'
    ///
    /// Comprobado en `MacZ` y en `Produccion_svr`: la misma errata en las dos,
    /// o sea que el boton IMPRIMIR de Mac31 tampoco saca este reporte hoy.
    ///
    /// El gemelo (`Querrys/32 Reporte Autos Columna Mala/`) es el mismo
    /// procedimiento leyendo la columna buena. El ALIAS de salida se deja igual
    /// —`AS KmPorLitroKmMaximo`— porque es el nombre con el que la plantilla
    /// espera el dato: lo que estaba mal era de donde se lee, no como se llama.
    /// </summary>
    /// <summary>
    /// 1360 y 1365, Ventas Cobros DINERO y Ventas Cobros CASCOS.
    ///
    /// Los dos tienen el mismo `spV2`,
    /// `sp_n_rptVentasCobrosDineroCascosGlobales`, que llama a
    /// `sp_n_rptVentasCobrosDinero`. Ese interno arma un SELECT dinamico contra
    /// `dbo.CobrosDineroUsado` y le pide VEINTISEIS columnas que la tabla no
    /// tiene:
    ///
    ///     Invalid column name 'CantidadUtilizadaG1'   (y 25 mas)
    ///
    /// Comprobado en `MacZ` y en `Produccion_svr`: faltan las mismas en las
    /// dos, o sea que el nodo "Cobros Dinero y Cascos" de Mac31 tampoco saca
    /// papel hoy, y Legacy contesta el mismo error con el mismo psp.
    ///
    /// Las 26 son de tres clases:
    ///   - 8 son errata de nombre: la columna es `CantidadUtilizadO`, con O.
    ///   - 16 son importes que la tabla no guarda (`ImporteUtilizadoGn`,
    ///     `ImporteSaldoGn`). Se derivan con la identidad que la tabla cumple
    ///     en los 15,322 renglones de MacZ sin una excepcion:
    ///     `ImporteGn = CantidadGn * PrecioUnitarioGn`.
    ///   - 2 son conteos (`NumCascosUtilizado`, `NumCascosSaldo`), derivados
    ///     con la otra identidad medida 15,322 de 15,322:
    ///     `NumCascos = CantidadG1+..+G7+CantidadMoto`.
    ///
    /// `TotalCascosUtilizado` y `TotalCascosSaldo` NO se recalculan: existen en
    /// la tabla y la suma derivada no les cuadra en 11,864 renglones, o sea que
    /// son un dato propio y rearmarlos seria cambiar importes.
    ///
    /// El gemelo son DOS procedimientos, porque el motor ejecuta lo que dice
    /// `spV2`: si se reemplazara solo el interno, el envolvente legacy seguiria
    /// llamando al roto. Por eso lo que se sustituye aqui es el ENVOLVENTE, y
    /// es el gemelo del envolvente el que llama al gemelo del interno.
    /// Script: `Querrys/33 Reporte Cobros Dinero Cascos Columnas Malas/`.
    ///
    /// En Tauro `dbo.CobrosDineroUsado` esta vacia: este reporte es de
    /// Zaragoza. El gemelo se instala en las dos para que exista donde se
    /// busca.
    /// </summary>
    private static readonly Dictionary<string, string> Reemplazos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sp_n_rptAutos"] = "sp_w_rptAutos",
        ["sp_n_rptVentasCobrosDineroCascosGlobales"] = "sp_w_rptVentasCobrosDineroCascosGlobales"
    };

    /// <summary>
    /// Devuelve el gemelo bueno si el procedimiento esta en la lista de roros, y
    /// el mismo nombre si no. Nunca devuelve vacio.
    /// </summary>
    public static string Bueno(string? procedimiento)
    {
        var nombre = (procedimiento ?? string.Empty).Trim();
        if (nombre.Length == 0) return nombre;

        return Reemplazos.TryGetValue(nombre, out var gemelo) ? gemelo : nombre;
    }

    /// <summary>Si este procedimiento tiene un gemelo que lo sustituye.</summary>
    public static bool EstaRoto(string? procedimiento)
        => Reemplazos.ContainsKey((procedimiento ?? string.Empty).Trim());
}
