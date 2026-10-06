using ISL_Service.Application.DTOs.Productos;

namespace ISL_Service.Application.Interfaces;

/// <summary>Lectura del catalogo de productos con precios y existencias.</summary>
public interface IProductosRepository
{
    Task<List<ProductoDto>> ConsultarAsync(ProductosFiltroSp filtro, CancellationToken ct);
}

/// <summary>Normaliza y valida los filtros del catalogo de productos.</summary>
public interface IProductosService
{
    Task<List<ProductoDto>> ConsultarAsync(ProductosFiltro filtro, CancellationToken ct);
}

/// <summary>Lo que pide el endpoint, con las listas como listas.</summary>
public class ProductosFiltro
{
    public int IDProducto { get; set; }
    public IEnumerable<int>? IDsMarca { get; set; }
    public IEnumerable<int>? IDsCategoria { get; set; }
    public IEnumerable<int>? IDsGrupoCategoria { get; set; }
    public int IDStatus { get; set; }
    public string? Clave { get; set; }
    public string? Descripcion { get; set; }
    public bool Exacto { get; set; }
    /// <summary>true = lista de precios Roshfrans con descuentos en cascada.</summary>
    public bool ListaRoshfrans { get; set; }
    /// <summary>true = solo productos de carga.</summary>
    public bool EsCarga { get; set; }
    /// <summary>Los porcentajes de descuento de la lista Roshfrans, en orden de aplicacion.</summary>
    public IEnumerable<decimal>? PorcentajesDescuento { get; set; }
}

/// <summary>Lo que recibe el SP, con las listas ya convertidas a cadena de `~`.</summary>
public class ProductosFiltroSp
{
    public int IDProducto { get; set; }
    public string IDsMarca { get; set; } = string.Empty;
    public string IDsCategoria { get; set; } = string.Empty;
    public string IDsGrupoCategoria { get; set; } = string.Empty;
    public int IDStatus { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Exacto { get; set; }
    public bool ListaRoshfrans { get; set; }
    public bool EsCarga { get; set; }
    public string PorcentajesDescuento { get; set; } = string.Empty;
}
