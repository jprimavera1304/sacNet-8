using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// EL CANDADO DE CLIENTE AL AGREGAR UN RENGLON, CONTRA LA BASE DE VERDAD.
/// </summary>
/// <remarks>
/// QUE PROTEGE
///
/// <c>sp_n_InsertarPedidoDetalle</c> calcula el descuento con el @IDCliente que
/// recibe en CADA renglon -lee <c>Clientes.IDDescuento</c> y de ahi
/// <c>[Detalle Descuentos]</c>- y NO lo compara nunca contra
/// <c>Pedidos.IDCliente</c>. Una llamada con otro cliente guarda el renglon en
/// el pedido correcto pero con la lista de descuentos de otro.
///
/// No es teorico. En el pedido 88729 (17-sep-2026) el cliente 1264 recibio 45 %
/// en dos aceites donde no tiene NINGUNA fila de tarifa; ese 45 solo pudo salir
/// de otra lista. Son 2,341 pesos en un solo pedido. La auditoria completa esta
/// en <c>Querrys/58 Descuento De Mas En Pedidos/</c>.
///
/// POR QUE EN EL SERVIDOR Y NO EN LA APP
///
/// El telefono manda el cliente desde su estado en memoria. Una version vieja
/// de la app no se puede arreglar hacia atras; el candado del servidor protege
/// tambien a los telefonos que nunca actualicen.
///
/// POR QUE CONTRA LAS DOS BASES
///
/// MacZ tiene desplegado <c>sp_w_InsertarPedidoDetalle</c>; Produccion_svr NO,
/// y ahi el repositorio cae al <c>sp_n_</c> de siempre. Son dos caminos hasta
/// la misma escritura: probar solo Zaragoza no dice nada de Tauro.
///
/// SE SALTA SOLA si no hay SQL Server, si el pedido de trabajo dejo de ser
/// borrador, si el producto ya esta dentro o si falta la tabla de bitacora:
/// medir sobre datos que cambiaron daria un fallo que no es una regresion.
///
/// NO TOCA PRODUCCION. Todo corre contra LAP-JUAND y cada caso borra lo que
/// creo.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CandadoDeClienteDelPedidoTests
{
    private const int TipoDocumento = 5;
    private const string Firma = "zzPruebaCandadoCliente";

    /// <summary>Los datos de trabajo de cada empresa.</summary>
    private sealed record Datos(
        string BaseDatos, int Pedido, int Usuario, int Cliente, int Domicilio,
        int Agente, int Almacen, int Producto, int ClienteAjeno);

    private static Datos DatosDe(string baseDatos) => baseDatos switch
    {
        // Pedido borrador de MacZ, el mismo que usa CapturaPedidoContraLaBaseTests.
        "MacZ" => new Datos("MacZ", 214463, 3, 30075, 55460, 9, 1, 24, 31208),
        // Pedido borrador de Produccion_svr (Tauro), categoria 148, almacen 2.
        "Produccion_svr" => new Datos("Produccion_svr", 215407, 1056, 2844, 57556, 0, 2, 2256, 103),
        _ => throw new ArgumentOutOfRangeException(nameof(baseDatos), baseDatos, "Base sin datos de prueba.")
    };

    private readonly ITestOutputHelper _out;
    public CandadoDeClienteDelPedidoTests(ITestOutputHelper salida) => _out = salida;

    private static string Cadena(string baseDatos, string firma) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Application Name={firma};";

    // ==================================================================
    // 1. EL CASO BUENO
    // ==================================================================

    /// <summary>
    /// Con el cliente correcto el renglon se agrega y queda constancia de con
    /// que cliente y con que lista se calculo.
    /// </summary>
    /// <remarks>
    /// Es la linea base: si este falla, los demas no significan nada. La
    /// asercion que importa no es que devuelva el pedido, es que la bitacora
    /// guarde los DOS clientes y que coincidan.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Con_El_Cliente_Correcto_El_Renglon_Se_Agrega_Y_Queda_En_La_Bitacora(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia, d)) return;

        var repo = Repositorio(d, out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia, d);
        var desde = DateTime.Now.AddSeconds(-5);

        try
        {
            var snapshot = await repo.AgregarDetalleAsync(
                Solicitud(d, d.Cliente, cantidad: 1), d.Usuario, Firma, CancellationToken.None);

            Assert.Equal(d.Pedido, snapshot.IDPedido);
            Assert.Equal(renglonesAntes + 1, await RenglonesDelPedidoAsync(espia, d));

            var fila = await UltimaBitacoraAsync(espia, d, desde);
            Assert.NotNull(fila);
            Assert.Equal("agregado", fila!.Resultado);
            Assert.Equal(d.Cliente, fila.IDClientePedido);
            Assert.Equal(d.Cliente, fila.IDClienteEnviado);
            Assert.Equal(d.Producto, fila.IDProducto);
            // Legacy ya escribio el porcentaje: la bitacora lo guarda crudo.
            Assert.False(string.IsNullOrWhiteSpace(fila.Porcentajes),
                "La bitacora no guardo el porcentaje: sin el, una diferencia futura vuelve a ser irreconstruible.");
            _out.WriteLine($"[{baseDatos}] bitacora: {fila.Resultado} cliente={fila.IDClientePedido} pct={fila.Porcentajes} lista={fila.IDDescuento}");
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }
    }

    // ==================================================================
    // 2. EL CANDADO
    // ==================================================================

    /// <summary>
    /// Con un cliente distinto al del pedido, la operacion se rechaza y el
    /// pedido no se toca.
    /// </summary>
    /// <remarks>
    /// Las dos mitades importan igual. Que truene es la mitad visible; que el
    /// pedido quede EXACTAMENTE como estaba es la que evita el dano: un rechazo
    /// que ya escribio el renglon no sirve de nada.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Con_Un_Cliente_Distinto_Se_Rechaza_Y_El_Pedido_No_Se_Toca(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia, d)) return;

        var repo = Repositorio(d, out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia, d);
        var desde = DateTime.Now.AddSeconds(-5);

        try
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.AgregarDetalleAsync(Solicitud(d, d.ClienteAjeno, cantidad: 1), d.Usuario, Firma, CancellationToken.None));

            Assert.Contains("otro cliente", error.Message, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(renglonesAntes, await RenglonesDelPedidoAsync(espia, d));
            Assert.Equal(0, await RenglonesDelProductoAsync(espia, d));

            var fila = await UltimaBitacoraAsync(espia, d, desde);
            Assert.NotNull(fila);
            Assert.Equal("rechazado", fila!.Resultado);
            Assert.Equal(d.Cliente, fila.IDClientePedido);
            Assert.Equal(d.ClienteAjeno, fila.IDClienteEnviado);
            _out.WriteLine($"[{baseDatos}] rechazo registrado: pedido={fila.IDClientePedido} enviado={fila.IDClienteEnviado}");
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }
    }

    /// <summary>
    /// Cambiar de cliente a media captura no contamina el pedido: el primer
    /// renglon queda, el segundo se rechaza.
    /// </summary>
    /// <remarks>
    /// Es la forma exacta en que paso de verdad: el repartidor abre un pedido,
    /// cambia de cliente en la pantalla y sigue agregando. Antes el segundo
    /// renglon entraba con la tarifa del cliente equivocado.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Cambiar_De_Cliente_A_Media_Captura_No_Contamina_El_Pedido(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia, d)) return;

        var repo = Repositorio(d, out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia, d);
        var desde = DateTime.Now.AddSeconds(-5);

        try
        {
            await repo.AgregarDetalleAsync(Solicitud(d, d.Cliente, cantidad: 1), d.Usuario, Firma, CancellationToken.None);
            Assert.Equal(renglonesAntes + 1, await RenglonesDelPedidoAsync(espia, d));

            /*  La cantidad que dejo la primera alta NO siempre es 1: hay
                productos que se venden por caja y legacy guarda las piezas (en
                Tauro, pedir 1 deja 16). Por eso el invariante se mide contra lo
                que quedo, no contra un numero fijo.  */
            var cantidadTrasLaBuena = await CantidadDelProductoAsync(espia, d);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.AgregarDetalleAsync(Solicitud(d, d.ClienteAjeno, cantidad: 1), d.Usuario, Firma, CancellationToken.None));

            // Ni un renglon mas, y ni una pieza mas: el procedimiento ACUMULA
            // cuando se repite el producto, asi que un segundo paso habria
            // subido la cantidad sin crear fila nueva.
            Assert.Equal(renglonesAntes + 1, await RenglonesDelPedidoAsync(espia, d));
            Assert.Equal(cantidadTrasLaBuena, await CantidadDelProductoAsync(espia, d));
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }
    }

    /// <summary>
    /// Dos peticiones a la vez, una con el cliente bueno y otra con uno ajeno:
    /// pasa solo la buena.
    /// </summary>
    /// <remarks>
    /// El candado lee <c>Pedidos.IDCliente</c> en cada llamada, asi que no
    /// depende de ningun estado compartido entre peticiones. Esta prueba existe
    /// para que siga siendo cierto si alguien decide cachear ese dato: un cache
    /// mal puesto aqui reabre el agujero.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Dos_Peticiones_A_La_Vez_Solo_Deja_Pasar_La_Del_Cliente_Correcto(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia, d)) return;

        var repoUno = Repositorio(d, out var cacheUno, out var capacidadesUno);
        var repoDos = Repositorio(d, out var cacheDos, out var capacidadesDos);
        await CalentarEmpresaCsAsync(capacidadesUno, cacheUno);
        await CalentarEmpresaCsAsync(capacidadesDos, cacheDos);

        var renglonesAntes = await RenglonesDelPedidoAsync(espia, d);
        var desde = DateTime.Now.AddSeconds(-5);

        try
        {
            var buena = repoUno.AgregarDetalleAsync(Solicitud(d, d.Cliente, cantidad: 1), d.Usuario, Firma, CancellationToken.None);
            var mala = repoDos.AgregarDetalleAsync(Solicitud(d, d.ClienteAjeno, cantidad: 1), d.Usuario, Firma, CancellationToken.None);

            await Assert.ThrowsAsync<ArgumentException>(() => mala);
            await buena;

            Assert.Equal(renglonesAntes + 1, await RenglonesDelPedidoAsync(espia, d));

            /*  Se cuentan los eventos y no las piezas: la cantidad depende del
                empaque del producto y aqui lo que importa es que entro UNA alta
                y se rechazo UNA llamada.  */
            Assert.Equal(1, await EventosAsync(espia, d, desde, "agregado"));
            Assert.Equal(1, await EventosAsync(espia, d, desde, "rechazado"));
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }
    }

    // ==================================================================
    // 3. SIMULAR NO SIMULA: ESCRIBE
    // ==================================================================

    /// <summary>
    /// Aunque la peticion pida simular con la lista de otro, el descuento sale
    /// de la lista del cliente del pedido.
    /// </summary>
    /// <remarks>
    /// En el procedimiento, el INSERT y el COMMIT no estan condicionados por
    /// @Simular, y en el <c>sp_w_</c> ese parametro solo sirve para SALTARSE el
    /// candado de apartado. Es decir: una llamada marcada como simulacion graba
    /// un renglon real, con la lista que le pasen y sin validar existencias. El
    /// repositorio los manda en cero siempre, sin mirar el cuerpo.
    ///
    /// Lo que se mide es el efecto, no el parametro: el renglon tiene que salir
    /// con el MISMO porcentaje que sale sin pedir simulacion.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Pedir_Simular_Con_Otra_Lista_No_Cambia_El_Descuento(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;
        if (!await PrecondicionesAsync(espia, d)) return;

        var repo = Repositorio(d, out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);
        var desde = DateTime.Now.AddSeconds(-5);

        string? normal;
        try
        {
            await repo.AgregarDetalleAsync(Solicitud(d, d.Cliente, cantidad: 1), d.Usuario, Firma, CancellationToken.None);
            normal = await PorcentajesDelProductoAsync(espia, d);
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
        }

        string? simulado;
        try
        {
            var peticion = Solicitud(d, d.Cliente, cantidad: 1);
            peticion.Simular = 1;
            peticion.IDDescuentoSimular = 342;   // una lista real de otro cliente

            await repo.AgregarDetalleAsync(peticion, d.Usuario, Firma, CancellationToken.None);
            simulado = await PorcentajesDelProductoAsync(espia, d);
        }
        finally
        {
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }

        _out.WriteLine($"[{baseDatos}] sin simular = {normal} | pidiendo simular = {simulado}");
        Assert.Equal(normal, simulado);
    }

    // ==================================================================
    // 4. LO QUE EL CANDADO NO DEBE ESTORBAR
    // ==================================================================

    /// <summary>
    /// Una COMPRA no lleva cliente y el candado no le aplica.
    /// </summary>
    /// <remarks>
    /// Las compras (documento 11) toman la tarifa del PROVEEDOR y mandan
    /// IDCliente = 0 a proposito. Si el candado las midiera contra
    /// <c>Pedidos.IDCliente</c>, dejaria de poder capturarse una compra.
    ///
    /// La prueba no intenta capturar una compra de verdad -eso toca inventario
    /// y proveedores-: comprueba que lo que falle, si falla, NO sea el candado.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Una_Compra_No_La_Para_El_Candado_De_Cliente(string baseDatos)
    {
        var d = DatosDe(baseDatos);
        await using var espia = await EspiaAsync(d);
        if (espia == null) return;

        var repo = Repositorio(d, out var cache, out var capacidades);
        await CalentarEmpresaCsAsync(capacidades, cache);

        var peticion = Solicitud(d, idCliente: 0, cantidad: 1);
        peticion.IDTipoDocumento = 11;   // COMPRA
        peticion.IDProveedor = 1;

        var desde = DateTime.Now.AddSeconds(-5);
        try
        {
            await repo.AgregarDetalleAsync(peticion, d.Usuario, Firma, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.DoesNotContain("otro cliente", ex.Message, StringComparison.OrdinalIgnoreCase);
            _out.WriteLine($"[{baseDatos}] la compra fallo por otra cosa, que es lo correcto: {ex.Message}");
        }
        finally
        {
            /*  Limpia SIEMPRE, tambien cuando la compra entra. La primera version
                no lo hacia y dejo el producto dentro del pedido de trabajo: las
                otras pruebas se saltaron solas en la corrida siguiente y pasaron
                en verde sin haber medido nada.  */
            await BorrarRenglonDeLaPruebaAsync(espia, d);
            await LimpiarBitacoraAsync(espia, d, desde);
        }
    }

    // ==================================================================
    // Apoyos
    // ==================================================================

    private sealed record FilaBitacora(
        string Resultado, int IDClientePedido, int IDClienteEnviado,
        int IDProducto, string? Porcentajes, int IDDescuento);

    private static async Task<FilaBitacora?> UltimaBitacoraAsync(SqlConnection espia, Datos d, DateTime desde)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 Resultado, IDClientePedido, IDClienteEnviado, IDProducto, Porcentajes, IDDescuento
  FROM dbo.WPedidoRenglonBitacora
 WHERE IDPedido = @IDPedido AND Fecha >= @Desde
 ORDER BY IDPedidoRenglonBitacora DESC;", espia);
        cmd.Parameters.AddWithValue("@IDPedido", d.Pedido);
        cmd.Parameters.AddWithValue("@Desde", desde);
        await using var lector = await cmd.ExecuteReaderAsync();
        if (!await lector.ReadAsync()) return null;
        return new FilaBitacora(
            lector.GetString(0), lector.GetInt32(1), lector.GetInt32(2), lector.GetInt32(3),
            lector.IsDBNull(4) ? null : lector.GetString(4), lector.GetInt32(5));
    }

    private static async Task<int> EventosAsync(SqlConnection espia, Datos d, DateTime desde, string resultado)
    {
        await using var cmd = new SqlCommand(@"
SELECT COUNT(*) FROM dbo.WPedidoRenglonBitacora
 WHERE IDPedido = @IDPedido AND Fecha >= @Desde AND Resultado = @Resultado;", espia);
        cmd.Parameters.AddWithValue("@IDPedido", d.Pedido);
        cmd.Parameters.AddWithValue("@Desde", desde);
        cmd.Parameters.AddWithValue("@Resultado", resultado);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task LimpiarBitacoraAsync(SqlConnection espia, Datos d, DateTime desde)
    {
        await using var cmd = new SqlCommand(
            "DELETE FROM dbo.WPedidoRenglonBitacora WHERE IDPedido = @IDPedido AND Fecha >= @Desde;", espia);
        cmd.Parameters.AddWithValue("@IDPedido", d.Pedido);
        cmd.Parameters.AddWithValue("@Desde", desde);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<bool> PrecondicionesAsync(SqlConnection espia, Datos d)
    {
        var estatus = await EscalarAsync(espia, $"SELECT IDStatusPedido FROM Pedidos WHERE IDPedido = {d.Pedido}");
        if (estatus == null)
        {
            _out.WriteLine($"[{d.BaseDatos}] el pedido {d.Pedido} ya no existe: prueba saltada.");
            return false;
        }
        if (Convert.ToInt32(estatus) != 0)
        {
            _out.WriteLine($"[{d.BaseDatos}] el pedido {d.Pedido} ya no es borrador (estatus {estatus}): prueba saltada.");
            return false;
        }
        if (await RenglonesDelProductoAsync(espia, d) > 0)
        {
            _out.WriteLine($"[{d.BaseDatos}] el producto {d.Producto} ya esta en el pedido: una corrida anterior no limpio. Prueba saltada.");
            return false;
        }
        var cliente = await EscalarAsync(espia, $"SELECT IDCliente FROM Pedidos WHERE IDPedido = {d.Pedido}");
        if (Convert.ToInt32(cliente) != d.Cliente)
        {
            _out.WriteLine($"[{d.BaseDatos}] el pedido {d.Pedido} cambio de cliente (es {cliente}): prueba saltada.");
            return false;
        }
        if (await EscalarAsync(espia, "SELECT OBJECT_ID('dbo.WPedidoRenglonBitacora')") == null)
        {
            _out.WriteLine($"[{d.BaseDatos}] falta la tabla WPedidoRenglonBitacora (Querrys/59): prueba saltada.");
            return false;
        }
        return true;
    }

    private static PedidoAgregarDetalleRequest Solicitud(Datos d, int idCliente, int cantidad) => new()
    {
        IDPedido = d.Pedido,
        IDEmpresa = 1,
        IDCliente = idCliente,
        IDAgenteAutoriza = d.Agente,
        IDDomicilio = d.Domicilio,
        IDTipoDocumento = TipoDocumento,
        Fecha = string.Empty,
        IDAlmacen = d.Almacen,
        IDProducto = d.Producto,
        Cantidad = cantidad,
        Equipo = Firma
    };

    private static VentasPedidoCapturaRepository Repositorio(
        Datos d, out IMemoryCache cache, out CapacidadesEmpresaRepository capacidades)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Main"] = Cadena(d.BaseDatos, Firma) })
            .Build();
        cache = new MemoryCache(new MemoryCacheOptions());
        capacidades = new CapacidadesEmpresaRepository(configuracion);
        return new VentasPedidoCapturaRepository(
            configuracion, cache, NullLogger<VentasPedidoCapturaRepository>.Instance, capacidades);
    }

    private static Task CalentarEmpresaCsAsync(CapacidadesEmpresaRepository capacidades, IMemoryCache cache)
        => EmpresaCsPorOmision.ResolverAsync(capacidades, cache, CancellationToken.None);

    private async Task<SqlConnection?> EspiaAsync(Datos d)
    {
        var conexion = new SqlConnection(Cadena(d.BaseDatos, "zzEspiaCandadoCliente"));
        try
        {
            await conexion.OpenAsync();
            return conexion;
        }
        catch (SqlException ex)
        {
            _out.WriteLine($"[{d.BaseDatos}] sin SQL Server, prueba saltada: {ex.Message}");
            await conexion.DisposeAsync();
            return null;
        }
    }

    private static Task<int> RenglonesDelPedidoAsync(SqlConnection espia, Datos d)
        => ContarAsync(espia, $"SELECT COUNT(*) FROM [Pedidos Detalle] WHERE IDPedido = {d.Pedido}");

    private static Task<int> RenglonesDelProductoAsync(SqlConnection espia, Datos d)
        => ContarAsync(espia, $"SELECT COUNT(*) FROM [Pedidos Detalle] WHERE IDPedido = {d.Pedido} AND IDProducto = {d.Producto}");

    private static async Task<int> CantidadDelProductoAsync(SqlConnection espia, Datos d)
    {
        var valor = await EscalarAsync(espia,
            $"SELECT SUM(Cantidad) FROM [Pedidos Detalle] WHERE IDPedido = {d.Pedido} AND IDProducto = {d.Producto}");
        return valor == null ? 0 : Convert.ToInt32(valor);
    }

    private static async Task<string?> PorcentajesDelProductoAsync(SqlConnection espia, Datos d)
    {
        var valor = await EscalarAsync(espia,
            $"SELECT TOP 1 Porcentajes FROM [Pedidos Detalle] WHERE IDPedido = {d.Pedido} AND IDProducto = {d.Producto} ORDER BY IDPedidoDetalle DESC");
        return valor?.ToString();
    }

    /// <summary>
    /// Quita el renglon de la prueba por SQL directo, no por el repositorio: lo
    /// que se esta midiendo es el camino de ESCRITURA, y limpiar por el mismo
    /// camino podria tapar su propio fallo.
    /// </summary>
    private static async Task BorrarRenglonDeLaPruebaAsync(SqlConnection espia, Datos d)
    {
        await using var cmd = new SqlCommand(
            $"DELETE FROM [Pedidos Detalle] WHERE IDPedido = {d.Pedido} AND IDProducto = {d.Producto};", espia);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> ContarAsync(SqlConnection espia, string sql)
    {
        var valor = await EscalarAsync(espia, sql);
        return valor == null ? 0 : Convert.ToInt32(valor);
    }

    private static async Task<object?> EscalarAsync(SqlConnection espia, string sql)
    {
        await using var cmd = new SqlCommand(sql, espia);
        var valor = await cmd.ExecuteScalarAsync();
        return valor == DBNull.Value ? null : valor;
    }
}
