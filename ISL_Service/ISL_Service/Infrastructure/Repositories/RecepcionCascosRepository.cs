using System.Data;
using ISL_Service.Application.DTOs.RecepcionCascos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Recepcion de cascos usados: la hoja de papel de Tauro.
///
/// TODO LO QUE ESCRIBE PASA POR sp_w_*. De legacy solo se LEE el catalogo de
/// tipos y el de repartidores, y se lee DENTRO de los procedimientos: asi el
/// nombre del tipo o la validez del repartidor no dependen de lo que mande el
/// cliente.
///
/// EL DETALLE VIAJA COMO TVP Y NO COMO JSON
/// Las dos bases estan en nivel de compatibilidad 100 y ahi OPENJSON no existe.
/// Un parametro de tabla funciona en los dos niveles y manda la recepcion
/// completa en UNA llamada: con ocho inserts sueltos, un corte de red a la
/// mitad dejaria media hoja capturada.
/// </summary>
public class RecepcionCascosRepository : IRecepcionCascosRepository
{
    private const string SpTipos     = "dbo.sp_w_ConsultarTiposRecepcionCasco";
    private const string SpRepartidores = "dbo.sp_w_ConsultarRepartidoresRecepcionCasco";
    private const string SpConsultar = "dbo.sp_w_ConsultarRecepcionesCasco";
    private const string SpDetalle   = "dbo.sp_w_ConsultarRecepcionCascoDetalle";
    private const string SpBitacora  = "dbo.sp_w_ConsultarRecepcionCascoBitacora";
    private const string SpInsertar  = "dbo.sp_w_InsertarRecepcionCasco";
    private const string SpActualizar = "dbo.sp_w_ActualizarRecepcionCasco";
    private const string SpCancelar  = "dbo.sp_w_CancelarRecepcionCasco";
    private const string SpReporte   = "dbo.sp_w_rptRecepcionCascos";

    private const string TipoTablaDetalle = "dbo.WRecepcionCascoDetalleType";
    private const string TipoTablaIds     = "dbo.WRecepcionCascoIdListType";

    private readonly IConfiguration _configuration;

    public RecepcionCascosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<TipoRecepcionCascoDto>> ConsultarTiposAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = Sp(SpTipos, conn);
        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<TipoRecepcionCascoDto>(dt);
    }

    public async Task<List<RepartidorRecepcionCascoDto>> ConsultarRepartidoresAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = Sp(SpRepartidores, conn);
        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<RepartidorRecepcionCascoDto>(dt);
    }

    public async Task<List<RecepcionCascoDto>> ConsultarAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int? idRepartidor,
        int? estatus, bool incluirCanceladas, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpConsultar, conn);
        cmd.Parameters.AddWithValue("@FechaInicio", (object?)fechaInicio?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FechaFin", (object?)fechaFin?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IDRepartidor", (object?)idRepartidor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estatus", (object?)estatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IncluirCanceladas", incluirCanceladas ? 1 : 0);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<RecepcionCascoDto>(dt);
    }

    public async Task<List<DetalleRecepcionCascoDto>> ConsultarDetalleAsync(
        int idRecepcion, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpDetalle, conn);
        cmd.Parameters.AddWithValue("@IdRecepcion", idRecepcion);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<DetalleRecepcionCascoDto>(dt);
    }

    public async Task<List<BitacoraRecepcionCascoDto>> ConsultarBitacoraAsync(
        int idRecepcion, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpBitacora, conn);
        cmd.Parameters.AddWithValue("@IdRecepcion", idRecepcion);

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToList<BitacoraRecepcionCascoDto>(dt);
    }

    /*
      Se consulta con la tabla y no con un procedimiento nuevo porque es una
      pregunta de una sola columna que solo usa el servicio para decidir si
      contesta 404 o 409. Es nuestra tabla: ningun sp_n_ la mira.
    */
    public async Task<int> EstatusDeAsync(int idRecepcion, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Estatus FROM dbo.WRecepcionCasco WHERE IdRecepcion = @id";
        cmd.Parameters.AddWithValue("@id", idRecepcion);

        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor == null || valor == DBNull.Value ? 0 : Convert.ToInt32(valor);
    }

    public async Task<ResultadoRecepcionCascoDto> InsertarAsync(
        CrearRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpInsertar, conn);
        AgregarDatos(cmd, request, idUsuario, usuario, canal);

        var dt = await FillAsync(cmd, ct);
        return Resultado(dt);
    }

    public async Task<ResultadoRecepcionCascoDto> ActualizarAsync(
        ActualizarRecepcionCascoRequest request, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpActualizar, conn);
        cmd.Parameters.AddWithValue("@IdRecepcion", request.IdRecepcion);
        AgregarDatos(cmd, request, idUsuario, usuario, canal);

        var dt = await FillAsync(cmd, ct);
        return Resultado(dt);
    }

    public async Task<ResultadoCancelacionRecepcionCascoDto> CancelarAsync(
        IEnumerable<int> ids, string motivo, int idUsuario, string usuario, string? canal,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpCancelar, conn);

        var tabla = new DataTable();
        tabla.Columns.Add("IdRecepcion", typeof(int));
        /*
          Distinct: la pantalla puede mandar el mismo id dos veces si alguien
          marco y volvio a marcar, y el repetido rompe la llave primaria del
          TVP con un error que no le dice nada a nadie.
        */
        foreach (var id in ids.Where(i => i > 0).Distinct())
            tabla.Rows.Add(id);

        var pIds = cmd.Parameters.AddWithValue("@Ids", tabla);
        pIds.SqlDbType = SqlDbType.Structured;
        pIds.TypeName = TipoTablaIds;

        cmd.Parameters.AddWithValue("@Motivo", motivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Usuario", usuario ?? string.Empty);
        cmd.Parameters.AddWithValue("@Canal", (object?)Texto(canal) ?? DBNull.Value);

        var dt = await FillAsync(cmd, ct);
        if (dt.Rows.Count == 0)
            return new ResultadoCancelacionRecepcionCascoDto
            {
                ok = false, mensaje = "El procedimiento no devolvio resultado.", canceladas = 0
            };

        var fila = dt.Rows[0];
        return new ResultadoCancelacionRecepcionCascoDto
        {
            ok = Booleano(fila, "ok"),
            mensaje = Cadena(fila, "mensaje"),
            canceladas = Entero(fila, "canceladas"),
            advertencia = Cadena(fila, "advertencia")
        };
    }

    /*
      Los dos resultados del reporte en UNA llamada. El procedimiento devuelve
      los renglones y luego los totales por tipo; se leen con NextResult en vez
      de pedirlos dos veces, porque los totales tienen que venir del mismo
      momento que los renglones: entre dos llamadas alguien puede cancelar una
      recepcion y el papel saldria con un total que no cuadra con su propio
      desglose.
    */
    public async Task<DatosReporteRecepcionCascos> ConsultarDatosDeReporteAsync(
        DateTime fechaInicio, DateTime fechaFin, bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(SpReporte, conn);
        cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio.Date);
        cmd.Parameters.AddWithValue("@FechaFin", fechaFin.Date);
        /*
          EL MISMO NOMBRE QUE USA LA CONSULTA DE LA LISTA: es el mismo filtro,
          escrito igual. De fabrica va en 0, que es el papel de siempre.
        */
        cmd.Parameters.AddWithValue("@IncluirCanceladas", incluirCanceladas ? 1 : 0);

        var señal = CancelacionSegura.SeñalPara(cmd, ct);
        var datos = new DatosReporteRecepcionCascos();

        await using var reader = await cmd.ExecuteReaderAsync(señal);

        var renglones = new DataTable();
        renglones.Load(reader);          // Load avanza al siguiente resultado solo
        datos.Renglones = Funciones.DataTableToList<RenglonReporteRecepcionCascoDto>(renglones);

        if (!reader.IsClosed)
        {
            var totales = new DataTable();
            totales.Load(reader);
            datos.Totales = Funciones.DataTableToList<TotalPorTipoRecepcionCascoDto>(totales);
        }

        return datos;
    }

    public async Task<(string PathImagenes, string Logo, string Empresa)> ConsultarMarcaEmpresaAsync(
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        /*
          Los mismos tres campos que usa el reporte de usados a cambio.
          FacturasCorreoNombre es el nombre con el que la empresa se presenta
          hacia afuera; los demas campos de Constantes son internos.
        */
        cmd.CommandText =
            "SELECT TOP 1 PathImagenes, LogoMacReportes, FacturasCorreoNombre FROM dbo.Constantes";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ("", "", "");

        string Txt(int i) => reader.IsDBNull(i) ? "" : reader.GetString(i).Trim();
        return (Txt(0), Txt(1), Txt(2));
    }

    /* ---------------------------------------------------------------- */
    /* Ayudantes                                                         */
    /* ---------------------------------------------------------------- */

    private void AgregarDatos(
        SqlCommand cmd, CrearRecepcionCascoRequest request,
        int idUsuario, string usuario, string? canal)
    {
        cmd.Parameters.AddWithValue("@Fecha", (object?)request.Fecha?.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IDRepartidor", request.IdRepartidor);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)Texto(request.Observaciones) ?? DBNull.Value);

        var tabla = TablaDeDetalle(request.Piezas);
        var pDetalle = cmd.Parameters.AddWithValue("@Detalle", tabla);
        pDetalle.SqlDbType = SqlDbType.Structured;
        pDetalle.TypeName = TipoTablaDetalle;

        /*  Quien y desde donde: del token y del encabezado, nunca del cuerpo.  */
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Usuario", usuario ?? string.Empty);
        cmd.Parameters.AddWithValue("@Canal", (object?)Texto(canal) ?? DBNull.Value);
    }

    /*
      La tabla que espera el TVP. Se filtran las piezas en cero y se juntan los
      repetidos: el front manda una casilla por tipo y casi todas vienen vacias,
      y el tipo repetido rompe la llave primaria del TVP.
    */
    private static DataTable TablaDeDetalle(List<PiezasPorTipoRecepcionRequest>? piezas)
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

    private static ResultadoRecepcionCascoDto Resultado(DataTable dt)
    {
        if (dt.Rows.Count == 0)
            return new ResultadoRecepcionCascoDto
            {
                ok = false, mensaje = "El procedimiento no devolvio resultado.", idRecepcion = 0
            };

        var fila = dt.Rows[0];
        return new ResultadoRecepcionCascoDto
        {
            ok = Booleano(fila, "ok"),
            mensaje = Cadena(fila, "mensaje"),
            idRecepcion = Entero(fila, "idRecepcion"),
            advertencia = Cadena(fila, "advertencia")
        };
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

    /*
      La cancelacion solo llega a las consultas: cancelar una escritura a medias
      deja su transaccion abierta y sus candados puestos en una conexion que
      vuelve al pool. Quien decide es CancelacionSegura, leyendo el comando.
    */
    private static async Task<DataTable> FillAsync(SqlCommand cmd, CancellationToken ct)
    {
        var señal = CancelacionSegura.SeñalPara(cmd, ct);
        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(señal);
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
