using System.Data;
using ISL_Service.Application.DTOs.DashboardVentas;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class DashboardVentasRepository : IDashboardVentasRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarFiltros = "dbo.sp_w_ConsultarDashboardVentasFiltros";
    private const string SpConsultarKpis = "dbo.sp_w_ConsultarDashboardVentasKpis";
    private const string SpConsultarSerieMensual = "dbo.sp_w_ConsultarDashboardVentasSerieMensual";
    private const string SpConsultarTopProductos = "dbo.sp_w_ConsultarDashboardTopProductos";
    private const string SpConsultarTopClientes = "dbo.sp_w_ConsultarDashboardTopClientes";
    private const string SpConsultarTopCategorias = "dbo.sp_w_ConsultarDashboardTopCategorias";
    private const string SpConsultarTopMarcas = "dbo.sp_w_ConsultarDashboardTopMarcas";
    private const string SpConsultarVentasAlmacenes = "dbo.sp_w_ConsultarDashboardVentasAlmacenes";
    private const string SpConsultarVentasAgentes = "dbo.sp_w_ConsultarDashboardVentasAgentes";
    private const string SpConsultarVentasDetalle = "dbo.sp_w_ConsultarDashboardVentasDetalle";

    public DashboardVentasRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    public async Task<DashboardVentasFiltrosResponse> ConsultarFiltrosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarFiltros, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddFilterParams(cmd, request);

        var ds = new DataSet();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(ds);
        }

        var response = new DashboardVentasFiltrosResponse
        {
            Empresas = ds.Tables.Count > 0 ? Funciones.DataTableToList<DashboardEmpresaFiltroDto>(ds.Tables[0]) : new List<DashboardEmpresaFiltroDto>(),
            Almacenes = ds.Tables.Count > 1 ? Funciones.DataTableToList<DashboardAlmacenFiltroDto>(ds.Tables[1]) : new List<DashboardAlmacenFiltroDto>(),
            Agentes = ds.Tables.Count > 2 ? Funciones.DataTableToList<DashboardAgenteFiltroDto>(ds.Tables[2]) : new List<DashboardAgenteFiltroDto>(),
            Clientes = ds.Tables.Count > 3 ? Funciones.DataTableToList<DashboardClienteFiltroDto>(ds.Tables[3]) : new List<DashboardClienteFiltroDto>(),
            Productos = ds.Tables.Count > 4 ? Funciones.DataTableToList<DashboardProductoFiltroDto>(ds.Tables[4]) : new List<DashboardProductoFiltroDto>(),
            Categorias = ds.Tables.Count > 5 ? Funciones.DataTableToList<DashboardCategoriaFiltroDto>(ds.Tables[5]) : new List<DashboardCategoriaFiltroDto>(),
            Marcas = ds.Tables.Count > 6 ? Funciones.DataTableToList<DashboardMarcaFiltroDto>(ds.Tables[6]) : new List<DashboardMarcaFiltroDto>(),
            TiposDocumento = ds.Tables.Count > 7 ? Funciones.DataTableToList<DashboardTipoDocumentoFiltroDto>(ds.Tables[7]) : new List<DashboardTipoDocumentoFiltroDto>()
        };

        return response;
    }

    public async Task<DashboardVentasKpisDto?> ConsultarKpisAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableAsync(SpConsultarKpis, request, ct);
        return Funciones.DataTableToList<DashboardVentasKpisDto>(dt).FirstOrDefault();
    }

    public async Task<List<DashboardVentasSerieMensualDto>> ConsultarSerieMensualAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableAsync(SpConsultarSerieMensual, request, ct);
        return Funciones.DataTableToList<DashboardVentasSerieMensualDto>(dt);
    }

    public async Task<List<DashboardTopProductoDto>> ConsultarTopProductosAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableWithTopAsync(SpConsultarTopProductos, request, ct);
        return Funciones.DataTableToList<DashboardTopProductoDto>(dt);
    }

    public async Task<List<DashboardTopClienteDto>> ConsultarTopClientesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableWithTopAsync(SpConsultarTopClientes, request, ct);
        return Funciones.DataTableToList<DashboardTopClienteDto>(dt);
    }

    public async Task<List<DashboardTopCategoriaDto>> ConsultarTopCategoriasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableWithTopAsync(SpConsultarTopCategorias, request, ct);
        return Funciones.DataTableToList<DashboardTopCategoriaDto>(dt);
    }

    public async Task<List<DashboardTopMarcaDto>> ConsultarTopMarcasAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableWithTopAsync(SpConsultarTopMarcas, request, ct);
        return Funciones.DataTableToList<DashboardTopMarcaDto>(dt);
    }

    public async Task<List<DashboardVentasAlmacenDto>> ConsultarVentasAlmacenesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableAsync(SpConsultarVentasAlmacenes, request, ct);
        return Funciones.DataTableToList<DashboardVentasAlmacenDto>(dt);
    }

    public async Task<List<DashboardVentasAgenteDto>> ConsultarVentasAgentesAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableAsync(SpConsultarVentasAgentes, request, ct);
        return Funciones.DataTableToList<DashboardVentasAgenteDto>(dt);
    }

    public async Task<List<DashboardVentasDetalleDto>> ConsultarVentasDetalleAsync(DashboardVentasFiltroRequest request, CancellationToken ct = default)
    {
        var dt = await ExecuteSingleTableAsync(SpConsultarVentasDetalle, request, ct);
        return Funciones.DataTableToList<DashboardVentasDetalleDto>(dt);
    }

    private async Task<DataTable> ExecuteSingleTableAsync(string storedProcedure, DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(storedProcedure, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddFilterParams(cmd, request);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return dt;
    }

    private async Task<DataTable> ExecuteSingleTableWithTopAsync(string storedProcedure, DashboardVentasFiltroRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(storedProcedure, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddFilterParams(cmd, request);
        cmd.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = request.Top ?? 10 });

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return dt;
    }

    private static void AddFilterParams(SqlCommand cmd, DashboardVentasFiltroRequest request)
    {
        cmd.Parameters.Add(new SqlParameter("@FechaInicial", SqlDbType.DateTime) { Value = request.FechaInicial });
        cmd.Parameters.Add(new SqlParameter("@FechaFinal", SqlDbType.DateTime) { Value = request.FechaFinal });
        cmd.Parameters.Add(new SqlParameter("@IDEmpresa", SqlDbType.Int) { Value = (object?)request.IDEmpresa ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDAlmacen", SqlDbType.Int) { Value = (object?)request.IDAlmacen ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDAgente", SqlDbType.Int) { Value = (object?)request.IDAgente ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDCliente", SqlDbType.Int) { Value = (object?)request.IDCliente ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDProducto", SqlDbType.Int) { Value = (object?)request.IDProducto ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDCategoria", SqlDbType.Int) { Value = (object?)request.IDCategoria ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDMarca", SqlDbType.Int) { Value = (object?)request.IDMarca ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IDTipoDocumento", SqlDbType.Int) { Value = (object?)request.IDTipoDocumento ?? DBNull.Value });
    }
}
