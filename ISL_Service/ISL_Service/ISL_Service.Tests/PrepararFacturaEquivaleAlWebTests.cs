using ISL_Service.Infrastructure.Facturacion;

using Reglas = ISL_Service.Infrastructure.Facturacion.ReglasPreparacionFactura;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  `preparar` TIENE QUE DECIR LO MISMO QUE DICE HOY EL NAVEGADOR
  ══════════════════════════════════════════════════════════════════════════

  QUE SE MIDE AQUI Y POR QUE ESTA PRUEBA ES EL CONTRATO
  -----------------------------------------------------
  `POST /api/ventas/facturas` recibe el CFDI ya armado, asi que la regla que
  decide QUE dice el comprobante vivia en el navegador. Moverla al servidor solo
  sirve si el servidor dice EXACTAMENTE lo mismo: si una cifra cambia, el movil
  y el web emiten comprobantes distintos para la misma remision y nadie lo
  descubre hasta comparar dos PDF del SAT.

  Por eso los casos de abajo NO son inventados: son los numeros del web y de
  Mac31, con la cita al lado. Donde el web y Mac31 DIFIEREN hay una prueba que
  deja la diferencia escrita con las dos cifras, en vez de "arreglarla" en
  silencio.

  POR QUE AQUI NO HAY NI UNA LLAMADA A FACTURAPI NI A LA BASE
  -----------------------------------------------------------
  Timbrar emite un CFDI real y el candado antiproduccion COINCIDE en las dos
  bases locales, asi que desde esta maquina se timbra de verdad. No hay sandbox.
  Todo lo que se prueba aqui son funciones puras sobre renglones que ya se
  leyeron — que es exactamente la razon de que las reglas esten en
  `ReglasPreparacionFactura` y no mezcladas con los repositorios.
*/
public class PrepararFacturaEquivaleAlWebTests
{
    // ======================================================================
    // DATOS MEDIDOS
    // ======================================================================

    /*
      LA PARTIDA REAL DE MACZ, columna por columna.

      Medida con `EXEC sp_n_VentasDetalleMultiple @IDsVenta='154134'` el
      2026-10-14: remision 154134, partida 483747. Los nombres son los que
      devuelve el procedimiento, con sus mayusculas (`UnidadMedidaSAT` con las
      tres al final, `ClaveSAT` igual). Esto es el oraculo de los nombres: no se
      adivinaron.
    */
    private static Dictionary<string, object?> PartidaDeMacZ(
        int idVentaDetalle = 483747,
        decimal cantidad = 1m) => new()
    {
        ["IDVenta"] = 154134,
        ["IDVentaDetalle"] = idVentaDetalle,
        ["IDProducto"] = 198,
        ["Codigo"] = "L-47(LN2)-550",
        ["Concepto"] = "ACUMULADOR LTH L-47(LN2)-550",
        ["ConceptoFtm"] = "ACUMULADOR LTH L-47(LN2)-550",
        ["ClaveSAT"] = "26111707",
        ["UnidadMedidaSAT"] = "H87",
        ["Cantidad"] = cantidad,

        /*
          LOS NUMEROS SON LOS DEL RENGLON REAL, copiados de
          `EXEC sp_n_VentasDetalleMultiple @IDsVenta='154134'` en MacZ, partida
          483747. Estaban mal puestos antes —`PrecioLista` tenia el valor de
          `ImportePartida` e `ImportePartida` el del descuento— y con la columna
          corrida no se puede medir ninguna regla de precio.

              PrecioLista               3,862.07   lista SIN IVA
              ImportePartida            2,114.21   neto SIN IVA (3,862.07 - 1,747.86)
              MontoDescuento            1,747.86   descuento SIN IVA, YA restado
              PrecioListaConIvaRnd      2,388.68   neto por pieza CON IVA
              FacturaPrecioListaConIva  4,480.00   lista CON IVA = redondea(3,862.07 x 1.16)
              CostoUsadoCargoConIva       645.00   el casco
              TotalPagar                3,033.68   = 2,388.68 + 645.00
        */
        ["PrecioLista"] = 3862.07m,
        ["PrecioListaConIvaRnd"] = 2388.68m,
        ["FacturaPrecioListaConIva"] = 4480.00m,
        ["ImporteConIvaRnd"] = 2388.68m,
        ["FacturaSubtotalConIva"] = 4480.00m,
        ["SubTotalConUsadoConIva"] = 3033.68m,
        ["ImportePartida"] = 2114.21m,
        ["MontoDescuento"] = 1747.86m,
        ["CostoUsadoCargoConIva"] = 645.00m,
        ["TotalPagar"] = 3033.68m,
        ["NumPiezas"] = 1,
        ["CantidadFtm"] = cantidad,
        ["SoloServicios"] = 0
    };

    // ======================================================================
    // LAS CUATRO RAMAS DEL MONTO QUE PROPONE MAC31
    // ======================================================================

    /*
      mac: VentasFacturacion.cs:257-279 — leido del fuente, rama por rama.
      web: nucleo/facturarRemision.ts:199-206.

      La segunda rama es la que no se adivina y la que importa: cuando el DINERO
      ya esta facturado, lo que se propone facturar son SOLO LOS CASCOS. Sin ella
      la pantalla abre ofreciendo volver a facturar dinero ya facturado, y como
      el monto por remision SIEMPRE es editable nadie lo nota hasta tener dos
      CFDI por el mismo dinero.
    */
    [Theory]
    // (ImporteZ + Cascos) == TotalFacturado  ->  0: ya se facturo todo
    [InlineData(1000, 200, 1200, 0)]
    // ImporteZ == TotalFacturado  ->  Cascos: el dinero ya, los cascos no
    [InlineData(1000, 200, 1000, 200)]
    // ImporteZ > TotalFacturado  ->  ImporteZ - TotalFacturado
    [InlineData(1000, 200, 400, 600)]
    // en otro caso (ImporteZ < TotalFacturado)  ->  (ImporteZ + Cascos) - TotalFacturado
    [InlineData(1000, 200, 1100, 100)]
    /*
      REMISION NUEVA, NADA FACTURADO -> SOLO EL DINERO, NO LOS CASCOS.

      Sorprende, asi que se midio contra el fuente: con TotalFacturado = 0 la
      condicion `ImporteZ > TotalFacturado` es cierta, asi que cae en la TERCERA
      rama y propone 1000, no 1200. El `else` que suma los cascos solo se alcanza
      cuando `ImporteZ < TotalFacturado`.

      Consecuencia practica: los cascos de la remision no entran en la propuesta
      inicial y se facturan en una segunda vuelta — que es cuando entra la
      segunda rama. Si esta prueba esperara 1200 (como se escribio al principio),
      el movil propondria facturar cascos que Mac31 no propone.
    */
    [InlineData(1000, 200, 0, 1000)]
    public void El_monto_que_propone_Mac31_son_sus_cuatro_ramas_en_su_orden(
        decimal importeZ, decimal cascos, decimal facturado, decimal esperado)
    {
        Assert.Equal(esperado, Reglas.MontoQueProponeMac31(importeZ, cascos, facturado));
    }

    [Fact]
    public void Con_el_dinero_ya_facturado_solo_se_proponen_los_cascos()
    {
        // La segunda rama, aparte y con nombre, porque es la que se pierde al
        // migrar: `PorFacturar` daria lo mismo en este caso, pero no en el de
        // abajo, y confundirlas es el bug.
        Assert.Equal(200m, Reglas.MontoQueProponeMac31(1000m, 200m, 1000m));

        // Y aqui es donde se separan: lo que FALTA son 800, pero Mac31 propone
        // 600 porque cae en la tercera rama (ImporteZ > TotalFacturado).
        Assert.Equal(800m, Reglas.PorFacturar(1000m, 200m, 400m));
        Assert.Equal(600m, Reglas.MontoQueProponeMac31(1000m, 200m, 400m));
    }

    [Fact]
    public void Lo_que_falta_por_facturar_es_dinero_mas_cascos_menos_lo_facturado()
    {
        // mac :741-745. Las dos ramas de empresa estan COMENTADAS en Mac31
        // (:739-752), o sea que esta cuenta es la misma en Zaragoza y en Tauro.
        Assert.Equal(1200m, Reglas.PorFacturar(1000m, 200m, 0m));
        Assert.Equal(0m, Reglas.PorFacturar(1000m, 200m, 1200m));
    }

    // ======================================================================
    // LAS SEIS PUERTAS DEL LLAMADOR
    // ======================================================================

    private static Reglas.RemisionParaFacturar Rem(
        int id, int cliente = 500, string folio = "A-1", decimal total = 1234.5m, string cancelada = "NO")
        => new() { IdVenta = id, IdCliente = cliente, FolioFtm = folio, TotalPagar = total, Cancelada = cancelada };

    [Fact]
    public void Sin_remisiones_el_motivo_es_el_literal_de_Mac31()
    {
        Assert.Equal("SELECCIONE UNA REMISIÓN.",
            Reglas.MotivoQueImpideFacturar(Array.Empty<Reglas.RemisionParaFacturar>()));
    }

    [Fact]
    public void Un_CFDI_es_de_un_solo_receptor_y_eso_es_la_primera_puerta()
    {
        // ConsultarVentas.cs:4659. Va antes que las de cada renglon: si hay dos
        // clientes, no importa que una este cancelada.
        var motivo = Reglas.MotivoQueImpideFacturar(new[]
        {
            Rem(1, cliente: 500),
            Rem(2, cliente: 501, cancelada: "SI")
        });

        Assert.Equal("LAS REMISIONES DEBEN DE SER DEL MISMO CLIENTE.", motivo);
    }

    [Fact]
    public void El_orden_de_las_tres_puertas_de_renglon_es_el_de_Mac31()
    {
        // Total cero PRIMERO, aunque el folio tambien sea un cambio de vacios y
        // la remision este cancelada. Si faltan tres cosas, se queja de la
        // primera, y ese orden es el que la gente ya aprendio.
        var motivo = Reglas.MotivoQueImpideFacturar(new[]
        {
            Rem(1, folio: "CV-9", total: 0m, cancelada: "SI")
        });

        Assert.Equal("NO SE PUEDE FACTURAR UNA REMISIÓN CON TOTAL A PAGAR CERO.", motivo);
    }

    [Fact]
    public void Un_cambio_de_vacios_no_se_factura_y_se_dice_con_palabras()
    {
        /*
          En Mac31 este caso hace `return` EN SILENCIO (ConsultarVentas.cs:4688 y
          :4745). Se respeta no inventarle un texto de Mac31 —no existe— pero SI
          se contesta algo: un boton que no hace nada ni explica por que manda a
          la gente a preguntar. La frase es nuestra, es la misma que ya usa el
          web (facturarRemision.ts:547-550), y lleva el folio dentro.
        */
        var motivo = Reglas.MotivoQueImpideFacturar(new[] { Rem(1, folio: "CV-9") });

        Assert.Equal(
            "El folio CV-9 es un cambio de vacíos, no una remisión de venta: no se factura.",
            motivo);
    }

    [Fact]
    public void Una_remision_cancelada_se_nombra_con_su_folio_y_los_dos_espacios()
    {
        // El texto de Mac31 lleva DOS espacios alrededor del folio. Se copia tal
        // cual: es el que soporte reconoce al telefono.
        Assert.Equal("LA REMISIÓN  A-7  ESTA CANCELADA.",
            Reglas.MotivoQueImpideFacturar(new[] { Rem(1, folio: "A-7", cancelada: "SI") }));
    }

    [Fact]
    public void Tres_remisiones_del_mismo_cliente_y_vigentes_pasan()
    {
        Assert.Equal(string.Empty, Reglas.MotivoQueImpideFacturar(new[]
        {
            Rem(1, folio: "A-1"), Rem(2, folio: "A-2"), Rem(3, folio: "A-3")
        }));
    }

    // ======================================================================
    // EL FACTOR DE COMISION DE TARJETA
    // ======================================================================

    /*
      mac: VentasFacturacion.cs:2441-2454.

      LA CORRECCION QUE ESTA PRUEBA CLAVA: Mac31 compara el ID numerico
      (`int.Parse(cboFormaPago.SelectedValue) == 4`), y el web compara la CADENA
      (`formaPago === "4"`) contra `idFeFormaPago`, que el DTO del backend
      declara como la CLAVE DEL SAT (ClientesAnexosDtos.cs:127-131). La clave del
      SAT de tarjeta de credito es "04", y `"04" !== "4"`: con un domicilio
      fiscal que la traiga asi, el web NO aplica la comision y rechaza por el
      tope un total que Mac31 acepta. Aqui se parsea.
    */
    [Theory]
    [InlineData("4", 1.03)]
    [InlineData("04", 1.03)]   // la clave del SAT, con el cero: el web falla aqui
    [InlineData("28", 1.02)]
    [InlineData("01", 1.0)]
    [InlineData("03", 1.0)]
    [InlineData("", 1.0)]
    [InlineData(null, 1.0)]
    public void La_comision_de_tarjeta_se_decide_por_numero_y_no_por_cadena(
        string? formaPago, double esperado)
    {
        Assert.Equal((decimal)esperado, Reglas.FactorDeComision(formaPago));
    }

    [Fact]
    public void El_tope_con_tarjeta_de_credito_sube_un_tres_por_ciento()
    {
        // Es el UNICO sitio donde el tope sube por encima de lo que falta,
        // porque la comision del banco se factura tambien.
        var porFacturar = 10_000m;
        Assert.Equal(10_300m, porFacturar * Reglas.FactorDeComision("04"));
        Assert.Equal(10_200m, porFacturar * Reglas.FactorDeComision("28"));
        Assert.Equal(10_000m, porFacturar * Reglas.FactorDeComision("01"));
    }

    // ======================================================================
    // LOS CONCEPTOS: EQUIVALENCIA CON EL WEB, CAMPO POR CAMPO
    // ======================================================================

    [Fact]
    public void El_concepto_sale_identico_al_que_arma_el_web_hoy()
    {
        /*
          LA PRUEBA DE EQUIVALENCIA. Los ocho campos de `ConceptoCfdi` del web
          (nucleo/notaDeCredito.ts:1322-1336), con los valores de la partida real
          de MacZ. Si alguien cambia una columna, esto lo dice antes de que lo
          diga un PDF del SAT.
        */
        var conceptos = Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)PartidaDeMacZ() },
            conIva: true);

        var c = Assert.Single(conceptos);

        Assert.Equal("26111707", c.ClaveProdServ);                 // ClaveSAT
        Assert.Equal("ACUMULADOR LTH L-47(LN2)-550", c.Descripcion); // Concepto
        Assert.Equal("H87", c.ClaveUnidad);                        // UnidadMedidaSAT
        Assert.Equal(1m, c.Cantidad);                              // Cantidad
        Assert.Equal(2388.68m, c.PrecioUnitarioConIva);            // PrecioListaConIvaRnd

        // CERO, no MontoDescuento: el descuento ya esta dentro del precio de
        // arriba. Ver `El_descuento_del_concepto_va_en_cero...` mas abajo.
        Assert.Equal(0m, c.Descuento);
        Assert.Equal(198, c.IdProducto);                           // IDProducto
        Assert.Equal("L-47(LN2)-550", c.Codigo);                   // Codigo
    }

    [Fact]
    public void El_concepto_trae_TAMBIEN_el_precio_que_leeria_Mac31()
    {
        /*
          ══════════════════════════════════════════════════════════════════
          LA DIVERGENCIA MEDIDA, ESCRITA EN UNA PRUEBA
          ══════════════════════════════════════════════════════════════════

          Mac31 lee el precio del concepto de `FacturaPrecioListaConIva`
          (VentasFacturacion.cs:1411-1413) y lo manda en
          `PreciosListaConIvaRndLst` (:1429). El web lee `PrecioListaConIvaRnd`.
          NO SON LA MISMA COLUMNA:

              PrecioListaConIvaRnd      =  2,388.68  (ya con el descuento)
              FacturaPrecioListaConIva  =  4,480.00  (precio de lista con IVA)

          O sea: el web manda el precio YA DESCONTADO *y ademas* manda el
          descuento en el mismo concepto. Mac31 manda el precio de lista.

          NO SE CAMBIA POR CUENTA PROPIA porque quien convierte esas listas en
          conceptos del CFDI es MacServicios2, que no esta en este repositorio:
          desde aqui no se puede ver si aplica el descuento sobre el precio de
          lista ni como reparte el IVA. Elegir una columna a ciegas cambia el
          importe de comprobantes fiscales REALES.

          Lo que SI se hace es que la divergencia viaje en la respuesta y este
          clavada aqui, con las dos cifras, para resolverla con el dato delante.
        */
        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)PartidaDeMacZ() },
            conIva: true));

        Assert.Equal(2388.68m, c.PrecioUnitarioConIva);  // el NETO: lo que se timbra
        Assert.Equal(4480.00m, c.PrecioListaLegacy);     // el precio de LISTA
        Assert.Equal(4480.00m, c.SubtotalLegacy);        // FacturaSubtotalConIva
        Assert.NotEqual(c.PrecioUnitarioConIva, c.PrecioListaLegacy);

        // El aviso dice cual de las dos viaja al CFDI y a donde va la otra.
        Assert.Contains("PrecioListaConIvaRnd", Reglas.DivergenciasConLegacy.PrecioDelConcepto);
        Assert.Contains("Ventas Facturas Detalle", Reglas.DivergenciasConLegacy.PrecioDelConcepto);
    }

    [Fact]
    public void Sin_precios_con_iva_se_leen_las_columnas_de_la_rama_else_de_Mac31()
    {
        // La rama de Tauro (mac :1416-1420). YA VERIFICADA contra 12 facturas
        // timbradas de Produccion_svr: el precio sale de ImportePartida /
        // Cantidad con el IVA puesto (FacturapiService.cs:768-769), NO de
        // PrecioLista. Lo que queda del aviso de la
        // respuesta lo dice.
        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)PartidaDeMacZ() },
            conIva: false));

        // (ImportePartida / Cantidad) x 1.16 = 2,114.21 x 1.16
        Assert.Equal(2452.4836m, c.PrecioUnitarioConIva);
    }

    [Fact]
    public void Solo_las_partidas_marcadas_van_al_CFDI()
    {
        // mac :1421, el `if (bool.Parse(... "SELECCIONAR" ...))`.
        var partidas = new[]
        {
            (IReadOnlyDictionary<string, object?>)PartidaDeMacZ(idVentaDetalle: 1),
            PartidaDeMacZ(idVentaDetalle: 2),
            PartidaDeMacZ(idVentaDetalle: 3)
        };

        var conceptos = Reglas.ConceptosDeLasPartidas(
            partidas, conIva: true, marcadas: new HashSet<int> { 1, 3 });

        Assert.Equal(new[] { 1, 3 }, conceptos.Select(c => c.IdVentaDetalle));
    }

    [Fact]
    public void Sin_lista_de_marcadas_van_todas_como_abre_Mac31()
    {
        // `chkSeleccionar.Checked = true` en todas (mac :218). Null NO es un
        // conjunto vacio: vacio seria "ninguna marcada" y dejaria el CFDI sin
        // conceptos.
        var partidas = new[]
        {
            (IReadOnlyDictionary<string, object?>)PartidaDeMacZ(idVentaDetalle: 1),
            PartidaDeMacZ(idVentaDetalle: 2)
        };

        Assert.Equal(2, Reglas.ConceptosDeLasPartidas(partidas, conIva: true).Count);
        Assert.Empty(Reglas.ConceptosDeLasPartidas(
            partidas, conIva: true, marcadas: new HashSet<int>()));
    }

    [Fact]
    public void La_cantidad_capturada_gana_sobre_la_de_la_remision()
    {
        // Ahi esta la facturacion PARCIAL por partida (web:
        // FacturaDeRemisionPage.tsx:465-471). El TOPE lo pone la validacion, no
        // esto: aqui se respeta lo que se tecleo para que el rechazo lo diga la
        // validacion con su mensaje y no un recorte silencioso.
        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)PartidaDeMacZ(cantidad: 10m) },
            conIva: true,
            cantidadesCapturadas: new Dictionary<int, decimal> { [483747] = 4m }));

        Assert.Equal(4m, c.Cantidad);
        Assert.Equal(10m, c.CantidadOriginal);   // el tope, publicado
    }

    [Fact]
    public void Los_nombres_de_columna_se_leen_sin_distinguir_mayusculas()
    {
        // El procedimiento manda `UnidadMedidaSAT` y `ClaveSAT`; un serializador
        // podria mandarlos en camelCase. Apostar por una capitalizacion es una
        // pantalla que se rompe el dia que alguien toca un SP.
        var camel = new Dictionary<string, object?>
        {
            ["idVentaDetalle"] = 7,
            ["claveSAT"] = "26111707",
            ["unidadMedidaSat"] = "H87",
            ["cantidad"] = 2m,
            ["precioListaConIvaRnd"] = 100m,
            ["concepto"] = "ALGO",
            ["codigo"] = "X-1",
            ["idProducto"] = 9
        };

        var c = Assert.Single(Reglas.ConceptosDeLasPartidas(
            new[] { (IReadOnlyDictionary<string, object?>)camel }, conIva: true));

        Assert.Equal("26111707", c.ClaveProdServ);
        Assert.Equal("H87", c.ClaveUnidad);
        Assert.Equal(100m, c.PrecioUnitarioConIva);
        Assert.Equal(7, c.IdVentaDetalle);
    }

    // ======================================================================
    // LOS CASCOS SE SUMAN A LA PRIMERA REMISION
    // ======================================================================

    [Fact]
    public void Los_cascos_se_suman_completos_a_la_primera_remision()
    {
        /*
          No es un reparto nuestro: lo hace Mac31 con todas sus letras, y el
          comentario esta en el fuente — "A la primera remision se le suman los
          cascos" (VentasFacturacion.cs:1578-1582).
          web: FacturaDeRemisionPage.tsx:447-454.

          Si se repartieran proporcionalmente, la factura de Mac31 y la del web
          dirian cosas distintas sobre la MISMA remision, y el renglon de factura
          es lo que decide cuanto queda por facturar la proxima vez.
        */
        var reparto = Reglas.RepartirPorRemision(new[]
        {
            (1, "A-1", 1000m),
            (2, "A-2", 500m),
            (3, "A-3", 250m)
        }, totalCascos: 380m);

        Assert.Equal(3, reparto.Count);
        Assert.Equal(1380m, reparto[0].Total);   // 1000 + los 380 de casco
        Assert.Equal(500m, reparto[1].Total);
        Assert.Equal(250m, reparto[2].Total);
    }

    [Fact]
    public void Las_remisiones_que_quedan_en_cero_no_se_mandan()
    {
        // El servidor se las salta igual, pero mandarlas hace que el contrato de
        // las tres listas ALINEADAS POR POSICION mienta: el folio de la posicion
        // 2 dejaria de ser el de la remision de la posicion 2.
        var reparto = Reglas.RepartirPorRemision(new[]
        {
            (1, "A-1", 1000m),
            (2, "A-2", 0m),
            (3, "A-3", 250m)
        }, totalCascos: 0m);

        Assert.Equal(new[] { 1, 3 }, reparto.Select(x => x.IdVenta));
    }

    [Fact]
    public void Facturar_solo_cascos_deja_la_primera_remision_con_el_importe_del_casco()
    {
        // Es la segunda rama de `MontoQueProponeMac31`: el dinero ya esta
        // facturado y lo unico que falta son los cascos. Si esto devolviera lista
        // vacia, no se podria facturar un casco suelto.
        var reparto = Reglas.RepartirPorRemision(new[] { (1, "A-1", 0m) }, totalCascos: 380m);

        var unica = Assert.Single(reparto);
        Assert.Equal(380m, unica.Total);
    }

    [Fact]
    public void La_unidad_del_casco_es_pieza()
    {
        // H87 = pieza, la misma unidad con la que viajan los acumuladores. El
        // catalogo de cascos no trae unidad, y es lo que ya manda el web
        // (FacturaDeRemisionPage.tsx:478-480).
        Assert.Equal("H87", Reglas.UnidadDelCasco);
    }

    // ======================================================================
    // LAS VALIDACIONES, EN EL ORDEN DE MAC31
    // ======================================================================

    private static Reglas.DatosDeLaFactura Validos() => new()
    {
        RfcsDisponibles = 1,
        HayRfcElegido = true,
        FormaPago = "03",
        MetodoPago = "PUE",
        UsoCfdi = "G03",
        TotalDinero = 1000m,
        TotalCascos = 0m,
        TopeDeFacturacion = 1200m,
        Partidas = new List<(decimal, decimal)> { (2m, 5m) },
        PiezasDeCascos = 0,
        CorreosDisponibles = 1,
        CorreosMarcados = 1,
        EsZaragoza = false
    };

    [Fact]
    public void Con_todo_puesto_no_hay_reclamo()
    {
        Assert.Null(Reglas.PrimerReclamoDeFactura(Validos()));
    }

    [Fact]
    public void Sin_RFC_la_primera_queja_es_la_del_RFC_aunque_falten_los_tres_combos()
    {
        // mac :2379. El orden es el que la gente ya aprendio: si faltan cuatro
        // cosas, se queja de la primera.
        var d = Validos();
        d.RfcsDisponibles = 0;
        d.HayRfcElegido = false;
        d.FormaPago = "";
        d.MetodoPago = "";
        d.UsoCfdi = "";

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("INGRESE EL RFC DEL CLIENTE.", r!.Mensaje);
        Assert.Equal("rfc", r.Campo);
    }

    [Theory]
    [InlineData("formaPago", "SELECCIONE LA FORMA DE PAGO.")]
    [InlineData("metodoPago", "SELECCIONE EL MÉTODO DE PAGO.")]
    [InlineData("usoCfdi", "SELECCIONE EL USO DEL CFDI.")]
    public void Los_tres_combos_del_CFDI_se_quejan_en_el_orden_de_Mac31(
        string campo, string mensaje)
    {
        var d = Validos();
        if (campo == "formaPago") d.FormaPago = "   ";
        if (campo == "metodoPago") d.MetodoPago = "   ";
        if (campo == "usoCfdi") d.UsoCfdi = "   ";

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal(mensaje, r!.Mensaje);
        Assert.Equal(campo, r.Campo);
    }

    [Fact]
    public void El_tope_de_facturacion_lleva_el_importe_dentro_con_el_formato_de_Mac31()
    {
        /*
          mac :2456-2459. El importe va DENTRO del mensaje porque es lo que la
          persona compara contra lo que tecleo: "menor o igual al tope" sin el
          numero obliga a buscarlo en otra parte de la pantalla.

          El formato es `###,###,##0.00`, que con 10,300 da "10,300.00" y no
          "10300". Un total de dinero sin separador de miles se lee mal en voz
          alta, y estos mensajes se dictan por telefono.
        */
        var d = Validos();
        d.TotalDinero = 10_500m;
        d.TopeDeFacturacion = 10_300m;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("EL TOTAL A FACTURAR DEBE SER\nMENOR O IGUAL A $ 10,300.00", r!.Mensaje);
        Assert.Equal("monto", r.Campo);
    }

    [Fact]
    public void El_total_en_cero_se_queja_antes_que_el_tope()
    {
        // mac :1468-1475, dentro del `if (chkTotalModificado.Checked)` — que
        // siempre es true, porque la casilla es invisible y nace marcada (:223).
        var d = Validos();
        d.TotalDinero = 0m;
        d.TotalCascos = 0m;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("EL TOTAL A FACTURAR DEBE SER MAYOR DE CERO.", r!.Mensaje);
    }

    [Fact]
    public void No_se_puede_facturar_mas_piezas_de_las_que_trae_la_remision()
    {
        // mac :2512.
        var d = Validos();
        d.Partidas = new List<(decimal, decimal)> { (2m, 5m), (7m, 5m) };

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal(
            "LA CANTIDAD DE PRODUCTOS A FACTURAR\nNO PUEDE SER MAYOR A LA CANTIDAD DE PRODUCTOS DE LA REMISIÓN.",
            r!.Mensaje);
        Assert.Equal("cantidades", r.Campo);
    }

    [Fact]
    public void Sin_partidas_y_sin_cascos_no_hay_nada_que_facturar()
    {
        // mac :1453. Las dos condiciones van JUNTAS.
        var d = Validos();
        d.Partidas = new List<(decimal, decimal)>();
        d.PiezasDeCascos = 0;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("SELECCIONE LOS PRODUCTOS A FACTURAR.", r!.Mensaje);
    }

    [Fact]
    public void Se_puede_facturar_SOLO_cascos_sin_ninguna_partida()
    {
        /*
          Es el caso de la segunda rama de `MontoQueProponeMac31` —el dinero ya
          esta facturado y faltan los cascos— y por eso las dos condiciones de
          mac :1453 van juntas. Si se exigieran partidas, un casco suelto no se
          podria facturar desde el web ni desde el movil, y en Mac31 si.
        */
        var d = Validos();
        d.Partidas = new List<(decimal, decimal)>();
        d.PiezasDeCascos = 3;
        d.TotalDinero = 0m;
        d.TotalCascos = 1140m;
        d.TopeDeFacturacion = 1200m;

        Assert.Null(Reglas.PrimerReclamoDeFactura(d));
    }

    [Fact]
    public void Con_partidas_marcadas_pero_todas_en_cero_piezas_se_queja_de_la_cantidad()
    {
        // mac :2524. No es lo mismo que no tener partidas: hay renglones
        // marcados, pero con cantidad 0, y el mensaje es otro.
        var d = Validos();
        d.Partidas = new List<(decimal, decimal)> { (0m, 5m) };
        d.PiezasDeCascos = 0;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("LA CANTIDAD DE PRODUCTOS A FACTURAR\nDEBE SER MAYOR DE CERO.", r!.Mensaje);
    }

    [Fact]
    public void La_errata_TIENER_de_Mac31_se_conserva_en_esta_pantalla()
    {
        /*
          "NECESITA TIENER AL MENOS UN CORREO ELECTRÓNICO." — "TIENER" por
          "TENER". Es una errata de Mac31 y se conserva AQUI porque esta es la
          pantalla que la tiene (mac :2540). La de nota de credito lo escribe
          bien, y alli se usa el bueno: no se uniformizan a ciegas, porque el
          texto que soporte reconoce es el de cada pantalla.
        */
        var d = Validos();
        d.CorreosDisponibles = 0;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("NECESITA TIENER AL MENOS UN CORREO ELECTRÓNICO.", r!.Mensaje);
        Assert.Equal("correos", r.Campo);
    }

    [Fact]
    public void Con_correos_disponibles_pero_ninguno_marcado_se_pide_elegir_uno()
    {
        var d = Validos();
        d.CorreosMarcados = 0;

        var r = Reglas.PrimerReclamoDeFactura(d);
        Assert.NotNull(r);
        Assert.Equal("SELECCIONE UN CORREO.", r!.Mensaje);
    }

    // ======================================================================
    // LA VALIDACION DE DESCUENTO DE ZARAGOZA
    // ======================================================================

    [Fact]
    public void Fuera_de_Zaragoza_la_validacion_de_descuento_no_corre()
    {
        // mac :1496, el `if (Variables.funcionalidad.ToUpper().Contains("ZARA"))`.
        var d = Validos();
        d.EsZaragoza = false;
        d.TotalDinero = 1000m;
        d.SumaDeListaWeb = 10m;      // sobreprecio brutal
        d.SumaDeListaLegacy = 10m;
        d.TopeDeFacturacion = 99_999m;

        Assert.Null(Reglas.PrimerReclamoDeFactura(d));
    }

    [Fact]
    public void La_segunda_pasada_de_Mac31_tapa_siempre_el_sobreprecio_y_eso_se_respeta()
    {
        /*
          ══════════════════════════════════════════════════════════════════
          EL HALLAZGO: EN MAC31 ESTA VALIDACION NO BLOQUEA NUNCA
          ══════════════════════════════════════════════════════════════════

          Mac31 hace DOS pasadas (VentasFacturacion.cs:1496-1531). La primera
          divide el dinero entre la suma de los precios de lista: aqui 2000/1000 =
          200%, o sea descuento -100. NEGATIVO, deberia bloquear.

          Pero entonces hace una SEGUNDA (:1511-1512) que divide el dinero entre
          el total CON CASCOS: 2000/2000 = 100%, descuento 0. Y es esa la que
          decide (:1519). Como `conCascos = dinero + cascos` y `cascos >= 0`, ese
          cociente NUNCA pasa del 100%, asi que el descuento nunca sale negativo.

          O sea: la validacion de descuento de Zaragoza es, en Mac31, codigo
          muerto en la practica. El unico caso que podria bloquear es
          `conCascos == 0`, y ahi ya salto antes "EL TOTAL A FACTURAR DEBE SER
          MAYOR DE CERO." (:1470).

          El web, en cambio, SI bloquea: su segunda pasada divide el total CON
          cascos entre la suma de lista (FacturaDeRemisionPage.tsx:400-403), que es
          otra cuenta. O sea que hay facturas que Mac31 emite y el web rechaza.

          GANA MAC31: aqui no se bloquea. Si se bloqueara, el telefono negaria lo
          que la computadora de la oficina concede, y eso no se le puede explicar
          a quien esta facturando.
        */
        var d = Validos();
        d.EsZaragoza = true;
        d.TotalDinero = 2000m;
        d.TotalCascos = 0m;
        d.TopeDeFacturacion = 99_999m;
        d.SumaDeListaLegacy = 1000m;   // primera pasada: 200% -> descuento -100
        d.SumaDeListaWeb = 1000m;

        Assert.Null(Reglas.PrimerReclamoDeFactura(d));

        // La cuenta de Mac31, aparte, para que quede escrito que la primera
        // pasada SI detecta el sobreprecio y la segunda lo tapa.
        Assert.Equal(0m, Reglas.DescuentoRealDeMac31(1000m, 2000m, 2000m));
    }

    [Fact]
    public void El_sobreprecio_no_bloquea_pero_SE_DICE()
    {
        /*
          No se tira la deteccion: se tira el bloqueo. La primera pasada de Mac31
          es la que ve el sobreprecio, y eso es un dato util —se esta facturando
          por encima de los precios de lista— que la pantalla tiene que poder
          ensenar sin negar nada.
        */
        var aviso = Reglas.AvisoDeSobreprecio(esZaragoza: true, sumaDeLista: 1000m, totalDinero: 2000m);

        Assert.Contains("por encima de los precios de lista", aviso);
        Assert.Contains("-100.00%", aviso);
    }

    [Fact]
    public void Sin_sobreprecio_no_hay_aviso_y_fuera_de_Zaragoza_tampoco()
    {
        // Facturando 600 de 1000 de lista hay un 40% de descuento: normal.
        Assert.Equal(string.Empty,
            Reglas.AvisoDeSobreprecio(esZaragoza: true, sumaDeLista: 1000m, totalDinero: 600m));

        // Y fuera de Zaragoza la validacion no corre, asi que tampoco el aviso.
        Assert.Equal(string.Empty,
            Reglas.AvisoDeSobreprecio(esZaragoza: false, sumaDeLista: 1000m, totalDinero: 2000m));
    }

    [Fact]
    public void Sin_suma_de_lista_la_validacion_de_descuento_no_opina()
    {
        // mac :1497, el `if (SumaSubTotalSinUsadoConIvaRnd > 0)`. Dividir entre
        // cero no es "descuento infinito": es que no hay con que comparar.
        Assert.Equal(0m, Reglas.DescuentoRealDeMac31(0m, 2000m, 2000m));
        Assert.Equal(string.Empty,
            Reglas.AvisoDeSobreprecio(esZaragoza: true, sumaDeLista: 0m, totalDinero: 2000m));

        var d = Validos();
        d.EsZaragoza = true;
        d.TotalDinero = 2000m;
        d.TopeDeFacturacion = 99_999m;
        d.SumaDeListaWeb = 0m;
        d.SumaDeListaLegacy = 0m;

        Assert.Null(Reglas.PrimerReclamoDeFactura(d));
    }

    [Fact]
    public void El_descuento_normal_sale_positivo_y_redondeado_a_dos_decimales()
    {
        // mac :1500 y :1513 redondean por pasada. No es cosmetico: redondear
        // ANTES de restar de 100 decide si un caso al filo sale en -0.01 o 0.00.
        //     600 de 1000 de lista -> 60% facturado -> 40% de descuento
        Assert.Equal(40m, Reglas.DescuentoRealDeMac31(1000m, 600m, 600m));
        //     333.33 de 1000 -> 33.33% -> 66.67%
        Assert.Equal(66.67m, Reglas.DescuentoRealDeMac31(1000m, 333.33m, 333.33m));
    }

    // ======================================================================
    // COMPLEMENTO DE PAGO
    // ======================================================================

    [Fact]
    public void El_saldo_de_una_factura_son_los_tres_sumandos_de_Mac31()
    {
        // VentasComplementoPago.cs:146; web: complementoDePago.ts:151-153.
        //     Saldo = TotalFacturado - Abonos - NotasCredito
        Assert.Equal(600m, Reglas.SaldoDeFactura(1000m, 300m, 100m));
        Assert.Equal(0m, Reglas.SaldoDeFactura(1000m, 1000m, 0m));
        // Un saldo negativo NO se recorta a cero: si la base dice que se cobro
        // de mas, taparlo con un cero esconde el problema.
        Assert.Equal(-50m, Reglas.SaldoDeFactura(1000m, 1050m, 0m));
    }

    private static Reglas.DatosDelComplemento ComplementoValido() => new()
    {
        Facturas = 1,
        Clientes = new HashSet<int> { 500 },
        FormaPago = "03",
        APagar = 600m,
        Saldo = 600m,
        Renglones = new List<(decimal, decimal, string)> { (600m, 600m, "FZ-100") },
        CorreosDisponibles = 1,
        CorreosMarcados = 1
    };

    [Fact]
    public void Un_complemento_con_todo_puesto_no_tiene_reclamo()
    {
        Assert.Null(Reglas.PrimerReclamoDeComplemento(ComplementoValido()));
    }

    [Fact]
    public void Las_facturas_de_un_complemento_son_del_mismo_cliente()
    {
        var d = ComplementoValido();
        d.Clientes = new HashSet<int> { 500, 501 };

        var r = Reglas.PrimerReclamoDeComplemento(d);
        Assert.NotNull(r);
        Assert.Equal("LAS FACTURAS DEBEN DE PERTENECER AL MISMO CLIENTE.", r!.Mensaje);
    }

    [Fact]
    public void Una_factura_cancelada_o_pagada_se_nombra_con_su_folio()
    {
        var cancelada = ComplementoValido();
        cancelada.FolioCancelado = "FZ-100";
        Assert.Equal("LA FACTURA ESTA CANCELADA, FOLIO: FZ-100",
            Reglas.PrimerReclamoDeComplemento(cancelada)!.Mensaje);

        var pagada = ComplementoValido();
        pagada.FolioSinSaldo = "FZ-101";
        Assert.Equal("LA FACTURA YA ESTA PAGADA, FOLIO: FZ-101",
            Reglas.PrimerReclamoDeComplemento(pagada)!.Mensaje);
    }

    [Fact]
    public void El_monto_del_pago_no_puede_pasar_del_saldo_total()
    {
        var d = ComplementoValido();
        d.APagar = 700m;
        d.Saldo = 600m;
        d.Renglones = new List<(decimal, decimal, string)> { (700m, 600m, "FZ-100") };

        var r = Reglas.PrimerReclamoDeComplemento(d);
        Assert.NotNull(r);
        Assert.Equal("EL MONTO DEL PAGO NO PUEDE\nSER MAYOR DE  $ 600.00", r!.Mensaje);
    }

    [Fact]
    public void El_tope_por_renglon_lleva_el_folio_dentro_del_mensaje()
    {
        /*
          Con cinco facturas marcadas, un mensaje sin folio no dice cual hay que
          corregir. Mac31 lo mete, y aqui tambien: el total global cuadra —se
          paga de menos en una y de mas en otra— asi que la unica queja posible
          es la del renglon.
        */
        var d = ComplementoValido();
        d.Facturas = 2;
        d.APagar = 1000m;
        d.Saldo = 1000m;
        d.Renglones = new List<(decimal, decimal, string)>
        {
            (300m, 400m, "FZ-100"),
            (700m, 600m, "FZ-101")
        };

        var r = Reglas.PrimerReclamoDeComplemento(d);
        Assert.NotNull(r);
        Assert.Equal(
            "EL MONTO DEL PAGO $ 700.00\n  DEL FOLIO FZ-101\n  NO PUEDE SER MAYOR AL SALDO $ 600.00",
            r!.Mensaje);
    }

    [Fact]
    public void En_el_complemento_la_errata_TIENER_se_corrige()
    {
        /*
          Aqui SI se corrige, al contrario que en la factura, y por eso las dos
          decisiones estan en pruebas distintas: la pantalla de nota de credito
          escribe la misma frase BIEN (VentasNotaCredito.cs:3054), asi que en el
          complemento se usa la buena. No se uniformiza a ciegas: se usa la que
          la propia Mac31 escribe bien en la pantalla hermana.
        */
        var d = ComplementoValido();
        d.CorreosDisponibles = 0;

        Assert.Equal("NECESITA TENER AL MENOS UN CORREO ELECTRÓNICO.",
            Reglas.PrimerReclamoDeComplemento(d)!.Mensaje);
    }

    // ======================================================================
    // CORREOS
    // ======================================================================

    [Fact]
    public void Los_cinco_correos_se_parten_se_limpian_y_no_se_repiten()
    {
        /*
          Son CINCO COLUMNAS en la base (`Email`..`Email5`), no una lista, y cada
          una puede traer varios separados por coma — Mac31 los concatena asi
          para llenar su rejilla (Ventas.cs:6485+).

          Sin repetidos: las dos capitalizaciones de la misma clave pueden traer
          el mismo correo, y mandarle dos veces la factura al mismo cliente se ve
          como un error del sistema.
        */
        var correos = Reglas.CorreosDe(
            " ventas@tauro.com , compras@tauro.com ",
            "ventas@tauro.com",          // repetido
            "   ",                        // vacio
            null,
            "pagos@tauro.com;otro@tauro.com");

        Assert.Equal(
            new[] { "ventas@tauro.com", "compras@tauro.com", "pagos@tauro.com", "otro@tauro.com" },
            correos);
    }

    [Fact]
    public void Sin_ninguna_columna_con_dato_la_lista_sale_vacia_y_no_con_cadenas_vacias()
    {
        // Una lista con un "" dentro haria que `CorreosDisponibles > 0` y la
        // validacion de correo dejaria pasar un comprobante sin destinatario.
        Assert.Empty(Reglas.CorreosDe(null, "", "  ", ",", " ; "));
    }

    // ======================================================================
    // LECTURA DE NUMEROS
    // ======================================================================

    [Fact]
    public void Los_importes_se_leen_vengan_como_vengan_del_procedimiento()
    {
        // El mismo importe llega decimal de una clase C# y texto de un
        // `Dictionary<string, object?>`. Un "1,234.50" leido como 0 es una
        // factura por cero pesos.
        Assert.Equal(1234.50m, Reglas.Numero("1,234.50"));
        Assert.Equal(1234.50m, Reglas.Numero(1234.50m));
        Assert.Equal(1234.50m, Reglas.Numero(1234.50d));
        Assert.Equal(-50m, Reglas.Numero("-50"));
        Assert.Equal(0m, Reglas.Numero(null));
        Assert.Equal(0m, Reglas.Numero(""));
        Assert.Equal(0m, Reglas.Numero("N/A"));
    }

    [Fact]
    public void El_formato_de_dinero_es_el_de_los_mensajes_de_Mac31()
    {
        // `###,###,##0.00`: separador de miles y dos decimales SIEMPRE. Estos
        // numeros se dictan por telefono.
        Assert.Equal("10,300.00", Reglas.Dinero(10_300m));
        Assert.Equal("0.00", Reglas.Dinero(0m));
        Assert.Equal("1,234,567.89", Reglas.Dinero(1_234_567.89m));
        Assert.Equal("380.00", Reglas.Dinero(380m));
    }
}
