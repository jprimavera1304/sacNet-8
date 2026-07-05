using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

// Almacen de refresh tokens (solo para la app movil). Guarda el HASH del token
// (no el token). Tabla WRefreshToken (aditiva, prefijo W, no toca legacy).
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IConfiguration _configuration;
    private bool _schemaChecked;

    public RefreshTokenRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaChecked) return;
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = @"
IF OBJECT_ID('dbo.WRefreshToken','U') IS NULL
BEGIN
    CREATE TABLE dbo.WRefreshToken (
        Id             bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmpresaId      int NOT NULL,
        UserId         uniqueidentifier NOT NULL,
        LegacyUserId   int NOT NULL,
        UsuarioNombre  nvarchar(150) NOT NULL,
        Rol            nvarchar(100) NOT NULL,
        CompanyKey     nvarchar(100) NOT NULL,
        TokenHash      varbinary(32) NOT NULL,
        ExpiraEn       datetime2 NOT NULL,
        Revocado       bit NOT NULL CONSTRAINT DF_WRefreshToken_Revocado DEFAULT(0),
        FechaCreacion  datetime2 NOT NULL CONSTRAINT DF_WRefreshToken_FechaCreacion DEFAULT(SYSUTCDATETIME()),
        FechaUltimoUso datetime2 NULL
    );
    CREATE INDEX IX_WRefreshToken_TokenHash ON dbo.WRefreshToken(TokenHash);
    CREATE INDEX IX_WRefreshToken_UserId ON dbo.WRefreshToken(UserId);
END";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        await cmd.ExecuteNonQueryAsync(ct);
        _schemaChecked = true;
    }

    public async Task CreateAsync(RefreshTokenIdentity identity, byte[] tokenHash, DateTime expiraEnUtc, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = @"
INSERT INTO dbo.WRefreshToken (EmpresaId, UserId, LegacyUserId, UsuarioNombre, Rol, CompanyKey, TokenHash, ExpiraEn, Revocado, FechaCreacion)
VALUES (@EmpresaId, @UserId, @LegacyUserId, @UsuarioNombre, @Rol, @CompanyKey, @TokenHash, @ExpiraEn, 0, SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = identity.EmpresaId });
        cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.UniqueIdentifier) { Value = identity.UserId });
        cmd.Parameters.Add(new SqlParameter("@LegacyUserId", SqlDbType.Int) { Value = identity.LegacyUserId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioNombre", SqlDbType.NVarChar, 150) { Value = identity.UsuarioNombre ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 100) { Value = identity.Rol ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@CompanyKey", SqlDbType.NVarChar, 100) { Value = identity.CompanyKey ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@TokenHash", SqlDbType.VarBinary, 32) { Value = tokenHash });
        cmd.Parameters.Add(new SqlParameter("@ExpiraEn", SqlDbType.DateTime2) { Value = expiraEnUtc });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<RefreshTokenIdentity?> ValidateAsync(byte[] tokenHash, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = @"
UPDATE dbo.WRefreshToken SET FechaUltimoUso = SYSUTCDATETIME()
OUTPUT inserted.UserId, inserted.LegacyUserId, inserted.UsuarioNombre, inserted.Rol, inserted.EmpresaId, inserted.CompanyKey
WHERE TokenHash = @TokenHash AND Revocado = 0 AND ExpiraEn > SYSUTCDATETIME();";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.Add(new SqlParameter("@TokenHash", SqlDbType.VarBinary, 32) { Value = tokenHash });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new RefreshTokenIdentity(
            reader.GetGuid(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5));
    }

    public async Task RevokeAsync(byte[] tokenHash, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        const string sql = "UPDATE dbo.WRefreshToken SET Revocado = 1 WHERE TokenHash = @TokenHash;";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.Add(new SqlParameter("@TokenHash", SqlDbType.VarBinary, 32) { Value = tokenHash });
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

        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }
}
