# Ola 7 — completar el catalogo vivo de reportes de Mac31

Hecho el 6-oct-2026 contra el backend nuevo en `http://127.0.0.1:5087`
(base `LAP-JUAND` / `Produccion_svr` = Tauro, usuario `BancoPruebas`) y, cuando
habia con que comparar, contra **Legacy** (`MacServicios2` en
`http://localhost:63479`, `POST api/reportesV2/consultar`) **con el mismo renglon
de `dbo.Parametros`**. Los stored procedures que fallaron se volvieron a correr
pelados en `sqlcmd` para saber de quien es la culpa.

No se modifico nada de `Legacy/`, nada de `MacReportes2` y **ningun objeto de la
base**: ni SPs, ni `Template_Html`, ni `dbo.Reportes`. Lo unico que se escribio
son renglones de `dbo.Parametros`, que es lo que hace cada generacion de reporte
en los dos sistemas.

---

## 1. La respuesta corta

| | |
|---|---|
| Reportes del menu vivo de Mac31 que entregan documento | **de 65 a 75 de 99 (76 %)** |
| Reportes nuevos entregando documento | **10**: 1160, 118, 119, 77, 1243, 1244, 1246, 1247, 1236, 1250 |
| Reportes arreglados (ya existian y no servian) | **1**: el 1370 (Estado de Cuenta) pasa de **mas de 400 s / 502 en Azure** a **3.5 s** |
| Claves del catalogo | de **53** a **57** |
| Claves del catalogo que daban 500 en `legacy/generar` | de **6** a **0** |
| Defectos heredados NUEVOS documentados (rotos tambien en legacy) | **4**: 1375, 1380, la rama de pantalla del 118, y el 1415/1420 por la base del centro |
| Lo que resulto NO ser trabajo | **1355**: Mac31 nunca lo genera |

Dos correcciones al mapa previo, las dos comprobadas:

1. **El nodo "Cobranza" de Mac31 es el 1160 y el motor apuntaba al 1161**, que no
   lo usa ningun nodo. El 1172 si es "Cobranza Detallado" y ya estaba bien. El
   mapa tenia el reparto cruzado (`ReportesMenu.cs:732-740`).
2. **El 1355 no es un hueco.** El nodo "Cobros Dinero y Cascos" **nunca escribe
   el 1355**: siempre pone 1360 o 1365 segun el radio
   (`ReportesMenu.cs:2538-2556`), que es exactamente lo que ya hacia el backend
   nuevo. El 1355 es un renglon de `dbo.Reportes` que Mac31 no pide.

---

## 2. Capacidades

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Cobranza (nodo real del menu) | `api/reportesV2/consultar` id 1160 | `POST /api/reportes/ventas/cobranza/generar` y `legacy/generar` `cobranza` | `reportes.ventas.cobranza.ver` | **migrado** | el motor apuntaba al 1161, que no esta en ningun nodo |
| Clientes Acum Moto Lub mensual | id 118 / 119 | `legacy/generar` `clientes_acum_moto_lub` + `ventasMensual` | `reportes.ventas.clientes_acum_moto_lub.ver` | **migrado** | 118 = todas las categorias (solo Excel), 119 = una categoria |
| Pagos Detallado TAURO | id 77 | `legacy/generar` `pagos` | `reportes.ventas.pagos.ver` | **migrado** | el id lo elige `Constantes.Funcionalidad`, como Mac31 |
| Pagos Detallado Centro de Servicio | id 72 | `legacy/generar` `pagos` | `reportes.ventas.pagos.ver` | **cableado, no comprobable aqui** | solo se elige con `EsCentroServicio = 1`; estas dos bases valen 0 |
| Listas de precios personalizadas de Tauro | ids 1243/1244/1246/1247 | `legacy/generar` `listas_de_precios` + `tipoLista` 1-4 | `reportes.administracion.listas_de_precios.ver` | **migrado** | las cuatro IGUAL que Legacy, importe por importe |
| Estado de Cuenta | id 1370 | `legacy/generar` `estado_de_cuenta` | `reportes.ventas.estado_de_cuenta.ver` | **arreglado** | exige cliente; de >400 s a 3.5 s |
| Estado de Cuenta DINERO / CASCOS | ids 1375 / 1380 | `legacy/generar` `estado_de_cuenta` + `formatoEstadoCuenta` | `reportes.ventas.estado_de_cuenta.ver` | **alcanzable, roto en la base** | el SP envolvente llama al interno con 6 argumentos y el interno recibe 1 |
| Registro de Garantias | id 1236 | `legacy/generar` `garantias_registradas` | `reportes.ventas.garantias_registradas.ver` | **migrado (sin datos)** | `dbo.GarantiasRegistro` esta vacia en las dos bases |
| Control Acumulado de Estimaciones | id 1250 | `legacy/generar` `control_acumulado_de_estimaciones` | sin permiso (no existe en `WPermiso`) | **migrado (sin datos)** | `dbo.MC_ContratosEstimaciones` vacia; en Mac31 el nodo truena por falta de su `case` |
| Centros Pagos Entradas y Salidas | id 1415 | `legacy/generar` `centros_pagos_entradas_salidas` | `reportes.ventas.movimientos_centros.ver` | **cableado, falta la base del centro** | `MacZCS7`/`MacZCS8` no existen en este servidor |
| Centros Garantias | id 1420 | `legacy/generar` `centros_garantias` | `reportes.ventas.movimientos_centros.ver` | **cableado, falta la base del centro** | idem |
| Acumuladores y Productos por la ruta generica | ids 10 / 15 | `legacy/generar` `acumuladores_y_productos` | `reportes.ventas.acumuladores_y_productos.ver` | **arreglado** | antes 500 "Reporte de ventas no soportado" |
| Folios / Remisiones / Facturas / Concentrados por la ruta generica | ids 20, 23, 19, 21, 24, 1185 | `legacy/generar` con su clave | la de cada clave | **arreglado** | idem, las seis claves |
| Excel de Venta de Acumuladores | id 10, `salida:"excel"` | `legacy/generar` + `GET ReportesV3W` | `reportes.ventas.acumuladores_y_productos.ver` | **arreglado** | ya usa `VentaAcumuladoresExcelColumns` en vez de los nombres crudos del SP |
| Un `<img>` que no carga ya no cuesta el documento | n/a (NReco lo ignoraba) | todo reporte legacy | n/a | **arreglado** | ver §5.4 |

---

## 3. Reporte por reporte

`gen?` = el GET firmado entrego el documento. `coincide` = se le pidio a Legacy el
**mismo `psp`** y se comparo el multiconjunto de importes, los renglones y los
totales. "subconjunto" quiere decir **0 numeros inventados por el nuevo** y
algunos que `pdftotext` junta en la rejilla ancha: es artefacto de la extraccion,
no diferencia de dato (lo mismo que ya encontro la auditoria en el 71 y el 1170).

### 3.1 Los nuevos

| clave | id | ¿genero? | tamaño | ¿coincide con Legacy? | notas |
|---|---|---|---|---|---|
| `cobranza` | **1160** | si | pdf 259 294 B (1 874 lineas) | **subconjunto** (3 546 vs 3 547 importes, 0 inventados, 1 703 `<tr>`) | antes esta clave daba el 1161 |
| `clientes_acum_moto_lub` + `ventasMensual` | **118** | si | **xlsx 45 748 B** | no comparable (Legacy devuelve html) | en pdf el SP revienta; ver §5.2 |
| `clientes_acum_moto_lub` + `ventasMensual` + categoria | **119** | si | pdf 126 311 B (313 lineas) | **IGUAL** (249 `<tr>`, totales `5,155 / 4,517 / … / 10,906` identicos) | solo difiere el contador de paginas |
| `pagos` (Tauro) | **77** | si | pdf 89 811 B (146 lineas) | **subconjunto** (182 vs 208, 0 inventados, 146 `<tr>` exactos) | el mismo patron que el 71 verificado por la auditoria |
| `listas_de_precios` `tipoLista:1` | **1243** | si | pdf 117 876 B | **IGUAL** (374 = 374, 147 `<tr>`) | titulo "LISTA: TODAS LAS MARCAS" |
| `listas_de_precios` `tipoLista:2` | **1244** | si | pdf 117 037 B | **IGUAL** (333 = 333, 146 `<tr>`) | "LTH, GONHER, CRONOS, CALE, AMERICA, FULL POWER, DYNAMO, OPTIMA, HI TEC, XTREME, MOTOS" |
| `listas_de_precios` `tipoLista:3` | **1246** | si | pdf 105 070 B | **IGUAL** (238 = 238, 98 `<tr>`) | "CRONOS, GONHER, XTREME, LTH, CALE, AMERICA, FULL POWER, DYNAMO" |
| `listas_de_precios` `tipoLista:4` | **1247** | si | pdf 91 490 B | **IGUAL** (138 = 138, 49 `<tr>`) | "FULL POWER, AMERICA, LTH" |
| `garantias_registradas` | **1236** | si | pdf 78 043 B | los dos "No se encontro informacion" | `dbo.GarantiasRegistro` tiene 0 renglones |
| `control_acumulado_de_estimaciones` | **1250** | si | pdf 72 593 B | Legacy contesta 400 "El origen no contiene DataRows" | `dbo.MC_ContratosEstimaciones` vacia; el nuevo si entrega el papel vacio |
| `estado_de_cuenta` `formatoEstadoCuenta:"dinero"` | **1375** | **no** | 500 | — | defecto de la base, §5.1 |
| `estado_de_cuenta` `formatoEstadoCuenta:"cascos"` | **1380** | **no** | 500 | — | defecto de la base, §5.1 |
| `centros_pagos_entradas_salidas` | **1415** | **no** | 500 | — | `Invalid object name 'MacZCS7.dbo.Ventas Pagos'`, §5.3 |
| `centros_garantias` | **1420** | **no** | 500 | — | `Invalid object name 'MacZCS7.dbo.Garantias'`, §5.3 |

### 3.2 El 1370, antes y despues

| | antes | ahora |
|---|---|---|
| `estado_de_cuenta` sin cliente | sin respuesta a los 400 s (502 en Azure) | **400** "El estado de cuenta es de un cliente" |
| `estado_de_cuenta` con cliente | 1370 con Param7 = 1, que no es su formato | **pdf 82 518 B en 3.5 s**, 37 lineas, **IGUAL** que Legacy (74 = 74 importes, 27 `<tr>`) |
| `formatoEstadoCuenta` = dinero / cascos | inalcanzable (el resolver devolvia 1370 siempre) | 1375 / 1380, que la base tiene rotos |

### 3.3 Las seis claves que daban 500

| clave | id | ¿genero? | tamaño | ¿coincide con Legacy? |
|---|---|---|---|---|
| `acumuladores_y_productos` | 10 | si | pdf 89 044 B (59 lineas) | **IGUAL** en importes (0 y 0; difieren 7 enteros chicos por celdas que `pdftotext` parte) |
| `acumuladores_y_productos` productos | 15 | si | pdf 52 235 B | no comparado |
| `folios` | 20 | si | pdf 77 398 B (197 lineas) | **IGUAL** (148 = 148, 156 `<tr>`) |
| `folios` dinero/cascos | 23 | si | pdf 79 280 B (173 lineas) | **IGUAL** (444 = 444, 157 `<tr>`) |
| `remisiones` | 21 | si | pdf 102 383 B (241 lineas) | **IGUAL** (444 = 444, 164 `<tr>`) |
| `remisiones` saldo a favor | 19 | si | pdf 75 125 B | los dos vacios |
| `facturas` | 24 | si | pdf 75 380 B | los dos vacios |
| `concentrados` | 1185 | si | pdf 61 335 B | los dos vacios |

Los endpoints propios de esas seis familias siguen funcionando igual: se
comprobaron los seis despues del cambio (10, 21, 20, 24, 1185 y 1160).

### 3.4 Excel del reporte estrella

`acumuladores_y_productos` con `salida:"excel"` -> `200 xlsx 7 980 B`. Los
encabezados ahora son los de la lista tipada (`No`, `FechaInicial`, `FechaFinal`,
`Empresa`, `Almacen`, `Agente`, `Numero`, `Cliente`, `Categoria`, `Marca`,
`FechaEmision`, `HoraEmision`, `Folio`, `Clave`, `UnidadMedida`, `Ventas`,
`Garantias`, `Devolucion`, …) en vez de los nombres crudos del SP. Era un `case`
de una linea: `VentaAcumuladoresExcelColumns` estaba escrita desde el principio y
el `switch` de `BuildLegacyExcelTable` empezaba en el 15.

### 3.5 Barrido de regresion: las 57 claves

Se pidieron **las 57 claves del catalogo** de corrido. Entregan documento **51**;
fallan **6**, y los seis son defectos de la base ya conocidos o nuevos, ninguno
del motor:

```
k_ventas_pagos   493   Column name or number of supplied values does not match…  (ya documentado)
k_cli_dia        106   Invalid length parameter passed to the LEFT or SUBSTRING  (ya documentado)
k_cobros_dc     1360   Invalid column name 'CantidadUtilizadaG1'                (ya documentado)
k_mov_centros   1410   Invalid object name 'MacZCS7.dbo.Ventas'                 (ya documentado)
k_cen_pagos     1415   Invalid object name 'MacZCS7.dbo.Ventas Pagos'           (nuevo, misma causa)
k_cen_gar       1420   Invalid object name 'MacZCS7.dbo.Garantias'              (nuevo, misma causa)
```

---

## 4. Lo que se cambio en el codigo

Nada del motor se reescribio: se extendio. Ningun generador de plantilla nuevo.

| archivo | que |
|---|---|
| `Application/Reportes/ReportesCatalogo.cs` | +4 claves: `garantias_registradas`, `centros_pagos_entradas_salidas`, `centros_garantias`, `control_acumulado_de_estimaciones` |
| `Application/DTOs/Reportes/ReportesVentasDtos.cs` | +`FoliosDineroCascos`, `CobranzaStatus`, `FormatoEstadoCuenta`, `TipoLista`, `VentasMensual`, `IDContratoEstimacion`, `IDContrato`, `IDProductos` |
| `Infrastructure/Repositories/…Constants.cs` | +11 constantes de `IDReporte` (118, 119, 1160, 1236, 1243, 1244, 1246, 1247, 1250, 1415, 1420) |
| `…LegacyParams.cs` | `ResolveReporteLegacyVentas` pasa a `…Async` (necesita `Constantes`); +`ResolveReportePagos`, `ResolveReporteClientesAcumMotoLub`, `ResolveReporteFoliosLegacy`; `ResolveReporteEstadoCuenta` arreglado; `ResolveReporteListasPrecios` resuelve por empresa y por `tipoLista`; +6 casos de parametros; lista blanca +11 ids |
| `…LegacyParams.cs` (firmas) | `BuildDocumentosVentaLegacyParamsAsync`, `BuildConcentradosLegacyParams` y `BuildCobranzaLegacyParamsAsync` reciben el DTO base + los campos que usaban, para que los pueda llamar tambien la ruta generica |
| `…DataHelpers.cs` | +`ReporteVentaAcumuladores` y +`ReporteVentasCobranzaDetallado` en el `switch` de columnas de Excel |
| `…Models.cs` / `…Rendering.cs` | `LegacyConstants` lee `Funcionalidad` y `EsCentroServicio`; +`SanearImagenesNoCargables` |
| `…ReportesVentasRepository.cs` | abre la conexion antes de resolver el id; fuerza Excel en el 118; sanea imagenes antes del PDF |
| `Controllers/ReportesVentasController.cs` | 400 (no 500) cuando se pide estado de cuenta sin cliente |
| `Infrastructure/Reports/WkhtmltopdfHtmlPdfRenderer.cs` | `--load-error-handling ignore` y `--load-media-error-handling ignore` |
| `ISL_Service.Tests/ReportesCatalogoTests.cs` | 53 -> 57 claves; el permiso compartido del nodo "Movimientos Centros" pasa de "prohibido" a "la unica excepcion, y por que" |

**91 pruebas, 0 fallos. 0 errores de compilacion, 0 advertencias nuevas.**

### 4.1 Ids que dejan de ser alcanzables en Tauro (a proposito)

Porque Mac31 tampoco los usa alla. Ninguno se pierde: siguen saliendo donde
corresponde.

| id | ya no sale en | si sale en | por que |
|---|---|---|---|
| 71 (Pagos Detallado) | Tauro | Zaragoza | en Tauro Mac31 pone el 77 (`funcionalidad == "TAU"`) |
| 1240 (Listas de Precios) | Tauro | Zaragoza | en Tauro Mac31 pone una de las cuatro personalizadas |
| 1161 (Cobranza Detallado) | las dos | — | **ningun nodo de Mac31 lo usa**: el nodo "Cobranza" es el 1160 y "Cobranza Detallado" el 1172 |

Los renglones de `dbo.Parametros` que ya existan para esos ids **siguen
renderizando**: los tres siguen en `IsGenericVentasReporte` / `IsCobranzaReporte`.

---

## 5. Defectos heredados (no son trabajo nuestro, y hay que decirlo)

### 5.1 Estado de Cuenta DINERO y CASCOS: el SP envolvente llama mal al interno

`sp_n_rptVentasEstadoCuentaGlobales` (que es el `spV2` del 1375 y del 1380) hace:

```sql
-- sp_n_rptVentasEstadoCuentaGlobales, linea 226
EXEC sp_n_rptVentasEstadoCuenta @IDsEmpresa, @IDClienteTmp, @IDAgenteTmp, @FechaInicial, @FechaFinal, @Formato
```

y `sp_n_rptVentasEstadoCuenta` hoy tiene **un solo parametro, `@Params`**
(`sys.parameters`, comprobado en `Produccion_svr` **y** en `MacZ`). El envolvente
quedo escrito contra una firma anterior. Sin backend en medio:

```
EXEC dbo.sp_n_rptVentasEstadoCuentaGlobales '266649'
Procedure or function sp_n_rptVentasEstadoCuenta has too many arguments specified.
```

Legacy no puede dar otro resultado: es el mismo SP. Y en Mac31 estos dos ids ni
se piden, porque el combo que los elegia (`cboFormatoEdoCuenta`) **esta
comentado** (`ReportesMenu.cs:1375`).

**Que haria falta:** que el envolvente le mande un `IDParametro` al interno, o que
el interno recupere su firma de 6 argumentos. Es una linea de un `sp_n_`, o sea
legacy, o sea no se toca desde aqui. El resolver nuevo ya los alcanza: el dia que
el SP se arregle, funcionan sin tocar C#.

### 5.2 El 118 solo existe en Excel

Con `Presentacion = 0`, `sp_n_rptClienteCompraAcumMotoLubMensual` arma mal su SQL
dinamico y muere:

```
EXEC dbo.sp_n_rptClienteCompraAcumMotoLubMensual '266644'
Msg 156 … Incorrect syntax near the keyword 'FROM'.
```

Con `Presentacion = 3` entrega el xlsx completo (45 748 B). Mac31 fuerza Excel y
hasta dice por que: *"Acumuladores, Moto y Lubricantes no caben en pantalla, por
eso se mandan solo a Excel"* (`ReportesMenu.cs:3597-3601`). El backend nuevo hace
lo mismo. **No es una limitacion que inventamos: es la unica salida que el SP
sabe contestar.**

### 5.3 Centros: 1410, 1415 y 1420 cruzan a una base que no existe

Los tres SP traen el nombre de la base del centro escrito dentro
(`MacZCS7`, `MacZCS8`, …) y ninguna existe en este servidor (`sys.databases`). El
1410 ya estaba asi en la auditoria; el 1415 y el 1420 fallan por lo mismo y del
mismo modo. **No se pierde nada al apagar Legacy: alla pasa igual.**

De paso se corrigio **como se les llama**, que estaba mal y no se habia notado:
el `#ParamIds` del 1410, del 1415 y del 1420 declara **Param4 = Presentacion
(int)**, mientras el del 1445 mete `IDsCentro` en medio y corre la presentacion a
Param5. El motor les mandaba a los cuatro la forma del 1445. Mac31 hace lo mismo y
se salva por accidente: manda `IDsCentro` vacio en Param4, que SQL convierte a 0,
que es justo "Pantalla". Con la forma correcta el **Excel** de los tres tambien
funciona; antes habria salido el dataset de pantalla.

### 5.4 Una imagen que falta costaba el documento entero

El cuerpo del 1250 pide `#LogoCdMx#`, un tag que el motor no conoce, y el SP
devuelve ese logo como columna apuntando a `C:\Mac21\imagenes\GobiernoCDMX.png`,
que no esta ni empaquetada ni en este servidor. `ReplaceRemainingTags` convertia
el tag en `----` y wkhtmltopdf 0.12.4 **sale con codigo 1
(`ContentNotFoundError`)**: no hay PDF, ni con logo ni sin el.

Las banderas `--load-error-handling ignore` y `--load-media-error-handling ignore`
**no cubren este caso** (probado con el exe empaquetado y un html de una linea),
asi que se agregaron igual —son correctas— y ademas se quita del HTML el `<img>`
que no se puede cargar (`SanearImagenesNoCargables`). Con eso el 1250 entrega su
papel, y de paso queda tapado el agujero del **H4 de la auditoria**: el respaldo
http de imagenes con el certificado caducado ya no puede tumbar un reporte.

### 5.5 Lo que ya estaba documentado y sigue igual

9 reportes rotos en la base (106, 145, 493, 1166, 1245, 1360, 1365, 1410 y el 74
cuando se le manda filtro de agentes), 6 de la vertical Aviacion cuyo SP no existe
en ninguna de las dos bases, y 3 ids fantasma sin renglon en `dbo.Reportes` (240,
76, 1242). Ninguno se toco y ninguno es trabajo del backend nuevo.

---

## 6. Comportamiento por empresa

Antes de dar de alta cada id se comprobo `NombreReporte` **y** `spV2` **en las dos
bases**, que es la regla que pedia el R1 del mapa (el 1017 es "Compras" en Tauro y
"Cobranza Detallado" en Zaragoza). Los 11 ids nuevos **coinciden exactamente en
`Produccion_svr` y en `MacZ`**: mismo nombre y mismo `spV2`, y los 11 tienen sus
renglones de `Template_Html` (IDAplicacion 1, IDStatus 1) y su renglon de
`tipo_template_html`.

Y donde el id **si** depende de la empresa, ahora lo decide la base y no una
constante de C#: `Constantes.Funcionalidad` (`TAU` en `Produccion_svr`, `ZARA` en
`MacZ`) y `Constantes.EsCentroServicio` eligen entre 71/72/77 y entre 1240 y las
cuatro listas de Tauro, igual que `Variables.funcionalidad` en Mac31.

**Lo que no pude ejecutar:** generar en Zaragoza. Se levanto una segunda instancia
del backend apuntada a `MacZ` con `ConnectionStrings__Main` (sin tocar ningun
archivo) y arranco bien, pero `BancoPruebas` **no existe en `MacZ`**: cada empresa
tiene su propia base de usuarios. La ruta de Zaragoza queda verificada por codigo
y por consulta a la base, no por generacion.

---

## 7. Como se reproduce

```bash
cd ISL_Service/ISL_Service && dotnet build ISL_Service.csproj
nohup dotnet run --no-build --no-launch-profile --urls "http://127.0.0.1:5087" &

# token
curl -s -X POST http://127.0.0.1:5087/api/sesion/login \
  -H 'Content-Type: application/json' \
  -d '{"usuario":"BancoPruebas","contrasena":"Banco2026"}'

# generar -> devuelve una URL firmada; el GET entrega el documento
curl -s -X POST http://127.0.0.1:5087/api/reportes/ventas/legacy/generar \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $TOK" \
  -d '{"reporteKey":"listas_de_precios","fechaInicial":"2026-09-15","fechaFinal":"2026-09-23","idGrupoCategoria":1,"tipoLista":2}'

# el MISMO psp, a Legacy, para comparar el dato
curl -s -X POST http://localhost:63479/api/reportesV2/consultar \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $APP_TOKEN" \
  -d '{"token":"'"$APP_TOKEN"'","correoElectronicoToken":"app_web@tauro.com",
       "IDAplicacion":1,"idReporte":1244,"parametros":"266718",
       "parametrosCadena":"","MostrarReporte":0,"Presentacion":0}'
```

Nota util y que costo encontrar: el cuerpo que espera `api/reportesV2/consultar`
lleva el `psp` en el campo **`parametros`** y exige **`idReporte`** y
**`IDAplicacion`**; mandarlo como `psp` o `IDParametro` contesta
`400 "INTENTE GENERAR DE NUEVAMENTE EL REPORTE."`, que suena a dato perdido y es
solo el nombre del campo.

---

## 8. Que queda, y de quien es

| pendiente | de quien |
|---|---|
| Que `sp_n_rptVentasEstadoCuentaGlobales` deje de llamar al interno con 6 argumentos (desbloquea 1375 y 1380) | legacy / base |
| Que la rama de pantalla de `sp_n_rptClienteCompraAcumMotoLubMensual` arme bien su SQL (desbloquea el pdf del 118) | legacy / base |
| Restaurar o renombrar las bases `MacZCS*` (desbloquea 1410, 1415 y 1420) | infraestructura |
| Los 9 SP rotos del §5.5 | legacy / base |
| Comprobar en Tauro real el 72, que solo aparece con `EsCentroServicio = 1` | una instalacion de centro de servicio |
| Las listas de columnas de Excel que faltan (cosmetico, el xlsx sale con los nombres del SP) | a goteo |
| `Logo_Mac_WM.png` en `Assets/Logos/` (H4 de la auditoria; ya no tumba el PDF, pero la remision sale sin marca de agua en Azure) | nosotros, cuando toque |
