using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// El catalogo de centros de servicio (tabla CentrosServicio, de legacy).
/// </summary>
/// <remarks>
/// OJO CON LA COLUMNA BaseCentro: no guarda el nombre de la base, guarda un
/// PREFIJO de SQL listo para concatenar dentro de una consulta:
///
///     [MacZCS1].[dbo].
///
/// Legacy lo pega tal cual delante del nombre de la tabla. Aqui no se hace eso
/// —se cambia la base de la conexion, no se arma SQL con texto—, asi que hay
/// que sacarle el nombre: lo que va entre los primeros corchetes.
///
/// Se lee siempre de la base PRINCIPAL. Las bases de los centros no tienen ni
/// este catalogo ni las tablas de permisos (comprobado: MacZCS4 no tiene
/// UsuarioWeb ni WPermiso), y eso es justo lo que queremos — quien eres y que
/// puedes ver se decide en un solo sitio.
/// </remarks>
public class CentrosServicioRepository : ICentrosServicioRepository
{
    private const string CacheKey = "centros-servicio";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public CentrosServicioRepository(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<IReadOnlyList<CentroServicioItem>> ListarAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<CentroServicioItem>>(CacheKey, out var cacheado) && cacheado is not null)
            return cacheado;

        var lista = await LeerAsync(ct);
        _cache.Set(CacheKey, lista, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        return lista;
    }

    public async Task<CentroServicioItem?> BuscarAsync(int idCentro, CancellationToken ct = default)
    {
        if (idCentro <= 0) return null;
        var lista = await ListarAsync(ct);
        return lista.FirstOrDefault(x => x.IdCentro == idCentro);
    }

    private async Task<IReadOnlyList<CentroServicioItem>> LeerAsync(CancellationToken ct)
    {
        var resultado = new List<CentroServicioItem>();
        try
        {
            await using var conn = GetConnection();
            await conn.OpenAsync(ct);

            /* Si la tabla no esta, esta empresa simplemente no tiene centros:
               se devuelve vacio y el selector no se pinta. No es un error. */
            await using (var existe = new SqlCommand(
                "SELECT COUNT(*) FROM sys.tables WHERE name = 'CentrosServicio';", conn))
            {
                var n = Convert.ToInt32(await existe.ExecuteScalarAsync(ct));
                if (n == 0) return resultado;
            }

            await using var cmd = new SqlCommand(@"
SELECT IDCentro, Numero, NombreCentro, BaseCentro
FROM dbo.CentrosServicio
WHERE IDStatus = 1
ORDER BY Numero;", conn);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var baseReal = NombreDeLaBase(reader["BaseCentro"]?.ToString());
                if (string.IsNullOrWhiteSpace(baseReal)) continue;

                resultado.Add(new CentroServicioItem
                {
                    IdCentro = Convert.ToInt32(reader["IDCentro"]),
                    Numero = reader["Numero"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Numero"]),
                    Nombre = (reader["NombreCentro"]?.ToString() ?? "").Trim(),
                    Base = baseReal
                });
            }
        }
        catch (SqlException)
        {
            /* Degrada abierto: sin catalogo no hay selector, pero los reportes
               de la base principal siguen funcionando igual que hoy. */
            return new List<CentroServicioItem>();
        }

        return resultado;
    }

    /// <summary>"[MacZCS1].[dbo]." -> "MacZCS1". Null si no tiene esa forma.</summary>
    public static string NombreDeLaBase(string? prefijoDeLegacy)
    {
        var texto = (prefijoDeLegacy ?? "").Trim();
        if (texto.Length == 0) return "";

        var abre = texto.IndexOf('[');
        var cierra = texto.IndexOf(']');
        if (abre >= 0 && cierra > abre + 1)
            return texto.Substring(abre + 1, cierra - abre - 1).Trim();

        /* Por si algun dia alguien la captura sin corchetes. Se toma el primer
           pedazo antes del punto, que es lo unico que puede ser la base. */
        var punto = texto.IndexOf('.');
        return (punto > 0 ? texto[..punto] : texto).Trim();
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
}
