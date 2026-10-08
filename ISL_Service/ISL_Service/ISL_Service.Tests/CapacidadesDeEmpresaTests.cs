using ISL_Service.Application.DTOs.Empresa;
using ISL_Service.Application.Empresa;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;

namespace ISL_Service.Tests;

/// <summary>
/// LAS CAPACIDADES DE EMPRESA, MEDIDAS CONTRA LAS DOS BASES DE VERDAD.
///
/// Las dos instancias de ConstantesDeEmpresa que usan estas pruebas no son
/// inventadas: son los valores leidos de dbo.Constantes en Produccion_svr (Tauro) y
/// en MacZ (Zaragoza) el 07-10-2026. Si manana alguien cambia el mapa y rompe una
/// empresa, aqui se ve, en vez de verse en produccion y solo en la otra empresa.
/// </summary>
public class CapacidadesDeEmpresaTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 7);

    /// <summary>Tauro: Produccion_svr, tal como esta la base.</summary>
    private static ConstantesDeEmpresa Tauro() => new()
    {
        Servidor = "LAP-JUAND",
        BaseDatos = "Produccion_svr",
        Funcionalidad = "TAU",
        EsquemaPago = "TAU",
        EsCentroServicio = 0,
        EsAlcaldiaMC = 0,
        EsLuro = 0,
        EsAviacion = 0,
        InventarioNegativo = 0,
        Iva = 16.00m,
        NoDiasAtrasados = 2,
        NoDiasAdelantados = 1,
        DiasFolioConcentrado = 7,
        DiasFolioCobranza = 5,
        DiferenciaPagoVsTransCheq = 1.00m,
        FacturaPorcentajeDescMax = 40.00m,
        MaxTiempoInactividadMinutos = 31,
        ChecadorHoraTarde = 13,
        IdEmpresaCS = 0,
        IdsClienteZaragoza = "",
        DefaultNumCopias = 2,
        MaxNumCopias = 3,
        FechaOperacion = new DateOnly(2026, 10, 7),
        FechaOperacionPagos = new DateOnly(2026, 2, 27)
    };

    /// <summary>Zaragoza: MacZ, tal como esta la base.</summary>
    private static ConstantesDeEmpresa Zaragoza() => new()
    {
        Servidor = "LAP-JUAND",
        BaseDatos = "MacZ",
        Funcionalidad = "ZARA",
        EsquemaPago = "ZARA",
        EsCentroServicio = 0,
        EsAlcaldiaMC = 0,
        EsLuro = 0,
        EsAviacion = 0,
        InventarioNegativo = 1,
        Iva = 16.00m,
        NoDiasAtrasados = 8,
        NoDiasAdelantados = 2,
        DiasFolioConcentrado = 200,
        DiasFolioCobranza = 200,
        DiferenciaPagoVsTransCheq = 0m,
        FacturaPorcentajeDescMax = 43.00m,
        MaxTiempoInactividadMinutos = 99999,
        ChecadorHoraTarde = 14,
        IdEmpresaCS = 1,
        IdsClienteZaragoza = "19669",
        DefaultNumCopias = 1,
        MaxNumCopias = 3,
        FechaOperacion = new DateOnly(2026, 10, 7),
        FechaOperacionPagos = new DateOnly(2023, 8, 2)
    };

    // =====================================================================
    // GARANTIAS: el flujo va al reves, y es la trampa mas cara del inventario
    // =====================================================================

    [Fact]
    public void En_Zaragoza_Enviar_Al_Proveedor_Es_El_Ultimo_Paso()
    {
        /*
          ConsultarGarantias.cs:1836-1851: en ZARA, si el renglon ya esta
          EnviadaProveedor, GARANTIA y AJUSTE se NIEGAN. Y :2154 solo deja enviar las
          ya Remisionadas. O sea: enviar al proveedor cierra el expediente.
        */
        var c = MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades;

        Assert.True(c.GarantiaEnvioAProveedorEsElUltimoPaso);
        Assert.False(c.GarantiaExigeEnvioAProveedorPrimero);
    }

    [Fact]
    public void En_Tauro_Enviar_Al_Proveedor_Es_El_Primer_Paso_Y_Es_Requisito()
    {
        /*
          ConsultarGarantias.cs:1854-1868: en TAU, si EnviadaProveedor == "NO" se
          NIEGA la garantia. Y :2170 solo deja enviar las Registradas. Es la condicion
          exactamente opuesta a Zaragoza: un front que implemente una sola de las dos
          rompe la otra empresa en silencio, y el sintoma seria "el boton no me deja"
          sin ninguna pista.
        */
        var c = MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades;

        Assert.True(c.GarantiaExigeEnvioAProveedorPrimero);
        Assert.False(c.GarantiaEnvioAProveedorEsElUltimoPaso);
    }

    [Fact]
    public void Las_Dos_Reglas_De_Garantias_Nunca_Son_Las_Dos_Verdaderas()
    {
        // Son excluyentes por construccion: si alguna vez salen las dos en true, el
        // front se queda sin saber que flujo dibujar.
        foreach (var crudas in new[] { Tauro(), Zaragoza() })
        {
            var c = MapaDeCapacidades.Armar(crudas, Hoy).Capacidades;
            Assert.False(c.GarantiaEnvioAProveedorEsElUltimoPaso
                      && c.GarantiaExigeEnvioAProveedorPrimero);
        }
    }

    // =====================================================================
    // FACTURACION: otra aritmetica, no otra columna
    // =====================================================================

    [Fact]
    public void Tauro_Prorratea_Al_Facturar_Y_Zaragoza_Usa_Precio_De_Lista_Con_Iva()
    {
        /*
          VentasFacturacion.cs:1409-1418.
            ZARA -> FacturaPrecioListaConIva + SubTotalConUsadoConIva (ya con IVA)
            TAU  -> precioLista + ImportePartida / cantidadOriginal * Cantidad
          No es una columna distinta: es otra aritmetica, y por eso los totales de
          una pantalla hecha para una empresa no cuadran en la otra.
        */
        var tau = MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades;
        var zara = MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades;

        Assert.True(tau.FacturacionProrrateaImporte);
        Assert.False(tau.FacturacionMuestraPreciosConIva);

        Assert.False(zara.FacturacionProrrateaImporte);
        Assert.True(zara.FacturacionMuestraPreciosConIva);
    }

    [Fact]
    public void Tauro_No_Tiene_Nota_De_Credito_Ni_Complemento_De_Pago()
    {
        // ConsultarVentasFacturas.cs:93-100 y :1205-1210.
        var tau = MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades;
        var zara = MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades;

        Assert.False(tau.PermiteNotaDeCredito);
        Assert.False(tau.PermiteComplementoDePago);
        Assert.True(zara.PermiteNotaDeCredito);
        Assert.True(zara.PermiteComplementoDePago);
    }

    // =====================================================================
    // LO QUE CAMBIA ENTRE LAS DOS BASES DE VERDAD
    // =====================================================================

    [Fact]
    public void Zaragoza_Permite_Vender_Sin_Existencia_Y_Tauro_No()
    {
        // Constantes.InventarioNegativo: 1 en MacZ, 0 en Produccion_svr. Lo decide la
        // base, no el codigo.
        Assert.True(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.PermiteVenderSinExistencia);
        Assert.False(MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades.PermiteVenderSinExistencia);
    }

    [Fact]
    public void Zaragoza_Exige_Hoja_De_Cobro_Para_Pagar_Y_El_Centro_De_Servicio_No()
    {
        /*
          VentasPago.cs:2389/:2401 - la condicion es ZARA **y no** centro de servicio.
          Si el front solo mira la funcionalidad, un centro de servicio de Zaragoza se
          queda sin poder cobrar.
        */
        Assert.True(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.ExigeHojaDeCobroParaPagar);

        var zaraCS = Zaragoza();
        zaraCS.EsCentroServicio = 1;
        Assert.False(MapaDeCapacidades.Armar(zaraCS, Hoy).Capacidades.ExigeHojaDeCobroParaPagar);

        Assert.False(MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades.ExigeHojaDeCobroParaPagar);
    }

    [Fact]
    public void El_Iva_Se_Publica_Como_Fraccion_No_Como_Porcentaje()
    {
        // sp_n_ConsultaConstantes devuelve IVA/100 como PorcentajeIVA, y Mac31 lo usa
        // asi. Publicar 16 en vez de 0.16 multiplicaria por cien cualquier total.
        Assert.Equal(0.16m, MapaDeCapacidades.Armar(Tauro(), Hoy).Parametros.PorcentajeIva);
        Assert.Equal(0.16m, MapaDeCapacidades.Armar(Zaragoza(), Hoy).Parametros.PorcentajeIva);
    }

    [Fact]
    public void Los_Umbrales_Salen_De_La_Base_Y_No_Son_Iguales_En_Las_Dos()
    {
        var tau = MapaDeCapacidades.Armar(Tauro(), Hoy).Parametros;
        var zara = MapaDeCapacidades.Armar(Zaragoza(), Hoy).Parametros;

        Assert.Equal(2, tau.NoDiasAtrasados);
        Assert.Equal(8, zara.NoDiasAtrasados);
        Assert.Equal(40.00m, tau.FacturaPorcentajeDescMax);
        Assert.Equal(43.00m, zara.FacturaPorcentajeDescMax);
        Assert.Equal(7, tau.DiasFolioConcentrado);
        Assert.Equal(200, zara.DiasFolioConcentrado);
    }

    // =====================================================================
    // LA FECHA DE PAGOS DE ZARAGOZA ES DE CADA USUARIO
    // =====================================================================

    [Fact]
    public void En_Zaragoza_La_Fecha_De_Pagos_Es_Por_Usuario_Y_En_Tauro_De_La_Empresa()
    {
        /*
          Esto no esta en ningun formulario: lo decide sp_n_ActualizarFechaOperacion.
            IF @Funcionalidad = 'TAU'  -> UPDATE Constantes.FechaOperacionPagos
            IF @Funcionalidad = 'ZARA' -> UPDATE [Usuarios Fechas] WHERE IDUsuario
          Un front que la trate como global le mostraria a un usuario la fecha de otro.
        */
        Assert.True(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.FechaDePagosEsPorUsuario);
        Assert.False(MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades.FechaDePagosEsPorUsuario);
    }

    [Fact]
    public void La_Fecha_De_Pagos_Del_Usuario_Le_Gana_A_La_De_La_Empresa()
    {
        var zara = Zaragoza();
        zara.FechaOperacionPagosDelUsuario = new DateOnly(2026, 10, 5);

        var fechas = MapaDeCapacidades.Armar(zara, Hoy).Fechas;

        Assert.Equal(new DateOnly(2026, 10, 5), fechas.OperacionPagos);
        Assert.Equal("05-10-2026", fechas.OperacionPagosFtm);
    }

    [Fact]
    public void Sin_Fila_Del_Usuario_Se_Usa_La_De_La_Empresa()
    {
        // sp_n_ConsultaConstantes inserta la fila con FechaOperacionPagos =
        // FechaOperacion; nosotros NO insertamos (leer no escribe) y devolvemos la de
        // la empresa, que es el valor con el que legacy la crearia.
        var zara = Zaragoza();
        zara.FechaOperacionPagosDelUsuario = null;

        Assert.Equal(new DateOnly(2023, 8, 2), MapaDeCapacidades.Armar(zara, Hoy).Fechas.OperacionPagos);
    }

    // =====================================================================
    // LOS LIMITES DE LAS DOS FECHAS
    // =====================================================================

    [Fact]
    public void La_Fecha_De_Ventas_Solo_Avanza_Un_Dia_Y_No_Mas_Alla_De_Manana()
    {
        /*
          sp_n_ActualizarFechaOperacion, rama de ventas:
            IF (DATEDIFF(dd, FechaOperacion, DATEADD(dd,1,getdate())) < 1)
               'LA FECHA DE VENTAS NO SE PUEDE ADELANTAR MAS DE UN DIA A LA FECHA
                ACTUAL.' + ROLLBACK
          Con la fecha en hoy todavia se puede (quedaria en manana); con la fecha ya
          en manana, no.
        */
        var atrasada = Tauro();
        atrasada.FechaOperacion = new DateOnly(2026, 10, 5);
        var f1 = MapaDeCapacidades.Armar(atrasada, Hoy).Fechas;
        Assert.True(f1.PuedeAvanzarVentas);
        Assert.Equal(new DateOnly(2026, 10, 6), f1.SiguienteOperacionVentas);

        var enHoy = Tauro();
        enHoy.FechaOperacion = Hoy;
        Assert.True(MapaDeCapacidades.Armar(enHoy, Hoy).Fechas.PuedeAvanzarVentas);

        var enManana = Tauro();
        enManana.FechaOperacion = Hoy.AddDays(1);
        Assert.False(MapaDeCapacidades.Armar(enManana, Hoy).Fechas.PuedeAvanzarVentas);
    }

    [Fact]
    public void La_Ventana_De_La_Fecha_De_Pagos_Sale_De_NoDiasAtrasados_Y_NoDiasAdelantados()
    {
        // sp_n_ActualizarFechaOperacion, rama de pagos:
        //   @FechaInicial = DATEADD(dd, NoDiasAtrasados*-1, FechaOperacion)
        //   @FechaFinal   = DATEADD(dd, NoDiasAdelantados,  FechaOperacion)
        var tau = MapaDeCapacidades.Armar(Tauro(), Hoy).Fechas;
        Assert.Equal(new DateOnly(2026, 10, 5), tau.MinimaOperacionPagos);
        Assert.Equal(new DateOnly(2026, 10, 8), tau.MaximaOperacionPagos);

        var zara = MapaDeCapacidades.Armar(Zaragoza(), Hoy).Fechas;
        Assert.Equal(new DateOnly(2026, 9, 29), zara.MinimaOperacionPagos);
        Assert.Equal(new DateOnly(2026, 10, 9), zara.MaximaOperacionPagos);
    }

    // =====================================================================
    // CENTRO DE SERVICIO, LURO, OBRA Y AVIACION
    // =====================================================================

    [Fact]
    public void Un_Centro_De_Servicio_O_Luro_Dejan_Los_Catalogos_De_Producto_En_Solo_Lectura()
    {
        // El patron EsCentroServicio == 1 || EsLuro == 1, repetido en seis pantallas
        // de catalogos (ConsultarProductos.cs:984/:1009 y companeras).
        var cs = Zaragoza();
        cs.EsCentroServicio = 1;
        Assert.True(MapaDeCapacidades.Armar(cs, Hoy).Capacidades.CatalogosDeProductoSoloLectura);

        var luro = Zaragoza();
        luro.EsLuro = 1;
        Assert.True(MapaDeCapacidades.Armar(luro, Hoy).Capacidades.CatalogosDeProductoSoloLectura);

        Assert.False(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.CatalogosDeProductoSoloLectura);
        Assert.False(MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades.CatalogosDeProductoSoloLectura);
    }

    [Theory]
    [InlineData(0, false, true, "VENTAS", true)]
    [InlineData(1, true, false, "OPERACION", false)]
    [InlineData(2, true, false, "OPERACION", false)]
    [InlineData(3, true, false, "OPERACION", true)]
    public void EsAlcaldiaMC_No_Es_Un_Booleano(
        int valor, bool esObra, bool tieneReportes, string etiqueta, bool puedeCambiarFecha)
    {
        /*
          Vale 0, 1, 2 o 3 y es un selector de producto. Menu.cs:176-235 apaga casi
          todo el ERP con 1/2/3; Globales.cs:582 cambia la etiqueta con >= 1; y
          Menu.cs:2007 solo bloquea el cambio de fecha con 1 o 2 -el 3 lo deja, que
          parece un descuido de legacy pero se copia tal cual.
        */
        var c = Tauro();
        c.EsAlcaldiaMC = valor;
        var cap = MapaDeCapacidades.Armar(c, Hoy).Capacidades;

        Assert.Equal(esObra, cap.OperacionDeObra);
        Assert.Equal(tieneReportes, cap.TieneModuloDeReportes);
        Assert.Equal(etiqueta, cap.EtiquetaFechaDeOperacion);
        Assert.Equal(puedeCambiarFecha, cap.PuedeCambiarFechaDeOperacionDeVentas);
    }

    [Fact]
    public void Solo_El_Valor_2_De_EsAlcaldiaMC_Usa_La_Variante_Gam_De_Proveedores()
    {
        // Menu.cs:2107 abre MCConsultarProveedores con ==1 y :2133
        // ConsultarProveedoresGam con ==2. Mismo boton, dos formularios.
        for (var v = 0; v <= 3; v++)
        {
            var c = Tauro();
            c.EsAlcaldiaMC = v;
            Assert.Equal(v == 2, MapaDeCapacidades.Armar(c, Hoy).Capacidades.ProveedoresDeObraVarianteGam);
        }
    }

    [Fact]
    public void Con_Caja_De_Aviacion_No_Hay_Modulo_De_Reportes()
    {
        // ReportesMenu.cs:159 hace return seco cuando AviacionIDCaja != 0.
        var c = Tauro();
        c.AviacionIdCaja = 2;
        Assert.False(MapaDeCapacidades.Armar(c, Hoy).Capacidades.TieneModuloDeReportes);
    }

    [Fact]
    public void En_Aviacion_La_Etiqueta_De_Fecha_Es_Solo_FECHA_Y_No_Se_Muestra_La_De_Pagos()
    {
        // Globales.cs:594 arma el encabezado con "FECHA: " y sin la parte de Pagos.
        var c = Tauro();
        c.EsAviacion = 1;
        var cap = MapaDeCapacidades.Armar(c, Hoy).Capacidades;

        Assert.Equal("FECHA", cap.EtiquetaFechaDeOperacion);
        Assert.False(cap.MuestraFechaDePagos);
        Assert.True(cap.UsuariosConMulticajas);
    }

    // =====================================================================
    // GARANTIAS: CAMBIAR EL PRODUCTO AL EDITAR
    // =====================================================================

    [Fact]
    public void Cambiar_El_Producto_Al_Editar_Es_De_Zaragoza_Y_Fuera_De_Centro_De_Servicio()
    {
        // Garantia.cs:213: ZARA y EsCentroServicio == 0, las dos condiciones.
        Assert.True(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.GarantiaPermiteCambiarProductoAlEditar);
        Assert.False(MapaDeCapacidades.Armar(Tauro(), Hoy).Capacidades.GarantiaPermiteCambiarProductoAlEditar);

        var centro = Zaragoza();
        centro.EsCentroServicio = 1;
        var cap = MapaDeCapacidades.Armar(centro, Hoy).Capacidades;
        Assert.False(cap.GarantiaPermiteCambiarProductoAlEditar);

        /*
          Y EN EL MISMO CENTRO EL ORDEN DE LOS PASOS SIGUE SIENDO EL DE ZARAGOZA.
          Esta es la pareja que prueba que las dos capacidades NO son la misma: si
          "cambiar el producto" se hubiera colgado de
          GarantiaEnvioAProveedorEsElUltimoPaso, aqui las dos dirian true y el
          centro de servicio podria reescribir el producto de una garantia.
        */
        Assert.True(cap.GarantiaEnvioAProveedorEsElUltimoPaso);
    }

    [Fact]
    public void Cambiar_El_Producto_Al_Editar_Exige_ZARA_EXACTO()
    {
        /*
          Garantia.cs:213 compara con `==`, a diferencia de las otras diez ramas
          de funcionalidad del mismo archivo, que usan .Contains("ZARA"). Una
          tercera empresa "ZARA2" hereda el orden de los pasos pero NO el permiso
          de cambiarle el producto a una garantia ya registrada.
        */
        var otra = Zaragoza();
        otra.Funcionalidad = "ZARA2";
        var cap = MapaDeCapacidades.Armar(otra, Hoy).Capacidades;

        Assert.False(cap.GarantiaPermiteCambiarProductoAlEditar);
        Assert.True(cap.GarantiaEnvioAProveedorEsElUltimoPaso);
    }

    // =====================================================================
    // COMO SE COMPARA LA FUNCIONALIDAD
    // =====================================================================

    [Theory]
    [InlineData("zara")]
    [InlineData(" ZARA ")]
    [InlineData("Zara")]
    public void La_Caja_Y_Los_Espacios_Se_Perdonan(string funcionalidad)
    {
        // El valor sale de una columna de texto escrita a mano.
        var c = Zaragoza();
        c.Funcionalidad = funcionalidad;
        Assert.True(MapaDeCapacidades.Armar(c, Hoy).Capacidades.GarantiaEnvioAProveedorEsElUltimoPaso);
        Assert.Equal("ZARA", MapaDeCapacidades.Armar(c, Hoy).Discriminadores.Funcionalidad);
    }

    [Fact]
    public void La_Liquidacion_Automatica_De_Saldos_Exige_ZARA_EXACTO()
    {
        /*
          AsignarBancosTransferencias.cs:648 arranca con
              if (Variables.funcionalidad != "ZARA") return;
          igualdad exacta. Con un Contains, una tercera empresa llamada "ZARA2"
          empezaria a liquidar saldos sola -es dinero movido sin que nadie lo pida-,
          cosa que Legacy NO hace. Se copia la igualdad.
        */
        Assert.True(MapaDeCapacidades.Armar(Zaragoza(), Hoy).Capacidades.LiquidaSaldosMenoresAutomaticamente);

        var otra = Zaragoza();
        otra.Funcionalidad = "ZARA2";
        var cap = MapaDeCapacidades.Armar(otra, Hoy).Capacidades;
        Assert.False(cap.LiquidaSaldosMenoresAutomaticamente);
        // pero el resto de las reglas de Zaragoza si entran, como en Legacy:
        Assert.True(cap.GarantiaEnvioAProveedorEsElUltimoPaso);
    }

    // =====================================================================
    // EL SERVICIO
    // =====================================================================

    [Fact]
    public async Task El_Servicio_Pide_La_Fecha_De_Pagos_Con_El_IdUsuario_Del_Token()
    {
        // En Zaragoza la fecha de pagos es de cada usuario: si el id no llega al
        // repositorio, todos ven la de la empresa.
        var repo = new FakeCapacidadesRepo { Constantes = Zaragoza() };
        var service = new CapacidadesEmpresaService(repo);

        await service.ConsultarAsync(1065, CancellationToken.None);

        Assert.Equal(1065, repo.IdUsuarioRecibido);
    }

    [Fact]
    public async Task El_Servicio_Devuelve_Las_Capacidades_Ya_Traducidas()
    {
        var repo = new FakeCapacidadesRepo { Constantes = Tauro() };
        var service = new CapacidadesEmpresaService(repo);

        var r = await service.ConsultarAsync(3, CancellationToken.None);

        Assert.Equal("TAU", r.Discriminadores.Funcionalidad);
        Assert.True(r.Capacidades.GarantiaExigeEnvioAProveedorPrimero);
        Assert.Equal("Produccion_svr", r.Instalacion.BaseDatos);
    }

    private sealed class FakeCapacidadesRepo : ICapacidadesEmpresaRepository
    {
        public ConstantesDeEmpresa Constantes { get; set; } = new();
        public int IdUsuarioRecibido { get; private set; } = -1;

        public Task<ConstantesDeEmpresa> ConsultarAsync(int idUsuario, CancellationToken ct)
        {
            IdUsuarioRecibido = idUsuario;
            return Task.FromResult(Constantes);
        }
    }
}
