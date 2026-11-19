using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.AlmacenCascos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Repository Almacen de Cascos: SP sp_w_* y consultas directas a WMovimientoCasco / WMovimientoCascoDetalle.
/// </summary>
public class AlmacenCascosRepository : IAlmacenCascosRepository
{
    private readonly IConfiguration _configuration;
    private const string SpConsultar = "dbo.sp_w_ConsultarMovimientosCasco";
    private const string SpInsertarSalida = "dbo.sp_w_InsertarMovimientoCasco";
    private const string SpAceptarEntrada = "dbo.sp_w_AceptarEntradaCasco";
    private const string SpCancelar = "dbo.sp_w_CancelarMovimientoCasco";
    private const string SpActualizar = "dbo.sp_w_ActualizarMovimientoCasco";
    private const string SpInsertarTarimaKilos = "dbo.sp_w_InsertarMovimientoCascoTarima";
    private const string SpConsultarTarimasKilos = "dbo.sp_w_ConsultarMovimientoCascoTarimas";

    /*
      EL UNICO `sp_n_` DE ESTE REPOSITORIO, y va con su nombre a la vista para
      que se note. Es el que pinta "CASCOS ACTUAL" en Mac31 y es una lectura
      pura; el porque de no reescribirlo como `sp_w_` esta en
      ConsultarExistenciasAsync.
    */
    private const string SpExistencias = "dbo.sp_n_ConsultaTiposUsados";

    /*
      DENTRO DE UN CENTRO SE LEE LA TABLA DE LEGACY, Y NO ES UN PARCHE.

      Medido en produccion el 19-nov-2026: los movimientos de cascos de
      Zaragoza viven en `UsadosMovimientos` de CADA base de centro -2,108 en
      ZARA, 1,259 en AGRICOLA, 7,818 entre los trece- y la matriz tiene CERO.
      Es el mismo reparto que cortes.

      Las tablas de este modulo -WMovimientoCasco y companhia- son NUEVAS, se
      hicieron para la app, solo existen en la matriz y ahi tienen dos
      renglones. O sea que el modulo del telefono y la operacion real de
      cascos eran dos cosas distintas que se parecian: abrir la pantalla
      estando en un centro ensenaba una lista vacia mientras Mac31, en ese
      mismo centro, ensenaba dos mil.

      ASI QUE: en un centro se lee `sp_n_ConsultarUsadosMovimientos`, que es
      EXACTAMENTE el procedimiento que usa la pantalla "Consulta de
      Movimientos de Cascos" de Mac31 (Forms/UsadoMovimiento/
      ConsultarUsadosMovimientos.cs). Legacy es el oraculo y no se toca: se
      lee. En la matriz no cambia nada, byte por byte.

      LO QUE NO SE PUEDE TRAER, Y HAY QUE SABERLO: legacy no tiene tarimas ni
      kilos. Un movimiento de alla es una cabecera con su numero de cascos y
      ya -su propio "Detalle" es la misma cabecera de UN movimiento, no un
      desglose-. Por eso en un centro el detalle viene vacio: no es que falle,
      es que no existe.

      Y NO SE ESCRIBE EN UN CENTRO. Las escrituras piden la conexion con
      AbrirParaEscribirAsync y el modulo esta declarado SIN operaciones, asi
      que contestan "esta accion solo se puede hacer en la matriz". Es
      deliberado: la captura de un centro se hace en Mac31, que es de donde
      salieron esos 7,818, y escribir en la tabla nueva de un centro crearia
      una segunda verdad al lado de la de legacy.
    */
    private readonly IConexionDelCentro _centro;

    public AlmacenCascosRepository(IConfiguration configuration, IConexionDelCentro centro)
    {
        _configuration = configuration;
        _centro = centro;
    }

    /// <summary>La conexion de una LECTURA: la del centro elegido, o la matriz.</summary>
    /// <remarks>Llega abierta. Con centro 0 es la de siempre.</remarks>
    private Task<SqlConnection> AbrirParaLeerAsync(CancellationToken ct)
        => _centro.AbrirParaLeerAsync(ModuloDeCentros, ct);

    /// <summary>La conexion de una ESCRITURA. Fuera de la matriz la rechaza la capa.</summary>
    private Task<SqlConnection> AbrirParaEscribirAsync(string operacion, CancellationToken ct)
        => _centro.AbrirParaEscribirAsync(ModuloDeCentros, operacion, ct);

    /// <summary>La clave con la que este modulo esta declarado en ModulosConCentros.</summary>
    private const string ModuloDeCentros = "almacen.cascos";

    /// <summary>
    /// El procedimiento de legacy que pinta la pantalla de Mac31. Se llama tal
    /// cual y solo cuando hay un centro puesto.
    /// </summary>
    private const string SpMovimientosDeLegacy = "dbo.sp_n_ConsultarUsadosMovimientos";

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

    public async Task<List<MovimientoCascoDto>> ConsultarMovimientosAsync(int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct = default)
    {
        /*  DENTRO DE UN CENTRO, LA LISTA ES LA DE LEGACY. Ver el comentario del
            constructor: las tablas de este modulo no existen alla y los
            movimientos de verdad estan en `UsadosMovimientos`.  */
        if (!_centro.EsLaMatriz)
            return await ConsultarMovimientosDelCentroAsync(estatus, tipoMovimiento, fechaInicio, fechaFin, ct);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@Estatus", (object?)estatus ?? DBNull.Value);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        var list = Funciones.DataTableToList<MovimientoCascoDto>(dt);

        if (list.Count > 0)
        {
            var ids = list.Select(x => x.IdMovimiento).Distinct().ToList();
            var paramNames = ids.Select((_, i) => $"@Id{i}").ToList();

            var sql = $@"
SELECT IdMovimiento, COUNT(1) AS TotalTarimasConKilos
FROM dbo.WMovimientoCascoTarima
WHERE IdMovimiento IN ({string.Join(", ", paramNames)})
GROUP BY IdMovimiento";

            await using var cmdTar = new SqlCommand(sql, conn);
            for (var i = 0; i < ids.Count; i++)
                cmdTar.Parameters.AddWithValue(paramNames[i], ids[i]);

            var dtTar = new DataTable();
            using (var adapterTar = new SqlDataAdapter(cmdTar))
            {
                adapterTar.Fill(dtTar);
            }

            var map = dtTar.AsEnumerable()
                .ToDictionary(r => r.Field<int>("IdMovimiento"), r => r.Field<int>("TotalTarimasConKilos"));

            foreach (var mov in list)
            {
                if (map.TryGetValue(mov.IdMovimiento, out var total))
                    mov.TotalTarimasConKilos = total;
            }
        }

        if (tipoMovimiento.HasValue)
            list = list.Where(x => x.TipoMovimiento == tipoMovimiento.Value).ToList();

        if (fechaInicio.HasValue)
            list = list.Where(x => x.FechaCreacion.HasValue && x.FechaCreacion.Value.Date >= fechaInicio.Value.Date).ToList();

        if (fechaFin.HasValue)
            list = list.Where(x => x.FechaCreacion.HasValue && x.FechaCreacion.Value.Date <= fechaFin.Value.Date).ToList();

        return list;
    }

    /// <summary>
    /// Los movimientos de cascos de UN CENTRO DE SERVICIO, leidos de legacy.
    /// </summary>
    /// <remarks>
    /// ES EL MISMO PROCEDIMIENTO QUE LA PANTALLA DE MAC31, con los mismos
    /// parametros, para que la lista diga exactamente lo mismo que el programa
    /// de escritorio del centro. Si algun dia no coincide, el que tiene razon
    /// es Mac31.
    ///
    /// @TipoConsulta = 1 ("Zaragoza") deja solo los movimientos que tienen
    /// repartidor; el 2 son los de gallero y el 0 los trae todos. Se manda 1
    /// porque es lo que manda la pantalla, y la gracia de leer de legacy se
    /// perderia trayendo renglones que alla no se ven.
    ///
    /// LAS FECHAS VAN COMO TEXTO en `MM-dd-yyyy HH:mm:ss`, que es el formato de
    /// los defaults del propio procedimiento: el parametro es varchar y lo
    /// concatena dentro de su BETWEEN, asi que el formato lo decide quien
    /// llama.
    ///
    /// Y OJO CON EL ORDEN DE SUS FILTROS: el BETWEEN de fechas solo se aplica
    /// cuando no viene ni @IDCobro ni @FolioCobro. Se mandan los dos en cero a
    /// proposito.
    /// </remarks>
    private async Task<List<MovimientoCascoDto>> ConsultarMovimientosDelCentroAsync(
        int? estatus, int? tipoMovimiento, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct)
    {
        await using var conn = await AbrirParaLeerAsync(ct);

        /*  Sin rango no se pide: `UsadosMovimientos` de un centro tiene miles
            de renglones y el telefono siempre manda su periodo. El respaldo es
            el mes corriente, no "todo".  */
        var desde = fechaInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var hasta = fechaFin ?? DateTime.Today;

        await using var cmd = new SqlCommand(SpMovimientosDeLegacy, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@IDUsadoMovimiento", 0);
        cmd.Parameters.AddWithValue("@IDGallero", 0);
        cmd.Parameters.AddWithValue("@IDRepartidor", 0);
        cmd.Parameters.AddWithValue("@FolioInicial", 0);
        cmd.Parameters.AddWithValue("@FolioFinal", 0);
        cmd.Parameters.AddWithValue("@FechaInicial", desde.Date.ToString(FormatoFechaDeLegacy, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@FechaFinal", hasta.Date.AddDays(1).AddSeconds(-1).ToString(FormatoFechaDeLegacy, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@TipoConsulta", 1);
        cmd.Parameters.AddWithValue("@IDCobro", 0);
        cmd.Parameters.AddWithValue("@FolioCobro", 0);

        var dt = new DataTable();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            dt.Load(reader);
        }

        var lista = new List<MovimientoCascoDto>(dt.Rows.Count);
        foreach (DataRow fila in dt.Rows)
        {
            var esSalida = string.Equals(Texto(fila, "Movimiento"), "SALIDA", StringComparison.OrdinalIgnoreCase);
            var cancelada = fila.Table.Columns.Contains("FechaCancelacion") && fila["FechaCancelacion"] != DBNull.Value;
            var nombre = Texto(fila, "Nombre");
            var idRepartidor = Entero(fila, "IDRepartidor");

            lista.Add(new MovimientoCascoDto
            {
                IdMovimiento = Entero(fila, "IDUsadoMovimiento"),
                Folio = Texto(fila, "FolioFtm"),
                TipoMovimiento = esSalida ? TipoSalida : TipoEntrada,

                /*  LEGACY NO TIENE EL CICLO REGISTRADA -> ACEPTADA. Un
                    movimiento de alla ya ocurrio, asi que se presenta como
                    ACEPTADA; si tiene fecha de cancelacion, CANCELADA. Darle
                    "registrada" seria ofrecer un boton de aceptar que no
                    existe.  */
                Estatus = cancelada ? EstatusCancelada : EstatusAceptada,

                /*  En una SALIDA el almacen entrega y el repartidor RECIBE; en
                    una ENTRADA es al reves. Es la misma lectura que hace la
                    pantalla al decidir que nombre pintar.  */
                IdRepartidorRecibe = esSalida && idRepartidor > 0 ? idRepartidor : null,
                RecibeNombre = esSalida ? nombre : null,
                IdRepartidorEntrega = !esSalida && idRepartidor > 0 ? idRepartidor : null,
                EntregaNombre = esSalida ? null : nombre,

                TotalPiezas = Entero(fila, "NumCascos"),

                /*  Cero y no un numero inventado: legacy no guarda tarimas ni
                    kilos de estos movimientos. La pantalla lo ensena vacio
                    porque vacio esta.  */
                TotalTarimas = 0,
                TotalKilos = 0m,
                TotalTarimasConKilos = 0,

                Observaciones = Texto(fila, "Motivo"),
                UsuarioCreacion = Texto(fila, "Usuario"),
                FechaCreacion = Fecha(fila, "Fecha"),
                UsuarioCancelacion = cancelada ? Texto(fila, "UsuarioCancelacion") : null,
                FechaCancelacion = Fecha(fila, "FechaCancelacion"),
            });
        }

        /*  TIPO Y ESTATUS SE FILTRAN AQUI y no en el procedimiento: el de
            legacy no sabe de ninguno de los dos -su @TipoConsulta separa
            repartidores de galleros, no salidas de entradas-. Las FECHAS si
            van alla, que es donde estan los indices.  */
        if (tipoMovimiento.HasValue)
            lista = lista.Where(x => x.TipoMovimiento == tipoMovimiento.Value).ToList();

        if (estatus.HasValue)
            lista = lista.Where(x => x.Estatus == estatus.Value).ToList();

        return lista.OrderByDescending(x => x.FechaCreacion).ToList();
    }

    private const int TipoSalida = 1;
    private const int TipoEntrada = 2;
    private const int EstatusAceptada = 2;
    private const int EstatusCancelada = 3;

    /// <summary>El formato que esperan los varchar de fecha de legacy.</summary>
    private const string FormatoFechaDeLegacy = "MM-dd-yyyy HH:mm:ss";

    private static string Texto(DataRow fila, string columna)
        => fila.Table.Columns.Contains(columna) && fila[columna] != DBNull.Value
            ? Convert.ToString(fila[columna], CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";

    private static int Entero(DataRow fila, string columna)
        => fila.Table.Columns.Contains(columna) && fila[columna] != DBNull.Value
            ? Convert.ToInt32(fila[columna], CultureInfo.InvariantCulture)
            : 0;

    private static DateTime? Fecha(DataRow fila, string columna)
        => fila.Table.Columns.Contains(columna) && fila[columna] != DBNull.Value
            ? Convert.ToDateTime(fila[columna], CultureInfo.InvariantCulture)
            : null;

    public async Task<List<MovimientoCascoDetalleDto>> GetDetalleMovimientoAsync(int idMovimiento, CancellationToken ct = default)
    {
        /*  UN MOVIMIENTO DE UN CENTRO NO TIENE RENGLONES, y la lista vacia es
            la respuesta correcta, no un fallo. En legacy el movimiento es una
            CABECERA con su numero de cascos: su propio
            `sp_n_ConsultarUsadosMovimientosDetalle` devuelve otra vez esa
            cabecera, de UN movimiento, no un desglose por tarima. Las tarimas
            y los kilos son de este modulo y viven en la matriz.  */
        if (!_centro.EsLaMatriz) return [];

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT d.IdDetalle, d.IdMovimiento, d.IdTarima, d.IdTipoCasco, d.NumeroTarima, d.Piezas,
       ISNULL(tu.[Tipo de Usado], N'') AS NombreTarima,
       ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
FROM dbo.WMovimientoCascoDetalle d
LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
WHERE d.IdMovimiento = @IdMovimiento
ORDER BY d.NumeroTarima, d.IdDetalle";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }
        return Funciones.DataTableToList<MovimientoCascoDetalleDto>(dt);
    }

    public async Task<MovimientoCascoDto?> GetMovimientoAsync(int idMovimiento, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT IdMovimiento, TipoMovimiento, Estatus
FROM dbo.WMovimientoCasco
WHERE IdMovimiento = @IdMovimiento";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }

        return Funciones.DataTableToList<MovimientoCascoDto>(dt).FirstOrDefault();
    }

    public async Task<bool> ExisteNumeroTarimaDetalleAsync(int idMovimiento, int numeroTarima, CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT TOP 1 1
FROM dbo.WMovimientoCascoDetalle
WHERE IdMovimiento = @IdMovimiento AND NumeroTarima = @NumeroTarima";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@NumeroTarima", numeroTarima);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null && result != DBNull.Value;
    }

    public async Task<int> InsertarSalidaAsync(CreateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("crear", ct);
        using var tran = conn.BeginTransaction();

        try
        {
            int idMovimiento;
            await using (var cmdSp = new SqlCommand(SpInsertarSalida, conn, tran))
            {
                cmdSp.CommandType = CommandType.StoredProcedure;
                cmdSp.Parameters.AddWithValue("@IdRepartidorEntrega", request.IdRepartidorEntrega);
                cmdSp.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
                cmdSp.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

                await using var reader = await cmdSp.ExecuteReaderAsync(ct);
                idMovimiento = 0;
                if (await reader.ReadAsync(ct))
                    idMovimiento = Convert.ToInt32(reader["IdMovimiento"]);
                await reader.CloseAsync();
            }

            if (idMovimiento <= 0)
                throw new ArgumentException("No se obtuvo IdMovimiento al insertar cabecera.");

            var lineas = FlattenTarimas(request.Tarimas);
            var totalTarimas = lineas.Select(x => x.NumeroTarima).Distinct().Count();
            var totalPiezas = lineas.Sum(x => x.Piezas);

            foreach (var item in lineas)
            {
                await using var cmdDet = new SqlCommand(@"
INSERT INTO dbo.WMovimientoCascoDetalle (IdMovimiento, IdTarima, IdTipoCasco, NumeroTarima, Piezas)
VALUES (@IdMovimiento, @IdTarima, @IdTipoCasco, @NumeroTarima, @Piezas)", conn, tran);
                cmdDet.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdDet.Parameters.AddWithValue("@IdTarima", DBNull.Value);
                cmdDet.Parameters.AddWithValue("@IdTipoCasco", item.IdTipoCasco);
                cmdDet.Parameters.AddWithValue("@NumeroTarima", item.NumeroTarima);
                cmdDet.Parameters.AddWithValue("@Piezas", item.Piezas);
                await cmdDet.ExecuteNonQueryAsync(ct);
            }

            await using var cmdUpd = new SqlCommand(@"
UPDATE m
SET TotalTarimas = @TotalTarimas,
    TotalPiezas  = @TotalPiezas,
    TotalKilos   = 0,
    NombreTarima = det.TipoCascoDescripcion
FROM dbo.WMovimientoCasco m
OUTER APPLY (
    SELECT TOP 1 ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
    FROM dbo.WMovimientoCascoDetalle d
    LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
    WHERE d.IdMovimiento = @IdMovimiento
    ORDER BY d.NumeroTarima, d.IdDetalle
) det
WHERE m.IdMovimiento = @IdMovimiento", conn, tran);
            cmdUpd.Parameters.AddWithValue("@TotalTarimas", totalTarimas);
            cmdUpd.Parameters.AddWithValue("@TotalPiezas", totalPiezas);
            cmdUpd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
            await cmdUpd.ExecuteNonQueryAsync(ct);

            tran.Commit();
            return idMovimiento;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task AceptarEntradaAsync(CreateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("aceptar", ct);

        await using var cmd = new SqlCommand(SpAceptarEntrada, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimientoSalida", request.IdMovimientoSalida);
        cmd.Parameters.AddWithValue("@IdRepartidorRecibe", request.IdRepartidorRecibe);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarSalidaAsync(int idMovimiento, UpdateSalidaRequest request, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("editar", ct);
        using var tran = conn.BeginTransaction();

        try
        {
            await using (var cmdMov = new SqlCommand(@"
SELECT TipoMovimiento, Estatus
FROM dbo.WMovimientoCasco WITH (UPDLOCK, ROWLOCK)
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdMov.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                await using var reader = await cmdMov.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new ArgumentException("Movimiento no encontrado.");

                var tipoMov = reader.GetInt32(0);
                var estatus = reader.GetInt32(1);
                if (tipoMov != 1)
                    throw new ArgumentException("El movimiento no corresponde a una salida.");
                if (estatus == 3)
                    throw new ArgumentException("No se puede modificar un movimiento cancelado.");
                await reader.CloseAsync();
            }

            await using (var cmdUpdHeader = new SqlCommand(@"
UPDATE dbo.WMovimientoCasco
SET IdRepartidorEntrega = @IdRepartidorEntrega,
    Observaciones = @Observaciones,
    UsuarioModificacion = @Usuario,
    FechaModificacion = GETDATE()
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdUpdHeader.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdUpdHeader.Parameters.AddWithValue("@IdRepartidorEntrega", request.IdRepartidorEntrega);
                cmdUpdHeader.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
                cmdUpdHeader.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmdUpdHeader.ExecuteNonQueryAsync(ct);
            }

            await using (var cmdDel = new SqlCommand("DELETE FROM dbo.WMovimientoCascoDetalle WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdDel.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                await cmdDel.ExecuteNonQueryAsync(ct);
            }

            var lineas = FlattenTarimas(request.Tarimas);
            var totalTarimas = lineas.Select(x => x.NumeroTarima).Distinct().Count();
            var totalPiezas = lineas.Sum(x => x.Piezas);

            foreach (var item in lineas)
            {
                await using var cmdDet = new SqlCommand(@"
INSERT INTO dbo.WMovimientoCascoDetalle (IdMovimiento, IdTarima, IdTipoCasco, NumeroTarima, Piezas)
VALUES (@IdMovimiento, @IdTarima, @IdTipoCasco, @NumeroTarima, @Piezas)", conn, tran);
                cmdDet.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdDet.Parameters.AddWithValue("@IdTarima", DBNull.Value);
                cmdDet.Parameters.AddWithValue("@IdTipoCasco", item.IdTipoCasco);
                cmdDet.Parameters.AddWithValue("@NumeroTarima", item.NumeroTarima);
                cmdDet.Parameters.AddWithValue("@Piezas", item.Piezas);
                await cmdDet.ExecuteNonQueryAsync(ct);
            }

            await using (var cmdUpdTot = new SqlCommand(@"
UPDATE m
SET TotalTarimas = @TotalTarimas,
    TotalPiezas  = @TotalPiezas,
    NombreTarima = det.TipoCascoDescripcion,
    UsuarioModificacion = @Usuario,
    FechaModificacion = GETDATE()
FROM dbo.WMovimientoCasco m
OUTER APPLY (
    SELECT TOP 1 ISNULL(tu.[Tipo de Usado], N'') AS TipoCascoDescripcion
    FROM dbo.WMovimientoCascoDetalle d
    LEFT JOIN [Catalogo TiposUsados] tu ON tu.IdTipoUsado = d.IdTipoCasco
    WHERE d.IdMovimiento = @IdMovimiento
    ORDER BY d.NumeroTarima, d.IdDetalle
) det
WHERE m.IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdUpdTot.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdUpdTot.Parameters.AddWithValue("@TotalTarimas", totalTarimas);
                cmdUpdTot.Parameters.AddWithValue("@TotalPiezas", totalPiezas);
                cmdUpdTot.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmdUpdTot.ExecuteNonQueryAsync(ct);
            }

            tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task ActualizarEntradaAsync(int idMovimiento, UpdateEntradaRequest request, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("editar", ct);

        await using var cmd = new SqlCommand(SpActualizar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@IdRepartidorEntrega", DBNull.Value);
        cmd.Parameters.AddWithValue("@IdTarima", DBNull.Value);
        cmd.Parameters.AddWithValue("@NumeroTarima", DBNull.Value);
        cmd.Parameters.AddWithValue("@Piezas", DBNull.Value);
        cmd.Parameters.AddWithValue("@Kilos", DBNull.Value);
        cmd.Parameters.AddWithValue("@IdRepartidorRecibe", request.IdRepartidorRecibe);
        cmd.Parameters.AddWithValue("@Observaciones", string.IsNullOrWhiteSpace(request.Observaciones) ? DBNull.Value : request.Observaciones!.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MovimientoCascoTarimaKilosResultDto> UpsertMovimientoTarimaKilosAsync(int idMovimiento, int numeroTarima, decimal kilos, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("kilos", ct);
        using var tran = conn.BeginTransaction();

        try
        {
            await using (var cmd = new SqlCommand(SpInsertarTarimaKilos, conn, tran))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmd.Parameters.AddWithValue("@NumeroTarima", numeroTarima);
                cmd.Parameters.AddWithValue("@Kilos", kilos);
                cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmd.ExecuteNonQueryAsync(ct);
            }

            decimal totalKilos;
            int totalTarimas;
            await using (var cmdTot = new SqlCommand(@"
SELECT ISNULL(SUM(Kilos), 0) AS TotalKilos,
       COUNT(1) AS TotalTarimasConKilos
FROM dbo.WMovimientoCascoTarima
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdTot.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                await using var reader = await cmdTot.ExecuteReaderAsync(ct);
                totalKilos = 0;
                totalTarimas = 0;
                if (await reader.ReadAsync(ct))
                {
                    totalKilos = reader.GetDecimal(0);
                    totalTarimas = reader.GetInt32(1);
                }
                await reader.CloseAsync();
            }

            await using (var cmdUpd = new SqlCommand(@"
UPDATE dbo.WMovimientoCasco
SET TotalKilos = @TotalKilos,
    UsuarioModificacion = @Usuario,
    FechaModificacion = GETDATE()
WHERE IdMovimiento = @IdMovimiento", conn, tran))
            {
                cmdUpd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
                cmdUpd.Parameters.AddWithValue("@TotalKilos", totalKilos);
                cmdUpd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());
                await cmdUpd.ExecuteNonQueryAsync(ct);
            }

            tran.Commit();

            return new MovimientoCascoTarimaKilosResultDto
            {
                IdMovimiento = idMovimiento,
                NumeroTarima = numeroTarima,
                Kilos = kilos,
                TotalKilos = totalKilos,
                TotalTarimasConKilos = totalTarimas
            };
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task<List<MovimientoCascoTarimaKilosDto>> GetMovimientoTarimasKilosAsync(int idMovimiento, CancellationToken ct = default)
    {
        /*  UN MOVIMIENTO DE UN CENTRO NO TIENE RENGLONES, y la lista vacia es
            la respuesta correcta, no un fallo. En legacy el movimiento es una
            CABECERA con su numero de cascos: su propio
            `sp_n_ConsultarUsadosMovimientosDetalle` devuelve otra vez esa
            cabecera, de UN movimiento, no un desglose por tarima. Las tarimas
            y los kilos son de este modulo y viven en la matriz.  */
        if (!_centro.EsLaMatriz) return [];

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(SpConsultarTarimasKilos, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(cmd))
        {
            adapter.Fill(dt);
        }
        return Funciones.DataTableToList<MovimientoCascoTarimaKilosDto>(dt);
    }

    /// <summary>
    /// La existencia por tipo de casco, para el panel "CASCOS ACTUAL".
    /// </summary>
    /// <remarks>
    /// SE LLAMA A UN `sp_n_` Y NO SE TOCA, y eso tiene su motivo escrito en
    /// ExistenciaDeCascoDto: la existencia no esta en `[Catalogo TiposUsados]`
    /// sino en el JOIN con `EmpresaUsado` de la empresa en curso, y ese JOIN —con
    /// su eleccion de empresa (9 para TAU, la minima activa para el resto)— lo
    /// hace el procedimiento. Replicarlo aqui seria una segunda copia de una
    /// regla de empresa que ya existe, y las dos copias se separan.
    ///
    /// Los cinco parametros van en su valor neutro (0 / cadena vacia), que es
    /// exactamente lo que manda Mac31: trae TODOS los tipos de casco. El
    /// `@IDStatus = 0` no quiere decir "ninguno": el procedimiento lo pasa por
    /// `fn_RegresaTablaStatus(0)`, que devuelve todos los status.
    ///
    /// NO SE FILTRAN LOS INACTIVOS, y es deliberado. Un casco dado de baja con
    /// existencia en 300 es justo lo que hay que poder ver: esconderlo deja 300
    /// piezas fuera del total sin que nada lo diga. Mac31 tampoco los esconde.
    /// </remarks>
    public async Task<List<ExistenciaDeCascoDto>> ConsultarExistenciasAsync(CancellationToken ct = default)
    {
        /*  "CASCOS ACTUAL" ES DE LA BASE EN LA QUE SE MIRA.

            Este si es un `sp_n_` puro y existe en las trece bases, asi que
            dentro de un centro contesta la existencia DE ESE CENTRO: es el
            mismo panel que Mac31 pinta a la derecha de su pantalla cuando
            esta conectado al centro. Con la matriz puesta no cambia nada.  */
        await using var conn = await AbrirParaLeerAsync(ct);

        await using var cmd = new SqlCommand(SpExistencias, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IDTipoUsado", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 0);
        cmd.Parameters.AddWithValue("@TipoUsado", string.Empty);
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var lista = new List<ExistenciaDeCascoDto>();
        await using var lector = await cmd.ExecuteReaderAsync(ct);

        /*
          Se leen las columnas POR NOMBRE y no por posicion. El procedimiento
          devuelve diecisiete columnas de una tabla temporal y la posicion de
          `Existencia` depende del orden en que esa tabla se declaro: cualquier
          columna que se agregue en medio correria todos los indices, y el sintoma
          seria una existencia que de pronto ensena el peso en kilos.
        */
        var iId = lector.GetOrdinal("IDTipoUsado");
        var iDesc = lector.GetOrdinal("TipoUsado");
        var iCorta = lector.GetOrdinal("TipoUsadoCorto");
        var iExistencia = lector.GetOrdinal("Existencia");
        var iOrden = lector.GetOrdinal("Orden");

        while (await lector.ReadAsync(ct))
        {
            lista.Add(new ExistenciaDeCascoDto
            {
                IdTipoCasco = lector.IsDBNull(iId) ? 0 : Convert.ToInt32(lector.GetValue(iId)),
                Descripcion = lector.IsDBNull(iDesc) ? string.Empty : lector.GetString(iDesc).Trim(),
                DescripcionCorta = lector.IsDBNull(iCorta) ? string.Empty : lector.GetString(iCorta).Trim(),
                Existencia = lector.IsDBNull(iExistencia) ? 0 : Convert.ToInt32(lector.GetValue(iExistencia)),
                Orden = lector.IsDBNull(iOrden) ? 0 : Convert.ToInt32(lector.GetValue(iOrden))
            });
        }

        return lista;
    }

    public async Task CancelarMovimientoAsync(int idMovimiento, string motivoCancelacion, string usuario, CancellationToken ct = default)
    {
        /*  ESCRIBIR ES SOLO DE LA MATRIZ. Con un centro puesto la capa
            contesta "esta accion solo se puede hacer en la matriz" y no se
            abre nada: la captura de un centro se hace en Mac31, que es
            donde vive su historia.  */
        await using var conn = await AbrirParaEscribirAsync("cancelar", ct);

        await using var cmd = new SqlCommand(SpCancelar, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@IdMovimiento", idMovimiento);
        cmd.Parameters.AddWithValue("@Motivo", motivoCancelacion.Trim());
        cmd.Parameters.AddWithValue("@Usuario", string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static List<(int NumeroTarima, int IdTipoCasco, int Piezas)> FlattenTarimas(IEnumerable<SalidaTarimaDto> tarimas)
    {
        var result = new List<(int NumeroTarima, int IdTipoCasco, int Piezas)>();

        foreach (var tarima in tarimas)
        {
            foreach (var linea in tarima.Lineas)
            {
                result.Add((tarima.NumeroTarima, linea.IdTipoCasco, linea.Piezas));
            }
        }

        return result;
    }
}
