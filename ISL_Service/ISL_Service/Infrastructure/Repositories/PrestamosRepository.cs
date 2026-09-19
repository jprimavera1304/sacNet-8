using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Prestamos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Prestamos a empleados. AQUI SE MUEVE DINERO.
///
/// Los tres SP de escritura de legacy se llaman TAL CUAL, sin inventar nada
/// alrededor: alta, cambio del descuento semanal y cancelacion. Ningun sp_w_ de
/// por medio, porque no hay nada que agregar y cada capa extra es un lugar donde
/// se pierde el par result/mensaje.
///
/// DOS COSAS QUE NO SE EXPONEN, A PROPOSITO
/// ----------------------------------------
/// 1. sp_n_InsertarEmpleadoPrestamosPagos (abono manual). Hace
///    "SET Abonos = @Abono" — PISA el acumulado en vez de sumarlo — y no toca
///    Saldo ni [Fecha Pago]. Un abono capturado ahi deja el prestamo
///    descuadrado. Se usa mucho desde Mac31 (2148 renglones, 10 usuarios), asi
///    que el defecto es real y esta vivo; pero abrirlo tambien desde el web
///    seria multiplicar el descuadre por dos pantallas.
/// 2. sp_n_ValidaEmpleadosPrestamos. Es el BATCH SEMANAL que descuenta los
///    prestamos de la nomina: inserta el abono, baja el saldo y liquida. No
///    esta automatizado (no hay job ni en local ni en produccion): lo dispara
///    una persona desde Mac31. Colgarlo de un boton del web o del telefono
///    seria mover dinero de la nomina desde un celular. No se expone.
/// </summary>
public class PrestamosRepository : IPrestamosRepository
{
    private const string SpConsulta = "dbo.sp_n_ConsultaEmpleadoPrestamos";
    private const string SpInsertar = "dbo.sp_n_InsertaEmpleadoPrestamos";
    private const string SpActualizar = "dbo.sp_n_ActualizaEmpleadoPrestamos";
    private const string SpCancelar = "dbo.sp_n_CancelaEmpleadoPrestamos";

    // Las fechas entran a legacy como varchar en el formato de
    // Constantes.FormatoFecha, que vale 'MM-dd-yyyy' en las dos empresas.
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public PrestamosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<PrestamosResponse> ConsultarAsync(
        int idEmpleadoPrestamo, int idEmpleado, int pagados, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Llamada DIRECTA: sp_n_ConsultaEmpleadoPrestamos termina en EXEC (@Sql),
        // asi que no se puede envolver en un INSERT ... EXEC.
        await using var cmd = Sp(SpConsulta, conn);
        cmd.Parameters.AddWithValue("@IDEmpleadoPrestamo", idEmpleadoPrestamo);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@Pagados", pagados);
        // El cuarto parametro ya no aplica: el SP lo recalcula solo. Se manda
        // en 0 porque el procedimiento lo sigue pidiendo.
        cmd.Parameters.AddWithValue("@Cancelados", 0);

        var dt = await FillAsync(cmd, ct);
        return new PrestamosResponse { Rows = Funciones.DataTableToRows(dt) };
    }

    public async Task<(ResultadoLegacy Resultado, int IdEmpleadoPrestamo)> InsertarAsync(
        CrearPrestamoRequest request, int idUsuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpInsertar, conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", request.IdEmpleado);
        cmd.Parameters.AddWithValue("@FechaPrestamo", AFechaLegacy(request.FechaPrestamo));
        cmd.Parameters.AddWithValue("@MontoPrestamo", request.MontoPrestamo);
        cmd.Parameters.AddWithValue("@MotivoPrestamo", (request.MotivoPrestamo ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@FechaInicioPagos", AFechaLegacy(request.FechaInicioPagos));
        cmd.Parameters.AddWithValue("@MontoPagos", request.MontoPagos);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@PagoInmediato", request.PagoInmediato ? 1 : 0);
        cmd.Parameters.AddWithValue("@AgregarDeuda", request.AgregarDeuda ? 1 : 0);

        var dt = await FillAsync(cmd, ct);
        var resultado = LeerResultado(dt);

        var id = 0;
        if (dt.Rows.Count > 0)
            id = LeerInt(dt.Rows[0], "idEmpleadoPrestamo");

        return (resultado, id);
    }

    public async Task<ResultadoLegacy> ActualizarMontoPagosAsync(
        int idEmpleadoPrestamo, int idEmpleado, decimal montoPagos, int idUsuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Legacy SOLO deja cambiar el descuento semanal. Por eso la ruta del
        // endpoint es /monto-pagos y no un PUT generico: un PUT del prestamo
        // completo prometeria cambiar el monto o el motivo, y no se puede.
        await using var cmd = Sp(SpActualizar, conn);
        cmd.Parameters.AddWithValue("@IDEmpleadoPrestamo", idEmpleadoPrestamo);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);
        cmd.Parameters.AddWithValue("@MontoPagos", montoPagos);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        var dt = await FillAsync(cmd, ct);
        return LeerResultado(dt);
    }

    public async Task<ResultadoLegacy> CancelarAsync(int idEmpleadoPrestamo, int idUsuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpCancelar, conn);
        cmd.Parameters.AddWithValue("@IDEmpleadoPrestamo", idEmpleadoPrestamo);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);

        var dt = await FillAsync(cmd, ct);
        return LeerResultado(dt);
    }

    public async Task<bool> TienePrestamoAbiertoAsync(int idEmpleado, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // Es la MISMA condicion que evalua sp_n_InsertaEmpleadoPrestamos para
        // decidir si reusa el descuento del prestamo abierto. Se copia tal cual
        // a proposito: si aqui se preguntara otra cosa, el aviso al usuario
        // diria una cosa y legacy haria otra.
        await using var cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM dbo.EmpleadoPrestamos
            WHERE IDEmpleado = @IDEmpleado
              AND [Fecha Cancelacion] IS NULL
              AND [Fecha Pago] IS NULL
              AND PagoInmediato = 0", conn);
        cmd.Parameters.AddWithValue("@IDEmpleado", idEmpleado);

        var valor = await cmd.ExecuteScalarAsync(ct);
        var n = valor is int i ? i : Convert.ToInt32(valor ?? 0, CultureInfo.InvariantCulture);
        return n > 0;
    }

    public async Task<DateTime?> FinDelPeriodoNominaAbiertoAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        // SELECT directo y no sp_n_ConsultaPeriodosTipoSueldo porque ese SP
        // termina en EXEC (@Sql) y devuelve trece columnas para llenar un combo;
        // aqui hace falta UN dato. Es solo lectura sobre una tabla de legacy, que
        // es lo mismo que ya hace TienePrestamoAbiertoAsync mas arriba.
        //
        // Los filtros son los de Prestamos.cs:82-86, uno por uno:
        //   IDPeriodoStatus = 1  -> periodo abierto
        //   IDTipoSueldo    = 1  -> enumTiposSueldo.Nomina
        //   orden descendente, primer renglon
        //
        // Comprobado en las dos empresas: hoy hay exactamente UN periodo de
        // nomina abierto en cada una (Tauro 9523, Zaragoza 12551), asi que el
        // TOP 1 no esta eligiendo entre varios candidatos.
        await using var cmd = new SqlCommand(@"
            SELECT TOP 1 FechaFinal
            FROM dbo.PeriodosTipoSueldo
            WHERE IDPeriodoStatus = 1
              AND IDTipoSueldo = 1
            ORDER BY FechaFinal DESC", conn);

        var valor = await cmd.ExecuteScalarAsync(ct);
        if (valor is null || valor is DBNull) return null;
        return Convert.ToDateTime(valor, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Fecha -> texto MM-dd-yyyy. Sin fecha se manda vacio, que es lo que
    /// legacy entiende como "no hay".
    /// </summary>
    private static string AFechaLegacy(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture) : string.Empty;

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
