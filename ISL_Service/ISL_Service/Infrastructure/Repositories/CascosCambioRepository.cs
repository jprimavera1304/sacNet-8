using System.Data;
using ISL_Service.Application.DTOs.CascosCambio;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// "Cascos a cambio": la cuenta que hoy lleva Jazmin en Excel.
///
/// TODO LO QUE ESCRIBE PASA POR sp_w_*. De legacy solo se LEE el catalogo de
/// tipos, y se lee DENTRO de los procedimientos, no aqui: asi el precio no
/// puede viajar por la red ni pasar por el cliente.
///
/// EL DETALLE VIAJA COMO TVP Y NO COMO JSON
/// Las dos bases estan en nivel de compatibilidad 100 y ahi OPENJSON no existe
/// (probado). Un parametro de tabla funciona en los dos niveles y ademas manda
/// el movimiento completo en UNA sola llamada: si fueran siete inserts sueltos,
/// un corte de red a la mitad dejaria media entrega capturada.
/// </summary>
public class CascosCambioRepository : ICascosCambioRepository
{
    private const string SpTipos     = "dbo.sp_w_ConsultarTiposCascoCambio";
    private const string SpMovimientos = "dbo.sp_w_ConsultarCascosCambio";
    private const string SpDetalle   = "dbo.sp_w_ConsultarCascoCambioDetalle";
    private const string SpResumen   = "dbo.sp_w_ConsultarResumenCascosCambio";
    private const string SpInsertar  = "dbo.sp_w_InsertarCascoCambio";
    private const string SpCancelar  = "dbo.sp_w_CancelarCascoCambio";
    private const string SpSincronizarPrecios = "dbo.sp_w_SincronizarPreciosContraparteCascoCambio";

    private const string TipoTablaDetalle = "dbo.WCascoCambioDetalleType";

    private readonly IConfiguration _configuration;

    public CascosCambioRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<TipoCascoCambioDto>> ConsultarTiposAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = Sp(SpTipos, conn);
        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<TipoCascoCambioDto>(dt);
    }

    public async Task<List<MovimientoCascoCambioDto>> ConsultarMovimientosAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? tipoMovimiento, bool incluirCancelados,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpMovimientos, conn);
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TipoMovimiento", (object?)tipoMovimiento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IncluirCancelados", incluirCancelados ? 1 : 0);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<MovimientoCascoCambioDto>(dt);
    }

    public async Task<List<DetalleCascoCambioDto>> ConsultarDetalleAsync(int idMovimiento, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpDetalle, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<DetalleCascoCambioDto>(dt);
    }

    public async Task<List<ResumenTipoCascoCambioDto>> ConsultarResumenAsync(
        DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpResumen, conn);
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin?.Date ?? DBNull.Value);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<ResumenTipoCascoCambioDto>(dt);
    }

    public async Task<(bool Ok, string Mensaje, int IdMovimiento, string Advertencia)> InsertarAsync(
        CrearMovimientoCascoCambioRequest request, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpInsertar, conn);
        cmd.Parameters.AddWithValue("@Fecha", request.Fecha?.Date ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TipoMovimiento", request.TipoMovimiento);
        cmd.Parameters.AddWithValue("@Remision", (object?)Texto(request.Remision) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Persona", (object?)Texto(request.Persona) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)Texto(request.Observaciones) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Importe", request.Importe);
        cmd.Parameters.AddWithValue("@Usuario", usuario);

        var tabla = TablaDeDetalle(request.Piezas);
        var pDetalle = cmd.Parameters.AddWithValue("@Detalle", tabla);
        pDetalle.SqlDbType = SqlDbType.Structured;
        pDetalle.TypeName = TipoTablaDetalle;

        var dt = await FillAsync(cmd, ct);
        if (dt.Rows.Count == 0)
            return (false, "El procedimiento no devolvio resultado.", 0, string.Empty);

        var fila = dt.Rows[0];
        return (
            Booleano(fila, "ok"),
            Cadena(fila, "mensaje"),
            Entero(fila, "idMovimiento"),
            Cadena(fila, "advertencia"));
    }

    public async Task<(bool Ok, string Mensaje)> CancelarAsync(
        int idMovimiento, string motivo, string usuario, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpCancelar, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@Motivo", motivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@Usuario", usuario);

        var dt = await FillAsync(cmd, ct);
        if (dt.Rows.Count == 0)
            return (false, "El procedimiento no devolvio resultado.");

        return (Booleano(dt.Rows[0], "ok"), Cadena(dt.Rows[0], "mensaje"));
    }

    public async Task<SincronizacionPreciosDto> SincronizarPreciosContraparteAsync(
        string baseContraparte, string empresaContraparte, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpSincronizarPrecios, conn);
        cmd.Parameters.AddWithValue("@BaseContraparte", baseContraparte);
        cmd.Parameters.AddWithValue("@EmpresaContraparte", empresaContraparte);

        var dt = await FillAsync(cmd, ct);
        if (dt.Rows.Count == 0)
            return new SincronizacionPreciosDto { Ok = false, Mensaje = "El procedimiento no devolvio resultado." };

        return new SincronizacionPreciosDto
        {
            Ok = Booleano(dt.Rows[0], "ok"),
            Mensaje = Cadena(dt.Rows[0], "mensaje"),
            Tipos = Entero(dt.Rows[0], "tipos")
        };
    }

    /* ---------------------------------------------------------------- */
    /* Ayudantes                                                         */
    /* ---------------------------------------------------------------- */

    /*
      La tabla que espera el TVP. Se filtran las piezas en cero y se juntan los
      repetidos: el front manda una casilla por tipo y muchas vienen vacias, y
      el tipo repetido rompe la llave primaria del TVP con un error que no le
      dice nada a nadie.
    */
    private static DataTable TablaDeDetalle(List<PiezasPorTipoRequest>? piezas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("IDTipoUsado", typeof(int));
        tabla.Columns.Add("Piezas", typeof(int));

        if (piezas == null) return tabla;

        var agrupadas = piezas
            .Where(p => p != null && p.IdTipoUsado > 0 && p.Piezas > 0)
            .GroupBy(p => p.IdTipoUsado)
            .Select(g => new { IdTipoUsado = g.Key, Piezas = g.Sum(x => x.Piezas) });

        foreach (var p in agrupadas)
            tabla.Rows.Add(p.IdTipoUsado, p.Piezas);

        return tabla;
    }

    private static string? Texto(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    private static bool Booleano(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value) return false;
        return Convert.ToBoolean(fila[columna]);
    }

    private static string Cadena(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value) return string.Empty;
        return Convert.ToString(fila[columna]) ?? string.Empty;
    }

    private static int Entero(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna) || fila[columna] == DBNull.Value) return 0;
        return Convert.ToInt32(fila[columna]);
    }

    private static SqlCommand Sp(string nombre, SqlConnection conn)
        => new(nombre, conn) { CommandType = CommandType.StoredProcedure };

    private static async Task<DataTable> FillAsync(SqlCommand cmd, CancellationToken ct)
    {
        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);
        return dt;
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
