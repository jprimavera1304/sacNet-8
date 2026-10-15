using ISL_Service.Infrastructure.Facturacion;
using Reglas = ISL_Service.Infrastructure.Facturacion.ReglasPreparacionFactura;

namespace ISL_Service.Tests;

/*
  ═════════════════════════════════════════════════════════════════════════
  LA REGLA FISCAL, CLAVADA CONTRA 24 CFDI REALES YA TIMBRADOS
  ═════════════════════════════════════════════════════════════════════════

  QUE SE ESTA PROBANDO
  --------------------
  Que los conceptos que arma `preparar` suman EXACTAMENTE lo que el CFDI real
  cobro. No una regla inventada: el total de cada caso sale de
  `[Ventas Facturas].TotalFacturarDinero` de una factura TIMBRADA y no
  cancelada, y las partidas salen de `EXEC sp_n_VentasDetalleMultiple` para su
  remision. Doce de MacZ (Zaragoza, series Z / ZS) y doce de Produccion_svr
  (Tauro, serie T).

  POR QUE LOS DATOS VAN ESCRITOS AQUI DENTRO
  ------------------------------------------
  Porque la unica forma de probar facturacion en este proyecto es sin base y
  sin timbrar: timbrar desde local emite un CFDI de verdad. Asi que la medicion
  se hizo una vez contra las dos bases y quedo congelada en estas tablas. Si
  manana alguien cambia la regla, estas 24 cuentas se lo dicen sin tocar nada.

  LAS TRES CUENTAS QUE SE COMPARAN EN CADA CASO
  ---------------------------------------------
    REGLA        lo que arma `preparar` hoy
    WEB_HOY      precio neto x cantidad - MontoDescuento  (lo que manda el web)
    LISTA        FacturaPrecioListaConIva x cantidad      (la premisa de que
                 Legacy manda el precio de lista)
  La primera cuadra en las 24. Las otras dos no cuadran en ninguna.
*/
public class PrepararFacturaReglaFiscalTests
{
    private const decimal Iva = 0.16m;

    /// Un caso: la remision leida del SP y el total que cobro el CFDI real.
    public sealed record Caso(
        string Empresa,
        int IdVenta,
        string FolioDeLaFactura,
        decimal TotalFacturarDinero,
        List<Dictionary<string, object?>> Partidas)
    {
        public bool EsZaragoza => Empresa == "ZARA";
        public override string ToString() => $"{Empresa} {FolioDeLaFactura} (venta {IdVenta})";
    }

    private static Dictionary<string, object?> P(
        int idVentaDetalle, int idProducto, string codigo, string concepto,
        string claveSat, string unidadSat, decimal cantidad, int numPiezas,
        decimal precioLista, decimal precioListaConIvaRnd, decimal facturaPrecioListaConIva,
        decimal importeConIvaRnd, decimal facturaSubtotalConIva, decimal importePartida,
        decimal montoDescuento, decimal cascoConIva, int soloServicios) => new()
    {
        ["IDVentaDetalle"] = idVentaDetalle,
        ["IDProducto"] = idProducto,
        ["Codigo"] = codigo,
        ["Concepto"] = concepto,
        ["ClaveSAT"] = claveSat,
        ["UnidadMedidaSAT"] = unidadSat,
        ["Cantidad"] = cantidad,
        ["NumPiezas"] = numPiezas,
        ["PrecioLista"] = precioLista,
        ["PrecioListaConIvaRnd"] = precioListaConIvaRnd,
        ["FacturaPrecioListaConIva"] = facturaPrecioListaConIva,
        ["ImporteConIvaRnd"] = importeConIvaRnd,
        ["FacturaSubtotalConIva"] = facturaSubtotalConIva,
        ["ImportePartida"] = importePartida,
        ["MontoDescuento"] = montoDescuento,
        ["CostoUsadoCargoConIva"] = cascoConIva,
        ["SoloServicios"] = soloServicios
    };

    public static IEnumerable<object[]> Casos()
    {
        // ---- ZARA Z133083 ----
        yield return new object[] { new Caso("ZARA", 148815, "Z133083", 5221.00m, new List<Dictionary<string, object?>>
        {
            P(471868, 141, "FP-47(LN2)-600", "ACUMULADOR FULL-POWER FP-47(LN2)-600", "26111707", "H87", 1m, 1,
              4241.38m, 2804.00m, 4920.00m, 2804.00m, 4920.00m, 2417.59m, 1823.79m, 645.00m, 0),
            P(471869, 307, "FP-99-420", "ACUMULADOR FULL-POWER FP-99-420", "26111707", "H87", 1m, 1,
              3655.17m, 2417.00m, 4240.00m, 2417.00m, 4240.00m, 2083.45m, 1571.72m, 380.00m, 0),
        }) };

        // ---- ZARA Z133695 ----
        yield return new object[] { new Caso("ZARA", 149632, "Z133695", 38710.00m, new List<Dictionary<string, object?>>
        {
            P(474978, 2393, "AGM-L-49-900", "ACUMULADOR LTH AGM AGM-L-49-900", "26111707", "H87", 1m, 1,
              7155.17m, 4814.00m, 8300.00m, 4814.00m, 8300.00m, 4150.00m, 3005.17m, 760.00m, 0),
            P(474979, 2392, "AGM-L-48/91-760", "ACUMULADOR LTH AGM AGM-L-48/91-760", "26111707", "H87", 1m, 1,
              7155.17m, 4814.00m, 8300.00m, 4814.00m, 8300.00m, 4150.00m, 3005.17m, 645.00m, 0),
            P(474980, 307, "FP-99-420", "ACUMULADOR FULL-POWER FP-99-420", "26111707", "H87", 1m, 1,
              3655.17m, 2459.00m, 4240.00m, 2459.00m, 4240.00m, 2120.00m, 1535.17m, 380.00m, 0),
            P(474981, 128, "FP-42-550", "ACUMULADOR FULL-POWER FP-42-550", "26111707", "H87", 1m, 1,
              3741.38m, 2517.00m, 4340.00m, 2517.00m, 4340.00m, 2170.00m, 1571.38m, 495.00m, 0),
            P(474982, 32, "AM-75/86-650", "ACUMULADOR AMERICA AM-75/86-650", "26111707", "H87", 2m, 1,
              3112.07m, 2094.00m, 3610.00m, 4188.00m, 7220.00m, 3610.00m, 1307.07m, 645.00m, 0),
            P(474983, 14, "AM-42-500", "ACUMULADOR AMERICA AM-42-500", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 495.00m, 0),
            P(474984, 444, "AM-48/91(LN3)-615", "ACUMULADOR AMERICA AM-48/91(LN3)-615", "26111707", "H87", 2m, 1,
              3508.62m, 2361.00m, 4070.00m, 4722.00m, 8140.00m, 4070.00m, 1473.62m, 645.00m, 0),
            P(474985, 13, "AM-41-650", "ACUMULADOR AMERICA AM-41-650", "26111707", "H87", 1m, 1,
              3112.07m, 2094.00m, 3610.00m, 2094.00m, 3610.00m, 1805.00m, 1307.07m, 645.00m, 0),
            P(474986, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 1m, 1,
              3120.69m, 2100.00m, 3620.00m, 2100.00m, 3620.00m, 1810.00m, 1310.69m, 645.00m, 0),
            P(474987, 19, "AM-35-550", "ACUMULADOR AMERICA AM-35-550", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 645.00m, 0),
        }) };

        // ---- ZARA Z133696 ----
        yield return new object[] { new Caso("ZARA", 149633, "Z133696", 7609.00m, new List<Dictionary<string, object?>>
        {
            P(474988, 4, "AM-22F-450", "ACUMULADOR AMERICA AM-22F-450", "26111707", "H87", 1m, 1,
              2431.03m, 1636.00m, 2820.00m, 1636.00m, 2820.00m, 1410.00m, 1021.03m, 495.00m, 0),
            P(474989, 18, "AM-34-600", "ACUMULADOR AMERICA AM-34-600", "26111707", "H87", 2m, 1,
              3112.07m, 2094.00m, 3610.00m, 4188.00m, 7220.00m, 3610.00m, 1307.07m, 645.00m, 0),
        }) };

        // ---- ZARA Z133833 ----
        yield return new object[] { new Caso("ZARA", 150728, "Z133833", 8616.00m, new List<Dictionary<string, object?>>
        {
            P(476353, 129, "FP-65-850", "ACUMULADOR FULL-POWER FP-65-850", "26111707", "H87", 1m, 1,
              4103.45m, 2761.00m, 4760.00m, 2761.00m, 4760.00m, 2380.00m, 1723.45m, 760.00m, 0),
            P(476354, 23, "AM-34/78-750", "ACUMULADOR AMERICA AM-34/78-750", "26111707", "H87", 1m, 1,
              3258.62m, 2192.00m, 3780.00m, 2192.00m, 3780.00m, 1890.00m, 1368.62m, 645.00m, 0),
            P(476355, 15, "AM-58-575", "ACUMULADOR AMERICA AM-58-575", "26111707", "H87", 1m, 1,
              2620.69m, 1763.00m, 3040.00m, 1763.00m, 3040.00m, 1520.00m, 1100.69m, 495.00m, 0),
        }) };

        // ---- ZARA Z133834 ----
        yield return new object[] { new Caso("ZARA", 150729, "Z133834", 40698.00m, new List<Dictionary<string, object?>>
        {
            P(476356, 32, "AM-75/86-650", "ACUMULADOR AMERICA AM-75/86-650", "26111707", "H87", 1m, 1,
              3112.07m, 2094.00m, 3610.00m, 2094.00m, 3610.00m, 1805.00m, 1307.07m, 645.00m, 0),
            P(476357, 14, "AM-42-500", "ACUMULADOR AMERICA AM-42-500", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 495.00m, 0),
            P(476358, 27, "AM-99-390", "ACUMULADOR AMERICA AM-99-390", "26111707", "H87", 2m, 1,
              2689.66m, 1810.00m, 3120.00m, 3620.00m, 6240.00m, 3120.01m, 1129.66m, 380.00m, 0),
            P(476359, 444, "AM-48/91(LN3)-615", "ACUMULADOR AMERICA AM-48/91(LN3)-615", "26111707", "H87", 1m, 1,
              3508.62m, 2361.00m, 4070.00m, 2361.00m, 4070.00m, 2035.00m, 1473.62m, 645.00m, 0),
            P(476360, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 3m, 1,
              3120.69m, 2100.00m, 3620.00m, 6300.00m, 10860.00m, 5430.00m, 1310.69m, 645.00m, 0),
            P(476361, 19, "AM-35-550", "ACUMULADOR AMERICA AM-35-550", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 645.00m, 0),
            P(476362, 141, "FP-47(LN2)-600", "ACUMULADOR FULL-POWER FP-47(LN2)-600", "26111707", "H87", 1m, 1,
              4241.38m, 2854.00m, 4920.00m, 2854.00m, 4920.00m, 2460.00m, 1781.38m, 645.00m, 0),
            P(476363, 307, "FP-99-420", "ACUMULADOR FULL-POWER FP-99-420", "26111707", "H87", 1m, 1,
              3655.17m, 2459.00m, 4240.00m, 2459.00m, 4240.00m, 2120.00m, 1535.17m, 380.00m, 0),
            P(476364, 5235, "AGM-L-94R-850", "ACUMULADOR LTH AGM AGM-L-94R-850", "26111707", "H87", 2m, 1,
              7155.17m, 4814.00m, 8300.00m, 9628.00m, 16600.00m, 8300.00m, 3005.17m, 760.00m, 0),
        }) };

        // ---- ZARA Z133905 ----
        yield return new object[] { new Caso("ZARA", 150858, "Z133905", 16442.00m, new List<Dictionary<string, object?>>
        {
            P(476786, 139, "FP-49(LN5)-850", "ACUMULADOR FULL-POWER FP-49(LN5)-850", "26111707", "H87", 1m, 1,
              4767.24m, 3207.00m, 5530.00m, 3207.00m, 5530.00m, 2765.00m, 2002.24m, 760.00m, 0),
            P(476787, 125, "FP-34-650", "ACUMULADOR FULL-POWER FP-34-650", "26111707", "H87", 1m, 1,
              4224.14m, 2842.00m, 4900.00m, 2842.00m, 4900.00m, 2450.00m, 1774.14m, 645.00m, 0),
            P(476788, 201, "L-51R-500", "ACUMULADOR LTH L-51R-500", "26111707", "H87", 2m, 1,
              3534.48m, 2378.00m, 4100.00m, 4756.00m, 8200.00m, 4100.00m, 1484.48m, 495.00m, 0),
            P(476789, 3189, "AM-94R-800", "ACUMULADOR AMERICA AM-94R-800", "26111707", "H87", 1m, 1,
              3689.66m, 2482.00m, 4280.00m, 2482.00m, 4280.00m, 2140.00m, 1549.66m, 760.00m, 0),
        }) };

        // ---- ZARA Z133923 ----
        yield return new object[] { new Caso("ZARA", 150876, "Z133923", 32599.00m, new List<Dictionary<string, object?>>
        {
            P(476855, 5235, "AGM-L-94R-850", "ACUMULADOR LTH AGM AGM-L-94R-850", "26111707", "H87", 1m, 1,
              7155.17m, 4814.00m, 8300.00m, 4814.00m, 8300.00m, 4150.00m, 3005.17m, 760.00m, 0),
            P(476856, 141, "FP-47(LN2)-600", "ACUMULADOR FULL-POWER FP-47(LN2)-600", "26111707", "H87", 1m, 1,
              4241.38m, 2854.00m, 4920.00m, 2854.00m, 4920.00m, 2460.00m, 1781.38m, 645.00m, 0),
            P(476857, 138, "FP-48/91(LN3)-730", "ACUMULADOR FULL-POWER FP-48/91(LN3)-730", "26111707", "H87", 1m, 1,
              4767.24m, 3207.00m, 5530.00m, 3207.00m, 5530.00m, 2765.00m, 2002.24m, 645.00m, 0),
            P(476858, 126, "FP-41-750", "ACUMULADOR FULL-POWER FP-41-750", "26111707", "H87", 1m, 1,
              4224.14m, 2842.00m, 4900.00m, 2842.00m, 4900.00m, 2450.00m, 1774.14m, 645.00m, 0),
            P(476859, 307, "FP-99-420", "ACUMULADOR FULL-POWER FP-99-420", "26111707", "H87", 1m, 1,
              3655.17m, 2459.00m, 4240.00m, 2459.00m, 4240.00m, 2120.00m, 1535.17m, 380.00m, 0),
            P(476860, 14, "AM-42-500", "ACUMULADOR AMERICA AM-42-500", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 495.00m, 0),
            P(476861, 444, "AM-48/91(LN3)-615", "ACUMULADOR AMERICA AM-48/91(LN3)-615", "26111707", "H87", 1m, 1,
              3508.62m, 2361.00m, 4070.00m, 2361.00m, 4070.00m, 2035.00m, 1473.62m, 645.00m, 0),
            P(476862, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 2m, 1,
              3120.69m, 2100.00m, 3620.00m, 4200.00m, 7240.00m, 3620.00m, 1310.69m, 645.00m, 0),
            P(476863, 19, "AM-35-550", "ACUMULADOR AMERICA AM-35-550", "26111707", "H87", 1m, 1,
              2758.62m, 1856.00m, 3200.00m, 1856.00m, 3200.00m, 1600.00m, 1158.62m, 645.00m, 0),
        }) };

        // ---- ZARA Z134077 ----
        yield return new object[] { new Caso("ZARA", 151051, "Z134077", 38449.00m, new List<Dictionary<string, object?>>
        {
            P(477617, 5235, "AGM-L-94R-850", "ACUMULADOR LTH AGM AGM-L-94R-850", "26111707", "H87", 1m, 1,
              7155.17m, 4814.00m, 8300.00m, 4814.00m, 8300.00m, 4150.00m, 3005.17m, 760.00m, 0),
            P(477618, 2392, "AGM-L-48/91-760", "ACUMULADOR LTH AGM AGM-L-48/91-760", "26111707", "H87", 2m, 1,
              7155.17m, 4814.00m, 8300.00m, 9628.00m, 16600.00m, 8300.00m, 3005.17m, 645.00m, 0),
            P(477619, 135, "FP-35-575", "ACUMULADOR FULL-POWER FP-35-575", "26111707", "H87", 2m, 1,
              3750.00m, 2523.00m, 4350.00m, 5046.00m, 8700.00m, 4350.00m, 1575.00m, 645.00m, 0),
            P(477620, 27, "AM-99-390", "ACUMULADOR AMERICA AM-99-390", "26111707", "H87", 2m, 1,
              2689.66m, 1810.00m, 3120.00m, 3620.00m, 6240.00m, 3120.01m, 1129.66m, 380.00m, 0),
            P(477621, 444, "AM-48/91(LN3)-615", "ACUMULADOR AMERICA AM-48/91(LN3)-615", "26111707", "H87", 1m, 1,
              3508.62m, 2361.00m, 4070.00m, 2361.00m, 4070.00m, 2035.00m, 1473.62m, 645.00m, 0),
            P(477622, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 3m, 1,
              3120.69m, 2100.00m, 3620.00m, 6300.00m, 10860.00m, 5430.00m, 1310.69m, 645.00m, 0),
        }) };

        // ---- ZARA Z134078 ----
        yield return new object[] { new Caso("ZARA", 151052, "Z134078", 9886.00m, new List<Dictionary<string, object?>>
        {
            P(477623, 139, "FP-49(LN5)-850", "ACUMULADOR FULL-POWER FP-49(LN5)-850", "26111707", "H87", 1m, 1,
              4767.24m, 3207.00m, 5530.00m, 3207.00m, 5530.00m, 2765.00m, 2002.24m, 760.00m, 0),
            P(477624, 125, "FP-34-650", "ACUMULADOR FULL-POWER FP-34-650", "26111707", "H87", 1m, 1,
              4224.14m, 2842.00m, 4900.00m, 2842.00m, 4900.00m, 2450.00m, 1774.14m, 645.00m, 0),
            P(477625, 31, "AM-51R-500", "ACUMULADOR AMERICA AM-51R-500", "26111707", "H87", 1m, 1,
              2879.31m, 1937.00m, 3340.00m, 1937.00m, 3340.00m, 1670.00m, 1209.31m, 495.00m, 0),
        }) };

        // ---- ZARA ZS1139 ----
        yield return new object[] { new Caso("ZARA", 153242, "ZS1139", 4814.00m, new List<Dictionary<string, object?>>
        {
            P(483095, 11807, "SERVICIO AGM-L-48/91-760", "SERVICIO A DOMICILIO  ACUMULADOR LTH AGM AGM-L-48/91-760", "26111707", "H87", 1m, 1,
              4150.00m, 4814.00m, 4814.00m, 4814.00m, 4814.00m, 4150.00m, 0.00m, 0.00m, 1),
        }) };

        // ---- ZARA Z135016 ----
        yield return new object[] { new Caso("ZARA", 153250, "Z135016", 1896.00m, new List<Dictionary<string, object?>>
        {
            P(483116, 27, "AM-99-390", "ACUMULADOR AMERICA AM-99-390", "26111707", "H87", 1m, 1,
              2689.66m, 1896.00m, 3120.00m, 1896.00m, 3120.00m, 1634.24m, 1055.42m, 380.00m, 0),
        }) };

        // ---- ZARA Z135018 ----
        yield return new object[] { new Caso("ZARA", 153252, "Z135018", 2094.00m, new List<Dictionary<string, object?>>
        {
            P(483119, 18, "AM-34-600", "ACUMULADOR AMERICA AM-34-600", "26111707", "H87", 1m, 1,
              3112.07m, 2094.00m, 3610.00m, 2094.00m, 3610.00m, 1805.00m, 1307.07m, 645.00m, 0),
        }) };

        // ---- TAU T140728 ----
        yield return new object[] { new Caso("TAU", 384067, "T140728", 3864.00m, new List<Dictionary<string, object?>>
        {
            P(1095240, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 1m, 1,
              3120.69m, 4300.00m, 3620.00m, 2172.00m, 3864.00m, 1872.41m, 1248.28m, 680.00m, 0),
            P(1095242, 4, "AM-22F-450", "ACUMULADOR AMERICA AM-22F-450", "26111707", "H87", 1m, 1,
              2431.03m, 3340.00m, 2820.00m, 1692.00m, 3864.00m, 1458.62m, 972.41m, 520.00m, 0),
        }) };

        // ---- TAU T140770 ----
        yield return new object[] { new Caso("TAU", 384111, "T140770", 6006.00m, new List<Dictionary<string, object?>>
        {
            P(1095371, 2, "AM-42-400", "ACUMULADOR AMERICA AM-42-400", "26111707", "H87", 1m, 1,
              2500.00m, 3420.00m, 2900.00m, 1740.00m, 6006.00m, 1500.00m, 1000.00m, 520.00m, 0),
            P(1095372, 20, "AM-65-800", "ACUMULADOR AMERICA AM-65-800", "26111707", "H87", 1m, 1,
              3017.24m, 4300.00m, 3500.00m, 2100.00m, 6006.00m, 1810.34m, 1206.90m, 800.00m, 0),
            P(1095373, 32, "AM-75/86-650", "ACUMULADOR AMERICA AM-75/86-650", "26111707", "H87", 1m, 1,
              3112.07m, 4290.00m, 3610.00m, 2166.00m, 6006.00m, 1867.24m, 1244.83m, 680.00m, 0),
        }) };

        // ---- TAU T140860 ----
        yield return new object[] { new Caso("TAU", 384206, "T140860", 6012.00m, new List<Dictionary<string, object?>>
        {
            P(1095677, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 1m, 1,
              3120.69m, 4300.00m, 3620.00m, 2172.00m, 6012.00m, 1872.41m, 1248.28m, 680.00m, 0),
            P(1095678, 2, "AM-42-400", "ACUMULADOR AMERICA AM-42-400", "26111707", "H87", 1m, 1,
              2500.00m, 3420.00m, 2900.00m, 1740.00m, 6012.00m, 1500.00m, 1000.00m, 520.00m, 0),
            P(1095679, 20, "AM-65-800", "ACUMULADOR AMERICA AM-65-800", "26111707", "H87", 1m, 1,
              3017.24m, 4300.00m, 3500.00m, 2100.00m, 6012.00m, 1810.34m, 1206.90m, 800.00m, 0),
        }) };

        // ---- TAU T140922 ----
        yield return new object[] { new Caso("TAU", 384268, "T140922", 5437.00m, new List<Dictionary<string, object?>>
        {
            P(1095872, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 1m, 1,
              3120.69m, 4300.00m, 3620.00m, 2172.00m, 5437.00m, 1872.41m, 1248.28m, 680.00m, 0),
            P(1095873, 4, "AM-22F-450", "ACUMULADOR AMERICA AM-22F-450", "26111707", "H87", 1m, 1,
              2431.03m, 3340.00m, 2820.00m, 1692.00m, 5437.00m, 1458.62m, 972.41m, 520.00m, 0),
            P(1095874, 177, "MOTO LTH-TX4L-BS", "MOTO-BATERIA MOTO LTH MOTO LTH-TX4L-BS AGM", "26111707", "H87", 1m, 1,
              508.62m, 590.00m, 590.00m, 383.00m, 5437.00m, 330.60m, 178.02m, 0.00m, 0),
            P(1095875, 13932, "MOTO LTH-TX7A-FA", "MOTO-BATERIA MOTO LTH MOTO LTH-TX7A-FA", "26111707", "H87", 1m, 1,
              724.14m, 840.00m, 840.00m, 546.00m, 5437.00m, 470.69m, 253.45m, 0.00m, 0),
            P(1095876, 208, "MOTO LTH-TX9-BS", "MOTO-BATERIA MOTO LTH MOTO LTH-TX9-BS AGM", "26111707", "H87", 1m, 1,
              853.45m, 990.00m, 990.00m, 644.00m, 5437.00m, 554.74m, 298.71m, 0.00m, 0),
        }) };

        // ---- TAU T141035 ----
        yield return new object[] { new Caso("TAU", 385273, "T141035", 3018.00m, new List<Dictionary<string, object?>>
        {
            P(1096872, 443, "L-48/91(LN3)-615", "ACUMULADOR LTH L-48/91(LN3)-615", "26111707", "H87", 1m, 1,
              4336.21m, 5710.00m, 5030.00m, 3018.00m, 3018.00m, 2601.73m, 1734.48m, 680.00m, 0),
        }) };

        // ---- TAU T141446 ----
        yield return new object[] { new Caso("TAU", 387499, "T141446", 6096.00m, new List<Dictionary<string, object?>>
        {
            P(1099611, 19, "AM-35-550", "ACUMULADOR AMERICA AM-35-550", "26111707", "H87", 1m, 1,
              2758.62m, 3880.00m, 3200.00m, 1920.00m, 6096.00m, 1655.17m, 1103.45m, 680.00m, 0),
            P(1099612, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 1m, 1,
              3120.69m, 4300.00m, 3620.00m, 2172.00m, 6096.00m, 1872.41m, 1248.28m, 680.00m, 0),
            P(1099613, 31, "AM-51R-500", "ACUMULADOR AMERICA AM-51R-500", "26111707", "H87", 1m, 1,
              2879.31m, 3860.00m, 3340.00m, 2004.00m, 6096.00m, 1727.59m, 1151.72m, 520.00m, 0),
        }) };

        // ---- TAU T142019 ----
        yield return new object[] { new Caso("TAU", 388965, "T142019", 11387.00m, new List<Dictionary<string, object?>>
        {
            P(1102111, 203, "L-75/86-650", "ACUMULADOR LTH L-75/86-650", "26111707", "H87", 1m, 1,
              3844.83m, 5140.00m, 4460.00m, 2631.00m, 11387.00m, 2268.45m, 1576.38m, 680.00m, 0),
            P(1102112, 13868, "L-51RJIS-480", "ACUMULADOR LTH L-51RJIS-480", "26111707", "H87", 1m, 1,
              3534.48m, 4620.00m, 4100.00m, 2419.00m, 11387.00m, 2085.34m, 1449.14m, 520.00m, 0),
            P(1102113, 20, "AM-65-800", "ACUMULADOR AMERICA AM-65-800", "26111707", "H87", 1m, 1,
              3017.24m, 4300.00m, 3500.00m, 2065.00m, 11387.00m, 1780.17m, 1237.07m, 800.00m, 0),
            P(1102114, 24, "AM-47(LN2)-550", "ACUMULADOR AMERICA AM-47(LN2)-550", "26111707", "H87", 2m, 1,
              3120.69m, 4300.00m, 3620.00m, 4272.00m, 11387.00m, 3682.41m, 1279.48m, 680.00m, 0),
        }) };

        // ---- TAU T142074 ----
        yield return new object[] { new Caso("TAU", 389022, "T142074", 8266.00m, new List<Dictionary<string, object?>>
        {
            P(1102266, 7274, "MOTO AM-TX14-BS AGM", "MOTO-BATERIA MOTO AMERICA MOTO AM-TX14-BS AGM", "26111707", "H87", 2m, 1,
              887.93m, 1030.00m, 1030.00m, 1339.00m, 8266.00m, 1154.31m, 310.78m, 0.00m, 0),
            P(1102267, 234, "MOTO LTH-TX14-BS", "MOTO-BATERIA MOTO LTH MOTO LTH-TX14-BS AGM", "26111707", "H87", 2m, 1,
              1129.31m, 1310.00m, 1310.00m, 1703.00m, 8266.00m, 1468.10m, 395.26m, 0.00m, 0),
            P(1102268, 7273, "MOTO AM-TX12-BS AGM", "MOTO-BATERIA MOTO AMERICA MOTO AM-TX12-BS AGM", "26111707", "H87", 1m, 1,
              862.07m, 1000.00m, 1000.00m, 650.00m, 8266.00m, 560.35m, 301.72m, 0.00m, 0),
            P(1102269, 319, "MOTO LTH-TX12-BS", "MOTO-BATERIA MOTO LTH MOTO LTH-TX12-BS AGM", "26111707", "H87", 1m, 1,
              1077.59m, 1250.00m, 1250.00m, 813.00m, 8266.00m, 700.43m, 377.16m, 0.00m, 0),
            P(1102270, 1, "AM-24-420", "ACUMULADOR AMERICA AM-24-420", "26111707", "H87", 1m, 1,
              2715.52m, 3830.00m, 3150.00m, 1890.00m, 8266.00m, 1629.31m, 1086.21m, 680.00m, 0),
            P(1102271, 27, "AM-99-390", "ACUMULADOR AMERICA AM-99-390", "26111707", "H87", 1m, 1,
              2689.66m, 3520.00m, 3120.00m, 1872.00m, 8266.00m, 1613.80m, 1075.86m, 400.00m, 0),
        }) };

        // ---- TAU T142194 ----
        yield return new object[] { new Caso("TAU", 390056, "T142194", 2401.00m, new List<Dictionary<string, object?>>
        {
            P(1103415, 28, "AM-31P-900 AR**", "ACUMULADOR AMERICA AM-31P-900AR**", "26111707", "H87", 1m, 1,
              3508.62m, 5070.00m, 4070.00m, 2401.00m, 2401.00m, 2070.09m, 1438.53m, 1000.00m, 0),
        }) };

        // ---- TAU T142323 ----
        yield return new object[] { new Caso("TAU", 390185, "T142323", 24567.00m, new List<Dictionary<string, object?>>
        {
            P(1103851, 181, "L-31T-750 AR**", "ACUMULADOR LTH L-31T-750 AR**", "26111707", "H87", 10m, 1,
              3715.52m, 5310.00m, 4310.00m, 24567.00m, 24567.00m, 21178.46m, 1597.67m, 1000.00m, 0),
        }) };

        // ---- TAU T142348 ----
        yield return new object[] { new Caso("TAU", 390211, "T142348", 5700.00m, new List<Dictionary<string, object?>>
        {
            P(1103913, 168, "L-31T-900 AR**", "ACUMULADOR LTH L-31T-900 AR**", "26111707", "H87", 2m, 1,
              4094.83m, 5750.00m, 4750.00m, 5700.00m, 5700.00m, 4913.80m, 1637.93m, 1000.00m, 0),
        }) };

        // ---- TAU T142350 ----
        yield return new object[] { new Caso("TAU", 390213, "T142350", 2401.00m, new List<Dictionary<string, object?>>
        {
            P(1103917, 21, "AM-31T-900 AR**", "ACUMULADOR AMERICA AM-31T-900 AR**", "26111707", "H87", 1m, 1,
              3508.62m, 5070.00m, 4070.00m, 2401.00m, 2401.00m, 2070.09m, 1438.53m, 1000.00m, 0),
        }) };

    }

    private static List<Reglas.ConceptoPreparado> Preparar(Caso caso)
    {
        var partidas = caso.Partidas.Select(p => (IReadOnlyDictionary<string, object?>)p).ToList();

        var conceptos = Reglas.ConceptosDeLasPartidas(partidas, caso.EsZaragoza, porcentajeIva: Iva);

        Reglas.AjustarConceptosAlTotalAFacturar(
            conceptos,
            caso.EsZaragoza,
            caso.TotalFacturarDinero,
            Reglas.TotalEnDineroDeLasRemisiones(partidas, caso.EsZaragoza),
            Iva,
            partidas.Sum(p => Reglas.Numero(Reglas.Crudo(p, "Cantidad"))));

        return conceptos;
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_comprobante_que_arma_preparar_suma_lo_que_cobro_el_CFDI_real(Caso caso)
    {
        /*
          EL CRITERIO DE VERDAD: el CFDI tiene que cuadrar con lo que el cliente
          debe. Se permite un centavo de tolerancia porque Legacy manda el precio
          SIN IVA y deja que el PAC lo multiplique, y aqui se manda CON IVA; la
          division y la multiplicacion no viajan por el mismo redondeo.
        */
        var total = Preparar(caso).Sum(c => c.PrecioUnitarioConIva * c.Cantidad);

        Assert.True(Math.Abs(total - caso.TotalFacturarDinero) <= 0.01m,
            $"{caso}: la regla da {total:N2} y el CFDI real cobro {caso.TotalFacturarDinero:N2}.");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Ningun_concepto_lleva_descuento(Caso caso)
    {
        // En todo MacServicios2 no existe el campo `discount` del concepto
        // (FacturapiService.cs:860-872). Si alguien lo vuelve a poner, esto cae.
        Assert.All(Preparar(caso), c => Assert.Equal(0m, c.Descuento));
    }

    private static decimal CuentaDelWebDeHoy(Caso caso) => caso.Partidas.Sum(p =>
    {
        // Lo que el web manda hoy: el precio que lee (neto en Zaragoza, de
        // lista en Tauro) MENOS `MontoDescuento` en el mismo concepto.
        var precio = caso.EsZaragoza
            ? Reglas.Numero(p["PrecioListaConIvaRnd"])
            : Reglas.Numero(p["PrecioLista"]);
        return precio * Reglas.Numero(p["Cantidad"]) - Reglas.Numero(p["MontoDescuento"]);
    });

    private static decimal CuentaDelPrecioDeLista(Caso caso) => caso.Partidas.Sum(p =>
        Reglas.Numero(p["FacturaPrecioListaConIva"]) * Reglas.Numero(p["Cantidad"]));

    [Fact]
    public void La_cuenta_del_web_de_hoy_solo_coincide_cuando_no_hay_descuento()
    {
        /*
          Esta prueba no esta de adorno: fija por escrito que la diferencia
          existe y de cuanto es, para que nadie "arregle" el backend volviendo a
          la cuenta del navegador.

          La UNICA que coincide es ZS1139, y coincide por una razon concreta: es
          una remision de SOLO SERVICIO, con `MontoDescuento = 0`. Sin descuento
          que restar dos veces, las dos cuentas dan lo mismo. En cuanto hay
          descuento —o una caja— se separan.
        */
        var coinciden = new List<string>();
        var diferencias = new List<string>();

        foreach (var fila in Casos())
        {
            var caso = (Caso)fila[0];
            var web = CuentaDelWebDeHoy(caso);

            if (Math.Abs(web - caso.TotalFacturarDinero) <= 0.01m)
            {
                coinciden.Add($"{caso}");

                // Y cuando coincide es porque no habia descuento que repetir.
                Assert.Equal(0m, caso.Partidas.Sum(x => Reglas.Numero(x["MontoDescuento"])));
            }
            else
            {
                diferencias.Add($"{caso}: web {web:N2} vs CFDI {caso.TotalFacturarDinero:N2}");
            }
        }

        // 23 de 24 no cuadran. Si este numero baja, alguien cambio la cuenta.
        Assert.Equal(23, diferencias.Count);
        Assert.Equal(new[] { "ZARA ZS1139 (venta 153242)" }, coinciden);
    }

    [Fact]
    public void El_precio_de_LISTA_tampoco_es_lo_que_viaja_al_CFDI()
    {
        /*
          La premisa que se estaba investigando era que Legacy manda
          `FacturaPrecioListaConIva`. Si fuera asi, esta suma seria la del CFDI.
          No lo es en 23 de los 24 casos, y en los de caja se va por millones: la
          venta 30379 de MacZ daria 3,717,300.00 contra 18,021.76 de la remision,
          porque esa columna ya lleva `* NumPiezas` y la cantidad tambien.

          La excepcion es la misma ZS1139: alli `redondea(PrecioLista x 1.16)`
          da justo el neto porque no hubo descuento ni casco.
        */
        var coinciden = new List<string>();

        foreach (var fila in Casos())
        {
            var caso = (Caso)fila[0];
            if (Math.Abs(CuentaDelPrecioDeLista(caso) - caso.TotalFacturarDinero) <= 0.01m)
                coinciden.Add($"{caso}");
        }

        Assert.Equal(new[] { "ZARA ZS1139 (venta 153242)" }, coinciden);
    }

    [Fact]
    public void El_descuento_de_la_partida_medida_se_cobraba_de_menos()
    {
        /*
          EL CASO CONCRETO QUE ABRIO ESTO. MacZ, venta 154134, partida 483747:

              PrecioListaConIvaRnd      2,388.68   <- el neto, lo que se timbra
              FacturaPrecioListaConIva  4,480.00   <- el precio de lista
              MontoDescuento            1,747.86   <- YA restado en el neto
              ImporteConIvaRnd          2,388.68   <- lo que el cliente debe

          El web mandaba 2,388.68 de precio Y 1,747.86 de descuento: 640.82.
        */
        var partida = new Dictionary<string, object?>
        {
            ["IDVentaDetalle"] = 483747,
            ["Cantidad"] = 1m,
            ["NumPiezas"] = 1,
            ["PrecioLista"] = 3862.07m,
            ["PrecioListaConIvaRnd"] = 2388.68m,
            ["FacturaPrecioListaConIva"] = 4480.00m,
            ["ImporteConIvaRnd"] = 2388.68m,
            ["FacturaSubtotalConIva"] = 4480.00m,
            ["ImportePartida"] = 2114.21m,
            ["MontoDescuento"] = 1747.86m,
            ["CostoUsadoCargoConIva"] = 645.00m,
            ["SoloServicios"] = 0
        };

        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)partida },
            conIva: true, porcentajeIva: Iva));

        Assert.Equal(2388.68m, c.PrecioUnitarioConIva);
        Assert.Equal(0m, c.Descuento);
        Assert.Equal(2388.68m, c.PrecioUnitarioConIva * c.Cantidad - c.Descuento);

        // Lo que daba el web, para que la cifra quede escrita.
        Assert.Equal(640.82m, 2388.68m * 1m - 1747.86m);
    }

    [Fact]
    public void Una_caja_entra_por_PIEZAS_al_precio_por_pieza()
    {
        /*
          MacZ, venta 30379, partida 104648: CantidadFtm = 2 cajas,
          NumPiezas = 360, Cantidad = 720 piezas. Legacy manda
          `quantity = CantidadFtm * NumPiezas` (FacturapiService.cs:798), que es
          exactamente la columna `Cantidad`; y el precio es por PIEZA.

          Si alguien usara `CantidadFtm` con este precio, la factura saldria 360
          veces mas chica; si usara `FacturaPrecioListaConIva` (que ya lleva el
          `* NumPiezas`) con `Cantidad`, saldria 360 veces mas grande.
        */
        var partida = new Dictionary<string, object?>
        {
            ["IDVentaDetalle"] = 104648,
            ["Cantidad"] = 720m,
            ["CantidadFtm"] = 2m,
            ["NumPiezas"] = 360,
            ["PrecioLista"] = 12.45m,
            ["PrecioListaConIvaRnd"] = 14.44m,
            ["FacturaPrecioListaConIva"] = 5040.00m,
            ["ImporteConIvaRnd"] = 10398.24m,
            ["ImportePartida"] = 8964.00m,
            ["MontoDescuento"] = 0m,
            ["CostoUsadoCargoConIva"] = 0m,
            ["SoloServicios"] = 0
        };

        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)partida },
            conIva: true, porcentajeIva: Iva));

        Assert.Equal(720m, c.Cantidad);
        Assert.Equal(14.44m, c.PrecioUnitarioConIva);
        Assert.Equal(10396.80m, c.PrecioUnitarioConIva * c.Cantidad);  // 10,398.24 con el redondeo del SP
    }
}
