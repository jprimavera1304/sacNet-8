using System.Data;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// La preferencia de centro, en <c>dbo.WCentroPreferido</c> de la empresa.
/// </summary>
/// <remarks>
/// POR QUE SQL A PELO Y NO UNA ENTIDAD DE EF
/// -----------------------------------------
/// Por lo mismo que lo hace PermissionService, que vive al lado: la tabla puede
/// NO EXISTIR en una base que todavia no corrio el script, y una entidad
/// declarada en el contexto hace que cualquier consulta de esa base reviente al
/// mapear. Con SQL a pelo se puede preguntar primero si la tabla esta y
/// contestar <c>null</c> si no, que es como se comporta el resto de esta carpeta
/// con las tablas del web.
///
/// Una fila POR EMPRESA Y USUARIO: la clave primaria es (EmpresaId,
/// UsuarioWebId). Un usuario de Zaragoza no existe en Tauro —cada empresa tiene
/// su base— asi que no hay forma de que una preferencia de una se lea en la
/// otra, y la columna EmpresaId esta igual para que la tabla se parezca a las
/// demas del web y para que una base compartida no mezcle.
///
/// NO GUARDA EL NOMBRE DE LA BASE. Guarda el IDCentro, que es lo que el
/// contrato deja viajar. El nombre de la base es un dato del servidor y se
/// resuelve en la capa de conexion, cada vez.
/// </remarks>
public sealed class PreferenciaDeCentroService : IPreferenciaDeCentro
{
    private const string Tabla = "WCentroPreferido";

    private readonly AppDbContext _db;
    private readonly ILogger<PreferenciaDeCentroService> _logger;

    public PreferenciaDeCentroService(AppDbContext db, ILogger<PreferenciaDeCentroService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int?> LeerAsync(int empresaId, Guid usuarioWebId, CancellationToken ct)
    {
        try
        {
            await using var conn = await AbrirAsync(ct);
            if (!await ExisteLaTablaAsync(conn, ct)) return null;

            await using var cmd = new SqlCommand(@"
SELECT TOP 1 IdCentro
FROM dbo.WCentroPreferido
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = usuarioWebId });

            var valor = await cmd.ExecuteScalarAsync(ct);
            if (valor is null || valor is DBNull) return null;
            return Convert.ToInt32(valor);
        }
        catch (Exception ex)
        {
            /* Falla blando: sin preferencia se abre en la matriz, que es lo que
               pasa hoy. Se anota para que no se vuelva invisible. */
            _logger.LogWarning(ex, "No se pudo leer la preferencia de centro de {Usuario}.", usuarioWebId);
            return null;
        }
    }

    public async Task<bool> GuardarAsync(int empresaId, Guid usuarioWebId, int idCentro, CancellationToken ct)
    {
        try
        {
            await using var conn = await AbrirAsync(ct);
            if (!await ExisteLaTablaAsync(conn, ct)) return false;

            /*
              UPDATE primero e INSERT solo si no habia: el mismo patron que
              SincronizarCatalogoDePermisosAsync, y por la misma razon medida
              alla. Dos peticiones del mismo usuario a la vez (el web y el
              telefono, o dos pestañas) preguntaban "existe?", las dos oian "no"
              y la segunda chocaba con la clave primaria.
            */
            await using var cmd = new SqlCommand(@"
UPDATE dbo.WCentroPreferido
   SET IdCentro = @IdCentro,
       FechaActualizacion = SYSUTCDATETIME()
 WHERE EmpresaId = @EmpresaId
   AND UsuarioWebId = @UsuarioWebId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.WCentroPreferido (EmpresaId, UsuarioWebId, IdCentro, FechaActualizacion)
    SELECT @EmpresaId, @UsuarioWebId, @IdCentro, SYSUTCDATETIME()
     WHERE NOT EXISTS (
            SELECT 1 FROM dbo.WCentroPreferido WITH (UPDLOCK, HOLDLOCK)
             WHERE EmpresaId = @EmpresaId AND UsuarioWebId = @UsuarioWebId);
END", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = usuarioWebId });
            cmd.Parameters.Add(new SqlParameter("@IdCentro", SqlDbType.Int) { Value = idCentro < 0 ? 0 : idCentro });

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar la preferencia de centro de {Usuario}.", usuarioWebId);
            return false;
        }
    }

    public async Task<bool> OlvidarAsync(int empresaId, Guid usuarioWebId, CancellationToken ct)
    {
        try
        {
            await using var conn = await AbrirAsync(ct);
            if (!await ExisteLaTablaAsync(conn, ct)) return false;

            await using var cmd = new SqlCommand(@"
DELETE FROM dbo.WCentroPreferido
WHERE EmpresaId = @EmpresaId
  AND UsuarioWebId = @UsuarioWebId;", conn);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = usuarioWebId });

            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo borrar la preferencia de centro de {Usuario}.", usuarioWebId);
            return false;
        }
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    private static async Task<bool> ExisteLaTablaAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            $"SELECT CASE WHEN OBJECT_ID('dbo.{Tabla}', 'U') IS NOT NULL THEN 1 ELSE 0 END;", conn);
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is not null && Convert.ToInt32(valor) == 1;
    }
}
