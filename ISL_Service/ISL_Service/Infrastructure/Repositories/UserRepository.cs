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

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}
