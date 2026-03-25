namespace ISL_Service.Application.DTOs.DashboardVentas;

public class DashboardVentasFiltrosResponse
{
    public List<DashboardEmpresaFiltroDto> Empresas { get; set; } = new();
    public List<DashboardAlmacenFiltroDto> Almacenes { get; set; } = new();
    public List<DashboardAgenteFiltroDto> Agentes { get; set; } = new();
    public List<DashboardClienteFiltroDto> Clientes { get; set; } = new();
    public List<DashboardProductoFiltroDto> Productos { get; set; } = new();
    public List<DashboardCategoriaFiltroDto> Categorias { get; set; } = new();
    public List<DashboardMarcaFiltroDto> Marcas { get; set; } = new();
    public List<DashboardTipoDocumentoFiltroDto> TiposDocumento { get; set; } = new();
}

public class DashboardEmpresaFiltroDto
{
    public int IDEmpresa { get; set; }
    public string? Empresa { get; set; }
}

public class DashboardAlmacenFiltroDto
{
    public int IDAlmacen { get; set; }
    public string? Almacen { get; set; }
}

public class DashboardAgenteFiltroDto
{
    public int IDAgente { get; set; }
    public string? Nombre { get; set; }
}

public class DashboardClienteFiltroDto
{
    public int IDCliente { get; set; }
    public string? Cliente { get; set; }
}

public class DashboardProductoFiltroDto
{
    public int IDProducto { get; set; }
    public string? Codigo { get; set; }
    public string? Producto { get; set; }
}

public class DashboardCategoriaFiltroDto
{
    public int IDCategoria { get; set; }
    public string? Categoria { get; set; }
}

public class DashboardMarcaFiltroDto
{
    public int IDMarca { get; set; }
    public string? Marca { get; set; }
}

public class DashboardTipoDocumentoFiltroDto
{
    public int IDTipoDocumento { get; set; }
    public string? Documento { get; set; }
    public string? NombreDocumento { get; set; }
}
