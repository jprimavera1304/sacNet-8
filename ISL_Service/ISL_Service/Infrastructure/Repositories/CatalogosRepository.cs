using System.Data;
using ISL_Service.Application.DTOs.Catalogos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Catalogo de solo lectura. Consulta [Catalogo TiposUsados] con IDStatus = 1.
/// </summary>
public class CatalogosRepository : ICatalogosRepository
{
    private readonly IConfiguration _configuration;

    // El cache y el registro son para FiltroDeAgente: a que agente esta amarrado
    // un usuario no cambia en caliente y no queremos un viaje a la base cada vez
    // que se pinta un desplegable.
    private readonly IMemoryCache _cache;
    private readonly ILogger<CatalogosRepository> _logger;

    public CatalogosRepository(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<CatalogosRepository> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
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

    public async Task<List<RepartidorItemDto>> ListRepartidoresAsync(int? idStatus, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          Se agregan estatus y zona, que son dos de las tres columnas de la
          pantalla CONSULTA DE REPARTIDORES de Mac31, y el filtro por nombre.

          El texto del estatus sale de [Catalogo Status], igual que en los
          sp_n_Consulta*: no se arma con un CASE aqui para que diga exactamente
          lo que dice Mac31 el dia que alguien agregue un tercer estatus. Es LEFT
          JOIN y no INNER: un repartidor con IDStatus en nulo o invalido tiene que
          seguir saliendo en la lista -desaparecer del catalogo es peor que salir
          sin etiqueta-, y el INNER lo borraria del desplegable de ventas.

          El nombre se busca con LIKE 'texto%' ("empieza con"), que es lo que hace
          sp_n_ConsultaRepartidores.
        */
        const string sql = @"
SELECT R.IDRepartidor,
       ISNULL(R.Repartidor, N'') AS Repartidor,
       ISNULL(R.IDStatus, 0)     AS IDStatus,
       ISNULL(R.IDZona, 0)       AS IDZona,
       CONVERT(varchar(30), ISNULL(R.IDZona, 0)) AS Zona,
       ISNULL(S.Status, N'')     AS Status
FROM [Catalogo Repartidores] R
    LEFT JOIN [Catalogo Status] S ON S.IDStatus = R.IDStatus
WHERE (@IdStatus IS NULL OR R.IDStatus = @IdStatus)
  AND (@Texto IS NULL OR R.Repartidor LIKE @Texto + N'%')
ORDER BY R.Repartidor";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());

        var list = new List<RepartidorItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RepartidorItemDto
            {
                IdRepartidor = reader.GetInt32(0),
                Repartidor = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IdStatus = Convert.ToInt32(reader.GetValue(2)),
                IdZona = Convert.ToInt32(reader.GetValue(3)),
                Zona = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Status = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }
        return list;
    }

    public async Task<List<TarimaCatalogItemDto>> ListTarimasAsync(int? idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Compatibilidad: si el esquema no tiene dbo.WTarima (version legacy/removida),
        // el catálogo de tarimas se considera opcional y regresa vacío.
        await using (var existsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.WTarima', 'U')", conn))
        {
            var tableId = await existsCmd.ExecuteScalarAsync(ct);
            if (tableId == null || tableId == DBNull.Value)
                return new List<TarimaCatalogItemDto>();
        }

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

    /*
      ESTE DESPLEGABLE PUBLICABA EL PADRON COMPLETO. ARREGLADO.
      =======================================================

      Como estaba: sin filtro por agente. Un usuario ADMIN amarrado a UN agente
      veia los 1,042 clientes de los 15 agentes de la empresa, porque este SELECT
      no sabia nada del agente de quien preguntaba. Y el endpoint tampoco pedia
      ningun permiso `perm:` (solo el [Authorize] de la clase), asi que cualquier
      usuario autenticado podia pedirlo. Las dos cosas se arreglaron: el permiso
      en CatalogosController, el filtro aqui.

      El filtro se resuelve con la MISMA clase que GET api/clientes, el MISMO
      cache y el MISMO fallo cerrado: si no se puede saber a que agente pertenece
      el usuario, esto NO contesta (ConflictException -> 409). No se degrada a 0,
      porque 0 significa "ve todos" y degradar a 0 no quitaria informacion:
      apagaria el filtro.

      LO QUE NO CAMBIA: la FORMA de la respuesta. Sigue siendo la lista desnuda,
      sin el sobre { ok, message, data }. El front viejo ya la consume asi y
      cambiarla lo rompe; el sobre es para los endpoints nuevos.
    */
    public async Task<List<ClienteCatalogItemDto>> ListClientesAsync(int? idStatus, int idUsuario, CancellationToken ct = default)
    {
        var idAgente = await FiltroDeAgente.ResolverAsync(GetConnection, _cache, _logger, idUsuario, ct);

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
  AND (@IDAgente = 0 OR IDAgente = @IDAgente)
ORDER BY Nombre, Numero";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);
        // 0 = este usuario ve todos los clientes: el AND se apaga con el propio
        // parametro en vez de armar dos cadenas de SQL distintas.
        cmd.Parameters.AddWithValue("@IDAgente", idAgente);

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

    public async Task<List<BancoCatalogItemDto>> ListBancosAsync(int? idStatus, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /* Igual que repartidores: se agrega el TEXTO del estatus, que es la segunda
           columna de la pantalla CONSULTA DE BANCOS, y el filtro por nombre. */
        const string sql = @"
SELECT
    B.IDBanco,
    ISNULL(B.IDStatus, 0) AS IDStatus,
    ISNULL(B.Banco, N'')  AS Banco,
    ISNULL(S.Status, N'') AS Status
FROM dbo.[Catalogo Bancos] B
    LEFT JOIN [Catalogo Status] S ON S.IDStatus = B.IDStatus
WHERE (@IdStatus IS NULL OR B.IDStatus = @IdStatus)
  AND (@Texto IS NULL OR B.Banco LIKE @Texto + N'%')
ORDER BY B.Banco";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdStatus", (object?)idStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Texto", string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim());

        var list = new List<BancoCatalogItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new BancoCatalogItemDto
            {
                IDBanco = Convert.ToInt32(reader.GetValue(0)),
                IDStatus = Convert.ToInt32(reader.GetValue(1)),
                Banco = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Status = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return list;
    }
}
