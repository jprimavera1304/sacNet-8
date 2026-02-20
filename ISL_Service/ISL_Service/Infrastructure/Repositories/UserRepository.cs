using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Domain.Entities;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISL_Service.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        var usuarioTrim = usuario.Trim();
        return await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.UsuarioNombre == usuarioTrim, ct);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        return await _db.Usuarios
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
    }

    public async Task<WebLoginFallbackResult?> LoginWithFallbackAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        try
        {
            await using var cmd = new SqlCommand("dbo.sp_WebLogin_FallbackLegacy", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };

            cmd.Parameters.Add(new SqlParameter("@Usuario", SqlDbType.NVarChar, 80) { Value = usuario.Trim() });
            cmd.Parameters.Add(new SqlParameter("@ContrasenaPlano", SqlDbType.NVarChar, 250) { Value = contrasenaPlano });
            cmd.Parameters.Add(new SqlParameter("@ContrasenaHashWeb", SqlDbType.NVarChar, 250) { Value = contrasenaHashWeb });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            var resultCode = reader.GetInt32(reader.GetOrdinal("ResultCode"));
            if (resultCode == 0) return null;

            var source = reader.GetString(reader.GetOrdinal("Source"));
            var userId = reader.GetGuid(reader.GetOrdinal("UserId"));
            var usuarioDb = reader.GetString(reader.GetOrdinal("Usuario"));
            var contrasenaHash = reader.GetString(reader.GetOrdinal("ContrasenaHash"));
            var rol = reader.GetString(reader.GetOrdinal("Rol"));
            var empresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId"));
            var debeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena"));
            var estado = reader.GetInt32(reader.GetOrdinal("Estado"));
            var legacyUserId = ReadLegacyUserIdOrDefault(reader);

            await reader.DisposeAsync();

            if (legacyUserId <= 0)
            {
                legacyUserId = await ResolveLegacyUserIdAsync(conn, usuarioDb, ct);
            }

            return new WebLoginFallbackResult
            {
                ResultCode = resultCode,
                Source = source,
                UserId = userId,
                LegacyUserId = legacyUserId,
                Usuario = usuarioDb,
                ContrasenaHash = contrasenaHash,
                Rol = rol,
                EmpresaId = empresaId,
                DebeCambiarContrasena = debeCambiarContrasena,
                Estado = estado
            };
        }
        catch (SqlException ex) when (ex.Number is 2812 or 208)
        {
            // Fresh databases may not have legacy SP/tables yet; allow WEB-only auth.
            return await LoginWebOnlyAsync(conn, usuario, ct);
        }
    }

    private static async Task<WebLoginFallbackResult?> LoginWebOnlyAsync(SqlConnection conn, string usuario, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1
    Id,
    Usuario,
    ContrasenaHash,
    Rol,
    EmpresaId,
    DebeCambiarContrasena,
    Estado
FROM dbo.UsuarioWeb
WHERE UPPER(LTRIM(RTRIM(Usuario))) = UPPER(LTRIM(RTRIM(@Usuario)));", conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 30
        };
        cmd.Parameters.Add(new SqlParameter("@Usuario", SqlDbType.NVarChar, 80) { Value = usuario.Trim() });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var userId = reader.GetGuid(reader.GetOrdinal("Id"));
        var usuarioDb = reader.GetString(reader.GetOrdinal("Usuario"));
        var contrasenaHash = reader.GetString(reader.GetOrdinal("ContrasenaHash"));
        var rol = reader.GetString(reader.GetOrdinal("Rol"));
        var empresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId"));
        var debeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena"));
        var estado = reader.GetInt32(reader.GetOrdinal("Estado"));

        await reader.DisposeAsync();

        var legacyUserId = await ResolveLegacyUserIdAsync(conn, usuarioDb, ct);

        return new WebLoginFallbackResult
        {
            ResultCode = 1,
            Source = "WEB",
            UserId = userId,
            LegacyUserId = legacyUserId,
            Usuario = usuarioDb,
            ContrasenaHash = contrasenaHash,
            Rol = rol,
            EmpresaId = empresaId,
            DebeCambiarContrasena = debeCambiarContrasena,
            Estado = estado
        };
    }

    public async Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        var usuarioTrim = usuario.Trim();
        return await _db.Usuarios.AnyAsync(x => x.EmpresaId == empresaId && x.UsuarioNombre == usuarioTrim, ct);
    }

    public async Task<List<RoleCatalogItem>> ListRolesCatalogAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        var roles = new List<RoleCatalogItem>();
        try
        {
            await using var cmd = new SqlCommand(@"
SELECT Codigo, Nombre
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
ORDER BY Codigo;", conn);
            var effectiveEmpresaId = empresaId > 0 ? empresaId : await ResolveEmpresaIdAsync(ct);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = effectiveEmpresaId });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                roles.Add(new RoleCatalogItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Codigo")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }
        catch (SqlException ex) when (ex.Number is 208 or 207)
        {
            // WRol no existe en bases antiguas.
        }

        if (roles.Count == 0)
        {
            roles.Add(new RoleCatalogItem { Code = "SUPER_ADMIN", Name = "SuperAdmin" });
            roles.Add(new RoleCatalogItem { Code = "ADMIN", Name = "Admin" });
            roles.Add(new RoleCatalogItem { Code = "USER", Name = "User" });
        }

        return roles;
    }

    public async Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct)
    {
        var effectiveEmpresaId = (empresaId.HasValue && empresaId.Value > 0) ? empresaId.Value : await ResolveEmpresaIdAsync(ct);
        return await _db.Usuarios.AsNoTracking()
            .Where(x => x.EmpresaId == effectiveEmpresaId)
            .OrderBy(x => x.UsuarioNombre)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Usuario user, CancellationToken ct)
    {
        user.EmpresaId = await ResolveEmpresaIdAsync(ct);
        await _db.Usuarios.AddAsync(user, ct);
    }

    public async Task UpdateAsync(Usuario user, CancellationToken ct)
    {
        user.EmpresaId = await ResolveEmpresaIdAsync(ct);
        _db.Usuarios.Update(user);
    }

    public async Task<Usuario> UpsertWebAndLegacyAsync(
        string usuario,
        string contrasenaPlano,
        string contrasenaHashWeb,
        string nombre,
        string rol,
        bool debeCambiarContrasena,
        int estado,
        CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand("dbo.sp_WebUsuario_Upsert", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };

            cmd.Parameters.Add(new SqlParameter("@Usuario", SqlDbType.NVarChar, 80) { Value = usuario.Trim() });
            cmd.Parameters.Add(new SqlParameter("@ContrasenaPlano", SqlDbType.NVarChar, 250) { Value = contrasenaPlano });
            cmd.Parameters.Add(new SqlParameter("@ContrasenaHashWeb", SqlDbType.NVarChar, 250) { Value = contrasenaHashWeb });
            cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 255) { Value = nombre.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 30) { Value = rol.Trim() });
            cmd.Parameters.Add(new SqlParameter("@DebeCambiarContrasena", SqlDbType.Bit) { Value = debeCambiarContrasena });
            cmd.Parameters.Add(new SqlParameter("@Estado", SqlDbType.Int) { Value = estado });
            cmd.Parameters.Add(new SqlParameter("@IDPerfil", SqlDbType.Int) { Value = DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@IDStatusLegacy", SqlDbType.Int) { Value = 1 });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return await UpsertWebOnlyAsync(usuario, contrasenaHashWeb, rol, debeCambiarContrasena, estado, ct);
            }

            var resultId = reader.GetGuid(reader.GetOrdinal("Id"));
            var resultUsuario = reader.GetString(reader.GetOrdinal("Usuario"));
            var resultEmpresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId"));
            var resultRol = reader.GetString(reader.GetOrdinal("Rol"));
            var resultDebeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena"));
            var resultEstado = reader.GetInt32(reader.GetOrdinal("Estado"));
            var resultFechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion"));
            var resultFechaActualizacion = reader.GetDateTime(reader.GetOrdinal("FechaActualizacion"));
            var rolSolicitado = rol.Trim();

            if (!string.Equals(resultRol, rolSolicitado, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureRolPersistedAsync(conn, resultId, resultEmpresaId, rolSolicitado, ct);
                resultRol = rolSolicitado;
            }

            return new Usuario
            {
                Id = resultId,
                UsuarioNombre = resultUsuario,
                ContrasenaHash = contrasenaHashWeb,
                Rol = resultRol,
                EmpresaId = resultEmpresaId,
                DebeCambiarContrasena = resultDebeCambiarContrasena,
                Estado = resultEstado,
                FechaCreacion = resultFechaCreacion,
                FechaActualizacion = resultFechaActualizacion
            };
        }
        catch (SqlException)
        {
            // Rollout gradual: si SP/tabla legacy no existe o falla, persistir en UsuarioWeb.
            return await UpsertWebOnlyAsync(usuario, contrasenaHashWeb, rol, debeCambiarContrasena, estado, ct);
        }
    }

    private async Task<Usuario> UpsertWebOnlyAsync(
        string usuario,
        string contrasenaHashWeb,
        string rol,
        bool debeCambiarContrasena,
        int estado,
        CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        var usuarioTrim = usuario.Trim();
        var now = DateTime.UtcNow;

        var entity = await _db.Usuarios
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.UsuarioNombre == usuarioTrim, ct);

        if (entity is null)
        {
            entity = new Usuario
            {
                Id = Guid.NewGuid(),
                UsuarioNombre = usuarioTrim,
                ContrasenaHash = contrasenaHashWeb,
                Rol = rol.Trim(),
                EmpresaId = empresaId,
                DebeCambiarContrasena = debeCambiarContrasena,
                Estado = estado,
                FechaCreacion = now,
                FechaActualizacion = now
            };
            _db.Usuarios.Add(entity);
        }
        else
        {
            entity.ContrasenaHash = contrasenaHashWeb;
            entity.Rol = rol.Trim();
            entity.DebeCambiarContrasena = debeCambiarContrasena;
            entity.Estado = estado;
            entity.FechaActualizacion = now;
        }

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private static async Task EnsureRolPersistedAsync(
        SqlConnection conn,
        Guid userId,
        int empresaId,
        string rol,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
UPDATE dbo.UsuarioWeb
SET Rol = @Rol,
    FechaActualizacion = SYSUTCDATETIME()
WHERE Id = @Id
  AND EmpresaId = @EmpresaId;", conn);

        cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 30) { Value = rol });
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Usuario> UpdateUsuarioAndRolAsync(Guid userId, string usuarioNuevo, string rolNuevo, CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var currentCmd = new SqlCommand(@"
SELECT TOP 1 Usuario, DebeCambiarContrasena, Estado, FechaCreacion, FechaActualizacion
FROM dbo.UsuarioWeb
WHERE Id = @Id AND EmpresaId = @EmpresaId;", conn, (SqlTransaction)tx);
            currentCmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
            currentCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

            string? usuarioActual = null;
            bool debeCambiarContrasena = false;
            int estado = 1;
            DateTime fechaCreacion = DateTime.UtcNow;
            DateTime fechaActualizacion = DateTime.UtcNow;

            await using (var reader = await currentCmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new KeyNotFoundException("Usuario no encontrado.");

                usuarioActual = reader.GetString(reader.GetOrdinal("Usuario"));
                debeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena"));
                estado = reader.GetInt32(reader.GetOrdinal("Estado"));
                fechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion"));
                fechaActualizacion = reader.GetDateTime(reader.GetOrdinal("FechaActualizacion"));
            }

            var updateWeb = new SqlCommand(@"
UPDATE dbo.UsuarioWeb
SET Usuario = @UsuarioNuevo,
    Rol = @RolNuevo,
    FechaActualizacion = SYSUTCDATETIME()
WHERE Id = @Id AND EmpresaId = @EmpresaId;", conn, (SqlTransaction)tx);
            updateWeb.Parameters.Add(new SqlParameter("@UsuarioNuevo", SqlDbType.NVarChar, 80) { Value = usuarioNuevo });
            updateWeb.Parameters.Add(new SqlParameter("@RolNuevo", SqlDbType.NVarChar, 30) { Value = rolNuevo });
            updateWeb.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
            updateWeb.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await updateWeb.ExecuteNonQueryAsync(ct);

            if (!string.IsNullOrWhiteSpace(usuarioActual))
            {
                var updateLegacy = new SqlCommand(@"
UPDATE dbo.Usuarios
SET Usuario = @UsuarioNuevo,
    Nombre = @UsuarioNuevo
WHERE UPPER(LTRIM(RTRIM(Usuario))) = UPPER(LTRIM(RTRIM(@UsuarioActual)));", conn, (SqlTransaction)tx);
                updateLegacy.Parameters.Add(new SqlParameter("@UsuarioNuevo", SqlDbType.VarChar, 150) { Value = usuarioNuevo });
                updateLegacy.Parameters.Add(new SqlParameter("@UsuarioActual", SqlDbType.VarChar, 150) { Value = usuarioActual });
                await updateLegacy.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            return new Usuario
            {
                Id = userId,
                UsuarioNombre = usuarioNuevo,
                Rol = rolNuevo,
                EmpresaId = empresaId,
                DebeCambiarContrasena = debeCambiarContrasena,
                Estado = estado,
                FechaCreacion = fechaCreacion,
                FechaActualizacion = fechaActualizacion
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Usuario> UpdateEstadoWithLegacyAsync(Guid userId, int estado, CancellationToken ct)
    {
        var empresaId = await ResolveEmpresaIdAsync(ct);
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var currentCmd = new SqlCommand(@"
SELECT TOP 1 Usuario, Rol, DebeCambiarContrasena, FechaCreacion, FechaActualizacion
FROM dbo.UsuarioWeb
WHERE Id = @Id AND EmpresaId = @EmpresaId;", conn, (SqlTransaction)tx);
            currentCmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
            currentCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });

            string? usuario = null;
            string rol = "User";
            bool debeCambiarContrasena = false;
            DateTime fechaCreacion = DateTime.UtcNow;
            DateTime fechaActualizacion = DateTime.UtcNow;

            await using (var reader = await currentCmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new KeyNotFoundException("Usuario no encontrado.");

                usuario = reader.GetString(reader.GetOrdinal("Usuario"));
                rol = reader.GetString(reader.GetOrdinal("Rol"));
                debeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena"));
                fechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion"));
                fechaActualizacion = reader.GetDateTime(reader.GetOrdinal("FechaActualizacion"));
            }

            var updateWeb = new SqlCommand(@"
UPDATE dbo.UsuarioWeb
SET Estado = @Estado,
    FechaActualizacion = SYSUTCDATETIME()
WHERE Id = @Id AND EmpresaId = @EmpresaId;", conn, (SqlTransaction)tx);
            updateWeb.Parameters.Add(new SqlParameter("@Estado", SqlDbType.Int) { Value = estado });
            updateWeb.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
            updateWeb.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await updateWeb.ExecuteNonQueryAsync(ct);

            if (!string.IsNullOrWhiteSpace(usuario))
            {
                var updateLegacy = new SqlCommand(@"
UPDATE dbo.Usuarios
SET IDStatus = @Estado
WHERE UPPER(LTRIM(RTRIM(Usuario))) = UPPER(LTRIM(RTRIM(@Usuario)));", conn, (SqlTransaction)tx);
                updateLegacy.Parameters.Add(new SqlParameter("@Estado", SqlDbType.Int) { Value = estado });
                updateLegacy.Parameters.Add(new SqlParameter("@Usuario", SqlDbType.VarChar, 150) { Value = usuario });
                await updateLegacy.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            return new Usuario
            {
                Id = userId,
                UsuarioNombre = usuario ?? string.Empty,
                Rol = rol,
                EmpresaId = empresaId,
                DebeCambiarContrasena = debeCambiarContrasena,
                Estado = estado,
                FechaCreacion = fechaCreacion,
                FechaActualizacion = fechaActualizacion
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);

    private static int ReadLegacyUserIdOrDefault(SqlDataReader reader)
    {
        var byLegacyId = TryReadInt(reader, "LegacyId");
        if (byLegacyId > 0) return byLegacyId;

        var byIdUsuario = TryReadInt(reader, "IDUsuario");
        if (byIdUsuario > 0) return byIdUsuario;

        return 0;
    }

    private static int TryReadInt(SqlDataReader reader, string column)
    {
        var ordinal = TryGetOrdinal(reader, column);
        if (ordinal < 0 || reader.IsDBNull(ordinal)) return 0;
        return int.TryParse(reader.GetValue(ordinal)?.ToString(), out var value) ? value : 0;
    }

    private static int TryGetOrdinal(SqlDataReader reader, string column)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static async Task<int> ResolveLegacyUserIdAsync(SqlConnection conn, string usuario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(usuario)) return 0;
        var usuarioNorm = usuario.Trim();

        var idFromUsuarios = await TryResolveLegacyFromTableAsync(conn, "dbo", "Usuarios", "IDUsuario", "Usuario", "IDStatus", usuarioNorm, ct);
        if (idFromUsuarios > 0) return idFromUsuarios;

        var idFromUsuario = await TryResolveLegacyFromTableAsync(conn, "dbo", "Usuario", "IDUsuario", "Usuario", "IDStatus", usuarioNorm, ct);
        return idFromUsuario;
    }

    private static async Task<int> TryResolveLegacyFromTableAsync(
        SqlConnection conn,
        string schema,
        string table,
        string idColumn,
        string usuarioColumn,
        string? statusColumn,
        string usuario,
        CancellationToken ct)
    {
        if (!await TableHasColumnAsync(conn, schema, table, idColumn, ct)) return 0;
        if (!await TableHasColumnAsync(conn, schema, table, usuarioColumn, ct)) return 0;

        var hasStatusColumn = !string.IsNullOrWhiteSpace(statusColumn) &&
            await TableHasColumnAsync(conn, schema, table, statusColumn!, ct);

        var whereStatus = hasStatusColumn ? $" AND {statusColumn} = 1" : "";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT TOP 1 {idColumn}
FROM {schema}.{table}
WHERE UPPER(LTRIM(RTRIM(CAST({usuarioColumn} AS NVARCHAR(150))))) = UPPER(LTRIM(RTRIM(@Usuario)))
{whereStatus}
ORDER BY {idColumn};";
        cmd.Parameters.Add(new SqlParameter("@Usuario", SqlDbType.NVarChar, 150) { Value = usuario });

        var raw = await cmd.ExecuteScalarAsync(ct);
        return (raw is null || raw is DBNull) ? 0 : Convert.ToInt32(raw);
    }

    private static async Task<bool> TableHasColumnAsync(
        SqlConnection conn,
        string schema,
        string table,
        string column,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema
  AND TABLE_NAME = @table
  AND COLUMN_NAME = @column;";
        cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
        cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
        cmd.Parameters.Add(new SqlParameter("@column", SqlDbType.NVarChar, 128) { Value = column });
        var exists = await cmd.ExecuteScalarAsync(ct);
        return exists is not null;
    }

    private async Task<int> ResolveEmpresaIdAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        await using var countCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.EmpresaWeb;", conn);
        var countObj = await countCmd.ExecuteScalarAsync(ct);
        var count = (countObj is null || countObj is DBNull) ? 0 : Convert.ToInt32(countObj);
        if (count == 0)
            throw new InvalidOperationException("No existe empresa en dbo.EmpresaWeb.");
        if (count > 1)
            throw new InvalidOperationException("dbo.EmpresaWeb tiene mas de una fila. Debe existir exactamente una.");

        await using var idCmd = new SqlCommand("SELECT TOP 1 Id FROM dbo.EmpresaWeb ORDER BY Id;", conn);
        var idObj = await idCmd.ExecuteScalarAsync(ct);
        if (idObj is null || idObj is DBNull)
            throw new InvalidOperationException("No se pudo resolver EmpresaWeb.Id.");
        return Convert.ToInt32(idObj);
    }
}
