using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Escrituras de las tres fichas maestras de Mac31: cliente, producto y
/// proveedor. Llama a los sp_n_Insertar*/sp_n_Actualizar* tal cual, sin sp_w_ de
/// por medio: no hay nada que agregarles y cada capa extra es un lugar donde se
/// pierde el par result/mensaje del que dependen los avisos de la pantalla
/// ("YA EXISTE UN CLIENTE CON ESE RFC").
///
/// Las bajas van por <see cref="BajaLogicaLegacy"/>, que lee los valores
/// actuales del renglon antes de devolverselos al procedimiento. Sin eso, la
/// baja de un cliente lo deja sin nombre y sin telefonos, con las ventas viejas
/// apuntando a un renglon vaciado.
/// </summary>
public partial class MaestrosRepository : IMaestrosRepository
{
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public MaestrosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // -----------------------------------------------------------------------
    // Infraestructura compartida
    // -----------------------------------------------------------------------

    private async Task<ResultadoLegacy> EjecutarAsync(
        string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return BajaLogicaLegacy.LeerResultado(dt);
    }

    /// <summary>
    /// Igual, leyendo tambien el id que el procedimiento devuelve en una columna
    /// propia (IDResult, IDArchivo...).
    /// </summary>
    private async Task<(ResultadoLegacy Resultado, int Id)> EjecutarConIdAsync(
        string sp, string columnaId, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        var resultado = BajaLogicaLegacy.LeerResultado(dt);
        var id = dt.Rows.Count > 0 ? BajaLogicaLegacy.LeerInt(dt.Rows[0], columnaId) : 0;
        return (resultado, id);
    }

    /// <summary>
    /// Alta cuyo procedimiento no devuelve el id: si salio bien, se lee el MAX de
    /// la llave para poder contestar con la ubicacion del renglon nuevo. Se lee
    /// DESPUES del exito, no reserva nada y no decide ningun valor.
    /// </summary>
    private async Task<(ResultadoLegacy, int)> InsertarConUltimoIdAsync(
        string sp, string tabla, string columnaId, Action<SqlCommand> parametros, CancellationToken ct)
    {
        var resultado = await EjecutarAsync(sp, parametros, ct);
        if (!resultado.Ok) return (resultado, 0);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"SELECT ISNULL(MAX([{columnaId}]), 0) FROM dbo.{tabla}", conn);
        var valor = await cmd.ExecuteScalarAsync(ct);
        var id = valor is null or DBNull ? 0 : Convert.ToInt32(valor, CultureInfo.InvariantCulture);
        return (resultado, id);
    }

    private async Task<ResultadoLegacy?> CambiarStatusAsync(
        BajaLogicaLegacy.Descriptor def, int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        return await BajaLogicaLegacy.CambiarAsync(conn, def, id, idStatus, idUsuario, equipo, ct);
    }

    /// <summary>
    /// Texto para legacy: recortado, nunca nulo, CON el apostrofo.
    ///
    /// Legacy hacia Replace("'","") campo por campo y O'BRIEN quedaba OBRIEN.
    /// En estas escrituras el valor va como SqlParameter a un INSERT/UPDATE
    /// normal: no se concatena en ningun EXEC(@Sql), asi que no hay nada de lo
    /// que protegerse. La UNICA excepcion de todo este repositorio es
    /// sp_n_ActualizarClienteFacturacion, que si arma el UPDATE concatenando;
    /// ahi se usa <see cref="TextoParaSqlDinamico"/>.
    /// </summary>
    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

    /// <summary>
    /// Texto para un procedimiento que CONCATENA el valor dentro de un
    /// EXEC(@Sql).
    ///
    /// sp_n_ActualizarClienteFacturacion arma su UPDATE pegando las cadenas con
    /// comillas simples alrededor, asi que un apostrofo en la razon social
    /// rompe la sentencia. Se duplica, que es la forma correcta de escaparlo en
    /// T-SQL: asi "O'BRIEN S.A." se guarda como "O'BRIEN S.A." y no como
    /// "OBRIEN S.A." ni rompe nada. Legacy lo hacia solo en este endpoint y por
    /// esta razon; aqui queda escrito por que, para que nadie lo "limpie"
    /// pensando que es basura.
    /// </summary>
    private static string TextoParaSqlDinamico(string? valor)
        => (valor ?? string.Empty).Trim().Replace("'", "''");

    /// <summary>
    /// Une una lista con el delimitador '~' que esperan los sp_n_ que reciben
    /// varios valores en un solo parametro.
    ///
    /// Los numeros se escriben con cultura INVARIANTE a proposito. Legacy usaba
    /// string.Join con la cultura del servidor: en un servidor en espanol, un
    /// 12.5 se convierte en "12,5" y el procedimiento, que parte por comas en
    /// algunas ramas, lee dos valores donde habia uno. Es un bug de dinero que
    /// solo aparece segun la configuracion regional de la maquina.
    /// </summary>
    private static string Unir(IEnumerable<string> valores) => string.Join("~", valores);

    private static string Numero(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static SqlCommand Sp(string sp, SqlConnection conn)
        => new(sp, conn) { CommandType = CommandType.StoredProcedure };

    private static async Task<DataTable> FillAsync(SqlCommand cmd, CancellationToken ct)
    {
        var ds = new DataSet();
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds), ct);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
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
