using System.Data;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
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
END

/*
  EL CANAL DE LA SESION, AGREGADO DESPUES.

  ADITIVO y NULL. Las dos bases locales ya tienen la tabla creada (medido el
  2026-10-15: existe en MacZ y en Produccion_svr, con 13 columnas), asi que el
  CREATE de arriba ya no corre nunca y sin este ALTER la columna no apareceria.

  NULL significa -renglon de antes de este cambio-, y se lee como canal MOVIL
  (ver RefreshTokenIdentity): esta tabla la escribe SOLO la app. NO se le pone
  DEFAULT 'web' precisamente para no convertir en web a los repartidores que ya
  tienen la app instalada -eso les apagaria sus claves `app_movil.*` y les
  cerraria la captura-, que es lo unico que este despliegue podria romper.
*/
/*
  Y POR QUE VA EN UN TRY/CATCH: LA CARRERA DEL PRIMER MINUTO.

  En produccion esta columna NO EXISTE todavia (medido: las dos bases tienen 12
  columnas y ninguna es `Canal`), asi que este ALTER corre de verdad, una vez,
  en la PRIMERA peticion que toque la tabla despues del despliegue. Y justo ese
  es el momento en que puede haber varias a la vez: el despliegue reinicia el
  proceso y las apps que estaban abiertas reintentan su refresh todas juntas.

  Dos peticiones simultaneas pueden pasar las dos la comprobacion de
  `COL_LENGTH ... IS NULL` antes de que la primera termine; la segunda se queda
  esperando el bloqueo de esquema y, cuando entra, el ALTER le revienta con
  -Column names in each table must be unique- (error 2705). El sintoma seria un
  login que falla una sola vez, justo tras el despliegue — de los peores de
  diagnosticar, porque no se reproduce.

  Asi que si el ALTER falla se vuelve a mirar: si la columna YA ESTA, la
  agrego la otra sesion y no hay nada que arreglar. Si NO esta, el error es
  real y se relanza tal cual, para que se vea en el log y no se convierta
  despues en un -Invalid column name 'Canal'- sin explicacion.

  Y EL ALTER VA DENTRO DE UN EXEC, QUE NO ES ADORNO. Un TRY/CATCH a secas NO
  SIRVE AQUI: el 2705 lo levanta el COMPILADOR del lote, no su ejecucion, y un
  error de compilacion aborta el lote entero sin pasar por el CATCH. Comprobado
  en MacZ el 2026-10-15: con el ALTER escrito directo, el CATCH no se ejecuta y
  el error sale fuera; metido en un EXEC —que se compila aparte, al correr— el
  CATCH si lo recoge (2705) y un error de verdad sigue saliendo (se probo con
  una tabla inexistente: 4902 se relanza).

  LO QUE NO HACE FALTA COMPROBAR: permisos. La API se conecta como `sa` con
  sysadmin, asi que el ALTER no puede fallar por no poder alterar la tabla.
*/
IF OBJECT_ID('dbo.WRefreshToken','U') IS NOT NULL
   AND COL_LENGTH('dbo.WRefreshToken','Canal') IS NULL
BEGIN
    BEGIN TRY
        EXEC('ALTER TABLE dbo.WRefreshToken ADD Canal varchar(10) NULL;');
    END TRY
    BEGIN CATCH
        IF COL_LENGTH('dbo.WRefreshToken','Canal') IS NULL THROW;
    END CATCH
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
INSERT INTO dbo.WRefreshToken (EmpresaId, UserId, LegacyUserId, UsuarioNombre, Rol, CompanyKey, Canal, TokenHash, ExpiraEn, Revocado, FechaCreacion)
VALUES (@EmpresaId, @UserId, @LegacyUserId, @UsuarioNombre, @Rol, @CompanyKey, @Canal, @TokenHash, @ExpiraEn, 0, SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = identity.EmpresaId });
        cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.UniqueIdentifier) { Value = identity.UserId });
        cmd.Parameters.Add(new SqlParameter("@LegacyUserId", SqlDbType.Int) { Value = identity.LegacyUserId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioNombre", SqlDbType.NVarChar, 150) { Value = identity.UsuarioNombre ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@Rol", SqlDbType.NVarChar, 100) { Value = identity.Rol ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@CompanyKey", SqlDbType.NVarChar, 100) { Value = identity.CompanyKey ?? string.Empty });
        // El canal con el que se inicio la sesion. Se guarda para que el refresh
        // lo ARRASTRE en vez de volver a preguntarselo al cliente: si se le
        // preguntara, declarar "web" al renovar seria la salida facil del canal
        // movil y con ella la de `app_movil.acceso`.
        cmd.Parameters.Add(new SqlParameter("@Canal", SqlDbType.VarChar, 10)
        {
            Value = (object?)CanalDeAcceso.AClaim(identity.CanalSesion) ?? DBNull.Value
        });
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
OUTPUT inserted.UserId, inserted.LegacyUserId, inserted.UsuarioNombre, inserted.Rol, inserted.EmpresaId, inserted.CompanyKey, inserted.Canal
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
            reader.GetString(5),
            // NULL = renglon escrito antes de este cambio -> NoDeclarado, que
            // UserService resuelve como movil (ver su comentario).
            reader.IsDBNull(6) ? Canal.NoDeclarado : CanalDeAcceso.DeTexto(reader.GetString(6)));
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
