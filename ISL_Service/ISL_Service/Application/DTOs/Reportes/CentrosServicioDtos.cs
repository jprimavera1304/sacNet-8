namespace ISL_Service.Application.DTOs.Reportes;

/// <summary>
/// Un centro de servicio: su nombre y la base de la que salen SUS datos.
/// </summary>
/// <remarks>
/// El sistema es el mismo en la matriz y en cada centro; lo unico que cambia es
/// la base a la que se pregunta. En Mac31 eso se resolvia instalando una copia
/// por centro, apuntada a la suya. Aqui no hay doce instalaciones: hay un
/// selector, y este es el catalogo que lo llena.
///
/// <see cref="Base"/> NO viaja al front nunca. El navegador manda un IDCentro y
/// el servidor resuelve el nombre de la base; al reves seria dejar que el
/// cliente diga a que base quiere entrar.
/// </remarks>
public class CentroServicioItem
{
    public int IdCentro { get; set; }
    public int Numero { get; set; }
    public string Nombre { get; set; } = "";

    /// <summary>Nombre real de la base. Solo para uso del servidor.</summary>
    public string Base { get; set; } = "";
}

/// <summary>Lo que el front necesita para pintar el selector.</summary>
public class CentrosServicioResponse
{
    /// <summary>
    /// Si es false el selector NO se pinta y todo sale de la base principal,
    /// por mas centros que traiga la lista.
    /// </summary>
    public bool PuedeCambiar { get; set; }

    /// <summary>Como se llama la base principal en pantalla ("Zaragoza").</summary>
    public string NombrePrincipal { get; set; } = "";

    /// <summary>Solo los centros que ESTE usuario puede ver, sin el nombre de la base.</summary>
    public List<CentroServicioOpcion> Centros { get; set; } = new();
}

public class CentroServicioOpcion
{
    public int IdCentro { get; set; }
    public int Numero { get; set; }
    public string Nombre { get; set; } = "";
}
