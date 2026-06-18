using System.Data;
using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class VentasConsultaRepository : IVentasConsultaRepository
{
    private const int CommandTimeoutSeconds = 500;

    private readonly IConfiguration _configuration;

    public VentasConsultaRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        return new VentasConsultaCatalogosResponse
        {
            Empresas = await ConsultarEmpresasAsync(conn, ct),
            Almacenes = await ConsultarAlmacenesAsync(conn, ct),
            Agentes = await ConsultarAgentesAsync(conn, ct),
            TiposDocumento = BuildTiposDocumento(),
            EstatusVenta = BuildEstatusVenta()
        };
    }

    public async Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        AddVentasCommonParams(cmd, request);
        return await ExecuteRowsAsync(cmd, ct);
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPedidos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        cmd.Parameters.AddWithValue("@IDPedido", 0);
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", request.IDUsuario);
        cmd.Parameters.AddWithValue("@IDAgente", request.IDAgente);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@IDStatusPedido", request.IDStatusPedido);
        cmd.Parameters.AddWithValue("@FolioInicial", request.FolioInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FolioFinal", request.FolioFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaInicial", request.FechaInicial ?? request.FechaEmisionInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaFinal", request.FechaFinal ?? request.FechaEmisionFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelInicial", request.FechaCancelInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelFinal", request.FechaCancelFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", request.IDUsuarioActual);

        return await ExecuteRowsAsync(cmd, ct);
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPendientesImprimir", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDPedido", request.IDVenta);

        return await ExecuteRowsAsync(cmd, ct);
    }

    public async Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("sp_n_ConsultaVentasPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDPagoVenta", 0);
        cmd.Parameters.AddWithValue("@DiferenciaUsados", request.DiferenciaUsados ? 1 : 0);
        cmd.Parameters.AddWithValue("@IDCobro", request.IDCobro);
        cmd.Parameters.AddWithValue("@DineroExcedente", request.DineroExcedente ? 1 : 0);

        return await ExecuteRowsAsync(cmd, ct);
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

    private static void AddVentasCommonParams(SqlCommand cmd, VentasConsultaRequest request)
    {
        cmd.Parameters.AddWithValue("@IDsVenta", JoinIds(request.IDsVentaLst, request.IDsVenta));
        cmd.Parameters.AddWithValue("@IDVenta", request.IDVenta);
        cmd.Parameters.AddWithValue("@IDEmpresa", request.IDEmpresa);
        cmd.Parameters.AddWithValue("@IDCliente", request.IDCliente);
        cmd.Parameters.AddWithValue("@IDUsuario", request.IDUsuario);
        cmd.Parameters.AddWithValue("@IDAgente", request.IDAgente);
        cmd.Parameters.AddWithValue("@IDTipoDocumento", request.IDTipoDocumento);
        cmd.Parameters.AddWithValue("@TipoDocumento", request.TipoDocumento);
        cmd.Parameters.AddWithValue("@FolioInicial", request.FolioInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FolioFinal", request.FolioFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaEmisionInicial", request.FechaEmisionInicial ?? request.FechaInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaEmisionFinal", request.FechaEmisionFinal ?? request.FechaFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelInicial", request.FechaCancelInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaCancelFinal", request.FechaCancelFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaPagoInicial", request.FechaPagoInicial ?? string.Empty);
        cmd.Parameters.AddWithValue("@FechaPagoFinal", request.FechaPagoFinal ?? string.Empty);
        cmd.Parameters.AddWithValue("@Formato", request.Formato);
        cmd.Parameters.AddWithValue("@IDUsuarioActual", request.IDUsuarioActual);
        cmd.Parameters.AddWithValue("@IDsClientes", JoinIds(request.IDsClientesLst, request.IDsClientes));
        cmd.Parameters.AddWithValue("@IDsProducto", JoinIds(request.IDsProductoLst, request.IDsProducto));
        cmd.Parameters.AddWithValue("@IDProducto", request.IDProducto);
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarEmpresasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaEmpresas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@RazonSocial", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDEmpresa"),
                Nombre = ReadString(row, "NombreCorto", "RazonSocial", "Empresa"),
                Clave = ReadString(row, "Clave", "NombreCorto")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarAlmacenesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAlmacenes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDAlmacen", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Almacen", "");
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDAlmacen"),
                Nombre = ReadString(row, "Almacen"),
                Clave = ReadString(row, "Almacen")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<VentasConsultaCatalogoItem>> ConsultarAgentesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAgentes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@NumeroAgente", 0);
        cmd.Parameters.AddWithValue("@NombreAgente", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new VentasConsultaCatalogoItem
            {
                Id = ReadInt(row, "IDAgente"),
                Nombre = ReadString(row, "NombreAgente"),
                Clave = ReadString(row, "NumeroAgente")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static List<VentasConsultaCatalogoItem> BuildTiposDocumento()
    {
        return new List<VentasConsultaCatalogoItem>
        {
            new() { Id = 0, Nombre = "TODOS", Clave = "todos" },
            new() { Id = 10, Nombre = "REMISIONES", Clave = "remisiones" },
            new() { Id = 11, Nombre = "PEDIDOS", Clave = "pedidos" }
        };
    }

    private static List<VentasConsultaCatalogoItem> BuildEstatusVenta()
    {
        return new List<VentasConsultaCatalogoItem>
        {
            new() { Id = 0, Nombre = "REMISIONES", Clave = "remisiones" },
            new() { Id = 1, Nombre = "PEDIDOS", Clave = "pedidos" },
            new() { Id = 2, Nombre = "PENDIENTES DE IMPRIMIR", Clave = "pendientes-imprimir" },
            new() { Id = 3, Nombre = "PENDIENTES AUTORIZAR", Clave = "pendientes-autorizar" },
            new() { Id = 4, Nombre = "RECHAZADOS", Clave = "rechazados" }
        };
    }

    private static async Task<VentasConsultaRowsResponse> ExecuteRowsAsync(SqlCommand cmd, CancellationToken ct)
    {
        var table = await ExecuteFirstTableAsync(cmd, ct);
        var rows = DataTableToRows(table);
        return new VentasConsultaRowsResponse { Rows = rows, Total = rows.Count };
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var table = new DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(table), ct);
        return table;
    }

    private static List<Dictionary<string, object?>> DataTableToRows(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in table.Columns)
                item[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            rows.Add(item);
        }
        return rows;
    }

    private static int ReadInt(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (int.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        }
        return 0;
    }

    private static string ReadString(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            var text = Convert.ToString(value)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return string.Empty;
    }

    private static string JoinIds(IEnumerable<int>? values, string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();
        if (values == null) return string.Empty;
        return string.Join(",", values.Where(x => x > 0).Distinct());
    }
}
