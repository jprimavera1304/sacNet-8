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
    private static readonly Dictionary<string, string> Reemplazos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sp_n_rptAutos"] = "sp_w_rptAutos"
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
