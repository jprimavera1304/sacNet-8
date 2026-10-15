using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

using Reglas = ISL_Service.Infrastructure.Facturacion.ReglasPreparacionFactura;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  EL PRECIO DEL CONCEPTO TIENE QUE SER EL NETO, NO EL DE LISTA
  ══════════════════════════════════════════════════════════════════════════

  EL DEFECTO, Y POR QUE ERA PEOR QUE UN 500
  -----------------------------------------
  `preparar` contestaba 200 —"Factura preparada."— con un concepto de
  4,480.00 cuando el total a facturar de esa misma respuesta era 2,388.68:
  2,091.32 pesos de diferencia en una remision de UN renglon, y timbrable. Un
  CFDI que no suma lo que se cobra no se nota hasta que lo mira el SAT.

  LA CAUSA, MEDIDA Y NO SUPUESTA
  ------------------------------
  LOS DOS PROCEDIMIENTOS DE DETALLE LLAMAN IGUAL A COSAS DISTINTAS. Medido el
  2026-10-15 en MacZ, remision 154134 (lista 3,862.07 sin IVA, descuento
  1,747.86, neto 2,388.68):

    sp_n_VentasDetalleMultiple:  PrecioListaConIvaRnd = 2388.68  <- el NETO
                                 FacturaPrecioListaConIva = 4480.00
    sp_n_VentasDetalle (simple): PrecioListaConIvaRnd = 4480.00  <- el de LISTA
                                 FacturaPrecioListaConIva: NO EXISTE

  `ReglasPreparacionFactura` esta escrita contra el MULTIPLE —sus comentarios
  lo citan y las pruebas de escritorio usan sus numeros—, asi que leyendo del
  simple se llevaba el precio de LISTA al concepto. Eso explica las dos cosas
  que se vieron: el precio 4,480.00 y `precioListaLegacy = 0` (la columna de
  donde sale ese campo no existe en el simple).

  Y SALTABA JUSTO EN EL CASO NORMAL. El servicio mandaba UNA sola remision al
  procedimiento simple, para poder pasarle `@IDDescuento`. Con dos o mas
  remisiones iba al multiple y las cifras salian BIEN — que es lo que hacia
  que pareciera un problema de datos y no de codigo. Facturar desde la app es
  casi siempre una remision.

  QUE VIGILA ESTA PRUEBA
  ----------------------
  Lee las partidas por LA MISMA PUERTA que usa la preparacion del CFDI
  (`ConsultarDetalleParaFacturarAsync`, con el repositorio y el servicio de
  verdad), arma los conceptos con la misma regla, y compara contra la cifra que
  calcula LA PROPIA BASE. No hay numeros escritos a mano como oraculo: el
  oraculo es `ImporteConIvaRnd`, que es lo que la pantalla ensena y lo que el
  cliente paga.

  Si alguien hace que esa puerta vuelva al procedimiento simple, esto falla con
  las dos cifras a la vista. Comprobado rompiendolo a proposito.

  NO TIMBRA NADA: solo lee.
*/
[Collection(PruebasContraLaBase.Nombre)]
public class PrecioDelConceptoContraLaBaseTests
{
    private readonly ITestOutputHelper _out;
    public PrecioDelConceptoContraLaBaseTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static IConfiguration ConfigDe(string baseDatos) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = CadenaDe(baseDatos)
            })
            .Build();

    /// <summary>El servicio real, con su repositorio real.</summary>
    private static VentasPagosOperacionesService DetallesDe(string baseDatos) =>
        new(new VentasPagosOperacionesRepository(ConfigDe(baseDatos)));

    private async Task<bool> HayBaseAsync(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); return true; }
        catch (SqlException e)
        {
            _out.WriteLine($"Sin base {baseDatos}, la prueba se salta: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// EL CASO CENTRAL: la suma de `cantidad x precioUnitarioConIva` de los
    /// conceptos tiene que ser el dinero de la remision, el que la base
    /// calcula.
    /// </summary>
    /// <remarks>
    /// Las remisiones son de las dos empresas y de los dos tamanos, porque el
    /// defecto se escondia en el de UNA:
    ///
    ///   MacZ 154134  — Zaragoza, UNA partida de UNA pieza. Es el caso que
    ///                  devolvia 4,480.00 en vez de 2,388.68.
    ///   MacZ 153251  — Zaragoza, DOS partidas con cantidad 3 y 2. Aqui el
    ///                  simple decia 3,620.00 y 3,499.99 donde el neto por
    ///                  pieza es 2,099.60 y 2,030.00.
    ///   Produccion_svr 390271 — Tauro, una partida de una pieza.
    ///   Produccion_svr 390269 — Tauro, una partida de DIEZ piezas, que es
    ///                  donde se ve si la division por la cantidad esta bien.
    /// </remarks>
    [Theory]
    [InlineData("MacZ", 154134, true)]
    [InlineData("MacZ", 153251, true)]
    [InlineData("Produccion_svr", 390271, false)]
    [InlineData("Produccion_svr", 390269, false)]
    public async Task La_Suma_De_Los_Conceptos_Es_El_Dinero_De_La_Remision(
        string baseDatos, int idVenta, bool esZaragoza)
    {
        if (!await HayBaseAsync(baseDatos)) return;

        var constantes = await new VentasFacturacionRepository(ConfigDe(baseDatos))
            .LeerConstantesAsync(default);

        /* La variante sale de `funcionalidad`, igual que en el servicio. */
        Assert.Equal(esZaragoza, (constantes.Funcionalidad ?? string.Empty)
            .ToUpperInvariant().Contains("ZARA", StringComparison.Ordinal));

        /*
          UNA SOLA REMISION, Y POR LA PUERTA DE FACTURAR. Las dos cosas
          importan: es el caso que se rompia, y es el camino que lo decide. Si
          esto se cambiara por `ConsultarDetalleAsync` con un solo id, leeria
          del procedimiento simple y la prueba fallaria — que es justo lo que
          se quiere.
        */
        var filas = (await DetallesDe(baseDatos)
            .ConsultarDetalleParaFacturarAsync(new[] { idVenta }, default)).Rows;

        if (filas.Count == 0)
        {
            _out.WriteLine($"{baseDatos}: la remision {idVenta} no esta, no se pudo medir.");
            return;
        }

        var partidas = filas
            .Select(f => (IReadOnlyDictionary<string, object?>)f)
            .ToList();

        var conceptos = Reglas.ConceptosDeLasPartidas(
            partidas, conIva: esZaragoza, porcentajeIva: constantes.PorcentajeIva);

        Assert.NotEmpty(conceptos);

        /*
          EL ORACULO: `ImporteConIvaRnd` por partida, que es el importe CON IVA
          y con el descuento YA aplicado. Sumado, es el dinero de la remision
          sin los cascos — exactamente lo que `preparar` publica como total a
          facturar en dinero.
        */
        var dineroDeLaBase = partidas.Sum(p => Numero(p, "ImporteConIvaRnd"));
        var dineroDeLosConceptos = conceptos.Sum(c => c.PrecioUnitarioConIva * c.Cantidad);

        /*
          Un peso de tolerancia, y por una razon concreta: `ImporteConIvaRnd`
          esta REDONDEADO a peso por la base (2534.00 donde el importe exacto
          es 2534.40), asi que exigir el centavo seria exigir que no redondee.
          Un peso no esconde nada de lo que esta prueba busca: el defecto eran
          2,091.32 pesos en un renglon, y en 153251 serian mas de 7,000.
        */
        Assert.True(Math.Abs(dineroDeLosConceptos - dineroDeLaBase) <= 1.00m,
            $"{baseDatos} remision {idVenta}: los conceptos suman {dineroDeLosConceptos} "
            + $"y la base dice {dineroDeLaBase}. Si la diferencia es grande, el precio del "
            + $"concepto es el de LISTA y no el NETO: mira de que procedimiento vienen las "
            + $"partidas (ver ConsultarDetalleParaFacturarAsync).");

        _out.WriteLine(
            $"{baseDatos} remision {idVenta}: {conceptos.Count} conceptos, "
            + $"conceptos={dineroDeLosConceptos}, base={dineroDeLaBase}");
    }

    /// <summary>
    /// Y QUE EL PRECIO DE LISTA NO SE CUELE COMO PRECIO DEL CONCEPTO, dicho
    /// renglon por renglon.
    ///
    /// Es la otra cara de la de arriba: la suma podria cuadrar por casualidad
    /// con dos partidas que se compensaran. Aqui se compara cada concepto con
    /// el neto por pieza de SU partida.
    /// </summary>
    [Theory]
    [InlineData("MacZ", 154134)]
    [InlineData("MacZ", 153251)]
    public async Task En_Zaragoza_Cada_Concepto_Es_El_Neto_Por_Pieza(string baseDatos, int idVenta)
    {
        if (!await HayBaseAsync(baseDatos)) return;

        var filas = (await DetallesDe(baseDatos)
            .ConsultarDetalleParaFacturarAsync(new[] { idVenta }, default)).Rows;
        if (filas.Count == 0) return;

        var partidas = filas.Select(f => (IReadOnlyDictionary<string, object?>)f).ToList();

        var conceptos = Reglas.ConceptosDeLasPartidas(partidas, conIva: true);

        foreach (var c in conceptos)
        {
            var fila = partidas.First(p =>
                Convert.ToInt32(p["IDVentaDetalle"]) == c.IdVentaDetalle);

            var cantidad = Numero(fila, "Cantidad");
            var netoPorPieza = cantidad > 0m ? Numero(fila, "ImporteConIvaRnd") / cantidad : 0m;
            var listaConIva = Numero(fila, "FacturaPrecioListaConIva");

            Assert.True(Math.Abs(c.PrecioUnitarioConIva - netoPorPieza) <= 1.00m,
                $"{baseDatos} {idVenta} partida {c.IdVentaDetalle}: el concepto dice "
                + $"{c.PrecioUnitarioConIva} y el neto por pieza es {netoPorPieza}.");

            /* Y si en esa partida hubo descuento, el precio del concepto NO
               puede ser el de lista: es la comprobacion que caza el defecto
               aunque el neto y la lista estuvieran cerca. */
            if (listaConIva > 0m && Math.Abs(listaConIva - netoPorPieza) > 1.00m)
            {
                Assert.True(Math.Abs(c.PrecioUnitarioConIva - listaConIva) > 1.00m,
                    $"{baseDatos} {idVenta} partida {c.IdVentaDetalle}: el concepto se llevo el "
                    + $"precio de LISTA ({listaConIva}) en vez del neto ({netoPorPieza}).");
            }
        }
    }

    /// <summary>
    /// LA TRAMPA, DEJADA ESCRITA CON LA BASE EN LA MANO.
    ///
    /// Que los dos procedimientos usan el mismo nombre para cosas distintas no
    /// es una suposicion de quien escribio el arreglo: se mide aqui. Si algun
    /// dia alguien iguala los dos procedimientos, esto falla y obliga a
    /// revisar si la puerta separada sigue haciendo falta — en vez de que la
    /// explicacion se quede en un comentario que ya no es verdad, que es como
    /// se nos colo el defecto del IVA.
    /// </summary>
    [Fact]
    public async Task Los_Dos_Procedimientos_No_Dicen_Lo_Mismo_En_Esa_Columna()
    {
        if (!await HayBaseAsync("MacZ")) return;

        var delMultiple = (await DetallesDe("MacZ")
            .ConsultarDetalleParaFacturarAsync(new[] { 154134 }, default)).Rows;

        var delSimple = (await DetallesDe("MacZ").ConsultarDetalleAsync(
            new ISL_Service.Application.DTOs.VentasPagosOperaciones.VentasDetalleConsultaRequest
            {
                IdsVenta = new List<int> { 154134 }
            }, default)).Rows;

        if (delMultiple.Count == 0 || delSimple.Count == 0) return;

        var multiple = (IReadOnlyDictionary<string, object?>)delMultiple[0];
        var simple = (IReadOnlyDictionary<string, object?>)delSimple[0];

        var netoMultiple = Numero(multiple, "PrecioListaConIvaRnd");
        var listaSimple = Numero(simple, "PrecioListaConIvaRnd");

        // En el multiple es el NETO y coincide con el importe por pieza.
        var cantidad = Numero(multiple, "Cantidad");
        Assert.True(cantidad > 0m);
        Assert.True(Math.Abs(netoMultiple - Numero(multiple, "ImporteConIvaRnd") / cantidad) <= 1.00m);

        // En el simple es OTRO numero, y mas alto: es el de lista.
        Assert.True(listaSimple > netoMultiple,
            $"El simple dice {listaSimple} y el multiple {netoMultiple}. Si ya son iguales, "
            + "los procedimientos se igualaron y hay que revisar "
            + "ConsultarDetalleParaFacturarAsync.");

        _out.WriteLine($"multiple={netoMultiple} simple={listaSimple}");
    }

    private static decimal Numero(IReadOnlyDictionary<string, object?> fila, string columna)
        => Reglas.Numero(Reglas.Crudo(fila, columna));
}
