# Inventario del frontend de MAC — resumen

Este documento acompaña a `inventario-legacy-frontend.tsv`, que es el backlog. Aqui esta lo que el TSV
no puede decir en una celda: cuanto falta, que cambia entre empresas, como funcionan los permisos y que
conviene **no** copiar.

Todo salio de leer `Legacy/Mac31/Mac31` (228 formularios, ~190,000 lineas de C# + Designer),
`Web/QR_Web/docs2`, `Web/QR_Web/docs`, `Movil/mac-mac-mobile` y los 84 controladores de
`sacNet-8/ISL_Service`. No se ejecuto nada: todas las afirmaciones llevan `archivo:linea`.
Lo que no se pudo determinar quedo con `?` y dice que falta.

## Como leer el TSV

Una fila = **una accion** (un boton, un doble clic, una entrada de menu contextual). Si una pantalla
tiene 22 botones, son 22 filas. Las columnas que mas conviene entender antes de usarlo:

- **`permiso_legacy`** es `<NombreDeLaClaseDelForm>.<NameDelBoton>`, porque asi lo resuelve Legacy
  (ver la seccion de permisos). `<Forma>.acceso` significa que el candado es solo de entrada a la
  pantalla. `(sin permiso)` significa que esa accion **no tiene ningun candado**, y eso es un hallazgo,
  no un dato faltante.
- **`endpoint_nuevo`** se cruzo contra los 700 endpoints reales del backend. `(falta)` significa que
  hoy no hay API: la pantalla no se puede conectar aunque se dibuje.
- **`existe_web` / `existe_movil`** se comprobaron leyendo el codigo de cada front, accion por accion.
  `incompleto` quiere decir que la pantalla existe pero esa accion no, o que el boton existe sin
  handler, o que falta un filtro que Legacy si tiene; el motivo concreto esta en `notas`.
  **Una excepcion a vigilar:** en las filas de reportes, `existe_web = si` se refiere al **web viejo**;
  el web nuevo (`docs2`) no tiene modulo de reportes.
- **`aplica_zaragoza` / `aplica_tauro`** con `?` no es pereza: casi siempre es una rama cuya condicion
  esta comentada en Legacy, o una bandera (`EsCentroServicio`, `EsAlcaldiaMC`, `EsLuro`) que **no
  corresponde a ninguna de las dos empresas en operacion**. Esos casos hay que preguntarlos, no deducirlos.
- **`campos_clave`** trae todas las columnas de la rejilla o campos del formulario, sin recortar: la
  decision de que es principal y que va al detalle es posterior, pero el dato no se podia perder.
  La excepcion son los reportes, donde las columnas **no son determinables desde el front**
  (las arma el SP + la plantilla) y en su lugar se registro el `IDReporte`, la clave web-movil y el SP.

## Lo que hay que saber antes de planear nada

1. **Legacy no es un programa, son seis configuraciones.** El brief hablaba de dos banderas; hay seis,
   y una de ellas (`EsAlcaldiaMC`) apaga practicamente todo el ERP. La seccion de diferencias por
   empresa tiene la lista con evidencia.
2. **El permiso de cada boton es el nombre del control de WinForms**, y el catalogo vive en la base
   (`n_Formas`/`n_Procesos`), no en el repo. Cualquier plan de permisos del front nuevo depende de leer
   esa tabla.
3. **El backend va mas adelante que los dos fronts.** Hay familias enteras de endpoints sin pantalla.
   Eso cambia el orden del trabajo: en varios modulos lo unico que falta es el front.
4. **Una parte de Legacy esta muerta y no hay que migrarla.** Pantallas que no se instancian, botones
   sin handler, validaciones comentadas. Esta identificada en su propia seccion para que nadie gaste
   semanas reconstruyendo algo que en Mac31 tampoco funciona.
5. **El movil practicamente no existe como cobertura funcional.**
   `shared_mobile/lib/modules/verticals/vertical_modules.dart:4` es literalmente una lista vacia.

## Los numeros

| | |
|---|---:|
| formularios de Legacy leidos | **228** |
| pantallas distintas en el inventario | **228** |
| **acciones inventariadas** | **1,651** |
| de ellas, reportes | 204 filas (**71 reportes distintos**, cada uno con sus dos salidas) |
| modulos | 18 |
| ramas por empresa encontradas | **336** (184 `funcionalidad` + 152 `EsCentroServicio`), mas 57 `EsquemaPago`, 17 `EsAlcaldiaMC`, 13 `EsLuro`, 7 `EsAviacion` |
| endpoints del backend cruzados | 700 |

### Cuanto falta

| | acciones | % |
|---|---:|---:|
| **no existen en el web** | **1,427** | 86% |
| existen en el web pero incompletas | 49 | 3% |
| existen en el web | 175 | 11% |
| **no existen en el movil** | **1,515** | 92% |
| existen en el movil pero incompletas | 15 | 1% |
| existen en el movil | 121 | 7% |
| sin endpoint en el backend (`(falta)`) | 695 | 42% |
| **con endpoint listo** | **956** | **58%** |

Dicho de otra forma: **de 1,651 acciones, 1,476 no se pueden hacer hoy desde el web** (las que faltan
mas las incompletas) y **1,530 no se pueden hacer desde el movil**. Pero **956 ya tienen API**, asi que
en mas de la mitad del trabajo lo unico que falta es el front.

Y un matiz que cambia como se leen las dos filas de "existen": de los 175 `si` del web, **105 son
reportes del web viejo** y 32 son de nomina. El web nuevo (`docs2`) por si solo cubre unas 38 acciones.

Por prioridad: **525 acciones de uso diario** (alta), 388 media, 738 baja.

### Modulos por tamaño, ordenados por carga de uso diario

| modulo | pantallas | acciones | alta | media | baja | en web | en movil | con endpoint |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Ventas | 38 | 356 | 208 | 92 | 56 | 34 | 15 | 196 |
| Cobranza | 12 | 119 | 106 | 4 | 9 | 3 | 0 | 88 |
| Reportes | 4 | 140 | 36 | 64 | 40 | 103 | 103 | 115 |
| Tesoreria | 9 | 61 | 36 | 15 | 10 | 0 | 0 | 36 |
| Productos | 8 | 49 | 27 | 15 | 7 | 0 | 0 | 14 |
| Nomina | 5 | 38 | 27 | 5 | 6 | 32 | 0 | 27 |
| Clientes | 6 | 36 | 19 | 10 | 7 | 0 | 0 | 21 |
| Inventario | 3 | 20 | 15 | 2 | 3 | 0 | 0 | 14 |
| Seguridad | 18 | 57 | 14 | 7 | 36 | 0 | 0 | 16 |
| Compras | 2 | 21 | 14 | 5 | 2 | 0 | 0 | 8 |
| Shell | 4 | 16 | 7 | 6 | 3 | 3 | 3 | 9 |
| Proveedores | 4 | 23 | 6 | 4 | 13 | 0 | 0 | 9 |
| Descuentos | 1 | 10 | 6 | 3 | 1 | 0 | 0 | 6 |
| Catalogos | 69 | 314 | 4 | 107 | 203 | 0 | 0 | 154 |
| Aviacion | 31 | 247 | 0 | 41 | 206 | 0 | 0 | 146 |
| Obra | 17 | 132 | 0 | 0 | 132 | 0 | 0 | 91 |
| Gasolina | 2 | 11 | 0 | 8 | 3 | 0 | 0 | 6 |
| Utilerias | 1 | 1 | 0 | 0 | 1 | 0 | 0 | 0 |
| **TOTAL** | **234** | **1651** | **525** | **388** | **738** | **175** | **121** | **956** |

### Los modulos mas grandes que NO existen en el web

| modulo | acciones que faltan | pantallas | prioridad | endpoints listos |
|---|---:|---:|---|---:|
| Ventas | 322 | 38 | alta | 167 (51%) |
| Catalogos | 314 | 69 | baja | 154 (49%) |
| Aviacion | 247 | 31 | baja | 146 (59%) |
| Obra | 132 | 17 | baja | 91 (68%) |
| Cobranza | 116 | 12 | alta | 85 (73%) |
| Tesoreria | 61 | 9 | alta | 36 (59%) |
| Seguridad | 57 | 18 | baja | 16 (28%) |
| Productos | 49 | 8 | alta | 14 (28%) |
| Reportes | 37 | 4 | baja | 13 (35%) |
| Clientes | 36 | 6 | alta | 21 (58%) |
| Proveedores | 23 | 4 | baja | 9 (39%) |
| Compras | 21 | 2 | alta | 8 (38%) |


## Las diferencias por empresa

Legacy no ramifica por dos valores, ramifica por **seis**, y el brief solo nombraba dos.
Esta es la lista completa, con el conteo de sitios activos (sin contar `.Designer.cs` ni lineas comentadas con `////`):

| bandera | sitios | que discrimina | donde se carga |
|---|---:|---|---|
| `Variables.funcionalidad` (`"ZARA"` / `"TAU"`) | **184** | la empresa: Zaragoza o Tauro | `Utils/Globales.cs:458`, de `Constantes.Funcionalidad` |
| `Variables.EsCentroServicio` (0/1) | **152** | si la instalacion es un centro de servicio | `Utils/Globales.cs:470` |
| `Variables.EsquemaPago` | **57** | esquema de pago viejo vs nuevo | `Utils/Globales.cs`, de `Constantes.EsquemaPago` |
| `Variables.EsAlcaldiaMC` (0/1/2/3) | **17** | el producto: MAC normal (0) vs Alcaldia/Obra/Aviacion (1/2/3) | `Utils/Globales.cs:468` |
| `Variables.EsLuro` (0/1) | **13** | instalacion Luro (base `[MacL]`) | `Utils/Globales.cs:471` |
| `Variables.EsAviacion` (0/1) | **7** | instalacion de aviacion | `Utils/Globales.cs` |
| `Variables.InventarioNegativo` | 4 | si se permite vender sin existencia | `Utils/Globales.cs` |

`enabledModules` **no existe en Legacy** (0 coincidencias): es un concepto solo del movil y del web.
En Legacy el equivalente es la combinacion de `ValidaPermisos` + los `Visible = false` de `Menu.cs`.

### Lo que el brief llamaba "enabledModules": Menu.cs apaga modulos enteros

`Menu.cs` esconde botones de menu completos segun la bandera, **antes** de cualquier permiso:

| evidencia | condicion | que desaparece |
|---|---|---|
| `Menu.cs:141,150` | `funcionalidad` contiene `ZARA` | **aparece** GALLEROS; en Tauro esta oculto |
| `Menu.cs:148-149` | `funcionalidad` contiene `ZARA` | se ocultan los dos contadores de folios sin asignar |
| `Menu.cs:142` | siempre | **aparece** ALMACEN CASCOS |
| `Menu.cs:155-156` | siempre | DOCUMENTOS y PROYECTOS ocultos por defecto |
| `Menu.cs:159-170` | `EsAlcaldiaMC == 0` | se ocultan los 10 catalogos de Obra/MC (contratistas, contratos, unidades de medida, cargos, residentes, tipos de obra, tipos de recurso, tipos de contratacion, tipos de documento, partidas presupuestales) |
| `Menu.cs:176-235` | `EsAlcaldiaMC` 1, 2 o 3 | se oculta **casi todo MAC** (COMPRAS en `:194`, AJUSTES AL INVENTARIO en `:195`, INVENTARIO TRANSITO en `:180`): VENTAS, COBRANZA, COMPRAS, CORTES, CLIENTES, PROVEEDORES, PRODUCTOS, FACTURAS, ASISTENCIAS, CONCENTRADOS, GARANTIAS, GASOLINA, ALMACEN CASCOS, REPORTES, CONFIGURACION, EMPRESAS, DIAS FESTIVOS, INVENTARIO TRANSITO, GARANTIAS ACEPTADAS, AJUSTES AL INVENTARIO, REASIGNAR AGENTE Y CARTERA, LIQUIDACIONES (COBROS y DESCUENTOS), EMPLEADOS, PRESTAMOS, ASISTENCIAS Y RECIBOS, COBROS INGRESOS, COBROS TRABAJADOS y **el grupo CATALOGOS entero** |
| `Menu.cs:285` | `EsAlcaldiaMC` 2 o 3 | **aparece** PROYECTOS |
| `Menu.cs:296-316` | `EsAlcaldiaMC` 2 o 3 | se ocultan RECORDATORIOS y TIPOS DE RECORDATORIO y BANCOS TRANSFERENCIAS; **aparecen** el grupo OPERACION y RECAUDACIONES; se oculta el logo |
| `Menu.cs:243-252` | `EsAlcaldiaMC` 2 o 3 | cambia el tema: fondo blanco, grupo en cafe, ventana de 702px en vez de maximizada |

### Un mismo boton, dos pantallas distintas

| evidencia | condicion | que pasa |
|---|---|---|
| `Menu.cs:2516` vs `Menu.cs:2544` | `EsCentroServicio == 0` / `== 1` | el boton **CORTES** abre `ConsultarCortes` o `ConsultarCortesCS`: dos formas distintas, con sus propias columnas y botones |

### Catalogos de solo lectura

Patron repetido: `if ((Variables.EsCentroServicio == 1) || (Variables.EsLuro == 1))` → se corta el alta y la
modificacion con el mensaje *"NO TIENE PERMITIDO ALTA O MODIFICACION DE ..."*.

| pantalla | lineas (alta / modificacion) |
|---|---|
| `Forms/Productos/ConsultarProductos.cs` | 984 / 1009 |
| `Forms/Productos/ConsultarCategorias.cs` | 322 / 345 |
| `Forms/Productos/ConsultarGrupoCategorias.cs` | 281 / 304 |
| `Forms/Productos/ConsultarMarcas.cs` | 283 / 306 |
| `Forms/Catalogos/ConsultarTipoUsados.cs` | 355 / 376 |
| `Forms/Productos/RelacionGrupoCategoriaMarcas.cs` | 283 (solo una) |

Aparte, `Menu.cs` corta el acceso **antes de abrir** varios catalogos cuando `EsCentroServicio == 1`,
con otro mensaje (*"NO SE PUEDEN MODIFICAR LOS CATALOGOS DESDE EL CENTRO DE SERVICIO."*):
lineas **611, 620, 653, 689, 725, 760, 1056, 1092**.

### El encabezado y la sesion

| evidencia | condicion | que cambia |
|---|---|---|
| `Login.cs:254`, `Menu.cs:90` | `ZARA` **y** `EsCentroServicio == 1` | el titulo de la app pasa a ser `Variables.NombreCentroServicio` |
| `Login.cs:690` | `EsCentroServicio == 1` | al entrar se consulta el inventario en transito y se avisa de pendientes |
| `Login.cs:729` | `funcionalidad == "ZARA"` | al entrar se revisan cambios de vigencia pendientes |
| `Globales.cs:582` | `EsAlcaldiaMC >= 1` | la etiqueta de fecha dice "Operacion" en vez de "Ventas" |
| `Globales.cs:594` | `EsAviacion == 1` | el encabezado muestra solo `FECHA:` |
| `Globales.cs:598` | `EsAlcaldiaMC == 0` | el encabezado muestra fecha de **Ventas** y fecha de **Pagos** |
| `Menu.cs:227` | `EsAlcaldiaMC` 1/2/3 | se oculta la fecha de pagos |
| `Menu.cs:328` | `TAU` **y** `IDUsuario == 3 o 40` | aparece el tablero de recordatorios en el inicio |
| `Menu.cs:1433` | `funcionalidad` contiene `TAU` | solo ahi se calculan los folios sin asignar |
| `Globales.cs:297` | `IDUsuario == 40` **y** `TAU` | rama especial para un usuario concreto |

### El dato que cambia como se lee toda esta tabla

`EsCentroServicio` vale **0 en las dos bases que existen** (`Produccion_svr` = `TAU`, `MacZ` = `ZARA`),
y lo decide la base, no el codigo
(`sacNet-8/migracion/ola-11-reportes-restantes.md:263`, `auditoria-final.md:546`).
Un centro de servicio es una **tercera clase de instalacion** que hoy no esta levantada: las bases
`MacZCS*` no existen en el servidor (`auditoria-final.md:544`).
Por eso, en el TSV, las acciones que solo viven tras `EsCentroServicio == 1` van con
`aplica_zaragoza = ?` y `aplica_tauro = no`: no son de ninguna de las dos empresas en operacion,
y no se pueden comprobar sin una instalacion de centro de servicio.

Lo mismo con Luro: `Constantes.BDLuro` vale `'[MacL].[dbo].'` en `MacZ` y **vacio** en Tauro
(`zaragoza-estado.md:353`), asi que `EsLuro` es del lado Zaragoza y `MacL` no esta restaurada.

### Ventas: donde Zaragoza y Tauro dejan de ser el mismo programa

Ventas concentra **91 de las 184** ramas por `funcionalidad` y **75 de las 152** por `EsCentroServicio`.
No son matices de presentacion: cambian las columnas de la rejilla, la regla de autorizacion y
**como se calcula lo que se factura**. Es la seccion que mas facil se pierde al rehacer el front.

#### La rejilla de ConsultarVentas no tiene las mismas columnas

| evidencia | ZARA | TAU |
|---|---|---|
| `ConsultarVentas.cs:1729` | columna `cargos` **oculta** | visible |
| `ConsultarVentas.cs:1737` | columna `Abonos` **oculta** | visible |
| `ConsultarVentas.cs:1745` | columna `Descuentos` **oculta** | visible |
| `ConsultarVentas.cs:1753` | columna `Saldo` **oculta** | visible |
| `ConsultarVentas.cs:1703` | `NombreCliente` a **340 px** | 210 px |
| `ConsultarVentas.cs:2060` / `:2073` | encabezado **"ACU/CASC/ACE"** | **"DIAS VENC"** |
| `ConsultarVentas.cs:2341`, `:2396-2415` | rejilla de saldos con columna **ESTATUS** | con columna **ACUM/CASC/ACE** |

#### Un credito, tres creditos

Zaragoza lleva **tres** condiciones de credito (acumuladores, aceites, cascos) donde Tauro lleva una.
Eso se propaga por toda la captura:

| evidencia | que cambia |
|---|---|
| `Ventas.cs:1584` | `lblDiasCredito` muestra tres condiciones en ZARA, una en TAU |
| `Ventas.cs:1666`, `:1705` | `lblMaxDiasVencidos` vale **"0/0/0"** en ZARA y **"0"** en TAU |
| `Ventas.cs:2230` | TAU manda `MaxDiasVencidos`; ZARA manda los tres `...Acumuladores/Aceites/Cascos` |
| `Ventas.cs:2863` / `:2879` | **la regla de autorizacion del pedido**: TAU exige autorizacion si un contador > 0; ZARA si **cualquiera** de los tres > 0 |
| `Clientes.cs:1036` | solo en ZARA son **obligatorias** `cboCondicionesCreditoAceites` y `cboCondicionesCreditoCascos` |
| `Clientes.cs:1025` | mismo aviso, distinto texto: ZARA dice "...DE ACUMULADORES" |
| `Ventas.cs:1628` | solo TAU consulta `CreditoDisponible()` y calcula `limiteCredito` |
| `Ventas.cs:1642`, `:1649` | ZARA **oculta** `lblPagosRealizadosFechas` y `lblPagosRealizadosMonto`; TAU los muestra como "PAGOS $x / 3 MESES = $limite" |

#### Lo que cada empresa no puede hacer

| evidencia | condicion | que se bloquea |
|---|---|---|
| `ConsultarVentasFacturas.cs:93-100` | `TAU` | **se ocultan `rbNotasCredito` y `rbComplementosPago` y se deshabilita `btnNuevaNotaCredito`**: Tauro no tiene nota de credito ni complemento de pago. Eso deja `VentasNotaCredito` (3556 lineas) y `VentasComplementoPago` (1310) como **pantallas de facto solo de Zaragoza** |
| `ConsultarVentasFacturas.cs:1205-1210` | `TAU` | ademas oculta `btnNuevaFactura`, `btnNuevaNotaCredito` y `btnNuevoComplementoPago` |
| `ConsultarVentas.cs:2936` | `TAU` | **no deja cancelar una remision ya facturada**; en Zaragoza si se puede |
| `ConsultarVentas.cs:2945-2960` | `TAU` | al cancelar, pregunta si se quita el folio del **concentrado** y pide contraseña. Zaragoza no tiene concentrados aqui |
| `ConsultarVentas.cs:468-547` | `ZARA && EsCentroServicio==0` | **los folios `A-` y `G-` no se imprimen desde aqui**: hace `return` y los desvia a Garantias |
| `Ventas.cs:1941-1999` | `ZARA` | **bloquea agregar Lubricantes y Filtros** (van por ACEITES), con excepcion para `IDCLIENTE_SERVICIO_DOMICILIO_ZARA` y sus 5 variantes `_CS1.._CS5` |
| `Ventas.cs:187` | `ZARA && EsCentroServicio==0 && Facturar==0 && SoloAceites==1` | **DESCUENTO ADICIONAL** solo existe en Zaragoza |
| `Ventas.cs:273` / `:575` | `ZARA` / `TAU` | ZARA muestra `cboAgenteAutorizo` y `lblUsuarioAutorizo`; TAU los oculta junto con `chkSoloLogistica` |
| `Menu.cs:141,150` | `ZARA` | GALLEROS es catalogo **solo de Zaragoza** |
| `Usuarios.cs:62` | `TAU` | "PERMITIR GARANTIAS SIN COSTO" y "PERMITIR CORTE DEL DIA" solo existen en Tauro, y se premarcan **por IDUsuario a mano** (22, 40, 1, 3 / 1065, 40, 1, 3) |
| `Empleados.cs:273` | `ZARA` | los bonos de asistencia, puntualidad y salida tarde se ocultan siempre: son de Tauro (`Empleados.cs:215`) |
| `ConsultaEmpleados.cs:53` | `TAU` | el boton EXCEL solo existe en Tauro |

#### La diferencia de fondo: cada empresa factura distinto

`VentasFacturacion.cs:1409-1418` — **ZARA** toma el precio de `FacturaPrecioListaConIva` y
`SubTotalConUsadoConIva`; **TAU** toma `precioLista` y **prorratea** el importe de la partida.
No es una columna distinta: es otra aritmetica.

Y arrastra la presentacion completa de la pantalla:

| evidencia | ZARA | TAU |
|---|---|---|
| `VentasFacturacion.cs:1040` / `:1053` | **"PRECIO C/IVA"** = `FacturaPrecioListaConIva` | **"PRECIO"** = `precioLista` |
| `VentasFacturacion.cs:1077` / `:1090` | **"IMPORTE C/IVA"** = `FacturaSubtotalConIva` | **"IMPORTE"** = `ImportePartida` |
| `VentasFacturacion.cs:966` | reordena `FacturaPrecioListaConIva`(3), `cantidadMostrarFtm`(4), `FacturaSubtotalConIva`(5) | — |
| `VentasFacturacion.cs:716`, `:812`, `:2470` | — | reordena `ImporteZ`(3) y `Cascos`(4); `columnaImporteSinCascos = "ImporteZ"` |
| `VentasFacturacion.cs:1492`, `:3162-3200` | recalcula `porcentajeDescuentoFactura` / `porcentajeDescuentoReal` | — |
| `VentasNotaCredito.cs:2040-2049` | usa `importeConIvaRnd` y `PrecioListaConIvaRnd` | usa `importe` y `precioLista` |

#### Imprimir tampoco es lo mismo

| evidencia | condicion | que pasa |
|---|---|---|
| `ConsultarVentas.cs:4380` | `TAU` | abre `ConfirmacionImpresion` para elegir impresora y copias |
| `ConsultarVentas.cs:4409-4440` | `ZARA && EsCentroServicio==0` | imprime directo con `Remision = 1001`, sin preguntar impresora |
| `ConsultarVentas.cs:4444-4468` | `ZARA && EsCentroServicio==1` | imprime **otro reporte**: `RemisionCentroServicio = 1210` |
| `ConsultarVentas.cs:4326` | `!CONTRASENA_IMPRIMIR && EsCentroServicio==0` | pide contraseña para imprimir |

Numeros de `enumReportes` que cambian por empresa: `Remision = 1001`, `RemisionSinPrecio = 1201`,
`RemisionCentroServicio = 1210`, `FacturaElectronica = 1003`,
`FacturaElectronicaCustomTauZara = 1310`, `Simular = 1175`, `Garantias = 1235`.

#### Lo que un centro de servicio hace distinto en ventas

| evidencia | que cambia |
|---|---|
| `Ventas.cs:351` | aparecen `lblEmpresaCS`, `cboEmpresaCS` y `chkFacturaCascos`; las empresas se cargan con `ConsultarEmpresasZara` |
| `Ventas.cs:361` | aparece `chkRemisionMayorista` |
| `ConsultarVentas.cs:978`, `ConsultarVentasFacturas.cs:793` | la busqueda de cliente se fuerza a `Identico=1`: **solo por numero completo** |
| `ConsultarVentas.cs:1201` | la pantalla arranca forzada en la pestaña **Emitidas** |
| `VentasFacturacion.cs:292`, `:512` | se **ocultan `btnRFC` y `btnBuscarCliente`**: no se puede cambiar a quien se factura |
| `VentasFacturacion.cs:1633`, `:2095` | el `IDEmpresa` que se timbra es `IDEmpresaMayorista`, no la del usuario |
| `VentasFacturacion.cs:1643`, `:1768`, `:4251` | aparece y se calcula `txtTotalConComision` |
| `ConsultarVentas.cs:2921`, `:2928` | con `EsCentroServicio==0 && NuevoEsquemaPagos==0` no se puede cancelar una remision pagada ni con abonos |
| `ConsultarVentasSeleccionar.cs:91` | en TAU la fecha inicial es la **FechaOperacion** de la constante; en el resto, hoy menos un año |

#### Un detalle que no se puede deducir del codigo

`Ventas.btnHuella` (texto **"CLIENTE MATRIZ F7"**) tiene sus **10 ramas de visibilidad comentadas**
(`Ventas.cs:344, 396, 486, 891, 969, 2190, 2423, 2574, 2611, 5950`). No se puede saber en que empresa
debe verse. Quedo en el TSV con `aplica_zaragoza = ?` y `aplica_tauro = ?`: **hace falta preguntarle a
alguien que opere las dos empresas.**

## Garantias: el flujo va al reves en cada empresa

`ConsultarGarantias` (5071 lineas, 22 acciones) es la pantalla con la diferencia por empresa mas
peligrosa de todo el inventario, porque **no cambia la presentacion: cambia el orden del proceso.**

| accion | evidencia | ZARA | TAU |
|---|---|---|---|
| GARANTIA | `:1836` / `:1854` | **niega** si el renglon ya esta `EnviadaProveedor` | **solo permite** si `EnviadaProveedor == "SI"` |
| AJUSTE | `:2053` / `:2072` | **niega** si ya esta `EnviadaProveedor` | **solo** si `EnviadaProveedor == "SI"` |
| ENVIAR PROVEEDOR / DIAGNOSTICAR | `:2154` / `:2170` | solo garantias **Remisionadas** | solo garantias **Registradas** |
| DEVOLVER CLIENTE | `:2269` | niega si ya esta `EnviadaProveedor` | sin ese freno |
| REMISIONAR | `:2374` | niega si ya esta `EnviadaProveedor` | sin ese freno |
| AGREGAR a seleccionados | `:4227` | no deja agregar una ya `EnviadaProveedor` | sin ese freno |

Leido junto: en **Zaragoza** mandar al proveedor es el **ultimo** paso y bloquea todo lo demas; en
**Tauro** es el **primer** paso y es requisito para poder hacer garantia o ajuste. Un front que
implemente una sola de las dos reglas rompe a la otra empresa en silencio, y el sintoma seria
"el boton no me deja" sin ninguna pista.

Ademas:

- `ConsultarGarantias.cs:1887` — en **TAU** los usuarios **22 (GISEL), 40 (PAMELA), 1 (ADMIN) y
  3 (JUAN)** se saltan la validacion de monto < 300. Cuatro IDs escritos en el codigo.
- `ConsultarGarantiasAceptar.cs:456` — en **ZARA** la rejilla tiene la columna **CENTRO SERVICIO**
  (`NombreCentroServicio`); en Tauro esa columna no existe.
- Centro de servicio: `:2415` REMISIONAR exige **una sola** garantia y pide forma de pago (venta de
  contado); `:2763` si `TotalPagar > 0` **cobra la remision automaticamente** abriendo `VentasPagoCS`;
  `:2823` imprime `RemisionCentroServicio` en vez de la remision normal; `:3923` con
  `DevolverCliente` **no pide confirmacion**.

## Cobros y pagos: la hoja de cobro es una regla solo de Zaragoza

`VentasPago` (4638 lineas) concentra **27 ramas** por empresa, el maximo del repositorio.
La de fondo es esta:

**En Zaragoza no se puede aplicar un pago sin una hoja de cobro abierta.**
`VentasPago.cs:2389` y `:2401` niegan con *"SE REQUIERE UNA HOJA DE COBRO PARA APLICAR
PAGOS/DESCTOS."* cuando es `ZARA && EsCentroServicio == 0 && IDCobro == 0` y no hay saldo a favor.
La misma regla se repite en `ConsultarVentasPagosUsados.cs:1296` (actualizar cascos), `:2477`
(nuevo pago) y `:3090` (pago por diferencia de cascos). En Tauro esa puerta no existe.

Otras que cambian el resultado, no el aspecto:

| evidencia | condicion | que cambia |
|---|---|---|
| `VentasPago.cs:1257` | `TAU` | el saldo se acumula con la columna **`saldo`**; en ZARA con **`saldoImporte`** |
| `VentasPago.cs:1315`, `:1360` | `TAU` | muestra **TOTAL PAGAR**; en ZARA muestra `ImporteZ` como "IMPORTE" |
| `VentasPago.cs:642` / `:651` | `ZARA` / CS | la etiqueta del total es "SALDO DINERO:" / "SALDO CASCOS:" / "TOTAL:" |
| `VentasPago.cs:204` / `:219` | `TAU` / `ZARA` | el pago **por kilo** (`rbCascoKilo`) solo existe en Tauro, y los costos G1..G8 quedan de solo lectura |
| `VentasPago.cs:693` | `TAU` | aparece el panel de liquidacion (CAMIONETA / AGENTE); `:2676` lo valida como obligatorio |
| `VentasPago.cs:770` | `ZARA` | **oculta efectivo, tarjeta y saldo a favor** como formas de pago |
| `VentasPago.cs:2003` | `ZARA` | aparece el panel de tarjeta con credito/debito y banco |
| `VentasPago.cs:3296` | `ZARA` | hay **tope de monto para pagar en efectivo** |
| `VentasPago.cs:3113` / `:2342` | `ZARA` / — | ZARA valida el precio de cada casco excedente G1..G8 contra su precio original; `ValidaPrecioCascosExcedentes` devuelve `true` de inmediato en ZARA en el otro camino |
| `VentasPago.cs:3607`, `:3627` | `ZARA && NuevoEsquemaPagos == 0` | pide **contraseña** para descuento, nota de credito, transferencia, deposito y efectivo |
| `VentasPago.cs:3663` | `ZARA` | el `IDTipoPago` 11 **se manda como 911** para no chocar con otra funcionalidad del SP |
| `VentasPago.cs:3464` | `ZARA` | el mensaje dice "INGRESE EL MONTO DEL PAGO"; en el resto "DEL ABONO" |
| `ConsultarCobros.cs:2483` | `TAU` | **no deja cancelar una remision ya facturada**; en ZARA no existe ese freno |
| `Cobranza.cs:479`, `:1772` | `ZARA` / `TAU` | la rejilla se arma con **`GridDetallesZara`** o **`GridDetallesTau`**: dos configuraciones de columnas distintas. En TAU ademas mueve `EsFacturaParaPago` al ordinal 13 |
| `Concentrado.cs:136` / `:143` | `TAU` / `ZARA` | **SOLO DEVUELTAS** solo existe en Tauro, y `SinCambioVigencia` vale 1 en TAU y 0 en ZARA |
| `Concentrado.cs:384`, `:1207` | `TAU` | AGREGAR y ELIMINAR piden **contraseña**; en ZARA no |
| `Concentrado.cs:1457` | `TAU` | valida que la fecha del folio caiga en el rango permitido |
| `Concentrado.cs:2272` | `ZARA && IDCobro > 0` | al guardar **actualiza tambien la hoja de cobro** |
| `ConsultarVentasPagosUsados.cs:2792`, `:3358` | `ZARA && IDBancoTransferenciaMovimiento > 0` | al cancelar el pago **cancela tambien el movimiento bancario** |
| `VentasPagoCS.cs:394` | `ZARA` | con `RemisionMayorista == 1` calcula **3% de comision** |
| `Garantia.cs:213` | `ZARA && EsCentroServicio == 0` | se puede **elegir el producto**; en el resto llega fijo |

### Una pantalla que no vale migrar

**Liquidaciones de agente/camioneta** (21 acciones en el TSV) esta muerta por los dos extremos:
`LiquidacionesAgenteCamioneta.btnGuardar` y `btnCerrar` **no tienen handler** (no se puede guardar), y
`Menu.cs:1242-1255` tiene comentados el `new`, el `ValidaPermisos` y el `ValidaOperadorProcesos`, asi
que **la pantalla no se abre**. Su pantalla de consulta son 48 lineas donde solo `btnNuevo` y
`btnBuscar` tienen handler y **la rejilla no configura ni una columna**. Tampoco hay ninguna ruta en el
backend nuevo para liquidaciones de agente/camioneta. Quedaron inventariadas y marcadas, pero la
recomendacion es no invertir ahi sin preguntar si el proceso sigue vivo.

## CorteDiario vs CorteDiarioCS: el caso que mejor explica el problema

Un solo boton del menu (CORTES) abre **dos pantallas distintas** segun `EsCentroServicio`
(`Menu.cs:2516` y `:2544`). No es la misma pantalla con una columna de mas: cuadran cosas distintas.

| | **CorteDiario** (`EsCentroServicio = 0`) | **CorteDiarioCS** (`= 1`) |
|---|---|---|
| que cuadra | la caja del dia de oficina central: ventas SAE, cobranza MAC y gastos | los retiros de un centro de servicio contra sus saldos por forma de pago |
| botones | 7: Guardar, Imprimir, **Agregar**, **Eliminar**, **Ver detalle MAC**, **Cambiar fecha**, Cerrar | **3**: Guardar, Imprimir, Cerrar |
| selector | `dtFechaCorte` | **`cboTipoRetiro`** (efectivo, cheque, transferencia, tarjeta credito, tarjeta debito, deposito efectivo) + `dtFechaFinal` |
| campos de dinero | SAE (SaeEsquina, SaeEsquinaDomingo, SaeLocalTauro, TotalSae) + MAC (MacAgentes, MacCamioneta, DepositoCheque, TransferLocalTauro, TransferTiendaEsquina, Transfer, TotalMac, LiquidacionTiendas, TarjetasTiendas, TarjetasTotal, NoAplicado, SobranteLiquidaciones) + desglose (DepositoEfectivo, Tarjetas, Cheque, Efectivo, **Morraya**) + totales | una **matriz 6x4**: SaldoAnterior, Pagos, Total, Retiro por cada forma de pago, mas TotalSaldoAnterior, TotalVentasPagos, Total, TotalRetiro, SalidaPagos |
| rejillas | `gridDesglose` = **GASTOS** (TipoGasto; Concepto; Monto) + eliminados al editar | `gridDesglose` = **PAGOS DE CLIENTES** (FolioFtm; FechaPago; NombreCliente; TipoPago; Abono; DetallePago) + `gridDesgloseSalidas` = **SALIDAS DE USADOS PAGADOS** |
| gastos | si, con Agregar/Eliminar; si el tipo es prestamo llama `NuevoPrestamo()` (`cs:1289`) | **no existen** |
| fecha | bloqueada salvo IDUsuario 1 o 3 (`cs:146`); `btnCambiarFecha` solo si `IDCorte == 0` (`cs:129`) | libre al crear, bloqueada al editar (`cs:166`) |
| imprimir | **se oculta** al crear (`cs:137`) | queda visible (la linea que lo ocultaba esta comentada, `cs:108`) |
| reporte | `enumReportes.CorteDiario` + `CorteDiarioDetalleMAC` | `enumReportes.CorteCS` (con IDTipoRetiro y FechaFinal) |
| **permisos** | **ninguno**: nunca pasa por `ValidaOperadorProcesos` | **si** (`ConsultarCortesCS.cs:522`), y si `btnGuardar` llega deshabilitado **esconde el retiro de efectivo y las observaciones** (`cs:195-205`) |
| recarga | solo al cambiar la fecha | al cambiar `cboTipoRetiro` **recarga tres consultas** (`cs:1229`) |

Y las pantallas de consulta tampoco coinciden: `ConsultarCortes` tiene `btnReporteGastos` (la unica
entrada a `ReporteGastos`) y `ConsultarCortesCS` no; en cambio `ConsultarCortesCS` tiene `cboTipoRetiro`,
que **cambia que columnas se muestran** (efectivo `cs:254`, cheque `cs:299`, transferencia `cs:336`,
tarjetas `cs:373`, deposito efectivo `cs:444`).

**Asimetria de permisos a registrar:** `CorteDiario` no tiene candado por proceso y `CorteDiarioCS` si.
Quien rehaga esto tiene que decidir cual de las dos esta bien.

**Bug encontrado:** `ConsultarCortesCS.cs:656` manda `corteInputDTO.UrlCancelar` (`api/cortes/cancelar`)
en vez de `UrlCancelarCS` (`api/cortescs/cancelar`), que si esta definida en `CorteInputDTO.cs:91`.
Cancelar un corte de centro de servicio esta pegandole al endpoint del corte normal.

### Zaragoza liquida sola los saldos chicos, Tauro no

`AsignarBancosTransferencias.cs:648` — `VerificarLiquidacionSaldoMenor()` arranca con
`if (Variables.funcionalidad != "ZARA") return;`. Cuando si es Zaragoza, despues de aplicar el deposito:

1. **Dinero** (`cs:665-702`): por cada remision, si `0 < saldoImporte <= ZaraSaldoLiquidarDineroMonto`,
   inserta un pago tipo Descuento por el saldo completo con motivo `ZaraSaldoLiquidarDineroMotivo`.
2. **Cascos** (`cs:706-748`): igual con `saldoCascos <= ZaraSaldoLiquidarCascosMonto` y `DiffUsados = 1`.

O sea: **en Zaragoza los saldos chicos se liquidan solos con un descuento; en Tauro quedan vivos.**
Dos detalles: la comparacion es `!= "ZARA"` exacta (no `.Contains`), asi que cualquier variante del
string apagaria el automatismo sin aviso; y el mensaje de error de `cs:784` siempre imprime
`ZaraSaldoLiquidarDineroMonto` aunque la liquidacion sea de cascos.

### Cascos: misma tecla, dos significados

| evidencia | condicion | que cambia |
|---|---|---|
| `ConsultarUsadosMovimientos.cs:46` | `TAU` | titulo `CASCOS ACTUAL - TAURO`, `rbZaragoza` se renombra "TAURO" y **`rbGalleros` se oculta**: en Tauro no hay flujo de galleros |
| `ConsultarUsadosMovimientos.cs:251` | `TAU` | imprime `ComprobanteMovimientoCascosDiarioTauro` con `Param2 = FechaInicial` → **reporte del DIA**. En ZARA imprime `ComprobanteMovimientoCascosZaragoza` o `VentaCascos` con `Param2 = IDUsadoMovimiento` → **reporte del MOVIMIENTO** |
| `ConsultarUsadosMovimientos.cs:230` | `ZARA` | al imprimir **exige fila seleccionada**; Tauro no |
| `EntradaSalidaUsados.cs:1270` | `ZARA` | **solo Zaragoza imprime automaticamente** el comprobante al guardar; Tauro guarda y no imprime |
| `ConsultarUsadosMovimientos.cs:130`, `EntradaSalidaUsados.cs:423`, `DineroUsadoCobro.cs:825`, `:875` | `TAU` | la columna del casco es `TipoUsadoCorto`; en el resto `TipoUsado` |
| `EntradaSalidaUsados.cs:157` | `EsCentroServicio == 1` | aparecen `txtTotalPagar` y `cboTipoEntrada` (compra de cascos / entrega de deposito / bonificacion / ajuste). Sin CS no hay tipo de entrada ni total a pagar |
| `EntradaSalidaUsados.cs:179` | `EsCentroServicio == 1` | aparece `cboTipoSalida` (salida camioneta / ajuste salida) |
| `EntradaSalidaUsados.cs:784` | `EsCentroServicio == 1` y entrega de deposito | **exige el folio de la remision** o aborta el guardado |

## Reportes: el modulo donde el front nuevo esta mas cerca y mas lejos a la vez

**71 reportes distintos**, 133 filas de tipo `reporte` en el TSV (cada reporte lleva dos filas, una por
salida). Y es importante entender como funcionan, porque **no son pantallas**:

`ReportesMenu` no pinta ninguna rejilla. Arma un `ParametroInputDTO` con `Param1..Param16`, lo guarda
con `GenericService.Actualizar` y **abre Chrome** en `URL_REPORTES_V2?psp=<id>`. El HTML y las columnas
los arma el SP + la plantilla del lado de MacReportes. Por eso en esas filas `campos_clave` va con `?`
y lo que se registro en su lugar es el **`IDReporte`/enum, la clave web-movil y el SP** de cada uno:
las columnas de salida **no son determinables desde el front** y hay que sacarlas de las plantillas.

Las unicas dos salidas son `btnPantalla` (PANTALLA F5) y `btnExcel` (EXCEL F6). No hay boton imprimir
ni PDF aparte: el PDF es lo que sale de "pantalla".

### Cuanto esta hecho

| | reportes |
|---|---:|
| en Legacy | **71** |
| en el web viejo (`docs/modules/verticals/reportes`) | **53** |
| en el movil (`shared_mobile/lib/features/reportes`) | **53** — el mismo conjunto exacto, comprobado por diff de claves |
| en el **web nuevo** (`docs2`) | **0** — no hay modulo de reportes |

De los 53 del web viejo, solo **6** tienen configuracion propia en `opciones/`
(acumuladores-productos, cobranza, concentrados, facturas, folios, remisiones); los otros **47** pasan
por un generico (`LEGACY_GENERIC_REPORT_KEYS`). Las carpetas `opciones/administracion`,
`opciones/compras` y `opciones/inventario` tienen solo un `.gitkeep`.

**Los 18 que faltan en los dos fronts:** 6 de Aviacion (cobranza consolidada, depositos, auxiliar,
tarjetas, fianzas, ingresos para contabilidad — y solo 2 tienen endpoint nuevo), 6 de nomina
(asistencias 1025, solo asistencias 1024, inasistencias 1190, vacaciones 1027, recibos de nomina 1020
y el encadenado de comisiones de Zaragoza — **ninguno tiene endpoint en sacNet-8**), 1 de MC (control
acumulado de estimaciones), 3 sin nodo de menu (centros pagos entradas/salidas 1415, centros garantias
1420, garantias registradas 1236 cuyo nodo esta comentado en `ReportesMenu.cs:808-809`) y 2 de formas muertas.

### Los permisos de reportes son un tercer esquema

`ReportesMenu.cs:679-702` tiene **su propia copia** de la validacion. El permiso no es `Forma.btnAlgo`:
es `Forma` = `ReportesMenu` y `proceso` = `(prefijo + " " + node.Text).Replace(" ","-").ToUpper()`,
o sea `VENTAS-<NODO>`, `COMPRAS-<NODO>`, `INVENTARIO-<NODO>`, `ADMINISTRACION-<NODO>`,
`COBRANZA-<NODO>`, `CONTABILIDAD-<NODO>`. Y **se resuelve al cargar el arbol**, no al pulsar: si falla,
`node.Tag = 0` y los botones quedan grises.

Con un bug dentro: `ReportesMenu.cs:739` concatena sin espacio —
`ValidaPermisos("Ventas Pagadas" + "Cobranza Pagadas")` — asi que el proceso que busca en la base es
**`VENTAS-PAGADASCOBRANZA-PAGADAS`** y no `VENTAS-COBRANZA-PAGADAS`. Quedo tal cual en el TSV.

### Diferencias por empresa en reportes

| evidencia | condicion | que cambia |
|---|---|---|
| `ReportesMenu.cs:3346` | `funcionalidad == "TAU"` | Pagos detallado: el reporte **71** se cambia por **77** (`VentasPagosDetalladoTauro`). Mismo SP, otra plantilla |
| `ReportesMenu.cs:3387` | `EsCentroServicio == 1` | unica rama CS del archivo: **71 → 72** y **75 → totalizado CS**. Se aplica a `parametroInputDTO.IDReporte`, asi que **si ya entro la rama TAU (77), esta no entra** |
| `ReportesMenu.cs:3365` | `funcionalidad == "ZARA"` | con `chkPagosExcedentes` marcado manda `Cheques = 3` en Param12; en Tauro ese parametro va siempre 0 |
| `ReportesMenu.cs:1714` | `ZARA` | solo Zaragoza muestra `chkPagosExcedentes` |
| `ReportesMenu.cs:929` | `ZARA` | agrega el nodo **Asistencias** (1026) a ADMINISTRACION: ZARA tiene 8 hijos, Tauro 7 |
| `ReportesMenu.cs:1295` | `ZARA` | Hoja de Cobro: solo ZARA muestra `gbFormato`. **En Tauro sale siempre DETALLADO (1465) y el TOTALIZADO (1466) es inalcanzable** |
| `ReportesMenu.cs:297`, `:2834` | `ZARA` | `cboStatusFolios` gana "SALDO A FAVOR" y el reporte cambia a **19**. En Tauro ese reporte es inalcanzable |
| `ReportesMenu.cs:3932` | `TAU` | Listas de precios: tipoLista 1/2/3/4 → **1243/1244/1246/1247**, cada uno con **su propio SP** `sp_n_rptListasPreciosTauro*`. En ZARA siempre **1240** |
| `ReportesMenu.cs:3791` | `TAU` | Inventario: si el grupo es FILTROS → 212/227; en ZARA siempre 210/225 |
| `ReportesMenu.cs:1851`, `:1877` | `TAU` | oculta tabProductos/grupos/categorias/marcas y muestra `gbTipoLista`; y re-titula `gbGastos` como **"INCREMENTAR"** con `chkIncluirDescuentos` |
| `ReportesMenu.cs:1794`, `:1812` | `ZARA` | Inventario actual: `chkPedido` solo en ZARA, y **con costos solo para IDUsuario 1059, 1060 y 3** (hardcode). En Tauro el inventario con costos lo ve cualquiera |
| `ReportesMenu.cs:5541` | `ZARA` | Cobranza: `chkFiltrarFechas` solo en ZARA y solo con estatus vigentes y vencidas |
| `ReporteEmpleadoAsistenciaRecibos.cs:145` | `ZARA` | tras los recibos de nomina lanza **un segundo reporte** encadenado, con fechas fijas de `Variables.FechaComisionesZara*`. En Tauro no sale |
| `ReportesMenu.cs:159` | `AviacionIDCaja != 0` | **hace `return`**: con caja de aviacion no existen VENTAS/COMPRAS/INVENTARIO/ADMINISTRACION, solo COBRANZA (5) + CONTABILIDAD (1) |
| `ReportesMenu.cs:528`, `:530` | `EsAlcaldiaMC == 1` vs `== 2` | **ambas llaman al mismo `CargaArbolEmpresaMC()`**: la distincion no cambia nada. Y es codigo muerto, porque `Menu.cs:176-183` esconde `btnReportes` cuando `EsAlcaldiaMC` es 1/2/3 |
| `ReportesMenu.cs:2452` | **sin `if`** | Transferencias Folios manda `Param3 = Variables.IDEmpresaZara` **ignorando las empresas seleccionadas**: el reporte queda amarrado a Zaragoza en las dos empresas. Esto parece un bug, no una regla |

### Dos cosas que no hay que migrar

`ReporteCobranza.cs` y `ReporteVentasPagos.cs` son **formas muertas**: sus `btnPantalla_Click` y
`btnExcel_Click` estan **vacios** (`ReporteCobranza.cs:37-48`, `ReporteVentasPagos.cs:198-206`) y
**nadie hace `new` de ninguna de las dos en todo Mac31**. Lo vivo son los nodos "Cobranza" y "Pagos"
de `ReportesMenu`.

### Y una puerta trasera

`ReportesMenu.cs:3361`: si se teclea **`HACHIBRUNO`** en `txtVisualizar`, el reporte de Pagos manda
`Visualizar = 1` (importes reales) y luego borra el texto. Es una contraseña escondida en el codigo.

## Los permisos

El brief decia que Legacy los resuelve con `ValidaPermisos(...)`. Es verdad a medias, y la mitad que
falta es la que importa para reconstruir el front.

### Hay dos mecanismos, no uno

**1. Acceso a la pantalla** — `Globales.ValidaPermisos(Form)` (`Utils/Globales.cs:189`), **105 pantallas**.
Busca en `dtUsuarioActualFormasProcesos` una fila con `Forma = <nombre de la clase>` y `acceso = 1`.
Le basta **un solo** proceso con acceso para dejar entrar. Es el filtro de `Menu.cs`.

**2. Habilitado de cada boton** — `Globales.ValidaOperadorProcesos(Form)` (`Utils/Globales.cs:84`), **149 call sites**.
Este es el verdadero permiso por accion, y funciona al reves de lo que uno esperaria:

1. recorre la forma y **deshabilita todos los botones** (`Globales.cs:100-112`);
2. lee las filas de `n_Formas`/`n_Procesos` para esa forma y, por cada una con `acceso = 1`,
   busca el control cuyo `Name` sea igual al `proceso` y **lo vuelve a habilitar** (`Globales.cs:131-165`);
3. baja un nivel dentro de los `Panel` para repetirlo (`Globales.cs:113-126`, `:150-163`).

Entonces el identificador de un permiso es **`<NombreDeLaClaseDelForm>.<NameDelBoton>`**.
No es una convencion que alguien escribio: es literalmente el nombre del control de WinForms
usado como clave de base de datos.

`ValidaPermisos(Form, Button)` — el que nombraba el brief — existe (`Globales.cs:217`) pero
**solo se usa 4 veces en todo Legacy**: `LiquidacionesDescuento.btnAplicar`, `.btnAutorizar`,
`.btnRechazar` y un `VentasFacturacion.btn`. Si se inventaria buscando ese metodo, se concluye
que casi nada tiene permiso, y es falso.

### Tres consecuencias para el front nuevo

- **El catalogo de permisos no esta en el repo.** Vive en `n_Formas`/`n_Procesos` en la base. El codigo
  solo dice *donde* se aplica, no *cuales* existen. Para la lista autoritativa hay que leer la base.
- **El nombre del permiso esta amarrado al nombre de un control de WinForms.** Cualquier permiso que
  el front nuevo quiera respetar tiene que seguir llamandose `btnAlgo`, o hace falta una tabla de traduccion.
  Esto ya mordio: ver `Movil/mac-mac-mobile/lib/app/runtime/modules_config.dart`, donde se explica que
  el backend *"borra los permisos ventas.* que no sean web-only y los recalcula desde el boton
  btnAutorizarPedido de Mac31"*.
- **El perfil Administrador se salta todo.** `Globales.cs:87`, `:193` y `:220` devuelven acceso total
  antes de consultar nada. Un front nuevo que no replique ese atajo dejara al administrador fuera.

### Hallazgos sueltos

- **Puerta trasera por `Tag`.** En `ValidaOperadorProcesos`, un boton con `Tag != null` y distinto de `""`
  **no se deshabilita nunca** (`Globales.cs:103-110`). Es un bypass de permisos que depende de una
  propiedad de diseño.
- **Validaciones comentadas.** `Menu.cs:2053-2072` deja el CHECADOR DE ASISTENCIA sin validar permisos
  (todo el bloque esta con `////`), igual que `Menu.cs:1244` con las liquidaciones de agentes/camionetas.
  `Menu.cs:2094` (catalogos de Facturama) nunca valido nada.
- **Logica de permisos duplicada tres veces.** `Utils/Globales.cs:189`, `Forms/Reportes/ReportesMenu.cs:682`
  y `Forms/Aviacion/ReportesAviacion.cs:2579` reimplementan la misma consulta por separado.
- **Credenciales en el codigo.** `Utils/Variables.cs:24-26` lleva `APP_TOKEN`, `APP_CORREO_ELECTRONICO`
  y `APP_PWD` escritos a mano. No es del inventario de pantallas, pero aparece al leer el mecanismo de sesion.

## Lo que hay hoy en el web y en el movil

Esto se midio leyendo el codigo, no el backlog.

### Web nuevo (`Web/QR_Web/docs2/src/paginas/`) — 16 pantallas

`login`, `inicio`, `inicio-clasico`, `SelectorDeVista`, `ventas` (+`ModalPagos`, `PanelSaldosCliente`),
`pedido`, `devolucion`, `usados`, `multipago`, `cascos-cambio`, `empleados` (+`FormularioEmpleado`, `ModalHuellas`),
`asistencias` (+`ModalDia`), `prestamos`, `usuarios` (+`ModalPermisos`), `roles` (+`ModalPermisosDeRol`), `modulos`.

Los botones literales que existen hoy, pantalla por pantalla:

| pantalla | botones |
|---|---|
| asistencias | Actualizar, Reintentar |
| cascos-cambio | + Registrar movimiento, Ver, Guardar (F2), Cancelar, Imprimir, Limpiar, Limpiar filtros |
| devolucion | Agregar, Guardar |
| empleados | Limpiar (+ Capturar huella, Borrar huella, Cerrar en el modal) |
| prestamos | Modificar, Guardar, Cancelar, Cerrar, Limpiar filtros |
| multipago | Repartir automatico |
| usados | Deshacer |
| ventas | Limpiar, Volver (+ Nuevo pago, Cancelar pago, Cerrar en el modal) |
| usuarios / roles / modulos | Cambiar, Crear, Guardar, Cerrar |
| inicio | Cerrar sesion, Reintentar |

### Web viejo (`Web/QR_Web/docs/modules/`) — 30 pantallas

Tiene **diez cosas que el web nuevo todavia no tiene**, y por eso no se puede apagar todavia:

`verticals/reportes` (11 modulos js), `custom/zaragoza/dashboard-ventas`, `verticals/checador`,
`custom/tauro/cheques`, `custom/tauro/pagos-proveedores`, `custom/tauro/proveedores`,
`custom/aviacion/qr` (210 archivos js), `core/login/reset`, `core/usuarios-internos`,
y los 9 modulos de `custom/isl/*` (torneos, jornadas, equipos, profesores, temporadas... que son
otro producto, no MAC).

Y el propio modulo de reportes del web viejo esta a medias: de sus cuatro categorias
(`ventas`, `administracion`, `compras`, `inventario`), **solo `ventas` tiene opciones** —
6 (acumuladores-productos, cobranza, concentrados, facturas, folios, remisiones).
`administracion/`, `compras/` e `inventario/` son carpetas vacias.

### Movil (`Movil/mac-mac-mobile/`) — 7 modulos declarados + 4 pantallas

Modulos del catalogo (`shared_mobile/lib/modules/core/core_modules.dart`): `inicio`, `login`, `usuarios`,
`permisos_modulos`, `permisos_roles`, `permisos_usuarios`, `reportes`. Todos son core/administracion.

`shared_mobile/lib/modules/verticals/vertical_modules.dart:4` dice literalmente:

```dart
const sharedVerticalModules = <AppModule>[];
```

**La lista de modulos verticales del movil esta vacia.** Lo unico de negocio que existe son cuatro
pantallas cableadas a mano en `lib/modules/verticals/`: `mis_pedidos`, `pedidos_autorizar`,
`repartidor_home`, `repartidor_perfil`.

Traducido: el movil cubre **captura y autorizacion de pedidos para el repartidor, y nada mas**.
Cualquier `existe_movil = no` del TSV sale de ahi, no de una suposicion.

## Lo que ya existe pero esta incompleto

Son **49 acciones** en **21 pantallas**: la pantalla esta, pero la accion no se puede hacer, o el
boton no tiene handler, o falta un filtro que Legacy si ofrece. Es el trabajo mas barato del
backlog, porque la pantalla y su cableado ya existen.

### ConsultarVentas

- **buscar (BUSCAR F2)** (web/movil) — ConsultarVentas.cs:235; EN ZARAGOZA se ocultan las columnas cargos/Abonos/Descuentos/Saldo y NombreCliente se ensancha a 340 px (ConsultarVentas.cs:1703-1753); en CS cboVentas se fuerza a Emitidas (ConsultarVentas.cs:1201); WEB (nucleo/ventas.ts:861 type Filtros) solo tiene fechaInicial/fechaFinal/idAgente/cliente/folio: le faltan cboEmpresa, cboAlmacen, cboTipoDocumento, chkFolioMayorista y el filtro por producto (txtClave + cboAlmacenProducto); MOVIL solo consulta pedidos propios (mis_pedidos.dart POST api/ventas/consulta/pedidos)
- **buscar cliente (... F1)** (web) — ConsultarVentas.cs:909; en CS fuerza Identico=1 o sea solo por numero exacto (ConsultarVentas.cs:978); en web el cliente es un campo de texto libre sin buscador con rejilla
- **simular sin** (web) — ConsultarVentas.cs:2509; NO parte de una remision: abre captura en modo simulacion, deja capturar sin existencias y al guardar saca el reporte 1175 y cancela el pedido simulado; en web solo estan las validaciones previas y avisa que el reporte 1175 no esta portado (VentasPage.tsx:1441)
- **simular con** (web) — ConsultarVentas.cs:3693; parte de una remision ya hecha; mismo aviso en web
- **facturar** (web) — ConsultarVentas.cs:4630 valida mismo cliente, total distinto de cero, que no sea CV- y que no este cancelada y abre VentasFacturacion con ValidaOperadorProcesos (ConsultarVentas.cs:4778); el handler NO ramifica por empresa (lo que ramifica es el calculo dentro de VentasFacturacion); en web estan las validaciones y avisa que el timbrado se hace en Mac31 (VentasPage.tsx:1372)
- **procesar pedido** (web) — ConsultarVentas.cs:3090 btnProcesarPedido_click (minuscula en el nombre del handler); en web existe la accion pero cae en el aviso generico de no conectada (VentasPage.tsx:1343)
- **modificar pedido** (web) — ConsultarVentas.cs:2635 abre Ventas con el IDPedido; en web avisa que no esta conectada; en movil si se edita desde mis_pedidos/repartidor_home
- **cancelar pedido** (web) — ConsultarVentas.cs:3082; en web avisa que no esta conectada
- **cancelar pedido (panel pendientes de autorizar)** (web) — ConsultarVentas.Designer.cs:1052 es un SEGUNDO boton con otro Name apuntando al mismo btnCancelarPedido_Click: son DOS permisos distintos en la tabla de permisos para la misma accion
- **modificar pedido (panel pendientes de autorizar)** (web) — ConsultarVentas.Designer.cs:1067 segundo boton al mismo btnModificarPedido_Click; en rechazados Mac31 solo habilita este
- **autorizar pedido** (web) — ConsultarVentas.cs:3219 btnAutorizar_Click; el motivo de que un pedido caiga aqui difiere por empresa: TAURO lo marca por un solo contador de dias vencidos (Ventas.cs:2863) y ZARAGOZA por tres (acumuladores/aceites/cascos, Ventas.cs:2879) y la columna se rotula ACU/CASC/ACE en ZARA y DIAS VENC en TAU (ConsultarVentas.cs:2060-2073); en web avisa que no esta conectada; en movil si existe (pedidos_autorizar.dart)
- **rechazar pedido** (web) — ConsultarVentas.cs:3270 btnRechazar_Click; en movil pedidos_autorizar.dart NO tiene rechazar (solo autorizar), es la mitad de la pantalla

### ConsultarUsuarios

- **filtrar por estatus** (web/movil) — No hay boton buscar: cboEstatus_SelectedIndexChanged (ConsultarUsuarios.cs:60) relanza ConsultaUsuarios. En backend nuevo NO existe GET de usuarios Mac31 (UsuariosMacController solo escribe); GET api/usuarios es el usuario WEB con id guid. La pantalla web paginas/usuarios/UsuariosPage.tsx y el movil core_admin/users_screen.dart listan usuarios WEB, no los de Mac31
- **nuevo** (web/movil) — ConsultarUsuarios.cs:293. El web/movil dan de alta usuarios WEB (POST api/usuarios), no Mac31; el endpoint mac31 existe sin pantalla
- **modificar** (web/movil) — ConsultarUsuarios.cs:307
- **ver** (web/movil) — ConsultarUsuarios.cs:372 abre Usuarios en solo lectura
- **ver detalle (doble clic en rejilla)** (web/movil) — ConsultarUsuarios.cs:462
- **permisos** (web/movil) — ConsultarUsuarios.cs:209 abre UsuariosFormasProcesoPermisos. En el web paginas/usuarios/ModalPermisos.tsx y paginas/roles/ModalPermisosDeRol.tsx editan el modelo permisos-web (roles y overrides), NO la rejilla Forma/Proceso de Mac31; el endpoint de escritura mac31/procesos existe sin pantalla

### ConsultarProveedores

- **buscar** (web) — Menu.cs:1670 btnProveedores_Click + ValidaPermisos y Menu.cs:1684 ValidaOperadorProcesos. Menu.cs llama ValidaPermisos(form) y luego Globales.ValidaOperadorProcesos(form) (Utils/Globales.cs:84) por eso cada boton se habilita por su Name en n_Formas/n_Procesos. No hay GET de listado de proveedores en el backend nuevo (ProveedoresAdminController solo trae escritura y ProveedoresAnexosController los apoderados domicilios y documentos). En el web viejo existe docs/modules/custom/tauro/proveedores pero NO es este catalogo: pega contra api/Personas/consultar insertar actualizar cancelar reactivar y solo maneja el nombre; no tiene RFC domicilio telefonos email condiciones de credito ni descuento. En Movil (Movil/mac-mac-mobile) no existe: lib/modules/verticals solo trae mis_pedidos pedidos_autorizar repartidor_home repartidor_perfil y shared_mobile solo login home core_admin reportes.
- **limpiar** (web) — Menu.cs:1670 btnProveedores_Click + ValidaPermisos y Menu.cs:1684 ValidaOperadorProcesos. Menu.cs llama ValidaPermisos(form) y luego Globales.ValidaOperadorProcesos(form) (Utils/Globales.cs:84) por eso cada boton se habilita por su Name en n_Formas/n_Procesos. No hay GET de listado de proveedores en el backend nuevo (ProveedoresAdminController solo trae escritura y ProveedoresAnexosController los apoderados domicilios y documentos). En el web viejo existe docs/modules/custom/tauro/proveedores pero NO es este catalogo: pega contra api/Personas/consultar insertar actualizar cancelar reactivar y solo maneja el nombre; no tiene RFC domicilio telefonos email condiciones de credito ni descuento. En Movil (Movil/mac-mac-mobile) no existe: lib/modules/verticals solo trae mis_pedidos pedidos_autorizar repartidor_home repartidor_perfil y shared_mobile solo login home core_admin reportes.
- **nuevo** (web) — Menu.cs:1670 btnProveedores_Click + ValidaPermisos y Menu.cs:1684 ValidaOperadorProcesos. Menu.cs llama ValidaPermisos(form) y luego Globales.ValidaOperadorProcesos(form) (Utils/Globales.cs:84) por eso cada boton se habilita por su Name en n_Formas/n_Procesos. No hay GET de listado de proveedores en el backend nuevo (ProveedoresAdminController solo trae escritura y ProveedoresAnexosController los apoderados domicilios y documentos). En el web viejo existe docs/modules/custom/tauro/proveedores pero NO es este catalogo: pega contra api/Personas/consultar insertar actualizar cancelar reactivar y solo maneja el nombre; no tiene RFC domicilio telefonos email condiciones de credito ni descuento. En Movil (Movil/mac-mac-mobile) no existe: lib/modules/verticals solo trae mis_pedidos pedidos_autorizar repartidor_home repartidor_perfil y shared_mobile solo login home core_admin reportes.
- **modificar** (web) — Menu.cs:1670 btnProveedores_Click + ValidaPermisos y Menu.cs:1684 ValidaOperadorProcesos. Menu.cs llama ValidaPermisos(form) y luego Globales.ValidaOperadorProcesos(form) (Utils/Globales.cs:84) por eso cada boton se habilita por su Name en n_Formas/n_Procesos. No hay GET de listado de proveedores en el backend nuevo (ProveedoresAdminController solo trae escritura y ProveedoresAnexosController los apoderados domicilios y documentos). En el web viejo existe docs/modules/custom/tauro/proveedores pero NO es este catalogo: pega contra api/Personas/consultar insertar actualizar cancelar reactivar y solo maneja el nombre; no tiene RFC domicilio telefonos email condiciones de credito ni descuento. En Movil (Movil/mac-mac-mobile) no existe: lib/modules/verticals solo trae mis_pedidos pedidos_autorizar repartidor_home repartidor_perfil y shared_mobile solo login home core_admin reportes.
- **ver (solo lectura)** (web) — Menu.cs:1670 btnProveedores_Click + ValidaPermisos y Menu.cs:1684 ValidaOperadorProcesos. Menu.cs llama ValidaPermisos(form) y luego Globales.ValidaOperadorProcesos(form) (Utils/Globales.cs:84) por eso cada boton se habilita por su Name en n_Formas/n_Procesos. No hay GET de listado de proveedores en el backend nuevo (ProveedoresAdminController solo trae escritura y ProveedoresAnexosController los apoderados domicilios y documentos). En el web viejo existe docs/modules/custom/tauro/proveedores pero NO es este catalogo: pega contra api/Personas/consultar insertar actualizar cancelar reactivar y solo maneja el nombre; no tiene RFC domicilio telefonos email condiciones de credito ni descuento. En Movil (Movil/mac-mac-mobile) no existe: lib/modules/verticals solo trae mis_pedidos pedidos_autorizar repartidor_home repartidor_perfil y shared_mobile solo login home core_admin reportes.

### Menu

- **menu principal (4 grupos: OPERACION, CATALOGOS, ADMINISTRACION, REPORTES)** (web/movil) — Menu.Designer.cs: 72 botones. Cada entrada se habilita con Globales.ValidaPermisos(form) (Utils/Globales.cs:189). El web nuevo solo pinta 13 mosaicos; el movil 7 modulos declarados y sharedVerticalModules esta VACIO (vertical_modules.dart:4).
- **encabezado con fecha de operacion de ventas y de pagos** (web) — Globales.cs:582 si EsAlcaldiaMC>=1 la etiqueta dice 'Operacion' en vez de 'Ventas'; Globales.cs:594 EsAviacion==1 muestra solo FECHA; Globales.cs:598 EsAlcaldiaMC==0 muestra Ventas + Pagos; else solo Fecha. Menu.cs:227 oculta lblFechaOperacionPagos cuando EsAlcaldiaMC 1/2/3.
- **checador de asistencia por huella** (web) — btnBuscarHuella 'CHECADOR ASISTENCIA'. En Menu.cs:2053 la validacion de permisos esta COMENTADA: hoy cualquiera entra. El web tiene captura de huellas en empleados/ModalHuellas.tsx pero no el checador.

### UsuariosFormasProcesoPermisos

- **guardar** (web/movil) — UsuariosFormasProcesoPermisos.cs:135. Es la matriz Forma/Proceso que lee Globales.ValidaPermisos (Utils/Globales.cs:217). El web (ModalPermisos / RolesPage) y el movil (permission_users_screen, permission_roles_screen) manejan permisos-web por rol y override, no esta matriz. Es el equivalente legacy de las pantallas web de roles/usuarios/modulos: escribe el mapa forma/proceso de n_Formas/n_Procesos que es la lista autoritativa de permisos y que vive solo en la BASE, no en el repo. En Login.cs:554 ese mapa se carga a Globales.dtUsuarioActualFormasProcesos y de ahi lo leen Globales.ValidaPermisos (Globales.cs:217) y Globales.ValidaOperadorProcesos (Globales.cs:84). El perfil Administrador (ePerfil.Administrador) se salta TODA validacion (Globales.cs:87, :193, :220), asi que para un administrador la rejilla es decorativa
- **todos** (web/movil) — chkSeleccionar UsuariosFormasProcesoPermisos.cs:229 marca o desmarca todos los procesos de golpe
- **marcar/desmarcar acceso (clic en celda)** (web/movil) — UsuariosFormasProcesoPermisos.cs:242 CellClick y :267 CellDoubleClick alternan la casilla ACCESO

### ConsultarVentasPagos

- **nuevo pago (NUEVO PAGO)** (web) — ConsultarVentasPagos.cs:379 abre VentasPago; VOP aplicado desde ConsultarVentas.cs:3678; en web ModalPagos.tsx el boton existe con el permiso ventas.pagos.nuevo pero al pulsarlo avisa que el alta se hace en Mac31
- **filtrar por forma de pago** (web) — chkPagaConEfectivo_CheckedChanged ConsultarVentasPagos.cs:549 atiende los cuatro checks; el web lista los pagos pero no tiene estos filtros

### CobrosDetalle

- **devolucion (DEVOLUCION F9)** (web) — CobrosDetalle.cs:1865; en web la devolucion existe como pantalla propia pero no desde la hoja de cobro
- **cancelar remision (CANCELAR REMISION F7)** (web) — CobrosDetalle.cs:2658; en web la cancelacion existe en VentasPage pero no desde la hoja de cobro

### ConsultaEmpleados

- **excel** (web) — ConsultaEmpleados.cs:472 y la visibilidad en ConsultaEmpleados.cs:53: btnExcel SOLO se muestra en TAU, en ZARA queda oculto. En el web el boton EXCEL existe pero baja la tabla ya cargada en el navegador, no el reporte EmpleadosSueldosExcel de MacReportes
- **ver sueldos** (web) — chkVerSueldos ConsultaEmpleados.cs:541 pide CONTRASENA de la forma (ConfirmarContrasena con this.Name) antes de mostrar SUELDO y COMISION. En ZARA (ConsultaEmpleados.cs:196) ademas solo se muestran a los IDUsuario 1081 SAYURI, 1059 RICARDO y 3 JUAN y al resto se ocultan siempre; la rama TAU equivalente (ConsultaEmpleados.cs:224) esta COMENTADA asi que en TAU manda solo la contrasena. El web cambio el mecanismo: usa el permiso empleados.sueldo.ver y el backend ni manda los sueldos, no hay contrasena

### CapturaHuella

- **registrar huella** (web) — No hay boton: Guardar() se dispara solo cuando las capturas coinciden (CapturaHuella.cs:386 y :408). La misma pantalla sirve a USUARIO, EMPLEADO y CLIENTE segun el constructor (CapturaHuella.cs:54). El web solo cubre el caso EMPLEADO por api/checador; usuarios y clientes no tienen pantalla
- **consultar huellas registradas** (web) — CapturaHuella.cs:507; bloquea los combos de mano y dedo cuando el dedo ya esta registrado (linea 281)

### Login

- **cargar constantes de la empresa al entrar** (web/movil) — Globales.cs:458-471 carga TODO el comportamiento por empresa de la tabla de constantes. El web/movil solo reciben capacidades y modulos: NO reciben funcionalidad/EsCentroServicio/EsLuro/EsquemaPago/InventarioNegativo. Es el hueco raiz de las diferencias por empresa.

### Ventas

- **ver detalle (doble clic en rejilla de partidas)** (web/movil) — Ventas.cs:3595 gridDetalles_CellDoubleClick llama btnEditar_Click; en web y movil se toca el renglon para editarlo pero no hay doble clic como gesto

### VentasDevolucion

- **cargar partida (doble clic en rejilla de detalles)** (web) — VentasDevolucion.cs:821 gridDetalles_CellDoubleClick llama btnAgregar_Click; en web se toca el renglon pero no hay doble clic

### ConsultarVentasPagosUsados

- **cancelar pago (CANCEL PAGO F7)** (web) — ConsultarVentasPagosUsados.cs:2619; cs:2792 en ZARA si el pago trae IDBancoTransferenciaMovimiento>0 ademas cancela el movimiento bancario de la transferencia; el web solo cancela desde la consulta de pagos de la remision, no desde la hoja de cobro

### ConsultarCobros

- **pago con kilos (PAGO CON KILOS)** (web) — btnPagoConKilos_Click ConsultarCobros.cs:3298; en web el pago por kilos existe dentro de MultiPagoPage (precio por kilo y total de cascos) pero no como accion de la hoja de cobro

### Clientes

- **ver detalle de venta (doble clic en rejilla de saldos)** (web) — Clientes.cs:447. En el web el panel existe (paginas/ventas/PanelSaldosCliente.tsx) pero vive dentro de la pantalla de ventas, no en una ficha de cliente

### Usuarios

- **guardar** (web/movil) — Usuarios.cs:182. Usuarios.cs:62 solo en TAU hace VISIBLES las casillas PERMITIR GARANTIAS SIN COSTO y PERMITIR CORTE DEL DIA y las PREMARCA por IDUsuario a mano (garantias: 22 GISEL, 40 PAMELA, 1 ADMIN, 3 JUAN; corte del dia: 1065 HECTOR, 40 PAMELA, 1 ADMIN, 3 JUAN); en ZARA ambas quedan ocultas. Usuarios.cs:135 al EDITAR: en TAU el campo USUARIO queda editable y blanco, en ZARA se bloquea en gris (ReadOnly). El web/movil crean usuarios WEB con rol, no el usuario Mac31 con estas banderas

### Asistencias

- **cambiar empleado** (web) — cboEmpleado_SelectedIndexChanged Asistencias.cs:81. En el web no hay combo de empleado: solo un cuadro de texto por nombre o apellido

### ConsultaPrestamos

- **modificar** (web) — ConsultaPrestamos.cs:102 abre Prestamos en edicion con TODOS los campos (fecha del prestamo, monto del prestamo, fecha de inicio de pagos, monto de cada pago, motivo, pago inmediato, agregar a deuda). El web solo puede cambiar el MONTO DE CADA PAGO: el unico endpoint de edicion es PUT api/prestamos/{id}/monto-pagos

### Prestamos

- **guardar** (web) — Prestamos.cs:264; valida contra api/empleadosprestamo/validar antes de insertar. El alta en web esta completa; la edicion no (solo monto de cada pago). Sin ramas por empresa

### ReporteEmpleadoAsistenciaRecibos

- **cobranza pagadas de comisiones (automatico tras recibos, solo Zaragoza)** (web/movil) — RAMA EMPRESA ReporteEmpleadoAsistenciaRecibos.cs:145-150: solo se ejecuta si Variables.funcionalidad contiene ZARA; VerReporteDetalle ReporteEmpleadoAsistenciaRecibos.cs:152-220; web/movil tienen la clave cobranza_pagadas pero NO este encadenado con los empleados del recibo ni el filtro Param13 IDsEmpleado

### LectorQR

- **verificar (F8)** (web) — btnVerificar_Click (el boton se llama btnGuardar en el Designer con Text 'VERIFICAR F8'); en el web VIEJO existe Web/QR_Web/docs/modules/custom/aviacion/qr (login.html + main.js + proxy/server.js que pega a api/recaudaciones/consultar) pero es una app aparte solo de verificacion, sin el resto del modulo; Aviacion solo se habilita con Variables.EsAviacion==1 / EsAlcaldiaMC 2-3 (Utils/Globales.cs:469 y Menu.cs:304-320); no es Zaragoza ni Tauro


## Que se puede conectar hoy de verdad

El backend (`sacNet-8/ISL_Service/ISL_Service/Controllers/`) publica **700 endpoints en 84 controladores**
(extraidos de los atributos `[Http*]`; el inventario documentado en `migracion/inventario-nuevo.tsv`
cubre 248 de ellos). La columna `endpoint_nuevo` del TSV sale de cruzar cada accion contra esa lista.

La conclusion incomoda: **el backend va bastante mas adelante que los dos fronts**, y hay familias
completas de endpoints que no tienen ni una pantalla que las use.

### Endpoints listos y huerfanos (hay API, no hay pantalla)

| familia | endpoints | pantalla que falta |
|---|---:|---|
| `CatalogosAdmin` + `.Estructurados` | 84 | **ningun catalogo existe en el web nuevo**: las 270 acciones de catalogos estan a 0 |
| `AviacionCatalogos` | 39 | Aviacion no existe en ningun front |
| `ObraContratos` + `ObraCatalogosEscritura` + `Obra` | 61 | Obra no existe en ningun front |
| `CatalogosGenerales` | 28 | los catalogos solo se consumen como combos |
| `VentasUsadosOperaciones` | 21 | entrada/salida de cascos, venta a galleros, cobro de dinero y cascos |
| `ProveedoresAdmin` | 16 | proveedores existe solo en el web **viejo** |
| `VentasCobranzaOperaciones` + `VentasCobranzaConsulta` | 23 | cobranza no esta en el web nuevo |
| `VentasPagosOperaciones` | 13 | pagos nuevos (`ConsultarNuevosPagos`) no tiene pantalla |
| `VentasDescuentos` + `VentasDescuentosConsulta` | ~17 | liquidaciones de descuentos |
| `ClientesAdmin` + `ClientesAnexos` | ~15 | **clientes no existe**: solo `GET api/catalogos/clientes`, que no soporta ninguno de los 17 filtros de Legacy |
| `CambiosVigencia` | — | el controlador entero esta huerfano |
| `BancosTransferencias` | 6 | sin pantalla |
| `CortesAdmin` + `Cortes` | ~12 | **cortes no existe en ningun front**, ni normal ni CS |
| `VentasGarantias` | 9 | garantias no esta en el web nuevo (y en Legacy `ConsultarServicios` nunca funciono) |
| `UsuariosMac` | 7 | **solo escribe**: no hay `GET` de usuarios Mac31, asi que `ConsultarUsuarios` no tiene como listar |

### Huecos reales del backend (no hay API, y hace falta)

Esto es lo que **no se puede conectar aunque se haga la pantalla**:

- **Todo el modulo fiscal de salida**: no hay timbrado de nota de credito, ni descarga de XML, ni
  impresion de PDF de ningun CFDI, ni acuse de cancelacion. Solo existen `POST api/ventas/facturas` y
  `POST api/ventas/facturas/complemento-pago`.
- **Cambiar la fecha de operacion de VENTAS.** Solo existe `PUT api/constantes/fecha-operacion-pagos`.
  Es la fecha con la que se capturan todas las ventas del dia: sin eso el web no puede operar en una
  fecha distinta a hoy. **Prioridad alta y es un bloqueador de uso diario.**
- **Ningun reporte de cortes** (CorteDiario, CorteCS, CorteDiarioDetalleMAC, CortesGastos,
  ComprobanteMovimientoCascos*, VentaCascos). Solo el generico `POST api/reportes/ventas/legacy/generar`.
- **Los 6 reportes de nomina** (asistencias 1025, solo asistencias 1024, inasistencias 1190,
  vacaciones 1027, recibos de nomina 1020 y el encadenado de comisiones de Zaragoza): ninguno tiene
  endpoint; los recibos van a `/EmpleadosTipoSueldoRecibos/Index` de MacReportes.
- **El reporte 1175 (`Simular`)**: sin el, la simulacion de descuento del web no enseña el numero que
  se buscaba.
- **GET de listado** de agentes, autos, tipos de gasto (solo existe el combo del corte) y tipos de usado.
- **Cancelar recordatorio**, imprimir/excel de `ConsultarAutos`, domicilios y apoderados de proveedores
  de Obra, documentos de proyecto, sincronizacion offline de cajas de Aviacion.
- **`ScanForm` no es portable**: depende de WIA y de rutas locales. En navegador hay que rehacerlo como
  subida de archivo o camara.
- **Cancelar corte CS**: solo existe `POST api/cortes/{id}/cancelar`.

### Dos rutas publicadas con el prefijo duplicado

Aparecieron al mapear y quedaron en el TSV tal como estan publicadas, porque es lo que responde el servidor:

```
POST api/obra/catalogos//api/obra/proveedores
PUT  api/obra/catalogos//api/obra/proveedores/{id:int}
POST api/ventas/pagos//api/ventas/reasignar
GET  api/ventas/consulta//remision
```

### Un aviso sobre la columna `existe_web`

En las filas de **reportes**, `existe_web = si` significa que el reporte existe en el **web viejo**
(`docs/modules/verticals/reportes`, 53 de 71). En el **web nuevo (`docs2`) no hay modulo de reportes
en absoluto**. Es la unica parte del inventario donde "si" no quiere decir "ya esta en docs2", y
conviene no leerlo mal al planear.

## Lo que esta roto, muerto o sin candado en Legacy

Esto salio de leer el codigo, no de probarlo. Importa porque reconstruir el front es la oportunidad
de no copiar estos defectos — y porque alguno es un agujero de seguridad vivo hoy.

### Pantallas sin ningun permiso

- **`ConsultarColonias` permite editar el catalogo a cualquiera.** No esta en el menu: se abre como
  buscador (`ConsultarColonias(true)`) desde `Empresas.cs:580`, `Clientes.cs:1690`,
  `ClientesFacturacion.cs:371`, `Proveedores.cs:530`, `ProveedoresGam.cs:396` y
  `MCProveedores/ProveedoresDomicilios.cs:317`. **Nadie llama `ValidaOperadorProcesos` ni
  `ValidaPermisos` sobre ella**, asi que sus botones NUEVO y MODIFICAR quedan habilitados para todos.
  Desde el buscador de colonias se altera el catalogo sin permiso.
- **`FacturamaCatalogos` no valida nada** (`Menu.cs:2088`): quien vea el boton consulta el PAC.
- **El CHECADOR DE ASISTENCIA no valida nada**: el bloque de permisos esta comentado
  (`Menu.cs:2053-2072`).
- **El doble clic esquiva el candado.** `ConsultarRecordatorios.cs:672` abre el detalle sin pasar por
  `btnVer`/`btnModificar`, que son los que llevan el permiso.

### Pantallas inalcanzables (el codigo existe, el boton no se puede ver)

- **`ConsultarDocumentos`**: `Menu.cs:155` pone `btnDocumentos.Visible = false` para todas las empresas
  y la linea que lo reactivaria esta comentada en `Menu.cs:185`. El handler (`Menu.cs:2225`) esta completo.
- **`ConsultarProyectos`**: `btnProyectos.Visible = false` en `Menu.cs:157`, `= true` en `Menu.cs:285`
  (si `EsAlcaldiaMC` 2 o 3), y el segundo bloque 2|3 lo vuelve a poner `false` en `Menu.cs:314` y hace
  `return`. Nunca se ve.

### Pantallas que nunca funcionaron

- **Servicios / Garantias esta a medio hacer.** `ConsultarServicios` **no se instancia en ninguna parte**.
  Su rejilla no tiene columnas configuradas; `btnVer`, `btnImprimir`, `btnLimpiar` y `btnCerrar` no tienen
  handler. Y en la forma `Servicios`, `btnGuardar`/`btnAgregar`/`btnEditar`/`btnEliminar` tampoco:
  solo se habilitan (`Servicios.cs:331-335`). **La pantalla no guarda nada.**
  Ojo con esto: el backend nuevo **si** tiene `POST api/ventas/garantias` y una familia completa de
  endpoints, asi que si alguien pide "migrar garantias", la referencia no puede ser esta pantalla.

### Botones muertos

| donde | que pasa |
|---|---|
| `ConsultarCambiosVigencia.cs:474` | `btnRechazar` **arranca con `return;`**: las ~70 lineas siguientes son inalcanzables |
| `ConsultaEmpleados.cs:575` | `btnMostrarSueldos_Click` esta vacio |
| `ConsultarRecaudaciones.cs:884` y `:892` | `btnVer` y `btnRetiro` con handler vacio |
| `MenuAviacion` | `btnLiquidacionesDescuentos` no tiene handler |
| `Menu.cs:1244` | liquidaciones de agentes/camionetas: permisos comentados y la forma es un cascaron de 48 lineas |

Handlers sin boton en el Designer (codigo que no se puede disparar):
`MCConsultarContratos.btnGrupos`, `MCConsultarContratosConceptos.btnEliminar`,
`MCConsultarContratosGrupos.btnAceptar/btnCargarFormato/btnDescargarFormato`,
`MCConsultarContratosPeriodos.btnAceptar`, `MCConsultarContratosEstimaciones.btnAceptar`.

### Trampas para quien migre: el permiso depende del `Name` del boton, no del handler

Porque `ValidaOperadorProcesos` casa por `Name` del control, estos desajustes importan:

- `FechaOperacionVentas.cs:236`: el handler se llama `btnGuardar_Click` pero el boton es **`btnGuardarDias`**.
  El permiso es `FechaOperacionVentas.btnGuardarDias`.
- `UsuarioFecha.Designer.cs:90`: `btnAgregar` ("AGREGAR FECHA") esta cableado a **`btnGuardar_Click`**.
- `ConsultarTipoRecordatorios.cs:462`: el handler del filtro se llama `txtTipoGasto_KeyDown` y el control
  es `txtTipoRecordatorio`.
- `ConsultarClientes.Designer.cs:878`: `btnListasPreciosAceites` comparte handler con `btnListasPrecios`
  y se distinguen por `(sender as Button).Name`.
- `EconomicosAmortizaciones.cs:400`: los 6 botones comparten un unico `btnAgregar_Click`.
- `ReportesAviacion` **no usa `Forma.Proceso`**: llama `ValidaPermisos(string)` con cadenas literales
  (`"Cobranza Cobranza Consolidada"`, `"Contabilidad Poliza de Ingresos y Fianzas"`...). Es un tercer
  esquema de permisos, incompatible con los otros dos.

### Un boton, dos pantallas (hay que decidir en el front nuevo)

| evidencia | condicion | abre |
|---|---|---|
| `Menu.cs:2516` / `:2544` | `EsCentroServicio` 0 / 1 | `ConsultarCortes` / `ConsultarCortesCS` |
| `Menu.cs:2107` / `:2133` | `EsAlcaldiaMC` 1 / 2 | `MCConsultarProveedores` / `ConsultarProveedoresGam` |
| `Menu.cs:1639` / `:1700` | — | **dos** entradas de menu (EMPRESAS y DESCUENTOS) abren la misma `ConsultarEmpresas` |

### Bloqueos silenciosos (el boton se ve activo y no hace nada)

`ConsultarTipoUsados.cs:355` y `:376`: con `EsCentroServicio == 1 || EsLuro == 1` hace `return` **sin
mensaje**. En las pantallas equivalentes de Productos (`ConsultarCategorias.cs:324,348`,
`ConsultarGrupoCategorias.cs:283,307`, `ConsultarMarcas.cs:285`) si avisa. Es inconsistencia, no diseño.

### Comprobaciones duplicadas / codigo muerto en el menu

`Menu.cs:611` y `Menu.cs:620` validan **lo mismo** (centro de servicio sobre `ConsultarGrupoCategorias`);
el primer `return` hace que el segundo bloque nunca se alcance.

### El doble clic se salta el candado por empresa

Esto aparece en **4 pantallas de catalogo** y es un defecto real, no una rareza de estilo.
`btnModificar` lleva el `if ((EsCentroServicio == 1) || (EsLuro == 1))` que corta la modificacion,
pero el `dataGridView1_CellDoubleClick` llama directo a `VerX(soloLectura: false)` y **no repite esa
comprobacion**:

| pantalla | doble clic sin candado |
|---|---|
| `ConsultarProductos.cs` | **:940** |
| `ConsultarCategorias.cs` | **:282** |
| `ConsultarGrupoCategorias.cs` | **:241** |
| `ConsultarMarcas.cs` | **:243** |

Y hay un agravante: `Menu.cs` frena al centro de servicio **antes** de abrir la pantalla (lineas 611,
653, 689, 725, 760), pero **no frena a Luro**. Asi que para una instalacion con `EsLuro == 1` el unico
freno son esos `if` de dentro de la forma — y el doble clic los esquiva. **Con `EsLuro = 1` se pueden
modificar productos, categorias, subcategorias y marcas a pesar del bloqueo.**

Es tambien un aviso general de diseño para el front nuevo: en Legacy, **cada regla de negocio esta
escrita dos veces** (en el boton y en el doble clic) y en estos cuatro casos la segunda copia se olvido.

### El permiso se puede apagar abriendo la pantalla de otra forma

`DetalleDescuentos.cs:81` dice `if (!this.simular) Globales.ValidaOperadorProcesos(this);`.
O sea: cuando la pantalla se abre en modo simulacion (desde `Ventas.cs:5088` y `VentasSimular.cs:547`)
**todos sus botones quedan habilitados sin consultar ningun permiso**.

### `ValidaOperadorProcesos` solo apaga `Button`

Por eso las casillas de rejilla (`chkSeleccionar`, `chkSeleccionarQuitarCeros`, `chkTodos`...) y los
dobles clic **nunca se bloquean por permiso** en ninguna pantalla. Cualquier front nuevo que replique
el modelo de permisos de Legacy tiene que decidir que hace con las acciones que no son botones.

### La puerta trasera del `Tag` ya esta ocupada

`ValidaOperadorProcesos` no deshabilita un boton con `Tag != null`. Y resulta que
`Proveedores.cs:68/107/538` y `ProveedoresGam.cs:61/99/404` usan `btnBuscarColonia.Tag` **para guardar
el IDColonia**. Si algun dia se le agrega permiso por boton a esas formas, ese boton va a quedar
habilitado siempre, y el motivo no va a estar a la vista.

### Mas rutas del backend nuevo con el prefijo duplicado

Ademas de las de Obra ya citadas:

```
POST api/compras//api/inventario/transito                 (ComprasController.ProcesarTransito)
POST api/ventas/pagos//api/ventas/usados-credito/kilos
```

Y `ProveedoresPagosController` publica sus rutas con el token literal **`api/[controller]`** sin
resolver.

### Nombres de pantalla que dicen lo contrario de lo que son

Importa porque el permiso se resuelve por el nombre de la clase y del control:

- la clase **`GrupoCategorias`** muestra el titulo "CATEGORIAS"; la clase **`Categorias`** muestra
  "SUB CATEGORIAS". Estan cruzadas.
- `ConsultarCategorias.cs:322/345` avisa *"NO TIENE PERMITIDO ALTA O MODIFICACION DE CATEGORIAS"*
  aunque la pantalla es de **sub**categorias.
- `ProveedoresGam` se titula "PROVEEDORES" pero la pantalla que lo abre habla de "CONTRATISTAS".
- `ConsultarCompras.btnNuevo` tiene el handler `btnNuevaCompra_Click`: el permiso es
  `ConsultarCompras.btnNuevo`, no el nombre del handler.
- `Gasolina.lblAuto.Text = "Repartidor"`.
- `DetalleDescuentos` tiene `tabPage1` y `tabPage2` con el mismo texto "CATEGORIAS", y `tabPage1`
  **se elimina siempre** en `Load:134`: es una pestaña muerta.

### Filtros que existen y no se pueden usar

- `InventarioTransito.cboCentroServicio` esta en el Designer y **no aparece en ninguna linea del `.cs`**.
- `InventarioTransito.cboEstatus` queda `Enabled = false` (`Load:43`): solo se ve "EN TRASITO" (con la errata).
- `cboSubCategoria` se oculta con `Visible = false` en el `Load` de `ConsultarProductos` y de
  `ConsultarAjustesInventarios`.
- `AjustesInventarios.cboEmpresa` se llena en `Load:58` y **nunca se lee**.
- `ConsultarProductos.SetColumnas` tiene comentadas en bloque las columnas `precioLista2` ("P. GARAN")
  y `precioListaCS` ("P. C SERV"): existen en el DTO y nunca se ven.

### Un guardado que puede dejar el ajuste a medias

`AjustesInventarios.cs:589` (`btnGuardar_Click`) manda **un POST por renglon** y ante un error hace
`continue`. Puede guardar medio ajuste de inventario, poner `guardo = true` y cerrar la forma como si
todo hubiera salido bien. Si esto se migra tal cual, se migra el bug; deberia ser una sola transaccion.

### Mas validaciones comentadas

| donde | que se apago |
|---|---|
| `Productos.cs:443-449` | la confirmacion "¿ESTA CORRECTA LA INFORMACION?" antes de guardar un producto |
| `InventarioTransito.cs:404-421` | la validacion de "¿hay alguna fila marcada en SELECCIONAR?"; hoy opera sobre la fila seleccionada |
| `ConsultarProductos.cs:199` | `if (IDUsuario == 3)` que mostraba FACTURAMA; el boton quedo oculto para siempre |
| `GenerarPedido.cs:87` | marca `chkSeleccionar.Checked = true` pero deja comentado el `CheckedChanged`: el check se ve marcado sin aplicar su efecto |
| `Productos.cs:616` | la variante de `DescripcionSat` que incluia la categoria |

### Fuera del inventario de pantallas, pero visto al leer la sesion

`Utils/Variables.cs:24-26` lleva `APP_TOKEN`, `APP_CORREO_ELECTRONICO` y `APP_PWD` escritos a mano en el codigo.

### Un bug del backend nuevo que aparecio al mapear endpoints

`ObraCatalogosEscrituraController.cs` publica las rutas con el prefijo duplicado:
`POST api/obra/catalogos//api/obra/proveedores` y `PUT api/obra/catalogos//api/obra/proveedores/{id:int}`.
En el TSV quedaron tal cual estan publicadas.

## Lo que no se pudo determinar leyendo codigo

No se invento nada. Estas son las preguntas que quedaron abiertas y a quien hay que hacerselas.

### 171 filas con `?` en `aplica_zaragoza` / `aplica_tauro`

| modulo | filas con `?` | por que |
|---|---:|---|
| Catalogos | 133 | son los catalogos de Obra/MC: dependen de `EsAlcaldiaMC` (1/2/3), que **no es Zaragoza ni Tauro**. Son mutuamente excluyentes con los catalogos normales |
| Ventas | 24 | ramas cuya condicion esta **comentada** en Legacy, sobre todo las 10 de `Ventas.btnHuella` ("CLIENTE MATRIZ F7") |
| Inventario | 6 | dependen de `EsCentroServicio == 1` o `EsLuro == 1`, instalaciones que no estan levantadas |
| Reportes / Shell / Aviacion | 8 | idem, o reportes inalcanzables en una de las dos empresas |

**Lo que hace falta para cerrarlas:**

1. **Una instalacion de centro de servicio.** `EsCentroServicio` vale 0 en las dos bases que existen,
   y las bases `MacZCS*` no estan en el servidor. Hay ~150 ramas que no se pueden comprobar sin eso.
   Mientras no exista, toda esa funcionalidad es codigo que nadie puede ejecutar ni verificar.
2. **Saber si `EsLuro` sigue vivo.** `BDLuro` apunta a `[MacL].[dbo].` en MacZ y la base no esta
   restaurada; `sp_n_DevolucionVentaLuro` no existe en Produccion_svr. Si Luro ya no opera, 13 ramas
   y varios bloqueos de catalogo se pueden borrar en vez de migrar.
3. **Saber si `EsAlcaldiaMC` (Obra/MC) sigue vivo.** Son **132 acciones de Obra + 133 de catalogos MC**,
   casi un cuarto del inventario, y hoy el menu de Obra es inalcanzable
   (`ConsultarProyectos` no se puede abrir, ver la seccion de hallazgos). Si ese producto ya no se
   vende, es la poda mas grande disponible.
4. **`Ventas.btnHuella`**: el boton "CLIENTE MATRIZ F7" tiene sus 10 ramas de visibilidad comentadas.
   Hay que preguntarle a alguien que opere las dos empresas en que casos debe verse.
5. **Las listas blancas por `IDUsuario`.** Legacy tiene decisiones de negocio escritas como numeros de
   usuario: `Usuarios.cs:62` (22, 40, 1, 3 / 1065, 40, 1, 3), `ConsultaEmpleados.cs:196` (1081, 1059, 3),
   `Empleados.cs:215` (1, 3, 40, 9), `ConsultarGarantias.cs:1887` (22, 40, 1, 3),
   `ReportesMenu.cs:1812` (1059, 1060, 3), `Menu.cs:328` (3, 40), `Globales.cs:297` (40).
   **Ninguna de esas es una regla, son parches.** Antes de migrarlas hay que decidir cual es el permiso
   que representan; si se copian como IDs, el front nuevo nace con el mismo problema.

### Las columnas de salida de los reportes

En las 204 filas de reportes, las columnas **no son determinables desde el front**: `ReportesMenu`
manda parametros y abre una URL; el HTML lo arma el SP mas la plantilla del lado de MacReportes.
Para esas filas se registro el `IDReporte`/enum, la clave web-movil y el SP. **Las columnas hay que
sacarlas de las plantillas de MacReportes**, que no estan en este repositorio.

### El catalogo de permisos

Las 1,651 filas traen el permiso que **les corresponde** (`Forma.btnNombre`), pero la lista de los que
**existen de verdad** vive en `n_Formas`/`n_Procesos` en la base. Hay que leerla para saber cuales
estan dados de alta y cuales no, y cuantos de los 556 `(sin permiso)` son un hueco real y cuantos
simplemente nunca se necesitaron.

## Por donde conviene empezar

Esto no es un plan, es lo que se desprende de los numeros. Tres criterios cruzados: uso diario,
endpoints ya listos y tamaño.

**1. Lo que ya tiene API y es de uso diario** — aqui solo falta el front, y es donde el esfuerzo rinde mas:

| modulo | acciones que faltan | endpoints listos |
|---|---:|---:|
| Cobranza (hoja de cobro, cobranza, concentrados) | 116 | 85 de 116 (**73%**) |
| Tesoreria (cortes, bancos, cascos) | 61 | 36 (59%) |
| Clientes | 36 | 21 (58%) |
| Inventario (ajustes, transito) | 53 | 26 (49%) |

**2. El bloqueador que hay que resolver antes de que el web pueda operar de verdad:**
no existe endpoint para **cambiar la fecha de operacion de ventas**. Es la fecha con la que se
capturan todas las ventas del dia. Sin eso, el web solo puede trabajar "en hoy", y Legacy no.

**3. El hueco de raiz de las diferencias por empresa:** hoy el web y el movil reciben capacidades y
modulos, pero **no reciben `funcionalidad`, `EsCentroServicio`, `EsLuro`, `EsquemaPago` ni
`InventarioNegativo`** (`Globales.cs:458-471` los carga en Legacy; `GET api/capacidades` y `GET api/me`
no los exponen). Mientras eso no se publique, **cualquier pantalla nueva solo podra implementar una de
las dos empresas**, y el error no va a aparecer en pruebas: va a aparecer cuando la otra empresa use
la pantalla.

**4. Decisiones de poda que valen mas que cualquier sprint.** Antes de escribir codigo conviene
preguntar si siguen vivos Obra/MC (**265 acciones**), Aviacion (**247**) y Luro. Son **512 acciones**,
un 31% del inventario, en modulos que hoy estan inalcanzables o sin levantar.

**5. Lo que no hay que migrar** (esta en la seccion de hallazgos): `ConsultarServicios`/`Servicios`
nunca guardo nada, `VentasFacturar` y `ConsultarVentasNotasCredito` no se instancian,
`ReporteCobranza` y `ReporteVentasPagos` tienen los handlers vacios, y las liquidaciones de
agente/camioneta no se abren ni se pueden guardar.

**6. Y tres defectos que conviene arreglar en Legacy o no heredar:** el doble clic que se salta el
candado por empresa en cuatro catalogos, el guardado por renglon de `AjustesInventarios` que puede
dejar un ajuste a medias, y `ConsultarCortesCS.cs:656` pegandole al endpoint de cancelacion equivocado.
