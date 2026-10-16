namespace ISL_Service.Application.Security;

/// <summary>
/// LA PREGUNTA "PUEDE ESTE USUARIO ENTRAR A ESTE CENTRO", EN UN SOLO SITIO.
/// </summary>
/// <remarks>
/// POR QUE NO SE PREGUNTA A MANO EN CADA SITIO
/// -------------------------------------------
/// Porque hay DOS familias de claves (ver <see cref="PermisosDeCentros"/>) y un
/// sitio que se olvide de la vieja cierra una puerta que hoy esta abierta, sin
/// aviso y sin error: contesta 403 y parece que el usuario no tiene permiso. La
/// equivalencia ya la aplica <see cref="CompatibilidadDeCentros"/> sobre el
/// juego de permisos, pero este guardia pregunta ADEMAS por la clave vieja, para
/// que un juego de permisos armado por otro camino —una prueba, un fallback, un
/// snapshot viejo que todavia este en cache— siga abriendo igual. Cinturon y
/// tirantes, y el que sobra no hace daño: las dos claves significan lo mismo.
///
/// LAS TRES PREGUNTAS SON DISTINTAS Y SE PUEDEN DAR POR SEPARADO
/// ------------------------------------------------------------
///   cambiar  -> puede usar el selector. Sin esto no lo ve y todo sale de la
///               matriz, como hasta hoy. Es el interruptor de arriba: se puede
///               apagar sin tocarle los doce permisos de abajo.
///   ver      -> puede CONSULTAR ese centro.
///   operar   -> puede ESCRIBIR en ese centro. NO SE DEDUCE DE `ver`, nunca.
///
/// `operar` sin `ver` no existe como caso util —quien escribe en un centro lo
/// consulta—, pero aqui no se regala: si alguien reparte solo `operar`, lo que
/// contesta este guardia es lo que se repartio. Inventar la herencia al reves
/// seria la misma clase de regalo que la regla prohibe.
/// </remarks>
public static class GuardiaDeCentros
{
    /// <summary>El numero que significa "la matriz" en la cabecera X-Centro.</summary>
    public const int Matriz = 0;

    /// <summary>Puede cambiar de centro.</summary>
    public static bool PuedeCambiar(Func<string, bool> tiene)
    {
        ArgumentNullException.ThrowIfNull(tiene);
        return tiene(PermisosDeCentros.Cambiar)
            || tiene(PermisosDeCentros.CambiarLegado);
    }

    /// <summary>
    /// Puede CONSULTAR ese centro. <paramref name="numero"/> 0 o menos = la matriz.
    /// </summary>
    public static bool PuedeVer(int numero, Func<string, bool> tiene)
    {
        ArgumentNullException.ThrowIfNull(tiene);

        if (numero <= Matriz)
            return tiene(PermisosDeCentros.MatrizVer);

        return tiene(PermisosDeCentros.Ver(numero))
            || tiene(PermisosDeCentros.VerLegado(numero));
    }

    /// <summary>
    /// Puede ESCRIBIR en ese centro. <paramref name="numero"/> 0 o menos = la matriz.
    /// </summary>
    /// <remarks>
    /// NO MIRA `ver` NI LA FAMILIA VIEJA. La familia vieja no tiene clave de
    /// escribir —alla los centros solo servian para sacar reportes—, asi que no
    /// hay nada de donde heredar: aceptar `reportes.centros.&lt;n&gt;.ver` aqui
    /// seria exactamente deducir `operar` de `ver`.
    /// </remarks>
    public static bool PuedeOperar(int numero, Func<string, bool> tiene)
    {
        ArgumentNullException.ThrowIfNull(tiene);

        return numero <= Matriz
            ? tiene(PermisosDeCentros.MatrizOperar)
            : tiene(PermisosDeCentros.Operar(numero));
    }

    /// <summary>
    /// El motivo legible de que no se pueda consultar, para el 409 del contrato
    /// y para la tarjeta que pinta el telefono.
    /// </summary>
    public static string MotivoDeNoVer(string nombreDelCentro) =>
        $"Tu usuario no tiene permiso para consultar {Legible(nombreDelCentro)}.";

    /// <summary>
    /// El motivo legible de que se pueda consultar pero no escribir. Lo pinta el
    /// front junto al boton apagado; decir solo "no tienes permiso" manda a la
    /// persona a pedir un acceso que ya tiene.
    /// </summary>
    public static string MotivoDeNoOperar(string nombreDelCentro) =>
        $"Puedes consultar {Legible(nombreDelCentro)}, pero no guardar cambios ahi. "
        + "Ese permiso se da aparte en Accesos y permisos.";

    private static string Legible(string? nombre)
    {
        var n = (nombre ?? string.Empty).Trim();
        return n.Length > 0 ? n : "ese centro";
    }
}
