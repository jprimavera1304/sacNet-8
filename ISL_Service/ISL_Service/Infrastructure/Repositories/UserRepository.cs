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
    private const int EMPRESA_ID_UNICA = 1;
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct)
    {
        var usuarioTrim = usuario.Trim();
        return _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == EMPRESA_ID_UNICA && x.UsuarioNombre == usuarioTrim, ct);
    }

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Usuarios
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == EMPRESA_ID_UNICA, ct);
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

            return new WebLoginFallbackResult
            {
                ResultCode = resultCode,
                Source = reader.GetString(reader.GetOrdinal("Source")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                Usuario = reader.GetString(reader.GetOrdinal("Usuario")),
                ContrasenaHash = reader.GetString(reader.GetOrdinal("ContrasenaHash")),
                Rol = reader.GetString(reader.GetOrdinal("Rol")),
                EmpresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId")),
                DebeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena")),
                Estado = reader.GetInt32(reader.GetOrdinal("Estado"))
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

        return new WebLoginFallbackResult
        {
            ResultCode = 1,
            Source = "WEB",
            UserId = reader.GetGuid(reader.GetOrdinal("Id")),
            Usuario = reader.GetString(reader.GetOrdinal("Usuario")),
            ContrasenaHash = reader.GetString(reader.GetOrdinal("ContrasenaHash")),
            Rol = reader.GetString(reader.GetOrdinal("Rol")),
            EmpresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId")),
            DebeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena")),
            Estado = reader.GetInt32(reader.GetOrdinal("Estado"))
        };
    }

    public Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct)
    {
        var usuarioTrim = usuario.Trim();
        return _db.Usuarios.AnyAsync(x => x.EmpresaId == EMPRESA_ID_UNICA && x.UsuarioNombre == usuarioTrim, ct);
    }

    public Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct)
    {
        return _db.Usuarios.AsNoTracking()
            .Where(x => x.EmpresaId == EMPRESA_ID_UNICA)
            .OrderBy(x => x.UsuarioNombre)
            .ToListAsync(ct);
    }

    public Task AddAsync(Usuario user, CancellationToken ct)
    {
        user.EmpresaId = EMPRESA_ID_UNICA;
        return _db.Usuarios.AddAsync(user, ct).AsTask();
    }

    public Task UpdateAsync(Usuario user, CancellationToken ct)
    {
        user.EmpresaId = EMPRESA_ID_UNICA;
        _db.Usuarios.Update(user);
        return Task.CompletedTask;
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
            throw new InvalidOperationException("sp_WebUsuario_Upsert no devolvio filas.");

        return new Usuario
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UsuarioNombre = reader.GetString(reader.GetOrdinal("Usuario")),
            ContrasenaHash = contrasenaHashWeb,
            Rol = reader.GetString(reader.GetOrdinal("Rol")),
            EmpresaId = reader.GetInt32(reader.GetOrdinal("EmpresaId")),
            DebeCambiarContrasena = reader.GetBoolean(reader.GetOrdinal("DebeCambiarContrasena")),
            Estado = reader.GetInt32(reader.GetOrdinal("Estado")),
            FechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion")),
            FechaActualizacion = reader.GetDateTime(reader.GetOrdinal("FechaActualizacion"))
        };
    }

    public async Task<Usuario> UpdateUsuarioAndRolAsync(Guid userId, string usuarioNuevo, string rolNuevo, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var currentCmd = new SqlCommand(@"
SELECT TOP 1 Usuario, DebeCambiarContrasena, Estado, FechaCreacion, FechaActualizacion
FROM dbo.UsuarioWeb
WHERE Id = @Id AND EmpresaId = 1;", conn, (SqlTransaction)tx);
            currentCmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });

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
WHERE Id = @Id AND EmpresaId = 1;", conn, (SqlTransaction)tx);
            updateWeb.Parameters.Add(new SqlParameter("@UsuarioNuevo", SqlDbType.NVarChar, 80) { Value = usuarioNuevo });
            updateWeb.Parameters.Add(new SqlParameter("@RolNuevo", SqlDbType.NVarChar, 30) { Value = rolNuevo });
            updateWeb.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
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
                EmpresaId = EMPRESA_ID_UNICA,
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
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var currentCmd = new SqlCommand(@"
SELECT TOP 1 Usuario, Rol, DebeCambiarContrasena, FechaCreacion, FechaActualizacion
FROM dbo.UsuarioWeb
WHERE Id = @Id AND EmpresaId = 1;", conn, (SqlTransaction)tx);
            currentCmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });

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
WHERE Id = @Id AND EmpresaId = 1;", conn, (SqlTransaction)tx);
            updateWeb.Parameters.Add(new SqlParameter("@Estado", SqlDbType.Int) { Value = estado });
            updateWeb.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
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
                EmpresaId = EMPRESA_ID_UNICA,
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
}
