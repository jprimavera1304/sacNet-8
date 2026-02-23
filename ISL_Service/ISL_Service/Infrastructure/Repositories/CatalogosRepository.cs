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
WHERE IDStatus = 1
ORDER BY [Tipo de Usado]";
        await using var cmd = new SqlCommand(sql, conn);

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
}
