using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Operacion diaria de Mac31: compras, inventario en transito, cortes de caja,
/// transferencias bancarias y cambios de vigencia. Llama a los sp_n_ tal cual.
///
/// DOS DEPENDENCIAS DE LEGACY QUE AQUI NO SE REPITEN
/// -------------------------------------------------
/// 1. sp_n_ConsultaConstantes. Legacy lo llamaba antes de casi cada una de estas
///    operaciones, SOLO para leer la columna formatoFechaDB y saber en que orden
///    escribir las fechas. Dos consecuencias malas: ese procedimiento es la
///    rutina de CIERRE DEL DIA (si la fecha de operacion esta atrasada, mueve la
///    fecha del negocio y rehace el corte de inventario), y ademas si fallaba, la
///    operacion fallaba. En cancelar y regresar un cambio de vigencia lo llamaba
///    y tiraba el resultado sin leerlo: un viaje a la base que solo podia hacer
///    fallar la operacion. Aqui las fechas se formatean en el backend con el
///    formato que esas columnas tienen ('MM-dd-yyyy') y no se consulta nada.
/// 2. Funciones.ConvertFtmDate, que invertia dia y mes con Substring de posicion
///    fija: con una cadena vacia o de menos de diez caracteres lanzaba
///    ArgumentOutOfRange, y varios endpoints la llamaban SIN comprobar que la
///    fecha viniera. Aqui las fechas son DateTime y no hay cadena que recortar.
/// </summary>
public partial class OperacionMacRepository : IOperacionMacRepository
{
    /// <summary>
    /// El formato en el que legacy guarda las fechas que recibe como texto
    /// (Constantes.FormatoFecha). Es el mismo constante que ya usan
    /// EmpleadosRepository y PrestamosRepository.
    /// </summary>
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public OperacionMacRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // -----------------------------------------------------------------------
    // Infraestructura
    // -----------------------------------------------------------------------

    private async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

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

    // -----------------------------------------------------------------------
    // Ayudantes de formato
    // -----------------------------------------------------------------------

    /// <summary>Fecha -> 'MM-dd-yyyy'. Sin fecha, cadena vacia.</summary>
    private static string AFechaLegacy(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Inicio de un rango: la fecha a las 00:00:00, como la espera el
    /// procedimiento. Sin fecha, cadena vacia, que es lo que legacy entiende por
    /// "sin filtro".
    /// </summary>
    private static string ADesde(DateTime? fecha)
        => fecha.HasValue ? $"{AFechaLegacy(fecha)} 00:00:00" : string.Empty;

    /// <summary>
    /// Fin de un rango: la fecha a las 23:59:59. Se escribe asi y no como
    /// "menor que el dia siguiente" porque es lo que hacen los sp_n_ y cambiarlo
    /// dejaria fuera los movimientos del ultimo segundo del dia en el web y
    /// dentro en Mac31, con dos totales distintos para el mismo corte.
    /// </summary>
    private static string AHasta(DateTime? fecha)
        => fecha.HasValue ? $"{AFechaLegacy(fecha)} 23:59:59" : string.Empty;

    /// <summary>
    /// Texto para legacy: recortado y nunca nulo. CON el apostrofo: estos valores
    /// van como SqlParameter a procedimientos que los guardan, no los concatenan.
    /// </summary>
    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

    /// <summary>
    /// Numero con cultura INVARIANTE. Legacy usaba .ToString() con la cultura del
    /// servidor: donde el separador decimal es la coma, un 12.5 se vuelve "12,5"
    /// y el procedimiento, que parte cadenas, lee dos valores donde habia uno.
    /// </summary>
    private static string Numero(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Entero(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Un valor que el procedimiento va a convertir a numero.
    ///
    /// sp_n_InsertarBancoTransferencia hace CAST( @RefNumerica as decimal(22,0) )
    /// y lo mismo con @Autorizacion. Con la cadena vacia eso falla con "Error
    /// converting data type varchar to numeric" y tumba la carga del estado de
    /// cuenta completo — comprobado. Vacio se traduce a "0", que es lo que la
    /// columna numerica puede guardar y lo que significa "no hay".
    ///
    /// Que el valor sea de verdad numerico lo valida el controlador: aqui no se
    /// adivina, porque convertir una autorizacion con letras en 0 seria perder el
    /// dato en silencio.
    /// </summary>
    private static string Numerico(string? valor)
    {
        var limpio = Texto(valor);
        return limpio.Length == 0 ? "0" : limpio;
    }

    private static string Unir(IEnumerable<string> valores) => string.Join("~", valores);

    /// <summary>
    /// Arma el CorteInfo. La construccion vive en <see cref="CorteInfoLegacy"/>,
    /// que es publica para poder probar el formato sin base de datos: es la pieza
    /// mas fragil del modulo (veinte importes en una cadena que el procedimiento
    /// vuelve a partir con CHARINDEX) y la unica que se puede probar sola.
    /// </summary>
    private static string ArmarCorteInfo(DesgloseCorte d) => CorteInfoLegacy.Armar(d);

    private static SqlCommand Sp(string sp, SqlConnection conn)
        => new(sp, conn) { CommandType = CommandType.StoredProcedure };

    private static SqlCommand Sp(string sp, SqlConnection conn, SqlTransaction tx)
        => new(sp, conn, tx) { CommandType = CommandType.StoredProcedure };

    /*
      NO CONVERTIR ESTO A ExecuteReaderAsync(ct) SIN LEER ESTO PRIMERO.

      Parece un bloqueo de hilo que conviene volver asincrono, y lo es: el Fill
      es sincrono y retiene un hilo del ThreadPool durante toda la consulta. Pero
      el `ct` de Task.Run solo impide que el trabajo ARRANQUE; una vez empezado,
      la cancelacion se ignora y el lote llega siempre a su COMMIT. Eso es lo que
      hoy protege a las escrituras de este repositorio.

      Al convertirlo tal cual en captura, la cancelacion se volvio REAL y
      empezo a abortar lotes a media transaccion: con XACT_ABORT OFF eso no
      deshace nada y la conexion vuelve al pool con la transaccion abierta y los
      candados puestos. Costo un dia encontrarlo.

      Si algun dia hace falta volverlo asincrono, hay que pasar la señal de
      Infrastructure.Data.CancelacionSegura en vez del token, que es la que
      distingue consulta de escritura.
    */
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
