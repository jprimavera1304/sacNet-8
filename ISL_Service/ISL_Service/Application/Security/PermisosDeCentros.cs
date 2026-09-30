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
}
