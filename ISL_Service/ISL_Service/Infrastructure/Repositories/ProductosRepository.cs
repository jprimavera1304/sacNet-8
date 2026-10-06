using System.Data;
using ISL_Service.Application.DTOs.Productos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// `sp_n_ConsultaProductos`, el catalogo de productos con precios y existencias.
///
/// ES LENTO Y HAY QUE SABERLO: el SP usa cursores anidados cuando se pide la
/// lista Roshfrans (productos x porcentajes de descuento). Por eso el comando
/// lleva timeout explicito en vez del default de 30 s.
///
/// @Formato = 1 NO SE MANDA NUNCA, Y NO ES UNA DECISION DE DISENO: esta roto en
/// la base. El SP concatena un segundo SELECT despues de la lista de columnas
/// del primero (`INSERT INTO #tmp SELECT &lt;46 columnas&gt; SELECT P.IDProducto,
/// ...`), asi que con 1 el EXEC falla con error de sintaxis. El "Excel de
/// productos" de Mac31 lleva anos sin funcionar por este endpoint; aqui el Excel
/// lo arma el front con los mismos datos.
/// </summary>
public class ProductosRepository : IProductosRepository
{
    private const string SpConsultar = "dbo.sp_n_ConsultaProductos";
    private const int FormatoTodosLosCampos = 0;

    /// <summary>
    /// Dos minutos. Medido con los cursores anidados de la lista Roshfrans: el
    /// default de 30 s se queda corto y el error que da es un timeout seco que
    /// no dice que el problema son los cursores.
    /// </summary>
    private const int CommandTimeoutSeconds = 120;

    private readonly IConfiguration _configuration;

    public ProductosRepository(IConfiguration configuration)
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
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }

    public async Task<List<ProductoDto>> ConsultarAsync(ProductosFiltroSp filtro, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };

        cmd.Parameters.AddWithValue("@IDProducto", filtro.IDProducto);
        cmd.Parameters.AddWithValue("@IDsMarca", filtro.IDsMarca);
        cmd.Parameters.AddWithValue("@IDsCategoria", filtro.IDsCategoria);
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", filtro.IDsGrupoCategoria);
        cmd.Parameters.AddWithValue("@IDStatus", filtro.IDStatus);
        cmd.Parameters.AddWithValue("@Clave", filtro.Clave);
        cmd.Parameters.AddWithValue("@Descripcion", filtro.Descripcion);
        cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        cmd.Parameters.AddWithValue("@Identico", filtro.Exacto ? 1 : 0);
        cmd.Parameters.AddWithValue("@ListaRoshfrans", filtro.ListaRoshfrans ? 1 : 0);
        cmd.Parameters.AddWithValue("@EsCarga", filtro.EsCarga ? 1 : 0);
        cmd.Parameters.AddWithValue("@PorcentajesDescuento", filtro.PorcentajesDescuento);

        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        var lista = Funciones.DataTableToList<ProductoDto>(dt);

        /*
          DOS COLUMNAS SE LEEN A MANO PORQUE SE PUBLICAN CON OTRO NOMBRE.

          El SP las llama `PrecioListaConIvaRnd` y `PrecioRoshfransConIvaRnd`, y
          ese "Rnd" es mentira: no redondea nada, multiplica por el IVA y ya
          (`[Precio de Lista] * (1 + Constantes.IVA/100)`). Publicar el nombre
          con "Rnd" le diria al front que el numero ya viene redondeado y que
          puede imprimirlo tal cual; no viene. Se publican como
          `precioListaConIva` y `precioRoshfransConIva`, y por eso
          DataTableToList —que empareja por nombre exacto— no las alcanza.
        */
        for (int i = 0; i < lista.Count; i++)
        {
            var row = dt.Rows[i];
            lista[i].PrecioListaConIva = LeerDecimal(row, "PrecioListaConIvaRnd");
            lista[i].PrecioRoshfransConIva = LeerDecimal(row, "PrecioRoshfransConIvaRnd");
        }

        return lista;
    }

    private static decimal LeerDecimal(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna) || row[columna] == DBNull.Value) return 0m;
        return Convert.ToDecimal(row[columna]);
    }
}
