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

    // Entrada/salida y huellas. Estos si terminan tocando datos de legacy, pero
    // siempre por dentro del sp_w_, que llama a los sp_n_ de siempre.
    private const string SpRegistrarMovimiento = "dbo.sp_w_RegistrarChecadaMovimiento";
    private const string SpConsultarMovimientos = "dbo.sp_w_ConsultarMovimientosDia";
    private const string SpConsultarEmpleados = "dbo.sp_w_ConsultarEmpleadosChecador";
    private const string SpConsultarHuellas = "dbo.sp_w_ConsultarHuellasEmpleado";
    private const string SpGuardarHuella = "dbo.sp_w_GuardarHuellaEmpleado";
    private const string SpBajaHuella = "dbo.sp_w_BajaHuellaEmpleado";
    private const string SpAsistenciaDia = "dbo.sp_w_ConsultarAsistenciaDia";
    private const string SpConfiguracion = "dbo.sp_w_ConsultarConfiguracionChecador";

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

    public async Task<ChecadaMovimientoRegistradoDto> RegistrarMovimientoAsync(
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

        await using var cmd = CreateStoredProcedureCommand(SpRegistrarMovimiento, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Tipo", tipo);
        cmd.Parameters.AddWithValue("@IDEmpleadoHuella", (object?)idEmpleadoHuella ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Origen", origen);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        var row = table.Rows.Count > 0 ? table.Rows[0] : null;
        if (row == null)
            throw new InvalidOperationException($"{SpRegistrarMovimiento} no devolvio la checada registrada.");

        return new ChecadaMovimientoRegistradoDto
        {
            IDEmpleadoChecada = ReadInt(row, "IDEmpleadoChecada"),
            IDEmpleado = ReadInt(row, "IDEmpleado"),
            Empleado = ReadString(row, "Empleado"),
            Tipo = ReadString(row, "Tipo"),
            Fecha = ReadDate(row, "FechaHora") ?? DateTime.Now,
            HoraFtm = ReadString(row, "HoraFtm"),
            PrimeraDelDia = ReadInt(row, "PrimeraDelDia") == 1
        };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarMovimientosDiaAsync(
        DateTime fecha,
        int idEmpleado,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpConsultarMovimientos, conn);
        cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha.Date;
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarAsistenciaDiaAsync(
        DateTime fecha,
        int idEmpleado,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpAsistenciaDia, conn);
        cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha.Date;
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarConfiguracionAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpConfiguracion, conn);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarEmpleadosAsync(
        string filtro,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpConsultarEmpleados, conn);
        cmd.Parameters.AddWithValue("@Filtro", filtro ?? string.Empty);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task<ChecadaComidaRowsResponse> ConsultarHuellasAsync(
        int idEmpleado,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpConsultarHuellas, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task<ChecadaComidaRowsResponse> GuardarHuellaAsync(
        int idEmpleado,
        int idMano,
        int idDedo,
        byte[] huella,
        byte[] huella2,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpGuardarHuella, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@IDMano", idMano);
        cmd.Parameters.AddWithValue("@IDDedo", idDedo);
        // -1 = MAX. Sin esto el proveedor toma el tamaño del primer valor y una
        // captura mas grande se trunca, que en una huella significa que ya no
        // reconoce a nadie.
        cmd.Parameters.Add("@Huella", SqlDbType.VarBinary, -1).Value = huella;
        cmd.Parameters.Add("@Huella2", SqlDbType.VarBinary, -1).Value = huella2;
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return new ChecadaComidaRowsResponse { Rows = DataTableToRows(table) };
    }

    public async Task BajaHuellaAsync(
        int idEmpleadoHuella,
        int idUsuario,
        string equipo,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = CreateStoredProcedureCommand(SpBajaHuella, conn);
        cmd.Parameters.AddWithValue("@IDEmpleadoHuella", idEmpleadoHuella);
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
