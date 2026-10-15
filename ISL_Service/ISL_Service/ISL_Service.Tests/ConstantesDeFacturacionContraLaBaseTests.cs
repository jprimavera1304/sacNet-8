using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  LAS CONSTANTES DE FACTURACION, CONTRA LA BASE DE VERDAD
  ══════════════════════════════════════════════════════════════════════════

  EL DEFECTO QUE ESTA PRUEBA EXISTE PARA QUE NO VUELVA
  ----------------------------------------------------
  `LeerConstantesAsync` pedia una columna llamada `PorcentajeIva`, y en
  `dbo.Constantes` ESA COLUMNA NO EXISTE: se llama `IVA`. Consecuencia medida
  el 2026-10-15 con cuatro remisiones distintas y en las dos empresas:
  `POST /api/ventas/facturacion/preparar` contestaba
  "Invalid column name 'PorcentajeIva'" SIEMPRE, para cualquier folio. Y no
  solo preparar: el mismo metodo lo usa TIMBRAR
  (`VentasFacturacionService`), asi que por el camino del web y del telefono
  no se podia emitir un comprobante — encaja con que en seis meses todo
  hubiera salido de terminales de Mac31.

  POR QUE TIENE QUE SER CONTRA LA BASE Y NO UNA PRUEBA DE ESCRITORIO
  ------------------------------------------------------------------
  Porque el defecto era un NOMBRE DE COLUMNA. Ninguna prueba con un doble lo
  habria visto: el doble contesta lo que se le diga que conteste. Lo unico que
  caza "esta columna no existe" es ejecutar la consulta donde viven las
  columnas. Por eso aqui se construye el repositorio DE VERDAD y se le pide el
  dato a cada base.

  Y SE COMPRUEBA EL VALOR, NO SOLO QUE NO REVIENTE
  ------------------------------------------------
  Esa es la otra mitad, y es la que importa mas. `IVA` esta guardado como
  16.00, no como 0.16, y el campo se lee sin tocar. Aliasear `IVA` tal cual
  quita el 500 y deja un IVA CIEN VECES MAYOR... que ademas timbra. Es decir:
  cambiar un error visible por un error FISCAL SILENCIOSO. Una prueba que solo
  mirara que no hay excepcion habria dado luz verde a eso.

  Exigiendo 0.16 se cazan las dos trampas con una sola comprobacion: si
  alguien vuelve a poner el nombre malo, revienta; si alguien quita el
  `/ 100.0`, sale 16 y falla.

  SE SALTA SOLA si no hay SQL Server a mano, para que la suite siga corriendo
  en una maquina sin base. Pero NO se salta en silencio: dice por que.
*/
[Collection(PruebasContraLaBase.Nombre)]
public class ConstantesDeFacturacionContraLaBaseTests
{
    private readonly ITestOutputHelper _out;
    public ConstantesDeFacturacionContraLaBaseTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static VentasFacturacionRepository RepositorioDe(string baseDatos)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = CadenaDe(baseDatos)
            })
            .Build();

        return new VentasFacturacionRepository(config);
    }

    private async Task<bool> HayBaseAsync(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try
        {
            await conn.OpenAsync();
            return true;
        }
        catch (SqlException e)
        {
            _out.WriteLine($"Sin base {baseDatos}, la prueba se salta: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// EL IVA SALE EN TANTO POR UNO Y VALE 0.16 EN LAS DOS EMPRESAS.
    /// </summary>
    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task El_Porcentaje_De_Iva_Sale_En_Tanto_Por_Uno(string baseDatos)
    {
        if (!await HayBaseAsync(baseDatos)) return;

        var constantes = await RepositorioDe(baseDatos).LeerConstantesAsync(default);

        /*
          0.16 EXACTO. No "mayor que cero" y no "menor que uno":

            * con el nombre malo esto no llega, revienta antes;
            * sin el `/ 100.0` saldria 16 — que es > 0 y pasaria un
              "mayor que cero" tan ricamente, y luego timbraria un CFDI con un
              IVA cien veces mayor;
            * y si algun dia el SAT cambia la tasa, que falle AQUI y a
              proposito, con el numero a la vista, en vez de que se entere el
              primer cliente que reciba una factura rara.
        */
        Assert.Equal(0.16m, constantes.PorcentajeIva);

        /* Y las otras cinco columnas tambien existen: el defecto era pedir una
           columna que no esta, asi que lo que se vigila es la consulta
           COMPLETA, no solo el IVA. Si cualquiera de las seis se renombrara,
           LeerConstantesAsync habria tirado SqlException arriba. */
        Assert.False(string.IsNullOrWhiteSpace(constantes.Funcionalidad));
        Assert.False(string.IsNullOrWhiteSpace(constantes.RfcEmisor));
        Assert.False(string.IsNullOrWhiteSpace(constantes.AppMac));

        _out.WriteLine(
            $"{baseDatos}: funcionalidad={constantes.Funcionalidad} rfc={constantes.RfcEmisor} "
            + $"iva={constantes.PorcentajeIva}");
    }

    /// <summary>
    /// Y QUE EL NOMBRE MALO SIGA SIENDO MALO, dicho con la base en la mano.
    ///
    /// Esta es la prueba del diagnostico: deja escrito, medido y no opinado,
    /// que `PorcentajeIva` no existe como columna y que `IVA` si. Si alguien
    /// anade de verdad una columna `PorcentajeIva` a `dbo.Constantes`, esto
    /// falla y obliga a decidir a mano cual de las dos manda — en vez de que
    /// aparezca un segundo origen del IVA que nadie sabe cual gana.
    /// </summary>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task En_Constantes_La_Columna_Se_Llama_IVA(string baseDatos)
    {
        if (!await HayBaseAsync(baseDatos)) return;

        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT
    HayPorcentajeIva = CASE WHEN COL_LENGTH('dbo.Constantes','PorcentajeIva') IS NULL THEN 0 ELSE 1 END,
    HayIva           = CASE WHEN COL_LENGTH('dbo.Constantes','IVA') IS NULL THEN 0 ELSE 1 END;", conn);

        await using var rd = await cmd.ExecuteReaderAsync();
        Assert.True(await rd.ReadAsync());

        Assert.Equal(0, rd.GetInt32(0));   // PorcentajeIva: no existe
        Assert.Equal(1, rd.GetInt32(1));   // IVA: existe
    }

    // ======================================================================
    // Y QUE LAS CIFRAS CUADREN: un 200 con numeros mal es lo que se quiere
    // evitar, no solo el 500
    // ======================================================================

    /*
      EL IVA NO SE COMPRUEBA SOLO COMO VALOR, SE COMPRUEBA COMO RESULTADO.

      Que `LeerConstantesAsync` conteste 0.16 esta bien, pero lo que de verdad
      importa es que con ese 0.16 el comprobante salga con las cifras de la
      remision. Aqui se cierra el circulo completo:

        IVA leido del repositorio DE VERDAD
          -> Reglas.ConceptosDeLasPartidas
            -> comparado contra la cifra que calcula LA PROPIA BASE.

      El oraculo no son numeros escritos a mano aqui: es la columna
      `ImporteConIva` que devuelve `sp_n_VentasDetalleMultiple`, o sea lo que
      Mac31 usa. Si mi cuenta y la de la base no coinciden, el CFDI del web y
      el del escritorio son distintos para la misma remision.

      POR QUE ESTO CAZA EL IVA CIEN VECES MAYOR Y LA PRUEBA DE ARRIBA NO BASTA:
      en Tauro el precio del concepto es `ImportePartida / Cantidad * (1 + IVA)`.
      Medido en Produccion_svr, remision 390271: 2184.83 x 1.16 = 2534.40, que
      es exactamente `ImporteConIva`. Con IVA = 16 saldria 2184.83 x 17 =
      37142.11 y esto fallaria con las dos cifras a la vista.

      EN ZARAGOZA EL IVA NO ENTRA en el precio del concepto (se lee
      `PrecioListaConIvaRnd`, que ya viene con IVA y redondeado por la base), y
      tambien se comprueba: es la prueba de que el `/ 100.0` no se colo en la
      rama que no debia.

      NO TIMBRA NADA. Solo lee: el SP de detalle y una consulta de constantes.
      Timbrar desde local emite un CFDI real, y eso no se hace en una prueba.
    */
    [Theory]
    [InlineData("MacZ", 154134, true)]            // Zaragoza: precio ya con IVA
    [InlineData("Produccion_svr", 390271, false)] // Tauro: hay que ponerle el IVA
    public async Task Los_Conceptos_Cuadran_Con_Lo_Que_Calcula_La_Base(
        string baseDatos, int idVenta, bool esZaragoza)
    {
        if (!await HayBaseAsync(baseDatos)) return;

        var constantes = await RepositorioDe(baseDatos).LeerConstantesAsync(default);

        /* La variante sale de `funcionalidad`, igual que en el servicio: si
           alguna base cambiara de variante, que falle aqui y no en un CFDI. */
        var zaraEnLaBase = (constantes.Funcionalidad ?? string.Empty)
            .ToUpperInvariant().Contains("ZARA", StringComparison.Ordinal);
        Assert.Equal(esZaragoza, zaraEnLaBase);

        var partidas = await LeerPartidasAsync(baseDatos, idVenta);
        if (partidas.Count == 0)
        {
            /* La base local no llega a esa remision. No se da por buena en
               silencio: se dice, y la prueba no mide. */
            _out.WriteLine($"{baseDatos}: la remision {idVenta} no esta, no se pudo medir.");
            return;
        }

        var conceptos = ISL_Service.Infrastructure.Facturacion.ReglasPreparacionFactura
            .ConceptosDeLasPartidas(partidas, conIva: esZaragoza,
                porcentajeIva: constantes.PorcentajeIva);

        Assert.NotEmpty(conceptos);

        foreach (var concepto in conceptos)
        {
            var fila = partidas.First(p =>
                Convert.ToInt32(p["IDVentaDetalle"]) == concepto.IdVentaDetalle);

            /* El importe que calcula la base para esa partida, CON IVA. En
               Zaragoza se compara contra el neto por pieza redondeado, que es
               justo la columna que la regla lee. */
            var esperado = esZaragoza
                ? Convert.ToDecimal(fila["PrecioListaConIvaRnd"]) * concepto.Cantidad
                : Convert.ToDecimal(fila["ImporteConIva"]);

            var calculado = concepto.PrecioUnitarioConIva * concepto.Cantidad;

            /* Un centavo de tolerancia, y ni uno mas: la division por la
               cantidad deja decimales y la base redondea a dos. Mas holgura
               dejaria pasar un error de verdad. */
            Assert.True(Math.Abs(calculado - esperado) <= 0.01m,
                $"{baseDatos} remision {idVenta} partida {concepto.IdVentaDetalle}: "
                + $"la regla calcula {calculado} y la base dice {esperado} "
                + $"(IVA leido = {constantes.PorcentajeIva}).");
        }

        /* Y EL TOTAL DE LA REMISION. `TotalPagar` es la cifra que la pantalla
           ensena y la que el cliente paga: la suma de los importes redondeados
           de las partidas mas los cascos tiene que dar eso. Es la unica
           comprobacion que mira el conjunto y no renglon por renglon. */
        var totalPagar = Convert.ToDecimal(partidas[0]["TotalPagar"]);
        var sumaPartidas = partidas.Sum(p => Convert.ToDecimal(p["ImporteConIvaRnd"]));
        var sumaCascos = partidas.Sum(p =>
            Convert.ToDecimal(p["CostoUsadoCargoConIva"]) * Convert.ToDecimal(p["Cantidad"]));

        Assert.True(Math.Abs((sumaPartidas + sumaCascos) - totalPagar) <= 0.01m,
            $"{baseDatos} remision {idVenta}: partidas {sumaPartidas} + cascos {sumaCascos} "
            + $"= {sumaPartidas + sumaCascos}, y la base dice TotalPagar = {totalPagar}.");

        _out.WriteLine(
            $"{baseDatos} remision {idVenta}: {conceptos.Count} conceptos, "
            + $"iva={constantes.PorcentajeIva}, partidas={sumaPartidas}, cascos={sumaCascos}, "
            + $"totalPagar={totalPagar}");
    }

    /// <summary>
    /// Las partidas tal como las devuelve `sp_n_VentasDetalleMultiple`, que es
    /// el MISMO procedimiento que usa el servicio de preparacion. Leer de otro
    /// sitio probaria otra cosa.
    /// </summary>
    private static async Task<List<IReadOnlyDictionary<string, object?>>> LeerPartidasAsync(
        string baseDatos, int idVenta)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.sp_n_VentasDetalleMultiple", conn)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        cmd.Parameters.AddWithValue("@IDsVenta", idVenta.ToString());

        var filas = new List<IReadOnlyDictionary<string, object?>>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var fila = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < rd.FieldCount; i++)
                fila[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            filas.Add(fila);
        }

        return filas;
    }
}
