# Zaragoza medida de verdad (empresa `ZARA`, base `MacZ`)

Hecho el 7-oct-2026. Todo número de esta página sale de una llamada HTTP real, de
una consulta real a la base, o de un documento que se descargó y se abrió.

Hasta ahora **toda** la migración se había medido contra `Produccion_svr` (Tauro).
Este documento es la primera medición contra **Zaragoza**, y lo que destapa es que
*medir contra una sola empresa deja huecos que no se ven*: la mitad del banco de
comparación no estaba probando nada en Zaragoza, y un reporte llevaba meses
imprimiéndose con la plantilla de la otra empresa.

## Cómo se levantó (y qué NO se tocó)

| | |
|---|---|
| Legacy contra Zaragoza | `http://localhost:63480` — **copia** de `Legacy/MacServicios2/Mac3Servicios` en `C:\tmp\legacy-maczy`, con el `Web.config` **de la copia** apuntando a `LAP-JUAND`/`MacZ` |
| Legacy contra Tauro | `http://localhost:63479`, sin tocar |
| Backend nuevo contra Zaragoza | `http://127.0.0.1:5062` |
| Backend nuevo contra Tauro | `http://127.0.0.1:5064` |
| Usuario | `BancoPruebas` / `Banco2026` |

**Nada de `Legacy/` se modificó.** **Ningún objeto de la base se modificó**: ni un
procedimiento, ni `dbo.Reportes`, ni `Template_Html`. Lo único que se escribió son
renglones de `dbo.Parametros`, que es lo que hace cada generación de reporte en
los dos sistemas.

### La única escritura deliberada: el cierre del día

`MacZ` estaba en `FechaOperacion = 2026-10-02` y hoy es `2026-10-07`.
`sp_n_ConsultaConstantes` **no consulta: hace el cierre del día** (congela el
inventario y rueda la fecha), y la dispara el primero que toque el sistema —lo
llaman 155 de las 488 capacidades de Legacy. Si se dispara a media corrida, los
dos backends no leen el mismo estado y las diferencias que salen son del orden en
que se preguntó, no del código.

Así que se rodó **una vez, a propósito y por el lado de Legacy**, antes de
comparar nada, que es lo que manda `CUIDADO-consultas-que-escriben.md`.
`FechaOperacion` quedó en `2026-10-07` y no se movió durante ninguna corrida
(el comparador lo vigila y lo habría dicho).

---

## 1. El banco de comparación contra Zaragoza

```
133 coinciden, 0 con diferencias, 2 no comparables, 135 casos
```

Y Tauro, corrido después con los mismos casos: **135 de 135, 0 diferencias**, sin
cambio respecto de la línea base.

### Lo que de verdad cambió: 59 casos no probaban nada

El comparador contaba desde su primera versión los casos que comparan **vacío
contra vacío** —salen `OK` y no prueban nada— pero **nunca los imprimía**. Al
mirarlos contra Zaragoza:

| | |
|---|---|
| Casos que coincidían **sin ver un renglón** (primera corrida) | **59 de 135** |
| Después de reparametrizar | **38** |
| De esos 38, vacíos **también** en Tauro | **18** |
| Vacíos **solo** en Zaragoza | **20** |
| Casos que en **Tauro** estaban vacíos y en Zaragoza **sí comparan datos** | **7** |

**41 casos** recibieron su juego de parámetros de Zaragoza. No se les cambiaron
los ids: cada caso declara ahora un bloque `por_empresa`, y la empresa se elige
con `MAC_EMPRESA`. Por omisión sigue siendo `tauro`, así que las corridas de
siempre no cambian de comportamiento.

Los ids no son portables: el `IDVenta` 369614 existe en Tauro y en `MacZ` el más
alto es **140964**. Un caso escrito con ids de Tauro corrido contra Zaragoza
devuelve vacío de los dos lados y sale verde. Esa era la mentira.

### Los siete casos apuntados para repetirse contra MacZ: ya comparan

Eran los peores del banco, porque estaban escritos **a ciegas** sobre tablas
vacías en Tauro: sin `solo_campos`, sin notas, comparando nada contra nada.

| caso | en Tauro | en Zaragoza, ahora |
|---|---|---|
| `ola4-cobro-clientes` | `dbo.Cobros` = 0 renglones | **42 campos comparados, los 42 iguales** |
| `ola4-cobros-consultar` | vacío | **57 de 57** |
| `ola4-usados-cobros-dinero-consultar` | vacío | **93 de 93** |
| `ola4-usados-excedentes-consultar` | vacío | **39 de 39** |
| `ola2-cobros-centro-servicio` | vacío | **8 de 8** |
| `ola2-usados-cobro-dinero-detalle` | vacío | **55 de 55** |
| `ola3-huellas-usuarios` | vacío | **6 de 6** |

El ruido que salía al principio (cientos de "FALTA en el nuevo" con `0` o `null`
del lado de Legacy) **no era dato**: es el cascarón del DTO de Legacy, que declara
columnas que el procedimiento no devuelve y `DataTableToList` rellena con el valor
por omisión. Cada caso lo dice ahora por su nombre, columna por columna.

---

## 2. Dos defectos de Legacy que solo se ven en Zaragoza

Los dos son de la **misma familia**, y los dos esconden un número: *el
procedimiento devuelve una columna con un nombre y el DTO de Legacy la declara con
otro, así que el mapeo por nombre exacto no empareja y Legacy la entrega en `0`
siempre.* **El backend nuevo entrega lo que dice la base.**

### 2.1 `SaldoFavorCascos`: Legacy enseña 0, la base dice 25

`sp_n_ConsultarCobranzaDetalle` devuelve `SaldoFavor` y `SaldoFavorCascos` (F
mayúscula, como se llaman en `dbo.Ventas`). El DTO de Legacy las declara
`Saldofavor` y `SaldofavorCascos` (f minúscula).

```
cobranza 49334   legacy SaldofavorCascos = 0     nuevo SaldoFavorCascos = 25
dbo.Ventas -> las columnas se llaman SaldoFavor y SaldoFavorCascos
```

En Tauro no se nota porque la cobranza de prueba no tenía saldo a favor de cascos.

> **Es un número que Mac31 no enseña y el web sí va a enseñar.** Conviene que lo
> sepa quien revise la pantalla de cobranza, porque va a ver aparecer un saldo que
> antes no estaba.

### 2.2 `IDAgente`: Legacy enseña 0 en las 208 remisiones

`sp_n_ConsultaVentas` declara su tabla temporal con la columna **`IDagente`** (a
minúscula) y el DTO de Legacy la llama `IDAgente`:

```
spcv (MacZ y Produccion_svr, la MISMA linea en las dos):
  ( IDVenta int, ..., IDUsuario int, IDagente int, IDTipoDocumento int, ...
legacy  IDAgente = 0, 0, 0 ...        nuevo  IDagente = 7, 26, 30 ...
```

Está igual en las dos bases. En Tauro no se nota porque allá `Ventas.IDAgente`
viene en `0` en esos renglones; en Zaragoza está poblada y la pérdida se ve.

### 2.3 Y uno de Mac31: código muerto en el crédito de Zaragoza

Mac31 solo llama al servicio de crédito disponible cuando la funcionalidad
contiene `"TAU"` (`Ventas.cs:1627`). `Constantes.Funcionalidad` vale `TAU` en
Tauro y **`ZARA`** en `MacZ`. Pero **dentro** de ese bloque hay un
`if (funcionalidad.Contains("ZARA"))` que pinta `"MONTO DE PAGOS $..."`
(`Ventas.cs:1643`): como `"ZARA"` no contiene `"TAU"`, esa rama **nunca se
ejecuta en Zaragoza**.

El servicio de Legacy sí sabe contestar (`sumaPagosMeses` 114 332,
`promedioPagosMeses` 38 110.67 para el cliente 33320), pero **Mac31 no se lo
pregunta**, y el backend nuevo copia esa guarda. Si el negocio quiere ese dato en
Zaragoza es **una decisión de producto**, no un defecto: quitar la guarda es una
línea.

---

## 3. Los dos casos no comparables en Zaragoza

No es que fallen: es que **a los dos lados se les está preguntando otra cosa**, y
por diseño. Salen aparte del conteo, con su razón escrita, y no cuentan como
coincidencia.

| caso | por qué |
|---|---|
| `Concentrado abierto que tiene un folio de venta` | Mac31 solo consulta el concentrado antes de cancelar cuando la funcionalidad contiene `"TAU"` (`ConsultarVentas.cs:2945`). El backend nuevo copia esa guarda, así que en Zaragoza contesta `0` **a propósito**, mientras el servicio pelado de Legacy devuelve el folio 19372 del concentrado abierto 62864. Se verifica en Tauro, donde la guarda sí se abre |
| `Corte de caja - siguiente fecha` | `dbo.Cortes` tiene **0 renglones** en `MacZ`. El nuevo contesta el 404 que declara su contrato; Legacy entrega su DTO en ceros con `FechaCorte = '0001-01-01'`. Un año 1 no es una fecha de corte siguiente. Se verifica en Tauro |

---

## 4. Los reportes contra Zaragoza

| | Zaragoza (`MacZ`) | Tauro (ola 11) |
|---|---|---|
| Del menú vivo de Mac31 que **entregan documento** | **68 de 99 (68.7 %)** | 76 de 99 (77 %) |
| No entregan | **31** | 23 |
| …rotos **también** en el Legacy de su empresa | **21** | 16 |
| …inalcanzables a propósito / sin renglón en `dbo.Reportes` | **10** | 7 |
| **Deuda NUESTRA** | **1**, ya corregida (el 1160/1161) | 0 |
| Comparados contra Legacy con el mismo `psp` | **65 de 68** (los otros 3 Legacy no los entrega) | 76 de 76 |
| **Reportes en los que el nuevo inventa números que Legacy no tiene** | **0** (era 1 antes del arreglo) | 0 |
| Traen contenido real | **51** | 61 |
| Vacíos **en los dos sistemas** | **17** | 13 |

Cuadra: 68 + 21 + 10 = 99.

La ventana usada fue **2026-07-01 a 2026-07-09**, no la de Tauro (15–23 sep 2026),
porque en `MacZ` **no hay un solo movimiento después del 2026-08-17**
(`dbo.Ventas`: 86 893 renglones, del 2023-02-01 al 2026-08-17).

### La predicción del encargo se cumplió

Hay ids que son **otro reporte según la empresa**, y lo decide la base
(`Constantes.Funcionalidad`), no una constante de C#:

- el **71** (Pagos Detallado) y el **1240** (Listas de Precios) **sí** generan en
  Zaragoza y **no** en Tauro;
- el **77** y las cuatro listas personalizadas (1243/1244/1246/1247) **sí** en
  Tauro y **no** aquí.

### Ids cuyo renglón de `dbo.Reportes` difiere entre las dos bases

228 renglones en `Produccion_svr`, **229** en `MacZ`. Difieren 3, y 1 solo existe
en Zaragoza:

| id | en Tauro | en Zaragoza |
|---|---|---|
| **1001** | `Impresion de Remision V2` | **`Reporte de Cascos Excedentes`** |
| **1017** | `Compras Comprobación` / `sp_n_rptComprasComprobacionGlobales` | **`Cobranza Detallado`** / **`sp_n_rptVentasSaldosGlobales`** |
| **1210** | `Remision Centro Servicio` / `sp_n_rptVentasRemisionCentroServicio` | mismo nombre / **`sp_n_rptVentasRemisionSinPrecio`** |
| **1202** | **NO EXISTE** | `Remision Zaragoza Generico` |

Ninguno de los cuatro está en el menú vivo de Mac31, así que **no están en el
denominador de 99 y no hay defecto activo por ellos**. El **1202** ya lo atiende
el backend nuevo por su camino propio (`RemisionImpresionRepository.cs:51`,
`RemisionZaragozaHtmlBuilder.cs`), no por el módulo de reportes.

**El 1017 es la advertencia viva**: en Zaragoza se llama igual y usa el mismo
`spV2` que el 1160/1161/1162, y los cuatro tienen **plantillas distintas**.
Cualquiera que agregue un id por número ahí se equivoca de empresa — que es justo
la trampa en la que caímos.

---

## 5. Lo que falla POR NUESTRO BACKEND, y qué se corrigió

**Uno, y está corregido: el reporte de Cobranza de Zaragoza (1160 → 1161).**

Es lo único de toda la medición —135 casos del banco y 99 reportes— que fallaba
por el backend nuevo y funcionaba en Legacy.

### Qué pasaba

El nodo "Cobranza" se imprimía con la plantilla de Tauro. Pedidos los dos con el
**mismo renglón de `dbo.Parametros`**:

| | antes | Legacy | ahora |
|---|---|---|---|
| importes impresos | **1 298** | 6 734 | **6 733** |
| **números que el nuestro imprime y Legacy no tiene** | **46** | — | **0** |
| columnas numéricas | 4 | 11 | 11 |

### Por qué

No era el dato ni el procedimiento: `sp_n_rptVentasSaldosGlobales` **no mira el
id** y devuelve lo mismo en los dos casos. Era la **plantilla**. Mac31 manda el
1160 en las dos empresas, pero el servicio de Legacy lo cambia por el 1161 cuando
la funcionalidad es `"ZARA"`, antes de elegir la plantilla
(`MacServicios2/.../ReporteV2Service.cs:783`):

```csharp
if ((funcionalidad.ToUpper() == "ZARA") &&
    (IDReporte == (int)enumReportes.VentasCobranzaDetallado))
{
    reporteInputDTO.idReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
    IDReporte = (int)enumReportes.VentasCobranzaDetalladoZara;
}
```

Es **la única rama por empresa que le queda viva** a ese archivo (la de Listas de
Precios Tauro está comentada), y era la única que faltaba copiar. El comentario
del resolver decía *"el 1161 no lo usa ningún nodo: era el que estaba aquí por
descarte"*. Era falso, y **solo se ve midiendo contra Zaragoza**.

### Qué se cambió

| archivo | qué |
|---|---|
| `Application/Reportes/ReporteCobranzaPorEmpresa.cs` (**nuevo**) | una función pura, `Resolver(funcionalidad)`, con el porqué y la línea de Legacy. Aparte del repositorio para poder probarla sin DI ni base |
| `Infrastructure/Repositories/ReportesVentasRepository.LegacyParams.cs` | la clave `cobranza` pasa a resolverse **con las constantes en la mano**, junto a `pagos` y `listas_de_precios`, y sale del `switch` plano |
| `Infrastructure/Repositories/ReportesVentasRepository.cs` | `GenerarCobranzaAsync` resuelve el id **después** de abrir la conexión, porque ahora depende de la base |
| `ISL_Service.Tests/ReporteCobranzaPorEmpresaTests.cs` (**nuevo**) | 14 pruebas: que Zaragoza reciba 1161, que Tauro **siga** recibiendo 1160, y que la condición no se afloje a un "no es Tauro" |

La comparación es por **igualdad exacta con `"ZARA"`**, no "no es Tauro": una
tercera empresa (`MacL`) tiene que seguir con el 1160, que es lo que hace Legacy.
Hay una prueba que lo fija.

**Comprobado en vivo**, no solo en pruebas: Zaragoza devuelve `idReporte 1161` y
Tauro `1160`; el PDF trae los mismos importes y las mismas columnas que Legacy, y
**0 inventados**.

**Pruebas: de 508 a 522, 0 fallos.** Banco: Zaragoza 133/135 + 2 no comparables,
Tauro **135/135 sin cambio**.

---

## 6. Lo que falta POR DESPLIEGUE

De los 124 `sp_w_`/`sp_mo_` que el código declara, **faltan 74 en `MacZ`**. De
esos, **54 son de la liga deportiva (ISL)**, que no es MAC (46 por el nombre + 8
cuyo script vive bajo `Querrys/SL/`), y **uno es un nombre de prueba**
(`sp_w_X`, un fixture de `Ola12AislamientoYFugasTests.cs`).

**Los de MAC que faltan de verdad son 17, y los 17 tienen script.**

| módulo | cuántos | ¿está en Tauro? | script que los instala |
|---|---|---|---|
| **Cascos a Cambio** | **8** | **SÍ** | `Querrys/23 Modulo Cascos A Cambio/03_Tablas_Y_SPs_CascosCambio.sql` (completo), más los incrementales de `Querrys/12 Cascos a cambio/` (20, 21, 22, 23, 25, 29, 31, 32) |
| **Cheques** | **5** | no | `Querrys/Cheques/02_sp_w_InsertarCheque.sql`, `03_sp_w_ActualizarCheque.sql`, `04_sp_w_CambiarEstatusCheque.sql`, `05_sp_w_ConsultarCheques.sql`, `06_sp_w_ConsultarChequeDetalle.sql` |
| **Tarimas** | **4** | no | `Querrys/z sin uso/z Tarimas sin uso/sp_w_{Insertar,Actualizar,Cancelar,Consultar}Tarima(s).sql` |

**Comprobado en vivo, en las dos empresas:**

```
GET /api/cascos-cambio/tipos   Zaragoza -> 500 "Error de base de datos"
                               Tauro    -> 200, con los tipos y sus precios
GET /api/cheques               Zaragoza -> 500      Tauro -> 500
GET /api/tarimas               Zaragoza -> 500      Tauro -> 500
```

O sea: **Cascos a Cambio es el único hueco propio de Zaragoza** (está desplegado
en Tauro y no aquí). **Cheques y Tarimas no están desplegados en NINGUNA de las
dos**: no son deuda de Zaragoza, son dos módulos que nunca se instalaron en esta
máquina. Tarimas además vive en `z sin uso`, lo que sugiere que está apagado a
propósito; **eso lo tiene que confirmar el negocio antes de instalarlo.**

**No se desplegó ninguno.** Son 17 procedimientos y cada uno tiene su script
nombrado arriba.

### Los dos que la medición anterior contaba y no son despliegue

`sp_w_ConsultaVentas` y `sp_w_CancelarVentaDelConcentrado` **no existen y no hace
falta que existan ahora**: no los llama nadie. Aparecen únicamente dentro de
comentarios, como lo que *haría falta* para volver atómicas dos operaciones
(`VentasConsultaService.cs:115` y `VentasCancelacionService.cs:127`). Crearlos es
trabajo pendiente de diseño, no un despliegue que falte.

---

## 7. Lo que NO se puede verificar en Zaragoza, y por qué

### Del banco: 20 casos vacíos solo en Zaragoza

Las tablas están **vacías en `MacZ`** (conteo real, no supuesto):

```
Catalogo Galleros 0   Catalogo Autos 0        MC_Contratos 0
ProveedoresApoderados/Documentos/Domicilios 0  InventarioPedidos 0
GarantiasRegistro 0   UsadosMovimientos 0     VentasCobrosUsados 0
Cortes 0              EmpleadoPrestamos 0     VentasPagosKilos 0
Gasolina 0            Detalle Descuentos Log 0
```

Y dos que no son "vacío" sino "no existe el dato":

- **Pedidos en estatus 2 (autorizados)**: `MacZ` tiene 84 058 pedidos en estatus 1
  y 6 553 en estatus 11, pero **cero en estatus 2**. Cambiarle el estatus al caso
  sería cambiar lo que el caso prueba, así que se deja y se anota.
- **`ola4-garantias-aceptar-*`**: `sp_n_ConsultarGarantiasAceptar` cruza a
  `[MacZCS1..MacZCS11].dbo.Garantias` y esas bases **no están en este servidor**.
  Falla **igual en el Legacy de Zaragoza**, con el mismo texto:
  `Invalid object name 'MacZCS1.dbo.Garantias'.` Es infraestructura.

### De los reportes

| qué | por qué |
|---|---|
| **1410, 1415, 1420, 1465, 1466, 1470** | `MacZCS1`…`MacZCS11` no existen (la única base `Mac*` de `LAP-JUAND` es `MacZ`), y los **11** centros de `MacZ` tienen `BaseCentro` lleno —en Tauro los 1-6 lo tienen vacío porque son la matriz—. El SP **no acepta filtro de centro**: su cursor recorre los que traigan los datos. No hay nada que mandar desde el código |
| **1405, 1406, 1407** | `Constantes.BDLuro = '[MacL].[dbo].'` en `MacZ` y **vacío** en Tauro. El SP pega `@BDLuro` crudo en su SQL dinámico: en Tauro el JOIN cae a la tabla local y el reporte sale; aquí apunta a `MacL`, que no está restaurada. **Estos seis funcionaban en Tauro y aquí no, y es configuración de la base, no código** |
| **1426, 1428, 1429, 1430, 1432, 1455** | su `spV2` no existe en **ninguna** de las dos bases (vertical Aviación). Haría falta el volcado de producción de esa empresa |
| **72, 76** | `Constantes.EsCentroServicio = 0`; el 76 además no tiene renglón en ninguna base. Hace falta una instalación de centro de servicio |
| **77, 1243, 1244, 1246, 1247** | son los de Tauro; con `Funcionalidad = ZARA` Mac31 y el resolver eligen el 71 y el 1240. Verificados en Tauro |
| **240, 76, 1242** | **sin renglón en `dbo.Reportes` en ninguna de las dos bases**: Legacy tampoco puede generarlos |
| **1235** Garantías | entrega pero vacío **en los dos sistemas**, con 24 854 renglones en `dbo.Garantias`. Es el filtro del propio SP; sin el front de Mac31 al lado no se puede distinguir "filtro correcto" de "filtro roto" |
| los 17 vacíos, con ventana de 16 meses | **ninguno** pasa a traer datos, y los 15 comparables siguen **iguales a Legacy con 0 inventados**. Sus tablas están vacías en `MacZ` |
| ventana ancha en los de alto volumen (20, 21, 23) | `wkhtmltopdf` no termina el PDF de 16 meses. Es volumen del render, no el reporte |

### Un reporte que MEJORA respecto de Tauro

El **1166** (Cobranza Totalizado) daba 500 en Tauro y en Zaragoza **entrega**,
porque `sp_n_rptVentasSaldosGlobales` tiene una rama propia
`IF @Funcionalidad = 'ZARA' AND @IDReporte = 1166`.

---

## 8. Qué desplegar

Por la regla de desplegar solo la empresa en curso, y solo lo que cambió:

1. **Backend** (`sacNet-8`). Es el único artefacto con cambio de código: el
   reporte de Cobranza de Zaragoza. **Sin esto, la pantalla de Cobranza de
   Zaragoza imprime la plantilla de Tauro**, con 4 columnas en vez de 11.
2. **Script de base, solo si se quiere Cascos a Cambio en Zaragoza**:
   `Querrys/23 Modulo Cascos A Cambio/03_Tablas_Y_SPs_CascosCambio.sql` más los
   incrementales de `Querrys/12 Cascos a cambio/`. **No se corrió.** No toca
   legacy: solo crea `sp_w_` y sus tablas.
3. **Nada del móvil ni del web** salió de aquí: no se tocó ni una ruta, ni una
   clave de permiso, ni un contrato.

## 9. Las cuatro preguntas

- **¿Zaragoza está completa?** Para leer, prácticamente sí: 133 de 135 casos del
  banco coinciden y los 2 restantes no son comparables por diseño. Lo que **no**
  está completo es el **módulo de Cascos a Cambio**, que existe en Tauro y aún no
  en `MacZ`, y 20 casos del banco siguen sin poder probarse porque sus tablas
  están vacías en esta base.
- **¿Qué falla por nuestro backend?** Fallaba **una sola cosa** y **ya está
  corregida**: el reporte de Cobranza usaba la plantilla del 1160 en vez del 1161
  de Zaragoza, y por eso imprimía 46 números que Legacy no tiene. Hoy no falla
  nada más por nuestro backend.
- **¿Qué falta por despliegue?** **17 procedimientos**, todos con script
  nombrado: Cascos a Cambio (8, el único hueco propio de Zaragoza), Cheques (5) y
  Tarimas (4) —estos dos no están en ninguna de las dos empresas—. Y, fuera de
  nuestro alcance: las bases `MacZCS1..11`, `MacL`, y los siete defectos de `sp_n_`
  que fallan igual en Legacy.
- **¿Se puede empezar el Web y el Móvil de Zaragoza solo contra este backend?**
  **Sí**, con dos salvedades que conviene decir antes y no después: la pantalla de
  **Cascos a Cambio** no va a funcionar hasta que corra su script, y hay
  **números que van a aparecer y que Mac31 no enseñaba** —el `IDAgente` de las
  remisiones y el `SaldoFavorCascos` de la cobranza—, porque Legacy los perdía por
  un desajuste de nombre entre su DTO y el procedimiento. No son invenciones del
  backend nuevo: son lo que dice la base.
