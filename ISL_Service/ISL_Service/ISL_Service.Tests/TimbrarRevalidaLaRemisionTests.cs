using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Facturacion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  TIMBRAR VUELVE A COMPROBAR QUE LA REMISION SE PUEDE FACTURAR
  ══════════════════════════════════════════════════════════════════════════

  EL AGUJERO QUE CIERRA
  ---------------------
  `preparar` comprueba la reja pero NO lanza: devuelve el motivo en el campo
  `bloqueo` de la respuesta, para que la pantalla lo ensene con el resto a la
  vista. Eso esta bien para la pantalla y era la UNICA defensa: un cliente que
  ignorara ese campo —o una llamada a la API a mano— timbraba un CFDI REAL de
  una remision CANCELADA. `VentasFacturacionService` no mencionaba `Cancelada`
  ni una vez.

  Medido el 2026-10-15: la remision 154134 de MacZ esta cancelada desde el
  13-10-2026 (`sp_n_ConsultaVentas` devuelve `Cancelada = "SI"`), `preparar`
  contesta 200 con su bloqueo, y el timbrado no miraba nada de eso.

  Y UN CFDI NO SE DESHACE: hay que cancelarlo ante el SAT. Es preexistente,
  pero hasta ahora nadie timbraba por este camino, y la app que se publica
  lleva Facturar — seria entregar un camino nuevo a un agujero viejo.

  ⚠️  AQUI NO SE TIMBRA NUNCA, Y NO ES UNA PRECAUCION: ES UN REQUISITO.
  ---------------------------------------------------------------------
  No hay sandbox de Facturapi y el candado antiproduccion COINCIDE en las dos
  bases locales, asi que timbrar desde esta maquina emite un CFDI real. Todas
  las pruebas de abajo se detienen ANTES del envio al PAC: comprueban que la
  validacion RECHAZA, nunca que el PAC acepta.

  Como se consigue, y es a proposito: el cliente de Facturapi y el repositorio
  van sin configurar. Si alguna de estas pruebas llegara a pasar la reja,
  reventaria al intentar hablar con el PAC en vez de timbrar — y el caso que se
  mide es precisamente que NO llega. El caso "se puede facturar y timbra" NO SE
  PRUEBA AQUI: no se puede probar sin emitir un comprobante fiscal, y por eso
  lo que se mide es que la reja deja pasar (`SinReclamoDeLaReja`), no que el
  timbrado termine.
*/
public class TimbrarRevalidaLaRemisionTests
{
    private readonly ITestOutputHelper _out;
    public TimbrarRevalidaLaRemisionTests(ITestOutputHelper salida) => _out = salida;

    // ==================================================================
    // Casos de la reja, con renglones armados como los devuelve la consulta
    // ==================================================================

    /// <summary>
    /// EL CASO DEL INFORME: remision cancelada. Antes timbraba.
    /// </summary>
    [Fact]
    public async Task Una_Remision_Cancelada_No_Se_Timbra()
    {
        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Timbrar(Fila(idVenta: 154134, folio: "135020", cancelada: "SI")));

        Assert.Contains("CANCELADA", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("135020", e.Message);
        _out.WriteLine(e.Message);
    }

    /// <summary>
    /// Y las otras dos puertas de la misma reja, que ahora tambien alcanzan al
    /// timbrado: total cero y un cambio de vacios.
    /// </summary>
    [Fact]
    public async Task Una_Remision_Con_Total_Cero_No_Se_Timbra()
    {
        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Timbrar(Fila(idVenta: 1, folio: "100", totalPagar: 0m)));

        Assert.Contains("TOTAL A PAGAR CERO", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_Cambio_De_Vacios_No_Se_Timbra()
    {
        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Timbrar(Fila(idVenta: 1, folio: "CV-123")));

        Assert.Contains("cambio de vac", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Clientes distintos en el mismo CFDI: tampoco. Es la primera puerta de la
    /// reja y se comprueba para dejar claro que el timbrado hereda TODAS, no
    /// solo la de cancelada.
    /// </summary>
    [Fact]
    public async Task Dos_Remisiones_De_Clientes_Distintos_No_Se_Timbran()
    {
        var e = await Assert.ThrowsAsync<ConflictException>(() => Timbrar(
            Fila(idVenta: 1, folio: "100", idCliente: 10),
            Fila(idVenta: 2, folio: "101", idCliente: 99)));

        Assert.False(string.IsNullOrWhiteSpace(e.Message));
        _out.WriteLine(e.Message);
    }

    // ==================================================================
    // FALLAR CERRADO
    // ==================================================================

    /// <summary>
    /// NO VINO EL RENGLON: no se timbra. De lo que no se sabe nada no se emite
    /// un comprobante fiscal.
    /// </summary>
    [Fact]
    public async Task Si_La_Remision_No_Aparece_No_Se_Timbra()
    {
        /* Se PIDE la remision 1 y la consulta no devuelve ningun renglon. No es
           lo mismo que no pedir ninguna: eso es una factura sin remision y no
           pasa por la reja (ver Una_Factura_Sin_Remision_No_Relee_Nada). */
        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Servicio(new ConsultaDoble()).TimbrarAsync(Peticion(1), 1125, "QA", default));

        Assert.Contains("NO SE ENCONTRO", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NO SE TIMBRO", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Y si vinieron ALGUNAS pero no todas, tampoco: se nombra la que falta.
    /// </summary>
    [Fact]
    public async Task Si_Falta_Una_De_Varias_No_Se_Timbra()
    {
        var consulta = new ConsultaDoble(Fila(idVenta: 1, folio: "100", idCliente: 10));

        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Servicio(consulta).TimbrarAsync(Peticion(1, 2), 1125, "QA", default));

        Assert.Contains("NO SE ENCONTRO", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", e.Message);
    }

    /// <summary>
    /// LA LECTURA REVIENTA: no se timbra. Un error al leer no puede
    /// interpretarse como "esta bien" — es justo la direccion en la que un
    /// candado se cae solo.
    /// </summary>
    [Fact]
    public async Task Si_No_Se_Puede_Leer_El_Estado_No_Se_Timbra()
    {
        var consulta = new ConsultaQueRevienta(new InvalidOperationException("la base no contesta"));

        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Servicio(consulta).TimbrarAsync(Peticion(1), 1125, "QA", default));

        Assert.Contains("NO SE PUDO COMPROBAR", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NO SE TIMBRO", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Una CANCELACION no se disfraza de fallo de la base: sube tal cual. Lo
    /// que importa es que tampoco timbro.
    /// </summary>
    [Fact]
    public async Task Una_Cancelacion_Sube_Tal_Cual()
    {
        var consulta = new ConsultaQueRevienta(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Servicio(consulta).TimbrarAsync(Peticion(1), 1125, "QA", default));
    }

    // ==================================================================
    // LO QUE NO SE PUEDE ROMPER
    // ==================================================================

    /// <summary>
    /// UNA REMISION BUENA PASA LA REJA.
    ///
    /// No se comprueba que timbre —eso emitiria un CFDI real— sino que la reja
    /// NO es lo que la detiene: el fallo que llega es ya del cliente de
    /// Facturapi o de las constantes, o sea de pasos posteriores. Si la reja la
    /// hubiera rechazado, el mensaje seria el de la reja.
    /// </summary>
    [Fact]
    public async Task Una_Remision_Vigente_Pasa_La_Reja()
    {
        var consulta = new ConsultaDoble(Fila(idVenta: 1, folio: "100", idCliente: 10));

        var excepcion = await Record.ExceptionAsync(() =>
            Servicio(consulta).TimbrarAsync(Peticion(1), 1125, "QA", default));

        /* Algo falla despues —el repositorio va en null!— pero NO es la reja.
           Que sea un NullReferenceException es la prueba de que se paso de la
           reja y se detuvo en el paso siguiente, sin tocar el PAC. */
        Assert.NotNull(excepcion);
        Assert.IsType<NullReferenceException>(excepcion);
        foreach (var dela in new[] { "CANCELADA", "TOTAL A PAGAR CERO", "cambio de vac",
                                     "NO SE ENCONTRO", "NO SE PUDO COMPROBAR" })
        {
            Assert.DoesNotContain(dela, excepcion!.Message, StringComparison.OrdinalIgnoreCase);
        }

        _out.WriteLine($"paso la reja; se detuvo despues con: {excepcion!.GetType().Name}: {excepcion.Message}");
    }

    /// <summary>
    /// UNA FACTURA SIN REMISION NO PASA POR LA REJA, y no puede pasar: no hay
    /// remision que releer. Si se le exigiera, se habria cerrado una funcion
    /// que hoy existe.
    ///
    /// Se comprueba que la consulta NO se llama: con cero remisiones, releer
    /// seria un viaje a preguntar por nada.
    /// </summary>
    [Fact]
    public async Task Una_Factura_Sin_Remision_No_Relee_Nada()
    {
        var consulta = new ConsultaDoble();

        await Record.ExceptionAsync(() =>
            Servicio(consulta).TimbrarAsync(Peticion(), 1125, "QA", default));

        Assert.Equal(0, consulta.Llamadas);
    }

    /// <summary>
    /// LA LECTURA PIDE VER LAS DE OTROS, y es deliberado: aqui no se autoriza a
    /// nadie, se mira el ESTADO de la remision para poder NEGARSE. Con
    /// `puedeVerDeOtros: false`, la remision de otro agente no vendria, la reja
    /// la tomaria por inexistente y dejaria de timbrar a quien hoy timbra
    /// legitimamente: un candado de integridad convertido en una restriccion de
    /// visibilidad nueva.
    /// </summary>
    [Fact]
    public async Task El_Estado_Se_Lee_Pudiendo_Ver_Las_De_Otros()
    {
        var consulta = new ConsultaDoble(Fila(idVenta: 1, folio: "100", idCliente: 10));

        await Record.ExceptionAsync(() =>
            Servicio(consulta).TimbrarAsync(Peticion(1), 1125, "QA", default));

        Assert.True(consulta.UltimoPuedeVerDeOtros);
        Assert.Equal(1125, consulta.UltimoIdUsuario);
    }

    /// <summary>
    /// Y EL ESTADO SE LEE DEL SERVIDOR, no de la peticion: el cliente puede
    /// mandar lo que quiera en el cuerpo y la reja sigue mirando lo que dice la
    /// consulta. Aqui la peticion "declara" folios inventados y la remision
    /// cancelada se rechaza igual.
    /// </summary>
    [Fact]
    public async Task Lo_Que_Declare_El_Cliente_No_Cambia_La_Reja()
    {
        var consulta = new ConsultaDoble(Fila(idVenta: 7, folio: "777", cancelada: "SI"));

        var peticion = Peticion(7);
        peticion.FoliosVenta = new List<string> { "NO-CANCELADA-LO-JURO" };

        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            Servicio(consulta).TimbrarAsync(peticion, 1125, "QA", default));

        Assert.Contains("CANCELADA", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("777", e.Message);
    }

    // ==================================================================
    // Andamio
    // ==================================================================

    private static Task Timbrar(params Dictionary<string, object?>[] filas)
        => Servicio(new ConsultaDoble(filas))
            .TimbrarAsync(Peticion(filas.Select(f => Convert.ToInt32(f["IDVenta"])).ToArray()),
                1125, "QA", default);

    /// <summary>
    /// Un renglon como lo devuelve `sp_n_ConsultaVentas`, con los cinco campos
    /// que mira la reja y los nombres reales de sus columnas.
    /// </summary>
    private static Dictionary<string, object?> Fila(
        int idVenta,
        string folio,
        int idCliente = 10,
        decimal totalPagar = 3033.68m,
        string cancelada = "NO") => new()
        {
            ["IDVenta"] = idVenta,
            ["IDCliente"] = idCliente,
            ["FolioFtm"] = folio,
            ["TotalPagar"] = totalPagar,
            ["Cancelada"] = cancelada
        };

    /// <summary>
    /// Una peticion COMPLETA, porque `Validar` corre antes de la reja y rechaza
    /// una peticion a medias con un ArgumentException — y entonces la prueba
    /// mediria la validacion de entrada, no la reja. Los valores son los de una
    /// factura real de MacZ; lo unico que cambia entre pruebas son las
    /// remisiones.
    /// </summary>
    private static FacturaTimbrarRequest Peticion(params int[] idsVenta) => new()
    {
        IdsVenta = idsVenta.ToList(),

        /* `Validar` exige un folio por remision. Son el dato del CLIENTE, y
           justamente por eso la reja no los mira: lee el estado del servidor
           (ver Lo_Que_Declare_El_Cliente_No_Cambia_La_Reja). */
        FoliosVenta = idsVenta.Select(id => id.ToString()).ToList(),
        TotalesFacturar = idsVenta.Select(_ => 2388.68m).ToList(),

        /* Solo lo mira `Validar` cuando NO hay remisiones (una factura sin
           remision necesita su pedido). Va siempre para que el caso de
           "sin remision" tambien llegue mas alla de la validacion de entrada
           y se pueda medir que la reja NO se invoca. */
        IdPedido = 1,
        IdEmpresa = 1,
        IdClienteFacturacion = 19299,
        FormaPago = "03",
        MetodoPago = "PUE",
        RegimenFiscal = "601",
        UsoCfdi = "G03",
        Receptor = new FacturaReceptor
        {
            Rfc = "DAZ100826S29",
            RazonSocial = "REFACCIONES AUTOMOTRICES TAURO SA DE CV",
            CodigoPostal = "07910"
        },
        Conceptos = new List<FacturaConcepto>
        {
            new()
            {
                ClaveProdServ = "26111707",
                Descripcion = "ACUMULADOR LTH L-47(LN2)-550",
                ClaveUnidad = "H87",
                Cantidad = 1m,
                PrecioUnitarioConIva = 2388.68m
            }
        }
    };

    /// <summary>
    /// El servicio REAL, con el repositorio y el cliente de Facturapi en
    /// `null!` A PROPOSITO.
    ///
    /// Es lo que garantiza que estas pruebas NO PUEDAN TIMBRAR: la reja corre
    /// antes de leer las constantes, asi que si una peticion la pasa, el
    /// metodo revienta en el siguiente paso en vez de hablar con el PAC. Dicho
    /// al reves: aqui es imposible emitir un comprobante aunque la reja
    /// falle, que es exactamente la propiedad que se quiere en una prueba de
    /// facturacion corriendo contra bases cuyo candado antiproduccion
    /// coincide.
    /// </summary>
    private static VentasFacturacionService Servicio(IVentasConsultaService consulta)
        => new(null!, null!, NullLogger<VentasFacturacionService>.Instance, consulta);

    private sealed class ConsultaQueRevienta : ConsultaDeVentasDobleBase
    {
        private readonly Exception _e;
        public ConsultaQueRevienta(Exception e) => _e = e;

        public override Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(
            VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
            => throw _e;
    }

    private sealed class ConsultaDoble : ConsultaDeVentasDobleBase
    {
        private readonly List<Dictionary<string, object?>> _filas;

        public ConsultaDoble(params Dictionary<string, object?>[] filas) => _filas = filas.ToList();

        public int Llamadas { get; private set; }
        public bool UltimoPuedeVerDeOtros { get; private set; }
        public int UltimoIdUsuario { get; private set; }

        public override Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(
            VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        {
            Llamadas++;
            UltimoPuedeVerDeOtros = puedeVerDeOtros;
            UltimoIdUsuario = idUsuarioToken;

            return Task.FromResult(new VentasConsultaRowsResponse { Rows = _filas });
        }
    }
}
