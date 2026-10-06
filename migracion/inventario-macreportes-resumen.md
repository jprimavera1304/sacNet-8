# Inventario de MacReportes (Legacy/MacReportes2/MacReportes)

Inventario de **solo lectura**. Nada de este proyecto fue modificado.

Proyecto: ASP.NET MVC 5 (`Microsoft.AspNet.Mvc 5.2.7`) sobre **.NET Framework 4.7.1**.
Solucion: `D:\JuanD\GitHub\MAC\Legacy\MacReportes2\MacReportes\MacReportes.sln`

---

## 1. Como funciona el pipeline, de principio a fin

### Lo primero que sorprende: MacReportes no sabe nada de la base de datos

No hay una sola cadena de conexion a SQL, ni un `SqlCommand`, ni el nombre de un stored procedure en
todo el proyecto. La unica coincidencia de `StoredProc` en el codigo es el **nombre del parametro de
la URL** (`StoredProcParam`), que se pasa tal cual al backend.

MacReportes es **un renderizador de HTML a PDF y nada mas**. Toda la consulta a base, toda la
plantilla y todo el armado del HTML viven en el otro proyecto legacy:
`D:\JuanD\GitHub\MAC\Legacy\MacServicios2\Mac3Servicios` (`Models/Service/ReportesServ`,
`Models/Service/ReportesV2Serv`, `Models/Service/VentasServ/VentasImprimirServ`).

Por eso la columna `stored_procedures` del TSV dice "ninguno en este proyecto": **no se deduce del
codigo de MacReportes cual SP corre cada reporte**. Para sacar ese mapeo hay que inventariar
`Mac3Servicios` (y la tabla `Reportes` / `Parametros` de la base, de donde V2 lee los parametros
cuando `psp` es un entero).

### El recorrido completo

1. **Entra una peticion GET con todo en el QueryString.** Ruta unica: la default de MVC
   (`App_Start/RouteConfig.cs`: `{controller}/{action}/{id}`). No hay atributos de ruta ni filtros
   propios (`FilterConfig.cs` solo registra `HandleErrorAttribute`). **No hay ningun tipo de
   autenticacion**: quien conozca la URL genera el reporte.

2. **Se lee el archivo `MacReportes.xml`** que esta junto al `Web.config` (`Variables.REPORTES_XML`,
   resuelto con `Server.MapPath(".")`). Ese archivo contiene **una sola linea con la URL base del
   backend** y nada mas; hoy, en local, dice `https://localhost:44341/`. Es el unico punto de
   configuracion del despliegue: por eso cada empresa tiene su **propia instancia de MacReportes en
   IIS** con su `MacReportes.xml` apuntando a `Mac3Servicios` / `MacZServicios` / `MacDevServicios`
   (las variantes estan comentadas en `Utils/Variables.cs`). Si el archivo falta, la pantalla
   responde "NO SE ENCONTRO EL ARCHIVO DE CONFIGURACION".
   El `Web.config` tambien trae `appSettings/UrlServiciosMAC` y un `connectionStrings` llamado `Mac3`
   que contiene una URL con `providerName="System.Data.SqlClient"` — **ninguno de los dos se lee
   desde el codigo**.

3. **Se llama al backend por REST** (`RestSharp 106.11.7`), POST con `AddJsonBody`, Bearer token.
   El token (`Variables.APP_TOKEN`) es un **JWT hardcodeado en el codigo fuente y ya expirado**
   (`exp` 1601199612 = sep-2020), junto con el correo `app_web@tauro.com`. Antes de cada llamada se
   desactiva la validacion del certificado SSL (`ServerCertificateValidationCallback => true`) y se
   fuerza TLS 1.2. Endpoints usados (`Utils/Variables.cs`):
   - `api/reportes/consultar` — V1
   - `api/reportesV2/consultar` — V2
   - `api/ventashtml/consultar` — remision y lote de ventas
   - `api/ventashtml/simular` — simulacion (definido; ver "codigo muerto")
   - `api/constantes/consultar` — solo para traer `PathDescargar`
   - `api/empleadostiposueldorecibos/consultar` — recibos de nomina
   - Declarados pero sin usar: `api/empleados/consultar`, `api/periodostiposueldo/consultar`,
     `api/empleadosperiodotiposueldoasistencias/consultar` (+ el POST de insercion),
     `api/reportes/consultar` via `URL_CONSULTA_REPORTES`... y `URL_REPORTES_CONSULTAR`, que se
     referencia solo en codigo comentado.

4. **El backend devuelve el HTML ya armado** dentro de un JSON. El objeto que vuelve
   (`Models/Reportes/ReporteResponse.cs` -> `ReporteDTO`) trae: `html`, `orientacion`, `ancho`,
   `alto`, `marginTop/Bottom/Left/Right`, `maxLineas`, `Folio`, `NombreCompleto`, `PathDescargar`.
   O sea: **el tamano de pagina y los margenes tambien los decide el backend**, no MacReportes.

5. **Se convierte ese HTML a PDF.** Hay **dos motores distintos conviviendo**:

   | Motor | Libreria | Version | De donde sale |
   |---|---|---|---|
   | Principal | **NReco.PdfGenerator** (`HtmlToPdfConverter`), que por debajo ejecuta **wkhtmltopdf.exe** | `1.1.14.0` | **NO esta en `packages.config`**: es un `HintPath` directo a `bin\NReco.PdfGenerator.dll` (`MacReportes.csproj`, lineas 65-68). |
   | Secundario | **HtmlRenderer.PdfSharp** (`PdfGenerator.GeneratePdf`) sobre **PDFsharp** | `HtmlRenderer.Core 1.5.0.5`, `HtmlRenderer.PdfSharp 1.5.0.6`, `PDFsharp 1.32.3057.0` | `packages.config` |

   **No se usa iTextSharp ni Rotativa.** `bin\IronPdf.dll` y `bin\Spire.License.dll` estan en `bin`
   pero **no se referencian desde el `.csproj` ni aparecen en el codigo**; en `packages.config` quedo
   `Iron.Assets.Rendering.Windows 20.0.0`, residuo de una prueba. De Spire.XLS solo quedan bloques
   comentados en `ReportesV2Controller.DownloadReport`.

   El reparto entre motores esta **hardcodeado como un `if` gigante de IDs** en
   `Controllers/ReportesController.cs`. Estos 19 reportes van por NReco/wkhtmltopdf:
   **10, 15, 20, 71, 75, 105, 110, 150, 205, 210, 220, 225, 230, 1005, 1010, 1015, 1025, 1030, 1035**.
   **Todo lo demas de V1 cae en HtmlRenderer/PdfSharp.** `ReportesV2Controller`,
   `EmpleadosTipoSueldoRecibosController` y `VentasHtmlController` usan **siempre** NReco.

   Margenes: la rama NReco **ignora** los del backend y pone fijos `Top=4, Bottom=6, Left=2, Right=8`.
   La rama PdfSharp si los usa... **con un bug**: `config.MarginTop = reporteDTO.marginBottom`
   (asigna `marginBottom` a `MarginTop`). La orientacion se compara **distinto** segun la rama: NReco
   hace `.ToLower()` contra `"portrait"`/`"landscape"`; PdfSharp compara contra `"Portrait"` con
   mayuscula, y cualquier otro valor cae en Landscape.

6. **wkhtmltopdf viaja dentro del proyecto**: `App_Data/wkhtmltopdf/wkhtmltopdf.exe` (22.5 MB) junto
   con 19 DLLs del runtime de VC++ (`msvcp140.dll`, `ucrtbase.dll`, `vcruntime140.dll`,
   `concrt140.dll`, los `api-ms-win-crt-*`). Es la ruta por convencion de NReco en aplicaciones web;
   el codigo **no setea `PdfToolPath`**, asi que depende de esa carpeta exacta. El `debug.log` de la
   raiz son errores de ese proceso (`directory_reader_win.cc ... FindFirstFile`).
   **Al migrar: esto ata el reporte a Windows y a poder lanzar un proceso hijo.**

7. **Como sale el archivo.** Tres caminos:
   - `Descargar == 0` (lo normal): no se escribe nada a disco. `Response.ContentType = "application/pdf"`,
     `content-length`, `Response.BinaryWrite(bytes)` y el action **devuelve `null`** (se escribe
     directo en la respuesta, saltandose el pipeline de MVC).
   - `Descargar != 0`: escribe el PDF en `reporteDTO.PathDescargar` (creando el directorio si no
     existe), con nombre `tauro_<Folio>_<NombreCompleto recortado a 20 chars>.pdf`, sanitizado con
     `Regex.Replace(..., "[^0-9a-zA-Z]+", "_")`. Luego `Content-Disposition: attachment` +
     `Response.TransmitFile`. **El prefijo `tauro_` esta hardcodeado para las tres empresas.**
     Cierra con `Response.End()`.
   - Excel: `return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre)`
     — ese si es un `ActionResult` normal.
   - Factura electronica (1003 / 1310): `reporteDTO.html` **no es HTML, es una ruta de archivo en
     disco**; se lee con `File.ReadAllBytes` y se devuelve como `application/pdf`, `application/xml`
     o `application/zip` segun `enumPresentacion`.

8. **Si algo falla** no hay 500: se devuelve `View()` con `ViewBag.mensaje`, `ViewBag.fecha`,
   `ViewBag.hostInfo` (`Request.Url.AbsoluteUri`) y `ViewBag.hostServicios`. Las cinco vistas
   `.cshtml` del proyecto son **solo esa pantalla de error** ("NO SE PUDO GENERAR LA INFORMACION,
   COPIE EL TEXTO Y ENVIELO AL ADMINISTRADOR"). **Ninguna vista Razor participa en armar un reporte.**

### Donde viven las plantillas

**No hay plantillas en MacReportes.** Lo mas parecido es `Variables.TEMPLATE_HTML_ERROR`, un HTML
inline para el mensaje de error (`Funciones.MensajeGenericoError` reemplaza `#mensaje#` y `#servidor#`).
Las plantillas de verdad (con sus `#Logo#`, `#CanceladaDetalle#`, etc.) estan en `Mac3Servicios`:
`Models/Service/ReportesV2Serv/RepFormatoNormalService.cs`, `RepFormatoColumnas3Service.cs`,
`RepFacturaCustomService.cs`, `RepV2VentasAcumuladoresService.cs`, `ReporteV2Service.cs`.

### Fuentes y logos: donde se resuelven de verdad

**En MacReportes no hay ni una fuente ni un logo de reporte.**

- `fonts/` contiene **solo las 5 variantes de `glyphicons-halflings-regular`** (.eot, .svg, .ttf,
  .woff, .woff2), que vienen con Bootstrap 3.4.1 y se referencian unicamente desde
  `Content/bootstrap.css`. Sirven a la pantalla de error, **no a los PDFs**.
- `imagenes/` contiene **un unico archivo, `cancelada.png`**, y **no esta referenciado en ningun
  `.cs`, `.cshtml` ni `.css`** del proyecto. **Para que se usa: `?`** — probablemente lo pide el HTML
  que manda el backend por URL relativa, o quedo sin usar. Hay que buscarlo en las plantillas de
  `Mac3Servicios`.
- Las fuentes reales del PDF son **las que tenga instaladas el Windows donde corre wkhtmltopdf**,
  porque el HTML llega con sus `font-family` desde el backend. **Eso no se puede inventariar desde
  aqui: `?`** — hay que leer el CSS de las plantillas en `Mac3Servicios`.
- Los logos salen de la **base de datos de cada empresa**: tabla `Constantes`, columnas
  `PathImagenes` + `LogoMacReportes`. `Mac3Servicios` las concatena y mete la ruta en el `#Logo#` del
  HTML (`Mac3Servicios/Models/Service/ReportesV2Serv/ReporteV2Service.cs`, lineas 113-116, 338-341,
  770-773, 2066-2069, y el `.Replace("#Logo#", logo)` de la 2200). Son **rutas de disco local tipo
  `C:\Mac21\imagenes\...`**, que funcionan solo porque wkhtmltopdf corre en la misma maquina. Hay
  tambien un `LogoMacWM` (marca de agua) por el mismo mecanismo. Esto ya quedo documentado en el
  codigo nuevo: `sacNet-8/ISL_Service/ISL_Service/Infrastructure/Reports/RemisionHtmlBuilder.cs:151`
  y `Infrastructure/Repositories/CascosCambioRepository.cs:86`.

---

## 2. Los numeros de reporte que conoce (`Utils/enumReportes.cs`)

Son **151 valores** en `enumReportes`. El enum es lo unico que define el catalogo; MacReportes solo
usa el numero para (a) decidir si es remision, (b) elegir motor de PDF, (c) elegir el DTO con que
deserializa el Excel. Para todo lo demas el numero viaja al backend.

| Numero | Nombre (Description) | Clave del enum |
|---|---|---|
| 10 | Venta Acumuladores | `VentaAcumuladores` |
| 15 | Venta Productos | `VentaProductos` |
| 19 | Remisiones Con Saldo a Favor | `VentasRemisionesSaldoFavor` |
| 20 | Folios (GLOBAL) | `VentasFoliosGlobal` |
| 21 | Remisiones | `VentasRemisiones` |
| 22 | Impuestos | `VentasRemisionesImpuestos` |
| 23 | Folios Dinero y Usados | `VentasFoliosDineroUsados` |
| 24 | Facturas | `VentasFacturas` |
| 71 | Ventas Pagos Detallado | `VentasPagosDetallado` |
| 72 | Ventas Pagos Detallado CS | `VentasPagosDetalladoCS` |
| 73 | Ventas Pagos Descuentos | `VentasPagosDescuentos` |
| 74 | Ventas Pagos Transferencias Duplicadas | `VentasPagosTransferenciasDuplicadas` |
| 75 | Ventas Pagos Totalizado | `VentasPagosTotalizado` |
| 76 | Ventas Pagos Totalizado CS | `VentasPagosTotalizadoCS` |
| 77 | Ventas Pagos Detallado Tauro | `VentasPagosDetalladoTauro` |
| 103 | Reporte Global de Clientes | `ClientesGlobal` |
| 105 | Reporte de Clientes Con Compra | `ClientesConCompra` |
| 106 | Reporte de Clientes Con Compra Por Dia | `ClientesConCompraPorDia` |
| 110 | Reporte de Clientes Sin Compra | `ClientesSinCompra` |
| 112 | Reporte de Clientes CON y SIN Compra | `ClientesConSinCompra` |
| 113 | Reporte de Clientes Descuentos | `ClientesDescuentos` |
| 114 | Reporte de Clientes SIN Descuentos | `ClientesDescuentosSin` |
| 116 | Reporte de Clientes Compras Acum Moto Lub | `ClientesAcumMotoLub` |
| 118 | Reporte de Clientes Compras Acum Moto Lub Mensual | `ClientesAcumMotoLubMensual` |
| 119 | Reporte de Clientes Compras Acum Moto Lub Mensual Por Categoria | `ClientesAcumMotoLubMensualCategoria` |
| 120 | Compra Acumuladores | `CompraAcumuladores` |
| 125 | Compra Productos | `CompraProductos` |
| 130 | Compras - Facturas | `ComprasFacturas` |
| 140 | Ventas - Utilidad Acumuladores | `VentasUtilidadAcumuladores` |
| 145 | Ventas - Utilidad Productos | `VentasUtilidadProductos` |
| 150 | Ventas Usados Creditos | `VentasUsadosCreditos` |
| 205 | Inventario Acumuladores | `InventarioAcumuladores` |
| 210 | Inventario Productos | `InventarioProductos` |
| 212 | Inventario Filtros | `InventarioFiltros` |
| 220 | Inventario Acumuladores Costo | `InventarioAcumuladoresCosto` |
| 225 | Inventario Productos Costo | `InventarioProductosCosto` |
| 227 | Inventario Filtros Costo | `InventarioFiltrosCosto` |
| 230 | Ajustes al Inventario | `AjustesInventario` |
| 235 | Pedido Acumuladores | `PedidoAcumuladores` |
| 240 | Pedido Productos | `PedidoProductos` |
| 493 | Comparativo Ventas y Pagos | `VentasPagosComparativo` |
| 1001 | Impresion de Remision V2 | `Remision` |
| 1003 | Impresion de Factura Electronica | `FacturaElectronica` |
| 1005 | Concentrado | `Concentrado` |
| 1006 | Hoja de Cobro | `Cobro` |
| 1007 | Factura Zaragoza | `FacturaZaragoza` |
| 1008 | Concentrado Devueltas | `ConcentradoDevueltas` |
| 1010 | Cobranza de Agente | `CobranzaAgente` |
| 1015 | Compra | `Compra` |
| 1017 | Compra Comprobacion | `CompraComprobacion` |
| 1020 | Recibo | `ReciboTipoSueldo` |
| 1025 | Asistencias | `Asistencias` |
| 1026 | AsistenciasZaragoza | `AsistenciasZaragoza` |
| 1027 | Vacaciones | `Vacaciones` |
| 1030 | Ventas Descuentos Ajustes | `VentasDescuentosAjustes` |
| 1035 | Lista Acumuladores | `ListaAcumuladores` |
| 1135 | Inventario Faltante Acumuladores | `InventarioFaltanteAcumuladores` |
| 1136 | Inventario Pedido Acumuladores | `InventarioPedidoAcumuladores` |
| 1137 | Inventario Pedido Acumuladores Con Ventas | `InventarioPedidoAcumuladoresConVentas` |
| 1140 | Inventario Faltante Productos | `InventarioFaltanteProductos` |
| 1141 | Inventario Faltante Filtros | `InventarioFaltanteFiltros` |
| 1160 | Reporte de Ventas Cobranza Detallado | `VentasCobranzaDetallado` |
| 1161 | Reporte de Ventas Cobranza Detallado | `VentasCobranzaDetalladoZara` |
| 1162 | Reporte de Cobranza Pagadas Detallado | `VentasCobranzaPagadasDetalladoZara` |
| 1165 | Reporte de Ventas Cobranza Totalizado | `VentasCobranzaTotalizado` |
| 1166 | Reporte de Cobranza Pagadas Totalizado | `VentasCobranzaPagadasTotalizadoZara` |
| 1170 | Reporte de Ventas Cobranza Desglosado | `VentasCobranzaDesglosado` |
| 1172 | Reporte de Ventas Cobranza Zaragoza | `VentasCobranzaZaragoza` |
| 1175 | Simular de Remision | `Simular` |
| 1180 | Folios (SIN ASIGNAR) | `VentasFoliosSinAsignar` |
| 1185 | Concentrados Detalle | `ConcentradosDetalle` |
| 1190 | Inasistencias | `Inasistencias` |
| 1201 | Remision Sin Precio | `RemisionSinPrecio` |
| 1210 | Remision Centro Servicio | `RemisionCentroServicio` |
| 1220 | Garantias Enviadas al Proveedor | `GarantiasEnviadasProveedor` |
| 1222 | Garantias Devueltas al Cliente | `GarantiasDevueltasCliente` |
| 1230 | Gasolinas Detallado | `GasolinasDetallado` |
| 1231 | Gasolinas Mensual | `GasolinasMensual` |
| 1235 | Garantias | `Garantias` |
| 1236 | Garantias Registradas | `GarantiasRegistradas` |
| 1240 | Listas Precios Zaragoza | `ListasPreciosZaragoza` |
| 1241 | Listas Precios Tauro | `ListasPreciosTauro` |
| 1242 | Listas Precios Costo | `ListasPreciosCosto` |
| 1243 | Listas Precios Personalizado Tauro - Todas | `ListasPreciosPersonalizadoTauroTodas` |
| 1244 | Listas Precios Personalizado Tauro - Todas Menos Tauro | `ListasPreciosPersonalizadoTauroTodasMenosTauro` |
| 1245 | Listas Precios Productos Zaragoza | `ListasPreciosProductosZaragoza` |
| 1246 | Listas Precios Personalizado Tauro  - CRONOS,  GOHNER Y XTREME,  LTH      CALE, AMERICA, FP, DYNAMO | `ListasPreciosPersonalizadoTauro_FP_CALE_DY_LTH_AM_CRO_GON_XT` |
| 1247 | Listas Precios Personalizado Tauro - FP, AMERICA, LTH | `ListasPreciosPersonalizadoTauro_FP_AMERICA_LTH` |
| 1250 | Control Acumulado de Estimaciones | `ControlAcumuladoEstimaciones` |
| 1251 | Caratula de Estimacion | `CaratulaEstimacion` |
| 1255 | Comprobante de Movimiento de Cascos Zaragoza | `ComprobanteMovimientoCascosZaragoza` |
| 1256 | Venta de Cascos | `VentaCascos` |
| 1260 | Comprobante de Movimiento de Cascos Tauro (DIARIO) | `ComprobanteMovimientoCascosDiarioTauro` |
| 1265 | Comprobante de Movimiento de Cascos Tauro | `ComprobanteMovimientoCascosTauro` |
| 1300 | Autos | `Autos` |
| 1305 | Ventas Cascos Excedentes | `VentasCascosExcedentes` |
| 1310 | Impresion de Factura Electronica Custom Tauro y Zaragoza | `FacturaElectronicaCustomTauZara` |
| 1315 | Corte Diario | `CorteDiario` |
| 1317 | Corte Diario Detalle MAC | `CorteDiarioDetalleMAC` |
| 1320 | Cortes | `Cortes` |
| 1325 | Movimientos Almacen | `MovimientosAlmacen` |
| 1330 | Gastos Detallado | `GastosDetallado` |
| 1335 | Gastos Totalizado | `GastosTotalizado` |
| 1340 | MC Constancia Contratista | `MC_ConstanciaContratista` |
| 1345 | Corte CS | `CorteCS` |
| 1350 | Productos Movimientos | `ProductosMovimientos` |
| 1355 | Ventas Cobros Dinero y Cascos | `VentasCobrosDineroCascos` |
| 1360 | Ventas Cobros Dinero | `VentasCobrosDinero` |
| 1365 | Ventas Cobros Cascos | `VentasCobrosCascos` |
| 1370 | Estado de Cuenta | `VentasEstadoCuenta` |
| 1375 | Estado de Cuenta DINERO | `VentasEstadoCuentaDinero` |
| 1380 | Estado de Cuenta CASCOS | `VentasEstadoCuentaCascos` |
| 1385 | Aviacion Comprobante de Pago | `AviacionComprobantePago` |
| 1386 | Aviacion Comprobante de Documento | `AviacionComprobanteDocumento` |
| 1390 | Aviacion Recaudaciones / Amortizaciones / Inscripciones | `AviacionRecaAmortInscrip` |
| 1395 | AccesosMac | `AccesosMac` |
| 1400 | Aviacion Comprobante de Devolucion Fianza | `AviacionComprobanteDevolucionFianza` |
| 1405 | Transferencias Clientes | `TransferenciasClientes` |
| 1406 | Transferencias Estatus | `TransferenciasEstatus` |
| 1407 | Transferencias Folios | `TransferenciasFolios` |
| 1410 | Centros Remisiones | `CentrosRemisiones` |
| 1415 | Centros Pagos Entradas y Salidas | `CentrosPagosEntradasSalidas` |
| 1420 | Centros Garantias | `CentrosGarantias` |
| 1425 | Aviacion Recaudacion | `AviacionRecaudacion` |
| 1426 | Aviacion Recaudacion Consolidada | `AviacionRecaudacionConsolidada` |
| 1428 | Aviacion Recaudacion Depositos | `AviacionRecaudacionDepositos` |
| 1429 | Aviacion Recaudacion Auxiliar | `AviacionRecaudacionAuxiliar` |
| 1430 | Aviacion Fianza | `AviacionFianza` |
| 1432 | Aviacion Tarjetas | `AviacionRecaudacionTarjetas` |
| 1433 | Aviacion Tarjetas Por Economico | `AviacionRecaudacionTarjetasPorEconomico` |
| 1434 | Aviacion Tarjetas Por Dia | `AviacionRecaudacionTarjetasPorDia` |
| 1435 | Comprobacion Productos | `ComprobacionProductos` |
| 1440 | Clientes Facturas RFC | `ClientesFacturasRFC` |
| 1445 | Centro de Servicio Completo | `CentrosCompleto` |
| 1450 | Aviacion Distribucion Tarjeta | `AviacionDistribucionTarjeta` |
| 1455 | Aviacion Ingresos Contabilidad | `AviacionIngresosContabilidad` |
| 1460 | Aviacion Auxiliar de Mayor | `AviacionAuxiliarMayor` |
| 1465 | Hoja de Cobro Total Cobrado | `HojaCobroTotalCobrado` |
| 1470 | Hoja de Cobro Total Cascos | `HojaCobroTotalCascos` |
| 1475 | Ventas de MotoBaterias Por Cliente | `VentasMotoBateriasPorCliente` |
| 1480 | Ventas de MotoBaterias Por Folio | `VentasMotoBateriasPorFolio` |
| 1485 | Ventas de MotoBaterias Por Codigo | `VentasMotoBateriasPorCodigo` |
| 1490 | Ventas de MotoBaterias Por Marca | `VentasMotoBateriasPorMarca` |
| 1495 | Hoja de Cobro Cheques | `HojaCobroCheques` |
| 1500 | Aviacion Operadores | `AviacionOperadores` |
| 1505 | Aviacion Balanza de Comprobacion | `AviacionBalanzaComprobacion` |
| 1510 | Aviacion Balanza de Comprobacion Por Economico | `AviacionBalanzaComprobacionEconomico` |
| 1515 | Aviacion Poliza de Cheques | `AviacionPolizaCheques` |
| 1520 | Aviacion Poliza de Cheques Excel | `AviacionPolizaChequesExcel` |
| 1525 | Aviacion Economicos Excel | `AviacionEconomicosExcel` |
| 1530 | Aviacion Poliza de Ingresos Excel | `AviacionPolizaIngresosExcel` |

### Enums auxiliares del mismo archivo

- `enumPresentacion`: `0 Pantalla`, `1 Impresora`, `2 Pdf`, `3 Excel`, `4 Xml`, `5 Zip`.
  Es el `p` / `MostrarReporte` de la URL.
- `enumFormatoPresentacion`: `0 Todo`, `1 Excel`. **No se usa en ningun lado del proyecto.**
- `enumReportesFormatoSaldos`: `0 Totalizado`, `1 Detallado`. **Tampoco se usa.**

### Los 96 reportes con salida Excel (xlsx) en V2

Son los `case` del `switch` de `ReporteService.ConsultarV2`, cada uno con su DTO fuertemente tipado.
Si `Presentacion == Excel` y el ID **no** esta en esta lista, el `switch` no hace nada, `dtReporte`
queda vacio y el usuario recibe el PDF de "sin datos" — **silenciosamente**.

10, 15, 19, 20, 21, 22, 23, 24, 71, 73, 74, 75, 77, 103, 105, 106, 110, 112, 113, 114, 116, 118, 119,
120, 125, 130, 140, 145, 150, 205, 210, 212, 220, 225, 227, 230, 235, 240, 493, 1017, 1026, 1027,
1135, 1140, 1141, 1160, 1161, 1162, 1166, 1170, 1185, 1190, 1220, 1222, 1230, 1231, 1235, 1236,
1240, 1241, 1243, 1245, 1300, 1305, 1320, 1325, 1330, 1335, 1395, 1405, 1406, 1407, 1425, 1426,
1428, 1429, 1430, 1432, 1433, 1434, 1435, 1440, 1445, 1450, 1455, 1465, 1470, 1475, 1480, 1485,
1490, 1495, 1500, 1520, 1525, 1530.

Hay un `case` comentado para `VentasFoliosGlobalTotalizado`, que **ni existe en el enum**.

Los DTO de esas 96 filas estan todos en un solo archivo:
`Models/Reportes/ReporteResponse.cs` (clases `Rep*DTO`). **Ahi esta el esquema de columnas exacto
que espera cada xlsx** — es la fuente mas fiable para saber que devuelve cada SP.

---

## 3. Recursos fisicos que hay que llevarse

| Recurso | Ruta | Hace falta? |
|---|---|---|
| **wkhtmltopdf.exe** (22.5 MB) | `Legacy/MacReportes2/MacReportes/App_Data/wkhtmltopdf/wkhtmltopdf.exe` | **SI, es el motor del 100% de los PDF de V2 y de 19 de V1.** |
| 19 DLLs del runtime VC++ que lo acompanan | `App_Data/wkhtmltopdf/*.dll` (`msvcp140.dll`, `ucrtbase.dll`, `vcruntime140.dll`, `concrt140.dll`, 15 `api-ms-win-crt-*.dll`) | SI, si se sigue usando wkhtmltopdf. |
| **NReco.PdfGenerator.dll 1.1.14.0** | `Legacy/MacReportes2/MacReportes/bin/NReco.PdfGenerator.dll` | **SI y con cuidado: no esta en NuGet en el repo, solo este binario.** Verificar licencia antes de reusarlo. |
| `MacReportes.xml` (1 linea: URL del backend) | `Legacy/MacReportes2/MacReportes/MacReportes.xml` | Como referencia de configuracion; en el mundo nuevo es una entrada de `appsettings`. |
| `imagenes/cancelada.png` | `Legacy/MacReportes2/MacReportes/imagenes/cancelada.png` | **`?`** No se referencia desde el codigo. Llevarselo por si el HTML del backend lo pide. |
| Fuentes `glyphicons-halflings-regular.{eot,svg,ttf,woff,woff2}` | `Legacy/MacReportes2/MacReportes/fonts/` | **NO.** Son de Bootstrap 3 y solo las usa la pantalla de error. |
| Bootstrap 3.4.1 / jQuery 3.4.1 / Modernizr / `Content/Site.css` | `Content/`, `Scripts/` | **NO.** Solo visten la pantalla de error. |
| **Logos de empresa** | **NO estan en el repo.** Viven en disco del servidor, en `Constantes.PathImagenes` + `Constantes.LogoMacReportes` (y `LogoMacWM`), tipicamente `C:\Mac21\imagenes\`. | **SI, pero hay que sacarlos del servidor de cada empresa.** Consultar `SELECT TOP 1 PathImagenes, LogoMacReportes FROM dbo.Constantes` en cada base. |
| **Plantillas HTML de los reportes** | **NO estan aqui.** `Legacy/MacServicios2/Mac3Servicios/Models/Service/ReportesServ/` y `.../ReportesV2Serv/` | SI, pero son del otro inventario. |
| Fuentes tipograficas reales de los PDF | `?` — dependen del Windows donde corre wkhtmltopdf y del `font-family` de las plantillas del backend. | Hay que determinarlo leyendo el CSS de las plantillas. |
| Basura del repo que **no** hay que llevarse | `MyFile.pdf`, `tauro.pdf` (PDFs de prueba en la raiz), `debug.log`, `.vs/`, `obj/`, `bin/IronPdf.dll`, `bin/Spire.License.dll`, `Publish/` | NO. |

---

## 4. Logica de negocio que esta en C# y NO en los stored procedures

Es poca, pero toda importa porque si se omite el PDF sale distinto.

1. **El reparto de motor de PDF por ID de reporte** (`ReportesController.cs`). Una lista blanca de
   19 IDs usa wkhtmltopdf; el resto usa PdfSharp. **Los dos motores renderizan CSS distinto**, asi
   que mover un reporte de rama cambia su aspecto. Esta regla no esta en ninguna tabla ni SP: es un
   `if` con 19 comparaciones y ~25 lineas de comentarios de IDs que ya no existen en el enum
   (`VentasPagosChequesGlobalAgentes`, `ClientesConDireccionAgente`, `VentasFoliosPagados`, etc.).

2. **Los margenes fijos de la rama wkhtmltopdf**: `Top=4, Bottom=6, Left=2, Right=8`. Pisan lo que
   manda el backend. Ya se replico en el codigo nuevo
   (`sacNet-8/.../Application/Services/RemisionImpresionService.cs:26`).

3. **El bug de margen de la rama PdfSharp**: `config.MarginTop = reporteDTO.marginBottom`. Si se
   "arregla" al migrar, los reportes de esa rama cambian de paginacion.

4. **El `String.Format(html, DateTime.Now)`**. V1, recibos y VentasHtml aplican `String.Format` al
   HTML del backend, asi que **la plantilla usa `{0}` como marcador de fecha/hora de impresion** —
   y cualquier `{` o `}` literal en el HTML revienta. `ReportesV2Controller.MostrarPdf` **NO** lo
   hace (lo tiene comentado), asi que V1 y V2 no se comportan igual.

5. **El nombre del archivo descargado**: `tauro_<Folio>_<NombreCompleto truncado a 20>.pdf` con
   `Regex.Replace("[^0-9a-zA-Z]+", "_")`. El prefijo `tauro_` es literal para las tres empresas.
   Ya replicado en `sacNet-8/.../Application/Services/RemisionImpresionService.cs:183`.

6. **El parseo de parametros de la URL**, que es la parte mas fragil:
   - V1: `StoredProcParam` -> `@` se cambia por espacio, **y `~` se cambia por `@@`**;
     `ParameterFields` -> `@` se cambia por espacio.
   - V2: `psp` -> `@` por espacio. **Y si `psp` es un entero puro, se interpreta como el ID de una
     fila de la tabla `Parametros` del backend y `idr`/`idu`/`pcad`/`p` NO se leen de la URL.**
     Esa bifurcacion es pura logica C# (`int.TryParse`) y es facil de perder al migrar.
   - IDs de venta: lista separada por `~` (`ids` en V1, `idV` en VentasHtml).
   - Los delimitadores estan declarados en `Variables` (`|`, `[`, `~`, `@`) pero **solo se usa el `~`**.

7. **El parseo a mano del JSON de respuesta** (`Utils/Funciones.cs`). En vez de deserializar, busca
   substrings: `"IdentificadorReporte":`, `"PresentacionReporte":`, `"nombreReporte":"`, `"html":"`,
   `"result":{"`, y despues hace `.Replace("\\r","").Replace("\\n","").Replace("\\t","")` y
   `.Replace("\\\"","")` sobre el HTML. **De ahi salen el IDReporte y la Presentacion reales que usa
   V2 para decidir el formato de salida.** Es fragil y dependiente del orden de los campos del JSON.

8. **La construccion del xlsx a mano** (`Utils/OfficeOpenXML.cs`, DocumentFormat.OpenXml 2.13.0,
   singleton `Lazy<T>`): primera fila = nombres de columna del `DataTable`, mapeo de tipo .NET a
   `CellValues` (`Format(Type)`). **Sin estilos, sin formatos de numero, sin anchos de columna, sin
   fechas formateadas** — las fechas se escriben como `CellValues.Date` con el `ToString()` por
   defecto, lo que suele salir mal en Excel. Nombre del archivo:
   `<nombreReporte sanitizado>_ddMMyyyy_HHmmss.xlsx`.

9. **La conversion de `List<T>` a `DataTable`** (`Funciones.ListToDataTable<T>`): el **orden de las
   propiedades en el DTO define el orden de las columnas del Excel**. Reordenar un DTO al migrar
   cambia el Excel que recibe el usuario.

10. **`maxLineas` del `ReporteDTO` se recibe y nunca se usa.** La paginacion la decide el motor.

11. **Fallback de Excel sin datos**: si `Presentacion == Excel` pero `dtReporte.Rows.Count == 0`, V2
    **no devuelve un xlsx vacio**: cae al camino de PDF con el HTML de "sin datos".

### Codigo muerto, duplicado y riesgos

- `ReportesController.GeneraExcel` — cuerpo entero comentado, devuelve `DataTable` vacio.
- `ReportesController.ConsultaReporte` — comentado completo.
- `VentasImprimirService.ConsultarSimular` (publico y privado) — **nunca se llama**; el endpoint
  `api/ventashtml/simular` queda sin usar. La simulacion (1175) pasa por `Consultar`, igual que 1001.
- `VentasImprimirService.Consultar` crea **una segunda instancia de si mismo** para llamar al
  overload privado, en lugar de llamarlo directo.
- `Views/VentasImprimir/Index.cshtml` existe **sin controlador** (`VentasImprimirController` no esta
  en el `.csproj` ni en el disco): `/VentasImprimir/Index` da 404.
- `Servicios/Nomina/Empleado/EmpleadoService.cs` y
  `Servicios/Nomina/EmpleadoPeriodoTipoSueldoAsistencia/...Service.cs` existen pero **ningun
  controlador los usa**.
- `Models/Reporte/` y `Models/Reportes/` coexisten; **solo `Models/Reportes/` esta en el `.csproj`**.
- **Carpeta duplicada y desactualizada**: `Legacy/MacReportes2/MacReportes/MacReportes/` es una copia
  completa y **vieja** del proyecto (su `enumReportes.cs` pesa 8.6 KB contra los 15.1 KB del bueno y
  **le faltan al menos 11 reportes**: 19, 73, 74, 77, 106, 114, 116, 118, 119, 212...; y su
  `ReportesV2Controller` tiene el manejo de Zip comentado). **La version viva es la de arriba**, la
  que apunta `MacReportes.sln`. No migrar la de adentro.
- **Secretos en el repo**: el JWT y el correo `app_web@tauro.com` estan en `Utils/Variables.cs`.
- **Sin autenticacion ni autorizacion** en ninguna accion.
- Los helpers `MostrarPdf` / `DownloadReport` son `public` y por eso MVC los expone como acciones;
  no se pueden invocar de forma util por URL (parametros no primitivos), pero convendria
  `[NonAction]`.

---

## 5. Donde ramifica por empresa, y por que

**En el codigo C# de MacReportes no hay ni un `if` por empresa.** No existe la palabra Tauro,
Zaragoza ni Aviacion en una condicion. La ramificacion es de otras tres formas:

1. **Por instancia desplegada (la de verdad).** Cada empresa corre **su propio MacReportes en IIS**,
   y lo unico que las distingue es la linea de `MacReportes.xml`, que apunta al backend de esa
   empresa (`Mac3Servicios` = Tauro, `MacZServicios` = Zaragoza, etc. — comentados en
   `Variables.cs`). De ahi cuelga todo: base distinta, `Constantes` distintas, logo distinto,
   `PathDescargar` distinto. **Esto encaja con la regla de que cada empresa tiene su base.**

2. **Por tabla `Constantes` de cada base**, leida en el backend: `PathImagenes`, `LogoMacReportes`,
   `LogoMacWM`, `PathDescargar`. Es lo que hace que la misma remision salga con el logo de Tauro o de
   Zaragoza sin que MacReportes lo sepa.

3. **Por numero de reporte distinto para la misma capacidad.** La empresa esta **codificada en el
   ID**, no en una condicion. Pares y familias que hay que respetar al migrar:
   - Listas de precios: `1240 Zaragoza` / `1241 Tauro` / `1242 Costo` /
     `1243, 1244, 1246, 1247` personalizadas de Tauro / `1245 Productos Zaragoza`.
   - Cobranza: `1160 VentasCobranzaDetallado` vs `1161 VentasCobranzaDetalladoZara`;
     `1162` y `1166` son exclusivos de Zaragoza; `1172 VentasCobranzaZaragoza`.
   - Asistencias: `1025 Asistencias` vs `1026 AsistenciasZaragoza`.
   - Facturas: `1003 FacturaElectronica`, `1007 FacturaZaragoza`,
     `1310 FacturaElectronicaCustomTauZara`.
   - Cascos: `1255 ComprobanteMovimientoCascosZaragoza` vs `1260`/`1265` de Tauro.
   - Pagos: `71 VentasPagosDetallado` vs `77 VentasPagosDetalladoTauro`;
     `72`/`76` son las variantes "CS" (centro de servicio).
   - **Aviacion es practicamente un modulo aparte**: 24 reportes, del `1385` al `1530`
     (comprobantes, recaudaciones, fianzas, tarjetas, balanzas, polizas, auxiliar de mayor).
   - Esto **confirma** la nota de memoria de que Zaragoza imprime otro reporte y no la remision de
     Tauro con otro logo.
   **Donde se decide cual numero se manda: `?`** No esta en MacReportes. Lo arma quien construye la
   URL — el front de Mac31 o el web.

4. **Lo que NO ramifica y deberia**: el prefijo `tauro_` del nombre del PDF descargado es literal
   para las tres empresas.

---

## 6. Resumen para la migracion

- MacReportes es **una capa fina**: ~1.900 lineas utiles, 4 controladores, 6 metodos reales.
  Lo unico irremplazable que aporta es **HTML -> PDF con wkhtmltopdf** y el **xlsx con OpenXml**.
- **El trabajo pesado de la migracion no esta aqui**: esta en `Mac3Servicios`, que es quien tiene
  las plantillas, los SP y la logica de los 151 reportes. Este inventario deja claro que ese es el
  siguiente paso obligatorio.
- Al reemplazarlo hay que preservar: los margenes 4/6/2/8, el `{0}` de la fecha, el nombre
  `tauro_<folio>_<cliente>.pdf`, el `~` como separador de IDs, la bifurcacion de `psp` entero, el
  orden de columnas de los DTO de Excel, y el reparto de motor por ID (o aceptar que algunos
  reportes cambien de aspecto y validarlos uno por uno).
- Pendientes de averiguar (marcados `?` en el TSV y arriba): que usa `imagenes/cancelada.png`, que
  fuentes tipograficas asumen las plantillas, el mapeo reporte -> stored procedure, y quien decide
  el numero de reporte por empresa.
