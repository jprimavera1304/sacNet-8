using ISL_Service.Application.DTOs.CatalogosAdmin;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// Los catalogos de un solo renglon: agentes, almacenes, bancos, grupos de
/// categoria, unidades de medida, tipos de gasto, tipos de domicilio,
/// condiciones de credito, documentos, galleros, repartidores, tipos de
/// recordatorio y dias festivos.
///
/// Todos siguen el mismo molde de legacy: un sp_n_Insertar*, un
/// sp_n_Actualizar* que pisa todas las columnas, y un renglon result/mensaje de
/// salida. Lo unico que cambia entre uno y otro es la lista de parametros, asi
/// que aqui no hay abstraccion: hay listas de parametros, que es lo que se tiene
/// que poder leer de un golpe cuando alguien compare contra el SP.
/// </summary>
public partial class CatalogosAdminRepository
{
    // -----------------------------------------------------------------------
    // Agentes
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusAgente = new(
        "Agentes", "IDAgente", "@IDAgente", "dbo.sp_n_ActualizarAgente",
        new[] { ("@NumeroAgente", "Agente"), ("@NombreAgente", "Nombre") });

    public Task<(ResultadoLegacy, int)> InsertarAgenteAsync(CrearAgenteRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarAgente", "Agentes", "IDAgente", cmd =>
        {
            cmd.Parameters.AddWithValue("@NumeroAgente", r.NumeroAgente);
            cmd.Parameters.AddWithValue("@NombreAgente", Texto(r.NombreAgente));
        }, ct);

    public Task<ResultadoLegacy> ActualizarAgenteAsync(int id, ActualizarAgenteRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarAgente", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAgente", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NumeroAgente", r.NumeroAgente);
            cmd.Parameters.AddWithValue("@NombreAgente", Texto(r.NombreAgente));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusAgenteAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusAgente, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Almacenes
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusAlmacen = new(
        "Almacenes", "IDAlmacen", "@IDAlmacen", "dbo.sp_n_ActualizarAlmacen",
        new[] { ("@Almacen", "Almacen") });

    public Task<(ResultadoLegacy, int)> InsertarAlmacenAsync(string nombre, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarAlmacen", "Almacenes", "IDAlmacen",
            cmd => cmd.Parameters.AddWithValue("@Almacen", Texto(nombre)), ct);

    public Task<ResultadoLegacy> ActualizarAlmacenAsync(int id, string nombre, int idStatus, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarAlmacen", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDAlmacen", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Almacen", Texto(nombre));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusAlmacenAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusAlmacen, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Bancos
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusBanco = new(
        "[Catalogo Bancos]", "IDBanco", "@IDBanco", "dbo.sp_n_ActualizarBanco",
        new[] { ("@Banco", "Banco") });

    public Task<(ResultadoLegacy, int)> InsertarBancoAsync(string nombre, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarBanco", "[Catalogo Bancos]", "IDBanco",
            cmd => cmd.Parameters.AddWithValue("@Banco", Texto(nombre)), ct);

    public Task<ResultadoLegacy> ActualizarBancoAsync(int id, string nombre, int idStatus, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarBanco", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDBanco", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@Banco", Texto(nombre));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusBancoAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusBanco, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Grupos de categoria
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusGrupoCategoria = new(
        "GrupoCategorias", "IDGrupoCategoria", "@IDGrupoCategoria", "dbo.sp_n_ActualizarGrupoCategoria",
        new[] { ("@GrupoCategoria", "GrupoCategoria") });

    public Task<(ResultadoLegacy, int)> InsertarGrupoCategoriaAsync(string nombre, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarGrupoCategoria", "GrupoCategorias", "IDGrupoCategoria",
            cmd => cmd.Parameters.AddWithValue("@GrupoCategoria", Texto(nombre)), ct);

    public Task<ResultadoLegacy> ActualizarGrupoCategoriaAsync(int id, string nombre, int idStatus, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarGrupoCategoria", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGrupoCategoria", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@GrupoCategoria", Texto(nombre));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusGrupoCategoriaAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusGrupoCategoria, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Unidades de medida
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusUnidadMedida = new(
        "[Catalogo Unidades Medida]", "IDUnidadMedida", "@IDUnidadMedida", "dbo.sp_n_ActualizarUnidadMedida",
        new[] { ("@UnidadMedida", "UnidadMedida") });

    public Task<(ResultadoLegacy, int)> InsertarUnidadMedidaAsync(string nombre, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarUnidadMedida", "[Catalogo Unidades Medida]", "IDUnidadMedida",
            cmd => cmd.Parameters.AddWithValue("@UnidadMedida", Texto(nombre)), ct);

    public Task<ResultadoLegacy> ActualizarUnidadMedidaAsync(int id, string nombre, int idStatus, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarUnidadMedida", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDUnidadMedida", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@UnidadMedida", Texto(nombre));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusUnidadMedidaAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusUnidadMedida, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Tipos de gasto
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusTipoGasto = new(
        "[Catalogo Tipos Gasto]", "IDTipoGasto", "@IDTipoGasto", "dbo.sp_n_ActualizarTipoGasto",
        new[] { ("@TipoGasto", "TipoGasto") }, PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarTipoGastoAsync(string nombre, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarTipoGasto", "[Catalogo Tipos Gasto]", "IDTipoGasto", cmd =>
        {
            cmd.Parameters.AddWithValue("@TipoGasto", Texto(nombre));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarTipoGastoAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarTipoGasto", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoGasto", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@TipoGasto", Texto(nombre));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusTipoGastoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusTipoGasto, id, idStatus, idUsuario, equipo, ct);

    // -----------------------------------------------------------------------
    // Tipos de domicilio
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusTipoDomicilio = new(
        "[Catalogo Tipos Domicilio]", "IDTipoDomicilio", "@IDTipoDomicilio", "dbo.sp_n_Actualizar_TipoDomicilio",
        new[] { ("@TipoDomicilio", "TipoDomicilio") }, PideUsuario: true, PideEquipo: true);

    /// <summary>
    /// Alta de tipo de domicilio.
    ///
    /// DOS RAREZAS DE ESTE PROCEDIMIENTO, LAS DOS COMPROBADAS EN Produccion_svr.
    ///
    /// 1. NO DEVUELVE 1 EN LA COLUMNA 'result': devuelve el id nuevo
    ///    (SELECT @IDTipoDomicilio AS result). Funciona como exito solo porque
    ///    result > 0, y el id sale de donde nadie lo buscaria. Aqui se lee como
    ///    lo que es.
    ///
    /// 2. EL PROCEDIMIENTO ESTA ROTO EN ESTA BASE, Y DE LA PEOR FORMA. Inserta
    ///    bien en [Catalogo Tipos Domicilio] y despues, para leer el id, hace
    ///    "SELECT MAX(IDTipoDomicilio) FROM MC_TiposDomicilio" — una tabla que no
    ///    existe aqui. O sea que la fila SE GUARDA y la llamada revienta despues:
    ///    el usuario ve un error, vuelve a intentarlo y crea un duplicado. Lo
    ///    comprobe llamando el endpoint: la fila quedo con IDTipoDomicilio 3 y la
    ///    respuesta fue 500 "Invalid object name 'MC_TiposDomicilio'".
    ///
    ///    Legacy falla igual, y no se puede arreglar en el procedimiento porque
    ///    los sp_n_ no se tocan. Lo que si se puede es no mentirle al usuario: si
    ///    el error es EXACTAMENTE ese nombre de tabla, se comprueba si la fila
    ///    quedo y, si quedo, se contesta exito con su id. Cualquier otro error se
    ///    propaga tal cual. El filtro es estrecho a proposito: tragarse errores de
    ///    SQL en general taparia problemas de verdad.
    /// </summary>
    public async Task<(ResultadoLegacy, int)> InsertarTipoDomicilioAsync(string nombre, int idUsuario, string equipo, CancellationToken ct)
    {
        const string TablaQueFalta = "MC_TiposDomicilio";
        var limpio = Texto(nombre);

        try
        {
            var resultado = await EjecutarAsync("dbo.sp_n_Insertar_TipoDomicilio", cmd =>
            {
                cmd.Parameters.AddWithValue("@TipoDomicilio", limpio);
                cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
                cmd.Parameters.AddWithValue("@Equipo", equipo);
            }, ct);

            if (resultado.Result <= 0)
                return (resultado, 0);

            return (new ResultadoLegacy { Result = 1, Mensaje = resultado.Mensaje }, resultado.Result);
        }
        catch (SqlException ex) when (ex.Message.Contains(TablaQueFalta, StringComparison.OrdinalIgnoreCase))
        {
            // El INSERT del procedimiento ya corrio; lo que reventó fue el SELECT
            // del id. Si la fila esta, la operacion salio: se contesta con su id.
            var id = await IdDeTipoDomicilioAsync(limpio, ct);
            if (id > 0)
                return (new ResultadoLegacy { Result = 1, Mensaje = string.Empty }, id);

            // No quedo la fila: entonces si fue un fallo de verdad y se dice.
            throw;
        }
    }

    /// <summary>
    /// El id del tipo de domicilio por su nombre. Solo lo usa el alta, para
    /// recuperar el id que el procedimiento no pudo leer (ver arriba).
    /// </summary>
    private async Task<int> IdDeTipoDomicilioAsync(string nombre, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT ISNULL(MAX(IDTipoDomicilio), 0) FROM dbo.[Catalogo Tipos Domicilio] WHERE TipoDomicilio = @Nombre", conn);
        cmd.Parameters.AddWithValue("@Nombre", nombre);

        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0 : Convert.ToInt32(valor, System.Globalization.CultureInfo.InvariantCulture);
    }

    public Task<ResultadoLegacy> ActualizarTipoDomicilioAsync(int id, string nombre, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_Actualizar_TipoDomicilio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoDomicilio", id);
            cmd.Parameters.AddWithValue("@IDStatus", idStatus);
            cmd.Parameters.AddWithValue("@TipoDomicilio", Texto(nombre));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusTipoDomicilioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusTipoDomicilio, id, idStatus, idUsuario, equipo, ct);

    // -----------------------------------------------------------------------
    // Condiciones de credito
    // -----------------------------------------------------------------------

    // La llave de la tabla se llama IDCondicionesCredito (plural) y el parametro
    // del SP IDCondicionCredito (singular). No es un error de transcripcion:
    // legacy lo tiene asi.
    private static readonly CatalogoStatus StatusCondicionCredito = new(
        "[Catalogo Condiciones Credito]", "IDCondicionesCredito", "@IDCondicionCredito",
        "dbo.sp_n_ActualizarCondicionCredito",
        new[] { ("@CondicionCredito", "Condicion de Credito"), ("@DiasCredito", "Dias de Credito") });

    public Task<(ResultadoLegacy, int)> InsertarCondicionCreditoAsync(CrearCondicionCreditoRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarCondicionCredito",
            "[Catalogo Condiciones Credito]", "IDCondicionesCredito", cmd =>
        {
            cmd.Parameters.AddWithValue("@CondicionCredito", Texto(r.CondicionCredito));
            cmd.Parameters.AddWithValue("@DiasCredito", r.DiasCredito);
        }, ct);

    public Task<ResultadoLegacy> ActualizarCondicionCreditoAsync(int id, ActualizarCondicionCreditoRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCondicionCredito", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCondicionCredito", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@CondicionCredito", Texto(r.CondicionCredito));
            cmd.Parameters.AddWithValue("@DiasCredito", r.DiasCredito);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusCondicionCreditoAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusCondicionCredito, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Documentos
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusDocumento = new(
        "[Catalogo Documentos]", "IDDocumento", "@IDDocumento", "dbo.sp_n_ActualizarDocumento",
        new[] { ("@Documento", "Documento"), ("@Descripcion", "Descripcion"), ("@Obligatorio", "Obligatorio") },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarDocumentoAsync(CrearDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarDocumento", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@Documento", Texto(r.Documento));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@Obligatorio", r.Obligatorio ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarDocumento", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDDocumento", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Documento", Texto(r.Documento));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@Obligatorio", r.Obligatorio ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusDocumentoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusDocumento, id, idStatus, idUsuario, equipo, ct);

    // -----------------------------------------------------------------------
    // Galleros
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusGallero = new(
        "[Catalogo Galleros]", "IDGallero", "@IDGallero", "dbo.sp_n_ActualizarGallero",
        new[] { ("@Gallero", "Gallero"), ("@Saldo", "Saldo") }, PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarGalleroAsync(CrearGalleroRequest r, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarGallero", "[Catalogo Galleros]", "IDGallero", cmd =>
        {
            cmd.Parameters.AddWithValue("@Gallero", Texto(r.Gallero));
            cmd.Parameters.AddWithValue("@Saldo", r.Saldo);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Modificacion de gallero.
    ///
    /// El saldo NO se puede cambiar por aqui aunque el SP reciba @Saldo: el
    /// "Saldo = @Saldo" de sp_n_ActualizarGallero esta COMENTADO. Por eso el
    /// request no lo pide: ofrecerlo seria prometer un cambio de dinero que la
    /// base no va a hacer. Se manda el saldo ACTUAL para no depender de que el
    /// comentario siga ahi el dia que alguien lo descomente.
    /// </summary>
    public async Task<ResultadoLegacy> ActualizarGalleroAsync(int id, ActualizarGalleroRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        var saldo = await SaldoActualGalleroAsync(id, ct);

        return await EjecutarAsync("dbo.sp_n_ActualizarGallero", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDGallero", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Gallero", Texto(r.Gallero));
            cmd.Parameters.AddWithValue("@Saldo", saldo);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);
    }

    public Task<ResultadoLegacy?> CambiarStatusGalleroAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusGallero, id, idStatus, idUsuario, equipo, ct);

    private async Task<decimal> SaldoActualGalleroAsync(int id, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT ISNULL(Saldo, 0) FROM dbo.[Catalogo Galleros] WHERE IDGallero = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? 0m : Convert.ToDecimal(valor, System.Globalization.CultureInfo.InvariantCulture);
    }

    // -----------------------------------------------------------------------
    // Repartidores
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusRepartidor = new(
        "[Catalogo Repartidores]", "IDRepartidor", "@IDRepartidor", "dbo.sp_n_ActualizarRepartidor",
        new[] { ("@IDZona", "IDZona"), ("@Repartidor", "Repartidor") });

    public Task<(ResultadoLegacy, int)> InsertarRepartidorAsync(CrearRepartidorRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarRepartidor", "[Catalogo Repartidores]", "IDRepartidor", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDZona", r.IdZona);
            cmd.Parameters.AddWithValue("@Repartidor", Texto(r.Repartidor));
        }, ct);

    public Task<ResultadoLegacy> ActualizarRepartidorAsync(int id, ActualizarRepartidorRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarRepartidor", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDRepartidor", id);
            cmd.Parameters.AddWithValue("@IDZona", r.IdZona);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Repartidor", Texto(r.Repartidor));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusRepartidorAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusRepartidor, id, idStatus, 0, string.Empty, ct);

    // -----------------------------------------------------------------------
    // Tipos de recordatorio
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusTipoRecordatorio = new(
        "[Catalogo Tipos Recordatorio]", "IDTipoRecordatorio", "@IDTipoRecordatorio",
        "dbo.sp_n_ActualizarTipoRecordatorio",
        new[] { ("@TipoRecordatorio", "TipoRecordatorio"), ("@EsUrgente", "EsUrgente"), ("@EsImportante", "EsImportante") },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarTipoRecordatorioAsync(CrearTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarTipoRecordatorio", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@TipoRecordatorio", Texto(r.TipoRecordatorio));
            cmd.Parameters.AddWithValue("@EsUrgente", r.EsUrgente ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsImportante", r.EsImportante ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarTipoRecordatorioAsync(int id, ActualizarTipoRecordatorioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarTipoRecordatorio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoRecordatorio", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@TipoRecordatorio", Texto(r.TipoRecordatorio));
            cmd.Parameters.AddWithValue("@EsUrgente", r.EsUrgente ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsImportante", r.EsImportante ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusTipoRecordatorioAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusTipoRecordatorio, id, idStatus, idUsuario, equipo, ct);

    // -----------------------------------------------------------------------
    // Dias festivos
    // -----------------------------------------------------------------------

    private static readonly CatalogoStatus StatusDiaFestivo = new(
        "DiasFestivos", "IDDiaFestivo", "@IDDiaFestivo", "dbo.sp_n_ActualizarDiaFestivo",
        new[] { ("@DiaFestivo", "DiaFestivo") }, PideUsuario: true, PideEquipo: true);

    public async Task<List<Dictionary<string, object?>>> ConsultarDiasFestivosAsync(DiasFestivosFiltro filtro, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp("dbo.sp_n_ConsultaDiasFestivos", conn);
        cmd.Parameters.AddWithValue("@IDDiaFestivo", filtro.IdDiaFestivo);
        cmd.Parameters.AddWithValue("@IDStatus", filtro.IdStatus);
        cmd.Parameters.AddWithValue("@DiaFestivoInicial", AFechaLegacy(filtro.DiaFestivoInicial));
        cmd.Parameters.AddWithValue("@DiaFestivoFinal", AFechaLegacy(filtro.DiaFestivoFinal));

        var dt = await FillAsync(cmd, ct);
        return Funciones.DataTableToRows(dt);
    }

    public Task<(ResultadoLegacy, int)> InsertarDiaFestivoAsync(DateTime dia, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarDiaFestivo", "DiasFestivos", "IDDiaFestivo", cmd =>
        {
            cmd.Parameters.AddWithValue("@DiaFestivo", AFechaLegacy(dia));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarDiaFestivoAsync(int id, ActualizarDiaFestivoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarDiaFestivo", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDDiaFestivo", id);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@DiaFestivo", AFechaLegacy(r.DiaFestivo));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusDiaFestivoAsync(int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusDiaFestivo, id, idStatus, idUsuario, equipo, ct);

    // -----------------------------------------------------------------------
    // Ayudante de este archivo
    // -----------------------------------------------------------------------

    /// <summary>
    /// Alta cuyo SP no devuelve el id. Si legacy contesto exito se lee el MAX de
    /// la llave para poder contestar con la ubicacion del renglon nuevo.
    ///
    /// Se lee DESPUES del exito y nunca antes: no reserva nada y no se usa para
    /// decidir ningun valor. Es exactamente lo que ya hace
    /// GET api/usuarios/siguiente-numero de la ola 3.
    /// </summary>
    private async Task<(ResultadoLegacy, int)> InsertarConUltimoIdAsync(
        string sp, string tabla, string columnaId,
        Action<Microsoft.Data.SqlClient.SqlCommand> parametros, CancellationToken ct)
    {
        var resultado = await EjecutarAsync(sp, parametros, ct);
        if (!resultado.Ok) return (resultado, 0);

        var id = await UltimoIdAsync(tabla, columnaId, ct);
        return (resultado, id);
    }
}
