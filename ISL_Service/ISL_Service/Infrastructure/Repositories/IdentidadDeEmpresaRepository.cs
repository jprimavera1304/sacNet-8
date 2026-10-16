using System.Data;
using ISL_Service.Application.Centros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Repositories;

/// <inheritdoc cref="IIdentidadDeEmpresaRepository"/>
/// <remarks>
/// SE CACHEA, INCLUIDO EL "NO". Preguntarle a doce bases quien son en cada
/// peticion son doce viajes a SQL de mas, y los viajes de mas son exactamente
/// lo que ya puso lento este backend una vez. El veredicto NEGATIVO se cachea
/// igual que el positivo: si no se cacheara, una base caida convertiria cada
/// peticion en una espera de Connect Timeout — un centro roto no puede frenar
/// la matriz.
///
/// Y SE ABRE CON TIMEOUT CORTO. La base de un centro puede estar apagada; el
/// usuario no tiene por que esperar los 15 segundos por omision para que le
/// digan que ese centro no esta.
/// </remarks>
public class IdentidadDeEmpresaRepository : IIdentidadDeEmpresaRepository
{
    private const string ClaveMatriz = "identidad-empresa:matriz";
    private const string ClaveBaseMatriz = "identidad-empresa:base-matriz";
    private const string PrefijoClaveBase = "identidad-empresa:base:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>Segundos para abrir la base de un centro que puede estar apagada.</summary>
    private const int SegundosParaConectar = 5;
    private const int SegundosDeComando = 10;

    /*
      LAS TRES COLUMNAS QUE DICEN DE QUIEN ES UNA BASE. Un SELECT TOP 1 que no
      escribe nada: ver IdentidadDeEmpresa sobre por que NO se usa
      sp_n_ConsultaConstantes.
    */
    private const string SqlIdentidad = @"
SELECT TOP 1
    LTRIM(RTRIM(ISNULL(FacturasCorreoNombre, '')))  AS NombreComercial,
    UPPER(LTRIM(RTRIM(ISNULL(Funcionalidad, ''))))  AS Funcionalidad,
    ISNULL(EsCentroServicio, 0)                     AS EsCentroServicio,
    DB_NAME()                                       AS BaseDatos
FROM dbo.Constantes;";

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public IdentidadDeEmpresaRepository(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<IdentidadDeEmpresa> DeLaMatrizAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<IdentidadDeEmpresa>(ClaveMatriz, out var cacheada) && cacheada is not null)
            return cacheada;

        var (identidad, baseDatos) = await LeerAsync(CadenaDeLaEmpresa.De(_configuration), ct);
        _cache.Set(ClaveMatriz, identidad, Ttl);
        _cache.Set(ClaveBaseMatriz, baseDatos, Ttl);
        return identidad;
    }

    public async Task<string> BaseDeLaMatrizAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<string>(ClaveBaseMatriz, out var cacheada) && cacheada is not null)
            return cacheada;

        await DeLaMatrizAsync(ct);
        return _cache.TryGetValue<string>(ClaveBaseMatriz, out var leida) && leida is not null ? leida : "";
    }

    public async Task<IdentidadDeEmpresa> DeLaBaseAsync(string nombreDeLaBase, CancellationToken ct = default)
    {
        /* El nombre ya viene del catalogo, nunca del cliente; aun asi se valida,
           por el mismo motivo que lo valida BaseDelCentro: una lista blanca con
           una comprobacion de mas no estorba. */
        if (!BaseDelCentro.EsNombreDeBaseValido(nombreDeLaBase))
            return IdentidadDeEmpresa.Desconocida;

        var clave = PrefijoClaveBase + nombreDeLaBase.Trim().ToLowerInvariant();
        if (_cache.TryGetValue<IdentidadDeEmpresa>(clave, out var cacheada) && cacheada is not null)
            return cacheada;

        var cadena = BaseDelCentro.Apuntar(CadenaDeLaEmpresa.De(_configuration), nombreDeLaBase);
        var (identidad, _) = await LeerAsync(cadena, ct);

        // Tambien se cachea la Desconocida: ver el remarks de la clase.
        _cache.Set(clave, identidad, Ttl);
        return identidad;
    }

    private static async Task<(IdentidadDeEmpresa Identidad, string BaseDatos)> LeerAsync(
        string cadena, CancellationToken ct)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(cadena) { ConnectTimeout = SegundosParaConectar };
            await using var conn = new Mac3SqlServerConnector(builder.ConnectionString).GetConnection;
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(SqlIdentidad, conn)
            {
                CommandType = CommandType.Text,
                CommandTimeout = SegundosDeComando
            };

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct))
                return (IdentidadDeEmpresa.Desconocida, "");

            var identidad = new IdentidadDeEmpresa(
                rd.IsDBNull(0) ? "" : rd.GetString(0).Trim(),
                rd.IsDBNull(1) ? "" : rd.GetString(1).Trim(),
                !rd.IsDBNull(2) && Convert.ToInt32(rd.GetValue(2)) == 1);

            var baseDatos = rd.IsDBNull(3) ? "" : rd.GetString(3).Trim();
            return (identidad, baseDatos);
        }
        catch (SqlException)
        {
            /* FALLA CERRADO. La base no esta, no hay permiso, o no tiene
               Constantes: no se pudo probar de quien es, asi que no es de nadie.
               No se propaga la excepcion porque esto se llama tambien al ARMAR
               EL CATALOGO: un centro apagado tiene que desaparecer de la lista,
               no tumbar la pantalla de los otros once. */
            return (IdentidadDeEmpresa.Desconocida, "");
        }
    }
}
