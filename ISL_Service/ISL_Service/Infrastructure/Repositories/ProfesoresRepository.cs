using System.Data;
using ISL_Service.Application.DTOs.Profesores;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public class ProfesoresRepository : IProfesoresRepository
{
    private readonly IConfiguration _configuration;

    private const string SpConsultarProfesores = "dbo.sp_w_ConsultarProfesores";
    private const string SpInsertarProfesor = "dbo.sp_w_InsertarProfesor";
    private const string SpActualizarProfesor = "dbo.sp_w_ActualizarProfesor";
    private const string SpInhabilitarProfesor = "dbo.sp_w_InhabilitarProfesor";

    public ProfesoresRepository(IConfiguration configuration)
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

    public async Task<List<ProfesorDto>> ConsultarAsync(byte? estado, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarProfesores, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        var list = Funciones.DataTableToList<ProfesorDto>(dt);
        EnrichUsuariosAuditoria(conn, list);
        return list;
    }

    public async Task<ProfesorDto?> InsertarAsync(CreateProfesorRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInsertarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Telefono", request.Telefono.Trim());
        cmd.Parameters.AddWithValue("@Correo", string.IsNullOrWhiteSpace(request.Correo) ? DBNull.Value : request.Correo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var item = await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
        EnrichUsuarioAuditoria(conn, item);
        return item;
    }

    public async Task<ProfesorDto?> ActualizarAsync(Guid id, UpdateProfesorRequest request, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpActualizarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@ProfesorId", id);
        cmd.Parameters.AddWithValue("@Nombre", request.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Telefono", request.Telefono.Trim());
        cmd.Parameters.AddWithValue("@Correo", string.IsNullOrWhiteSpace(request.Correo) ? DBNull.Value : request.Correo.Trim());
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var item = await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
        EnrichUsuarioAuditoria(conn, item);
        return item;
    }

    public async Task<ProfesorDto?> InhabilitarAsync(Guid id, string? motivo, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpInhabilitarProfesor, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@ProfesorId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());

        var item = await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
        EnrichUsuarioAuditoria(conn, item);
        return item;
    }

    public async Task<ProfesorDto?> HabilitarAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
UPDATE dbo.WProfesor
SET
    Estado = 1,
    FechaActualizacion = SYSUTCDATETIME(),
    UsuarioActualizacionId = @UsuarioId,
    FechaCancelacion = NULL,
    UsuarioCancelacionId = NULL,
    MotivoCancelacion = NULL
WHERE Id = @ProfesorId;

SELECT TOP (1)
    Id,
    Nombre,
    Telefono,
    Correo,
    Estado,
    FechaCreacion,
    UsuarioCreacionId,
    FechaActualizacion,
    UsuarioActualizacionId,
    FechaCancelacion,
    UsuarioCancelacionId,
    MotivoCancelacion,
    RowVer
FROM dbo.WProfesor
WHERE Id = @ProfesorId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("@ProfesorId", id);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

        var item = await ExecuteSingleAsync<ProfesorDto>(cmd, ct);
        EnrichUsuarioAuditoria(conn, item);
        return item;
    }

    private static async Task<T?> ExecuteSingleAsync<T>(SqlCommand cmd, CancellationToken ct = default) where T : class
    {
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var dt = new DataTable();
        dt.Load(reader);
        return Funciones.DataTableToList<T>(dt).FirstOrDefault();
    }

    private static void EnrichUsuariosAuditoria(SqlConnection conn, List<ProfesorDto> items)
    {
        if (items == null || items.Count == 0) return;

        var ids = items
            .Select(x => x.UsuarioCreacionId)
            .Concat(items.Where(x => x.UsuarioActualizacionId.HasValue).Select(x => x.UsuarioActualizacionId!.Value))
            .Concat(items.Where(x => x.UsuarioCancelacionId.HasValue).Select(x => x.UsuarioCancelacionId!.Value))
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var usuarios = GetUsuariosByIds(conn, ids);
        foreach (var item in items)
        {
            item.UsuarioCreacion = ResolveUsuario(item.UsuarioCreacion, item.UsuarioCreacionId, usuarios);
            item.UsuarioActualizacion = ResolveUsuario(item.UsuarioActualizacion, item.UsuarioActualizacionId, usuarios);
            item.UsuarioCancelacion = ResolveUsuario(item.UsuarioCancelacion, item.UsuarioCancelacionId, usuarios);
        }
    }

    private static void EnrichUsuarioAuditoria(SqlConnection conn, ProfesorDto? item)
    {
        if (item is null) return;
        EnrichUsuariosAuditoria(conn, new List<ProfesorDto> { item });
    }

    private static string? ResolveUsuario(string? existing, Guid? id, Dictionary<Guid, string> usuarios)
    {
        if (!string.IsNullOrWhiteSpace(existing))
            return existing.Trim();

        if (id.HasValue && id.Value != Guid.Empty && usuarios.TryGetValue(id.Value, out var nombre))
            return nombre;

        return null;
    }

    private static Dictionary<Guid, string> GetUsuariosByIds(SqlConnection conn, List<Guid> ids)
    {
        var result = new Dictionary<Guid, string>();
        if (ids == null || ids.Count == 0) return result;

        // Estructura esperada en este proyecto.
        TryLoadUsuariosFromTable(conn, "dbo", "UsuarioWeb", "Id", "Usuario", ids, result);
        // Fallbacks por variaciones de esquema.
        TryLoadUsuariosFromTable(conn, "dbo", "UsuarioWeb", "Id", "Nombre", ids, result);
        TryLoadUsuariosFromTable(conn, "dbo", "Usuarios", "Id", "Usuario", ids, result);
        TryLoadUsuariosFromTable(conn, "dbo", "Usuario", "Id", "Usuario", ids, result);
        TryLoadUsuariosFromTable(conn, "dbo", "Usuario", "Id", "Nombre", ids, result);

        return result;
    }

    private static bool TryLoadUsuariosFromTable(
        SqlConnection conn,
        string schema,
        string table,
        string idColumn,
        string usuarioColumn,
        List<Guid> ids,
        Dictionary<Guid, string> result)
    {
        if (!TableHasColumn(conn, schema, table, idColumn) || !TableHasColumn(conn, schema, table, usuarioColumn))
            return false;

        using var cmd = conn.CreateCommand();
        var paramNames = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            var p = $"@p{i}";
            paramNames.Add(p);
            cmd.Parameters.AddWithValue(p, ids[i]);
        }

        cmd.CommandText = $@"
SELECT {idColumn}, {usuarioColumn}
FROM {schema}.{table}
WHERE {idColumn} IN ({string.Join(",", paramNames)});";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0)) continue;
            var idRaw = reader.GetValue(0);
            if (!Guid.TryParse(Convert.ToString(idRaw), out var id) || id == Guid.Empty) continue;

            var usuario = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1))?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(usuario))
                result[id] = usuario;
        }

        return true;
    }

    private static bool TableHasColumn(SqlConnection conn, string schema, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema
  AND TABLE_NAME = @table
  AND COLUMN_NAME = @column;";
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@column", column);
        return cmd.ExecuteScalar() != null;
    }
}
