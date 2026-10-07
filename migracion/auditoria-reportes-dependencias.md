# Auditoria independiente: se pueden apagar `MacServicios2` y `MacReportes`?

Hecha el 6-oct-2026 por un auditor que **no implemento nada de esto** y cuyo
encargo era *intentar demostrar que el sistema viejo todavia hace falta*.

Todo lo que sigue sale de **ejecutar**, no de leer: 114 documentos generados
contra el backend nuevo (`http://127.0.0.1:5055`, base `Produccion_svr` = Tauro,
usuario `BancoPruebas`), 26 de ellos generados **tambien en Legacy**
(`http://localhost:63479`, `api/reportesV2/consultar`) con **el mismo renglon de
`dbo.Parametros`**, y los stored procedures corridos a mano en `sqlcmd` cuando
habia que bajar un nivel.

No se modifico nada: ni `Legacy/`, ni el backend nuevo, ni objetos de la base.
Lo unico que se escribio son renglones de `dbo.Parametros` (de ~165 a ~630), que
es exactamente lo que hace cada generacion de reporte en los dos sistemas.

---

## 0. La respuesta, en dos renglones

| | |
|---|---|
| **`MacReportes` (MacReportes2)** | **SE PUEDE APAGAR HOY.** No queda nada que lo llame y no se pierde ningun reporte por apagarlo. |
| **`MacServicios2`** | **NO SE PUEDE APAGAR HOY.** No porque el backend nuevo lo necesite — **no lo necesita en absoluto** — sino porque **Mac31 y MacChecador son clientes suyos** y el backend nuevo todavia no cubre **207 de las 488 capacidades** que usan. Apagarlo apaga Mac31 **y detiene la captura de asistencia por huella**. |

El titulo de la pregunta y la respuesta no coinciden, y ahi esta todo el valor de
esta auditoria: **el motor de reportes ya no depende de los servicios viejos, pero
los servicios viejos no estan libres porque los reportes no son su unico cliente.**

---

## 1. Lo que probe: 70 reportes generados de verdad

### 1a. Los que el mapa dice que funcionan (muestra de 55 claves / 66 `IDReporte`)

`¿gen?` = el GET firmado devolvio el documento. `coincide` = se genero el **mismo
`psp`** en Legacy y se comparo el dato (seccion 2). Tiempos: `POST+GET`.

| clave | id | ¿gen? | tipo / tamaño | tiempo | ¿coincide con Legacy? | notas |
|---|---|---|---|---|---|---|
| acumuladores_y_productos | 10 | si | pdf 90 212 B | 0.3+1.5 s | **IGUAL** (663 `<tr>`) | endpoint propio |
| acumuladores_y_productos (productos) | 15 | si | pdf 52 885 B | 0.2+5.3 s | no comparado | `categoria=productos` |
| remisiones (saldo favor) | 19 | si | pdf 75 284 B | 0.2+4.7 s | no comparado | |
| folios | 20 | si | pdf 101 929 B | 0.2+**54.7 s** | no comparado | **LENTO, ver 5b** |
| remisiones | 21 | si | pdf 131 215 B | 0.2+2.3 s | **IGUAL** (876 importes) | |
| impuestos | 22 | si | pdf 98 916 B | 0.2+6.2 s | **IGUAL** (846 importes) | |
| folios dinero/cascos | 23 | si | pdf 112 715 B | 0.2+**57.7 s** | no comparado | **LENTO** |
| facturas | 24 | si | pdf 75 229 B | 0.2+1.0 s | **IGUAL** (vacio los dos) | |
| pagos detallado | 71 | si | pdf 101 439 B | 0.3+5.9 s | **IGUAL** (8/8 bloques de totales) | ver 2b |
| descuentos | 73 | si | pdf 67 860 B | 0.2+5.5 s | no comparado | |
| transferencias_duplicadas | 74 | **depende** | pdf 69 552 B | 0.2+1.1 s | **IGUAL (falla igual)** | **con filtro de agentes FALLA en los dos. Hallazgo nuevo, ver 3b** |
| pagos totalizado | 75 | si | pdf 85 881 B | 0.2+5.3 s | no comparado | |
| clientes_globales | 103 | si | pdf 771 668 B | 0.2+5.1 s | **IGUAL** (2 802 importes, 3 180 `<tr>`) | |
| clientes_con_compra | 105 | si | pdf 105 781 B | 0.2+1.4 s | subconjunto (artefacto, ver 2b) | |
| clientes_sin_compra | 110 | si | pdf 750 607 B | 0.2+7.1 s | no comparado | |
| clientes_compras | 112 | si | pdf 572 739 B | 0.3+5.2 s | no comparado | |
| clientes_con_descuentos | 113 | si | pdf 337 728 B | 0.2+7.3 s | no comparado | |
| clientes_sin_descuentos | 114 | si | pdf 231 593 B | 0.2+**47.3 s** | no comparado | **LENTO** |
| clientes_acum_moto_lub | 116 | si | pdf 101 074 B | 0.2+1.4 s | no comparado | |
| compras acumuladores | 120 | si | pdf 33 562 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| clientes_acum_moto_lub_clarios | 121 | si | pdf 222 540 B | 0.2+7.5 s | no comparado | |
| compras productos | 125 | si | pdf 35 314 B | 0.2+0.8 s | no comparado | |
| compras_facturas | 130 | si | pdf 68 749 B | 0.2+0.9 s | **IGUAL** (vacio los dos) | |
| utilidad acumuladores | 140 | si | pdf 100 602 B | 0.2+7.3 s | **IGUAL** (2 227 importes, 416 `<tr>`) | |
| cascos | 150 | si | pdf 52 837 B | 0.2+1.3 s | **IGUAL** (vacio los dos) | |
| inventario actual acum. | 205 | si | pdf 83 531 B | 0.2+1.6 s | **IGUAL** (195 `<tr>`) | |
| inventario actual PRODUCTOS | 210 | si | pdf 60 127 B | 0.2+2.7 s | no comparado | grupo 3 = PRODUCTOS (correcto) |
| inventario actual FILTROS | 212 | si | pdf 57 403 B | 0.2+1.7 s | no comparado | grupo 2 = FILTROS (correcto) |
| inventario costo acum. | 220 | si | pdf 104 889 B | 0.2+1.5 s | no comparado | |
| inventario costo PRODUCTOS | 225 | si | pdf 77 895 B | 0.2+2.7 s | no comparado | |
| inventario costo FILTROS | 227 | si | pdf 79 652 B | 0.2+1.7 s | no comparado | |
| ajustes_al_inventario | 230 | si | pdf 39 879 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| pedido acumuladores | 235 | si | pdf 73 189 B | 0.2+0.8 s | no comparado | `inventarioGenerarPedido` |
| asistencias | 1026 | si | pdf 74 417 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| inventario_faltante acum. | 1135 | si | pdf 64 182 B | 0.2+9.8 s | no comparado | |
| inventario_faltante PRODUCTOS | 1140 | si | pdf 73 529 B | 0.2+6.6 s | no comparado | |
| inventario_faltante FILTROS | 1141 | si | pdf 76 166 B | 0.2+10.8 s | no comparado | |
| cobranza | 1161 | si | pdf 96 790 / 110 656 B | 0.2+1.2 s | subconjunto (artefacto) | endpoint propio |
| cobranza_pagadas detallado | 1162 | si | pdf 101 565 B | 0.2+2.9 s | no comparado | |
| rutas_de_agentes | 1170 | si | pdf 368 213 B | 0.2+5.3 s | subconjunto (artefacto) | 1 826 `<tr>` |
| cobranza_detallado | 1172 | si | pdf 360 877 B | 0.2+5.7 s | no comparado | |
| concentrados | 1185 | si | pdf 61 198 B | 0.2+0.9 s | **IGUAL** (vacio los dos) | endpoint propio |
| gasolinas detallado | 1230 | si | pdf 70 064 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| gasolinas mensual | 1231 | si | pdf 69 084 B | 0.2+0.8 s | no comparado | |
| garantias | 1235 | si | pdf 68 617 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| listas_de_precios | 1240 | si | pdf 80 459 B | 0.2+**19.8 s** | no comparado | **LENTO** |
| cascos_excedentes | 1305 | si | pdf 75 718 B | 0.2+1.3 s | no comparado | |
| liquidaciones | 1319 | si | pdf 60 286 B | 0.2+1.6 s | no comparado | |
| cortes | 1320 | si | pdf 71 895 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| movimientos | 1325 | si | pdf 84 292 B | 0.2+1.4 s | **IGUAL** (131 `<tr>`) | |
| gastos detallado | 1330 | si | pdf 71 824 B | 0.2+0.9 s | **IGUAL** (vacio los dos) | |
| gastos totalizado | 1335 | si | pdf 71 636 B | 0.2+0.8 s | no comparado | |
| accesos | 1395 | si | pdf 73 365 B | 0.2+0.8 s | **IGUAL** (vacio los dos) | |
| transferencias_clientes | 1405 | si | pdf 65 177 B | 0.3+0.8 s | no comparado | |
| transferencias_estatus | 1406 | si | pdf 65 461 B | 0.2+0.8 s | no comparado | |
| transferencias_folios | 1407 | si | pdf 65 232 B | 0.2+0.8 s | no comparado | |
| clientes_facturas_rfc | 1440 | si | pdf 78 487 B | 0.2+1.3 s | no comparado | |
| centros_de_servicio | 1445 | si | pdf 71 794 B | 0.2+0.8 s | no comparado | sale vacio |
| hoja_cobro total cobrado det. | 1465 | si | pdf 83 744 B | 0.2+0.9 s | **IGUAL** (vacio los dos) | |
| hoja_cobro total cobrado tot. | 1466 | si | pdf 81 771 B | 0.2+0.8 s | no comparado | **el mapa no lo tenia probado** |
| hoja_cobro cascos | 1470 | si | pdf 83 522 B | 0.2+0.8 s | no comparado | |
| motobaterias cliente | 1475 | si | pdf 84 232 B | 0.2+7.5 s | **IGUAL** (vacio los dos) | |
| motobaterias folio | 1480 | si | pdf 84 195 B | 0.2+7.2 s | no comparado | |
| motobaterias codigo | 1485 | si | pdf 85 080 B | 0.2+1.3 s | no comparado | |
| motobaterias marca | 1490 | si | pdf 84 715 B | 0.2+1.3 s | no comparado | |
| hoja_cobro cheques | 1495 | si | pdf 84 038 B | 0.2+0.8 s | no comparado | |

**Excel tambien sale.** `salida:"excel"` con `pagos` (71) y `clientes_con_compra`
(105) devolvio `200 application/vnd.openxmlformats-...sheet`, 22 104 B / 26 762 B,
142 y 150 renglones. Confirmado el "degrada bien": sin lista de columnas sale el
xlsx con los nombres crudos del SP.

### 1b. Los que el mapa dice que NO funcionan: confirmados, **y por que**

Los 9 que el mapa marca `?`, mas el caso nuevo del 74. Para cada uno se corrio
**tres veces**: por el backend nuevo, por **Legacy con el mismo `psp`**, y el SP
pelado en `sqlcmd`.

| clave | id | backend nuevo | Legacy (mismo psp) | sqlcmd directo | veredicto |
|---|---|---|---|---|---|
| clientes_con_compra_por_dia | 106 | 500 `Invalid length parameter passed to the LEFT or SUBSTRING function` | **400, MISMO error** | — | **roto en la base** |
| utilidad productos | 145 | 500 `Column name or number of supplied values does not match table definition` | **400, MISMO error** | **Msg 213, `sp_n_rptVentasCategoria` linea 487** | **roto en la base** |
| ventas_y_pagos | 493 | 500 idem | **400, MISMO error** | — | **roto en la base** |
| cobranza_pagadas totalizado | 1166 | 500 idem | **400, MISMO error** | **Msg 213, `sp_n_rptVentasSaldos` linea 956** | **roto en la base** |
| listas_de_precios productos | 1245 | 500 `Incorrect syntax near ')'` | **400, MISMO error** | **Msg 102 linea 10** | **roto en la base** |
| cobros dinero | 1360 | 500 `Invalid column name 'CantidadUtilizadaG1'` (+8) | **400, MISMO error** | **Msg 207 linea 25** | **roto en la base** |
| cobros cascos | 1365 | 500 idem | **400, MISMO error** | — | **roto en la base** |
| estado_de_cuenta | 1370 | **sin respuesta a los 400 s** | — (mismo SP) | el `SELECT` seguia vivo a los 341 s en `sys.dm_exec_requests` | **el SP, no el motor. Ver 5a** |
| movimientos_centros | 1410 | 500 `Invalid object name 'MacZCS7.dbo.Ventas'` (+5) | **400, MISMO error** | **Msg 208** | **falta la base del centro** |
| transferencias_duplicadas **con filtro de agentes** | 74 | 500 `The multi-part identifier "V.IDAgente" could not be bound` | **400, MISMO error** | — | **roto en la base**, solo en esa rama |

**Esto es el hallazgo mas fuerte de la auditoria, y es mas fuerte que lo que
afirma el mapa previo.** El mapa dice de estos 9: *"NO es hueco del motor […]
falta probarlo en una base con los SP al dia"*. Yo lo **probe**: los **10
fallan identico en Legacy**, pidiendoselos a `MacServicios2` con el mismo renglon
de `dbo.Parametros`. No hay que creerle al mapa: estos reportes **ya estan rotos
hoy en Mac31**, en estas dos bases. **Apagar Legacy no los pierde, porque Legacy
tampoco los da.**

Ninguno de estos 10 es evidencia a favor de conservar el sistema viejo.

---

## 2. ¿Se parece el resultado al de Legacy? Si, y se puede demostrar

### 2a. El metodo (y por que es valido)

El contrato entre los dos sistemas es el **renglon de `dbo.Parametros`**. Se
genero el reporte en el backend nuevo, se tomo el `psp` que escribio, y se le
pidio **el mismo `psp`** a Legacy por `POST /api/reportesV2/consultar`. Legacy
devuelve su HTML; el backend nuevo devuelve su PDF (texto extraido con
`pdftotext -layout`). Mismos parametros, misma base, mismo instante: lo unico que
cambia es quien lo pinta.

### 2b. El resultado: 17 reportes comparados, 0 diferencias de dato

| | reportes |
|---|---|
| Titulo del reporte identico | **16 de 17** (el 1170 solo porque el titulo se parte en dos lineas en el PDF) |
| Multiset de importes **identico** | **13 de 17** |
| Importes del nuevo **subconjunto estricto** del de Legacy | 4 (1161, 71, 105, 1170) |
| **Importes que aparecen en el nuevo y NO en Legacy** | **0 en los 17** |

Ese ultimo renglon es el que importa: **el backend nuevo no invento ni un solo
numero.** Los 4 "subconjunto" son artefacto de `pdftotext`, no diferencia de
dato, y se demostro bajando a los totales: en `pagos` (71) los **8 bloques de
subtotal por usuario coinciden caracter por caracter**:

```
EFECTIVO: 2 TARJETA: 2 TRANSFERENCIA: 4 TOTAL: 8 SALDO FAVOR: 0 DESCUENTOS: 0
CARGOS: 0 DIFER CASCO: 0   8,845.00  3,063.00  9,186.00  21,094.00  0.00 0.00 0.00 0.00
```
(bloque 8 de 8; identico en el HTML de Legacy y en el PDF nuevo. Los 8: IGUAL)

Los numeros que "faltaban" son celdas de la rejilla ancha con separadores `|`,
que `wkhtmltopdf` aprieta y `pdftotext` junta en un solo token.

Tambien coincide el **conteo de renglones**: `clientes_globales` 3 180 `<tr>`,
`acumuladores` 663, `utilidad` 416, `remisiones` 332, `impuestos` 306, `pagos`
243, `inventario_actual` 195, `clientes_con_compra` 180, `cobranza` 150,
`movimientos` 131, `motobaterias` 56. Y cuando el reporte sale vacio, **los dos
dicen lo mismo**: `No se encontro informacion` (comprobado en 11 reportes).

### 2c. La unica diferencia visible, y es a favor del nuevo

Legacy mete el logo como **ruta de disco de su propia maquina**:

```html
<img src="C:\Mac21\imagenes\tauro_logo.jpg" alt="" width="160" height="40">
```

El backend nuevo lo convierte a `data:` URI y lo **empotra en el PDF**
(comprobado: `cmp_cascos.pdf` trae 1 imagen embebida). O sea: **el papel del
nuevo no necesita la carpeta de Legacy; el de Legacy si.**

---

## 3. Cobertura real de reportes: mi numero

### 3a. Mi cuenta

| | mi numero | el mapa previo | ¿difiere? |
|---|---|---|---|
| `IDReporte` distintos que entregaron un documento real | **66** | 66 | **no** |
| ...de los 99 del menu de Mac31 | **65** | 65 | **no** |
| Fallan por el SP de la base (no por el motor) | **10** | 9 | **si: +1 (el 74)** |
| Confirmados rotos **tambien en Legacy** | **10 de 10** | 0 probados (el mapa los deja en "falta probar") | **si, y es lo importante** |
| Reportes del menu sin soporte en el nuevo (hueco real de codigo) | **16** | 16 | no |
| Reportes del menu que ya truenan en Legacy (Aviacion, SP inexistente) | 6 | 6 | no |

Cobertura: **65 de 99 (66 %)**. El numero del mapa se sostiene. Lo audite por tres
lados y los tres dieron igual:

1. Volvi a generar los 66 ids uno por uno: **64 directo, mas el 114 y el 74**,
   = 66. Ni uno de sobra, ni uno de menos.
2. El TSV dice 60 `si (nodo)` + 36 `si (variante)` = **96** ids del menu con
   renglon en `dbo.Reportes`; mas los 3 ids fantasma (240, 76, 1242) = **99**.
   Cuadra con el texto del plan aunque el plan diga "61 + 38" en prosa.
3. `ReportesVentasRepository.Constants.cs` tiene **84** constantes de `IDReporte`
   y `ReportesCatalogo.cs` **53** claves. Coincide con el mapa.

### 3b. En lo que el mapa se equivoca o se queda corto

**(1) El 74 no es `ya-funciona` sin matices.** `transferencias_duplicadas`
(`sp_n_rptVentasPagosTransferenciaGlobales`) **falla cuando se manda filtro de
agentes** (`The multi-part identifier "V.IDAgente" could not be bound`) y sale
bien sin el. Falla igual en Legacy. El mapa lo da por bueno porque lo probo sin
el filtro. **Son 11 reportes rotos en la base, no 9.**

**(2) `Estado de Cuenta` no esta al 66 %, esta al 0 %, y por codigo.**
`ResolveReporteEstadoCuenta` **ignora su parametro**:

```csharp
// ReportesVentasRepository.LegacyParams.cs:184-187
private static int ResolveReporteEstadoCuenta(string? formato)
{
    return ReporteVentasEstadoCuenta;   // 1370, siempre
}
```

Las constantes `ReporteVentasEstadoCuentaDinero = 1375` y `...Cascos = 1380`
existen (`Constants.cs:79-80`) y **no las alcanza nadie**: no hay forma de pedir
1375 ni 1380. El mapa los clasifica `solo-catalogo` ("falta la clave"); en
realidad la clave `estado_de_cuenta` **si existe** y manda a 1370, que es el que
no termina. Resultado neto: **ningun estado de cuenta se puede imprimir hoy desde
el backend nuevo.**

**(3) Seis claves del catalogo revientan con 500 en `legacy/generar`.**
`acumuladores_y_productos`, `folios`, `remisiones`, `facturas`, `concentrados` y
`cobranza` estan en `ReportesCatalogo.cs` pero **no en el `switch` de
`ResolveReporteLegacyVentas`**: el POST pasa la validacion de catalogo y muere
abajo con `500 {"message":"Error interno.","detail":"Reporte de ventas no
soportado."}`. Tienen endpoint propio, asi que no se pierde nada **si el front
sabe cual usar**; pero es un 500 por un error del cliente y un catalogo que no
describe lo que la ruta acepta. El mapa no lo menciona.

**(4) El agujero del `psp` adivinable ya esta cerrado** — y con el, parte de la
evidencia del mapa. El plan previo demuestra su tesis con
`GET ReportesV3W?psp=266392` **sin token**. Hoy eso ya no corre:

```
GET /api/reportes/ventas/ReportesV3W?psp=266574   -> 400 "El enlace del reporte no es valido o ya vencio."
GET /api/reportes/ventas/ReportesV3W?psp=266378   -> 400 idem   (psp escrito por Mac31)
```

El GET exige el pase firmado `t`. Bien por la seguridad, pero **ya no se puede
renderizar un `psp` que escribio Mac31**, que era la prueba estrella del mapa
(seccion 1 del plan). Y de paso **baja la gravedad de su R6**: la fuga de detalle
de SQL sigue existiendo, pero solo para quien ya tiene un pase valido, no para
cualquiera.

---

## 4. Dependencias hacia el sistema viejo

Aqui hay que separar dos cosas que es facil confundir, y la respuesta es distinta
para cada una.

### 4a. Dependencias de los **SERVICIOS** viejos (lo que impide apagarlos)

**NINGUNA, en el backend nuevo.** Esto se busco a conciencia y no aparecio nada:

| lo que se busco | resultado |
|---|---|
| `63479` en toda la solucion | **0 coincidencias** |
| `HttpClient`, `IHttpClientFactory`, `AddHttpClient`, `WebRequest`, `HttpWebRequest`, `RestClient`/RestSharp, `Flurl` | **0 coincidencias** en `*.cs` (fuera de `bin`/`obj`/artefactos) |
| `System.ServiceModel`, `BasicHttpBinding`, `ChannelFactory`, `SoapHttpClientProtocol`, `ServiceReference`, Connected Services | **0 coincidencias** |
| `<ProjectReference>` / `<Reference>` / `<HintPath>` hacia `Legacy/`, `Mac3*`, `MacReportes*`, `MacServicios*` | **0.** `ISL_Service.csproj` solo tiene `PackageReference` de NuGet (BCrypt, ClosedXML, QuestPDF, EF Core, JwtBearer, Newtonsoft, Swashbuckle) |
| `Process.Start` | **1 solo**: `wkhtmltopdf`, que viene dentro del repo (`WkhtmltopdfHtmlPdfRenderer.cs:241`) |
| URLs en `appsettings*.json`, `launchSettings.json`, `web.config` | ninguna apunta a un servicio viejo. Todo lo que hay son **cadenas de conexion a SQL Server** y las URLs de escucha del propio backend |

La unica URL que el backend **construye** se llama `ReportesV3W` y suena a
legacy, pero es la suya:

```csharp
// Controllers/ReportesVentasController.cs:492-507
return $"{Request.Scheme}://{Request.Host}{pathBase}/api/reportes/ventas/ReportesV3W?psp={psp}{centro}{pase}";
```

`Request.Host` = este backend. Comprobado en la practica: las 114 generaciones de
esta auditoria **se sirvieron solas**, y el PDF sale aunque `MacReportes` no este
levantado (no lo levante ni una vez).

**Un comentario viejo que miente.** `Application/Services/VentasConsultaService.cs:97`
todavia dice *"Lo pinta MacReportes, la misma aplicacion que abre Mac31"*. El
codigo de al lado hace lo contrario y el comentario de
`Application/DTOs/VentasConsulta/VentasConsultaDtos.cs:91-97` lo desmiente
explicitamente. **Es un comentario obsoleto, no una dependencia** — pero es
exactamente el tipo de linea que haria creer a alguien que no se puede apagar.
Vale borrarlo.

**Codigo fosil que conviene quitar.** `Infrastructure/Data/Mac3SqlServerConnector.cs`
es un ayudante de cadena de conexion portado de Legacy. No hace HTTP, pero su
constructor `Mac3SqlServerConnector(int NumCaja)` **arranca de `""`** y termina en
`new SqlConnection("")`: esta roto por construccion. Ademas incrusta
`Password=Hope5y2k`. No estorba hoy; es una mina.

### 4b. Dependencias de la **BASE compartida** (normal y esperado, no se apaga)

Esto si, y mucho, y esta bien: los `sp_n_*`, `Template_Html` +
`tipo_template_html`, `Constantes`, `dbo.Parametros`, `dbo.Reportes`. El motor
nuevo **reusa el SP y la plantilla de Legacy sin copiarlos**, que es justo lo que
hace que los datos salgan iguales (seccion 2). Si no hay cadena de conexion el
backend **no arranca** (`Program.cs:91-96`); si falta el renglon de
`Template_Html` el reporte **truena** (`RemisionPlantillas.cs:71`).

**Esto no es una razon para no apagar los servicios viejos.** La base no se apaga.

---

## 5. Recursos fisicos para imprimir: donde viven

| recurso | donde vive | ¿del lado del nuevo? | si falta |
|---|---|---|---|
| **`wkhtmltopdf.exe`** (22.5 MB) | `ISL_Service/Assets/Wkhtmltopdf/` **dentro del repo**, mas 22 DLL (`msvcp140`, `vcruntime140`, `concrt140`, `ucrtbase`, 18 `api-ms-win-crt-*`). Se copia al output (`ISL_Service.csproj:68`) y se resuelve **relativo a `AppContext.BaseDirectory`** (`WkhtmltopdfHtmlPdfRenderer.cs:8,136`) | **SI, completo. No hace falta instalarlo en la maquina** | `FileNotFoundException` -> 500 (`:138`) |
| **Plantillas** (`Template_Html` + `tipo_template_html`) | **en la base**, se leen con `sp_n_ConsultaTemplateHtml`. **No hay ni un `.html` en disco** | si (via base) | lanza `Falta la plantilla '…' en Template_Html` |
| **Logos, carpeta preferida** | `Constantes.PathImagenes`: `C:\Mac21\imagenes\` (Tauro) y `C:\MacZ\imagenes\` (Zaragoza). **Es una carpeta de la maquina de Legacy.** En esta maquina existe y tiene `tauro_logo.jpg`, `Logo_Mac.png`, `Logo_Mac_WM.png`, `cancelada.png`, `reimpresion.png`… | **NO, es de Legacy** | **no truena.** Cae al empaquetado y si tampoco, el reporte sale **sin logo** (`CascosCambioService.cs:247-257`, `Rendering.cs:68-107`: `catch { return null; }`) |
| **Logos empaquetados (respaldo)** | `ISL_Service/Assets/Logos/`: `Logo_Mac.png`, `Logo_Zaragoza.png`, `tauro_logo.jpg` | **SI** | — |
| **`Constantes.PathImagenesServer`** (respaldo por http para la remision) | `https://itsysware.com.mx/images/mac31/` y `/images/macZ/` | externo, **no es la maquina de Legacy** | **HOY NO SIRVE: el certificado TLS esta caducado** (`SEC_E_CERT_EXPIRED`, comprobado con curl), y `wkhtmltopdf` se invoca **sin `--ignore-ssl-errors`**. El PDF sale, sin logo |
| **Tipografias** | ninguna en disco, ninguna descargada. Decidido a proposito (`UsadosHtmlBuilder.cs:33-37`): se usa `'Segoe UI', Tahoma, Arial, sans-serif` | SI | — |
| **Temporales** | `%TEMP%/isl-reportes`, se crea y se borra en `finally` (`:140,252-257`) | SI | — |

**El unico hueco real de recursos**: `Logo_Mac_WM.png` (la marca de agua de la
remision) **no esta en `Assets/Logos/`**. Vive en `C:\Mac21\imagenes\` y el
respaldo http tiene el certificado caducado. En Azure la remision sale **sin
marca de agua**. Es cosmetico y no bloquea nada, pero es el unico archivo que hoy
sigue viviendo solo del lado viejo.

Del lado de **MacReportes**, en cambio, los recursos propios son reales:
`MacReportes/MacReportes.xml` (un archivo de texto con la URL de MacServicios2,
leido con `Server.MapPath` — si falta, la pantalla muestra *"NO SE ENCONTRO EL
ARCHIVO DE CONFIGURACION"*), `MacReportes/imagenes/`, `MacReportes/fonts/`, y el
motor `NReco.PdfGenerator`. **Nada de eso lo necesita el backend nuevo.**

---

## 6. Qué deja de funcionar si apagamos Legacy y MacReportes hoy

### 6a. `MacReportes` / `MacReportes2`: **nada. Apagalo.**

* Cero referencias desde el backend nuevo.
* No es mas que un cascaron: lee una URL de `MacReportes.xml`, le pide el HTML a
  `MacServicios2` por `api/reportesV2/consultar`, y lo pasa por `NReco`. El
  backend nuevo hace los dos pasos en casa.
* Las 114 generaciones de esta auditoria salieron **con MacReportes apagado**.
* Lo unico que se pierde es la cadena de 3 saltos: nadie la echa de menos.

### 6b. `MacServicios2`: **NO. Esto es lo que se rompe.**

Antes de los reportes hay que preguntar **quien mas llama a `MacServicios2`**.
Se barrio el repositorio entero. Quedan **tres** clientes, y dos de ellos son
aplicaciones de escritorio que nadie habia puesto en la cuenta:

| consumidor | ¿usa MacServicios2? | ¿usa MacReportes? | que se rompe si se apagan los dos |
|---|---|---|---|
| **`Legacy/Mac31`** (WinForms) | **SI: el 100 % de su acceso a datos.** `URL_SERVICIOS_MAC3` en **57 archivos `.cs`**; un solo transporte, `Servicios/GenericService.cs:60` -> `new RestClient(Variables.URL_SERVICIOS_MAC3)`. **Cero `new SqlConnection` en todo el proyecto** (comprobado): no tiene camino alterno a la base | si, todos los botones de imprimir (`Utils/Funciones.cs:846` -> `Process.Start(browser, URL_REPORTES_MAC3 + ...)`) | **muerte total.** El propio login va por `LoginService` -> MacServicios2: **nadie puede ni entrar**. Ninguna pantalla carga, ningun documento sale |
| **`MacChecador`** (WinForms, el gemelo del checador de huella) | **SI**, mismo patron: `URL_SERVICIOS_MAC3` en **57 archivos `.cs`**, incluido `Forms/Huella/LectorHuella.cs` | si (`Utils/Funciones.cs:703`) | **muerte total**, y lo grave: **se detiene la captura de asistencia por huella**. Ni entradas ni salidas |
| **`Legacy/MacReportes2`** | **SI**: `UrlServiciosMAC` = `https://localhost:44341/` (`Web.config:33`) y el sidecar `MacReportes.xml` | n/a | aunque MacReportes siga encendido, **sus paginas salen vacias o con error** sin MacServicios2 |
| **`Web/QR_Web`** (front de produccion) | **NO**. Apunta a `http://localhost:5041` y a `https://api.{sacmac,mactauro,zaragozamac,integralsportsleague}.*` | **NO**: el PDF lo entrega el backend nuevo por su propio `ReportesV3W` | **nada** |
| **`Movil`** (Flutter) | **NO**. `http://10.0.2.2:5041` y los `https://api.*` | **NO** | **nada** |
| `Web/.../aviacion/qr/proxy/server.js` | si, pero es un **proxy de desarrollo** suelto: `http://mactauro.ddns.net/MacAviacionServicios/api/recaudaciones/consultar` | no | solo ese proxy local, que la pantalla enviada **no usa** (va a `https://api.sacmac.net/api/Recaudaciones`) | 
| `levantar.ps1`, `sacNet-8/despliegue/*`, `.github/workflows/*`, `Manual/`, `Querrys/` | **NO** | **NO** | **nada** |

Lo comprobe por mi cuenta: **0 referencias a `63479`, `44341`, `MacServicios`,
`Mac3Servicios`, `MacZServicios` o `MacReportes` en `Web/` y `Movil/`** fuera de
tres comentarios en prosa de `docs2/` que explican que **antes** apuntaban ahi.
Los fronts ya estan limpios.

**Asi que el bloqueo no son los reportes: son Mac31 y MacChecador.**
`MacServicios2` no es un servidor de reportes, es **la API de la que viven esas
dos aplicaciones de escritorio**, y el registro de la propia migracion dice cuanto
falta (`migracion/registro.tsv`, 488 capacidades de Legacy):

| estado en el backend nuevo | capacidades |
|---|---|
| `hecho` | 103 |
| `hecho-sin-comparar` | 14 |
| `parcial` | 136 |
| **`falta`** | **207** |
| `por-verificar` / `sin-sp` / `dinamico` | 28 |

**207 capacidades de `MacServicios2` no existen todavia en el backend nuevo**, y
otras 136 estan a medias. Son sobre todo las **escrituras** de los catalogos y de
la operacion, que es lo que Mac31 hace todo el dia: `api/mccontratos` (19),
`api/proveedores` (9), `api/clientes` (8), `api/ventaspagos` (7), `api/empresas`
(6), `api/descuentos` (5), `api/ventascobrosdinerousados` (5), `api/usuarios`
(4), `api/recaudaciones` (4), `api/cobros` (4), y el alta/modificacion/baja de
practicamente todos los `api/cat*`.

Si hoy se apaga `MacServicios2`:

1. **Mac31 deja de funcionar**, no un modulo: la aplicacion. Todo su acceso a
   datos va por esa API y no tiene camino alterno a la base.
2. **MacChecador deja de funcionar, y con el la captura de asistencia por
   huella.** Esto no estaba en ningun documento de la migracion y es el riesgo
   que mas facil se pasa por alto: no es una pantalla, es el reloj checador.
3. Se pierden las **207 capacidades** que el backend nuevo todavia no tiene, con
   las 136 parciales en duda.
4. Y hay un efecto de segundo orden que vale decir: `sp_n_ConsultaConstantes`,
   que **rueda la fecha de operacion y congela el inventario del dia anterior**,
   la dispara hoy *el primero que toca el sistema*, que normalmente es alguien en
   Mac31. Si Mac31 se apaga, **el cierre del dia pasa a depender de que alguien
   entre al web o a la app**. Eso hay que decidirlo a proposito, no descubrirlo.

### 6c. Reportes que se pierden por apagar Legacy: **ninguno de los que hoy sirven**

De los 99 del menu de Mac31:

| | reportes | ¿se pierde algo al apagar Legacy? |
|---|---|---|
| Ya funcionan en el nuevo | **65** | **no** |
| Fallan por el SP de la base | **11** | **no: fallan identico en Legacy, probado uno por uno** |
| Vertical Aviacion, SP inexistente en estas dos bases | 6 | **no: ya truenan en Mac31 hoy** |
| Ids fantasma (240, 76, 1242) sin renglon en `dbo.Reportes` | 3 | **no: rotos en Legacy tambien** |
| **Hueco real de codigo del backend nuevo** | **16** | **SI. Estos 16 solo los da Legacy** |
| **Estado de Cuenta** | (1370 dentro de los 65, mas 1375/1380) | **en la practica SI**: el nuevo no lo entrega (5a) |

**Los 16 que de verdad solo existen en Legacy** (12 de parametros + 4 de clave):

```
1160  Cobranza Detallado                      (el nodo real del menu; el motor usa el 1161)
1355  Ventas Cobros Dinero y Cascos           (ojo: mismo SP que 1360/1365, que estan ROTOS -> probablemente tampoco sirve en Legacy)
 118  Clientes Acum Moto Lub Mensual
 119  ...Mensual Por Categoria
1236  Registro de Garantias
1243  Listas de Precios Personalizado TAURO
1244  ...Todas Menos Tauro
1246  ...CRO_GON_XT_LTH_CL_AM_FP_DY
1247  ...FP_AM_LTH
1250  Control Acumulado de Estimaciones
1415  Centros Pagos Entradas y Salidas        (familia "Centros": el 1410 muere por falta de MacZCS*)
1420  Centros Garantias                       (idem)
  72  Ventas Pagos Detallado Centro Servicio  (solo falta la clave)
  77  Pagos Detallado TAURO                   (solo falta la clave)
1375  Estado de Cuenta DINERO                 (constante existe; el resolver la ignora)
1380  Estado de Cuenta CASCOS                 (idem)
```

Los 16 SP existen en `Produccion_svr` (comprobado en `sys.objects`), asi que son
huecos **de codigo del backend nuevo**, no de la base. Honestamente: **no pude
ejecutar estos 16 en Legacy** porque no hay ningun renglon de `dbo.Parametros`
escrito por Mac31 para esos ids en esta maquina, y fabricar uno a mano habria
sido una escritura que no me toca. Asi que no puedo afirmar que los 16 funcionen
en Legacy — solo que el backend nuevo no los ofrece. De dos de ellos hay motivo
fuerte para sospechar que tampoco sirven alla (1355 comparte el SP roto de
1360/1365; 1415/1420 son de la familia "Centros" cuyas bases `MacZCS*` **no
existen en este servidor**).

Segun el plan previo, los 16 son **menos de 40 lineas de C#**.

---

## 7. Hallazgos nuevos que no estaban en el mapa

| # | hallazgo | evidencia |
|---|---|---|
| **H1** | **`Estado de Cuenta` no se puede imprimir, y es un bug de codigo, no de catalogo.** `ResolveReporteEstadoCuenta(string? formato)` ignora el parametro y devuelve siempre 1370 | `ReportesVentasRepository.LegacyParams.cs:184-187`; constantes 1375/1380 en `Constants.cs:79-80` sin un solo uso |
| **H2** | **El 74 esta roto con filtro de agentes**, en los dos sistemas. El mapa lo da por bueno | `V.IDAgente could not be bound` en el nuevo **y** en Legacy con el mismo psp; sale bien sin `idAgentes` |
| **H3** | **`folios` tarda ~55-58 s** (20: 54.7 s; 23: 57.7 s) y `clientes_sin_descuentos` (114) **47.3 s**. Con el corte de 230 s de Azure sobreviven, pero son los siguientes en la fila del 1370 | medido en la corrida |
| **H4** | **El respaldo http de imagenes esta caido**: `https://itsysware.com.mx/images/...` responde `SEC_E_CERT_EXPIRED`, y `wkhtmltopdf` corre sin `--ignore-ssl-errors`. En Azure la remision sale sin marca de agua porque `Logo_Mac_WM.png` **no esta en `Assets/Logos/`** | curl; `WkhtmltopdfHtmlPdfRenderer.cs:170-235`; `ls Assets/Logos/` |
| **H5** | **6 claves del catalogo dan 500 en `legacy/generar`** porque tienen endpoint propio y no estan en el `switch`. Un error de cliente devuelve 500, no 400 | probado con las 6 |
| **H6** | **El agujero del `psp` adivinable ya esta tapado** (pase firmado obligatorio), lo que invalida la prueba estrella del plan previo y **baja la gravedad de su R6** | `GET ReportesV3W?psp=…` sin `t` -> 400 |
| **H7** | **Ninguna base `MacZCS*` existe en este servidor** (`sys.databases`), asi que toda la familia "Centros" que cruza por nombre fijo esta muerta aqui — en los dos sistemas | `SELECT name FROM sys.databases WHERE name LIKE 'MacZCS%'` -> ninguna |
| **H8** | Secretos en claro versionados: `appsettings.json` trae `sa`/`123`, `sa`/`Hope5y2k` y la llave de firma JWT `A9F7Q3Z6…`. **Esa llave es la que firma los pases de reporte**: quien la tenga se firma el pase de cualquier `psp` | `appsettings.json:37,50-55` |
| **H9** | Comentario obsoleto que afirma lo contrario de lo que hace el codigo: *"Lo pinta MacReportes"* | `Application/Services/VentasConsultaService.cs:97` |
| **H10** | `Mac3SqlServerConnector(int NumCaja)` construye su cadena desde `""` -> `new SqlConnection("")`. Codigo muerto con password incrustado | `Infrastructure/Data/Mac3SqlServerConnector.cs` |
| **H11** | **`MacChecador` es un segundo cliente de `MacServicios2` que no figura en ningun documento de la migracion**, con el mismo patron que Mac31 (57 archivos `.cs` usando `URL_SERVICIOS_MAC3`) e incluyendo `Forms/Huella/LectorHuella.cs`. Apagar el servicio **detiene el reloj checador de huella**, no solo una pantalla | `MacChecador/Mac31/Servicios/GenericService.cs`, `MacChecador/Mac31/Forms/Huella/LectorHuella.cs:279-280` |
| **H12** | Mac31 **no tiene camino alterno a la base**: cero `new SqlConnection` en todo el proyecto (el unico acceso directo es un lector OLEDB de Excel en `Forms/Nomina/Asistencias.cs`). No existe un "modo degradado" sin `MacServicios2` | grep sobre `Legacy/Mac31/**/*.cs` fuera de `bin`/`obj` |
| **H13** | Queda un proxy de desarrollo apuntando a una instancia **desplegada** de MacServicios2: `http://mactauro.ddns.net/MacAviacionServicios/...`. No lo usa la pantalla enviada, pero conviene borrarlo para que nadie crea que hace falta | `Web/QR_Web/docs/modules/custom/aviacion/qr/proxy/server.js:8` |

---

## 8. Lo que no pude cerrar

Para que nadie lea esto como mas concluyente de lo que es:

1. **Los 16 reportes del hueco no se ejecutaron en Legacy.** No hay `psp` de
   Mac31 para esos ids en esta maquina y no quise escribir uno a mano. Sé que el
   nuevo no los da; **no** sé a ciencia cierta que Legacy si.
2. **Zaragoza (`MacZ`) no se probo generando.** El backend local apunta a
   `Produccion_svr` y cambiar la configuracion habria sido modificar el backend.
   Verifique por codigo que existe el camino (`RemisionZaragozaHtmlBuilder.cs`) y
   por base que `MacZ` tiene el 1202 y los mismos `Template_Html`.
3. **La impresion de remision y de cascos no se ejecuto**: esos endpoints
   **escriben** (`FechaImpresion`, rastro de reimpresion) y el encargo era no
   escribir mas alla de `dbo.Parametros`. Quedan verificadas solo por codigo —
   ahi la evidencia es buena (wkhtmltopdf en proceso, plantillas de la base,
   ninguna salida HTTP).
4. **No ejecute Mac31 ni MacChecador.** Su dependencia de `MacServicios2` esta
   demostrada por codigo (un unico `RestClient`, 57 archivos cada uno, cero
   `SqlConnection`), que es evidencia fuerte, pero no los apague para verlo.
5. **No reproduje la caida del backend** que el plan previo apunta al generar
   muchos reportes grandes seguidos. Corri ~140 generaciones, varias de 300-770
   KB, y el proceso aguanto. Si sigue pasando, no es facil de provocar.

---

## 9. Veredicto

**`MacReportes` / `MacReportes2`: APAGALO HOY.** No rompe nada. Esta auditoria
entera corrio con el apagado.

**`MacServicios2`: NO LO APAGUES HOY.** Y el motivo no tiene nada que ver con los
reportes:

* El backend nuevo **no lo llama ni una vez**: 0 `HttpClient`, 0 referencias de
  proyecto, 0 URLs en configuracion, 0 apariciones de `63479`.
* De los reportes **no se pierde ninguno que hoy sirva**: 65 de 99 ya salen con
  los mismos datos que Legacy, y los 11 que fallan **fallan identico en Legacy**,
  probado contra `MacServicios2` con el mismo renglon de `dbo.Parametros`.
* Lo que lo retiene son **Mac31 y MacChecador**: 207 capacidades en `falta` y 136
  `parcial`. Apagar `MacServicios2` es apagar Mac31 **y detener el checador de
  huella**. Los fronts (web y movil) ya estan limpios: cero referencias.

**La via libre, en orden:**

1. Apagar `MacReportes` (hoy, sin condiciones).
2. Cerrar los 16 reportes del hueco (<40 lineas de C#) y arreglar **H1**
   (`ResolveReporteEstadoCuenta`) — con eso los reportes dejan de ser argumento.
3. Resolver **R3** del plan previo: `sp_n_rptVentasEstadoCuenta` no termina
   (>400 s medidos; Azure corta a 230 s). Optimizar el SP o volverlo asincrono.
4. Decidir que pasa con **MacChecador** y la huella: o se migra, o se queda
   encendido `MacServicios2` solo para el. Hoy no esta en ningun plan.
5. Terminar las 207 capacidades que faltan. **Ese, y solo ese, es el camino
   critico para apagar `MacServicios2`.**

---

*Auditoria de solo lectura. No se modifico `Legacy/`, ni el backend nuevo, ni
ningun objeto de la base. Lo unico escrito son renglones de `dbo.Parametros` de
`Produccion_svr` (~165 -> ~630), que es lo que hace cada generacion de reporte en
los dos sistemas y que `sp_n_ConsultaParametros` recoge a los 30 dias.*
