using System.Data;
using ISL_Service.Application.DTOs.Nomina;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los tres batches de nomina: calcular los recibos de un periodo, generar los
/// periodos que falten y descontar los prestamos.
///
/// POR QUE ESTAN JUNTOS Y APARTE DE Empleados Y Prestamos
/// -----------------------------------------------------
/// Porque los tres son procesos, no edicion de fichas: borran y vuelven a
/// escribir renglones de varios empleados a la vez. PrestamosRepository
/// deliberadamente NO expone el batch semanal, y eso sigue siendo correcto para
/// esa pantalla: desde la ficha de un prestamo no se dispara el descuento de la
/// nomina de todos. Aqui si se expone, pero en su propio modulo, con su propio
/// permiso y con una ruta que dice lo que hace.
///
/// LO QUE NO SE PUDO ARREGLAR, Y SE DICE
/// ------------------------------------
/// sp_n_ConsultaEmpleadosTipoSueldoRecibo recupera sus filtros haciendo
/// INSERT ... EXEC sp_n_ConsultaParametros con el id de un renglon de la tabla
/// Parametros. Eso obliga a que la pantalla guarde los filtros antes y mande el
/// id, que es lo mismo que hacia legacy. Pasarle los datos directamente
/// significaria reescribir el procedimiento, y los sp_n_ no se tocan.
/// </summary>
public class NominaBatchRepository : INominaBatchRepository
{
    /// <summary>
    /// Los dos procedimientos del recibo. Cual se usa lo decide
    /// Constantes.AppMac: si contiene "MACT" es Tauro, y si no, es la version de
    /// Zaragoza. Es la misma regla de legacy, leida del mismo lugar.
    ///
    /// Que la version de Zaragoza sea el camino por omision no es un descuido:
    /// Tauro es la instalacion con el ejecutable MacT.exe y cualquier otra
    /// empresa cae en la variante Zara. Queda escrito porque una instalacion nueva
    /// con un AppMac distinto calcularia con las reglas de Zaragoza sin que nadie
    /// lo decidiera.
    /// </summary>
    private const string SpRecibosTauro = "dbo.sp_n_ConsultaEmpleadosTipoSueldoRecibo";
    private const string SpRecibosZara = "dbo.sp_n_ConsultaEmpleadosTipoSueldoReciboZara";

    private const string SpGenerarPeriodos = "dbo.sp_n_ValidaPeriodosTipoSueldo";
    private const string SpDescontarPrestamos = "dbo.sp_n_ValidaEmpleadosPrestamos";

    private readonly IConfiguration _configuration;

    public NominaBatchRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Cual de las dos variantes del recibo usa esta empresa.
    /// </summary>
    public async Task<bool> EsTauroAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // SELECT directo sobre Constantes y NO sp_n_ConsultaConstantes, por la
        // razon de siempre: ese procedimiento es la rutina de CIERRE DEL DIA (si la
        // fecha de operacion esta atrasada, la mueve y rehace el corte de
        // inventario). Llamarlo para leer una columna de texto es disparar el
        // cierre del dia de rebote. Legacy lo hacia.
        await using var cmd = new SqlCommand("SELECT TOP 1 ISNULL(AppMac, '') FROM dbo.Constantes", conn);
        var valor = await cmd.ExecuteScalarAsync(ct);
        var appMac = valor is null or DBNull ? string.Empty : Convert.ToString(valor) ?? string.Empty;
        return appMac.Contains("MACT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calcula los recibos del periodo. ESCRIBE: el procedimiento borra los
    /// recibos del periodo y los vuelve a insertar con un cursor.
    ///
    /// Se ejecuta UNA vez. Legacy lo corria DOS veces en la misma peticion —una
    /// para la nomina y otra para la comision, con el periodo siguiente calculado
    /// como "el de nomina mas uno"— y sin transaccion: si la segunda fallaba, los
    /// recibos de nomina ya estaban rehechos y los de comision no. Esa heuristica
    /// del "mas uno" tampoco se hereda: supone que el periodo de comision es
    /// siempre el consecutivo inmediato, y si no lo es, calcula la nomina de un
    /// periodo ajeno. Aqui cada periodo se pide por su id.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> CalcularRecibosAsync(
        CalcularRecibosRequest r, bool esTauro, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(esTauro ? SpRecibosTauro : SpRecibosZara, conn);
        cmd.Parameters.AddWithValue("@Params", r.IdParametros.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", r.IdPeriodoTipoSueldo);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

    /// <summary>
    /// Genera los periodos de nomina que falten. ESCRIBE.
    ///
    /// En legacy esto se disparaba en el LOGIN de Mac31: cualquiera que entrara los
    /// creaba, y dos personas entrando al mismo tiempo los creaban dos veces. Aqui
    /// es una accion explicita con su permiso.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GenerarPeriodosAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpGenerarPeriodos, conn);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

    /// <summary>
    /// Descuenta los prestamos de la nomina. ESCRIBE Y MUEVE DINERO: inserta los
    /// pagos en EmpleadoPrestamosPagos, baja el saldo y liquida los prestamos
    /// pagados. El procedimiento abre su propia transaccion.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> DescontarPrestamosAsync(int idUsuario, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpDescontarPrestamos, conn);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

    private static SqlCommand Sp(string sp, SqlConnection conn)
        => new(sp, conn) { CommandType = CommandType.StoredProcedure };

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
