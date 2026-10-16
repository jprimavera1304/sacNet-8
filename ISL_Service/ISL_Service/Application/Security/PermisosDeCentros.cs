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
/// </remarks>
public static class PermisosDeCentros
{
    /// <summary>Todas las claves de centros empiezan asi.</summary>
    public const string Prefijo = "reportes.centros.";

    /// <summary>
    /// Puede usar el selector de centros. Sin esto no lo ve, y todo sale de la
    /// base principal aunque tenga permiso de ver centros.
    /// </summary>
    public const string Cambiar = "reportes.centros.cambiar";

    /// <summary>
    /// El permiso de UN centro, por su Numero: 2004 -> "reportes.centros.2004.ver".
    /// </summary>
    /// <remarks>
    /// Se usa Numero y no IDCentro porque Numero es lo que la gente reconoce
    /// (2004 = ORIENTE) y no cambia entre bases.
    /// </remarks>
    public static string Ver(int numero) => $"{Prefijo}{numero}.ver";

    /* ------------------------------------------------------------------ */
    /* LA FAMILIA NUEVA ES OTRA, Y SE LLAMA ClavesDeCentros               */
    /* ------------------------------------------------------------------ */

    /*
      ESTE ARCHIVO NOMBRA SOLO LA FAMILIA VIEJA. A PROPOSITO.

      La familia nueva del contrato -`centros.cambiar`, `centros.<n>.ver`,
      `centros.<n>.operar` y las dos de la matriz- la nombra ClavesDeCentros, y
      la compatibilidad entre las dos la resuelve CapacidadesDeCentros al
      CONSUMIRLAS. Aqui solo vive la vieja.

      UNA CLASE POR FAMILIA, Y NO ES UN GUSTO: se intento poner las dos aqui
      -`Cambiar` pasaba a ser la nueva y `CambiarLegado` la vieja- y salio el
      peor fallo posible. `ClavesDeCentros.CambiarCompatible` VALE
      `PermisosDeCentros.Cambiar`, asi que al cambiarle el significado a esta
      constante la compatibilidad se quedo apuntando a la clave NUEVA: el codigo
      seguia compilando, las pruebas de la capa de centros seguian verdes, y la
      clave vieja -la unica que la gente tiene concedida hoy- dejaba de valer.
      O sea, el selector apagado para las 13 concesiones de MacZ y las 11 de
      Produccion_svr, sin un solo error en el log.

      Con una clase por familia eso ya no se puede escribir: la constante dice
      en su nombre de que familia es. Lo vigila
      CompatibilidadDeCentrosTests.La_Clave_Compatible_Es_La_Vieja.
    */

    /// <summary>
    /// Si esa clave es de centros, de CUALQUIERA de las dos familias.
    /// </summary>
    /// <remarks>
    /// Lo pregunta <see cref="ClavesQueProtegenAlgo"/>: estas claves se componen
    /// en tiempo de ejecucion con el Numero que esta en la base, asi que la
    /// reflexion no las ve y sin esto la pantalla de permisos las esconderia
    /// —incluido el permiso de ESCRIBIR en un centro, que es el que mas importa
    /// poder revisar—.
    ///
    /// Se compara con el PUNTO incluido para no llevarse por delante un
    /// hipotetico `centroscostos.*`.
    /// </remarks>
    public static bool EsDeCentros(string? clave)
    {
        var k = (clave ?? string.Empty).Trim();
        if (k.Length == 0) return false;
        return k.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase)
            || k.StartsWith(ClavesDeCentros.Prefijo, StringComparison.OrdinalIgnoreCase);
    }
}
