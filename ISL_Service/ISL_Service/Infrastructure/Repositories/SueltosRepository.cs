using System.Data;
using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Sueltos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los cuatro sueltos: proyectos de obra por consulta ciudadana, recordatorios,
/// servicios y la cola de sincronizacion de cajas.
///
/// OCHO DE LOS NUEVE PROCEDIMIENTOS DE ESTE ARCHIVO NO ESTAN EN Produccion_svr
/// (comprobado en sys.objects): los cuatro de proyectos, los tres de servicios y
/// el de sincronizacion. Legacy falla igual contra esta base. Se implementan con
/// los parametros exactos que manda MacServicios2 y la comprobacion queda
/// pendiente para la instalacion que si los tiene. Los tres de recordatorios si
/// existen.
/// </summary>
public class SueltosRepository : ISueltosRepository
{
    private const string FormatoFechaLegacy = "MM-dd-yyyy";

    private readonly IConfiguration _configuration;

    public SueltosRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // =======================================================================
    // RECORDATORIOS
    // =======================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarRecordatoriosAsync(
        RecordatoriosFiltro filtro, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaRecordatorios", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecordatorio", filtro.IdRecordatorio);
            cmd.Parameters.AddWithValue("@IDTipoRecordatorio", filtro.IdTipoRecordatorio);
            cmd.Parameters.AddWithValue("@IDStatusRecordatorio", filtro.IdStatusRecordatorio);
            cmd.Parameters.AddWithValue("@Recordatorio", Texto(filtro.Recordatorio));
            cmd.Parameters.AddWithValue("@EsUrgente", filtro.EsUrgente);
            cmd.Parameters.AddWithValue("@EsImportante", filtro.EsImportante);
            cmd.Parameters.AddWithValue("@FechaInicial", ADesde(filtro.Desde));
            cmd.Parameters.AddWithValue("@FechaFinal", AHasta(filtro.Hasta));
        }, ct);

    public Task<(ResultadoLegacy, int)> InsertarRecordatorioAsync(
        CrearRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarRecordatorio", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoRecordatorio", r.IdTipoRecordatorio);
            cmd.Parameters.AddWithValue("@Recordatorio", Texto(r.Recordatorio));
            cmd.Parameters.AddWithValue("@FechaLimite", AFecha(r.FechaLimite));
            cmd.Parameters.AddWithValue("@FechaEspecifica", AFecha(r.FechaEspecifica));
            cmd.Parameters.AddWithValue("@EsUrgente", r.EsUrgente ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsImportante", r.EsImportante ? 1 : 0);
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarRecordatorioAsync(
        int id, ActualizarRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarRecordatorio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRecordatorio", id);
            cmd.Parameters.AddWithValue("@IDTipoRecordatorio", r.IdTipoRecordatorio);
            cmd.Parameters.AddWithValue("@IDStatusRecordatorio", r.IdStatusRecordatorio);
            cmd.Parameters.AddWithValue("@Recordatorio", Texto(r.Recordatorio));
            cmd.Parameters.AddWithValue("@FechaLimite", AFecha(r.FechaLimite));
            cmd.Parameters.AddWithValue("@FechaEspecifica", AFecha(r.FechaEspecifica));
            cmd.Parameters.AddWithValue("@EsUrgente", r.EsUrgente ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsImportante", r.EsImportante ? 1 : 0);
            cmd.Parameters.AddWithValue("@FechaTerminado", AFecha(r.FechaTerminado));
            cmd.Parameters.AddWithValue("@FechaCancelacion", AFecha(r.FechaCancelacion));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // SERVICIOS
    // =======================================================================

    /// <summary>
    /// Consulta de servicios.
    ///
    /// Si viene el folio inicial se busca por rango de folios y se conserva solo la
    /// empresa: es la precedencia de legacy. Alla tenia un defecto de los que no se
    /// ven: el campo del folio era texto sin valor por omision, asi que una consulta
    /// que no mandara folio entraba igual a esa rama (null != "") y mandaba nulo al
    /// procedimiento, tirando los filtros que el usuario si habia puesto.
    ///
    /// DOS PARAMETROS DEL PROCEDIMIENTO ESTAN MAL ESCRITOS EN LEGACY y se
    /// reproducen tal cual: @echaRecepcionFinal y @echaRegistroInicial, sin la F.
    /// No es un error de transcripcion: si el procedimiento no los tuviera asi, la
    /// llamada de MacServicios2 fallaria siempre. El procedimiento no esta en esta
    /// base para confirmarlo, asi que se manda lo que manda legacy — cambiarlo por
    /// el nombre "correcto" seria romper la llamada en la instalacion donde hoy
    /// funciona.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarServiciosAsync(
        ServiciosFiltro filtro, CancellationToken ct)
    {
        var porFolio = filtro.FolioInicial > 0;

        return ConsultarAsync("dbo.sp_n_ConsultaServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDServicio", porFolio ? 0 : filtro.IdServicio);
            cmd.Parameters.AddWithValue("@IDEmpresa", filtro.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDCliente", porFolio ? 0 : filtro.IdCliente);
            cmd.Parameters.AddWithValue("@IDRepartidor", porFolio ? 0 : filtro.IdRepartidor);
            cmd.Parameters.AddWithValue("@IDUsuario", porFolio ? 0 : filtro.IdUsuario);
            cmd.Parameters.AddWithValue("@FolioInicial", filtro.FolioInicial);
            cmd.Parameters.AddWithValue("@FolioFinal", filtro.FolioFinal);
            cmd.Parameters.AddWithValue("@FechaRecepcionInicial", porFolio ? string.Empty : ADesde(filtro.RecepcionDesde));
            // Los dos nombres truncados son de legacy, no de aqui.
            cmd.Parameters.AddWithValue("@echaRecepcionFinal", porFolio ? string.Empty : AHasta(filtro.RecepcionHasta));
            cmd.Parameters.AddWithValue("@echaRegistroInicial", porFolio ? string.Empty : ADesde(filtro.RegistroDesde));
            cmd.Parameters.AddWithValue("@FechaRegistroFinal", porFolio ? string.Empty : AHasta(filtro.RegistroHasta));
            cmd.Parameters.AddWithValue("@FechaCancelInicial", porFolio ? string.Empty : ADesde(filtro.CancelacionDesde));
            cmd.Parameters.AddWithValue("@FechaCancelFinal", porFolio ? string.Empty : AHasta(filtro.CancelacionHasta));
            // @Formato era el interruptor "0 todos los campos / 1 Excel" de
            // WinForms. Siempre 0: el Excel lo arma el front con los mismos datos.
            cmd.Parameters.AddWithValue("@Formato", 0);
        }, ct);
    }

    /// <summary>
    /// Alta de servicio. La fecha de recepcion se formatea como en todos los demas
    /// endpoints; legacy era el unico que mandaba la cadena cruda del cliente.
    /// </summary>
    public Task<(ResultadoLegacy, List<Dictionary<string, object?>>)> InsertarServicioAsync(
        CrearServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConRenglonesAsync("dbo.sp_n_InsertarServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDEmpresa", r.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDProducto", r.IdProducto);
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdRepartidor);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Cantidad", r.Cantidad);
            cmd.Parameters.AddWithValue("@FechaRecepcion", AFecha(r.FechaRecepcion));
            cmd.Parameters.AddWithValue("@Importe", r.Importe);
            cmd.Parameters.AddWithValue("@Motivo", Texto(r.Motivo));
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Cancela el servicio.
    ///
    /// AQUI SE RECONSTRUYE UNA LLAMADA QUE EN LEGACY NO EXISTE. El repositorio de
    /// MacServicios2 ejecuta sp_n_CancelarServicio SIN UN SOLO PARAMETRO: sus tres
    /// AddWithValue estan comentados, y los nombres que usaban eran de otro modulo
    /// (@IDAjusteInventario). O sea que ese endpoint cancela "el servicio" sin decir
    /// cual. Tampoco valida nada: todo su bloque de validaciones esta comentado.
    ///
    /// El procedimiento no esta en Produccion_svr, asi que no hay forma de leer su
    /// firma. Se manda lo que la operacion necesita para significar algo —que
    /// servicio, quien lo cancela y desde donde— y queda marcado como NO
    /// VERIFICABLE: hay que confirmarlo contra la instalacion que si tiene el
    /// procedimiento antes de darlo por bueno. Mandar cero parametros, como hace
    /// legacy, seria copiar un endpoint que no puede funcionar.
    /// </summary>
    public Task<ResultadoLegacy> CancelarServicioAsync(
        int idServicio, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_CancelarServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDServicio", idServicio);
            cmd.Parameters.AddWithValue("@IDUsuarioCancelacion", idUsuario);
            cmd.Parameters.AddWithValue("@EquipoCancelacion", equipo);
        }, ct);

    // =======================================================================
    // SINCRONIZACION
    // =======================================================================

    /// <summary>
    /// La cola de sincronizacion de cajas.
    ///
    /// SIEMPRE CONTRA LA BASE DE ESTE BACKEND. Legacy tenia una bandera booleana en
    /// el cuerpo que, en true, reemplazaba la conexion por la cadena guardada en
    /// Constantes.AviacionDBServer: el cliente elegia la base destino desde el JSON.
    /// Eso no se hereda. La base a la que apunta el backend no la decide quien
    /// llama; si hace falta consultar la cola de otra instalacion, se consulta desde
    /// el backend de esa instalacion.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarSincronizacionAsync(
        SincronizacionFiltro filtro, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaSincronizacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCaja", filtro.IdCaja);
            cmd.Parameters.AddWithValue("@IDStatusSincronizacion", filtro.IdStatusSincronizacion);
            cmd.Parameters.AddWithValue("@IDTabla", filtro.IdTabla);
            cmd.Parameters.AddWithValue("@NumRegistros", filtro.NumRegistros);
            cmd.Parameters.AddWithValue("@UltimaSincronizacion", filtro.UltimaSincronizacion);
        }, ct);

    // =======================================================================
    // PROYECTOS
    // =======================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarProyectosAsync(
        ProyectosFiltro filtro, CancellationToken ct)
    {
        var clave = Texto(filtro.Clave);
        var porClave = clave.Length > 0;

        return ConsultarAsync("dbo.sp_n_Consultar_Proyectos", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProyecto", porClave ? 0 : filtro.IdProyecto);
            cmd.Parameters.AddWithValue("@IDUnidadTerritorial", porClave ? 0 : filtro.IdUnidadTerritorial);
            cmd.Parameters.AddWithValue("@IDContratista", porClave ? 0 : filtro.IdContratista);
            cmd.Parameters.AddWithValue("@IDStatus", porClave ? 0 : filtro.IdStatus);
            cmd.Parameters.AddWithValue("@Clave", clave);
            cmd.Parameters.AddWithValue("@FolioGanador", porClave ? string.Empty : Texto(filtro.FolioGanador));
            cmd.Parameters.AddWithValue("@NombreGanador", porClave ? string.Empty : Texto(filtro.NombreGanador));
            cmd.Parameters.AddWithValue("@Contrato", porClave ? string.Empty : Texto(filtro.Contrato));
            cmd.Parameters.AddWithValue("@FechaContratacionInicial", porClave ? string.Empty : ADesde(filtro.ContratacionDesde));
            cmd.Parameters.AddWithValue("@FechaContratacionFinal", porClave ? string.Empty : AHasta(filtro.ContratacionHasta));
        }, ct);
    }

    public Task<(ResultadoLegacy, int)> InsertarProyectoAsync(
        ProyectoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_Insertar_Proyecto", "IDResult", cmd =>
        {
            // El alta no recibe el estatus desde el cuerpo: nace activo.
            cmd.Parameters.AddWithValue("@IDStatus", 1);
            ParametrosProyecto(cmd, r, idUsuario, equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarProyectoAsync(
        int id, ActualizarProyectoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_Actualizar_Proyecto", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProyecto", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            ParametrosProyecto(cmd, r, idUsuario, equipo);
        }, ct);

    /// <summary>
    /// Las cinco facturas del proyecto. La lista se reparte en orden en los diez
    /// parametros fijos del procedimiento; los huecos van en 0, que es lo que
    /// legacy manda cuando el campo no viene.
    /// </summary>
    public Task<ResultadoLegacy> ActualizarFacturasProyectoAsync(
        int id, FacturasProyectoRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_Actualizar_Proyecto_Facturas", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProyecto", id);

            for (var i = 0; i < 5; i++)
            {
                var f = i < r.Facturas.Count ? r.Facturas[i] : new FacturaProyecto();
                cmd.Parameters.AddWithValue($"@CuentaConFactura{i + 1}", f.CuentaConFactura ? 1 : 0);
                cmd.Parameters.AddWithValue($"@MontoFactura{i + 1}", f.Monto);
            }
        }, ct);

    /// <summary>
    /// Los parametros que comparten el alta y la modificacion del proyecto, en el
    /// orden del procedimiento.
    ///
    /// NO SE MANDAN los campos *Docto del DTO de legacy (FolioGanadorDocto,
    /// ContratoDocto, los cinco EnvioEvidenciasFotoDocto...): ningun procedimiento
    /// de escritura los recibe. Tampoco @IDUnidadTerritorial, que solo se usa al
    /// consultar.
    /// </summary>
    private static void ParametrosProyecto(SqlCommand cmd, ProyectoRequest r, int idUsuario, string equipo)
    {
        cmd.Parameters.AddWithValue("@IDAlcaldia", r.IdAlcaldia);
        cmd.Parameters.AddWithValue("@IDContratista", r.IdContratista);
        cmd.Parameters.AddWithValue("@Clave", Texto(r.Clave));
        cmd.Parameters.AddWithValue("@Denominacion", Texto(r.Denominacion));
        cmd.Parameters.AddWithValue("@CuentaConFolioGanador", r.CuentaConFolioGanador ? 1 : 0);
        cmd.Parameters.AddWithValue("@FolioGanador", Texto(r.FolioGanador));
        cmd.Parameters.AddWithValue("@UbicacionGanador", Texto(r.UbicacionGanador));
        cmd.Parameters.AddWithValue("@GeolocalizacionGanador", Texto(r.GeolocalizacionGanador));
        cmd.Parameters.AddWithValue("@NombreGanador", Texto(r.NombreGanador));
        cmd.Parameters.AddWithValue("@DescripcionGanador", Texto(r.DescripcionGanador));
        cmd.Parameters.AddWithValue("@SeEjecutoElProyectoConsulta", r.SeEjecutoElProyectoConsulta ? 1 : 0);
        cmd.Parameters.AddWithValue("@FechaAsambleaAutorizo", AFecha(r.FechaAsambleaAutorizo));
        cmd.Parameters.AddWithValue("@FechaDictaminacion", AFecha(r.FechaDictaminacion));
        cmd.Parameters.AddWithValue("@CuentaConFolioSustituto", r.CuentaConFolioSustituto ? 1 : 0);
        cmd.Parameters.AddWithValue("@FolioSustituto", Texto(r.FolioSustituto));
        cmd.Parameters.AddWithValue("@UbicacionSustituto", Texto(r.UbicacionSustituto));
        // Legacy saneaba todos los textos menos este, por olvido. Aqui ninguno se
        // mutila: van como parametro a un INSERT/UPDATE, no concatenados.
        cmd.Parameters.AddWithValue("@GeolocalizacionSustituto", Texto(r.GeolocalizacionSustituto));
        cmd.Parameters.AddWithValue("@NombreSustituto", Texto(r.NombreSustituto));
        cmd.Parameters.AddWithValue("@DescripcionSustituto", Texto(r.DescripcionSustituto));
        cmd.Parameters.AddWithValue("@CuentaConContrato", r.CuentaConContrato ? 1 : 0);
        cmd.Parameters.AddWithValue("@Contrato", Texto(r.Contrato));
        cmd.Parameters.AddWithValue("@FechaInicioContrato", AFecha(r.FechaInicioContrato));
        cmd.Parameters.AddWithValue("@FechaTerminoContrato", AFecha(r.FechaTerminoContrato));
        cmd.Parameters.AddWithValue("@CapituloGasto", Texto(r.CapituloGasto));
        cmd.Parameters.AddWithValue("@PresupuestoAsignadoProyecto", r.PresupuestoAsignadoProyecto);
        cmd.Parameters.AddWithValue("@Contratado", r.Contratado);
        cmd.Parameters.AddWithValue("@NumFactura", Texto(r.NumFactura));
        cmd.Parameters.AddWithValue("@MontoFactura", r.MontoFactura);
        cmd.Parameters.AddWithValue("@Ejercido", r.Ejercido);
        cmd.Parameters.AddWithValue("@SaldoDelCompromiso", r.SaldoDelCompromiso);
        cmd.Parameters.AddWithValue("@AvancePresupuestal", r.AvancePresupuestal);
        cmd.Parameters.AddWithValue("@Economia", r.Economia);
        cmd.Parameters.AddWithValue("@CuentaConFechaActaEntregaProyecto", r.CuentaConFechaActaEntregaProyecto ? 1 : 0);
        cmd.Parameters.AddWithValue("@FechaActaEntregaProyecto", AFecha(r.FechaActaEntregaProyecto));
        cmd.Parameters.AddWithValue("@CuentaConFechaEntregaTrabajos", r.CuentaConFechaEntregaTrabajos ? 1 : 0);
        cmd.Parameters.AddWithValue("@FechaEntregaTrabajos", AFecha(r.FechaEntregaTrabajos));
        cmd.Parameters.AddWithValue("@AreaResponsable", Texto(r.AreaResponsable));
        cmd.Parameters.AddWithValue("@AvanceEjecucion", r.AvanceEjecucion);
        cmd.Parameters.AddWithValue("@EnvioEvidenciasFoto", r.EnvioEvidenciasFoto ? 1 : 0);
        cmd.Parameters.AddWithValue("@NumEvidenciasFoto", r.NumEvidenciasFoto);
        cmd.Parameters.AddWithValue("@CuentaConEvidenciasFoto1", r.CuentaConEvidenciasFoto1 ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaConEvidenciasFoto2", r.CuentaConEvidenciasFoto2 ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaConEvidenciasFoto3", r.CuentaConEvidenciasFoto3 ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaConEvidenciasFoto4", r.CuentaConEvidenciasFoto4 ? 1 : 0);
        cmd.Parameters.AddWithValue("@CuentaConEvidenciasFoto5", r.CuentaConEvidenciasFoto5 ? 1 : 0);
        cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);
    }

    // =======================================================================
    // Infraestructura
    // =======================================================================

    private static string AFecha(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString(FormatoFechaLegacy, CultureInfo.InvariantCulture) : string.Empty;

    private static string ADesde(DateTime? fecha)
        => fecha.HasValue ? $"{AFecha(fecha)} 00:00:00" : string.Empty;

    private static string AHasta(DateTime? fecha)
        => fecha.HasValue ? $"{AFecha(fecha)} 23:59:59" : string.Empty;

    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

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

    /// <summary>
    /// Para el alta de servicio, cuyo procedimiento devuelve el renglon completo
    /// (con el folio asignado) ademas del result/mensaje.
    /// </summary>
    private async Task<(ResultadoLegacy, List<Dictionary<string, object?>>)> EjecutarConRenglonesAsync(
        string sp, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp(sp, conn);
        parametros(cmd);

        var dt = await FillAsync(cmd, ct);
        return (BajaLogicaLegacy.LeerResultado(dt), Funciones.DataTableToRows(dt));
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
