using System.Data;
using ISL_Service.Application.DTOs.Catalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Catalogo de solo lectura. Consulta [Catalogo TiposUsados] con IDStatus = 1.
/// </summary>
public class CatalogosRepository : ICatalogosRepository
{
    private readonly IConfiguration _configuration;

    public CatalogosRepository(IConfiguration configuration)
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
            throw new InvalidOperationException("ConnectionString no encontrada.");
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    public async Task<List<TipoCascoItemDto>> ListTiposCascoAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT IdTipoUsado AS IdTipoCasco, 
       ISNULL([Tipo de Usado], N'') AS Descripcion
FROM [Catalogo TiposUsados]
WHERE (@IdStatus IS NULL OR IDStatus = @IdStatus)
ORDER BY [Tipo de Usado]";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);

        var list = new List<TipoCascoItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TipoCascoItemDto
            {
                IdTipoCasco = reader.GetInt32(0),
                Descripcion = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<RepartidorItemDto>> ListRepartidoresAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT IDRepartidor, ISNULL(Repartidor, N'') AS Repartidor
FROM [Catalogo Repartidores]
WHERE (@IdStatus IS NULL OR IDStatus = @IdStatus)
ORDER BY Repartidor";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);

        var list = new List<RepartidorItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RepartidorItemDto
            {
                IdRepartidor = reader.GetInt32(0),
                Repartidor = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<List<TarimaCatalogItemDto>> ListTarimasAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT IdTarima, ISNULL(NombreTarima, N'') AS NombreTarima, IdTipoCasco, NumeroCascosBase
FROM dbo.WTarima
WHERE (@IdStatus IS NULL OR IdStatus = @IdStatus)
ORDER BY NombreTarima";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);

        var list = new List<TarimaCatalogItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TarimaCatalogItemDto
            {
                IdTarima = reader.GetInt32(0),
                NombreTarima = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IdTipoCasco = reader.GetInt32(2),
                NumeroCascosBase = reader.GetInt32(3)
            });
        }
        return list;
    }

    public async Task<List<ClienteCatalogItemDto>> ListClientesAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT 
    IDCliente,
    IDStatus,
    ISNULL(CONVERT(NVARCHAR(50), Numero), N'') AS Numero,
    ISNULL(Nombre, N'') AS Nombre
FROM dbo.Clientes
WHERE (@IdStatus IS NULL OR IDStatus = @IdStatus)
ORDER BY Nombre, Numero";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);

        var list = new List<ClienteCatalogItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ClienteCatalogItemDto
            {
                IDCliente = Convert.ToInt32(reader.GetValue(0)),
                IDStatus = Convert.ToInt32(reader.GetValue(1)),
                Numero = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Nombre = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return list;
    }

    public async Task<List<BancoCatalogItemDto>> ListBancosAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT 
    IDBanco,
    IDStatus,
    ISNULL(Banco, N'') AS Banco
FROM dbo.[Catalogo Bancos]
WHERE (@IdStatus IS NULL OR IDStatus = @IdStatus)
ORDER BY Banco";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);

        var list = new List<BancoCatalogItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new BancoCatalogItemDto
            {
                IDBanco = Convert.ToInt32(reader.GetValue(0)),
                IDStatus = Convert.ToInt32(reader.GetValue(1)),
                Banco = reader.IsDBNull(2) ? "" : reader.GetString(2)
            });
        }
        return list;
    }
}
