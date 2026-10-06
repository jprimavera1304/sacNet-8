# Reportes: mapa de cobertura Legacy -> sacNet-8

Hecho el 6-oct-2026 leyendo el codigo y **ejecutando** contra el backend local
(`http://127.0.0.1:5055`, base `Produccion_svr` = Tauro, usuario `BancoPruebas`).
Todo numero de esta pagina sale de una consulta o de una llamada HTTP real.
El detalle reporte por reporte esta en `reportes-cobertura.tsv` (229 filas).

---

## 1. La respuesta corta

**Tu sospecha era correcta, y por mas de lo que pensabas.** El motor es generico;
el catalogo no es el cuello de botella principal; y los 228 renglones de
`dbo.Reportes` **no son 228 reportes**: son 149 reportes vivos y 79 fosiles de la
epoca de Crystal Reports.

| | reportes |
|---|---|
| Renglones en `dbo.Reportes` (Tauro) | **228** |
| Renglones en `dbo.Reportes` (Zaragoza) | **229** (solo 1 de mas: el 1202) |
| **Sin `spV2`** -> invisibles para el motor **por datos, no por codigo** | **79** |
| Con `spV2` pero cuyo SP **no existe en ninguna de las dos bases** (vertical Aviacion/Contabilidad) | **21** |
| **Reportes vivos** (con `spV2` y con su SP presente) | **128** |
| **Reportes que Mac31 de verdad le ofrece al usuario** (`ReportesMenu.cs`) | **99** |
| **Comprobados funcionando: devolvieron un PDF real** | **66** |
| De los 99 del menu, ya funcionan | **65 (66 %)** |
| De los 99 del menu, fallan por el SP de la base LOCAL (no por el motor) | **9** |
| De los 99 del menu, necesitan codigo nuevo de verdad | **16** (12 parametros + 4 catalogo) |
| De los 99 del menu, no aplican (vertical Aviacion) | **6** |
| De los 99 del menu, **no tienen renglon en `dbo.Reportes`** (roto tambien en legacy) | **3** |

**El catalogo nuevo no tiene 43 entradas: tiene 53** (`ReportesCatalogo.cs` cuenta
53 `Ventas(...)` / `Grupo(...)` / `new(...)`, y su propio doc-comment dice 53).
Esas 53 claves resuelven a **76 `IDReporte` distintos**, porque una clave elige
entre varias variantes segun `formato` / `idGrupoCategoria` / `dineroCascos`.
Y el motor conoce **84 constantes de `IDReporte`**.

Asi que la cobertura real no es "43 de 228" (19 %). Es **65 de los 99 que el
usuario ve (66 %)**, y con 16 cambios chicos sube a **90 de 99 (91 %)**.

### La evidencia

78 generaciones en la primera pasada + 31 reintentos. Ejemplos textuales:

```
POST /api/reportes/ventas/legacy/generar {"reporteKey":"cobranza_detallado"}
  -> 200 {"idReporte":1172,"nombreReporte":"Reporte de Cobranza",
          "storedProcedure":"sp_n_rptVentasSaldosGlobalesZara"}
GET  .../ReportesV3W?psp=...   -> 200 application/pdf  347 969 bytes
POST .../concentrados/generar  -> idReporte 1185  -> PDF  61 384 bytes
POST .../facturas/generar      -> idReporte   24  -> PDF  75 298 bytes
POST .../remisiones/generar {"estatusFolio":"saldo_favor"} -> idReporte 19 -> PDF 75 475 bytes
POST .../legacy/generar {"reporteKey":"accesos"}           -> idReporte 1395 -> PDF 276 795 bytes
```

66 `IDReporte` distintos entregaron PDF. La lista completa esta en el TSV
(`clasificacion = ya-funciona`; la columna `notas` trae el tamano y el SP).

### La prueba de que el limite es el candado de ids y nada mas

Hay renglones viejos en `dbo.Parametros` que **Mac31 escribio** (septiembre 2026).
Llame al GET anonimo con esos `psp`, sin pasar por el POST del backend:

```
GET ReportesV3W?psp=266392  (IDReporte 1230, Gasolina Detallado)  -> 200 PDF 69 856 B
GET ReportesV3W?psp=266399  (IDReporte   10, Venta Acumuladores)  -> 200 PDF 90 171 B
GET ReportesV3W?psp=266381  (IDReporte 1222)  -> 500 "El psp no corresponde a un reporte soportado."
GET ReportesV3W?psp=266378  (IDReporte 1001)  -> 500 idem
GET ReportesV3W?psp=266377  (IDReporte    5)  -> 500 idem
```

Dos cosas quedan probadas de una vez:

1. **El motor nuevo renderiza parametros guardados por Mac31** sin tocarlos: el
   contrato con legacy es el renglon de `Parametros`, no el DTO del web.
2. Lo unico que rechaza al 1222 es la **lista blanca de ids** en
   `ReportesVentasRepository.cs:401` (`IsGenericVentasReporte`) y el `throw` de
   `:402`.

---

## 2. Que limita de verdad la cobertura (en orden)

Los candidatos que planteaste, resueltos uno por uno:

| candidato | es el limite? | evidencia |
|---|---|---|
| **Los parametros que cada SP necesita** | **SI, el principal** | `BuildLegacyVentasParamsAsync` (`ReportesVentasRepository.LegacyParams.cs:428-899`) es un `switch` escrito a mano con 72 ids y un `default: throw new InvalidOperationException("Reporte de ventas no soportado.")` (`:896-897`). |
| **La lista blanca de ids del render** | **SI, el segundo** | `IsGenericVentasReporte` (`LegacyParams.cs:237-325`, 68 ids) mas las 6 `Is*Reporte` de familia. Bloquea el GET aunque los parametros ya esten guardados (probado arriba con el 1222). |
| **El catalogo `ReportesCatalogo.cs`** | **SI, pero es la capa mas barata** | `ReportesVentasController.cs:239-243` **prohibe** mandar `idReporte` en el body y exige que `reporteKey` este en el catalogo. Es un candado real de HTTP, pero abrirlo cuesta 2 lineas por reporte. |
| **Los permisos** | **NO** | El endpoint exige `perm:reportes.ver_modulo` y luego el permiso del reporte; 5 reportes del catalogo ya van con `SinPermiso = null` y **no se bloquean**. Un reporte nuevo puede entrar con `null` y funcionar el dia 1. |
| **Las variantes de plantilla (`Aspx`)** | **NO. Es un no-problema** | De los 149 reportes con `spV2`, `Aspx` solo vale `rptTemplate` (127), `3columnas` (13) o vacio (9). Los tres caminos ya existen (`ReportesVentasRepository.cs:496-510`). **No hace falta ningun generador nuevo.** |
| **Las listas de columnas de Excel** | **NO. Es cosmetico** | `BuildLegacyExcelTable` (`DataHelpers.cs:174-201`) termina en `_ => Array.Empty<LegacyExcelColumn>()` y entonces hace `source.Copy()`: sale el xlsx con los nombres crudos del SP. Comprobado: `accesos` (1395, sin lista) -> `200 xlsx 7 516 B` con encabezados `FechaInicial, FechaFinal, Movimiento, UsoContrasenaAutorizacion, ContrasenaCorrecta, Usuario`. Igual con `cortes` (1320) y `garantias` (1235). |
| **Las plantillas que faltan** | **NO existe ese caso** | 0 reportes clasificados `necesita-plantilla`: todos los que tienen `spV2` tienen filas en `Template_Html` + `tipo_template_html`. |

### El plan, de lo que mas desbloquea por menos trabajo

**Paso 1 - cuatro ids que son UNA LINEA cada uno (4 reportes).**
Son variantes del **mismo `spV2`** y de la **misma forma de parametros** que un
reporte que ya funciona; basta agregar la etiqueta `case` a la rama existente y
el id a `IsGenericVentasReporte`:

| id | nombre | ya existe la rama de | por que es una linea |
|---|---|---|---|
| 1160 | Cobranza Detallado | 1161 (`cobranza`) | mismo `sp_n_rptVentasSaldosGlobales`, mismas 3 plantillas. **Y es el que usa el nodo real de Mac31** (`nodeEmpresasVentasCobranza`), mientras el motor eligio el 1161. |
| 1355 | Ventas Cobros Dinero y Cascos | 1360 / 1365 | mismo `sp_n_rptVentasCobrosDineroCascosGlobales`, 4 plantillas. Es el nodo de Mac31 (`nodeEmpresasVentasCobrosDineroCascos`); el motor solo tiene las dos mitades. |
| 118 | Clientes Acum Moto Lub Mensual | 116 / 121 | mismo `sp_n_rptClienteCompraAcumMotoLubMensual`. |
| 119 | ...Mensual Por Categoria | 116 / 121 | idem, otra plantilla. |

**Paso 2 - cuatro reportes a los que solo les falta la clave (4 reportes).**
Ya tienen parametros y ya estan en la lista blanca. Falta `clave` en
`ReportesCatalogo.cs` mas rama en `ResolveReporteLegacyVentas` (mas permiso, o `null`):
`72` (Pagos Detallado Centro de Servicio), `77` (Pagos Detallado TAURO),
`1375` (Estado de Cuenta DINERO), `1380` (Estado de Cuenta CASCOS).
En Mac31 los cuatro se eligen con un combo dentro de la forma, no con un nodo.

**Paso 3 - ocho casos de parametros nuevos (8 reportes).** Dos racimos:

* **Listas de precios personalizadas de Tauro** - `1243`, `1244`, `1246`, `1247`.
  Cuatro `spV2` distintos (`sp_n_rptListasPreciosTauroTodas`,
  `...TodasMenosTauro`, `..._CRO_GON_XT_LTH_CL_AM_FP_DY`, `..._FP_AM_LTH`) pero
  **todos cuelgan de la misma forma de Mac31** que 1240/1241, cuyo `case` ya esta
  escrito con 17 parametros (`LegacyParams.cs:883-894`). Hay que confirmar la
  firma de cada SP con `sys.parameters` y reusar el bloque.
* **Centros y varios** - `1415` (Centros Pagos Entradas y Salidas, mismo molde que
  1410/1445), `1420` (Centros Garantias, molde de 1235), `1236` (Registro de
  Garantias, molde de 1235), `1250` (Control Acumulado de Estimaciones, molde propio).

Despues de los pasos 1-3: **de 65 a 81 reportes del menu funcionando (82 %)**, con
menos de 40 lineas de C# y ningun cambio de arquitectura.

**Paso 4 - arreglar tres ids fantasma (bug, no cobertura).** Tres constantes del
motor **no existen en `dbo.Reportes` de ninguna de las dos bases**:

```
240  ReportePedidoProductos  -> hoy: POST inventario_actual + inventarioGenerarPedido
                                + idGrupoCategoria 3 responde
                                500 "Reporte no encontrado en sp_ConsultaReportes."  (COMPROBADO)
76   ReporteVentasPagosTotalizadoCS
1242 ReporteListasPreciosCosto -> y el nodo nodeAdministracionListasPreciosZaragoza
                                  de Mac31 tambien apunta a 1242: esta roto en legacy igual.
```

Candidato: el pedido de productos deberia ser `1136`/`1137` (`Pedido ACUMULADORES`
comparte `sp_n_rptInventarioPedidosAcumuladores` con el 235).

**Paso 5 - 38 listas de columnas de Excel.** 38 de los 66 reportes que ya funcionan
sacan el xlsx con los nombres crudos del SP. Es trabajo de transcribir encabezados,
se puede hacer a goteo y no bloquea a nadie.
Y hay una que ya esta escrita y nadie usa: `VentaAcumuladoresExcelColumns`
(`ReportesVentasRepository.Constants.cs:100`) **no aparece en ningun lado** del
`switch` de `BuildLegacyExcelTable`, que empieza en `ReporteVentaProductos`. O sea
que el Excel del reporte estrella (10, Venta de Acumuladores) sale con los nombres
crudos del SP teniendo la lista lista. Es un `case` de una linea.

**Lo que NO hay que hacer:** generadores de plantilla nuevos (0 casos), tocar
permisos para abrir reportes (`null` ya sirve), ni escribir un motor por reporte.

---

## 3. Los pares de reportes "equivalentes"

Primero una correccion a la premisa: **las dos tablas `Reportes` son casi
identicas**, no una por empresa.

* Tauro 228 renglones, Zaragoza 229. El unico id que falta en Tauro es **1202**
  (`Remision Zaragoza Generico`).
* Solo **2** ids tienen distinto `SP`/`spV2` entre bases (ver riesgos).
* Las dos bases tienen `Modern_Spanish_CI_AS`, misma coleccion.

O sea: **el par no se resuelve eligiendo base, se resuelve eligiendo id en tiempo
de ejecucion** (por `Constantes.Funcionalidad` / `EsCentroServicio`, o por el combo
de la forma). Y el mecanismo es bonito: **varios `IDReporte` comparten el mismo
`spV2` y lo unico que cambia es el grupo de `Template_Html`.** El id elige el
PAPEL, no los datos.

Los pares que preguntaste:

| par | veredicto |
|---|---|
| **1240 / 1241** | Si son el mismo reporte. Los dos usan `sp_n_rptListasPrecios`. 1240 "Listas de Precios" (5 plantillas) / 1241 "Listas de Precios TAURO" (6 plantillas). El motor llama a su constante `ReporteListasPreciosZaragoza` = **1240**, nombre enganoso: 1240 es el generico y el especifico de Tauro es el 1241, que el motor **no usa nunca**. |
| **1160 / 1161 (/1162)** | Si, y hay **tres**: 1160, 1161 y 1162, todos `sp_n_rptVentasSaldosGlobales`, 3 plantillas cada uno. Mac31 usa **1160** en el nodo del menu y **1162** en el de "Cobranza Pagadas". El motor usa **1161** y **1162**. Falta el 1160. Hermanos: **1165 / 1166** "Cobranza Totalizado", mismo SP; el motor usa el 1166. |
| **1025 / 1026** | **No son el mismo reporte.** 1024 y 1025 ("Asistencia") comparten `sp_n_ConsultaEmpleadosTipoSueldoRecibo`; 1026 ("Asistencias") es `sp_n_rptAsistenciasZaragoza`. El par real es **1024 / 1025**. El motor usa solo el 1026 (clave `asistencias`) y funciona (PDF 319 661 B). |
| **1255 / 1260 / 1265** | **1265 no existe en ninguna base.** 1255 "Comprobante de Cascos" (`sp_n_rptUsadosMovimientos`) y 1260 "Comprobante de Cascos TAURO" (`sp_n_rptUsadosMovimientosDiario`) son **SP distintos**: dos reportes parecidos, no dos formatos del mismo. Ninguno esta en el menu ni en el motor. |

Otros pares y tripletes que aparecieron solos y valen igual:

```
sp_n_rptVentasPagosGlobales              -> 71, 72, 73, 75, 77
sp_n_rptVentasMotoGlobales               -> 1475, 1480, 1485, 1490   (los 4 ya funcionan)
sp_n_rptVentasRemisionesGlobales         -> 19, 21, 22               (los 3 ya funcionan)
sp_n_rptCobrosGlobales                   -> 1465, 1466, 1470
sp_n_rptVentasCobrosDineroCascosGlobales -> 1355, 1360, 1365
sp_n_rptInventarioActualCategoria        -> 210, 212
sp_n_rptInventarioPedidosAcumuladores    -> 235, 1136, 1137
sp_n_rptInventarioFaltanteProductos      -> 1140, 1141
sp_n_rptGasolinas                        -> 1230, 1231
sp_n_rptClienteCompraAcumMotoLubMensual  -> 118, 119
sp_n_rptIngresosContabilidad             -> 1455, 1460, 1505, 1510   (SP inexistente: Aviacion)
sp_n_rptRecaudacionesTarjetasGlobales    -> 1432, 1433, 1434         (idem)
```

---

## 4. Lo que descubri del motor y no estaba documentado

1. **El motor solo lee `spV2`. La columna `SP` se ignora por completo.**
   `ConsultarConfiguracionAsync` (`Rendering.cs:33-35`) hace `row["SPV2"]` y si
   viene vacio lanza *"El reporte no tiene SPV2 configurado."* Eso explica de golpe
   los 79 reportes "que faltan": no les falta codigo, les falta un dato. Y
   **ninguno de los 79 esta en el menu de Mac31** - son los de `ArchivoRPT` mas
   Crystal (`rptVentasPagosAgentes`, `rptVentasFoliosAgentes`, `rptVentasGlobal`...).
   Es la mejor evidencia de obsolescencia que hay.

2. **`spV2` puede traer VARIOS stored procedures separados por `|`** y el motor los
   ejecuta todos, una `DataTable` por SP (`DataHelpers.cs:66`). Ejemplo real: el
   1005 (`Concentrado`) trae tres.

3. **Los parametros del SP se descubren en tiempo de ejecucion con
   `SqlCommandBuilder.DeriveParameters`** (`DataHelpers.cs:73`) y se asignan **por
   posicion** desde la cadena partida por `|`. Consecuencias que nadie escribio:
   * Un `spV2` nuevo **no necesita codigo** para ser llamado: si pide un solo
     `@IDParametro`, funciona tal cual.
   * Es **un viaje extra a SQL por SP y por peticion** (DeriveParameters hace su
     propia consulta) y necesita permiso de ver la definicion.
   * Si alguien agrega un parametro **en medio** de un `sp_n_` legacy, todos los
     reportes que lo usan se desalinean **sin error**: se corren los valores.
   * Hay un escape propio para valores que contienen `|`: `~`, `@@` y `[`
     (`DataHelpers.cs:63`, `:88-91`).

4. **La plantilla sale de DOS tablas, no de una.** `sp_n_ConsultaTemplateHtml` hace
   `INNER JOIN tipo_template_html` para traer `Orientacion, Ancho, Alto,
   MarginTop/Bottom/Left/Right, MaxLineas`. `Template_Html` guarda el HTML por
   pedazo; `tipo_template_html` guarda la geometria de la hoja. **Si falta el
   renglon de `tipo_template_html`, el reporte truena aunque el HTML exista** (el
   join lo borra y el motor lanza *"No se encontraron templates HTML"*). Hoy no
   pasa en ninguno de los 149, pero es la trampa a vigilar al dar de alta uno nuevo.

5. **`IDReporteMaster` redirige la busqueda de plantilla**
   (`ReportesVentasRepository.cs:487`):
   `templateReportId = config.IDReporteMaster > 0 ? config.IDReporteMaster : idReporte`.
   Es el mecanismo oficial para que una variante reuse el papel de otra.

6. **El Excel degrada bien.** Sin lista de columnas sale `source.Copy()`: el xlsx
   trae los nombres crudos del SP. Es feo, no esta roto. Comprobado con tres reportes.

7. **La ruta anonima `ReportesV3W` es completamente replayable**: todo el estado del
   reporte vive en `dbo.Parametros`. Mac31 y el backend nuevo escriben ahi el mismo
   formato, y el backend puede renderizar un `psp` que escribio Mac31 (probado). Eso
   convierte la migracion en algo verificable lado a lado: mismo `psp`, el papel de
   Mac31 contra el papel del web.

8. **`sp_n_ConsultaParametros` BORRA.** Despues del `SELECT` hace
   `DELETE Parametros WHERE Fecha <= DATEADD(dd,-30,getdate())`. O sea: **cada GET de
   un reporte hace recoleccion de basura en una tabla compartida.** Funciona, pero un
   GET que escribe no es lo que nadie espera, y si dos peticiones entran juntas
   compiten por el mismo DELETE.

9. **El controlador prohibe `idReporte` en el body a proposito** y lo dice en un
   comentario (`ReportesVentasController.cs:233-240`): *"mandas la clave de un
   reporte que si puedes y el id del que no"*. Por eso el catalogo **si** es un
   candado real, aunque sea el mas barato de abrir.

10. **El catalogo tiene 53 entradas, no 43.** Si el 43 viene de algun front, los dos
    estan desincronizados y conviene revisarlo.

---

## 5. Riesgos concretos

**R1 - El mismo numero es OTRO reporte segun la empresa.** Lo mas peligroso que
encontre, porque cualquier constante `IDReporte` en C# es correcta para una empresa
y silenciosamente falsa para la otra:

| id | en Tauro (`Produccion_svr`) | en Zaragoza (`MacZ`) |
|---|---|---|
| **1017** | `Compras Comprobacion` / `sp_n_rptComprasComprobacionGlobales` | **`Cobranza Detallado` / `sp_n_rptVentasSaldosGlobales`** |
| **1210** | `Remision Centro Servicio` / `sp_n_rptVentasRemisionCentroServicio` | `Remision Centro Servicio` / **`sp_n_rptVentasRemisionSinPrecio`** |
| **1001** | `Impresion de Remision V2` | `Reporte de Cascos Excedentes` |

Ninguno de los tres esta hoy en el motor, asi que **no hay bug activo** - pero es una
mina para el que agregue ids por numero. Regla a adoptar: antes de dar de alta un
`IDReporte`, comprobar `NombreReporte` mas `spV2` **en las dos bases**.

**R2 - Reportes cuyo SP no existe en ninguna base: 21.** Toda la familia Aviacion /
Recaudaciones / Contabilidad (1340, 1390, 1425-1434, 1435, 1450, 1455, 1460, 1500,
1505, 1510, 1515, 1520, 1525, 1530) apunta a SPs (`sp_n_rptRecaudacionesGlobales`,
`sp_n_rptIngresosContabilidad`, `sp_n_rpPolizaCheques`, ...) que **no estan ni en
`Produccion_svr` ni en `MacZ`**. Y **6 de ellos siguen colgados del menu de Mac31**
(1426, 1428, 1429, 1430, 1432, 1455): en estas dos empresas esos nodos ya truenan en
legacy. No migrarlos; si alguien los pide, es otra empresa y otra base.

**R3 - Estado de Cuenta (1370) no puede vivir en Azure.**
`sp_n_rptVentasEstadoCuenta` con **un solo mes** (2026-03-01 a 2026-03-31) llevaba
**mas de 6 minutos corriendo** en `sys.dm_exec_requests` y mi cliente se rindio a los
300 s / 420 s. El `CommandTimeout` del motor es 500 s (`DataHelpers.cs:71`), pero
**Azure App Service corta la peticion HTTP a los 230 s**: en produccion este reporte
da 502 sin importar el timeout de SQL. Hay que optimizar el SP o volverlo asincrono
(generar y avisar), no subir timeouts.

**R4 - Hay reportes que cruzan a OTRA base por nombre fijo.** `movimientos_centros`
(1410) devolvio `Invalid object name 'MacZCS7.dbo.Ventas'`: el SP trae el nombre de la
base del centro escrito dentro. `MacZCS7` no existe en este servidor
(`sys.databases` lo confirma). Es distinto de `BaseDelCentro`, que reapunta la
conexion: aqui el **SP** cruza. Si el centro no esta restaurado o se renombra, el
reporte muere y el mensaje no dice que falta.

**R5 - 9 reportes del menu fallan por el SP de la base LOCAL, no por el motor.** Lo
verifique bajando un nivel: corri el mismo SP con el mismo `psp` en `sqlcmd` y da
**exactamente el mismo error**, sin el backend en medio:

```
EXEC dbo.sp_n_rptVentasCategoriaUtilidad '266490'
Msg 213 ... Procedure sp_n_rptVentasCategoria, Line 487
Column name or number of supplied values does not match table definition.
```

Los 9: `106` (clientes_con_compra_por_dia, *Invalid length parameter passed to LEFT or
SUBSTRING*), `145` (utilidad productos), `493` (ventas_y_pagos), `1166`
(cobranza_pagadas totalizado) - los cuatro *Column name or number of supplied
values...* -, `1245` (listas_de_precios productos, *Incorrect syntax near )*),
`1360` / `1365` (cobros dinero/cascos, *Invalid column name CantidadUtilizadaG1*),
`1370` (timeout, R3) y `1410` (R4). **Falta probarlos en una base con los SP al dia**;
en el TSV van como `?` por eso, no porque el motor no los acepte.

**R6 - Fuga de detalle de SQL.** Esos 500 salieron con
`{"message":"Error de base de datos.","detail":"Invalid column name CantidadUtilizadaG1..."}`.
El detalle de SQL llega al navegador en un endpoint **anonimo** (`ReportesV3W` no pide
token). Vale taparlo.

**R7 - Ids fantasma.** 240, 76 y 1242 estan en el codigo y no en la tabla (paso 4).
El 240 es un 500 reproducible hoy.

### Lo que toque (y lo que no)

* **No modifique nada de `Legacy/`** ni ningun objeto de la base.
* **Si** se escribieron renglones en `dbo.Parametros` de `Produccion_svr`: es lo que
  hace cada generacion de reporte (`sp_n_ActualizarParametro`), es una tabla de
  borrador y el propio `sp_n_ConsultaParametros` borra lo de mas de 30 dias. Pasaron
  de 45 a ~165 renglones.
* El backend local **se murio y volvio solo** a mitad de la pasada (una tanda de
  `connection refused` en el log) justo despues de varias generaciones grandes
  seguidas. No pude reproducirlo aislado; queda apuntado como cosa a vigilar al
  generar reportes grandes en serie.

---

## 6. Como leer `reportes-cobertura.tsv`

229 filas (la union de las dos tablas), ordenadas por `id_reporte`.

| clasificacion | filas | significa |
|---|---|---|
| `ya-funciona` | 66 | devolvio un PDF real en esta corrida; `notas` trae el tamano |
| `necesita-parametros` | 50 | `BuildLegacyVentasParamsAsync` no tiene `case`; **solo 12 estan en el menu** |
| `obsoleto` | 100 | 79 sin `spV2` (y sin nodo en el menu) mas 21 con SP inexistente |
| `?` | 9 | el motor los acepta; fallo el SP de la base local (R5) |
| `solo-catalogo` | 4 | listos salvo la clave en `ReportesCatalogo.cs` |
| `necesita-plantilla` | 0 | no existe ese caso |
| `necesita-excel` | 0 como clase | es cosmetico: se anota dentro de `que_le_falta` en los 38 `ya-funciona` que no tienen lista de columnas |

`en_menu_mac31` distingue `si (nodo)` (61 ids, un nodo del arbol de
`ReportesMenu.cs`) de `si (variante)` (38 ids mas, que se eligen con un combo dentro
de la forma) - 99 en total.
