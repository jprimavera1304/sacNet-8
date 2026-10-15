using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.SeguridadMac;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Usuarios de Mac31, huellas, constantes de empresa y parametros de reporte.
///
/// LO QUE ARREGLA ESTE REPOSITORIO, EN UNA FRASE: los tres lotes del usuario
/// (almacenes, procesos y fechas) ya no se aplican a medias.
///
/// En MacServicios2 esos tres endpoints partian dos cadenas unidas con '~',
/// recorrian el arreglo y ejecutaban el procedimiento UNA VEZ POR ELEMENTO, sin
/// transaccion:
///   - si fallaba en la quinta de ocho, las cuatro anteriores quedaban escritas y
///     no habia forma de deshacerlas ni de saber por donde seguir;
///   - la respuesta se armaba con el result/mensaje de la ULTIMA vuelta, asi que
///     un fallo intermedio podia terminar contestando exito;
///   - si las dos listas tenian distinta longitud, reventaba con un indice fuera
///     de rango o —peor— apareaba el estatus de un elemento con el id de otro.
/// Aqui cada lote recibe una lista de pares y corre dentro de una transaccion.
/// </summary>
public class SeguridadMacRepository : ISeguridadMacRepository
{
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    /// <summary>
    /// Las columnas que escribe el UPDATE de sp_n_ActualizarUsuario y que hay que
    /// acarrear en una baja. Incluye la contrasena: sin acarrearla, dar de baja a
    /// un usuario le deja la contrasena vacia y, si alguien lo reactiva, entra sin
    /// contrasena.
    /// </summary>
    private static readonly BajaLogicaLegacy.Descriptor StatusUsuarioMac = new(
        "Usuarios", "IDUsuario", "@IDUsuario", "dbo.sp_n_ActualizarUsuario",
        new[]
        {
            ("@IDPerfil", "IDPerfil"),
            ("@NumUsuario", "NumUsuario"),
            ("@Usuario", "Usuario"),
            ("@Contrasena", "Contrasena"),
            ("@Nombre", "Nombre"),
            ("@AplicaContrasenaAutorizacion", "AplicaContrasenaAutorizacion"),
            ("@ContrasenaAutorizacion", "ContrasenaAutorizacion"),
            ("@Multicajas", "Multicajas")
        });

    private readonly IConfiguration _configuration;

    public SeguridadMacRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // =======================================================================
    // USUARIO DE MAC31
    // =======================================================================

    /*
      EL `SELECT MAX(IDUsuario)` LE DABA A UN CAPTURISTA EL USUARIO DEL OTRO.

      Lo que habia: se llamaba a sp_n_InsertarUsuario en una conexion, se cerraba,
      se abria OTRA y se preguntaba `SELECT ISNULL(MAX(IDUsuario), 0) FROM
      dbo.Usuarios`. El id que contestaba era el ultimo de la TABLA, no el del
      renglon que acababa de insertar esta peticion.

      Con dos capturistas dando de alta a la vez -que es lo normal en un alta
      masiva de empleados- al primero se le contesta el id del segundo. Y no
      termina ahi: la pantalla sigue con ese id y le escribe los ALMACENES y los
      PERMISOS al usuario equivocado. Se nota semanas despues, cuando alguien ve
      lo que no debia.

      COMO SE ARREGLA SIN TOCAR EL SP (prohibido editar un sp_n_):

      `IDUsuario` es columna IDENTITY en dbo.Usuarios (comprobado en las dos
      bases), asi que @@IDENTITY devuelve el id que generó ESTA SESION. La clave
      es que sea LA MISMA conexion: @@IDENTITY es por sesion, no global -a
      diferencia de IDENT_CURRENT, que tiene el mismo defecto que el MAX()-, asi
      que otro usuario insertando al mismo tiempo no lo mueve.

      No se usa SCOPE_IDENTITY() porque el INSERT ocurre DENTRO del procedimiento:
      es otro ambito, y desde aqui SCOPE_IDENTITY() devolveria NULL.

      Se comprobo que dbo.Usuarios no tiene disparadores en ninguna de las dos
      bases (sys.triggers vacio), que es la unica cosa que podria hacer que
      @@IDENTITY apuntara a otra tabla.

      Y se verifica ademas que el renglon leido es el del usuario que se pidio:
      si por lo que sea no cuadra, se contesta 0 en vez de un id ajeno. Es seguro
      porque el propio procedimiento rechaza nombres repetidos
      ("EL USUARIO YA ESTA REGISTRADO").
    */
    public async Task<(ResultadoLegacy, int)> InsertarUsuarioMacAsync(CrearUsuarioMacRequest r, CancellationToken ct)
    {
        var usuario = Texto(r.Usuario);

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        ResultadoLegacy resultado;
        await using (var cmd = Sp("dbo.sp_n_InsertarUsuario", conn))
        {
            cmd.Parameters.AddWithValue("@IDPerfil", r.IdPerfil);
            cmd.Parameters.AddWithValue("@NumUsuario", r.NumUsuario);
            cmd.Parameters.AddWithValue("@Usuario", usuario);
            cmd.Parameters.AddWithValue("@Contrasena", Texto(r.Contrasena));
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@AplicaContrasenaAutorizacion", r.AplicaContrasenaAutorizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@ContrasenaAutorizacion", Texto(r.ContrasenaAutorizacion));
            cmd.Parameters.AddWithValue("@Multicajas", r.Multicajas ? 1 : 0);

            var dt = await FillAsync(cmd, ct);
            resultado = BajaLogicaLegacy.LeerResultado(dt);
        }

        if (!resultado.Ok) return (resultado, 0);

        // MISMA conexion = misma sesion = @@IDENTITY es el de este INSERT.
        await using var cmdId = new SqlCommand(
            @"SELECT CONVERT(int, ISNULL(@@IDENTITY, 0))
              WHERE EXISTS (SELECT 1 FROM dbo.Usuarios
                            WHERE IDUsuario = CONVERT(int, @@IDENTITY)
                              AND Usuario = @Usuario);", conn);
        cmdId.Parameters.AddWithValue("@Usuario", usuario);

        var valor = await cmdId.ExecuteScalarAsync(ct);
        var id = valor is null or DBNull ? 0 : Convert.ToInt32(valor, CultureInfo.InvariantCulture);
        return (resultado, id);
    }

    public Task<ResultadoLegacy> ActualizarUsuarioMacAsync(int id, ActualizarUsuarioMacRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarUsuario", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUsuario", id);
            cmd.Parameters.AddWithValue("@IDPerfil", r.IdPerfil);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumUsuario", r.NumUsuario);
            cmd.Parameters.AddWithValue("@Usuario", Texto(r.Usuario));
            cmd.Parameters.AddWithValue("@Contrasena", Texto(r.Contrasena));
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@AplicaContrasenaAutorizacion", r.AplicaContrasenaAutorizacion ? 1 : 0);
            cmd.Parameters.AddWithValue("@ContrasenaAutorizacion", Texto(r.ContrasenaAutorizacion));
            cmd.Parameters.AddWithValue("@Multicajas", r.Multicajas ? 1 : 0);
        }, ct);

    public async Task<ResultadoLegacy?> CambiarStatusUsuarioMacAsync(int id, int idStatus, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        return await BajaLogicaLegacy.CambiarAsync(conn, StatusUsuarioMac, id, idStatus, 0, string.Empty, ct);
    }

    /// <summary>
    /// Los almacenes del usuario, en una transaccion.
    /// </summary>
    public Task<LoteSeguridadResponse> GuardarAlmacenesDeUsuarioAsync(
        int idUsuario, AlmacenesDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct)
        => EnLoteAsync(
            r.Almacenes.Count,
            "almacen",
            (i, cmd) =>
            {
                var a = r.Almacenes[i];
                cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
                cmd.Parameters.AddWithValue("@IDAlmacen", a.IdAlmacen);
                cmd.Parameters.AddWithValue("@IDStatus", a.IdStatus);
                cmd.Parameters.AddWithValue("@IDUsuarioMovimiento", idUsuarioMovimiento);
                cmd.Parameters.AddWithValue("@Equipo", equipo);
            },
            "dbo.sp_n_ActualizarUsuarioAlmacen",
            i => r.Almacenes[i].IdAlmacen.ToString(CultureInfo.InvariantCulture),
            ct);

    /// <summary>
    /// Los procesos del usuario, en una transaccion.
    ///
    /// Este procedimiento NO recibe @Equipo (los otros dos del lote si). Se manda
    /// lo que pide y nada mas.
    /// </summary>
    public Task<LoteSeguridadResponse> GuardarProcesosDeUsuarioAsync(
        int idUsuario, ProcesosDeUsuarioRequest r, int idUsuarioMovimiento, CancellationToken ct)
        => EnLoteAsync(
            r.Procesos.Count,
            "proceso",
            (i, cmd) =>
            {
                var p = r.Procesos[i];
                cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
                cmd.Parameters.AddWithValue("@IDProceso", p.IdProceso);
                cmd.Parameters.AddWithValue("@IDStatus", p.IdStatus);
                cmd.Parameters.AddWithValue("@IDUsuarioMovimiento", idUsuarioMovimiento);
            },
            "dbo.sp_n_ActualizarUsuarioFormaProceso",
            i => r.Procesos[i].IdProceso.ToString(CultureInfo.InvariantCulture),
            ct);

    /// <summary>
    /// Las fechas en las que el usuario puede operar, en una transaccion.
    ///
    /// La fecha se manda como 'MM-dd-yyyy 00:00:00', que es lo que armaba legacy
    /// invirtiendo dia y mes con posiciones de caracteres. Aqui es una fecha de
    /// verdad y se formatea; alla una cadena corta o vacia reventaba la llamada.
    /// </summary>
    public Task<LoteSeguridadResponse> GuardarFechasDeUsuarioAsync(
        int idUsuario, FechasDeUsuarioRequest r, int idUsuarioMovimiento, string equipo, CancellationToken ct)
        => EnLoteAsync(
            r.Fechas.Count,
            "fecha",
            (i, cmd) =>
            {
                var f = r.Fechas[i];
                cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
                cmd.Parameters.AddWithValue("@IDStatus", f.IdStatus);
                cmd.Parameters.AddWithValue("@Fecha", $"{f.Fecha.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture)} 00:00:00");
                cmd.Parameters.AddWithValue("@IDUsuarioMovimiento", idUsuarioMovimiento);
                cmd.Parameters.AddWithValue("@Equipo", equipo);
            },
            "dbo.sp_n_ActualizarUsuarioFecha",
            i => r.Fechas[i].Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ct);

    // =======================================================================
    // HUELLAS
    // =======================================================================

    /// <summary>
    /// Guarda la huella.
    ///
    /// LAS DOS PLANTILLAS VAN COMO VARBINARY(MAX) EXPLICITO. Legacy las pasaba con
    /// AddWithValue a secas y ADO.NET podia inferir un varbinary(8000),
    /// recortando la plantilla en silencio: una huella truncada no identifica a
    /// nadie y el error no aparece al guardarla, aparece semanas despues cuando
    /// alguien no puede checar.
    ///
    /// Y SE LEE EL RESULTADO DE VERDAD. El repositorio de legacy nunca asignaba
    /// result ni mensaje en este metodo —a diferencia de todos los demas— asi que
    /// la respuesta siempre salia con result 0 y mensaje vacio: el llamador no
    /// podia distinguir una huella guardada de una rechazada por el procedimiento.
    /// </summary>
    public async Task<ResultadoLegacy> GuardarHuellaAsync(
        GuardarHuellaRequest r, byte[] huella, byte[]? huella2, int idUsuarioMovimiento, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp("dbo.sp_n_ActualizarHuella", conn);
        cmd.Parameters.AddWithValue("@IDUsuario", r.IdUsuario);
        cmd.Parameters.AddWithValue("@IDEmpleado", r.IdEmpleado);
        cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
        cmd.Parameters.AddWithValue("@IDMano", r.IdMano);
        cmd.Parameters.AddWithValue("@IDDedo", r.IdDedo);
        cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);

        cmd.Parameters.Add(new SqlParameter("@Huella", SqlDbType.VarBinary, -1) { Value = huella });
        // Huella2 nula es el caso NORMAL (una sola plantilla). En legacy eso
        // reventaba la llamada con una excepcion de argumento nulo.
        cmd.Parameters.Add(new SqlParameter("@Huella2", SqlDbType.VarBinary, -1)
        {
            Value = huella2 is { Length: > 0 } ? huella2 : DBNull.Value
        });

        cmd.Parameters.AddWithValue("@IDUsuarioMovimiento", idUsuarioMovimiento);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        var dt = await FillAsync(cmd, ct);
        return BajaLogicaLegacy.LeerResultado(dt);
    }

    // =======================================================================
    // BITACORA DE CONTRASENAS DE AUTORIZACION
    // =======================================================================

    /// <summary>
    /// Apunta un intento de contrasena de autorizacion. Devuelve el id del
    /// renglon, que legacy leia y tiraba sin devolverlo.
    /// </summary>
    public Task<(ResultadoLegacy, int)> RegistrarContrasenaCapturadaAsync(
        RegistrarContrasenaCapturadaRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarContrasenaCodigo", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDForma", r.IdForma);
            cmd.Parameters.AddWithValue("@IDProceso", r.IdProceso);
            cmd.Parameters.AddWithValue("@Forma", Texto(r.Forma));
            cmd.Parameters.AddWithValue("@Correcto", r.Correcto ? 1 : 0);
            cmd.Parameters.AddWithValue("@Contrasena", Texto(r.Contrasena));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // CONSTANTES
    // =======================================================================

    /// <summary>
    /// Guarda los cuatro parametros de operacion. El procedimiento devuelve el
    /// renglon de constantes ya actualizado, que es lo que la pantalla vuelve a
    /// pintar.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ActualizarConstantesAsync(
        ActualizarConstantesRequest r, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ActualizarConstante", cmd =>
        {
            cmd.Parameters.AddWithValue("@MaximaDiferenciaUsados", r.MaximaDiferenciaUsados);
            cmd.Parameters.AddWithValue("@HorasLaborales", r.HorasLaborales);
            cmd.Parameters.AddWithValue("@DiasFolioConcentrado", r.DiasFolioConcentrado);
            cmd.Parameters.AddWithValue("@NumDiasPedidosRechazados", r.NumDiasPedidosRechazados);
        }, ct);

    /*
      LAS DOS FECHAS SALEN DEL MISMO PROCEDIMIENTO, Y LA CADENA VACIA NO ES NEUTRA.

      sp_n_ActualizarFechaOperacion decide la rama por @FechaOperacionPagos:
          IF @FechaOperacionPagos = ''   -> AVANZA LA FECHA DE VENTAS un dia,
                                            rehace el freeze de AlmacenProductoHist
                                            y apunta FechaOperacionLog tipo 'V'
          IF @FechaOperacionPagos <> ''  -> mueve la fecha de PAGOS
      Aqui decia que la cadena vacia era "no la cambies". No lo es: es la rutina de
      cierre del dia. Por eso el controlador ahora EXIGE la fecha de pagos, y el
      avance de la de ventas tiene su propio metodo con su propio permiso, en vez de
      salir por descuido de un cuerpo incompleto.

      Y la rama de pagos ramifica por empresa dentro del procedimiento:
          @Funcionalidad = 'TAU'  -> UPDATE Constantes.FechaOperacionPagos  (global)
          @Funcionalidad = 'ZARA' -> UPDATE [Usuarios Fechas]               (por usuario)
      De ahi la capacidad fechaDePagosEsPorUsuario que publica
      GET /api/empresa/capacidades.
    */
    public Task<List<Dictionary<string, object?>>> ActualizarFechaOperacionPagosAsync(
        ActualizarFechaOperacionPagosRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        if (!r.FechaOperacionPagos.HasValue)
            throw new ArgumentException(
                "La fecha de operacion de pagos es obligatoria: sin ella el procedimiento avanza la fecha de ventas.");

        return ConsultarAsync("dbo.sp_n_ActualizarFechaOperacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@FechaOperacionPagos",
                $"{r.FechaOperacionPagos.Value.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture)} 00:00:00");
            cmd.Parameters.AddWithValue("@Valido", r.Valido ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);
    }

    /// <summary>
    /// Avanza un dia la fecha de operacion de VENTAS: la cadena vacia en
    /// @FechaOperacionPagos es como legacy pide esta rama (Mac31 manda un
    /// ConstanteInputDTO sin fecha, FechaOperacionVentas.cs:49-52).
    ///
    /// @Valido va en 0 siempre: esa bandera solo afecta al tope inferior de la fecha
    /// de PAGOS y la rama de ventas no la mira.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> AvanzarFechaOperacionVentasAsync(
        int idUsuario, string equipo, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ActualizarFechaOperacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@FechaOperacionPagos", string.Empty);
            cmd.Parameters.AddWithValue("@Valido", 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Apunta que el usuario sigue trabajando. Es el latido de la sesion de
    /// Mac31: con el, la pantalla de usuarios conectados sabe quien esta dentro.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> RegistrarActividadAsync(
        int idUsuario, string equipo, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ActualizarUsuarioActividad", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // PARAMETROS DE REPORTE
    // =======================================================================

    /// <summary>
    /// Lee un renglon de parametros por su id.
    ///
    /// EL PROCEDIMIENTO SOLO FILTRA POR ID, y eso vale la pena saberlo: legacy
    /// exigia tambien el usuario y el nombre del equipo, los validaba... y no los
    /// mandaba. O sea que pedia tres datos y filtraba por uno, y cualquiera con un
    /// id podia leer los parametros guardados por otro equipo. Aqui se pide solo el
    /// id, que es lo que el procedimiento usa; el candado esta en el permiso, no en
    /// un filtro que no existe.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarParametrosAsync(int id, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaParametros",
            cmd => cmd.Parameters.AddWithValue("@ID", id), ct);

    /// <summary>
    /// Guarda los veinte parametros con los que se va a abrir un reporte. Los
    /// nulos se convierten a cadena vacia; en legacy cualquiera nulo tiraba la
    /// llamada con una excepcion de argumento nulo.
    ///
    /// El nombre del equipo sale del token, no del cuerpo: es parte de la llave con
    /// la que el reporte recupera sus parametros, y dejarlo viajar en el cuerpo
    /// permite escribir sobre los parametros de otra maquina.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> GuardarParametrosAsync(
        GuardarParametrosRequest r, int idUsuario, string equipo, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ActualizarParametro", cmd =>
        {
            cmd.Parameters.AddWithValue("@NombreEquipo", equipo);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@IDReporte", r.IdReporte);

            var valores = r.EnOrden();
            for (var i = 0; i < valores.Length; i++)
                cmd.Parameters.AddWithValue($"@Param{i + 1}", valores[i]);
        }, ct);

    // =======================================================================
    // Infraestructura
    // =======================================================================

    /// <summary>
    /// Ejecuta el procedimiento una vez por renglon DENTRO DE UNA TRANSACCION. Al
    /// primer rechazo deshace todo y devuelve el mensaje de legacy diciendo en que
    /// renglon fue.
    /// </summary>
    /// <param name="total">Cuantos renglones hay.</param>
    /// <param name="queEs">Como se llama el renglon en el mensaje de error ("almacen", "proceso", "fecha").</param>
    /// <param name="parametros">Pone los parametros del renglon i.</param>
    /// <param name="sp">El procedimiento.</param>
    /// <param name="etiqueta">Como identificar el renglon i en el mensaje.</param>
    private async Task<LoteSeguridadResponse> EnLoteAsync(
        int total,
        string queEs,
        Action<int, SqlCommand> parametros,
        string sp,
        Func<int, string> etiqueta,
        CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        for (var i = 0; i < total; i++)
        {
            await using var cmd = new SqlCommand(sp, conn, tx) { CommandType = CommandType.StoredProcedure };
            parametros(i, cmd);

            var dt = await FillAsync(cmd, ct);
            var resultado = BajaLogicaLegacy.LeerResultado(dt);

            if (!resultado.Ok)
            {
                await tx.RollbackAsync(ct);
                var detalle = string.IsNullOrWhiteSpace(resultado.Mensaje)
                    ? "la base lo rechazo"
                    : resultado.Mensaje;
                return new LoteSeguridadResponse
                {
                    Guardo = false,
                    Mensaje = $"No se guardo nada: en el {queEs} {etiqueta(i)}, {detalle}",
                    Aplicados = 0
                };
            }
        }

        await tx.CommitAsync(ct);
        return new LoteSeguridadResponse { Guardo = true, Mensaje = string.Empty, Aplicados = total };
    }

    private async Task<ResultadoLegacy> EjecutarAsync(string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return BajaLogicaLegacy.LeerResultado(dt);
    }

    private async Task<(ResultadoLegacy, int)> EjecutarConIdAsync(
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

    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

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
