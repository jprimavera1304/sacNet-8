namespace ISL_Service.Application.DTOs.Centros;

/// <summary>
/// La respuesta de GET /api/centros, con la forma exacta del contrato.
/// </summary>
/// <remarks>
/// SALE SOLO LO QUE EL USUARIO PUEDE VER, y el filtro es del servidor. Mandar
/// la lista completa con una bandera `puedeVer=false` y dejar que el front la
/// esconda es ensenarle a cualquiera con la consola abierta el mapa de las
/// sucursales de la empresa.
///
/// EL NOMBRE DE LA BASE NO ESTA EN NINGUN CAMPO, a proposito. Ver
/// <see cref="Application.Centros.CentroPedido"/>.
/// </remarks>
public class CentrosResponse
{
    /// <summary>Si es false el selector no se pinta y todo sale de la matriz.</summary>
    public bool PuedeCambiar { get; set; }

    public MatrizItem Matriz { get; set; } = new();

    public List<CentroItem> Centros { get; set; } = new();
}

/// <summary>La base principal de la empresa.</summary>
public class MatrizItem
{
    /// <summary>
    /// El nombre COMERCIAL de la empresa. Nunca la palabra "Principal".
    /// De donde sale, ver <see cref="Application.Interfaces.IIdentidadDeEmpresaRepository"/>.
    /// </summary>
    public string Nombre { get; set; } = "";

    public bool PuedeOperar { get; set; }
}

public class CentroItem
{
    public int IdCentro { get; set; }
    public int Numero { get; set; }
    public string Nombre { get; set; } = "";

    /// <summary>Puede consultarlo.</summary>
    public bool PuedeVer { get; set; }

    /// <summary>Puede escribir en el. No se deduce de <see cref="PuedeVer"/>.</summary>
    public bool PuedeOperar { get; set; }
}
