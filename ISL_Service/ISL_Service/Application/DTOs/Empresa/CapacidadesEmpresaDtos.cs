namespace ISL_Service.Application.DTOs.Empresa;

/// <summary>
/// LO QUE EL FRONT NECESITA SABER DE LA EMPRESA EN LA QUE ESTA CORRIENDO.
///
/// Mac31 lee dbo.Constantes al arrancar (Legacy/Mac31/Mac31/Utils/Globales.cs:439-576)
/// y con esos valores decide columnas, botones, reglas de negocio y numeros de
/// reporte: 430 ramas contadas en el inventario del front. El web y el movil no
/// recibian ninguno, asi que una pantalla nueva solo podia implementar UNA de las
/// dos empresas, y el fallo no salia en pruebas: salia en produccion, en la otra.
///
/// EL CONTRATO ES "CAPACIDADES", NO "NOMBRE DE EMPRESA".
/// El front pregunta capacidades.facturacionProrrateaImporte, no "soy Zaragoza?".
/// Los discriminadores crudos tambien viajan, pero son la salida de emergencia
/// para una rama que todavia no tiene capacidad propia, no la via normal.
///
/// El mapa completo -cada capacidad, que cambia en Legacy con archivo y linea, y
/// como debe usarla el front- esta en MAC/docs-frontend/capacidades-de-empresa.md.
/// </summary>
public class CapacidadesEmpresaResponse
{
    public InstalacionInfo Instalacion { get; set; } = new();
    public DiscriminadoresEmpresa Discriminadores { get; set; } = new();
    public CapacidadesDeNegocio Capacidades { get; set; } = new();
    public ParametrosDeOperacion Parametros { get; set; } = new();
    public FechasDeOperacion Fechas { get; set; } = new();
}

/// <summary>
/// Que instalacion es, en terminos tecnicos. NO es el nombre comercial: sirve
/// para que el usuario sepa a donde esta conectado y para diagnosticar.
/// Mac31 lo pinta en su encabezado (Legacy/Mac31/Mac31/Menu.cs:1424-1425).
/// </summary>
public class InstalacionInfo
{
    /// <summary>SERVERPROPERTY('MachineName').</summary>
    public string Servidor { get; set; } = string.Empty;

    /// <summary>db_name(): Produccion_svr es Tauro, MacZ es Zaragoza.</summary>
    public string BaseDatos { get; set; } = string.Empty;

    /// <summary>
    /// Constantes.NombreCentroServicio. En Zaragoza con EsCentroServicio = 1 este
    /// nombre REEMPLAZA el titulo de la aplicacion
    /// (Legacy/Mac31/Mac31/Login.cs:254, Menu.cs:90). Vacio si no aplica.
    /// </summary>
    public string NombreCentroServicio { get; set; } = string.Empty;
}

/// <summary>
/// Los valores crudos de dbo.Constantes con los que Legacy ramifica. Viajan tal
/// cual para que una rama recien descubierta se pueda atender sin esperar una
/// version del backend; lo normal es usar CapacidadesDeNegocio.
/// </summary>
public class DiscriminadoresEmpresa
{
    /// <summary>
    /// Constantes.Funcionalidad, en mayusculas y sin espacios: "ZARA" o "TAU".
    /// 200 referencias en Legacy; es el discriminador principal.
    /// </summary>
    public string Funcionalidad { get; set; } = string.Empty;

    /// <summary>
    /// Constantes.EsquemaPago en mayusculas. 57 ramas contadas por el inventario;
    /// en el cliente de Mac31 solo queda una viva
    /// (Legacy/.../ConsultarVentasPagosUsados.cs:2503), el resto vive dentro de
    /// los procedimientos y del servicio viejo.
    /// </summary>
    public string EsquemaPago { get; set; } = string.Empty;

    /// <summary>
    /// Constantes.EsCentroServicio. 162 referencias. Vale 0 en las dos bases que
    /// existen hoy: un centro de servicio es una TERCERA clase de instalacion
    /// (bases MacZCS*, no restauradas).
    /// </summary>
    public bool EsCentroServicio { get; set; }

    /// <summary>
    /// Constantes.EsAlcaldiaMC. NO ES UN BOOLEANO: vale 0, 1, 2 o 3, y es un
    /// selector de producto, no una bandera. Con 1/2/3 Legacy apaga casi todo el
    /// ERP (Legacy/Mac31/Mac31/Menu.cs:176-235) y con 2 el mismo boton abre otro
    /// formulario (Menu.cs:2107 contra :2133).
    /// </summary>
    public int EsAlcaldiaMC { get; set; }

    /// <summary>Constantes.EsLuro. 11 referencias, todas candados de solo lectura de catalogos.</summary>
    public bool EsLuro { get; set; }

    /// <summary>Constantes.EsAviacion. 3 referencias en Mac31; Aviacion es un arbol de formularios aparte.</summary>
    public bool EsAviacion { get; set; }

    /// <summary>
    /// Constantes.EsAviacionServer. NULL en las dos bases locales, asi que hoy
    /// siempre sale false; se publica porque los procedimientos lo leen.
    /// </summary>
    public bool EsAviacionServidor { get; set; }

    /// <summary>
    /// Constantes.AviacionIDCaja. Con un valor distinto de 0 el arbol de reportes
    /// se corta de golpe (Legacy/.../ReportesMenu.cs:159).
    /// </summary>
    public int AviacionIdCaja { get; set; }

    /// <summary>Constantes.InventarioNegativo: 1 permite vender sin existencia.</summary>
    public bool InventarioNegativo { get; set; }
}

/// <summary>
/// LAS PREGUNTAS QUE EL FRONT DEBE HACER. Cada una se deriva de los
/// discriminadores y lleva en el comentario el sitio de Legacy que ramifica.
/// </summary>
public class CapacidadesDeNegocio
{
    /// <summary>
    /// Mandar al proveedor es el ULTIMO paso y, una vez hecho, bloquea garantia,
    /// ajuste, devolver al cliente, remisionar y agregar a seleccionados. Ademas
    /// solo se puede enviar una garantia ya REMISIONADA.
    /// Legacy: ConsultarGarantias.cs:1836-1851 y :2154-2167 (rama ZARA).
    /// </summary>
    public bool GarantiaEnvioAProveedorEsElUltimoPaso { get; set; }

    /// <summary>
    /// Mandar al proveedor es el PRIMER paso y es REQUISITO para hacer garantia o
    /// ajuste. Solo se puede enviar una garantia REGISTRADA.
    /// Legacy: ConsultarGarantias.cs:1854-1868 y :2170-2180 (rama TAU).
    /// </summary>
    public bool GarantiaExigeEnvioAProveedorPrimero { get; set; }

    /// <summary>
    /// Al EDITAR una garantia ya registrada se puede cambiar el PRODUCTO: la
    /// clave, el buscador de producto, el almacen del producto y la casilla de
    /// "clave otra" vuelven a quedar habilitados. Donde esta capacidad es
    /// <c>false</c>, abrir una garantia para editarla deja esos cuatro controles
    /// apagados y solo se puede tocar el diagnostico, el ajuste y el motivo.
    /// Legacy: Garantia.cs:213 (<c>funcionalidad == "ZARA" &amp;&amp;
    /// EsCentroServicio == 0</c>) y :217-220, los cuatro controles que reabre.
    ///
    /// ES CAPACIDAD PROPIA Y NO SE CUELGA DE
    /// <see cref="GarantiaEnvioAProveedorEsElUltimoPaso"/>: esa dice en que ORDEN
    /// van los pasos del flujo y esta dice quien puede CAMBIAR el producto al
    /// editar. Hoy las dos valen true en Zaragoza y eso las hace parecer la
    /// misma; no lo son, y amarrarlas rompe en silencio a la tercera empresa que
    /// herede el orden de Zaragoza sin heredar el permiso de edicion -o a un
    /// centro de servicio de Zaragoza, donde el orden sigue siendo el de ZARA y
    /// este permiso ya es false.
    /// </summary>
    public bool GarantiaPermiteCambiarProductoAlEditar { get; set; }

    /// <summary>
    /// Al facturar, el importe de la partida se PRORRATEA
    /// (ImportePartida / cantidadOriginal * Cantidad) y el precio sale de
    /// precioLista: VentasFacturacion.cs:1414-1418 (rama TAU). Con false se usan
    /// las columnas ya calculadas con IVA (FacturaPrecioListaConIva,
    /// SubTotalConUsadoConIva): VentasFacturacion.cs:1409-1413 (rama ZARA).
    /// </summary>
    public bool FacturacionProrrateaImporte { get; set; }

    /// <summary>
    /// Las columnas de la rejilla de facturacion son "PRECIO C/IVA" e
    /// "IMPORTE C/IVA" en vez de "PRECIO" e "IMPORTE".
    /// Legacy: VentasFacturacion.cs:1040/:1053 y :1077/:1090.
    /// </summary>
    public bool FacturacionMuestraPreciosConIva { get; set; }

    /// <summary>Existe nota de credito. Legacy: ConsultarVentasFacturas.cs:93-100 la apaga en TAU.</summary>
    public bool PermiteNotaDeCredito { get; set; }

    /// <summary>Existe complemento de pago. Mismo sitio que la nota de credito.</summary>
    public bool PermiteComplementoDePago { get; set; }

    /// <summary>
    /// No se puede aplicar pago ni descuento sin una hoja de cobro abierta.
    /// Legacy: VentasPago.cs:2389 y :2401 (ZARA y no centro de servicio),
    /// repetido en ConsultarVentasPagosUsados.cs:1296, :2477 y :3090.
    /// </summary>
    public bool ExigeHojaDeCobroParaPagar { get; set; }

    /// <summary>
    /// El credito del cliente son TRES condiciones (acumuladores, aceites,
    /// cascos) en vez de una, y la autorizacion del pedido se exige si CUALQUIERA
    /// de las tres pasa de cero. Legacy: Ventas.cs:1584, :1666, :2230,
    /// :2863/:2879, Clientes.cs:1036.
    /// </summary>
    public bool CreditoPorTresConceptos { get; set; }

    /// <summary>
    /// La rejilla de ventas muestra cargos, abonos, descuentos y saldo, y la
    /// columna de vencimiento dice "DIAS VENC". Con false esas cuatro columnas se
    /// ocultan y el encabezado dice "ACU/CASC/ACE".
    /// Legacy: ConsultarVentas.cs:1729, :1737, :1745, :1753, :2060/:2073.
    /// </summary>
    public bool VentasMuestraColumnasDeSaldo { get; set; }

    /// <summary>
    /// Se puede cancelar una remision ya facturada. Legacy:
    /// ConsultarVentas.cs:2936 lo prohibe en TAU; ConsultarCobros.cs:2483 repite
    /// el freno.
    /// </summary>
    public bool PermiteCancelarRemisionFacturada { get; set; }

    /// <summary>
    /// Existen los concentrados de folios: al cancelar se pregunta si se quita el
    /// folio del concentrado y se pide contrasena.
    /// Legacy: ConsultarVentas.cs:2945-2960, Concentrado.cs:136/:143.
    /// </summary>
    public bool UsaConcentrados { get; set; }

    /// <summary>
    /// No se pueden agregar lubricantes ni filtros a una venta normal: van por el
    /// flujo de ACEITES. Legacy: Ventas.cs:1941-1999, con excepcion para los
    /// clientes de servicio a domicilio (ver IdsClienteServicioDomicilio).
    /// </summary>
    public bool LubricantesYFiltrosVanPorAceites { get; set; }

    /// <summary>
    /// Los saldos chicos se liquidan SOLOS con un descuento al aplicar el
    /// deposito, hasta ZaraSaldoLiquidarDineroMonto y ZaraSaldoLiquidarCascosMonto.
    /// Legacy: AsignarBancosTransferencias.cs:648-748.
    /// </summary>
    public bool LiquidaSaldosMenoresAutomaticamente { get; set; }

    /// <summary>Existe el catalogo y el flujo de GALLEROS. Legacy: Menu.cs:141,150.</summary>
    public bool UsaGalleros { get; set; }

    /// <summary>El pago de cascos por kilo existe. Legacy: VentasPago.cs:204 (TAU) / :219 (ZARA lo apaga).</summary>
    public bool PagoDeCascosPorKilo { get; set; }

    /// <summary>
    /// Al imprimir una remision se abre el dialogo de impresora y copias. Con
    /// false se imprime directo. Legacy: ConsultarVentas.cs:4380 (TAU) contra
    /// :4409-4440 (ZARA).
    /// </summary>
    public bool ConfirmaImpresoraAlImprimir { get; set; }

    /// <summary>
    /// La remision se imprime con el reporte del centro de servicio (1210) en vez
    /// de la remision normal (1001). Legacy: ConsultarVentas.cs:4444-4468.
    /// </summary>
    public bool RemisionUsaReporteDeCentroDeServicio { get; set; }

    /// <summary>
    /// Productos, categorias, grupos de categoria, marcas, tipos de usado y la
    /// relacion grupo-marca quedan de SOLO LECTURA: no hay alta ni modificacion.
    /// Legacy: el patron EsCentroServicio == 1 || EsLuro == 1 en
    /// ConsultarProductos.cs:984/:1009, ConsultarCategorias.cs:322/:345,
    /// ConsultarGrupoCategorias.cs:281/:304, ConsultarMarcas.cs:283/:306,
    /// ConsultarTipoUsados.cs:355/:376, RelacionGrupoCategoriaMarcas.cs:283.
    /// </summary>
    public bool CatalogosDeProductoSoloLectura { get; set; }

    /// <summary>
    /// La factura se timbra a nombre de OTRA empresa (la mayorista), no la del
    /// usuario, y no se puede cambiar a quien se factura.
    /// Legacy: VentasFacturacion.cs:1633, :2095, y :292/:512 que ocultan btnRFC y
    /// btnBuscarCliente.
    /// </summary>
    public bool FacturaANombreDeEmpresaMayorista { get; set; }

    /// <summary>
    /// El corte del dia es el de centro de servicio: cuadra retiros por forma de
    /// pago, no la caja de oficina, y no tiene gastos.
    /// Legacy: Menu.cs:2516 abre ConsultarCortes y :2544 ConsultarCortesCS.
    /// </summary>
    public bool CorteDelDiaPorRetiros { get; set; }

    /// <summary>
    /// La busqueda de cliente se fuerza a coincidencia exacta: solo por numero
    /// completo. Legacy: ConsultarVentas.cs:978, ConsultarVentasFacturas.cs:793.
    /// </summary>
    public bool BuscaClientePorNumeroExacto { get; set; }

    /// <summary>
    /// La instalacion es de obra/alcaldia: el ERP de ventas, cobranza, compras,
    /// cortes, clientes, proveedores, productos, facturas, garantias, reportes y
    /// el grupo entero de catalogos NO EXISTE.
    /// Legacy: Menu.cs:176-235 (EsAlcaldiaMC 1, 2 o 3).
    /// </summary>
    public bool OperacionDeObra { get; set; }

    /// <summary>
    /// Los diez catalogos de obra (contratistas, contratos, unidades de medida,
    /// cargos, residentes, tipos de obra/recurso/contratacion/documento,
    /// partidas) se muestran. Legacy: Menu.cs:159-170 los esconde con
    /// EsAlcaldiaMC = 0.
    /// </summary>
    public bool MuestraCatalogosDeObra { get; set; }

    /// <summary>
    /// El modulo de proveedores de obra es la variante GAM, que es OTRO
    /// formulario. Legacy: Menu.cs:2107 (==1, MCConsultarProveedores) contra
    /// :2133 (==2, ConsultarProveedoresGam).
    /// </summary>
    public bool ProveedoresDeObraVarianteGam { get; set; }

    /// <summary>La instalacion es de aviacion.</summary>
    public bool OperacionDeAviacion { get; set; }

    /// <summary>
    /// Existe el modulo de reportes. Legacy lo apaga con EsAlcaldiaMC 1/2/3
    /// (Menu.cs:176-183) y recorta el arbol a COBRANZA + CONTABILIDAD cuando hay
    /// caja de aviacion (ReportesMenu.cs:159).
    /// </summary>
    public bool TieneModuloDeReportes { get; set; }

    /// <summary>
    /// El usuario puede tener multicajas y NO tiene contrasena de autorizacion.
    /// Legacy: Usuarios.cs:161 (EsAviacion).
    /// </summary>
    public bool UsuariosConMulticajas { get; set; }

    /// <summary>
    /// Existen "PERMITIR GARANTIAS SIN COSTO" y "PERMITIR CORTE DEL DIA" en el
    /// alta de usuario. Legacy: Usuarios.cs:62 (solo TAU).
    /// </summary>
    public bool UsuariosConPermisoDeGarantiasSinCosto { get; set; }

    /// <summary>
    /// Los bonos de asistencia, puntualidad y salida tarde existen en el alta de
    /// empleado. Legacy: Empleados.cs:215 (TAU) / :273 (ZARA los oculta).
    /// </summary>
    public bool EmpleadosConBonosDeAsistencia { get; set; }

    /// <summary>
    /// Se puede vender sin existencia. Legacy: Constantes.InventarioNegativo.
    /// Vale 1 en Zaragoza y 0 en Tauro, y lo decide la BASE, no el codigo.
    /// </summary>
    public bool PermiteVenderSinExistencia { get; set; }

    /// <summary>
    /// El rotulo de la fecha de operacion: "VENTAS" en MAC normal, "OPERACION"
    /// con EsAlcaldiaMC 1 o 2, "FECHA" en aviacion.
    /// Legacy: Globales.cs:582 y :594, Menu.cs:1403-1418.
    /// </summary>
    public string EtiquetaFechaDeOperacion { get; set; } = "VENTAS";

    /// <summary>
    /// El encabezado muestra tambien la fecha de pagos. Legacy: Globales.cs:598
    /// (solo con EsAlcaldiaMC = 0), Menu.cs:227 la esconde con 1/2/3.
    /// </summary>
    public bool MuestraFechaDePagos { get; set; }

    /// <summary>
    /// La fecha de operacion de PAGOS es DE CADA USUARIO, guardada en
    /// [Usuarios Fechas]; con false es una sola para toda la empresa, en
    /// Constantes.FechaOperacionPagos.
    ///
    /// Esto NO esta en ningun formulario: lo decide el procedimiento
    /// sp_n_ActualizarFechaOperacion (rama IF Funcionalidad = 'ZARA') y lo lee
    /// sp_n_ConsultaConstantes (misma rama). Un front que la trate como global
    /// mostraria a un usuario la fecha de otro.
    /// </summary>
    public bool FechaDePagosEsPorUsuario { get; set; }

    /// <summary>
    /// Se puede mover la fecha de operacion de ventas desde la interfaz.
    /// Legacy: Menu.cs:2007 hace return seco con EsAlcaldiaMC 1 o 2, antes de
    /// siquiera mirar permisos.
    /// </summary>
    public bool PuedeCambiarFechaDeOperacionDeVentas { get; set; }
}

/// <summary>
/// Los numeros y umbrales de dbo.Constantes que el front necesita para validar en
/// pantalla lo mismo que valida Legacy. No son capacidades: son datos.
/// </summary>
public class ParametrosDeOperacion
{
    /// <summary>IVA / 100, como lo calcula sp_n_ConsultaConstantes. 0.16 en las dos empresas.</summary>
    public decimal PorcentajeIva { get; set; }

    /// <summary>Dias que la fecha de pagos puede quedar ATRASADA respecto a la de ventas. 2 en Tauro, 8 en Zaragoza.</summary>
    public int NoDiasAtrasados { get; set; }

    /// <summary>Dias que la fecha de pagos puede quedar ADELANTADA. 1 en Tauro, 2 en Zaragoza.</summary>
    public int NoDiasAdelantados { get; set; }

    /// <summary>Dias para devolver un deposito.</summary>
    public int NoDiasDevDeposito { get; set; }

    /// <summary>Antiguedad maxima de un folio para entrar al concentrado. 7 en Tauro, 200 en Zaragoza.</summary>
    public int DiasFolioConcentrado { get; set; }

    /// <summary>Antiguedad maxima de un folio para la hoja de cobranza. 5 en Tauro, 200 en Zaragoza.</summary>
    public int DiasFolioCobranza { get; set; }

    /// <summary>Dias que se guardan los pedidos rechazados.</summary>
    public int NumDiasPedidosRechazados { get; set; }

    /// <summary>Tolerancia entre el pago y la transferencia o cheque. 1.00 en Tauro, 0 en Zaragoza.</summary>
    public decimal DiferenciaPagoVsTransCheq { get; set; }

    /// <summary>
    /// Tolerancia de diferencia en usados/cascos. Legacy lo lee CRUDO del
    /// DataTable, sin pasar por Variables, y ramifica con el:
    /// VentasUsados.cs:643 y :717.
    /// </summary>
    public decimal MaximaDiferenciaUsados { get; set; }

    /// <summary>Tope de descuento al facturar. 40 en Tauro, 43 en Zaragoza. Legacy: Ventas.cs:5703.</summary>
    public decimal FacturaPorcentajeDescMax { get; set; }

    /// <summary>Tope de descuento general.</summary>
    public decimal PorDescMax { get; set; }

    /// <summary>Minutos de inactividad antes de cerrar la sesion. 31 en Tauro, 99999 en Zaragoza (practicamente apagado).</summary>
    public int MaxTiempoInactividadMinutos { get; set; }

    /// <summary>Hora a partir de la cual el checador marca retardo. 13 en Tauro, 14 en Zaragoza.</summary>
    public int ChecadorHoraTarde { get; set; }

    /// <summary>Horas laborales del dia. Legacy lo lee crudo del DataTable.</summary>
    public int HorasLaborales { get; set; }

    /// <summary>Hora de entrada, como texto. Legacy lo lee crudo del DataTable.</summary>
    public string HoraEntrada { get; set; } = string.Empty;

    /// <summary>Hora de salida tarde, como texto. Legacy lo lee crudo del DataTable.</summary>
    public string SalidaTarde { get; set; } = string.Empty;

    /// <summary>Copias que trae marcadas el dialogo de impresion. 2 en Tauro, 1 en Zaragoza.</summary>
    public int DefaultNumCopias { get; set; }

    /// <summary>Tope de copias. Legacy lo usa como limite de un for.</summary>
    public int MaxNumCopias { get; set; }

    /// <summary>Agente que autoriza garantias. 9 en las dos.</summary>
    public int IdAgenteAutorizaGarantias { get; set; }

    /// <summary>
    /// Grupo de categoria que son SERVICIOS. Legacy lo lee crudo del DataTable y
    /// ramifica: ConsultarProductos.cs:185, Servicios.cs:448.
    /// </summary>
    public int IdGrupoCategoriaServicios { get; set; }

    /// <summary>Empresa del centro de servicio. 0 en Tauro, 1 en Zaragoza.</summary>
    public int IdEmpresaCS { get; set; }

    /// <summary>Empresa Zaragoza.</summary>
    public int IdEmpresaZara { get; set; }

    /// <summary>Empresa Luro.</summary>
    public int IdEmpresaLuro { get; set; }

    /// <summary>Cliente del centro de servicio.</summary>
    public int IdClienteCS { get; set; }

    /// <summary>
    /// Ids de cliente, separados por coma, que se tratan como "Zaragoza". Vacio
    /// en Tauro, 19669 en Zaragoza.
    /// </summary>
    public string IdsClienteZaragoza { get; set; } = string.Empty;

    /// <summary>
    /// Clientes de servicio a domicilio de Zaragoza (matriz y cada centro de
    /// servicio). Son la excepcion a LubricantesYFiltrosVanPorAceites:
    /// Legacy/.../Ventas.cs:1972-1997.
    /// </summary>
    public List<int> IdsClienteServicioDomicilio { get; set; } = new();

    /// <summary>Monto hasta el que se auto-liquida un saldo de dinero.</summary>
    public decimal ZaraSaldoLiquidarDineroMonto { get; set; }

    /// <summary>Monto hasta el que se auto-liquida un saldo de cascos.</summary>
    public decimal ZaraSaldoLiquidarCascosMonto { get; set; }

    /// <summary>Precio del casco por kilo, para el pago por kilo de Tauro.</summary>
    public decimal PrecioCascoKilo { get; set; }
}

/// <summary>
/// Las dos fechas de operacion, ya resueltas, con los limites que el
/// procedimiento va a aplicar. Con esto el front deshabilita el boton ANTES de
/// pedir el cambio, en vez de enterarse por un mensaje del SP.
/// </summary>
public class FechasDeOperacion
{
    /// <summary>La fecha de operacion de VENTAS (Constantes.FechaOperacion).</summary>
    public DateOnly OperacionVentas { get; set; }

    /// <summary>La misma, con el formato dd-MM-yyyy que pinta Mac31.</summary>
    public string OperacionVentasFtm { get; set; } = string.Empty;

    /// <summary>
    /// La fecha de operacion de PAGOS que le toca a ESTE usuario: de
    /// [Usuarios Fechas] cuando FechaDePagosEsPorUsuario, y de
    /// Constantes.FechaOperacionPagos si no.
    /// </summary>
    public DateOnly OperacionPagos { get; set; }

    /// <summary>La de pagos con formato dd-MM-yyyy.</summary>
    public string OperacionPagosFtm { get; set; } = string.Empty;

    /// <summary>
    /// Si se puede AVANZAR la fecha de ventas ahora mismo. El procedimiento solo
    /// la deja avanzar un dia y no mas alla de manana: exige
    /// DATEDIFF(dd, FechaOperacion, DATEADD(dd,1,getdate())) de al menos 1.
    /// </summary>
    public bool PuedeAvanzarVentas { get; set; }

    /// <summary>
    /// A que fecha quedaria la de ventas si se avanza. Es siempre OperacionVentas
    /// mas un dia: el procedimiento NO acepta una fecha a eleccion.
    /// </summary>
    public DateOnly SiguienteOperacionVentas { get; set; }

    /// <summary>
    /// Fecha minima que acepta el cambio de la de pagos:
    /// FechaOperacion - NoDiasAtrasados. Por debajo, Mac31 pide contrasena y
    /// manda Valido = 1 para saltarsela.
    /// </summary>
    public DateOnly MinimaOperacionPagos { get; set; }

    /// <summary>
    /// Fecha maxima que acepta el cambio de la de pagos:
    /// FechaOperacion + NoDiasAdelantados. Este tope NO se puede saltar con
    /// contrasena.
    /// </summary>
    public DateOnly MaximaOperacionPagos { get; set; }

    /// <summary>Quien cambio por ultima vez la fecha de ventas.</summary>
    public string UsuarioUltimoCambioVentas { get; set; } = string.Empty;

    /// <summary>Formato dd-MM-yyyy HH:mm, vacio si nunca se cambio.</summary>
    public string FechaUltimoCambioVentasFtm { get; set; } = string.Empty;

    /// <summary>Equipo desde el que se cambio la fecha de ventas.</summary>
    public string EquipoUltimoCambioVentas { get; set; } = string.Empty;

    /// <summary>Quien cambio por ultima vez la fecha de pagos.</summary>
    public string UsuarioUltimoCambioPagos { get; set; } = string.Empty;

    /// <summary>Formato dd-MM-yyyy HH:mm, vacio si nunca se cambio.</summary>
    public string FechaUltimoCambioPagosFtm { get; set; } = string.Empty;

    /// <summary>Equipo desde el que se cambio la fecha de pagos.</summary>
    public string EquipoUltimoCambioPagos { get; set; } = string.Empty;
}
