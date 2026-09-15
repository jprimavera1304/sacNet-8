using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Empleados: lectura y escritura contra legacy.
///
/// REGLA QUE NO SE ROMPE AQUI: los sp_n_ de LECTURA se llaman DIRECTO, nunca
/// envueltos. sp_n_ConsultaEmpleadosTipoSueldo termina en
/// "INSERT INTO #EmpleadosResult ... EXEC (@Sql)"; meterlo dentro de otro
/// INSERT ... EXEC hace que SQL Server conteste "An INSERT EXEC statement
/// cannot be nested". Es el mismo error que ya costo dias en el flujo de
/// autorizar pedidos (ver VentasPedidosRepository.AutorizarPedidosAsync).
/// </summary>
public class EmpleadosRepository : IEmpleadosRepository
{
    private const string SpConsultaEmpleados = "dbo.sp_n_ConsultaEmpleadosTipoSueldo";
    private const string SpActualizarEmpleado = "dbo.sp_n_ActualizarEmpleado";

    // Los dos unicos sp_w_ de este modulo. El de alta existe porque legacy no
    // devuelve el id del empleado que acaba de crear; ver el encabezado del
    // script 05_sp_w_InsertarEmpleado.sql.
    private const string SpInsertarEmpleado = "dbo.sp_w_InsertarEmpleado";
    private const string SpCatalogos = "dbo.sp_w_ConsultarCatalogosEmpleado";

    // Legacy recibe las fechas como texto en el formato de Constantes.FormatoFecha,
    // que vale 'MM-dd-yyyy' en las dos empresas. Se escribe aqui y no en el
    // controller para que ningun front tenga que saberlo.
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public EmpleadosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<EmpleadoDto>> ConsultarAsync(int idEmpleado, int idTipoSueldo, int idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpConsultaEmpleados, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", idTipoSueldo);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<EmpleadoDto>(dt);
    }

    public async Task<CatalogosEmpleadoDto> ConsultarCatalogosAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpCatalogos, conn);

        // Cuatro result sets en una sola ida: puestos, tipos de sueldo, agentes
        // y estatus. Es toda la razon de ser del SP.
        var ds = new DataSet();
        using (var adapter = new SqlDataAdapter(cmd))
            await Task.Run(() => adapter.Fill(ds), ct);

        return new CatalogosEmpleadoDto
        {
            Puestos = ds.Tables.Count > 0 ? Funciones.DataTableToList<PuestoDto>(ds.Tables[0]) : new(),
            TiposSueldo = ds.Tables.Count > 1 ? Funciones.DataTableToList<TipoSueldoDto>(ds.Tables[1]) : new(),
            Agentes = ds.Tables.Count > 2 ? Funciones.DataTableToList<AgenteDto>(ds.Tables[2]) : new(),
            Estatus = ds.Tables.Count > 3 ? Funciones.DataTableToList<EstatusDto>(ds.Tables[3]) : new()
        };
    }

    public async Task<(ResultadoLegacy Resultado, EmpleadoCreadoDto Creado)> InsertarAsync(
        CrearEmpleadoRequest request, int idUsuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpInsertarEmpleado, conn);
        AgregarCamposEmpleado(cmd, request, idUsuario);

        // Dos result sets: el de legacy (result, mensaje) y el nuestro
        // (idEmpleado, numeroEmpleado).
        var ds = new DataSet();
        using (var adapter = new SqlDataAdapter(cmd))
            await Task.Run(() => adapter.Fill(ds), ct);

        var resultado = LeerResultado(ds.Tables.Count > 0 ? ds.Tables[0] : null);
        var creado = new EmpleadoCreadoDto();

        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
        {
            var row = ds.Tables[1].Rows[0];
            creado.IdEmpleado = LeerInt(row, "idEmpleado");
            creado.NumeroEmpleado = LeerInt(row, "numeroEmpleado");
        }

        // Legacy puede contestar result = 1 y aun asi no haber dejado renglon
        // (su ROLLBACK no cambia el result). Si no hay empleado nuevo, no hubo
        // alta, dijera lo que dijera.
        if (creado.IdEmpleado <= 0)
            return (new ResultadoLegacy { Result = -1, Mensaje = "El alta no quedo registrada." }, creado);

        return (resultado, creado);
    }

    public async Task<ResultadoLegacy> ActualizarAsync(
        int idEmpleado, ActualizarEmpleadoRequest request, int idUsuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Escritura llamada DIRECTO al sp_n_: no hay nada que agregarle y cada
        // capa de por medio es una capa donde se pierde el result/mensaje.
        await using var cmd = Sp(SpActualizarEmpleado, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        AgregarCamposEmpleado(cmd, request, idUsuario);
        cmd.Parameters.AddWithValue("@IDStatus", request.IDStatus);

        var dt = await FillAsync(cmd, ct);
        return LeerResultado(dt);
    }

    public async Task<int> ContarRenglonesTipoSueldoAsync(int idEmpleado, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // SELECT directo sobre la tabla: es solo lectura y no hay sp_n_ que lo
        // conteste. No se escribe nada de legacy.
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.EmpleadoTipoSueldo WHERE IDEmpleado = @IDEmpleado", conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);

        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is int n ? n : Convert.ToInt32(valor ?? 0, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Los 15 campos que comparten el alta y el cambio, en el orden y con los
    /// nombres que pide legacy.
    ///
    /// @IDUsuario sale SIEMPRE del parametro, nunca del request: quien hace el
    /// movimiento se toma del token para que nadie pueda firmar un alta a
    /// nombre de otro.
    /// </summary>
    private static void AgregarCamposEmpleado(SqlCommand cmd, CrearEmpleadoRequest r, int idUsuario)
    {
        cmd.Parameters.AddWithValue("@IDPuesto", r.IDPuesto);
        cmd.Parameters.AddWithValue("@IDChecador", r.IDChecador);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", r.IDTipoSueldo);
        cmd.Parameters.AddWithValue("@IDAgente", r.IDAgente);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Nombre", (r.Nombre ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@FechaIngreso", AFechaLegacy(r.FechaIngreso));
        cmd.Parameters.AddWithValue("@NumeroSeguro", (r.NumeroSeguro ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@Rfc", (r.Rfc ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@Curp", (r.Curp ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@SueldoSemanal", r.SueldoSemanal);
        cmd.Parameters.AddWithValue("@Comision", r.Comision);
        cmd.Parameters.AddWithValue("@Asistencia", r.Asistencia);
        cmd.Parameters.AddWithValue("@Puntualidad", r.Puntualidad);
        cmd.Parameters.AddWithValue("@SalidaTarde", r.SalidaTarde);
    }

    /// <summary>
    /// El parametro @FechaIngreso de legacy es varchar(10). Se acepta ISO
    /// (yyyy-MM-dd) del front y se traduce a MM-dd-yyyy, que es lo que la
    /// columna [Fecha de Ingreso] interpreta bien.
    ///
    /// Se hace aqui, con cultura invariante, y no en el navegador: una fecha
    /// formateada por el cliente depende de su idioma, y 03-04-2026 leido al
    /// reves cambia el mes por el dia.
    /// </summary>
    private static string AFechaLegacy(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha)) return string.Empty;

        if (DateTime.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParse(fecha, CultureInfo.CurrentCulture, DateTimeStyles.None, out d))
            return d.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture);

        // Si no se entiende, se manda vacio en vez de basura: legacy con vacio
        // deja la fecha nula, que es honesto; con basura escribe cualquier cosa.
        return string.Empty;
    }

    private static ResultadoLegacy LeerResultado(DataTable? dt)
    {
        if (dt == null || dt.Rows.Count == 0)
            return new ResultadoLegacy { Result = -1, Mensaje = "El procedimiento no devolvio resultado." };

        var row = dt.Rows[0];
        return new ResultadoLegacy
        {
            Result = LeerInt(row, "result"),
            Mensaje = LeerString(row, "mensaje")
        };
    }

    private static int LeerInt(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return 0;
        var v = row[columna];
        return v == DBNull.Value ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
    }

    private static string LeerString(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return string.Empty;
        var v = row[columna];
        return v == DBNull.Value ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
    }

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
