using System.Data;
using System.Diagnostics;
using System.Globalization;
using ISL_Service.Application.DTOs.Asistencias;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Asistencias: la rejilla del periodo y la captura manual de un dia.
///
/// LOS TRES SP DE LECTURA SE LLAMAN DIRECTO Y JAMAS SE ENVUELVEN.
/// sp_n_ConsultaEmpleadosPeriodoAsistencia hace
/// "INSERT INTO #EmpleadosTmp ... EXEC (@Sql)" adentro, y
/// sp_n_ConsultaPeriodosTipoSueldo y ...Dias terminan en EXEC (@Sql).
/// Cualquier sp_w_ que los meta en un INSERT ... EXEC revienta con
/// "An INSERT EXEC statement cannot be nested". Es el error que se va a querer
/// cometer; queda escrito aqui para que no se cometa.
///
/// La ESCRITURA si va por sp_w_, pero esos hacen EXEC a secas (no capturan el
/// result set), asi que tampoco anidan nada.
/// </summary>
public class AsistenciasRepository : IAsistenciasRepository
{
    private const string SpPeriodos = "dbo.sp_n_ConsultaPeriodosTipoSueldo";
    private const string SpPeriodosDias = "dbo.sp_n_ConsultaPeriodosTipoSueldoDias";
    private const string SpRejilla = "dbo.sp_n_ConsultaEmpleadosPeriodoAsistencia";

    private const string SpGuardarDia = "dbo.sp_w_GuardarAsistenciaDia";
    private const string SpEliminarDia = "dbo.sp_w_EliminarAsistenciaDia";

    // La rejilla es un doble cursor (empleados x dias) dentro de legacy.
    // Medido: 914 ms con los 25 empleados de Tauro y 3.75 s con los 85 de
    // Zaragoza. Los 30 s del default de SqlCommand alcanzan, pero se pone
    // explicito para que el numero este a la vista y no dependa de un default
    // que alguien puede cambiar en otro lado.
    private const int TimeoutRejillaSegundos = 60;

    private readonly IConfiguration _configuration;

    public AsistenciasRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<PeriodoNominaDto>> ConsultarPeriodosAsync(int idTipoSueldo, int top, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpPeriodos, conn);
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", 0);
        cmd.Parameters.AddWithValue("@IDPeriodicidad", 0);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", idTipoSueldo);
        cmd.Parameters.AddWithValue("@IDPeriodoStatus", 0);
        // @Orden distinto de 0 = descendente. La pantalla quiere lo mas nuevo
        // arriba: nadie captura asistencias de hace tres anios.
        cmd.Parameters.AddWithValue("@Orden", 1);

        var dt = await FillAsync(cmd, ct);

        // El TOP se hace aqui y no en el SP porque el SP no lo tiene, y
        // agregarselo seria modificar legacy. Son 540 periodos en Tauro y 473
        // en Zaragoza: recortar en memoria cuesta nada y evita mandarle al
        // navegador diez anios de semanas para llenar un combo.
        var lista = new List<PeriodoNominaDto>(Math.Min(dt.Rows.Count, top > 0 ? top : dt.Rows.Count));
        foreach (DataRow row in dt.Rows)
        {
            if (top > 0 && lista.Count >= top) break;

            lista.Add(new PeriodoNominaDto
            {
                IdPeriodoTipoSueldo = LeerInt(row, "IDPeriodoTipoSueldo"),
                IdTipoSueldo = LeerInt(row, "IDTipoSueldo"),
                Descripcion = LeerString(row, "PeriodoTipoSueldo"),
                FechaInicial = LeerFecha(row, "FechaInicial"),
                FechaFinal = LeerFecha(row, "FechaFinal"),
                NumeroSemana = LeerInt(row, "NumeroSemana")
            });
        }
        return lista;
    }

    public async Task<List<DiaPeriodoDto>> ConsultarDiasAsync(int idPeriodoTipoSueldo, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpPeriodosDias, conn);
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);
        cmd.Parameters.AddWithValue("@IDPeriodicidad", 0);
        cmd.Parameters.AddWithValue("@IDTipoSueldo", 0);
        cmd.Parameters.AddWithValue("@IDPeriodoStatus", 0);

        var dt = await FillAsync(cmd, ct);

        var lista = new List<DiaPeriodoDto>(dt.Rows.Count);
        foreach (DataRow row in dt.Rows)
        {
            var fecha = LeerFecha(row, "FechaDia");

            // Se descartan los dias que caen FUERA del rango del propio periodo.
            //
            // No es paranoia: la copia local de Zaragoza tiene renglones sueltos
            // en PeriodosTipoSueldoDias (ids 999902 y 999903) colgados de una
            // semana de julio pero fechados en agosto, y con ellos este endpoint
            // devolvia NUEVE dias para una semana.
            //
            // Nueve encabezados serian una rejilla mentirosa: el SP de la
            // rejilla solo tiene siete huecos (fechaDia1..fechaDia7) e ignora en
            // silencio lo que sobra. Entonces la pantalla pintaria dos columnas
            // que nunca se llenan. Que las dos consultas digan lo mismo importa
            // mas que arrastrar un renglon que ni el sistema viejo usa.
            var inicial = LeerFecha(row, "FechaInicial");
            var final = LeerFecha(row, "FechaFinal");
            if (inicial != default && final != default && (fecha < inicial.Date || fecha > final.Date))
                continue;

            lista.Add(new DiaPeriodoDto
            {
                IdPeriodoTipoSueldoDia = LeerInt(row, "IDPeriodoTipoSueldoDia"),
                Fecha = fecha
            });
        }
        return lista;
    }

    public async Task<RejillaAsistenciaResponse> ConsultarRejillaAsync(
        int idPeriodoTipoSueldo, int idEmpleado, int idStatus, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpRejilla, conn);
        cmd.CommandTimeout = TimeoutRejillaSegundos;
        cmd.Parameters.AddWithValue("@IDPeriodoTipoSueldo", idPeriodoTipoSueldo);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@IDStatus", idStatus);

        var reloj = Stopwatch.StartNew();
        var dt = await FillAsync(cmd, ct);
        reloj.Stop();

        // Renglon completo tal cual, incluidos los NULL. En Zaragoza
        // 'asistencias' llega NULL para algunos empleados y eso tiene que
        // llegar asi al front (ver Funciones.DataTableToRows).
        return new RejillaAsistenciaResponse
        {
            Rows = Funciones.DataTableToRows(dt),
            Milisegundos = reloj.ElapsedMilliseconds
        };
    }

    public async Task GuardarDiaAsync(
        GuardarAsistenciaDiaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpGuardarDia, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", request.IdEmpleado);
        // Viaja la FECHA. El IDPeriodoTipoSueldoDia lo resuelve el sp_w_ adentro
        // de la base: si ese cruce viviera en el front, un id mal armado
        // escribiria la asistencia en la semana equivocada.
        cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = (request.Fecha ?? DateTime.Today).Date;
        cmd.Parameters.AddWithValue("@Inasistencia", request.Inasistencia ? 1 : 0);
        cmd.Parameters.AddWithValue("@Vacacion", request.Vacacion ? 1 : 0);
        cmd.Parameters.AddWithValue("@HoraEntrada", request.HoraEntrada ?? string.Empty);
        cmd.Parameters.AddWithValue("@HoraSalida", request.HoraSalida ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo ?? string.Empty);

        // El sp_w_ avisa sus problemas con RAISERROR (no hay periodo para esa
        // fecha, salida antes que entrada...). Se deja subir: el middleware de
        // errores lo convierte en la respuesta.
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EliminarDiaAsync(
        int idEmpleado, DateTime fecha, int idUsuario, string equipo, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpEliminarDia, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha.Date;
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo ?? string.Empty);

        await cmd.ExecuteNonQueryAsync(ct);
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
        return v == DBNull.Value ? string.Empty : (Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
    }

    private static DateTime LeerFecha(DataRow row, string columna)
    {
        if (!row.Table.Columns.Contains(columna)) return default;
        var v = row[columna];
        return v == DBNull.Value ? default : Convert.ToDateTime(v, CultureInfo.InvariantCulture);
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
