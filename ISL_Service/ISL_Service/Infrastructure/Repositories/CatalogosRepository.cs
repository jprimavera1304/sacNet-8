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

    /*
      LOS USUARIOS DE MAC31, PARA ELEGIR UNA PERSONA.

      VA POR EL PROCEDIMIENTO Y NO POR SQL PROPIO. sp_n_ConsultaUsuarios es el que
      usa Mac31 y el que ya se llama en ReportesVentasRepository.Catalogos.cs:88;
      escribir aqui un SELECT contra dbo.Usuarios seria una segunda definicion de
      "quien sale en la lista" (hoy: el filtro de estatus pasa por
      fn_RegresaTablaStatus, no por un IDStatus = n), y el dia que cambie una de
      las dos habria dos verdades.

      LAS COLUMNAS SE LEEN POR NOMBRE Y SON SOLO TRES. El procedimiento arma su
      SELECT con SQL dinamico, asi que el orden de las columnas no es un contrato;
      y de las quince que devuelve, tres son contrasenas en claro (`Contrasena`,
      `ContrasenaDesc`, `ContrasenaAutorizacion`). Se piden las tres que hacen
      falta por su nombre: lo que no se nombra no puede salir ni por accidente, ni
      ahora ni cuando alguien le agregue una columna al procedimiento.

      @IDStatus = 0 ES "TODOS" y no "ninguno": es lo que hace
      fn_RegresaTablaStatus dentro del procedimiento, igual que en el resto de los
      sp_n_Consulta*. Por eso un idStatus nulo entra como 0 y no se traduce a
      nada mas.
    */
    public async Task<List<UsuarioMacItemDto>> ListUsuariosMac31Async(
        int? idStatus, string? texto, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand("dbo.sp_n_ConsultaUsuarios", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@IDPerfil", 0);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus ?? 0);
        cmd.Parameters.AddWithValue("@Usuario", (texto ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@Identico", 0);
        // 1 esta ROTO en la base: con @Formato = 1 el procedimiento concatena un
        // segundo SELECT sin cerrar el primero. 0 es el unico que se puede pedir.
        cmd.Parameters.AddWithValue("@Formato", 0);

        var tabla = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            tabla.Load(reader);
        }

        var list = new List<UsuarioMacItemDto>();
        foreach (DataRow fila in tabla.Rows)
        {
            var item = new UsuarioMacItemDto
            {
                IdUsuario = Entero(fila, "IDUsuario"),
                Usuario = Celda(fila, "Usuario"),
                Nombre = Celda(fila, "Nombre")
            };

            // Un usuario sin id o sin clave no se puede elegir: seria un renglon
            // que al guardarse deja IDUsuarioRecibe en 0, que es justo lo que este
            // endpoint existe para evitar.
            if (item.IdUsuario > 0 && item.Usuario.Length > 0)
                list.Add(item);
        }

        return list;
    }

    private static int Entero(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value) return 0;
        return int.TryParse(Convert.ToString(fila[columna]), out var valor) ? valor : 0;
    }

    private static string Celda(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value) return string.Empty;
        return (Convert.ToString(fila[columna]) ?? string.Empty).Trim();
    }
}
