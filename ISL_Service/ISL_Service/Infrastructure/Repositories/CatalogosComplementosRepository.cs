using System.Data;
using ISL_Service.Application.DTOs.CatalogosComplementos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los `sp_n_Consulta*` de los siete catalogos complementarios.
///
/// NINGUNO PASA POR sp_n_ConsultaConstantes, Y ESO ES LO QUE LOS HACE MIGRABLES
/// ---------------------------------------------------------------------------
/// `sp_n_ConsultaConstantes` no es una consulta: cuando la fecha de operacion
/// esta atrasada hace el cierre del dia (inserta en FechaOperacionLog, borra y
/// rehace AlmacenProductoHist y adelanta Constantes.FechaOperacion), y en toda
/// llamada registra actividad del usuario. Los endpoints de legacy que lo
/// llaman son escrituras disfrazadas y quedaron fuera de esta entrega. Estos
/// siete no lo llaman: son lecturas de verdad.
///
/// TRES DETALLES DEL LADO SQL QUE HAY QUE CONOCER ANTES DE TOCAR ESTO
/// -----------------------------------------------------------------
/// 1. Todos arman el WHERE concatenando y lo corren con EXEC(@Sql), asi que el
///    apostrofo se quita en el service: el SqlParameter no protege adentro.
/// 2. Los tres `sp_n_ConsultaCatFE*` reciben @IDStatus y NO lo usan: la linea
///    con fn_RegresaTablaStatus esta comentada y el WHERE esta clavado a
///    `IDStatus = 1`. Por eso aqui no se expone el filtro: ofrecerlo seria
///    mentir sobre lo que hace.
/// 3. `sp_n_ConsultaMarcas` no acepta @IDMarca, solo @IDsMarca (una cadena
///    separada por `~`). Legacy resolvia el caso de un solo id convirtiendolo a
///    texto (MarcaService.cs:116-119); aqui se hace lo mismo en el service.
/// </summary>
public class CatalogosComplementosRepository : ICatalogosComplementosRepository
{
    private const string SpTiposDomicilio = "dbo.sp_n_Consultar_TiposDomicilio";
    private const string SpCondicionesCredito = "dbo.sp_n_ConsultaCondicionesCredito";
    private const string SpEmpresasSeries = "dbo.sp_n_ConsultaEmpresasSeries";
    private const string SpFeFormasPago = "dbo.sp_n_ConsultaCatFEFormasPago";
    private const string SpFeMetodosPago = "dbo.sp_n_ConsultaCatFEMetodosPago";
    private const string SpFeMotivosCancelacion = "dbo.sp_n_ConsultaCatFEMotivosCancelacion";
    private const string SpMarcas = "dbo.sp_n_ConsultaMarcas";

    /// <summary>0 = todos los campos. El 1 es el "Excel" de Mac31, que devuelve
    /// otras columnas con los encabezados en espanol; no se expone.</summary>
    private const int FormatoTodosLosCampos = 0;

    private readonly IConfiguration _configuration;

    public CatalogosComplementosRepository(IConfiguration configuration)
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

    private async Task<DataTable> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure };
        parametros(cmd);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);
        return dt;
    }

    public async Task<List<TipoDomicilioDto>> ConsultarTiposDomicilioAsync(int id, int idStatus, string texto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpTiposDomicilio, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoDomicilio", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@TipoDomicilio", texto);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
            // El SP recibe @Identico y nunca lo mira: su filtro es siempre
            // LIKE %texto%. Se manda 0 para no cambiar la firma de la llamada.
            cmd.Parameters.AddWithValue("@Identico", 0);
        }, ct);

        return Funciones.DataTableToList<TipoDomicilioDto>(dt);
    }

    public async Task<List<CondicionCreditoDto>> ConsultarCondicionesCreditoAsync(
        int id, int idStatus, string texto, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpCondicionesCredito, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCondicionCredito", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@CondicionCredito", texto);
            // @DiasCredito existe en el SP y no participa en ningun filtro.
            // Legacy no lo mandaba; aqui tampoco, para no dar a entender que
            // se puede buscar por plazo.
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        }, ct);

        return Funciones.DataTableToList<CondicionCreditoDto>(dt);
    }

    public async Task<List<EmpresaSerieDto>> ConsultarEmpresasSeriesAsync(
        int id, int idEmpresa, int idStatus, string texto, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpEmpresasSeries, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresaSerie", id);
            cmd.Parameters.AddWithValue("@IDEmpresa", idEmpresa);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Nombre", texto);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
        }, ct);

        return Funciones.DataTableToList<EmpresaSerieDto>(dt);
    }

    public async Task<List<CatalogoFeDto>> ConsultarCatalogoFeAsync(
        CatalogoFe cual, int id, string clave, bool exacto, CancellationToken ct)
    {
        var (sp, paramId, colId, colTexto) = cual switch
        {
            CatalogoFe.FormasPago => (SpFeFormasPago, "@IDFEFormaPago", "IDFEFormaPago", "FEFormaPago"),
            CatalogoFe.MetodosPago => (SpFeMetodosPago, "@IDFEMetodoPago", "IDFEMetodoPago", "FEMetodoPago"),
            CatalogoFe.MotivosCancelacion => (SpFeMotivosCancelacion, "@IDFEMotivoCancelacion", "IDFEMotivoCancelacion", "FEMotivo"),
            _ => throw new ArgumentOutOfRangeException(nameof(cual))
        };

        var dt = await EjecutarAsync(sp, cmd =>
        {
            cmd.Parameters.AddWithValue(paramId, id);
            // Se manda 0 porque es lo que mandaba legacy, pero el SP lo ignora:
            // su WHERE esta clavado a IDStatus = 1.
            cmd.Parameters.AddWithValue("@IDStatus", 0);
            cmd.Parameters.AddWithValue("@FEClave", clave);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
        }, ct);

        var lista = new List<CatalogoFeDto>(dt.Rows.Count);
        foreach (DataRow row in dt.Rows)
        {
            lista.Add(new CatalogoFeDto
            {
                Id = LeerInt(row, colId),
                IDStatus = LeerInt(row, "IDStatus"),
                FEClave = LeerTexto(row, "FEClave"),
                Descripcion = LeerTexto(row, colTexto),
                Status = LeerTexto(row, "Status")
            });
        }
        return lista;
    }

    public async Task<List<MarcaCatalogoDto>> ConsultarMarcasAsync(
        string idsMarca, int idStatus, string texto, bool exacto, CancellationToken ct)
    {
        var dt = await EjecutarAsync(SpMarcas, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsMarca", idsMarca);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Marca", texto);
            cmd.Parameters.AddWithValue("@Identico", exacto ? 1 : 0);
            cmd.Parameters.AddWithValue("@Formato", FormatoTodosLosCampos);
        }, ct);

        return Funciones.DataTableToList<MarcaCatalogoDto>(dt);
    }

    private static int LeerInt(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna) || row[columna] == DBNull.Value) return 0;
        return Convert.ToInt32(row[columna]);
    }

    private static string LeerTexto(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna) || row[columna] == DBNull.Value) return string.Empty;
        // Varias de estas columnas son char(n) y traen el relleno dentro. El
        // relleno es del tipo de la columna, no un dato.
        return Convert.ToString(row[columna])?.Trim() ?? string.Empty;
    }
}
