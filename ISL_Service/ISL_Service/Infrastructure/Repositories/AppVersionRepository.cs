using System.Data;
using ISL_Service.Application.DTOs.AppVersion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Version minima/recomendada de la app movil (tabla WAppVersion, nuestra).
///
/// Se cachea poco (1 min) porque el valor casi nunca cambia, pero cuando cambia
/// suele ser urgente: se acaba de detectar una version mala y se quiere sacar a
/// la gente de ella ya. Un minuto es el punto medio entre no pegarle a la base
/// en cada arranque de app y no tener que esperar a que expire un cache largo.
/// </summary>
public class AppVersionRepository : IAppVersionRepository
{
    private const string Sp = "dbo.sp_w_ConsultaVersionApp";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public AppVersionRepository(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<AppVersionResponse> ConsultarAsync(string plataforma, CancellationToken ct = default)
    {
        var key = $"appVersion:{plataforma}";
        if (_cache.TryGetValue(key, out AppVersionResponse? cacheada) && cacheada != null)
            return cacheada;

        var respuesta = await LeerAsync(plataforma, ct);

        _cache.Set(key, respuesta, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return respuesta;
    }

    private async Task<AppVersionResponse> LeerAsync(string plataforma, CancellationToken ct)
    {
        // Si algo falla (SP no desplegado, base caida) se devuelve 0.0.0, o sea
        // "no bloquees a nadie". Es deliberado: un problema de infraestructura
        // NUNCA debe dejar a los repartidores sin poder abrir la app.
        var abierta = new AppVersionResponse { Plataforma = plataforma };

        try
        {
            await using var conn = GetConnection();
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(Sp, conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Plataforma", plataforma);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return abierta;

            return new AppVersionResponse
            {
                Plataforma = LeerTexto(reader, "Plataforma", plataforma),
                VersionMinima = LeerTexto(reader, "VersionMinima", "0.0.0"),
                VersionRecomendada = LeerTexto(reader, "VersionRecomendada", "0.0.0"),
                UrlTienda = LeerTexto(reader, "UrlTienda", string.Empty),
                Mensaje = LeerTextoONulo(reader, "Mensaje")
            };
        }
        catch (SqlException)
        {
            return abierta;
        }
    }

    private static string LeerTexto(SqlDataReader reader, string columna, string porDefecto)
        => LeerTextoONulo(reader, columna) ?? porDefecto;

    private static string? LeerTextoONulo(SqlDataReader reader, string columna)
    {
        var i = reader.GetOrdinal(columna);
        return reader.IsDBNull(i) ? null : reader.GetString(i);
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
