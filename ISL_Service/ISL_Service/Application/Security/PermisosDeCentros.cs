namespace ISL_Service.Application.Security;

/// <summary>
/// Las claves de permiso de los centros de servicio, en un solo lugar.
/// </summary>
/// <remarks>
/// Viven aqui y no dentro de PermissionService porque las usan los dos lados:
/// quien las DA DE ALTA en el catalogo (Infrastructure) y quien las PREGUNTA al
/// contestar (el controlador). Escritas dos veces, el dia que cambie una
/// dejarian de casar y nadie veria un error — simplemente el selector se
/// quedaria vacio.
///
/// HAY DOS FAMILIAS Y LAS DOS VALEN. NO ES UN DESCUIDO.
/// ----------------------------------------------------
/// La familia VIEJA nacio dentro de reportes, porque los centros solo servian
/// para sacar reportes de otra base:
///
///     reportes.centros.cambiar
///     reportes.centros.&lt;numero&gt;.ver
///
/// La familia NUEVA sale de reportes, porque el centro ya no es un asunto de
/// reportes: es el sitio donde se trabaja, y hay cosas que se ESCRIBEN alla.
///
///     centros.cambiar
///     centros.&lt;numero&gt;.ver      consultar ese centro
///     centros.&lt;numero&gt;.operar   escribir en ese centro
///     centros.matriz.ver        consultar la matriz
///     centros.matriz.operar     escribir en la matriz
///
/// LA VIEJA NO SE BORRA Y SIGUE VALIENDO, y es la regla mas importante de este
/// archivo: quien hoy tiene acceso autorizado no lo pierde el dia del
/// despliegue y nadie tiene que volver a configurar nada. La equivalencia la
/// aplica <see cref="CompatibilidadDeCentros"/> sobre el juego de permisos, no
/// cada quien que pregunta: un sitio que se olvide de preguntar por las dos
/// claves seria una puerta cerrada sin aviso.
///
/// POR NUMERO Y NO POR IDCentro
/// ----------------------------
/// Se usa Numero porque es lo que la gente reconoce (2004 = ORIENTE) y no
/// cambia entre bases. IDCentro es un autonumerico de cada base: la misma
/// clave significaria otro centro en la otra empresa.
/// </remarks>
public static class PermisosDeCentros
{
    /* ------------------------------------------------------------------ */
    /* LA FAMILIA NUEVA — la canonica, la que se pregunta                 */
    /* ------------------------------------------------------------------ */

    /// <summary>Todas las claves nuevas de centros empiezan asi.</summary>
    public const string Prefijo = "centros.";

    /// <summary>
    /// Puede cambiar de centro. Sin esto no ve el selector, y todo sale de la
    /// matriz aunque tenga permiso de ver centros.
    /// </summary>
    public const string Cambiar = "centros.cambiar";

    /// <summary>El segmento que identifica a la matriz dentro de la clave.</summary>
    public const string SegmentoMatriz = "matriz";

    /// <summary>Puede CONSULTAR la matriz.</summary>
    public const string MatrizVer = "centros.matriz.ver";

    /// <summary>Puede ESCRIBIR en la matriz.</summary>
    public const string MatrizOperar = "centros.matriz.operar";

    /// <summary>
    /// Puede CONSULTAR un centro, por su Numero: 2004 -> "centros.2004.ver".
    /// </summary>
    public static string Ver(int numero) => $"{Prefijo}{numero}.ver";

    /// <summary>
    /// Puede ESCRIBIR en un centro, por su Numero: 2004 -> "centros.2004.operar".
    /// </summary>
    /// <remarks>
    /// `operar` NO SE DEDUCE DE `ver`, y es una decision de negocio que no se
    /// ablanda. Consultar un centro es barato de revisar; escribir en el no.
    /// Se concede a mano desde la pantalla de Accesos y permisos del web, centro
    /// por centro. Quien solo tenga `ver` entra, consulta, y las acciones de
    /// escribir salen apagadas con su motivo.
    /// </remarks>
    public static string Operar(int numero) => $"{Prefijo}{numero}.operar";

    /// <summary>
    /// La clave de consultar el centro, o la de la matriz si
    /// <paramref name="numero"/> no es un centro (0 o menos; ver el contrato:
    /// X-Centro 0 = matriz).
    /// </summary>
    public static string VerDe(int numero) => numero > 0 ? Ver(numero) : MatrizVer;

    /// <summary>La clave de escribir en el centro, o la de la matriz.</summary>
    public static string OperarDe(int numero) => numero > 0 ? Operar(numero) : MatrizOperar;

    /* ------------------------------------------------------------------ */
    /* LA FAMILIA VIEJA — sigue valiendo, no se borra                     */
    /* ------------------------------------------------------------------ */

    /// <summary>Todas las claves VIEJAS de centros empiezan asi.</summary>
    /// <remarks>
    /// Lo mira <c>PermissionService.EsReporteQueMandaLegacy</c> para EXIMIR a
    /// estas claves del reemplazo por lo que diga Mac31. Es la unica cosa de
    /// este archivo que, si se cambia sin cuidado, borra permisos en silencio:
    /// `reportes.centros.*` no existe en Mac31, asi que sin la exencion el
    /// recalculo del snapshot se las lleva todas y el selector se apaga para
    /// todo el mundo. Lo vigila <c>CompatibilidadDeCentrosTests</c>.
    /// </remarks>
    public const string PrefijoLegado = "reportes.centros.";

    /// <summary>La clave vieja de cambiar de centro. Vale como <see cref="Cambiar"/>.</summary>
    public const string CambiarLegado = "reportes.centros.cambiar";

    /// <summary>La clave vieja de un centro. Vale como <see cref="Ver(int)"/>.</summary>
    public static string VerLegado(int numero) => $"{PrefijoLegado}{numero}.ver";

    /* ------------------------------------------------------------------ */
    /* RECONOCER UNA CLAVE                                                */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Si esa clave es de centros, de cualquiera de las dos familias.
    /// </summary>
    public static bool EsDeCentros(string? clave)
    {
        var k = (clave ?? string.Empty).Trim();
        if (k.Length == 0) return false;
        return k.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase)
            || k.StartsWith(PrefijoLegado, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// El Numero del centro que menciona una clave de VER, de cualquiera de las
    /// dos familias, o <c>null</c> si esa clave no es de ver un centro concreto.
    /// </summary>
    /// <remarks>
    /// "centros.matriz.ver" contesta null a proposito: la matriz no tiene
    /// Numero, y confundirla con el centro 0 haria que la equivalencia inventara
    /// "centros.0.ver", una clave que nadie siembra y que nadie podria conceder.
    /// </remarks>
    public static int? NumeroDeClaveDeVer(string? clave)
    {
        var k = (clave ?? string.Empty).Trim();
        if (k.Length == 0) return null;

        string cuerpo;
        if (k.StartsWith(PrefijoLegado, StringComparison.OrdinalIgnoreCase))
            cuerpo = k[PrefijoLegado.Length..];
        else if (k.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
            cuerpo = k[Prefijo.Length..];
        else
            return null;

        const string sufijo = ".ver";
        if (!cuerpo.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase))
            return null;

        var numero = cuerpo[..^sufijo.Length];
        return int.TryParse(numero, out var n) && n > 0 ? n : null;
    }
}
