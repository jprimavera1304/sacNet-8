# Capacidades de empresa — el mapa que el front tiene que leer antes de dibujar nada

Este documento es para los agentes que construyan módulos del frontend de MAC. Si vas a hacer una
pantalla, lee al menos las dos primeras secciones y la tabla de capacidades de tu módulo.

**El problema que resuelve:** Legacy no es un programa, son varias configuraciones de un programa.
Mac31 lee `dbo.Constantes` al arrancar (`Legacy/Mac31/Mac31/Utils/Globales.cs:439-576`) y con esos
valores decide columnas, botones, reglas de negocio y números de reporte. El inventario contó **430
ramas** por seis de esos valores. El web y el móvil **no recibían ninguno**, así que cualquier
pantalla nueva solo podía implementar **una** de las dos empresas, y el fallo no aparecía en pruebas:
aparecía en producción, en la otra empresa.

Ya pasó una vez, medido: el nodo "Cobranza" de reportes es el **1160** en Tauro y el **1161** en
Zaragoza. Mac31 manda el 1160 en las dos y el servicio viejo lo cambia
(`Legacy/MacServicios2/.../ReporteV2Service.cs:783`). Midiendo contra Zaragoza, el backend nuevo
imprimía 1 298 importes contra los 6 734 de Legacy. El dato siempre estuvo bien; se estaba vaciando
en el molde equivocado. Quedan 430 ramas más.

---

## 1. De dónde saca el front las capacidades

### Al iniciar sesión: ya vienen en `/api/me`

```http
GET /api/me
Authorization: Bearer <token>
```

```jsonc
{
  "userId": "...", "idUsuario": 0, "usuario": "BancoPruebas",
  "rolLegacy": "SuperAdmin", "permissionsEnabled": true, "permissions": [ ... ],
  "companyKey": "tauro", "empresaId": 1, "debeCambiarContrasena": false,

  "empresa": {                 // <-- ESTO ES LO NUEVO
    "instalacion":      { "servidor": "...", "baseDatos": "...", "nombreCentroServicio": "" },
    "discriminadores":  { "funcionalidad": "TAU", ... },
    "capacidades":      { "facturacionProrrateaImporte": true, ... },
    "parametros":       { "porcentajeIva": 0.16, ... },
    "fechas":           { "operacionVentas": "2026-10-07", ... }
  }
}
```

Va dentro de `/api/me` **a propósito**: es la única llamada que los dos fronts ya hacen justo después
de autenticar y la única que ya cachean (`authz.cache.<userId>` en móvil, la sesión en el web). Un
bloque propio habría sumado un tercer round-trip al arranque y un tercer sitio donde preguntar lo
mismo.

Dónde engancharlo:

| | archivo | qué |
|---|---|---|
| Web | `Web/QR_Web/docs2/src/nucleo/permisos.ts` | tipo `Autorizacion` (líneas 32-38), parseo en `pedirAutorizacion` (74-91), `VACIA` (40-45) |
| Web | `Web/QR_Web/docs2/src/nucleo/sesion.ts` | si se persiste, llave nueva en `LLAVES_SESION` (línea 31) para que `limpiarSesion()` la borre sola |
| Móvil | `Movil/mac-mac-mobile/shared_mobile/lib/core/auth/authz_service.dart` | campo en `CurrentUserAuthz` (6-28), parseo en `fromJson` (91-107), default en `fromSession` (67-78) |

### Para volver a leerlas: endpoint propio

```http
GET /api/empresa/capacidades
Authorization: Bearer <token>
→ { "ok": true, "message": "Capacidades de la empresa.", "data": { ...el mismo objeto... } }
```

Úsalo después de mover la fecha de operación, al volver de segundo plano, o en la pantalla de
configuración. No exige permiso, igual que `/api/me`: sin esto el front no sabe qué dibujar, y dentro
no hay nada sensible.

**Ojo con el nombre:** `/api/capacidades` (sin `empresa`) **ya existía y es otra cosa**: son las
capacidades de **permiso del usuario**. Son dos ejes distintos y se combinan:

> una acción se dibuja si **la empresa la tiene** `&&` **el usuario tiene permiso**.

### Las dos reglas de oro

1. **Pregunta por la capacidad, no por la empresa.** `if (empresa.capacidades.facturacionProrrateaImporte)`,
   nunca `if (funcionalidad === "TAU")`. Esa es la diferencia entre un sistema configurable y uno
   lleno de `if empresa == ...`.
2. **Si `empresa` viene `null`, no asumas `false`.** Es null solo cuando la lectura de
   `dbo.Constantes` falló, y eso NO significa "esta empresa no puede". Trátalo como "todavía no sé":
   no dibujes reglas por empresa, enseña un reintento. Asumir `false` apagaría módulos enteros por un
   error de red.

---

## 2. Los discriminadores crudos, y cuándo usarlos

`empresa.discriminadores` trae los valores tal como están en la base. **No son la vía normal**: son la
salida de emergencia para una rama que descubras y que todavía no tenga capacidad propia. Si usas uno,
abre la petición de que se le ponga nombre: cada `if` sobre un discriminador crudo es deuda.

| campo | columna | Tauro | Zaragoza | sitios que ramifican en Legacy |
|---|---|---|---|---:|
| `funcionalidad` | `Funcionalidad` | `"TAU"` | `"ZARA"` | **200** |
| `esquemaPago` | `EsquemaPago` | `"TAU"` | `"ZARA"` | 1 en el cliente, 57 contando servidor y SP |
| `esCentroServicio` | `EsCentroServicio` | `false` | `false` | **162** |
| `esAlcaldiaMC` | `EsAlcaldiaMC` | `0` | `0` | 21 |
| `esLuro` | `EsLuro` | `false` | `false` | 11 |
| `esAviacion` | `EsAviacion` | `false` | `false` | 3 |
| `esAviacionServidor` | `EsAviacionServer` | `false` | `false` | solo dentro de SP |
| `aviacionIdCaja` | `AviacionIDCaja` | `0` | `0` | 1, pero apaga el módulo de reportes |
| `inventarioNegativo` | `InventarioNegativo` | `false` | **`true`** | 2 |

Cuatro cosas que no se ven en la tabla y muerden:

- **`esAlcaldiaMC` no es un booleano.** Vale `0`, `1`, `2` o `3`, y es un **selector de producto**, no
  una bandera. Con 1/2/3 Legacy apaga casi todo el ERP (`Menu.cs:176-235`), y con `2` **el mismo botón
  abre otro formulario** (`Menu.cs:2107` → `MCConsultarProveedores`, `:2133` →
  `ConsultarProveedoresGam`). Compararlo con `> 0` pierde esa distinción.
- **`esCentroServicio` vale 0 en las dos bases que existen.** Un centro de servicio es una **tercera
  clase de instalación** (bases `MacZCS*`, hoy no restauradas). Las 162 ramas detrás de esa bandera
  **no se pueden comprobar** contra ninguna de las dos empresas en operación. Impleméntalas desde el
  código de Legacy y márcalas como no verificadas.
- **`esLuro` es del lado Zaragoza y `MacL` no está restaurada** (`Constantes.BDLuro` vale
  `'[MacL].[dbo].'` en `MacZ` y vacío en Tauro).
- **Legacy mezcla dos formas de comparar** `funcionalidad`: `== "ZARA"` (31 sitios) y
  `.ToUpper().Contains("ZARA")` (el resto). El backend copia la que usa cada sitio; tú no tienes que
  pensarlo porque ya viene resuelto en `capacidades`. Lo que sí importa: **nunca compares el string tú
  mismo**, porque elegirías una de las dos y no sabes cuál toca.

---

## 3. Las capacidades, una por una

`empresa.capacidades`. Cada fila dice qué cambia en Legacy, con archivo y línea, y qué hace el front.

### 3.1 Garantías — **el flujo va al revés en cada empresa**

Esta es la trampa más peligrosa de todo el inventario, porque **no cambia la presentación: cambia el
orden del proceso.** `ConsultarGarantias.cs` tiene dos bloques espejados.

| capacidad | Zaragoza | Tauro |
|---|---|---|
| `garantiaEnvioAProveedorEsElUltimoPaso` | **`true`** | `false` |
| `garantiaExigeEnvioAProveedorPrimero` | `false` | **`true`** |
| `garantiaPermiteCambiarProductoAlEditar` | **`true`** | `false` |

```csharp
// ConsultarGarantias.cs:1836-1851  (ZARA)
if (Variables.funcionalidad.ToUpper().Contains("ZARA"))
    if (IDStatusGarantia == EnviadaProveedor)
        -> "YA FUE ENVIADA AL PROVEEDOR."  + return

// ConsultarGarantias.cs:1854-1868  (TAU) — la condición OPUESTA
if (Variables.funcionalidad.ToUpper().Contains("TAU"))
    if (EnviadaProveedor == "NO")
        -> "PRIMERO TIENE QUE SER ENVIADO AL PROVEEDOR." + return
```

Y al pulsar "enviar a proveedor":

```csharp
// :2154  ZARA -> solo se pueden enviar las REMISIONADAS
// :2170  TAU  -> solo se pueden enviar las REGISTRADAS
```

Leído junto:

- **Zaragoza:** `Registrada → Remisionada → EnviadaProveedor`. Mandar al proveedor es el **último**
  paso y **bloquea todo lo demás**: garantía (`:1840`), ajuste (`:2053`), devolver al cliente
  (`:2269`), remisionar (`:2374`) y agregar a seleccionados (`:4227`).
- **Tauro:** `Registrada → EnviadaProveedor → Remisionada/Ajuste`. Mandar al proveedor es el **primer**
  paso y es **requisito** para garantía (`:1857`) y ajuste (`:2072`). Los otros frenos no existen.

**Qué hace el front:** el orden de los pasos del asistente, qué botones están vivos en cada estatus y
el texto del bloqueo salen de estas dos capacidades. Si implementas una sola de las dos reglas, rompes
la otra empresa **en silencio**, y el síntoma que llega es "el botón no me deja" sin ninguna pista.

**La tercera, `garantiaPermiteCambiarProductoAlEditar`, es otro eje y no el mismo.** Las dos de arriba
hablan del **orden** de los pasos; ésta del **permiso de edición**: al abrir una garantía ya
registrada, si es `true` vuelven a quedar habilitados la clave, el buscador de producto, el almacén
del producto y la casilla de "clave otra" (`Garantia.cs:213` y `:217-220`); si es `false`, esos cuatro
controles se quedan apagados y sólo se puede tocar diagnóstico, ajuste y motivo.

Y se compara con **igualdad exacta**: `Garantia.cs:213` dice `Variables.funcionalidad == "ZARA"`, no
`.Contains("ZARA")` como las otras diez ramas del mismo archivo. Por eso no se cuelga de
`garantiaEnvioAProveedorEsElUltimoPaso` aunque hoy las dos valgan `true` en Zaragoza: un **centro de
servicio de Zaragoza** mantiene el orden de ZARA (`true`) y ya **no** puede cambiar el producto
(`false`), y amarrarlas le daría permiso de reescribir qué pieza se garantizó después de recibirla.

> Extra que no es capacidad: `ConsultarGarantias.cs:1887` — en Tauro los usuarios **22, 40, 1 y 3** se
> saltan la validación de monto < 300. Son cuatro ids escritos en el código; el backend no los publica
> porque son una excepción personal, no una regla de empresa.

### 3.2 Facturación — **otra aritmética, no otra columna**

| capacidad | Zaragoza | Tauro |
|---|---|---|
| `facturacionProrrateaImporte` | `false` | **`true`** |
| `facturacionMuestraPreciosConIva` | **`true`** | `false` |
| `permiteNotaDeCredito` | `true` | **`false`** |
| `permiteComplementoDePago` | `true` | **`false`** |

```csharp
// VentasFacturacion.cs:1409-1418
if (Variables.funcionalidad.ToUpper().Contains("ZARA"))
{
    PrecioListaConIvaRnd   = gridDetalles[i]["FacturaPrecioListaConIva"];
    SubTotalConUsadoConIva = gridDetalles[i]["SubTotalConUsadoConIva"];
}
else
{
    PrecioListaConIvaRnd   = gridDetalles[i]["precioLista"];
    SubTotalConUsadoConIva = (gridDetalles[i]["ImportePartida"] / cantidadOriginal) * Cantidad;
}
```

- **Zaragoza** toma dos columnas **ya calculadas con IVA**, específicas de facturación.
- **Tauro** toma `precioLista` (la columna genérica, **no** la de facturación) y **prorratea**:
  `ImportePartida / cantidadOriginal * Cantidad`.

Esos dos valores alimentan el DTO de timbrado (`:1429-1430`) y el acumulado
`SumaSubTotalSinUsadoConIvaRnd += PrecioListaConIvaRnd * Cantidad` (`:1432`). **No es una columna
distinta: es otra aritmética**, con otro redondeo por partida. Una pantalla hecha para una empresa no
cuadra en la otra, y no cuadra por centavos, que es la forma más difícil de detectar.

Arrastra la presentación completa:

| evidencia | Zaragoza | Tauro |
|---|---|---|
| `VentasFacturacion.cs:1040`/`:1053` | **"PRECIO C/IVA"** = `FacturaPrecioListaConIva` | **"PRECIO"** = `precioLista` |
| `:1077`/`:1090` | **"IMPORTE C/IVA"** = `FacturaSubtotalConIva` | **"IMPORTE"** = `ImportePartida` |
| `:966` | reordena `FacturaPrecioListaConIva`(3), `cantidadMostrarFtm`(4), `FacturaSubtotalConIva`(5) | — |
| `:716`, `:812`, `:2470` | — | reordena `ImporteZ`(3) y `Cascos`(4); `columnaImporteSinCascos = "ImporteZ"` |
| `VentasNotaCredito.cs:2040-2049` | `importeConIvaRnd`, `PrecioListaConIvaRnd` | `importe`, `precioLista` |

Y `ConsultarVentasFacturas.cs:93-100` + `:1205-1210` ocultan en Tauro `rbNotasCredito`,
`rbComplementosPago`, `btnNuevaNotaCredito` y `btnNuevoComplementoPago`: **`VentasNotaCredito`
(3 556 líneas) y `VentasComplementoPago` (1 310) son pantallas de facto solo de Zaragoza.** No las
pongas en el menú de Tauro.

> Dos defectos de Legacy que conviene no copiar: la rama de Tauro divide por `cantidadOriginal` **sin
> guardia de cero**, y la validación del tope de descuento está **comentada** al facturar
> (`VentasFacturacion.cs:1529`) aunque sigue activa al vender (`Ventas.cs:5703`).

### 3.3 Ventas y cobranza

| capacidad | Zaragoza | Tauro | qué cambia, y dónde |
|---|---|---|---|
| `exigeHojaDeCobroParaPagar` | **`true`** | `false` | **En Zaragoza no se puede aplicar un pago ni un descuento sin una hoja de cobro abierta.** `VentasPago.cs:2389`/`:2401`, repetido en `ConsultarVentasPagosUsados.cs:1296`, `:2477`, `:3090`. **La condición es `ZARA && !esCentroServicio`**: si el front solo mira la funcionalidad, un centro de servicio de Zaragoza se queda sin poder cobrar |
| `creditoPorTresConceptos` | **`true`** | `false` | Zaragoza lleva **tres** condiciones de crédito (acumuladores, aceites, cascos) donde Tauro lleva una. `Ventas.cs:1584` (tres etiquetas), `:1666`/`:1705` (`"0/0/0"` contra `"0"`), `:2230` (manda tres campos en vez de `MaxDiasVencidos`), **`:2863`/`:2879` la regla de autorización del pedido** (Tauro: un contador > 0; Zaragoza: cualquiera de los tres), `Clientes.cs:1036` (dos combos **obligatorios**). Tauro además consulta `CreditoDisponible()` y muestra "PAGOS $x / 3 MESES" (`Ventas.cs:1628`, `:1642`, `:1649`) |
| `ventasMuestraColumnasDeSaldo` | `false` | **`true`** | Zaragoza **oculta** cargos (`ConsultarVentas.cs:1729`), abonos (`:1737`), descuentos (`:1745`) y saldo (`:1753`), ensancha `NombreCliente` a 340 px (`:1703`) y la columna de vencimiento dice **"ACU/CASC/ACE"** en vez de **"DIAS VENC"** (`:2060`/`:2073`) |
| `permiteCancelarRemisionFacturada` | **`true`** | `false` | `ConsultarVentas.cs:2936` y `ConsultarCobros.cs:2483` lo prohíben en Tauro |
| `usaConcentrados` | `false` | **`true`** | Al cancelar, Tauro pregunta si se quita el folio del **concentrado** y pide contraseña (`ConsultarVentas.cs:2945-2960`). "SOLO DEVUELTAS" solo existe en Tauro y `SinCambioVigencia` vale 1 en TAU / 0 en ZARA (`Concentrado.cs:136`/`:143`). AGREGAR y ELIMINAR piden contraseña solo en Tauro (`:384`, `:1207`) |
| `lubricantesYFiltrosVanPorAceites` | **`true`** | `false` | En Zaragoza **no se pueden agregar lubricantes ni filtros** a una venta normal: van por ACEITES (`Ventas.cs:1941-1999`). **Con excepción** para los clientes de `parametros.idsClienteServicioDomicilio` |
| `liquidaSaldosMenoresAutomaticamente` | **`true`** | `false` | En Zaragoza, al aplicar el depósito, **los saldos chicos se liquidan solos** con un pago tipo Descuento, hasta `parametros.zaraSaldoLiquidarDineroMonto` y `...CascosMonto` (`AsignarBancosTransferencias.cs:648-748`). En Tauro quedan vivos |
| `usaGalleros` | **`true`** | `false` | GALLEROS es catálogo y flujo **solo de Zaragoza** (`Menu.cs:141,150`). En Tauro `rbGalleros` se oculta (`ConsultarUsadosMovimientos.cs:46`) |
| `pagoDeCascosPorKilo` | `false` | **`true`** | `rbCascoKilo` solo existe en Tauro; en Zaragoza los costos G1..G8 quedan de solo lectura (`VentasPago.cs:204`/`:219`). El precio está en `parametros.precioCascoKilo` |
| `confirmaImpresoraAlImprimir` | `false` | **`true`** | Tauro abre `ConfirmacionImpresion` para elegir impresora y copias (`ConsultarVentas.cs:4380`); Zaragoza imprime directo con `Remision = 1001` sin preguntar (`:4409-4440`) |

### 3.4 Centro de servicio y Luro

Recuerda: **las dos bases en operación tienen `esCentroServicio = false`**. Estas capacidades no se
pueden comprobar hoy.

| capacidad | cuándo | qué cambia |
|---|---|---|
| `catalogosDeProductoSoloLectura` | `esCentroServicio \|\| esLuro` | Productos, categorías, grupos de categoría, marcas, tipos de usado y la relación grupo-marca **no tienen alta ni modificación**: `ConsultarProductos.cs:984`/`:1009`, `ConsultarCategorias.cs:322`/`:345`, `ConsultarGrupoCategorias.cs:281`/`:304`, `ConsultarMarcas.cs:283`/`:306`, `ConsultarTipoUsados.cs:355`/`:376`, `RelacionGrupoCategoriaMarcas.cs:283`. Mensaje de Legacy: *"NO TIENE PERMITIDO ALTA O MODIFICACION DE ..."*. Aparte, `Menu.cs` corta el acceso **antes de abrir** varios catálogos (líneas 611, 620, 653, 689, 725, 760, 1056, 1092) con otro mensaje: *"NO SE PUEDEN MODIFICAR LOS CATALOGOS DESDE EL CENTRO DE SERVICIO."* |
| `facturaANombreDeEmpresaMayorista` | `esCentroServicio` | El `IDEmpresa` que se timbra es `IDEmpresaMayorista`, no la del usuario (`VentasFacturacion.cs:1633`, `:2095`), y **no se puede cambiar a quién se factura**: se ocultan `btnRFC` y `btnBuscarCliente` (`:292`, `:512`). Aparece y se calcula `txtTotalConComision` (`:1643`, `:1768`, `:4251`) |
| `corteDelDiaPorRetiros` | `esCentroServicio` | Un solo botón del menú abre **dos pantallas distintas** (`Menu.cs:2516` contra `:2544`): `CorteDiario` cuadra la caja de oficina (SAE + cobranza MAC + gastos, 7 botones) y `CorteDiarioCS` cuadra **retiros por forma de pago** (matriz 6×4, 3 botones, **sin gastos**). No es la misma pantalla con una columna de más |
| `buscaClientePorNumeroExacto` | `esCentroServicio` | La búsqueda se fuerza a `Identico=1`: **solo por número completo** (`ConsultarVentas.cs:978`, `ConsultarVentasFacturas.cs:793`). Además la pantalla arranca forzada en la pestaña **Emitidas** (`:1201`) |
| `remisionUsaReporteDeCentroDeServicio` | `ZARA && esCentroServicio` | Se imprime `RemisionCentroServicio = 1210` en vez de `Remision = 1001` (`ConsultarVentas.cs:4444-4468`) |

### 3.5 Variante de producto (obra / alcaldía) y aviación

| capacidad | cuándo | qué cambia |
|---|---|---|
| `operacionDeObra` | `esAlcaldiaMC >= 1` | **El ERP no existe.** `Menu.cs:176-235` esconde VENTAS, COBRANZA, COMPRAS, CORTES, CLIENTES, PROVEEDORES, PRODUCTOS, FACTURAS, ASISTENCIAS, CONCENTRADOS, GARANTIAS, GASOLINA, ALMACEN CASCOS, REPORTES, CONFIGURACION, EMPRESAS, DIAS FESTIVOS, INVENTARIO TRANSITO, GARANTIAS ACEPTADAS, AJUSTES AL INVENTARIO, REASIGNAR AGENTE Y CARTERA, LIQUIDACIONES, EMPLEADOS, PRESTAMOS, COBROS INGRESOS, COBROS TRABAJADOS y **el grupo CATALOGOS entero** |
| `muestraCatalogosDeObra` | `esAlcaldiaMC >= 1` | Los diez catálogos de obra (contratistas, contratos, unidades de medida, cargos, residentes, tipos de obra/recurso/contratación/documento, partidas presupuestales). `Menu.cs:159-170` los esconde cuando vale 0 |
| `proveedoresDeObraVarianteGam` | `esAlcaldiaMC == 2` | El mismo botón abre **otro formulario**: `MCConsultarProveedores` con 1, `ConsultarProveedoresGam` con 2 (`Menu.cs:2107` / `:2133`) |
| `operacionDeAviacion` | `esAviacion` | Aviación es un árbol de formularios aparte (`Forms/Aviacion/`) con su propio menú (`Login.cs:281`) |
| `tieneModuloDeReportes` | `!operacionDeObra && aviacionIdCaja == 0` | Legacy apaga `btnReportes` con `esAlcaldiaMC` 1/2/3 (`Menu.cs:176-183`) y **`ReportesMenu.cs:159` hace `return` seco** cuando `AviacionIDCaja != 0`: con caja de aviación solo quedan COBRANZA y CONTABILIDAD |
| `usuariosConMulticajas` | `esAviacion` | En el alta de usuario aparece `chkMulticajas` y **se oculta todo el bloque de contraseña de autorización** (`Usuarios.cs:161`) |
| `usuariosConPermisoDeGarantiasSinCosto` | `TAU && !esAviacion` | "PERMITIR GARANTIAS SIN COSTO" y "PERMITIR CORTE DEL DIA" solo existen en Tauro, y se premarcan **por IDUsuario a mano** (22, 40, 1, 3 / 1065, 40, 1, 3) — `Usuarios.cs:62` |
| `empleadosConBonosDeAsistencia` | `TAU` | Los bonos de asistencia, puntualidad y salida tarde son de Tauro (`Empleados.cs:215`); Zaragoza los oculta siempre (`:273`) |

### 3.6 Inventario

| capacidad | Zaragoza | Tauro |
|---|---|---|
| `permiteVenderSinExistencia` | **`true`** | `false` |

`Constantes.InventarioNegativo`. **Lo decide la base, no el código**, así que puede cambiar sin que se
toque nada: no lo cablees. Con `false`, la captura tiene que frenar antes de dejar agregar más de la
existencia; con `true`, no.

### 3.7 Encabezado y fechas

| capacidad | valor | qué cambia |
|---|---|---|
| `etiquetaFechaDeOperacion` | `"VENTAS"` \| `"OPERACION"` \| `"FECHA"` | El rótulo del encabezado. `"OPERACION"` con `esAlcaldiaMC >= 1` (`Globales.cs:582`), `"FECHA"` en aviación (`:594`), `"VENTAS"` en MAC normal. **Úsalo como texto, no reconstruyas la condición** |
| `muestraFechaDePagos` | `esAlcaldiaMC == 0 && !esAviacion` | `Globales.cs:598` solo muestra las dos fechas en MAC normal; `Menu.cs:227` esconde la de pagos con 1/2/3 |
| `fechaDePagosEsPorUsuario` | **`true` en Zaragoza** | Ver la sección 4. Es la capacidad que más fácil se pasa por alto |
| `puedeCambiarFechaDeOperacionDeVentas` | `esAlcaldiaMC != 1 && != 2` | `Menu.cs:2007` hace `return` seco con 1 o 2, **antes de siquiera mirar permisos**. Con 3 lo deja (parece un descuido de Legacy; se copia tal cual) |

---

## 4. Los parámetros: valídalos en pantalla con el mismo número que la base

`empresa.parametros` no son capacidades, son datos. Están para que el front valide **antes** de mandar
y para que no haya constantes escritas a mano en el código del front.

| campo | Tauro | Zaragoza | para qué |
|---|---:|---:|---|
| `porcentajeIva` | `0.16` | `0.16` | **Es la fracción, no el porcentaje** (`IVA/100`, como lo devuelve `sp_n_ConsultaConstantes`). Publicar 16 multiplicaría por cien cualquier total |
| `noDiasAtrasados` | 2 | 8 | Tope inferior de la fecha de pagos |
| `noDiasAdelantados` | 1 | 2 | Tope superior de la fecha de pagos |
| `noDiasDevDeposito` | 7 | 7 | Días para devolver un depósito |
| `diasFolioConcentrado` | 7 | 200 | Antigüedad máxima de un folio para el concentrado |
| `diasFolioCobranza` | 5 | 200 | Antigüedad máxima para la hoja de cobranza |
| `numDiasPedidosRechazados` | 30 | 30 | Días que se guardan los pedidos rechazados |
| `diferenciaPagoVsTransCheq` | 1.00 | 0 | Tolerancia pago vs transferencia/cheque |
| `maximaDiferenciaUsados` | — | — | Tolerancia de diferencia en usados. **Legacy la lee cruda del DataTable y ramifica con ella**: `VentasUsados.cs:643` y `:717`. No tenía campo en `Variables` |
| `facturaPorcentajeDescMax` | 40.00 | 43.00 | Tope de descuento (`Ventas.cs:5703`) |
| `porDescMax` | 20.00 | 20.00 | Tope de descuento general |
| `maxTiempoInactividadMinutos` | 31 | 99999 | Cierre por inactividad. En Zaragoza está **prácticamente apagado**: no inventes un timeout propio |
| `checadorHoraTarde` | 13 | 14 | Hora de retardo (`LectorHuella.cs:83`) |
| `horasLaborales`, `horaEntrada`, `salidaTarde` | | | Nómina y asistencias. Legacy los lee crudos del DataTable (`ReporteEmpleadoAsistenciaRecibos.cs:104-108`, `:505-526`) |
| `defaultNumCopias` / `maxNumCopias` | 2 / 3 | 1 / 3 | Diálogo de impresión: valor inicial y tope |
| `idAgenteAutorizaGarantias` | 9 | 9 | Agente que autoriza garantías |
| `idGrupoCategoriaServicios` | | | Grupo de categoría que son SERVICIOS. **Legacy lo lee crudo y ramifica** (`ConsultarProductos.cs:185`, `Servicios.cs:448`). Los otros cinco `ID_GRUPO_CATEGORIA_*` están **quemados en el código** de Legacy (`Variables.cs:117-121`): este es el único configurable |
| `idEmpresaCS` | 0 | 1 | Empresa del centro de servicio |
| `idEmpresaZara`, `idEmpresaLuro` | 1, 7 | 1, 7 | Ids de empresa |
| `idClienteCS` | 0 | 0 | Cliente del centro de servicio |
| `idsClienteZaragoza` | `""` | `"19669"` | Lista separada por coma de clientes que se tratan como "Zaragoza" |
| `idsClienteServicioDomicilio` | | | Clientes de servicio a domicilio: la **excepción** a `lubricantesYFiltrosVanPorAceites` (`Ventas.cs:1972-1997`) |
| `zaraSaldoLiquidarDineroMonto` / `...CascosMonto` | | | Umbrales de la auto-liquidación de saldos |
| `precioCascoKilo` | | | Para el pago de cascos por kilo de Tauro |

---

## 5. Las dos fechas de operación

`empresa.fechas` trae las dos fechas **ya resueltas** y, además, **los límites que el procedimiento va
a aplicar**, para que el front pueda deshabilitar el botón antes de pedir el cambio en vez de
enterarse por un mensaje del SP.

```jsonc
"fechas": {
  "operacionVentas": "2026-10-07", "operacionVentasFtm": "07-10-2026",
  "operacionPagos": "2026-02-27",  "operacionPagosFtm": "27-02-2026",
  "puedeAvanzarVentas": true,
  "siguienteOperacionVentas": "2026-10-08",
  "minimaOperacionPagos": "2026-10-05",
  "maximaOperacionPagos": "2026-10-08",
  "usuarioUltimoCambioVentas": "SISTEMA", "fechaUltimoCambioVentasFtm": "07-10-2026 00:10",
  "equipoUltimoCambioVentas": "TAURO",
  "usuarioUltimoCambioPagos": "MARYORIS", "fechaUltimoCambioPagosFtm": "28-02-2026 09:48",
  "equipoUltimoCambioPagos": "NAOLANY"
}
```

### 5.1 La de PAGOS es por usuario en Zaragoza — y esto no está en ningún formulario

Lo decide el procedimiento, no la pantalla:

```sql
-- sp_n_ActualizarFechaOperacion, rama de pagos
IF @Funcionalidad = 'TAU'
    UPDATE Constantes      SET FechaOperacionPagos = @FechaOperacionPagos        -- una para toda la empresa
IF @Funcionalidad = 'ZARA'
    UPDATE [Usuarios Fechas] SET FechaOperacionPagos = ... WHERE IDUsuario = @IDUsuario   -- una por usuario
```

Y `sp_n_ConsultaConstantes` la lee igual: la saca de `[Usuarios Fechas]` solo cuando la funcionalidad
es ZARA, e **inserta la fila si no existe**.

Por eso `capacidades.fechaDePagosEsPorUsuario`. **Un front que la trate como global le mostraría a un
usuario la fecha de otro**, y si además la pone en un caché compartido, se la cambia.

`fechas.operacionPagos` ya viene resuelta: es la del usuario cuando aplica, y la de la empresa cuando
no hay fila todavía (el backend **no inserta**: leer capacidades no escribe).

### 5.2 Cambiar la de PAGOS

```http
PUT /api/constantes/fecha-operacion-pagos
permiso: constantes.fechapagos.editar
{ "fechaOperacionPagos": "2026-10-06", "valido": false }
```

- La fecha es **obligatoria** (400 si falta). Ver la trampa en 5.4.
- Rango aceptado: `[minimaOperacionPagos, maximaOperacionPagos]`.
- `valido: true` **salta solo el tope inferior**; el superior no se puede saltar. Mac31 lo pone en
  `true` después de pedir una contraseña de autorización (`FechaOperacionVentas.cs:112-121`). Si tu
  pantalla no tiene ese paso, manda `false`.

### 5.3 Avanzar la de VENTAS — **nuevo**

```http
POST /api/constantes/fecha-operacion-ventas/avanzar
permiso: constantes.fechaventas.editar
{ "fechaEsperada": "2026-10-08" }        // opcional, recomendado
```

**No recibe fecha, y no es un recorte: el procedimiento no acepta una.**

```sql
-- sp_n_ActualizarFechaOperacion, rama de ventas (@FechaOperacionPagos = '')
SELECT @FechaInicial = FechaOperacion, @FechaFinal = DATEADD(dd, 1, getdate())
IF (DATEDIFF(dd, @FechaInicial, @FechaFinal) < 1)
   -> 'LA FECHA DE VENTAS NO SE PUEDE ADELANTAR MAS DE UN DÍA A LA FECHA ACTUAL.' + ROLLBACK
DELETE FROM AlmacenProductoHist WHERE FECHA = FechaOperacion
INSERT INTO AlmacenProductoHist (...)           -- el "freeze" del inventario
INSERT INTO FechaOperacionLog (TipoFecha='V', ...)
UPDATE Constantes SET FechaOperacion = DATEADD(dd, 1, FechaOperacion)
```

Mac31 hace exactamente esto: su botón manda un DTO **sin fecha**
(`Legacy/Mac31/Mac31/Forms/Catalogos/FechaOperacionVentas.cs:49-52`).

**Lo que la operación arrastra, y que el front debe advertir ANTES de llamar:**

- borra `AlmacenProductoHist` del día que se cierra y lo reescribe con las existencias de ahora;
- apunta el cambio en `FechaOperacionLog` con tipo `'V'`;
- **no es reversible desde la aplicación.**

Cómo usarlo:

1. Ofrece el botón solo con `capacidades.puedeCambiarFechaDeOperacionDeVentas` **y**
   `fechas.puedeAvanzarVentas`.
2. Confirma con el usuario, diciendo a qué día va a quedar (`fechas.siguienteOperacionVentas`) y que
   se recalcula el corte de inventario.
3. Manda `fechaEsperada = fechas.siguienteOperacionVentas`. Es un seguro contra el **doble avance**:
   dos pestañas abiertas, o un cierre automático entremedias, y el día avanza dos veces sin que nadie
   lo note. Si no coincide, el backend contesta **409** con las fechas reales y **no escribe**.
4. Al volver: `200` trae `fechas` ya actualizado, pero **vuelve a pedir
   `GET /api/empresa/capacidades`** si guardas el bloque entero en estado global.
5. Si no se puede, el backend contesta **400** con el texto literal del procedimiento. (La de pagos
   todavía contesta 200 con `result: -1` dentro de `data`; conviene alinearla.)

Medido contra `Produccion_svr`: avance correcto → `result: 1`, `FechaOperacion` 07→08, 507 renglones
en `AlmacenProductoHist`, un renglón nuevo en `FechaOperacionLog`. Segunda llamada → **400** con
*"LA FECHA DE VENTAS NO SE PUEDE ADELANTAR MAS DE UN DÍA A LA FECHA ACTUAL."* La base quedó
restaurada a su estado exacto.

### 5.4 La trampa: la cadena vacía NO es neutra

Las dos fechas salen del **mismo** procedimiento y la rama la decide el parámetro:

| `@FechaOperacionPagos` | qué hace |
|---|---|
| `''` | **avanza la fecha de VENTAS**, rehace el freeze de inventario y apunta `FechaOperacionLog` tipo `'V'` |
| cualquier fecha | mueve la fecha de **pagos** |

El endpoint de pagos mandaba cadena vacía cuando el cuerpo venía sin fecha, con un comentario que
decía que eso era *"no la cambies"*. **No lo era: era el cierre del día disparado por descuido.** Ya
está corregido (400 si falta la fecha), pero si escribes otro cliente de ese procedimiento, acuérdate.

### 5.5 Y la otra trampa: hay "consultas" que escriben

`sp_n_ConsultaConstantes` **no es una consulta**: cuando la fecha de operación está atrasada, borra y
rehace `AlmacenProductoHist` y adelanta `Constantes.FechaOperacion`. Es la rutina de cierre del día, y
**la dispara el primero que toque el sistema cada mañana**. 155 de las 488 capacidades de Legacy la
llaman.

Por eso **estas capacidades no se leen con ese procedimiento**: se leen con un `SELECT TOP 1` sobre
`dbo.Constantes`. Si se hubiera usado el SP, pedir las capacidades al iniciar sesión habría hecho el
cierre del día **en cada primer login de la mañana, desde el web**. Detalle completo en
`sacNet-8/migracion/CUIDADO-consultas-que-escriben.md`.

---

## 6. Reportes: los números cambian por empresa

Los `IDReporte` **no son los mismos en las dos empresas**. El front **no** debe elegirlos: el backend
ya los resuelve. Lista para que sepas qué hay detrás y no te sorprenda que el PDF salga distinto.

| nodo | Tauro | Zaragoza | quién decide |
|---|---|---|---|
| Cobranza | **1160** | **1161** | `ReporteV2Service.cs:783` (igualdad exacta con `"ZARA"`). Ya copiado en `sacNet-8/.../Application/Reportes/ReporteCobranzaPorEmpresa.cs` |
| Pagos detallado | **77** | 71 | `ReportesMenu.cs:3346`. Mismo SP, otra plantilla |
| Pagos (CS) | 72 | — | `ReportesMenu.cs:3387`. **Si ya entró la rama de Tauro (77), esta no entra** |
| Listas de precios | 1243/1244/1246/1247, **cada una con su propio SP** `sp_n_rptListasPreciosTauro*` | **1240** siempre | `ReportesMenu.cs:3932` |
| Inventario | 212/227 si el grupo es FILTROS, si no 210/225 | 210/225 siempre | `ReportesMenu.cs:3791` |
| Hoja de cobro | **solo DETALLADO (1465)**; el TOTALIZADO (1466) es inalcanzable | 1465 / 1466 con selector | `ReportesMenu.cs:1295` |
| Folios con "SALDO A FAVOR" | inalcanzable | 19 | `ReportesMenu.cs:297`, `:2834` |
| Remisión | `Remision = 1001` con diálogo de impresora | 1001 directo, o **1210** en centro de servicio | `ConsultarVentas.cs:4380`, `:4409`, `:4444` |

Otras diferencias del árbol: Zaragoza agrega el nodo **Asistencias** a ADMINISTRACION (8 hijos contra
7, `ReportesMenu.cs:929`), solo Zaragoza muestra `chkPagosExcedentes` (`:1714`) y `chkFiltrarFechas`
(`:5541`), el inventario con costos en Zaragoza solo lo ven los usuarios **1059, 1060 y 3**
(hardcode, `:1794`-`:1812`), y tras los recibos de nómina Zaragoza lanza **un segundo reporte
encadenado** (`ReporteEmpleadoAsistenciaRecibos.cs:145`).

> Dos defectos de Legacy en este terreno: `ReportesMenu.cs:2452` manda `Param3 = IDEmpresaZara`
> **sin ningún `if`**, así que Transferencias Folios queda amarrado a Zaragoza en las dos empresas; y
> `ReportesMenu.cs:3361` tiene una **puerta trasera**: teclear `HACHIBRUNO` en `txtVisualizar` enseña
> los importes reales.

---

## 7. Lo que NO es una capacidad de empresa

Para que nadie lo busque aquí:

- **Los permisos del usuario.** Van en `/api/me` → `permissions[]` y en `/api/capacidades`. El
  identificador de un permiso en Legacy es `<NombreDeLaClaseDelForm>.<NameDelBotón>`, y el catálogo
  vive en la base (`n_Formas`/`n_Procesos`), no en el repo.
- **Los módulos habilitados.** Van en `/api/modulos/disponibles`. `enabledModules` **no existe en
  Legacy**: su equivalente es `ValidaPermisos` + los `Visible = false` de `Menu.cs`.
- **Las excepciones por IDUsuario.** Legacy tiene varias escritas a mano (22/40/1/3 en garantías de
  Tauro, 1059/1060/3 en inventario con costos de Zaragoza, 40 en `Globales.cs:297`). No son reglas de
  empresa y no se publican; si una hace falta, se convierte en un permiso.
- **La marca visual** (logo, nombre comercial, colores). Eso ya lo resuelve el front por
  `companyKey` / dominio (`docs2/src/nucleo/marca.ts`, `lib/app/tenants_config.dart`).
- **Las constantes muertas.** `LocalSRCOP`, `LocalLFD`, `FederalSICV`, `FederalSRCOP` se cargan en
  Mac31 y nunca se leen. No se publican.

---

## 8. Lo que falta, y lo que no se puede comprobar

Honestidad sobre el alcance de este mapa:

1. **Las 162 ramas de `esCentroServicio` no están verificadas contra una instalación real.** Vale 0 en
   las dos bases. Las capacidades están derivadas del código de Legacy, no medidas.
2. **Luro igual:** `MacL` no está restaurada.
3. **Aviación y obra igual:** `esAviacion = 0` y `esAlcaldiaMC = 0` en las dos bases.
4. **La fecha de pagos de un usuario solo-web.** `BancoPruebas` tiene `idUsuario = 0` (es un usuario
   de `UsuarioWeb` sin contraparte en `dbo.Usuarios`). En Zaragoza eso significa que **todos los
   usuarios solo-web comparten la fila `IDUsuario = 0` de `[Usuarios Fechas]`**: si uno mueve la fecha
   de pagos, se la mueve a todos los demás. Hace falta decidir cómo se mapea un usuario web a un
   usuario de Mac31 antes de abrir esa pantalla al web.
5. **`FechaOperacionLog` no dice quién lo hizo.** El procedimiento escribe
   `IDUsuarioSistema`/`EquipoSistema` de `Constantes`, no el `@IDUsuario` que recibe. La bitácora del
   cambio de fecha siempre apunta al mismo usuario. Lo que sí queda bien es
   `Constantes.IDUsuarioFechaOperacion`.
6. **`Ventas.btnHuella` ("CLIENTE MATRIZ F7")** tiene sus 10 ramas de visibilidad comentadas: no se
   puede saber en qué empresa debe verse. **Hay que preguntárselo a alguien que opere las dos.**

---

## 9. Dónde está el código

| | ruta |
|---|---|
| DTO publicado | `sacNet-8/ISL_Service/ISL_Service/Application/DTOs/Empresa/CapacidadesEmpresaDtos.cs` |
| Fila cruda de `Constantes` | `.../Application/Empresa/ConstantesDeEmpresa.cs` |
| **La traducción a capacidades** | `.../Application/Empresa/MapaDeCapacidades.cs` |
| Lectura (sin SP, sin escribir) | `.../Infrastructure/Repositories/CapacidadesEmpresaRepository.cs` |
| Servicio | `.../Application/Services/CapacidadesEmpresaService.cs` |
| Endpoint propio | `.../Controllers/EmpresaCapacidadesController.cs` |
| En `/api/me` | `.../Controllers/MeController.cs`, `.../Application/DTOs/Responses/MeResponse.cs` |
| Fechas de operación | `.../Controllers/ConfiguracionMacController.cs`, `.../Infrastructure/Repositories/SeguridadMacRepository.cs` |
| Pruebas | `.../ISL_Service.Tests/CapacidadesDeEmpresaTests.cs` |

**`MapaDeCapacidades` es el único sitio del backend nuevo donde se escribe `"ZARA"` o `"TAU"` para
decidir una regla de negocio.** Si descubres una rama nueva, el cambio va ahí y en este documento, no
en el front. Y si aparece una tercera empresa, se cambia ahí y en ningún otro lado.
