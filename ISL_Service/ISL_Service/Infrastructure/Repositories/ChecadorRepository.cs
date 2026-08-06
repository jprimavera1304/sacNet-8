using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Checador;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Checadas de comida. Toda la logica (deducir inicio/fin, calcular minutos,
/// validar secuencia) vive en los sp_w_*; aqui solo se arman parametros y se lee
/// el resultado.
/// </summary>
public class ChecadorRepository : IChecadorRepository
{
    private const string SpRegistrarComida = "dbo.sp_w_RegistrarChecadaComida";
    private const string SpConsultarComida = "dbo.sp_w_ConsultarChecadasComida";
    private const string SpCancelarComida = "dbo.sp_w_CancelarChecadaComida";

    private readonly IConfiguration _configuration;

    public ChecadorRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ChecadaComidaRegistradaDto> RegistrarComidaAsync(
        int idEmpleado,
        string tipo,
        int? idEmpleadoHuella,
        string origen,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpRegistrarComida, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Tipo", tipo);
        cmd.Parameters.AddWithValue("@IDEmpleadoHuella", (object?)idEmpleadoHuella ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Origen", origen);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        var row = table.Rows.Count > 0 ? table.Rows[0] : null;
        if (row == null)
            throw new InvalidOperationException($"{SpRegistrarComida} no devolvio la checada registrada.");

        return new ChecadaComidaRegistradaDto
        {
            IDEmpleadoChecada = ReadInt(row, "IDEmpleadoChecada", "IdEmpleadoChecada"),
            IDEmpleado = ReadInt(row, "IDEmpleado", "IdEmpleado"),
            Tipo = ReadString(row, "Tipo"),
            FechaHora = ReadDate(row, "FechaHora") ?? DateTime.Now,
            MinutosComida = ReadNullableInt(row, "MinutosComida")
        };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarComidasAsync(
        int idEmpleado,
        DateTime fechaInicial,
        DateTime fechaFinal,
        bool incluirCanceladas,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpConsultarComida, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        // El SP recibe date, no datetime: si se manda la hora, un rango del mismo
        // dia se convertiria en "desde las 14:30 hasta las 09:00" y no traeria nada.
        cmd.Parameters.Add("@FechaInicial", SqlDbType.Date).Value = fechaInicial.Date;
        cmd.Parameters.Add("@FechaFinal", SqlDbType.Date).Value = fechaFinal.Date;
        cmd.Parameters.AddWithValue("@IncluirCanceladas", incluirCanceladas);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task CancelarComidaAsync(
        int idEmpleadoChecada,
        string motivo,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpCancelarComida, conn);
        cmd.Parameters.AddWithValue("@IDEmpleadoChecada", idEmpleadoChecada);
        cmd.Parameters.AddWithValue("@Motivo", motivo);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        return new Mac3SqlServerConnector(cs).GetConnection;
    }

    private static SqlCommand CreateStoredProcedureCommand(string sp, SqlConnection conn)
    {
        return new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
    }

    private static async Task<DataTable> ExecuteFirstTableAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds), ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    private static List<Dictionary<string, object?>> DataTableToRows(DataTable table)
    {
        return table.AsEnumerable().Select(row =>
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn column in table.Columns)
                item[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
            return item;
        }).ToList();
    }

    private static int ReadInt(DataRow row, params string[] names)
        => ReadNullableInt(row, names) ?? 0;

    private static int? ReadNullableInt(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) return parsed;
        }
        return null;
    }

    private static string ReadString(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return string.Empty;
    }

    private static DateTime? ReadDate(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            var value = row[name];
            if (value == DBNull.Value || value == null) continue;
            if (value is DateTime dt) return dt;
            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) return parsed;
        }
        return null;
    }
}
