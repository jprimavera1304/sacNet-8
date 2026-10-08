using ISL_Service.Application.DTOs.Empresa;

namespace ISL_Service.Application.Empresa;

/// <summary>
/// TRADUCE LOS DISCRIMINADORES DE LEGACY A CAPACIDADES DE NEGOCIO.
///
/// Esta clase es el unico sitio del backend nuevo donde se escribe "ZARA" o "TAU"
/// para decidir una regla de negocio. Todo lo demas -y todo el front- pregunta por
/// la capacidad. Si manana aparece una tercera empresa, se cambia aqui y nada mas.
///
/// Es codigo puro a proposito: no toca base, no toca DI, y se prueba con una
/// instancia de ConstantesDeEmpresa escrita a mano.
///
/// COMO SE COMPARA LA FUNCIONALIDAD, Y POR QUE NO SIEMPRE IGUAL
/// -----------------------------------------------------------
/// Legacy mezcla dos estilos en el mismo archivo:
///   Variables.funcionalidad.ToUpper().Contains("ZARA")   (el mayoritario)
///   Variables.funcionalidad == "ZARA"                    (31 sitios)
/// Aqui se usa <c>Contains</c> para la mayoria, porque es lo que hace la mayoria de
/// los sitios, PERO se respeta la igualdad exacta donde Legacy la usa y la
/// diferencia importa:
///   - <see cref="CapacidadesDeNegocio.LiquidaSaldosMenoresAutomaticamente"/>:
///     AsignarBancosTransferencias.cs:648 arranca con
///     <c>if (Variables.funcionalidad != "ZARA") return;</c>, igualdad exacta. Con
///     un <c>Contains</c> una tercera empresa llamada "ZARA2" empezaria a liquidar
///     saldos sola, que es dinero movido sin que nadie lo pida.
///   - el reporte de cobranza 1160/1161 ya tiene su propia clase con igualdad
///     exacta: Application/Reportes/ReporteCobranzaPorEmpresa.cs.
/// </summary>
public static class MapaDeCapacidades
{
    /// <summary>
    /// Arma la respuesta publica. <paramref name="hoy"/> entra como parametro (no
    /// se lee <c>DateTime.Today</c> adentro) para que la prueba pueda fijar el dia
    /// y comprobar el limite de la fecha de ventas.
    /// </summary>
    public static CapacidadesEmpresaResponse Armar(ConstantesDeEmpresa c, DateOnly hoy)
    {
        var funcionalidad = Normalizar(c.Funcionalidad);
        var esquemaPago = Normalizar(c.EsquemaPago);

        var esZara = funcionalidad.Contains("ZARA", StringComparison.Ordinal);
        var esZaraExacto = funcionalidad == "ZARA";
        var esTau = funcionalidad.Contains("TAU", StringComparison.Ordinal);
        var esCentroServicio = c.EsCentroServicio == 1;
        var esLuro = c.EsLuro == 1;
        var esAviacion = c.EsAviacion == 1;
        var esObra = c.EsAlcaldiaMC >= 1;

        var fechaPagos = c.FechaOperacionPagosDelUsuario ?? c.FechaOperacionPagos;

        return new CapacidadesEmpresaResponse
        {
            Instalacion = new InstalacionInfo
            {
                Servidor = c.Servidor,
                BaseDatos = c.BaseDatos,
                NombreCentroServicio = (c.NombreCentroServicio ?? string.Empty).Trim()
            },

            Discriminadores = new DiscriminadoresEmpresa
            {
                Funcionalidad = funcionalidad,
                EsquemaPago = esquemaPago,
                EsCentroServicio = esCentroServicio,
                EsAlcaldiaMC = c.EsAlcaldiaMC,
                EsLuro = esLuro,
                EsAviacion = esAviacion,
                EsAviacionServidor = c.EsAviacionServer == 1,
                AviacionIdCaja = c.AviacionIdCaja,
                InventarioNegativo = c.InventarioNegativo == 1
            },

            Capacidades = new CapacidadesDeNegocio
            {
                // Garantias: la misma pantalla, el flujo al reves.
                GarantiaEnvioAProveedorEsElUltimoPaso = esZara,
                GarantiaExigeEnvioAProveedorPrimero = esTau,

                /*
                  IGUALDAD EXACTA, Y NO ES UN DESCUIDO DE LEGACY.

                  Garantia.cs:213 es
                      if ((Variables.funcionalidad == "ZARA") && (Variables.EsCentroServicio == 0))
                  con `==`, mientras que las otras diez ramas de funcionalidad del
                  MISMO archivo (y las nueve de ConsultarGarantias.cs) usan
                  .ToUpper().Contains("ZARA"). Se copia el `==` que esta escrito:
                  con un Contains, una tercera empresa llamada "ZARA2" podria
                  cambiarle el producto a una garantia ya registrada -o sea
                  cambiar QUE pieza se garantizo despues de haberla recibido-, y
                  Legacy no la deja. Cuando la diferencia entre los dos estilos
                  cambia quien puede reescribir un documento, gana lo que dice el
                  archivo.
                */
                GarantiaPermiteCambiarProductoAlEditar = esZaraExacto && !esCentroServicio,

                // Facturacion: otra aritmetica.
                FacturacionProrrateaImporte = !esZara,
                FacturacionMuestraPreciosConIva = esZara,
                PermiteNotaDeCredito = !esTau,
                PermiteComplementoDePago = !esTau,

                // Ventas y cobranza.
                ExigeHojaDeCobroParaPagar = esZara && !esCentroServicio,
                CreditoPorTresConceptos = esZara,
                VentasMuestraColumnasDeSaldo = !esZara,
                PermiteCancelarRemisionFacturada = !esTau,
                UsaConcentrados = esTau,
                LubricantesYFiltrosVanPorAceites = esZara,
                LiquidaSaldosMenoresAutomaticamente = esZaraExacto,
                UsaGalleros = esZara,
                PagoDeCascosPorKilo = esTau,
                ConfirmaImpresoraAlImprimir = esTau,
                RemisionUsaReporteDeCentroDeServicio = esZara && esCentroServicio,

                // Centro de servicio y Luro.
                CatalogosDeProductoSoloLectura = esCentroServicio || esLuro,
                FacturaANombreDeEmpresaMayorista = esCentroServicio,
                CorteDelDiaPorRetiros = esCentroServicio,
                BuscaClientePorNumeroExacto = esCentroServicio,

                // Variante de producto y aviacion.
                OperacionDeObra = esObra,
                MuestraCatalogosDeObra = esObra,
                ProveedoresDeObraVarianteGam = c.EsAlcaldiaMC == 2,
                OperacionDeAviacion = esAviacion,
                TieneModuloDeReportes = !esObra && c.AviacionIdCaja == 0,
                UsuariosConMulticajas = esAviacion,
                UsuariosConPermisoDeGarantiasSinCosto = esTau && !esAviacion,
                EmpleadosConBonosDeAsistencia = esTau,

                // Inventario.
                PermiteVenderSinExistencia = c.InventarioNegativo == 1,

                // Encabezado y fechas.
                EtiquetaFechaDeOperacion = EtiquetaDeFecha(c.EsAlcaldiaMC, esAviacion),
                MuestraFechaDePagos = c.EsAlcaldiaMC == 0 && !esAviacion,
                FechaDePagosEsPorUsuario = esZaraExacto,
                PuedeCambiarFechaDeOperacionDeVentas = c.EsAlcaldiaMC != 1 && c.EsAlcaldiaMC != 2
            },

            Parametros = new ParametrosDeOperacion
            {
                PorcentajeIva = c.Iva / 100m,
                NoDiasAtrasados = c.NoDiasAtrasados,
                NoDiasAdelantados = c.NoDiasAdelantados,
                NoDiasDevDeposito = c.NoDiasDevDeposito,
                DiasFolioConcentrado = c.DiasFolioConcentrado,
                DiasFolioCobranza = c.DiasFolioCobranza,
                NumDiasPedidosRechazados = c.NumDiasPedidosRechazados,
                DiferenciaPagoVsTransCheq = c.DiferenciaPagoVsTransCheq,
                MaximaDiferenciaUsados = c.MaximaDiferenciaUsados,
                FacturaPorcentajeDescMax = c.FacturaPorcentajeDescMax,
                PorDescMax = c.PorDescMax,
                MaxTiempoInactividadMinutos = c.MaxTiempoInactividadMinutos,
                ChecadorHoraTarde = c.ChecadorHoraTarde,
                HorasLaborales = c.HorasLaborales,
                HoraEntrada = (c.HoraEntrada ?? string.Empty).Trim(),
                SalidaTarde = (c.SalidaTarde ?? string.Empty).Trim(),
                DefaultNumCopias = c.DefaultNumCopias,
                MaxNumCopias = c.MaxNumCopias,
                IdAgenteAutorizaGarantias = c.IdAgenteAutorizaGarantias,
                IdGrupoCategoriaServicios = c.IdGrupoCategoriaServicios,
                IdEmpresaCS = c.IdEmpresaCS,
                IdEmpresaZara = c.IdEmpresaZara,
                IdEmpresaLuro = c.IdEmpresaLuro,
                IdClienteCS = c.IdClienteCS,
                IdsClienteZaragoza = (c.IdsClienteZaragoza ?? string.Empty).Trim(),
                IdsClienteServicioDomicilio = c.IdsClienteServicioDomicilio
                    .Where(id => id != 0)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
                ZaraSaldoLiquidarDineroMonto = c.ZaraSaldoLiquidarDineroMonto,
                ZaraSaldoLiquidarCascosMonto = c.ZaraSaldoLiquidarCascosMonto,
                PrecioCascoKilo = c.PrecioCascoKilo
            },

            Fechas = new FechasDeOperacion
            {
                OperacionVentas = c.FechaOperacion,
                OperacionVentasFtm = Ftm(c.FechaOperacion),
                OperacionPagos = fechaPagos,
                OperacionPagosFtm = Ftm(fechaPagos),

                /*
                  EL PROCEDIMIENTO SOLO DEJA AVANZAR UN DIA, Y NO MAS ALLA DE MANANA.
                  sp_n_ActualizarFechaOperacion, rama de ventas:
                      SELECT @FechaInicial = FechaOperacion,
                             @FechaFinal   = DATEADD(dd, 1, getdate())
                      IF (DATEDIFF(dd, @FechaInicial, @FechaFinal) < 1)
                         'LA FECHA DE VENTAS NO SE PUEDE ADELANTAR MAS DE UN DIA A
                          LA FECHA ACTUAL.' + ROLLBACK
                  DATEDIFF(dd, fechaOperacion, hoy+1) >= 1  equivale a
                  fechaOperacion <= hoy.
                */
                PuedeAvanzarVentas = c.FechaOperacion <= hoy,
                SiguienteOperacionVentas = c.FechaOperacion.AddDays(1),

                MinimaOperacionPagos = c.FechaOperacion.AddDays(-c.NoDiasAtrasados),
                MaximaOperacionPagos = c.FechaOperacion.AddDays(c.NoDiasAdelantados),

                UsuarioUltimoCambioVentas = TextoOAdmin(c.UsuarioFechaOperacion),
                FechaUltimoCambioVentasFtm = FtmConHora(c.FechaCambioFechaOperacion),
                EquipoUltimoCambioVentas = (c.EquipoFechaOperacion ?? string.Empty).Trim(),

                UsuarioUltimoCambioPagos = TextoOAdmin(c.UsuarioFechaOperacionPagos),
                FechaUltimoCambioPagosFtm = FtmConHora(c.FechaCambioFechaOperacionPagos),
                EquipoUltimoCambioPagos = (c.EquipoFechaOperacionPagos ?? string.Empty).Trim()
            }
        };
    }

    /// <summary>
    /// Mac31 escribe la etiqueta de la fecha en tres variantes
    /// (Globales.cs:582 y :594): con EsAlcaldiaMC 1 o 2 dice "OPERACION", en
    /// aviacion solo "FECHA", y en MAC normal "VENTAS".
    /// </summary>
    private static string EtiquetaDeFecha(int esAlcaldiaMC, bool esAviacion)
    {
        if (esAviacion) return "FECHA";
        return esAlcaldiaMC >= 1 ? "OPERACION" : "VENTAS";
    }

    /// <summary>
    /// Mayusculas y sin espacios de sobra. El valor sale de una columna de texto
    /// escrita a mano, asi que lo que se perdona es la caja y el espacio; no se
    /// perdona otra palabra.
    /// </summary>
    private static string Normalizar(string? valor)
        => (valor ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// El mismo ISNULL(..., 'ADMIN') que hace sp_n_ConsultaConstantes cuando la
    /// fecha nunca se cambio.
    /// </summary>
    private static string TextoOAdmin(string? valor)
    {
        var t = (valor ?? string.Empty).Trim();
        return t.Length == 0 ? "ADMIN" : t;
    }

    private static string Ftm(DateOnly f) => f.ToString("dd-MM-yyyy");

    private static string FtmConHora(DateTime? f)
        => f.HasValue ? f.Value.ToString("dd-MM-yyyy HH:mm") : string.Empty;
}
