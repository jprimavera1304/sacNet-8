using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Data;

/// <summary>
/// Apunta una cadena de conexion a la base de otro centro de servicio.
/// </summary>
/// <remarks>
/// EL SISTEMA ES EL MISMO EN LA MATRIZ Y EN CADA CENTRO; lo unico que cambia es
/// de que base salen los datos. En Mac31 eso se resolvia instalando una copia
/// por centro con su propia cadena. Aqui es una sola instalacion, asi que la
/// cadena se reescribe por peticion: mismo servidor, mismo usuario, misma
/// seguridad — SOLO cambia el catalogo.
///
/// POR QUE SqlConnectionStringBuilder Y NO UN REEMPLAZO DE TEXTO. Buscar
/// "Database=" y sustituir parece mas simple y es una puerta abierta: la misma
/// cadena puede decir `Database`, `Initial Catalog` o ninguno de los dos, y un
/// nombre con un `;` dentro se convertiria en opciones de conexion nuevas. El
/// builder entiende la cadena en vez de adivinarla.
///
/// NADA DE ESTO ACEPTA TEXTO DEL CLIENTE. El front manda un IDCentro; el nombre
/// de la base sale de dbo.CentrosServicio, en el servidor. Aun asi se valida el
/// nombre aqui, porque una lista blanca con una comprobacion de mas no estorba
/// y sin ella cualquier error de captura en esa tabla llegaria a la conexion.
/// </remarks>
public static class BaseDelCentro
{
    /// <summary>
    /// La misma cadena, apuntada a <paramref name="baseDeDatos"/>.
    /// Si el nombre viene vacio devuelve la cadena tal cual (la principal).
    /// </summary>
    public static string Apuntar(string cadenaOriginal, string? baseDeDatos)
    {
        if (string.IsNullOrWhiteSpace(cadenaOriginal))
            throw new ArgumentException("La cadena de conexion viene vacia.", nameof(cadenaOriginal));

        var destino = (baseDeDatos ?? "").Trim();
        if (destino.Length == 0) return cadenaOriginal;

        if (!EsNombreDeBaseValido(destino))
            throw new ArgumentException($"Nombre de base no valido: '{destino}'.", nameof(baseDeDatos));

        var builder = new SqlConnectionStringBuilder(cadenaOriginal) { InitialCatalog = destino };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Letras, numeros y guion bajo. Nada de espacios, puntos, corchetes ni
    /// punto y coma: un nombre de base de verdad no los necesita, y cualquiera
    /// de ellos aqui significa que alguien capturo mal (o algo peor).
    /// </summary>
    public static bool EsNombreDeBaseValido(string? nombre)
    {
        var texto = (nombre ?? "").Trim();
        if (texto.Length == 0 || texto.Length > 128) return false;
        foreach (var c in texto)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }
}
