namespace ISL_Service.Application.Security;

/// <summary>
/// LA FAMILIA DE PERMISOS DE CENTROS DEL CONTRATO, Y SU COMPATIBILIDAD.
/// </summary>
/// <remarks>
/// POR QUE UNA FAMILIA NUEVA Y NO SEGUIR CON `reportes.centros.*`
/// --------------------------------------------------------------
/// Porque los centros dejan de ser una cosa de reportes: cualquier modulo que
/// se declare (ver <see cref="Centros.ModulosConCentros"/>) va a preguntar lo
/// mismo. Un permiso que se llama `reportes.*` y gobierna la captura de
/// pedidos es una mentira que se descubre sola el dia que alguien revisa la
/// pantalla de permisos.
///
/// LA COMPATIBILIDAD ES OBLIGATORIA Y NO ES UNA CORTESIA
/// ----------------------------------------------------
/// Las claves `reportes.centros.*` YA ESTAN REPARTIDAS en las dos bases. Si la
/// familia nueva no las reconociera, el dia del despliegue toda la gente que
/// hoy entra a un centro dejaria de entrar, y el sintoma seria "se me quito el
/// selector" sin que nadie hubiera tocado sus permisos. Por eso
/// `reportes.centros.cambiar` vale como `centros.cambiar` y
/// `reportes.centros.&lt;n&gt;.ver` vale como `centros.&lt;n&gt;.ver`.
///
/// `operar` NO TIENE EQUIVALENTE VIEJO, Y ESO ES EL PUNTO. Hoy no existe
/// ninguna clave que signifique "puede ESCRIBIR en ese centro", asi que no hay
/// nada que heredar: escribir fuera de la matriz se concede a mano. Deducir
/// `operar` de `ver` convertiria a doce mil consultas en doce mil permisos de
/// escritura que nadie concedio.
///
/// Las claves NO se siembran aqui: el catalogo lo arma
/// Infrastructure/Security/PermissionService.cs, que es de otro dueno. Este
/// archivo solo las NOMBRA y responde preguntas sobre un juego de permisos ya
/// leido.
/// </remarks>
public static class ClavesDeCentros
{
    /// <summary>Todas las claves nuevas empiezan asi.</summary>
    public const string Prefijo = "centros.";

    /// <summary>Puede cambiar de centro. Sin esto, todo sale de la matriz.</summary>
    public const string Cambiar = "centros.cambiar";

    /// <summary>Consultar la matriz.</summary>
    public const string MatrizVer = "centros.matriz.ver";

    /// <summary>Escribir en la matriz.</summary>
    public const string MatrizOperar = "centros.matriz.operar";

    /// <summary>Consultar ese centro, por su Numero: 2004 -> "centros.2004.ver".</summary>
    /// <remarks>
    /// Numero y no IDCentro, igual que en la familia vieja: Numero es lo que la
    /// gente reconoce (2004 = ORIENTE) y es lo mismo en las dos bases, mientras
    /// IDCentro es un consecutivo que puede coincidir entre empresas distintas
    /// apuntando a centros distintos — exactamente lo que pasa hoy entre MacZ y
    /// Produccion_svr.
    /// </remarks>
    public static string Ver(int numero) => $"{Prefijo}{numero}.ver";

    /// <summary>Escribir en ese centro.</summary>
    public static string Operar(int numero) => $"{Prefijo}{numero}.operar";

    /// <summary>La clave vieja que vale como <see cref="Cambiar"/>.</summary>
    public const string CambiarCompatible = PermisosDeCentros.Cambiar;

    /// <summary>La clave vieja que vale como <see cref="Ver(int)"/>.</summary>
    public static string VerCompatible(int numero) => PermisosDeCentros.Ver(numero);
}
