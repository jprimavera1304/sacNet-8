using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// EL GUARDADO DE PEDIDOS, CONTRA LA BASE DE VERDAD, Y LO QUE DEJA DETRAS.
/// </summary>
/// <remarks>
/// Lo que estas pruebas vigilan no es el resultado que se le devuelve al
/// agente: es lo que queda en el servidor DESPUES. Al cerrar cada caso se
/// comprueba siempre lo mismo, y es lo unico que de verdad importa:
///
///   * ninguna transaccion abierta,
///   * ningun candado de escritura ni applock retenido,
///   * ningun renglon duplicado,
///   * ningun pedido a medias.
///
/// COMO SE OBSERVA
/// ---------------
/// La conexion del repositorio se marca con `Application Name` propio, asi que
/// el espia puede preguntar por <c>sys.dm_exec_sessions</c> y
/// <c>sys.dm_tran_locks</c> SOLO por las sesiones de esta prueba. Hay que
/// mirarlo desde fuera: la sesion que fuga es la que hay que observar, y
/// preguntarle a ella misma no dice nada de lo que ve el resto del servidor.
/// Y hay que mirarlo DESPUES de que el repositorio devolvio la conexion al
/// pool, porque el problema es justo ese: que vuelve al pool sucia.
///
/// POR QUE SOLO ZARAGOZA
/// ---------------------
/// MacZ es la unica base local con <c>sp_w_InsertarPedidoDetalle</c> desplegado
/// -el que trae el guardia de transaccion- y con un pedido borrador conocido.
/// Tauro escribe por el camino de respaldo al <c>sp_n_</c>, y ahi la defensa es
/// la red de seguridad del repositorio, que se mide en
/// <see cref="CapturaTransaccionesYCandadosTests"/> contra LAS DOS bases.
///
/// SE SALTA SOLA si no hay SQL Server, si el pedido de trabajo ya no es
/// borrador, o si el producto no tiene existencia: medir sobre datos que
/// cambiaron daria un fallo que no es una regresion.
///
/// NO TOCA PRODUCCION. Todo corre contra LAP-JUAND, y cada caso borra el
/// renglon que creo.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CapturaPedidoContraLaBaseTests
{
    private const string BaseDatos = "MacZ";

    // Pedido borrador conocido de MacZ (IDStatusPedido = 0) y sus datos de
    // cabecera. Los trae el pedido mismo: si alguno dejo de cuadrar, la prueba
    // se salta en vez de inventar un pedido nuevo.
    private const int Pedido = 214463;
    private const int Usuario = 3;
    private const int Cliente = 30075;
    private const int Domicilio = 55460;
    private const int Agente = 9;
    private const int TipoDocumento = 5;
    private const int Almacen = 1;

    // Un producto de la MISMA categoria que los dos renglones que el pedido ya
    // trae (un pedido no puede mezclar categorias) y que NO esta en el pedido,
    // para que la insercion cree un renglon nuevo y se pueda contar.
    private const int Producto = 24;

    // Marca de las conexiones del repositorio, para que el espia sepa a quien
    // mirar sin confundirse con el backend local ni con otras pruebas.
    private const string Firma = "zzPruebaCaptura";

    private readonly ITestOutputHelper _out;
    public CapturaPedidoContraLaBaseTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDelRepositorio =>
        $"Server=LAP-JUAND;Database={BaseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Application Name={Firma};";

    private static string CadenaDelEspia =>
        $"Server=LAP-JUAND;Database={BaseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Application Name=zzEspiaCaptura;";

    // ==================================================================
    // 1. GUARDADO NORMAL
    // ==================================================================

    /// <summary>
    /// Agregar un renglon y quitarlo deja el pedido y el inventario como estaban.
    /// </summary>
    /// <remarks>
    /// Es el caso feliz, y sirve de linea base: si este falla, lo que sigue no
    /// significa nada. Las aserciones que lo hacen valioso son las del cierre
    /// -un solo renglon, cero transacciones, cero candados- y la de inventario:
    /// la existencia fisica NO se mueve al capturar (se mueve al procesar), y
    /// lo APARTADO sube exactamente las piezas pedidas y vuelve a bajar al
    /// borrar. Un renglon duplicado o un apartado que no regresa se ven aqui.
    /// </remarks>
    [Fact]
    public async Task Agregar_Y_Quitar_Un_Renglon_Deja_El_Pedido_Y_El_Inventario_Como_Estaban()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        var repo = Repositorio(out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);
        var apartadoAntes = await ApartadoDeAsync(espia);
        var existenciaAntes = await ExistenciaDeAsync(espia);

        int detalle;
        try
        {
            var snapshot = await repo.AgregarDetalleAsync(Solicitud(cantidad: 2), Usuario, Firma, CancellationToken.None);
            Assert.Equal(Pedido, snapshot.IDPedido);

            detalle = await DetalleDelProductoAsync(espia);
            _out.WriteLine($"renglon nuevo = {detalle}");

            Assert.True(detalle > 0, "La insercion no dejo renglon: el guardado normal no esta guardando.");
            Assert.Equal(1, await RenglonesDelProductoAsync(espia));
            Assert.Equal(renglonesAntes + 1, await RenglonesDelPedidoAsync(espia));
            Assert.Equal(apartadoAntes + 2, await ApartadoDeAsync(espia));

            // La existencia fisica no se toca al capturar: eso pasa al procesar
            // el pedido. Si se moviera aqui, el almacen quedaria descuadrado
            // para todos los demas.
            Assert.Equal(existenciaAntes, await ExistenciaDeAsync(espia));

            await NadaColgadoAsync(espia, "despues de agregar");
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(repo, espia);
        }

        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
        Assert.Equal(apartadoAntes, await ApartadoDeAsync(espia));
        Assert.Equal(existenciaAntes, await ExistenciaDeAsync(espia));
        await NadaColgadoAsync(espia, "despues de quitar");
    }

    // ==================================================================
    // 2. CANCELACION DE SOLICITUDES
    // ==================================================================

    /// <summary>
    /// Con el token YA cancelado, la peticion muere ANTES de abrir la conexion
    /// y no escribe nada.
    /// </summary>
    /// <remarks>
    /// LO QUE SE MIDIO AQUI, Y QUE CORRIGE LA IDEA INTUITIVA.
    ///
    /// La primera version de esta prueba exigia que el renglon SI quedara
    /// escrito, razonando que el embudo de mutaciones ignora el token. Medido:
    /// no queda. El token se revisa antes, en <c>conn.OpenAsync(ct)</c>, y
    /// ninguna escritura llega a arrancar.
    ///
    /// Eso esta BIEN y es la otra mitad del invariante: una peticion que llega
    /// cancelada no deja nada, ni completo ni a medias. Lo que el embudo
    /// protege es el caso distinto -la cancelacion que cae cuando el comando YA
    /// esta corriendo en SQL Server-, y ahi no se puede provocar a voluntad
    /// desde fuera: se mide con los procedimientos de juguete en
    /// <see cref="CapturaTransaccionesYCandadosTests.Con_El_Token_Ya_Cancelado_La_Mutacion_Igual_Se_Termina"/>
    /// y en
    /// <see cref="CapturaTransaccionesYCandadosTests.Cancelar_A_Media_Transaccion_Deja_Candados_Y_La_Red_De_Seguridad_Los_Suelta"/>.
    ///
    /// El IDEmpresaCS se calienta antes para que la cancelacion no se adelante
    /// a un viaje de lectura que no tiene nada que ver con la escritura.
    /// </remarks>
    [Fact]
    public async Task Con_El_Token_Ya_Cancelado_No_Se_Escribe_Nada_Ni_Queda_Nada_Colgado()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        var repo = Repositorio(out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);
        var apartadoAntes = await ApartadoDeAsync(espia);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, cts.Token));

        // Ni renglon nuevo, ni medio renglon, ni apartado fantasma.
        Assert.Equal(0, await RenglonesDelProductoAsync(espia));
        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
        Assert.Equal(apartadoAntes, await ApartadoDeAsync(espia));
        await NadaColgadoAsync(espia, "tras llegar ya cancelada");
    }

    /// <summary>
    /// Cancelar mientras la captura va en camino.
    /// </summary>
    /// <remarks>
    /// El momento en que cae la cancelacion no se puede fijar desde fuera, asi
    /// que la prueba no exige un resultado: exige el INVARIANTE. El renglon
    /// esta UNA vez o no esta -nunca dos, nunca a medias-, las piezas apartadas
    /// cuadran con lo que quedo escrito, y la sesion que escribio no se queda
    /// con transaccion ni candados. Eso ultimo es lo que le pasaba al agente
    /// que cerraba la pantalla y dejaba a los demas esperando los 500 s del
    /// CommandTimeout.
    ///
    /// El retraso se calcula midiendo primero una captura completa, para que la
    /// cancelacion caiga dentro de la ventana en que el comando esta corriendo
    /// y no antes de abrir la conexion.
    /// </remarks>
    [Fact]
    public async Task Cancelar_A_Media_Captura_No_Deja_Renglones_A_Medias_Ni_Candados()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        var repo = Repositorio(out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);
        var apartadoAntes = await ApartadoDeAsync(espia);

        // Cuanto tarda una captura completa en esta maquina. Se agrega y se
        // borra, asi que el pedido queda como estaba antes de la medicion.
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        await repo.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, CancellationToken.None);
        reloj.Stop();
        await BorrarRenglonDeLaPruebaAsync(repo, espia);
        _out.WriteLine($"una captura completa tarda {reloj.ElapsedMilliseconds} ms");

        try
        {
            var aMitad = Math.Max(5, reloj.ElapsedMilliseconds / 2);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(aMitad));

            /* No se exige NINGUN error en particular, y eso tambien es un
               hallazgo: cuando la cancelacion cae mientras se esta leyendo el
               resultado, SqlClient no siempre levanta OperationCanceledException
               -se vio tambien SqlException "A severe error occurred on the
               current command ... Operation cancelled by user"-. Lo que se mide
               es lo que quedo en la base, no como se llamo el error. */
            var error = await Record.ExceptionAsync(() =>
                repo.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, cts.Token));
            _out.WriteLine($"la llamada cancelada termino con: {error?.GetType().Name ?? "nada, alcanzo a terminar"}");

            var renglones = await RenglonesDelProductoAsync(espia);
            _out.WriteLine($"cancelando a los {aMitad} ms quedaron {renglones} renglon(es) (0 o 1, nunca 2)");

            Assert.InRange(renglones, 0, 1);
            Assert.Equal(renglonesAntes + renglones, await RenglonesDelPedidoAsync(espia));
            // Si quedo renglon, tiene que traer exactamente la pieza pedida: un
            // renglon con cantidad 0 o duplicada seria un pedido a medias.
            Assert.Equal(apartadoAntes + renglones, await ApartadoDeAsync(espia));
            await NadaColgadoAsync(espia, "tras cancelar a media captura");
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(repo, espia);
        }

        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
    }

    // ==================================================================
    // 3. ERRORES DEL PROCEDIMIENTO Y ROLLBACK
    // ==================================================================

    /// <summary>
    /// Si el procedimiento rechaza, no se queda nada escrito ni nada bloqueado.
    /// </summary>
    /// <remarks>
    /// Se pide mas de lo que hay en el almacen, que es el rechazo que
    /// <c>sp_w_InsertarPedidoDetalle</c> agrego -compara contra la existencia
    /// DISPONIBLE, no contra la fisica- y sale por su camino de error, con el
    /// applock ya tomado.
    ///
    /// Lo que se vigila: el applock SE LIBERA y la transaccion SE DESHACE. El
    /// bug era el orden dentro del CATCH -se soltaba el applock antes del
    /// ROLLBACK-, y entonces el siguiente repartidor entraba a validar
    /// existencias y se quedaba esperando candados que nadie habia soltado.
    /// </remarks>
    [Fact]
    public async Task Un_Rechazo_Del_Procedimiento_No_Escribe_Nada_Ni_Retiene_El_Applock()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        var repo = Repositorio(out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);
        var existencia = await ExistenciaDeAsync(espia);

        // Mas piezas de las que hay: el rechazo es por la regla de disponible.
        var imposible = existencia + 1000;

        var error = await Record.ExceptionAsync(() =>
            repo.AgregarDetalleAsync(Solicitud(cantidad: imposible), Usuario, Firma, CancellationToken.None));

        _out.WriteLine($"pedir {imposible} piezas devolvio: {error?.GetType().Name} {error?.Message}");

        // El contrato de rechazo de legacy y del envoltorio es el mismo:
        // result = 0 + mensaje, que el repositorio convierte en ArgumentException.
        Assert.NotNull(error);

        Assert.Equal(0, await RenglonesDelProductoAsync(espia));
        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
        Assert.Equal(existencia, await ExistenciaDeAsync(espia));
        await NadaColgadoAsync(espia, "tras el rechazo");
    }

    // ==================================================================
    // 4. OPERACIONES SIMULTANEAS
    // ==================================================================

    /// <summary>
    /// Dos capturas del mismo producto a la vez: ni se duplican ni se trban.
    /// </summary>
    /// <remarks>
    /// El applock por (almacen, producto) existe justo para esto: validar y
    /// luego insertar no basta, porque dos sesiones que leen "queda 1" al mismo
    /// tiempo pasan las dos. Aqui las dos piden poco, asi que las dos deben
    /// pasar, pero EN FILA.
    ///
    /// Lo que la hace valiosa: al terminar no queda NINGUN applock retenido ni
    /// transaccion abierta de las dos sesiones, y las piezas apartadas son
    /// exactamente la suma de lo que cada una pidio. Si el applock se quedara
    /// puesto -o si una transaccion quedara abierta- el tercer agente esperaria
    /// hasta los 500 s de CommandTimeout, que es el sintoma que se reporto.
    /// </remarks>
    [Fact]
    public async Task Dos_Capturas_Simultaneas_Del_Mismo_Producto_Se_Serializan_Sin_Duplicar()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        // Dos instancias distintas, como dos peticiones HTTP distintas: cada
        // una con su cache, igual que el contenedor las resuelve por peticion.
        var uno = Repositorio(out var cacheUno, out var capacidadesUno);
        var dos = Repositorio(out var cacheDos, out var capacidadesDos);
        await CalentarEmpresaCsAsync(capacidadesUno, cacheUno);
        await CalentarEmpresaCsAsync(capacidadesDos, cacheDos);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);
        var apartadoAntes = await ApartadoDeAsync(espia);

        try
        {
            var a = Task.Run(() => uno.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, CancellationToken.None));
            var b = Task.Run(() => dos.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, CancellationToken.None));

            var errores = new List<Exception>();
            foreach (var tarea in new[] { a, b })
            {
                var error = await Record.ExceptionAsync(() => tarea);
                if (error != null) errores.Add(error);
            }

            var exitos = 2 - errores.Count;
            foreach (var error in errores) _out.WriteLine($"una de las dos fue rechazada: {error.Message}");
            _out.WriteLine($"exitos = {exitos}");

            // Ninguna se puede quedar colgada: si el applock no se libera, la
            // segunda espera y revienta por timeout.
            Assert.DoesNotContain(errores, e => e is TaskCanceledException or TimeoutException);

            // Legacy acumula el mismo producto en UN renglon, asi que lo que se
            // cuenta son PIEZAS, no renglones: 'productos' es suma de piezas.
            Assert.Equal(apartadoAntes + exitos, await ApartadoDeAsync(espia));
            Assert.InRange(await RenglonesDelProductoAsync(espia), 0, 1);

            await NadaColgadoAsync(espia, "tras las dos capturas simultaneas");
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(uno, espia);
        }

        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
        Assert.Equal(apartadoAntes, await ApartadoDeAsync(espia));
        await NadaColgadoAsync(espia, "tras limpiar las dos capturas");
    }

    // ==================================================================
    // 5. PERDIDA DE CONEXION
    // ==================================================================

    /// <summary>
    /// Si la base no contesta, el pedido no se queda a medias.
    /// </summary>
    /// <remarks>
    /// Se apunta el repositorio a un servidor que no existe, con timeout de
    /// conexion corto. Lo que se exige es que el error sea de conexion -no un
    /// NullReference ni un cuelgue- y que el pedido de trabajo siga exactamente
    /// igual: una caida de red no puede dejar un renglon a medias.
    /// </remarks>
    [Fact]
    public async Task Si_No_Hay_Base_No_Se_Escribe_Nada_Y_El_Pedido_Queda_Intacto()
    {
        await using var espia = await EspiaAsync();
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia)) return;

        var renglonesAntes = await RenglonesDelPedidoAsync(espia);

        var repo = new VentasPedidoCapturaRepository(
            Configuracion("Server=LAP-JUAND-QUE-NO-EXISTE;Database=MacZ;User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Connect Timeout=3;"),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<VentasPedidoCapturaRepository>.Instance,
            new CapacidadesEmpresaRepository(Configuracion("Server=LAP-JUAND-QUE-NO-EXISTE;Database=MacZ;User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Connect Timeout=3;")));

        var error = await Record.ExceptionAsync(() =>
            repo.AgregarDetalleAsync(Solicitud(cantidad: 1), Usuario, Firma, CancellationToken.None));

        _out.WriteLine($"sin base: {error?.GetType().Name}");
        Assert.IsType<SqlException>(error);

        Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia));
        Assert.Equal(0, await RenglonesDelProductoAsync(espia));
    }

    // ==================================================================
    // Plomeria
    // ==================================================================

    /// <summary>
    /// LA ASERCION QUE CIERRA TODOS LOS CASOS.
    /// </summary>
    /// <remarks>
    /// Se mira SOLO a las sesiones marcadas con la firma de esta prueba, y se
    /// mira cuando ya volvieron al pool. Dos numeros: transacciones abiertas y
    /// candados retenidos (los de escritura y los applock; los compartidos no
    /// paran a nadie).
    /// </remarks>
    private async Task NadaColgadoAsync(SqlConnection espia, string cuando)
    {
        var transacciones = Convert.ToInt32(await EscalarAsync(espia, $@"
SELECT ISNULL(SUM(open_transaction_count), 0)
FROM sys.dm_exec_sessions
WHERE program_name = '{Firma}'"));

        var candados = Convert.ToInt32(await EscalarAsync(espia, $@"
SELECT COUNT(*)
FROM sys.dm_tran_locks l
JOIN sys.dm_exec_sessions s ON s.session_id = l.request_session_id
WHERE s.program_name = '{Firma}'
  AND l.request_status = 'GRANT'
  AND (l.request_mode IN ('X', 'U', 'IX', 'IU') OR l.resource_type = 'APPLICATION')"));

        _out.WriteLine($"{cuando}: transacciones={transacciones}, candados={candados}");

        Assert.Equal(0, transacciones);
        Assert.Equal(0, candados);
    }

    private async Task<SqlConnection?> EspiaAsync()
    {
        var conn = new SqlConnection(CadenaDelEspia);
        try
        {
            await conn.OpenAsync();
            return conn;
        }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {BaseDatos}: las pruebas de captura NO se corrieron.");
            await conn.DisposeAsync();
            return null;
        }
    }

    /// <summary>
    /// La base local llega a ~marzo 2026 y se le han corrido scripts: antes de
    /// medir hay que confirmar que el pedido de trabajo sigue siendo un
    /// borrador, que el producto no esta ya dentro, y que hay existencia. Si
    /// algo cambio, la prueba se salta: un fallo por datos movidos se lee como
    /// una regresion y no lo es.
    /// </summary>
    private async Task<bool> PrecondicionesAsync(SqlConnection espia)
    {
        var estatus = await EscalarAsync(espia, $"SELECT IDStatusPedido FROM Pedidos WHERE IDPedido = {Pedido}");
        if (estatus == null)
        {
            _out.WriteLine($"El pedido {Pedido} ya no existe en {BaseDatos}: prueba saltada.");
            return false;
        }

        if (Convert.ToInt32(estatus) != 0)
        {
            _out.WriteLine($"El pedido {Pedido} ya no es borrador (estatus {estatus}): prueba saltada.");
            return false;
        }

        if (await RenglonesDelProductoAsync(espia) > 0)
        {
            _out.WriteLine($"El producto {Producto} ya esta en el pedido {Pedido}: una corrida anterior no limpio. Prueba saltada.");
            return false;
        }

        var existencia = await ExistenciaDeAsync(espia);
        if (existencia < 10)
        {
            _out.WriteLine($"El producto {Producto} no tiene existencia suficiente en el almacen {Almacen}: prueba saltada.");
            return false;
        }

        return true;
    }

    private static PedidoAgregarDetalleRequest Solicitud(int cantidad) => new()
    {
        IDPedido = Pedido,
        IDEmpresa = 1,
        IDCliente = Cliente,
        IDAgenteAutoriza = Agente,
        IDDomicilio = Domicilio,
        IDTipoDocumento = TipoDocumento,
        // La fecha del pedido la manda Constantes.FechaOperacion, no el reloj;
        // vacia, el envoltorio usa la de la empresa, que es lo que hace el front.
        Fecha = string.Empty,
        IDAlmacen = Almacen,
        IDProducto = Producto,
        Cantidad = cantidad,
        Equipo = Firma
    };

    private static VentasPedidoCapturaRepository Repositorio(out IMemoryCache cache, out CapacidadesEmpresaRepository capacidades)
    {
        var configuracion = Configuracion(CadenaDelRepositorio);
        cache = new MemoryCache(new MemoryCacheOptions());
        capacidades = new CapacidadesEmpresaRepository(configuracion);
        return new VentasPedidoCapturaRepository(
            configuracion, cache, NullLogger<VentasPedidoCapturaRepository>.Instance, capacidades);
    }

    private static IConfiguration Configuracion(string cadena) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Main"] = cadena })
            .Build();

    /// <summary>
    /// El IDEmpresaCS se resuelve ANTES de la escritura y SI respeta el token.
    /// Dejarlo cacheado es lo que hace que la prueba de cancelacion mida el
    /// embudo de mutaciones y no ese viaje previo.
    /// </summary>
    private static Task CalentarEmpresaCsAsync(CapacidadesEmpresaRepository capacidades, IMemoryCache cache)
        => EmpresaCsPorOmision.ResolverAsync(capacidades, cache, CancellationToken.None);

    private static Task<int> RenglonesDelPedidoAsync(SqlConnection espia)
        => ContarAsync(espia, $"SELECT COUNT(*) FROM [Pedidos Detalle] WHERE IDPedido = {Pedido}");

    private static Task<int> RenglonesDelProductoAsync(SqlConnection espia)
        => ContarAsync(espia, $"SELECT COUNT(*) FROM [Pedidos Detalle] WHERE IDPedido = {Pedido} AND IDProducto = {Producto}");

    /// <summary>
    /// Piezas apartadas del producto, contadas SOLO dentro del pedido de
    /// trabajo. Se cuenta aqui y no sobre vw_w_PedidoRenglonApartado a
    /// proposito: esa vista suma los ~2330 borradores de la base local, y
    /// cualquiera de ellos puede moverse entre dos aserciones y tirar la prueba
    /// por algo que no tiene nada que ver. Lo que estas pruebas mueven es este
    /// pedido, y es lo que tienen que medir.
    /// </summary>
    private static Task<int> ApartadoDeAsync(SqlConnection espia)
        => ContarAsync(espia, $@"
SELECT ISNULL(SUM(Cantidad), 0) FROM [Pedidos Detalle]
WHERE IDPedido = {Pedido} AND IDProducto = {Producto}");

    private static Task<int> ExistenciaDeAsync(SqlConnection espia)
        => ContarAsync(espia, $"SELECT ISNULL(Existencia, 0) FROM AlmacenProducto WHERE IDAlmacen = {Almacen} AND IDProducto = {Producto}");

    private static async Task<int> DetalleDelProductoAsync(SqlConnection espia)
        => await ContarAsync(espia, $@"
SELECT ISNULL(MAX(IDPedidoDetalle), 0) FROM [Pedidos Detalle]
WHERE IDPedido = {Pedido} AND IDProducto = {Producto}");

    /// <summary>
    /// Limpia lo que la prueba creo, por el MISMO camino que usa la pantalla
    /// (sp_w_EliminarPedidoDetalle). Si ese camino fallara, el borrado directo
    /// de abajo evita dejar el pedido sucio para la siguiente corrida.
    /// </summary>
    private async Task BorrarRenglonDeLaPruebaAsync(VentasPedidoCapturaRepository repo, SqlConnection espia)
    {
        var detalle = await DetalleDelProductoAsync(espia);
        if (detalle <= 0) return;

        try
        {
            await repo.EliminarDetalleAsync(new PedidoEliminarDetalleRequest
            {
                IDPedido = Pedido,
                IDPedidoDetalle = detalle,
                SinModificarObservaciones = 1
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _out.WriteLine($"El borrado por el camino normal fallo ({ex.GetType().Name}: {ex.Message}); se limpia a mano.");
        }

        if (await RenglonesDelProductoAsync(espia) > 0)
        {
            await using var cmd = new SqlCommand(
                $"DELETE FROM [Pedidos Detalle] WHERE IDPedido = {Pedido} AND IDProducto = {Producto};", espia);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> ContarAsync(SqlConnection conn, string sql)
        => Convert.ToInt32(await EscalarAsync(conn, sql) ?? 0);

    private static async Task<object?> EscalarAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        var valor = await cmd.ExecuteScalarAsync();
        return valor == DBNull.Value ? null : valor;
    }
}
