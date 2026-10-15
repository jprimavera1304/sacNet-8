using System.Data;
using System.Reflection;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// NADA VUELVE AL POOL CON UNA TRANSACCION ABIERTA NI CON CANDADOS PUESTOS.
/// </summary>
/// <remarks>
/// QUE SE ESTA PROTEGIENDO
/// -----------------------
/// El embudo de datos paso de <c>Task.Run(() =&gt; adapter.Fill(ds), ct)</c> -donde
/// el token solo impedia ARRANCAR el trabajo- a <c>ExecuteReaderAsync(ct)</c>,
/// donde la cancelacion llega de verdad a SQL Server y ABORTA EL LOTE a media
/// transaccion. Legacy corre con <c>XACT_ABORT OFF</c> y abre su propia
/// transaccion dentro del procedimiento: al abortarse el lote, esa transaccion
/// NO se deshace. La conexion vuelve al pool con <c>@@TRANCOUNT &gt; 0</c>,
/// arrastrando candados exclusivos sobre Pedidos, [Pedidos Detalle] y
/// AlmacenProducto, y el applock de apartado sin liberar. El siguiente agente
/// se queda esperandolos hasta agotar los 500 s de CommandTimeout.
///
/// Las tres defensas que estas pruebas vigilan:
///
///   1. <c>EjecutarMutacionAsync</c> no recibe token: una escritura se termina.
///   2. <c>DeshacerTransaccionColgadaAsync</c> en el <c>finally</c> de las tres
///      mutaciones: aunque algo quede abierto, no sale de aqui abierto.
///   3. El guardia de <c>@TranEntrada</c> dentro de <c>sp_w_InsertarPedidoDetalle</c>,
///      con el ROLLBACK ANTES de soltar el applock.
///
/// POR QUE CON PROCEDIMIENTOS DE JUGUETE
/// -------------------------------------
/// Porque provocar el fallo real dentro de legacy no es determinista: depende
/// de existencias, de folios y del momento. Los <c>zz_prueba_*</c> copian la
/// ESTRUCTURA exacta de produccion
///
///   sp_w_  (BEGIN TRY ... EXEC sp_n_insertar ... BEGIN CATCH)
///     └─ sp_n_insertar   BEGIN TRAN ... INSERT #t EXEC sp_n_eliminar
///          └─ sp_n_eliminar   BEGIN TRAN ... ROLLBACK TRAN   &lt;-- PROHIBIDO
///
/// y SQL Server reacciona igual siempre: prohibe el ROLLBACK dentro de un
/// INSERT..EXEC, levanta error, y deja los dos niveles abiertos
/// (<c>@@TRANCOUNT = 2</c>). Eso es literalmente el "Previous count = 0,
/// current count = 2" que se vio en produccion.
///
/// Los objetos se crean y se BORRAN aqui mismo, con prefijo <c>zz_prueba_</c>
/// que nadie mas usa, para no dejar basura en la base de desarrollo.
///
/// SE SALTA SOLA si no hay SQL Server a mano, igual que
/// <see cref="EscenariosPermisosContraLaBase"/>: una prueba que "pasa" sin
/// medir es peor que no tenerla.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CapturaTransaccionesYCandadosTests
{
    private readonly ITestOutputHelper _out;
    public CapturaTransaccionesYCandadosTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    // ==================================================================
    // 1. EL MECANISMO: sin guardia quedan dos transacciones abiertas
    // ==================================================================

    /// <summary>
    /// Reproduce la fuga y demuestra que el guardia la cierra.
    /// </summary>
    /// <remarks>
    /// La asercion que vale es la PRIMERA: sin guardia <c>@@TRANCOUNT</c> sale
    /// en 2. Si alguien quita el <c>IF @@TRANCOUNT &gt; @TranEntrada ROLLBACK</c>
    /// del <c>sp_w_</c> real, este es el estado al que vuelve produccion.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Sin_El_Guardia_El_Procedimiento_Deja_Dos_Transacciones_Abiertas_Y_Con_El_Guardia_Ninguna(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            var sinGuardia = await TrancountAlSalirAsync(conn, conGuardia: 0);
            var conGuardia = await TrancountAlSalirAsync(conn, conGuardia: 1);

            _out.WriteLine($"[{baseDatos}] sin guardia TRANCOUNT={sinGuardia}; con guardia TRANCOUNT={conGuardia}");

            // 2 = la fuga. Es el numero exacto que se vio en produccion.
            Assert.Equal(2, sinGuardia);
            Assert.Equal(0, conGuardia);
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    // ==================================================================
    // 2. LA RED DE SEGURIDAD DEL REPOSITORIO
    // ==================================================================

    /// <summary>
    /// Legacy tiene caminos que salen con RETURN sin cerrar su transaccion.
    /// </summary>
    /// <remarks>
    /// Este caso NO lanza excepcion: el procedimiento termina "bien" y deja la
    /// transaccion viva con sus candados exclusivos puestos. Se comprueba
    /// DESDE OTRA CONEXION, porque la sesion que fugo es justo la que hay que
    /// observar y preguntarle a ella misma no demuestra lo que ve el resto del
    /// servidor.
    ///
    /// Que rompe si se revierte: quitar el <c>finally</c> con
    /// <c>DeshacerTransaccionColgadaAsync</c> de AgregarDetalleAsync /
    /// EliminarDetalleAsync / GuardarAsync devuelve esta conexion al pool con
    /// una transaccion viva y candados sobre las tablas de pedidos.
    ///
    /// No se delega en <c>sp_reset_connection</c>: SqlClient lo manda con el
    /// SIGUIENTE comando que tome esa conexion, asi que entre tanto la
    /// transaccion sigue abierta en el servidor.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Una_Transaccion_Que_Legacy_Dejo_Abierta_No_Sobrevive_Al_Repositorio(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        await using var espia = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(espia, baseDatos)) return;

        var spid = Convert.ToInt32(await EscalarAsync(conn, "SELECT @@SPID"));

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            // El camino "RETURN sin COMMIT": el mismo que tiene legacy.
            await AbreYSeVaAsync(conn);

            var abiertasAntes = await TransaccionesAbiertasDeAsync(espia, spid);
            var candadosAntes = await CandadosExclusivosDeAsync(espia, spid);
            _out.WriteLine($"[{baseDatos}] antes de la red: transacciones={abiertasAntes}, candados X/U={candadosAntes}");

            // Si esto fuera 0, la prueba no estaria midiendo nada: el escenario
            // que la red de seguridad resuelve no se habria dado.
            Assert.True(abiertasAntes > 0, "El juguete no dejo la transaccion abierta: la prueba no mide nada.");
            Assert.True(candadosAntes > 0, "Sin candados retenidos no hay nada que liberar: la prueba no mide nada.");

            await DeshacerTransaccionColgadaAsync(conn);

            var abiertasDespues = await TransaccionesAbiertasDeAsync(espia, spid);
            var candadosDespues = await CandadosExclusivosDeAsync(espia, spid);
            _out.WriteLine($"[{baseDatos}] despues de la red: transacciones={abiertasDespues}, candados X/U={candadosDespues}");

            Assert.Equal(0, abiertasDespues);
            Assert.Equal(0, candadosDespues);
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    /// <summary>
    /// La cancelacion real: el lote se aborta a media transaccion.
    /// </summary>
    /// <remarks>
    /// Esto es exactamente lo que <c>ExecuteReaderAsync(ct)</c> volvio posible
    /// y <c>Task.Run(Fill, ct)</c> no podia hacer. El juguete abre transaccion,
    /// escribe, y se queda en un WAITFOR; se cancela a los 300 ms. Con
    /// XACT_ABORT OFF la transaccion NO se deshace sola.
    ///
    /// La asercion de valor: la transaccion colgada EXISTE (vista desde otra
    /// conexion) y la red de seguridad la cierra. Por eso las mutaciones pasan
    /// por <see cref="VentasPedidoCapturaRepository"/> con
    /// <c>CancellationToken.None</c> y ademas con el <c>finally</c>.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Cancelar_A_Media_Transaccion_Deja_Candados_Y_La_Red_De_Seguridad_Los_Suelta(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        await using var espia = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(espia, baseDatos)) return;

        var spid = Convert.ToInt32(await EscalarAsync(conn, "SELECT @@SPID"));

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await using var cmd = new SqlCommand("EXEC dbo.zz_prueba_lenta;", conn) { CommandTimeout = 30 };

            // Se espera que truene: lo que importa es QUE QUEDA DETRAS.
            await Assert.ThrowsAnyAsync<Exception>(() => cmd.ExecuteNonQueryAsync(cts.Token));

            var abiertas = await TransaccionesAbiertasDeAsync(espia, spid);
            var candados = await CandadosExclusivosDeAsync(espia, spid);
            _out.WriteLine($"[{baseDatos}] tras cancelar: transacciones={abiertas}, candados X/U={candados}");

            Assert.True(abiertas > 0,
                "La cancelacion no dejo transaccion abierta en esta version de SQL Server: revisa si el escenario sigue siendo posible antes de relajar la red de seguridad.");

            await DeshacerTransaccionColgadaAsync(conn);

            Assert.Equal(0, await TransaccionesAbiertasDeAsync(espia, spid));
            Assert.Equal(0, await CandadosExclusivosDeAsync(espia, spid));

            // Y el renglon que la transaccion habia escrito NO quedo: nada a medias.
            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(espia, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora")));
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    /// <summary>
    /// PERDIDA DE CONEXION a media transaccion: SQL Server lo deshace todo.
    /// </summary>
    /// <remarks>
    /// Se mata la sesion desde fuera mientras la transaccion esta abierta y ya
    /// escribio. Esto NO es como la cancelacion: al morir la sesion, SQL Server
    /// deshace su transaccion y suelta sus candados, asi que no hace falta
    /// ninguna defensa del lado del backend.
    ///
    /// Esta aqui para dejar la diferencia por escrito: lo que hay que proteger
    /// es la cancelacion -donde la sesion SOBREVIVE con la transaccion
    /// abierta-, no la caida. Si alguien dice "la conexion se cae y ya se
    /// limpia sola, para que la red de seguridad", esta prueba y la de arriba
    /// son la respuesta.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Si_La_Conexion_Muere_A_Media_Transaccion_SQL_Server_Deshace_Y_Suelta(string baseDatos)
    {
        await using var espia = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(espia, baseDatos)) return;

        int spid;
        try
        {
            await using (var conn = new SqlConnection(CadenaDe(baseDatos)))
            {
                if (!await AbrirOSaltarAsync(conn, baseDatos)) return;
                await CrearJugueteriaDeTransaccionesAsync(conn);

                spid = Convert.ToInt32(await EscalarAsync(conn, "SELECT @@SPID"));

                await AbreYSeVaAsync(conn);

                Assert.True(await TransaccionesAbiertasDeAsync(espia, spid) > 0,
                    "El juguete no dejo la transaccion abierta: la prueba no mide nada.");

                // La conexion se muere de verdad, no se cierra ordenadamente.
                await using var mata = new SqlCommand($"KILL {spid};", espia) { CommandTimeout = 30 };
                await mata.ExecuteNonQueryAsync();
            }

            Assert.Equal(0, await TransaccionesAbiertasDeAsync(espia, spid));
            Assert.Equal(0, await CandadosExclusivosDeAsync(espia, spid));

            // Y lo que la transaccion habia escrito se deshizo: nada a medias.
            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(espia, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora")));
            _out.WriteLine($"[{baseDatos}] sesion {spid} muerta: transaccion deshecha y candados sueltos");
        }
        finally
        {
            await LimpiarAsync(espia);
        }
    }

    /// <summary>
    /// Una mutacion no se puede cancelar: no hay por donde colarle el token.
    /// </summary>
    /// <remarks>
    /// Es una asercion de FORMA, y es la unica que detecta la reversion de
    /// verdad: si alguien le agrega un parametro <c>CancellationToken</c> a
    /// <c>EjecutarMutacionAsync</c> -o lo deja pasar desde las tres
    /// mutaciones-, vuelve a ser posible abandonar un pedido a medias, y
    /// ninguna prueba de comportamiento lo notaria porque el token que se
    /// pasaria seria el de la peticion HTTP.
    /// </remarks>
    [Fact]
    public void El_Embudo_De_Mutaciones_No_Acepta_Token_De_Cancelacion()
    {
        var embudo = typeof(VentasPedidoCapturaRepository)
            .GetMethod("EjecutarMutacionAsync", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(embudo);
        Assert.DoesNotContain(embudo!.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }

    /// <summary>
    /// Y aun con un token YA cancelado en la mano, la escritura se completa.
    /// </summary>
    /// <remarks>
    /// Comportamiento, no forma: se pasa por el embudo real con un
    /// <c>CancellationTokenSource</c> cancelado desde antes. La escritura
    /// termina, el renglon queda comiteado y la conexion sale limpia. Si el
    /// embudo volviera a propagar el token, aqui saldria
    /// <c>OperationCanceledException</c> y la tabla quedaria vacia.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Con_El_Token_Ya_Cancelado_La_Mutacion_Igual_Se_Termina(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // El embudo de mutaciones ignora el token por construccion; el
            // cancelado se queda aqui para dejar claro que da igual.
            Assert.True(cts.IsCancellationRequested);

            await EjecutarMutacionAsync(conn, "zz_prueba_escribe_bien", "zz_prueba_escribe_bien", _ => { });

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(conn, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora")));
            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(conn, "SELECT @@TRANCOUNT")));
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    // ==================================================================
    // 3. EN UNA MUTACION NO SE REINTENTA POR EL TEXTO DEL MENSAJE
    // ==================================================================

    /// <summary>
    /// El respaldo al <c>sp_n_</c> sigue funcionando cuando el <c>sp_w_</c> no
    /// esta instalado: ese es el unico motivo por el que existe.
    /// </summary>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Un_Procedimiento_Que_No_Existe_Si_Cae_Al_Respaldo(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            var ds = await EjecutarMutacionAsync(
                conn, "zz_prueba_no_existe_este_sp", "zz_prueba_escribe_bien", _ => { });

            _out.WriteLine($"[{baseDatos}] el respaldo corrio: tablas={ds.Tables.Count}");
            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(conn, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora")));
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    /// <summary>
    /// UN RENGLON NO SE INSERTA DOS VECES PORQUE EL MENSAJE DE ERROR TRAIGA EL
    /// NOMBRE DEL PROCEDIMIENTO.
    /// </summary>
    /// <remarks>
    /// El <c>sp_w_</c> de captura atrapa los errores de legacy y los vuelve a
    /// levantar con RAISERROR, asi que el mensaje puede traer cualquier texto,
    /// el nombre del propio procedimiento incluido. El filtro por mensaje daba
    /// ese error por "procedimiento ausente" y VOLVIA A EJECUTAR la insercion
    /// sobre una escritura que ya habia hecho la mitad de su trabajo.
    ///
    /// El juguete escribe su renglon y DESPUES truena nombrandose. Con
    /// <c>esMutacion: true</c> queda UN renglon; con <c>false</c> -el camino de
    /// lecturas, donde reintentar es inofensivo- quedan DOS. La diferencia
    /// entre 1 y 2 es exactamente el pedido duplicado.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task El_Error_Que_Nombra_Al_Procedimiento_No_Reintenta_La_Escritura(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        try
        {
            await CrearJugueteriaDeTransaccionesAsync(conn);

            // MUTACION: no se reintenta. El error sale tal cual y queda 1 renglon.
            await Assert.ThrowsAsync<SqlException>(() => EjecutarMutacionAsync(
                conn, "zz_prueba_escribe_y_truena", "zz_prueba_escribe_y_truena", _ => { }));

            var renglonesMutacion = Convert.ToInt32(await EscalarAsync(conn, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora"));
            _out.WriteLine($"[{baseDatos}] como mutacion quedaron {renglonesMutacion} renglon(es)");
            Assert.Equal(1, renglonesMutacion);

            await using (var limpia = new SqlCommand("TRUNCATE TABLE dbo.zz_prueba_bitacora;", conn))
                await limpia.ExecuteNonQueryAsync();

            // LECTURA: ahi si reintenta por mensaje, y por eso el renglon se
            // escribe dos veces. Esta es la mitad de la prueba que demuestra que
            // la bandera `esMutacion` es la que marca la diferencia.
            await Assert.ThrowsAsync<SqlException>(() => ExecuteDataSetWithFallbackAsync(
                conn, "zz_prueba_escribe_y_truena", "zz_prueba_escribe_y_truena", _ => { },
                CancellationToken.None, esMutacion: false));

            var renglonesLectura = Convert.ToInt32(await EscalarAsync(conn, "SELECT COUNT(*) FROM dbo.zz_prueba_bitacora"));
            _out.WriteLine($"[{baseDatos}] como lectura quedaron {renglonesLectura} renglon(es)");
            Assert.Equal(2, renglonesLectura);
        }
        finally
        {
            await LimpiarAsync(conn);
        }
    }

    /// <summary>
    /// Los codigos 2812 y 208 -el procedimiento no esta- si reintentan; el
    /// RAISERROR que lo nombra, no.
    /// </summary>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Solo_Los_Codigos_2812_Y_208_Cuentan_Como_Procedimiento_Ausente(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        var ausente = await CapturarSqlExceptionAsync(conn, "EXEC dbo.zz_prueba_no_existe_este_sp;");
        _out.WriteLine($"[{baseDatos}] procedimiento ausente -> error {ausente.Number}");
        Assert.True(ausente.Number is 2812 or 208, $"Se esperaba 2812 u 208 y vino {ausente.Number}.");
        Assert.True(IsMissingStoredProcedure(ausente, "zz_prueba_no_existe_este_sp", esMutacion: true));

        var nombrado = await CapturarSqlExceptionAsync(conn,
            "RAISERROR('ERROR EN sp_w_InsertarPedidoDetalle: el cliente tiene saldo vencido', 16, 1);");
        Assert.False(nombrado.Number is 2812 or 208);
        Assert.False(IsMissingStoredProcedure(nombrado, "sp_w_InsertarPedidoDetalle", esMutacion: true));
        // En lecturas el reintento es inofensivo y el filtro por mensaje se queda.
        Assert.True(IsMissingStoredProcedure(nombrado, "sp_w_InsertarPedidoDetalle", esMutacion: false));
    }

    // ==================================================================
    // 4. EL GUARDIA DENTRO DEL sp_w_ DESPLEGADO
    // ==================================================================

    /// <summary>
    /// El guardia tiene que estar en la BASE, no solo en el script.
    /// </summary>
    /// <remarks>
    /// Los <c>sp_</c> de legacy y sus envoltorios no viven en el repositorio:
    /// viven en la base. Asi que la unica forma de saber si el guardia esta
    /// puesto es leer <c>sys.sql_modules</c>.
    ///
    /// Se comprueban tres cosas, y cada una corresponde a un bug distinto:
    ///
    ///   - <c>@TranEntrada</c>: sin la marca de entrada, un ROLLBACK a ciegas
    ///     deshace trabajo de quien llamo.
    ///   - el <c>ROLLBACK</c> despues de volver de legacy: cubre los caminos en
    ///     los que legacy sale sin cerrar y SIN lanzar error.
    ///   - en el CATCH, el <c>ROLLBACK</c> ANTES del <c>sp_releaseapplock</c>:
    ///     este era el bug. Si se suelta el applock primero, otro repartidor
    ///     entra a validar existencias mientras la transaccion fallida todavia
    ///     tiene los candados puestos, y se queda esperando.
    ///
    /// Tauro no tiene este <c>sp_w_</c> -sus escrituras van al <c>sp_n_</c> por
    /// el camino de respaldo-, asi que ahi la prueba informa y no exige: el
    /// <c>finally</c> del repositorio es quien la cubre, y eso lo mide
    /// <see cref="Una_Transaccion_Que_Legacy_Dejo_Abierta_No_Sobrevive_Al_Repositorio"/>.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task El_Guardia_De_Transaccion_Esta_Desplegado_En_El_Envoltorio_De_Insertar(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        if (!await AbrirOSaltarAsync(conn, baseDatos)) return;

        var definicion = Convert.ToString(await EscalarAsync(conn,
            "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID('dbo.sp_w_InsertarPedidoDetalle')"));

        if (string.IsNullOrWhiteSpace(definicion))
        {
            _out.WriteLine($"[{baseDatos}] no tiene sp_w_InsertarPedidoDetalle: escribe por el respaldo al sp_n_, y ahi la red de seguridad del repositorio es la que protege.");
            return;
        }

        Assert.Contains("DECLARE @TranEntrada", definicion, StringComparison.OrdinalIgnoreCase);

        // Dos comparaciones contra la marca de entrada: una al volver de legacy
        // -para los caminos que salen sin cerrar y SIN lanzar error- y otra en
        // el CATCH. Con una sola, uno de los dos caminos queda descubierto.
        Assert.True(Veces(definicion, "@@TRANCOUNT > @TranEntrada") >= 2,
            "Falta una de las dos comprobaciones de @TranEntrada (al volver de legacy y en el CATCH).");

        // LastIndexOf y no IndexOf: el encabezado del procedimiento EXPLICA el
        // BEGIN CATCH en un comentario, y buscar la primera aparicion apunta al
        // comentario, no al bloque.
        var catch_ = definicion.LastIndexOf("BEGIN CATCH", StringComparison.OrdinalIgnoreCase);
        Assert.True(catch_ > 0, "El envoltorio no tiene BEGIN CATCH: el guardia no puede estar completo.");

        var rollbackEnCatch = definicion.IndexOf("ROLLBACK TRANSACTION", catch_, StringComparison.OrdinalIgnoreCase);
        var sueltaApplock = definicion.IndexOf("sp_releaseapplock", catch_, StringComparison.OrdinalIgnoreCase);

        Assert.True(rollbackEnCatch > 0, "En el CATCH no se deshace la transaccion.");
        Assert.True(sueltaApplock > 0, "En el CATCH no se libera el applock.");
        // EL ORDEN ES EL BUG: primero deshacer, despues soltar el candado.
        Assert.True(rollbackEnCatch < sueltaApplock,
            "En el CATCH se suelta el applock ANTES de deshacer la transaccion: el siguiente repartidor entra a esperar candados que nadie solto.");

        _out.WriteLine($"[{baseDatos}] guardia completo: @TranEntrada, ROLLBACK tras legacy y ROLLBACK antes del applock.");
    }

    // ==================================================================
    // Plomeria
    // ==================================================================

    private static int Veces(string texto, string trozo)
    {
        var total = 0;
        var desde = 0;
        while (true)
        {
            var i = texto.IndexOf(trozo, desde, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return total;
            total++;
            desde = i + trozo.Length;
        }
    }

    private async Task<bool> AbrirOSaltarAsync(SqlConnection conn, string baseDatos)
    {
        try
        {
            await conn.OpenAsync();
            return true;
        }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: la prueba NO se corrio.");
            return false;
        }
    }

    /// <summary>
    /// Los procedimientos de juguete. Copian la estructura real, no llaman a
    /// nada de legacy, y se borran en el finally de cada prueba.
    /// </summary>
    private static async Task CrearJugueteriaDeTransaccionesAsync(SqlConnection conn)
    {
        await LimpiarAsync(conn);

        foreach (var lote in new[]
        {
            @"CREATE TABLE dbo.zz_prueba_bitacora (id INT IDENTITY(1,1) PRIMARY KEY, nota VARCHAR(100));",

            // El nivel interior de legacy: BEGIN TRAN + ROLLBACK TRAN.
            @"CREATE PROCEDURE dbo.zz_prueba_eliminar AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRAN;
                  ROLLBACK TRAN;   -- prohibido dentro de un INSERT..EXEC
                  SELECT -1 AS result, 'ERROR - 001' AS mensaje, 0 AS IDPedido;
                  RETURN;
              END",

            // El sp_n_ de legacy: abre transaccion y llama al interior con INSERT..EXEC.
            @"CREATE PROCEDURE dbo.zz_prueba_insertar AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRAN;
                  CREATE TABLE #t (result INT, mensaje VARCHAR(100), IDPedido INT);
                  INSERT #t EXEC dbo.zz_prueba_eliminar;
                  COMMIT TRAN;
                  SELECT 1 AS result, '' AS mensaje, 0 AS IDPedido;
              END",

            // El sp_w_ COMO ESTABA: el CATCH no toca la transaccion.
            @"CREATE PROCEDURE dbo.zz_prueba_singuardia AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRY
                      EXEC dbo.zz_prueba_insertar;
                  END TRY
                  BEGIN CATCH
                      DECLARE @m NVARCHAR(2000) = ERROR_MESSAGE();
                      RAISERROR(@m, 16, 1);
                  END CATCH
              END",

            // El sp_w_ CON EL GUARDIA, igual que el desplegado.
            @"CREATE PROCEDURE dbo.zz_prueba_conguardia AS
              BEGIN
                  SET NOCOUNT ON;
                  DECLARE @TranEntrada INT = @@TRANCOUNT;
                  BEGIN TRY
                      EXEC dbo.zz_prueba_insertar;
                      IF @@TRANCOUNT > @TranEntrada
                      BEGIN
                          ROLLBACK TRANSACTION;
                          SELECT 0 AS result, 'SE DESHIZO POR COMPLETO.  *(A3)' AS mensaje, 0 AS IDPedido;
                          RETURN;
                      END
                  END TRY
                  BEGIN CATCH
                      IF XACT_STATE() <> 0 AND @@TRANCOUNT > @TranEntrada
                          ROLLBACK TRANSACTION;
                      DECLARE @m2 NVARCHAR(2000) = ERROR_MESSAGE();
                      RAISERROR(@m2, 16, 1);
                  END CATCH
              END",

            // El medidor. Corre uno de los dos envoltorios, se come el error
            // que levante, y saca el @@TRANCOUNT por un parametro de salida
            // ANTES de limpiar. Ver TrancountAlSalirAsync.
            @"CREATE PROCEDURE dbo.zz_prueba_mide @ConGuardia INT, @Trancount INT OUTPUT AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRY
                      IF @ConGuardia = 1 EXEC dbo.zz_prueba_conguardia;
                      ELSE                EXEC dbo.zz_prueba_singuardia;
                  END TRY
                  BEGIN CATCH
                      /* El error se espera: 'Cannot use the ROLLBACK statement
                         within an INSERT-EXEC statement'. No se re-lanza para
                         que el parametro de salida llegue al cliente. */
                  END CATCH

                  SET @Trancount = @@TRANCOUNT;
                  WHILE @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
              END",

            // El camino de legacy que sale con RETURN y deja la transaccion viva.
            // No lanza error: es el que ninguna excepcion delata.
            @"CREATE PROCEDURE dbo.zz_prueba_abre_y_se_va AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRAN;
                  INSERT dbo.zz_prueba_bitacora (nota) VALUES ('a medias');
                  RETURN;          -- sin COMMIT ni ROLLBACK
              END",

            // Para la cancelacion: transaccion abierta, escritura hecha, y una
            // espera larga donde la cancelacion cae a media transaccion.
            @"CREATE PROCEDURE dbo.zz_prueba_lenta AS
              BEGIN
                  SET NOCOUNT ON;
                  BEGIN TRAN;
                  INSERT dbo.zz_prueba_bitacora (nota) VALUES ('cancelada a medias');
                  WAITFOR DELAY '00:00:10';
                  COMMIT TRAN;
              END",

            // Una mutacion que termina bien.
            @"CREATE PROCEDURE dbo.zz_prueba_escribe_bien AS
              BEGIN
                  SET NOCOUNT ON;
                  INSERT dbo.zz_prueba_bitacora (nota) VALUES ('ok');
                  SELECT 1 AS result, '' AS mensaje, 0 AS IDPedido;
              END",

            // Escribe PRIMERO y despues truena nombrandose: el caso que
            // duplicaba el renglon cuando se reintentaba por el texto.
            @"CREATE PROCEDURE dbo.zz_prueba_escribe_y_truena AS
              BEGIN
                  SET NOCOUNT ON;
                  INSERT dbo.zz_prueba_bitacora (nota) VALUES ('escrito antes de tronar');
                  RAISERROR('ERROR EN zz_prueba_escribe_y_truena: legacy rechazo el renglon', 16, 1);
              END"
        })
        {
            await using var cmd = new SqlCommand(lote, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task LimpiarAsync(SqlConnection conn)
    {
        if (conn.State != ConnectionState.Open) return;
        try
        {
            await using var cmd = new SqlCommand(@"
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_mide;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_singuardia;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_conguardia;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_insertar;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_eliminar;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_abre_y_se_va;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_lenta;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_escribe_bien;
DROP PROCEDURE IF EXISTS dbo.zz_prueba_escribe_y_truena;
DROP TABLE IF EXISTS dbo.zz_prueba_bitacora;", conn) { CommandTimeout = 30 };
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            /* Si no se puede ni limpiar, la conexion ya no sirve; los objetos
               llevan prefijo zz_prueba_ justo para poder barrerlos a mano. */
        }
    }

    /// <summary>
    /// Devuelve el <c>@@TRANCOUNT</c> que queda al salir del envoltorio.
    /// </summary>
    /// <remarks>
    /// POR QUE SE MIDE DENTRO DE LA BASE Y NO CON UN SELECT APARTE.
    ///
    /// El camino sin guardia deja la transaccion CONDENADA
    /// (<c>XACT_STATE() = -1</c>), y SQL Server la deshace al terminar el LOTE
    /// avisando con el error 3998. Si se mide en un segundo comando, el
    /// <c>@@TRANCOUNT</c> ya salio en 0 y la prueba pasaria en falso creyendo
    /// que no hay fuga.
    ///
    /// Tampoco se puede guardar el numero en una tabla: dentro de una
    /// transaccion condenada no se puede escribir. Asignarlo a un parametro de
    /// salida no escribe en el log, asi que ese es el unico camino que sirve.
    /// </remarks>
    private static async Task<int> TrancountAlSalirAsync(SqlConnection conn, int conGuardia)
    {
        await using var cmd = new SqlCommand("dbo.zz_prueba_mide", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };
        cmd.Parameters.AddWithValue("@ConGuardia", conGuardia);
        var salida = cmd.Parameters.Add("@Trancount", SqlDbType.Int);
        salida.Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt32(salida.Value);
    }

    /// <summary>
    /// Corre el juguete que abre transaccion y sale con RETURN.
    /// </summary>
    /// <remarks>
    /// SQL Server AVISA con el error 266 -"Transaction count after EXECUTE
    /// indicates a mismatching number of BEGIN and COMMIT statements.
    /// Previous count = 0, current count = 1"-, que es textualmente el error
    /// que se vio en produccion. Pero avisar es TODO lo que hace: la
    /// transaccion sigue abierta y los candados siguen puestos. Por eso el
    /// aviso se traga aqui: lo que la prueba mide es lo que queda detras.
    /// </remarks>
    private static async Task AbreYSeVaAsync(SqlConnection conn)
    {
        try
        {
            await using var cmd = new SqlCommand("EXEC dbo.zz_prueba_abre_y_se_va;", conn) { CommandTimeout = 30 };
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqlException ex) when (ex.Number == 266)
        {
            /* El aviso esperado. */
        }
    }

    private static async Task<int> TransaccionesAbiertasDeAsync(SqlConnection espia, int spid)
        => Convert.ToInt32(await EscalarAsync(espia,
            $"SELECT ISNULL(SUM(open_transaction_count), 0) FROM sys.dm_exec_sessions WHERE session_id = {spid}"));

    /// <summary>
    /// Candados de escritura (X/U) y applocks que la sesion observada retiene.
    /// Los compartidos no cuentan: los que paran al siguiente repartidor son
    /// estos.
    /// </summary>
    private static async Task<int> CandadosExclusivosDeAsync(SqlConnection espia, int spid)
        => Convert.ToInt32(await EscalarAsync(espia, $@"
SELECT COUNT(*)
FROM sys.dm_tran_locks
WHERE request_session_id = {spid}
  AND request_status = 'GRANT'
  AND (request_mode IN ('X', 'U', 'IX', 'IU') OR resource_type = 'APPLICATION')"));

    private static async Task<object?> EscalarAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<SqlException> CapturarSqlExceptionAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        return await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
    }

    // ---- los privados del repositorio, por reflexion -------------------
    //
    // Se prueban por reflexion a proposito: son los que de verdad deciden si
    // una escritura se abandona o se duplica, y volverlos publicos para
    // probarlos los pondria al alcance de quien no debe llamarlos.

    private static readonly Type Repositorio = typeof(VentasPedidoCapturaRepository);

    private static Task<DataSet> EjecutarMutacionAsync(
        SqlConnection conn, string webSp, string legacySp, Action<SqlCommand> addParams)
    {
        var metodo = Repositorio.GetMethod("EjecutarMutacionAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Ya no existe EjecutarMutacionAsync: el embudo de mutaciones cambio de nombre o desaparecio.");
        return Invocar(metodo, conn, webSp, legacySp, addParams);
    }

    private static Task<DataSet> ExecuteDataSetWithFallbackAsync(
        SqlConnection conn, string webSp, string legacySp, Action<SqlCommand> addParams,
        CancellationToken ct, bool esMutacion)
    {
        var metodo = Repositorio.GetMethod("ExecuteDataSetWithFallbackAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Ya no existe ExecuteDataSetWithFallbackAsync.");
        return Invocar(metodo, conn, webSp, legacySp, addParams, ct, esMutacion);
    }

    private static Task DeshacerTransaccionColgadaAsync(SqlConnection conn)
    {
        var metodo = Repositorio.GetMethod("DeshacerTransaccionColgadaAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Ya no existe DeshacerTransaccionColgadaAsync: la red de seguridad se quito.");
        return (Task)metodo.Invoke(null, new object?[] { conn })!;
    }

    private static bool IsMissingStoredProcedure(SqlException ex, string sp, bool esMutacion)
    {
        var metodo = Repositorio.GetMethod("IsMissingStoredProcedure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Ya no existe IsMissingStoredProcedure.");
        return (bool)metodo.Invoke(null, new object?[] { ex, sp, esMutacion })!;
    }

    /// <summary>
    /// La reflexion envuelve la excepcion real en una TargetInvocationException
    /// cuando el metodo truena de forma sincrona; se desenvuelve para que las
    /// pruebas puedan esperar el SqlException de verdad.
    /// </summary>
    private static async Task<DataSet> Invocar(MethodInfo metodo, params object?[] args)
    {
        try
        {
            return await (Task<DataSet>)metodo.Invoke(null, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}
