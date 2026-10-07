# Ola 4 — Las escrituras de ventas

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5081`.

El lote eran **100 capacidades** de ventas en `falta`, `parcial` o `por-verificar`.
Están las 100 en la tabla. Ninguna se recortó.

## El resumen, en números

| | |
|---|---|
| Capacidades del lote | 100 |
| Implementadas y compilando | 100 |
| Filas de la tabla | 101 (una capacidad se partio en dos rutas: la bandera `esAplicacion`) |
| **Comparadas contra Legacy en verde** | **35** |
| — de ellas, lecturas con el comparador | 17 |
| — de ellas, ESCRITURAS con efecto en base idéntico | 18 |
| No verificables, con su motivo escrito | 66 |
| Procedimientos `sp_n_` llamados | 111 |
| Procedimientos `sp_n_` tocados | **0** |
| Pruebas unitarias | 162 (72 nuevas), 0 fallos |

Las 66 no verificables no son 66 excusas distintas: son cinco motivos, y el más
repetido con diferencia es que **Tauro no usa esas pantallas** (`Cobros`,
`CobrosDineroUsado`, `VentasCobrosUsados` y los cortes están vacías, igual que ya
había visto la ola 2). El desglose está más abajo.

## Lo que se hizo, en una frase

Nueve módulos nuevos con **95 endpoints** que llaman a los **mismos 111 `sp_n_`**
que llamaba MacServicios2, con los mismos parámetros y en el mismo orden,
sacando la identidad del token en vez del cuerpo. Ningún `sp_n_` se tocó; nada de
la base se modificó; no se corrió ningún script.

| archivo | qué |
|---|---|
| `Infrastructure/Data/FiltrosLegacy.cs` | fechas, listas con virgulilla, apóstrofos — público para poder probarlo |
| `Infrastructure/Repositories/SpLegacyRepositoryBase.cs` | conexión, ejecución del SP y lectura de todas sus tablas |
| `Application/DTOs/VentasEscrituras/ResultadoLegacyDtos.cs` | el contrato de salida común |
| `Application/DTOs/VentasUsadosOperaciones/` + repo + service + controller | 20 endpoints de cascos usados |
| `Application/DTOs/VentasCobranzaOperaciones/` + repo + service + controller | 14 de cobranza, concentrado y cobro |
| `Application/DTOs/VentasGarantias/` + repo + service + controller | 9 de garantías |
| `Application/DTOs/InventarioOperaciones/` + repo + service + controller | 9 de ajustes, inventario de pedidos y tipos de casco |
| `Application/DTOs/VentasPagosOperaciones/` + repo + service + controller | 13 de pagos y operaciones sueltas de la remisión |
| `Application/DTOs/PedidosOperaciones/` + repo + service + controller | 4 de procesar, cancelar y ajustar pedidos |
| `Application/DTOs/VentasConsultasLegacy/` + repo + service + controller | 8 cabeceras de pantalla de ventas |
| `Application/DTOs/VentasDescuentos/` + repo + service + controller | 12 de descuentos y ajustes de venta |
| `Application/DTOs/VentasFacturacion/` + repo + service + controller | 5 de facturación electrónica |
| `Infrastructure/Facturacion/ReglasFacturacion.cs` | el candado del key, la serie, el folio, el `@Params` y el QR — puro y probado |
| `Infrastructure/Facturacion/FacturapiCliente.cs` | el timbrado por HTTP (el SDK de legacy es de .NET Framework) |
| `Infrastructure/Security/PermissionService.cs` | 50 permisos nuevos en `PermissionCatalogSeeds` |
| `ISL_Service.Tests/FiltrosLegacyOla4Tests.cs` | 12 casos |
| `ISL_Service.Tests/ReglasFacturacionTests.cs` | 32 casos |
| `ISL_Service.Tests/VentasEscriturasReglasTests.cs` | 28 casos |
| `migracion/comparador/comparar-escrituras.py` | el banco de ESCRITURAS: ciclo de instantánea + foto de base |
| `migracion/comparador/escrituras/` | los 17 casos de escritura, su resultado y la verificación contra la fuente |
| `migracion/comparador/casos/ola4-*.json` | los 17 casos de lectura |

---

## Cómo se comprobó

### Las lecturas: el comparador de siempre

```bash
cd migracion/comparador
NUEVO_BASE=http://127.0.0.1:5081 MAC_SCRATCH=<scratch> python comparar.py casos/ola4-*.json
```

```
17 coinciden, 0 con diferencias, 17 casos
```

La fecha de operación se miró antes y después: **2026-10-06 las dos veces**, igual
que hoy, así que ningún `sp_n_ConsultaConstantes` disparó el cierre del día a
media tanda.

### Las escrituras: ciclo de instantánea y DOS cosas comparadas

El banco de escrituras es nuevo y está en `comparador/comparar-escrituras.py`. El
ciclo, por caso:

```
volver  ->  foto  ->  LEGACY  ->  foto  ->  delta
volver  ->  foto  ->  NUEVO   ->  foto  ->  delta
volver
```

y lo que se compara son **los dos deltas**, no las dos respuestas HTTP. La foto es
un `SELECT` por cada tabla que la operación toca, con `COUNT`, `SUM` de los
importes y los valores de las columnas que cambian.

```bash
cd migracion/comparador
python comparar-escrituras.py escrituras/casos-ola4.json escrituras/resultado-ola4.json
```

```
17 con el mismo efecto en base, 0 distintos, 17 casos
fecha de operacion al empezar:  2026-10-06
fecha de operacion al terminar: 2026-10-06
```

(17 casos para 18 capacidades: el de ajuste de inventario cubre el alta y la
cancelación por separado y hay un caso extra de catálogo.)

### La instantánea estaba ocupada, y cómo se resolvió

Al llegar al turno de las escrituras había un `instantanea-en-uso-5085` de otro
agente. Se esperó —no se tocó la instantánea de nadie— y en cuanto se liberó se
puso `instantanea-en-uso-5081`, se corrió la tanda entera y se borró el marcador
al terminar. No hizo falta el plan B de las transacciones con rollback.

### Dos cosas que el banco de escrituras hace y vale la pena saber

**1. Una foto que falla no es una foto, y revienta.** La primera versión del banco
daba 18 de 18 en verde, y era mentira: cuatro de las consultas de foto tenían un
nombre de columna equivocado (`IDStatus` donde la tabla dice `IDStatusPedido`,
`Observaciones` donde el procedimiento escribe en `ObservacionesPago`). El texto
del error de SQL Server es el MISMO antes y después, así que el delta salía vacío
en los dos lados y el caso parecía coincidir. Es la peor forma de equivocarse
aquí: da verde sin haber mirado nada. Ahora el banco lanza una excepción si la
foto trae un error, y así salieron los dos casos rojos que de verdad había.

**2. El fallo transitorio de conexión se reintenta.** Justo después de `volver`,
SQL Server acaba de salir de `SINGLE_USER` y la primera conexión del pool de
legacy puede fallar con `Login failed for user 'sa'`. Eso no es un resultado, es
la base reconectándose, y sin reintento pinta de rojo un caso que está bien. Se
reintenta **solo cuando no hubo operación**: reintentar una escritura que sí llegó
la haría dos veces.

---

## Lo que se encontró (y se corrigió)

### 1. Un bug propio: la fecha que el procedimiento completa él

El caso más importante de toda la ola, y salió del banco de escrituras.

`sp_n_ActualizaStatusVentasDescuentosAjustes` y
`sp_n_InsertarVentasDescuentosAjustes` hacen, literalmente:

```sql
-- Agregar la hora
select @Fecha = @Fecha + ' ' + SUBSTRING(CAST(CONVERT(TIME, GETDATE()) as varchar(20)), 1, 8)
```

Mi primera versión mandaba la fecha en `yyyy-MM-dd HH:mm:ss`, como todas las
demás de la ola. El procedimiento la dejaba en `2026-10-06 00:00:00 23:23:32`, que
no es una fecha — **y como corre con `SET ANSI_WARNINGS OFF` no revienta**:
guardaba `NULL` en la columna de fecha y contestaba `result = 1`. O sea que el
ajuste quedaba autorizado **sin fecha de autorización**, y la pantalla decía que
todo bien.

Comprobado ejecutando el procedimiento con las dos formas sobre el mismo renglón:

| `@Fecha` que se manda | `[Fecha Autorizacion]` que queda |
|---|---|
| `2026-10-06 00:00:00` | `NULL` |
| `10-06-2026` | `2026-10-06 23:23:32` |

Arreglado con `FiltrosLegacy.SoloFecha()`, que manda la fecha sola y deja que el
procedimiento ponga la hora. Se barrieron los 111 procedimientos de la ola
buscando ese patrón: son **cuatro** los que completan la fecha
(`sp_n_ActualizaStatusVentasDescuentosAjustes`,
`sp_n_InsertarVentasDescuentosAjustes`, `sp_n_InsertarPedidoDetalle` y
`sp_n_ProcesarCompra` — los dos últimos sobre otros campos). Está probado en
`FiltrosLegacyOla4Tests`.

Después del arreglo, el renglón que deja el backend nuevo es **idéntico** al de
legacy, fecha incluida.

### 2. El separador NO es la coma

Las listas de ids de los `sp_n_` van con **virgulilla**, no con coma. Se verificó
leyendo el cuerpo de los procedimientos en la base: todos declaran
`@Delimitador varchar(1)` y le asignan `'~'` antes de partir la cadena con
`fn_DevuelveCadenaEnTabla`, y los comentarios de sus cabeceras lo repiten
(`'15692~7546~7547'`).

Importa decirlo porque equivocarse **no da error**: una lista con comas llega como
un solo valor que no es número, el procedimiento no encuentra nada que procesar y
contesta como si hubiera trabajado.

### 3. El `IDUsuario` del banco de pruebas vale 0, y dos procedimientos no hacen nada con 0

`BancoPruebas` no está ligado a ningún usuario de `dbo.Usuarios`, así que su token
lleva `idUsuario = 0` y todo lo que firma se firma con 0. Para la mayoría de los
procedimientos eso solo deja un movimiento sin dueño, pero **dos no hacen nada**:

```sql
-- sp_n_ActualizaStatusVentasDescuentosAjustes
IF @IDUsuarioAutorizacion > 0  UPDATE ...
IF @IDUsuarioRechazo      > 0  UPDATE ...
```

y `sp_n_CancelarPedido`, que **no cambia `IDStatusPedido`**: lo único que deja
escrito es la firma, así que con 0 no deja nada. Los dos contestan `result = 1`.
Verificado ejecutándolos con 0 y con 3 sobre el mismo renglón
(`escrituras/contra-fuente-ola4.json`):

| procedimiento | con `@IDUsuario = 0` | con `@IDUsuario = 3` |
|---|---|---|
| `sp_n_ActualizaStatusVentasDescuentosAjustes` | `0 0 0` (nada) | `3` autorizado |
| `sp_n_CancelarPedido` | `1 0 <null>` (nada) | `1 3` firmado |

En esos dos, el backend nuevo ahora **rechaza la operación** con un mensaje que lo
explica, en vez de contestar que sí y no hacer nada. En los dos casos legacy
también rechaza el 0, pero en su controlador y con "Información incompleta."

**Esto es un pendiente del banco, no del código**: mientras `BancoPruebas` no esté
ligado a un usuario de Mac31, ninguna escritura que dependa del usuario se puede
comparar por HTTP. Ligarlo es un cambio en la base y no se hizo.

### 4. Lo que legacy tenía mal y no se heredó

Son defectos de MacServicios2, no diferencias de contrato:

1. **El DELETE del catálogo de tipos de casco ERA el PUT.** Mismo método, mismos
   doce parámetros, y la única diferencia es que el DELETE no valida el nombre. El
   "borrado" dependía de que el cliente se acordara de mandar el `IDStatus` de
   baja — y si mandaba el cuerpo incompleto, **sobrescribía el renglón con ceros**:
   precios en cero, bonificación en cero, peso en cero. Un casco que vale 0 se
   cobra a 0. Aquí el cambio de estatus es su propio endpoint, lee el renglón y lo
   reescribe igual.

2. **Una cancelación de CFDI rechazada por el cliente quedaba como aceptada.**
   Legacy mapea bien los cuatro estados del SAT y acto seguido los sobreescribe
   con `Accepted(2)`, con el comentario "el servicio no informa bien". Resultado:
   la factura sigue **válida ante el SAT** y el sistema la da por cancelada. El
   backend nuevo guarda el estado real.

3. **`sendemail` era el único endpoint del sistema que no validaba el token.** Su
   servicio no llama a `validateJwtTokenAsync`; lo único que lo protegía era el
   handler global, que solo comprueba que *exista* la cabecera `Authorization` sin
   mirar su contenido. Cualquiera podía mandar la factura de cualquier cliente al
   correo que quisiera.

4. **Un candado de máquina dentro del código de producción.** `sendemail` no manda
   el correo si `Environment.MachineName` es `DESKTOP-0RTIOR5`, y contesta OK
   igual. En esa máquina la pantalla dice "enviado" y no se envió nada. No se
   hereda: un servidor web no es la máquina de nadie.

5. **La consulta de facturas CANCELA.** Cuando se pide por `IDVenta` y la factura
   está vigente, legacy pregunta a Facturapi y si allá aparece cancelada llama a
   `sp_n_CancelarVentaFactura` **desde el endpoint de consulta**. Es exactamente lo
   que advierte `CUIDADO-consultas-que-escriben.md`. La nueva no escribe.

6. **La fecha inicial de los pagos de centro de servicio llevaba 23:59:59.** Está
   literal en `findCSAsync`: le pega `" 23:59:59"` a las DOS fechas, así que un
   filtro "del día 5 al día 5" empieza y termina a las 23:59:59 y deja fuera el día
   entero menos el último segundo.

7. **Dos ramas muertas más.** `if (FolioInicial != "0") ... else if (FolioInicial != "0")`
   en las consultas de cobranza y de cobros: la segunda condición repite la
   primera, así que "buscar solo por folio de venta" nunca corría.

8. **La validación por suma.** El controlador de cancelar un pago con transferencia
   comprueba `(IDPagoVenta + IDPagoVentaUsado) == 0`: con 5 y -5 la suma es cero y
   rechaza algo válido.

9. **Legacy revienta con un `Substring` en cinco endpoints** cuando no se le mandan
   las fechas, porque las formatea sin comprobar que vengan: garantías por aceptar,
   ajustes de inventario, folios de venta, pagos de liquidación y remisiones en
   tránsito. Contesta 400 con el stack completo. Uno de los casos del comparador
   (`ola4-garantias-aceptar-consultar`) tiene que mandarle las fechas a mano solo
   para poder compararlo.

10. **`sp_n_InsertarDescuentos` se llama sin parámetros** y crea el renglón en
    blanco; `sp_n_ActualizarDescuentos` recibe solo `@IDDescuento` y **el nombre
    del descuento nunca llega a la base**, aunque el controlador lo valide.

11. **Los `result`/`mensaje` de garantías se perdían.** En insertar, actualizar y
    registrar, el repositorio de legacy nunca llena esos campos (solo lo hace el de
    aceptar), así que la respuesta siempre era `{IDResult:"", result:0, mensaje:""}`
    y las filas del procedimiento se tiraban: la pantalla no se enteraba de un
    fallo con su explicación.

### 5. La lógica escondida en C# que había que encontrar

| dónde | qué |
|---|---|
| Todas las consultas | leen `formatoFechaDB` con `sp_n_ConsultaConstantes`, **que hace el cierre del día**. El backend nuevo manda las fechas en ISO y no lo llama: con eso estas capacidades vuelven a ser lecturas de verdad |
| Cobranza, concentrado, cobro | prefijo `"0" + folio` antes de llamar, para que una cadena vacía no reviente la conversión a `int`. En la consulta de cobros los folios de venta ya venían en `"0"` y el prefijo los dejaba en `"00"` |
| Pedidos, procesar en lote | `IDPedidoLst.Sort()` sobre una `List<string>`: ordena **como texto**, así que `["2","10","9"]` se queda `["10","2","9"]`. Y ese orden decide **qué folio de remisión le toca a cada pedido** |
| Pedidos, cancelar y procesar | el SP se ejecuta **N veces, con una conexión nueva por iteración y sin ninguna transacción que las envuelva**. Un fallo a mitad deja hechos los anteriores |
| Pedidos, los dos de lote | la respuesta trae **solo los que fallaron**: un lote perfecto contesta lista vacía y nadie sabe qué folio le tocó a cada pedido |
| Garantías | `@GarantiasInfo` lo armaba el **cliente de WinForms** recorriendo la rejilla, con un formato posicional de texto (`CLAVE: x  -  VALE: y` pegado con virgulilla). Cada cliente podía escribirlo distinto |
| Cobros de dinero por cascos | la bandera `esAplicacion` del **cuerpo** decidía entre guardar el reparto y APLICARLO a los saldos de los clientes |
| Pagos de venta | la bandera `NuevoEsquemaPagos` del **cuerpo** decidía contra qué tabla se escribía, y con cualquier valor que no fuera 0 ni 1 no se ejecutaba ningún procedimiento y se contestaba lista vacía |
| Cobranza pendiente y vencida | el servicio **pisa siete de los catorce parámetros** con constantes antes de llamar, sin importar lo que mande el cliente |
| Ajustes de descuento, consultar | **ignora los filtros del cuerpo**: los lee de un renglón de `dbo.Parametros` por su `ID` y por POSICIÓN (`Param2`..`Param12`). Quien adivine un `ID` ve los filtros y los datos de otro |
| `limpiarcs` | los dos parámetros van **hardcodeados en 0**: la operación siempre fue global y los campos del cuerpo nunca llegaron a la base |
| Facturación | el `@Params` de `sp_n_ActualizarParametro` son veinte columnas de texto con campos pegados **dentro** por `"@@@@"` (cuatro arrobas, aunque el comentario diga cinco), que el reporte vuelve a leer por posición |
| Facturación | la serie sale de comparar el key de Facturapi contra el nombre del ejecutable con una cadena de `else if`, y de la serie depende la **numeración fiscal** |
| Facturación | el candado antiproducción: `FacturapiSecretKey` guarda el secreto **con el nombre del ejecutable embebido**, y hay que quitárselo para obtener el secreto real |
| Facturación, sendemail | si no encuentra el PDF hace `Thread.Sleep(4000)` y vuelve a mirar: bloquea un hilo del servidor apostando a que otro proceso acabe |
| Listas de usados | doce listas paralelas que el procedimiento cruza **por posición**; nadie comprobaba que midieran lo mismo, y el síntoma de que no es un renglón con el precio de otro |

### 6. Las reglas que se agregaron, y por qué

Todas son de la forma "exigir lo que legacy dejaba pasar en cero". No es celo:
estos procedimientos, con un id en cero, **no fallan** — hacen algo distinto y
callado.

* **Las listas paralelas tienen que medir lo mismo.** Es la regla que más protege
  de toda la ola: el procedimiento cruza por posición, y una lista con un elemento
  menos guarda un renglón con el precio de otro.
* **Entrada y salida de un ajuste de inventario son excluyentes.** Legacy acepta
  las dos a la vez y el movimiento resultante depende de cómo las sume el
  procedimiento. En un ajuste de existencias eso es inventario que no cuadra con lo
  que hay en el piso.
* **Un concentrado sin remisiones no se crea** (salvo `SoloDevueltas = 1`): legacy
  valida la lista al crear una cobranza y se olvida al crear un concentrado, así
  que quedaba un folio gastado para nada.
* **El centro de servicio es obligatorio al aceptar una garantía**: el procedimiento
  arma con SQL dinámico un `UPDATE` contra la base de ESE centro, y con 0 no sabe a
  qué base ir.
* **Rechazar un ajuste de descuento exige motivo**: es lo único que le explica al
  vendedor por qué le dijeron que no.
* **Porcentaje y monto de una garantía son excluyentes**: el procedimiento recibe
  los dos y el resultado depende de su orden interno. Es dinero que se le reconoce
  al cliente.
* **Un porcentaje de descuento está entre 0 y 100**: con 110 no hay error y el
  importe del renglón queda negativo, que es un abono disfrazado de descuento.
* **El mismo producto no puede venir dos veces** en un inventario de pedidos: dos
  renglones con dos conteos del mismo producto dejan "cuántos hay" con dos
  respuestas.
* **Se exige al menos un filtro** en las consultas donde el procedimiento, con todo
  en cero, barre `Ventas` o `[Ventas Pagos]` completas.
* **El motivo 01 del SAT exige el folio sustituto**: sin él el SAT rechaza la
  cancelación, y el mensaje que llega no se relaciona con ese campo.

### 7. Las decisiones que se conservaron aunque parezcan equivocadas

* **El orden del lote de pedidos se ordena como texto**, igual que legacy. Ordenar
  numéricamente —que es lo correcto— haría que el mismo lote procesado desde el web
  y desde Mac31 asignara **folios distintos** a los mismos pedidos. Dos mitades del
  sistema que numeran distinto es peor que un orden raro. Queda anotado como algo
  que algún día conviene revisar, pero no aquí y no en silencio.
* **Los lotes no se envuelven en una transacción.** Cada `sp_n_` hace su propio
  `COMMIT`, así que una transacción de fuera no puede deshacer lo que ya se
  confirmó dentro: el "todo o nada" sería una ilusión. Y mantener los candados de
  `Ventas`, `Folios` y `AlmacenProducto` durante un lote de cincuenta pedidos
  bloquearía a quien esté facturando. Lo que sí se arregló es el **reporte**: ahora
  vuelve un renglón por pedido.
* **El formato de `@GarantiasInfo` se respeta al byte.** Dos espacios antes y
  después del guion incluidos. La bitácora vieja ya está escrita así y alguien la
  lee con los ojos: cambiar el formato partiría en dos la historia de la misma
  tabla.
* **El apóstrofo se sigue quitando** del texto libre. Aquí todo va parametrizado,
  así que no es defensa contra inyección: es fidelidad de datos. Si el web guardara
  el apóstrofo, el mismo renglón escrito desde el web y desde Mac31 no sería igual.
* **Los mensajes literales de legacy se conservan** (`KEY PARA FACTURAR NO
  COINCIDE.`, `LA FACTURA HA SIDO CANCELADA.`): son los que la gente de mostrador
  reconoce y busca en sus notas.
* **La cancelación del cobro entero de dinero por cascos no se resucita.** En el
  repositorio de legacy la elección entre los dos procedimientos está comentada y
  el nombre se fuerza siempre al de la aplicación: esa operación no está en servicio
  desde quién sabe cuándo. Una operación que nadie ha podido ejecutar en años no
  tiene cómo estar probada.

---

## Diferencias intencionales (las de fondo)

1. **Renglones crudos en vez de DTO tipado.** Legacy mete cada tabla en un DTO y
   `DataTableToList` empareja por nombre **exacto**, así que lo que el procedimiento
   devuelve y el DTO no declara se **tira**, y lo que el DTO declara y el
   procedimiento no devuelve sale en `0`/`0001-01-01` de relleno. El nuevo pasa el
   renglón del procedimiento.
2. **`null` en vez de `0` y de `0001-01-01`.** Donde el procedimiento dice NULL,
   Mac31 pinta la celda **en blanco** (su rejilla está ligada al `DataTable`), así
   que `null` es el valor fiel. Un `0` en `TotalPedido` se lee como "vale cero".
3. **La identidad sale del token, nunca del cuerpo.** En legacy viajan en el cuerpo
   `IDUsuario`, `Equipo`, `IDUsuarioCancelo`, `IDUsuarioAutorizacion`,
   `IDUsuarioRechazo`, `IDUsuarioProcesar`, `IDUsuarioActual` y `NombreEquipo`.
   Dos de ellos son especialmente graves: quien **autoriza un descuento** y quien
   **acepta una garantía** son justo los datos que después se auditan.
4. **Las banderas del cuerpo que elegían procedimiento son rutas.** `esAplicacion`
   y `NuevoEsquemaPagos`. Una bandera que decide si se mueve dinero o no es una
   bandera que se manda mal una vez y nadie entiende por qué.
5. **Las listas son listas.** El pegado con virgulilla vive en el repositorio, no
   en el contrato: el cliente ya no tiene que saber cuál es el separador.
6. **Los ids que legacy pegaba con `"|"` bajan como columnas.** Tres en cobranza y
   concentrado, cuatro saldos en la cancelación de un pago con transferencia. Un
   campo `"a|b|c"` obliga a que las dos mitades recuerden el orden.
7. **POST → GET** donde la consulta es por un id (`cobros/{id}/clientes`). En
   legacy eran POST porque su handler exigía el token dentro del JSON.
8. **El `IDStatus` del catálogo de tipos de casco no viaja en el cuerpo de
   actualizar.** Se conserva el que tenía; el estatus se mueve por su propio
   endpoint.
9. **La consulta de facturas no escribe**, y la de legacy sí.
10. **La facturación recibe los conceptos en el request.** En legacy el servicio
    arma el CFDI leyendo la remisión de la base: dos mil líneas que componen los
    conceptos, el IVA, los usados y las dos variantes de empresa. Eso no se puede
    verificar sin emitir comprobantes fiscales de verdad, así que el contrato es
    explícito. Lo que sí hace completo este backend es todo lo demás: el candado,
    la serie, el folio, el timbrado por HTTP, el renglón de parámetros y el insert.
11. **El cliente de Facturapi va por HTTP y no por SDK.** `Facturapi.dll` es de
    .NET Framework y no se puede referenciar desde `net8.0`; lo que hace es un
    `POST https://www.facturapi.io/v2/invoices` con autenticación Basic.
12. **El timbrado no se reintenta nunca.** Un reintento emitiría un segundo CFDI
    real ante el SAT. Si la llamada no contesta a tiempo, el mensaje dice que hay
    que **revisar en Facturapi antes de volver a intentarlo**.
13. **Cuando el CFDI se emite y el guardado en Mac31 falla, se dice.** No se puede
    evitar —no hay forma de meter una llamada HTTP dentro de una transacción de SQL
    Server— así que el mensaje nombra el UUID y el id de Facturapi, que es lo único
    con lo que alguien puede arreglarlo a mano. Legacy no registra nada.

---

## Permisos nuevos

**50 claves**, todas en `PermissionCatalogSeeds` (`PermissionService.cs`), en
**siete familias nuevas**: `usados.*` (11), `cobranza.*` (9), `garantias.*` (6),
`descuentos.*` (7), `inventario.*` (6), `facturacion.*` (4) y `cobros.*` (7).

### Por qué familias nuevas y no `ventas.*`

Es la trampa de la sección 2.8 del resumen del inventario. Para `ventas.*` manda
Mac31: al construir el snapshot se **borra** del usuario todo lo que empiece por
`ventas.` y no esté en `VentasWebPermissionSeeds`, y se recalcula desde los
botones de la forma CONSULTA DE VENTAS. Una clave nueva ahí se guarda en
`WUsuarioPermiso` y **desaparece en el siguiente cálculo**, sin error y sin
rastro.

Y hay una razón de fondo además de la mecánica: **Mac31 tiene un botón por
pantalla, y aquí hace falta más fino**. Dentro de la misma pantalla hay
operaciones que mueven dinero y otras que no: guardar el reparto de un cobro de
cascos no mueve nada, **aplicarlo** mueve los saldos de los clientes. Cerrar un
cobro cuadra el día; **reabrirlo** deshace ese cuadre. Solicitar un descuento y
**autorizarlo** no puede ser la misma persona. Dar todo eso junto porque Mac31 no
sabe distinguirlo sería heredar una limitación de la pantalla vieja.

### Dónde SÍ se reusan las claves de Mac31

Donde la clave existe y significa exactamente lo mismo, porque así hay **una sola
verdad** en vez de dos interruptores que hay que acordarse de encender juntos:

| endpoint nuevo | clave de Mac31 que se reusa | el botón |
|---|---|---|
| procesar pedidos (venta y compra) | `ventas.pedidos.procesar` | `btnProcesarPedido` |
| cancelar pedidos | `ventas.pedidos.cancelar` | `btnCancelarPedido` |
| ajustar cascos a crédito del pedido | `ventas.pedidos.modificar` | `btnModificarPedido` |
| las cabeceras de consulta de ventas | `ventas.consultar` | `btnBuscar` |
| consultas de pagos y detalle de la remisión | `ventas.pagos.ver` | `btnPagos` |
| ventas pendientes | `ventas.pendientes.ver` | es del WEB y está en la lista blanca |
| timbrar y consultar facturas | `ventas.facturar` (además de la nueva) | `btnFacturar` |
| todas las consultas de usados | `ventas.usados` (además de `usados.ver`) | `btnUsados` |

Las consultas de usados aceptan **las dos** claves a propósito: así quien hoy
puede abrir los usados de una remisión sigue pudiendo consultarlos el primer día,
sin que nadie tenga que acordarse de encender una casilla nueva.

### Dónde NO se reusó, y por qué

* **`garantias.*` no reusa `ventas.garantias`.** Esa clave existe, pero viene del
  botón `btnAjustePedido` de la forma CONSULTA DE VENTAS: es el *ajuste de un
  pedido* por garantía, que se hace desde la pantalla de ventas. Este módulo son
  las **tres pantallas de garantías** de Mac31, que son otra cosa y otra gente.
* **`facturacion.cancelar` y `facturacion.enviar` no reusan `ventas.facturar`.**
  Cancelar un CFDI ante el SAT y mandar una factura por correo son dos cosas
  distintas de timbrarla.
* **`cobranza.*` no reusa `ventas.consultar`.** La cobranza son TRES pantallas
  distintas de Mac31 y hay gente que cobra sin consultar ventas.
* **El catálogo de tipos de casco no cae en `catalogos.*`.** Sus renglones son
  **precios**: cuatro costos, una bonificación y un costo de excedente. `catalogos.ver`
  abre desplegables. Es la misma razón por la que la ola 1 separó
  `catalogos.galleros.ver`.

---

## La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Ajustes de inventario: crear | `POST /api/ajusteinventario` | `POST /api/inventario/ajustes` | `inventario.ajustes.crear` | **OK** | ESCRITURA comparada: mismo renglon y MISMA existencia resultante (12 -> 32). El nuevo prohibe entrada Y salida a la vez |
| Ajustes de inventario: cancelar | `PUT /api/ajusteinventario` | `POST /api/inventario/ajustes/cancelar` | `inventario.ajustes.cancelar` | **OK** | ESCRITURA comparada: misma marca de cancelacion y misma existencia devuelta |
| Catalogo de tipos de casco: cambiar estatus (su DELETE) | `DELETE /api/cattipousado` | `POST /api/inventario/tipos-usado/status` | `usados.tipos.editar` | **OK** | En legacy el DELETE ERA el PUT: mismos 12 parametros. Con un cuerpo incompleto dejaba el tipo con todos los precios en CERO. Aqui es un cambio de estatus que lee el renglon y lo reescribe igual |
| Catalogo de tipos de casco: crear | `POST /api/cattipousado` | `POST /api/inventario/tipos-usado` | `usados.tipos.editar` | **OK** | ESCRITURA comparada en instantanea: mismo renglon nuevo en el catalogo |
| Catalogo de tipos de casco: modificar | `PUT /api/cattipousado` | `PUT /api/inventario/tipos-usado` | `usados.tipos.editar` | **OK** | ESCRITURA comparada en instantanea: misma fila despues. El nuevo CONSERVA el IDStatus |
| Cobranza pendiente y vencida: consultar | `POST /api/cobranzapendientesvencidos/consultar` | `POST /api/ventas/cobranza/pendientes-vencidos/consultar` | `cobranza.ver` | no verificable | Legacy PISA siete de los catorce parametros con constantes y exige FechaOperacionFtm; el nuevo no ofrece los siete que el servidor ignora, asi que los cuerpos no son equiparables. Los siete valores estan escritos con su significado en el repositorio |
| Ajustes de inventario: consultar | `POST /api/consultaajustesinventario/consultar` | `POST /api/inventario/ajustes/consultar` | `inventario.ajustes.ver` | no verificable | Legacy formatea las fechas SIN comprobar que vengan y revienta con un error de Substring; el nuevo las exige. Los cuerpos no son equiparables |
| Cobranza: consultar | `POST /api/cobranzas/consultar` | `POST /api/ventas/cobranza/cobranzas/consultar` | `cobranza.ver` | **OK** | comparador: ola4-cobranza-consultar |
| Cobros: consultar | `POST /api/cobros/consultar` | `POST /api/ventas/cobranza/cobros/consultar` | `cobranza.ver` | **OK** | comparador: ola4-cobros-consultar. Coinciden en el vacio |
| Cobros: clientes que pagaron | `POST /api/cobrosclientes/consultar` | `GET /api/ventas/cobranza/cobros/{id}/clientes` | `cobranza.ver` | **OK** | comparador: ola4-cobro-clientes |
| Consulta de ventas: detalle de pedidos | `POST /api/consultaventas/pedidos/consultar` | `POST /api/ventas/consultas/pedidos-detalle` | `ventas.consultar` | no verificable | La cascada de filtros de legacy TIRA el IDsClientes que acaba de calcular tres lineas antes; el nuevo lo conserva, asi que con el mismo cuerpo preguntan cosas distintas |
| Consulta de ventas: remisiones en transito | `POST /api/consultaventas/consultar/transito` | `POST /api/ventas/consultas/transito` | `ventas.consultar` | **OK** | comparador: ola4-ventas-transito-consultar |
| Ajustes de descuento: consultar | `POST /api/consultaventasdescuentosajustes/consultar` | `POST /api/ventas/descuentos/ajustes/consultar` | `descuentos.ajustes.ver` | no verificable | Legacy IGNORA los filtros del cuerpo: los lee de un renglon de dbo.Parametros por su ID y por POSICION (Param2..Param12). El nuevo los recibe con nombre. Verificado contra la fuente con sqlcmd |
| Garantias: consultar | `POST /api/garantias/consultar` | `POST /api/ventas/garantias/consultar` | `garantias.ver` | **OK** | comparador: ola4-garantias-consultar |
| Garantias: consultar las que faltan por aceptar | `POST /api/garantias/aceptar/consultar` | `POST /api/ventas/garantias/aceptar/consultar` | `garantias.ver` | **OK** | comparador: ola4-garantias-aceptar-consultar. Legacy REVIENTA aqui si no se le mandan las fechas |
| Garantias: consultar registros previos | `POST /api/garantias/registrar/consultar` | `POST /api/ventas/garantias/registros/consultar` | `garantias.ver` | **OK** | comparador: ola4-garantias-registro-consultar. Coinciden en el vacio |
| Pagos de venta: consultar | `POST /api/consultaventaspagos/consultar` | `POST /api/ventas/consultas/pagos + /pagos/nuevos` | `ventas.pagos.ver` | **OK** | comparador: ola4-ventas-pagos-consultar. La bandera NuevoEsquemaPagos del cuerpo se partio en DOS rutas: con cualquier valor que no fuera 0 ni 1, legacy no ejecutaba NINGUN procedimiento y contestaba lista vacia |
| Pagos de venta: los de centro de servicio | `POST /api/consultaventaspagos/centroservicio/consultar` | `POST /api/ventas/consultas/pagos/centro-servicio` | `ventas.pagos.ver` | no verificable | No comparable: legacy le pega ' 23:59:59' a la fecha INICIAL (esta literal en findCSAsync), asi que un rango de un dia deja fuera el dia. El nuevo le pone 00:00:00 |
| Pagos de venta: los de una liquidacion | `POST /api/consultaventaspagos/liquidacion/consultar` | `POST /api/ventas/consultas/pagos/liquidacion` | `ventas.pagos.ver` | no verificable | No hay liquidaciones en Tauro; el SP devuelve vacio para cualquier fecha |
| Ventas pendientes: consultar | `POST /api/consultaventaspendiente/consultar` | `POST /api/ventas/consultas/pendientes` | `ventas.pendientes.ver` | no verificable | LOS DOS LADOS FALLAN IGUAL: sp_n_ConsultaVentasPendiente referencia la tabla 'Ventas Pendiente', que NO EXISTE en Produccion_svr. Mismo mensaje de SQL Server en los dos |
| Descuentos (detalle): eliminar | `DELETE /api/descuentosdetalle` | `POST /api/ventas/descuentos/detalle/eliminar` | `descuentos.eliminar` | **OK** | ESCRITURA comparada: mismo renglon borrado y mismo total |
| Descuentos (detalle): crear | `POST /api/descuentosdetalle` | `POST /api/ventas/descuentos/detalle/alta` | `descuentos.editar` | **OK** | ESCRITURA comparada: mismo delta en Detalle Descuentos y su Log. **La ruta se movió a `/detalle/alta` en la ola 9**: en `/detalle` chocaba con la consulta de la ola 2 y ASP.NET contestaba 500 a las dos. Ver `ola-9-cierre-parciales.md`. |
| Descuentos (detalle): modificar | `PUT /api/descuentosdetalle` | `PUT /api/ventas/descuentos/detalle` | `descuentos.editar` | no verificable | El renglon recien creado no sobrevive al `volver`; firma verificada contra sys.parameters |
| Descuentos: eliminar | `DELETE /api/descuentos` | `POST /api/ventas/descuentos/eliminar` | `descuentos.eliminar` | no verificable | sp_n_EliminarDescuentos NO EXISTE en Produccion_svr. Legacy falla igual |
| Descuentos: consultar | `POST /api/descuentos/consultar` | `POST /api/ventas/descuentos/consultar` | `descuentos.ver` | no verificable | sp_n_ConsultaDescuentos NO EXISTE en Produccion_svr. Legacy falla igual |
| Descuentos: crear | `POST /api/descuentos` | `POST /api/ventas/descuentos` | `descuentos.editar` | no verificable | sp_n_InsertarDescuentos recibe CERO parametros: crea el renglon en blanco. El nombre que manda el cliente no llega a la base, y la respuesta lo dice |
| Descuentos: simular (ESCRIBE) | `POST /api/descuentos/simular` | `POST /api/ventas/descuentos/simular` | `descuentos.simular` | no verificable | ESCRIBE: hace INSERT INTO Descuentos cuando no encuentra el renglon de simulacion del usuario y el equipo. El equipo del nuevo sale del token, asi que el renglon no es el mismo que el de legacy |
| Descuentos: modificar | `PUT /api/descuentos` | `PUT /api/ventas/descuentos` | `descuentos.editar` | no verificable | sp_n_ActualizarDescuentos NO EXISTE en Produccion_svr. Legacy falla igual |
| Inventario de pedidos: consultar | `POST /api/inventariopedidos/consultar` | `POST /api/inventario/pedidos/consultar` | `inventario.pedidos.ver` | **OK** | comparador: ola4-inventario-pedidos-consultar |
| Inventario de pedidos: cancelar | `POST /api/inventariopedidos/cancelar` | `POST /api/inventario/pedidos/cancelar` | `inventario.pedidos.cancelar` | **OK** | ESCRITURA comparada: mismo status y misma marca de cancelacion |
| Inventario de pedidos: crear | `POST /api/inventariopedidos` | `POST /api/inventario/pedidos` | `inventario.pedidos.crear` | **OK** | ESCRITURA comparada: mismo delta en cabecera y detalle. Seis listas paralelas -> una lista de renglones |
| Pedidos (detalle): quitar renglon | `DELETE /api/pedidodetalles` | `DELETE /api/ventas/pedidos/captura/detalle (ya existia)` | `ventas.pedidos.crear` | no verificable | Ya estaba migrado. Revisado: @IDPedidoDetalle, @Observaciones, @SinModificarObservaciones |
| Pedidos (detalle): alta | `POST /api/pedidodetalles/alta` | `POST /api/ventas/pedidos/captura/detalle (ya existia)` | `ventas.pedidos.crear` | no verificable | Ya estaba migrado (VentasPedidoCapturaRepository, con fallback sp_w_/sp_n_). Revisado: mismos 50 parametros y mismo orden |
| Pedidos (cascos a credito): ajustar cantidad | `PUT /api/pedidousadoscredito` | `POST /api/ventas/pedidos/usados-credito` | `ventas.pedidos.modificar` | no verificable | Necesita un pedido abierto con cascos a credito; no hay en Tauro |
| Pedidos: borrar el borrador del usuario | `DELETE /api/pedidos` | `DELETE /api/ventas/pedidos/captura/borrador (ya existia)` | `ventas.pedidos.crear` | no verificable | Ya estaba migrado. OJO con este: sp_n_EliminarPedido recibe SOLO @IDUsuario y borra TODO el borrador de ese usuario |
| Pedidos: procesar como venta (en lote) | `POST /api/pedidos/ventas/procesar` | `POST /api/ventas/pedidos/ventas/procesar` | `ventas.pedidos.procesar` | no verificable | Consume folios de remision y mueve inventario: no se prueba contra un folio real sin dejar hueco. El ORDEN del lote se replico tal cual (ordinal por texto, probado en PedidosOrdenDeLoteTests) porque de el depende que folio le toca a cada pedido |
| Pedidos: procesar como compra | `POST /api/pedidos/compras/procesar` | `POST /api/ventas/pedidos/compras/procesar` | `ventas.pedidos.procesar` | no verificable | No hay pedidos de compra abiertos en Tauro |
| Pedidos: cancelar (en lote) | `PUT /api/pedidos/cancelar` | `POST /api/ventas/pedidos/cancelar` | `ventas.pedidos.cancelar` | no verificable | Verificado CONTRA LA FUENTE: el SP NO cambia IDStatusPedido; lo unico que deja escrito es la firma, y con IDUsuario 0 no deja nada. Legacy rechaza el 0. El nuevo ahora lo rechaza con un mensaje claro |
| Pedidos: modificar | `PUT /api/pedidos` | `PUT /api/ventas/pedidos/captura/{id} (ya existia)` | `ventas.pedidos.modificar` | no verificable | Ya estaba migrado. Revisado: los 15 parametros de sp_n_ActualizarPedido |
| Movimientos de usados: consultar | `POST /api/usadosmovimientos/consultar` | `POST /api/ventas/usados/movimientos/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-movimientos-consultar |
| Movimientos de usados: consultar los pagados | `POST /api/usadosmovimientos/pagados/consultar` | `POST /api/ventas/usados/movimientos/pagados/consultar` | `usados.ver / ventas.usados` | no verificable | Necesita un corte; no hay cortes de usados en Tauro |
| Movimientos de usados: cancelar | `POST /api/usadosmovimientos/cancelar` | `POST /api/ventas/usados/movimientos/cancelar` | `usados.movimientos.cancelar` | no verificable | El movimiento recien creado no sobrevive al `volver`; firma del SP verificada contra sys.parameters |
| Movimientos de usados: crear | `POST /api/usadosmovimientos` | `POST /api/ventas/usados/movimientos` | `usados.movimientos.editar` | **OK** | ESCRITURA comparada en instantanea: mismo delta en UsadosMovimientos y su detalle |
| Cobranza: modificar el detalle | `POST /api/cobranzasdetalle` | `POST /api/ventas/cobranza/cobranzas/actualizar` | `cobranza.cobranzas.editar` | no verificable | La cobranza recien creada no sobrevive al `volver`; firma verificada contra sys.parameters |
| Cobranza: cancelar | `POST /api/cobranzas/cancelar` | `POST /api/ventas/cobranza/cobranzas/cancelar` | `cobranza.cobranzas.cancelar` | **OK** | ESCRITURA comparada: misma marca de cancelacion |
| Cobranza: crear | `POST /api/cobranzas` | `POST /api/ventas/cobranza/cobranzas` | `cobranza.cobranzas.editar` | **OK** | ESCRITURA comparada: mismo delta en Cobranza y CobranzaDetalle |
| Cobros: reabrir | `POST /api/cobros/abrir` | `POST /api/ventas/cobranza/cobros/abrir` | `cobranza.cobros.abrir` | no verificable | Cobros esta VACIA en Tauro |
| Cobros: modificar | `POST /api/cobros/actualizar` | `POST /api/ventas/cobranza/cobros/actualizar` | `cobranza.cobros.editar` | no verificable | Idem. Este SP no recibe firma: no la declara |
| Cobros: cancelar | `POST /api/cobros/cancelar` | `POST /api/ventas/cobranza/cobros/cancelar` | `cobranza.cobros.cancelar` | no verificable | Idem |
| Cobros: cerrar | `POST /api/cobros/cerrar` | `POST /api/ventas/cobranza/cobros/cerrar` | `cobranza.cobros.cerrar` | no verificable | Idem |
| Cobros de dinero por cascos: consultar | `POST /api/ventascobrosdinerousados/consultar` | `POST /api/ventas/usados/cobros-dinero/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-cobros-dinero-consultar. Coinciden en el vacio |
| Cobros de dinero por cascos: consultar cheques | `POST /api/ventascobrosdinerousados/cheques/consultar` | `POST /api/ventas/usados/cobros-dinero/cheques/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-cheques-consultar |
| Cobros de dinero por cascos: consultar cortes de centro de servicio | `POST /api/ventascobrosdinerousados/cortescs/consultar` | `POST /api/ventas/usados/cobros-dinero/cortes-cs/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-cortes-cs-consultar. El nuevo manda ademas @IDsCobro, que el SP declara y legacy no mandaba |
| Cobros de dinero por cascos: modificar | `POST /api/ventascobrosdinerousados/actualizar` | `POST /api/ventas/usados/cobros-dinero/reparto` | `usados.cobros_dinero.editar` | no verificable | La bandera `esAplicacion` del cuerpo se partio en DOS rutas. Tabla vacia en Tauro |
| Cobros de dinero por cascos: modificar un cheque | `POST /api/ventascobrosdinerousados/cheques/actualizar` | `POST /api/ventas/usados/cobros-dinero/cheques/actualizar` | `usados.cobros_dinero.editar` | no verificable | Tabla vacia. El nuevo exige el id del cheque, que legacy no validaba |
| Cobros de dinero por cascos: cancelar | `POST /api/ventascobrosdinerousados/cancelar` | `POST /api/ventas/usados/cobros-dinero/cancelar` | `usados.cobros_dinero.cancelar` | no verificable | Tabla vacia en Tauro |
| Cobros de dinero por cascos: desligar un corte de centro de servicio | `POST /api/ventascobrosdinerousados/cortescs/eliminar` | `POST /api/ventas/usados/cobros-dinero/cortes-cs/eliminar` | `usados.cobros_dinero.editar` | no verificable | Idem. Es un DELETE sin ningun campo obligatorio en legacy |
| Cobros de dinero por cascos: crear | `POST /api/ventascobrosdinerousados` | `POST /api/ventas/usados/cobros-dinero` | `usados.cobros_dinero.editar` | no verificable | CobrosDineroUsado esta VACIA en Tauro: no hay cobro contra el que registrar. Firma del SP verificada contra sys.parameters; listas con virgulilla |
| Cobros de dinero por cascos: cheques | `POST /api/ventascobrosdinerousados/cheques` | `POST /api/ventas/usados/cobros-dinero/cheques` | `usados.cobros_dinero.editar` | no verificable | Tabla vacia en Tauro. El nuevo EXIGE la fecha del cheque; legacy reventaba sin ella |
| Cobros de dinero por cascos: cortes de centro de servicio | `POST /api/ventascobrosdinerousados/cortescs` | `POST /api/ventas/usados/cobros-dinero/cortes-cs` | `usados.cobros_dinero.editar` | no verificable | Tabla vacia. El nuevo exige centro y corte, que legacy no validaba |
| Ingresos de cascos usados: consultar | `POST /api/ventascobrosusados/consultar` | `POST /api/ventas/usados/cobros/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-cobros-consultar |
| Ingresos de cascos usados: cancelar | `POST /api/ventascobrosusados/cancelar` | `POST /api/ventas/usados/cobros/cancelar` | `usados.cobros.cancelar` | no verificable | Idem |
| Ingresos de cascos usados: crear | `POST /api/ventascobrosusados` | `POST /api/ventas/usados/cobros` | `usados.cobros.editar` | no verificable | VentasCobrosUsados esta VACIA en Tauro (hace falta un cobro) |
| Concentrado: modificar el detalle | `POST /api/concentradosdetalle` | `POST /api/ventas/cobranza/concentrados/actualizar` | `cobranza.concentrados.editar` | no verificable | Idem |
| Concentrado: crear | `POST /api/concentrados` | `POST /api/ventas/cobranza/concentrados` | `cobranza.concentrados.editar` | no verificable | No hay remisiones devueltas sin concentrar en Tauro; firma verificada contra sys.parameters |
| Concentrado: cancelar | `POST /api/concentrados/cancelar` | `POST /api/ventas/cobranza/concentrados/cancelar` | `cobranza.concentrados.cancelar` | no verificable | Idem |
| Ajustes de descuento: autorizar o rechazar | `POST /api/ventasdescuentosajustes/status` | `POST /api/ventas/descuentos/ajustes/status` | `descuentos.ajustes.autorizar` | no verificable | Verificado CONTRA LA FUENTE: el SP solo escribe con IDUsuario > 0, y el usuario del banco vale 0. Legacy rechaza el 0 en el controlador, asi que no hay cuerpo comun. El nuevo ahora lo rechaza con un mensaje claro |
| Facturacion: consultar | `POST /api/ventasfacturas/consultar` | `POST /api/ventas/facturas/consultar` | `facturacion.ver / ventas.facturar` | **OK** | comparador: ola4-facturas-consultar. Y una diferencia de fondo: la consulta de legacy ESCRIBE (llama a sp_n_CancelarVentaFactura); la nueva no |
| Facturacion: cancelar | `POST /api/ventasfacturas/cancelar` | `POST /api/ventas/facturas/cancelar` | `facturacion.cancelar` | no verificable | Cancela ante el SAT de verdad. El nuevo guarda el estatus REAL que contesto el SAT; legacy lo sobreescribe siempre con 'aceptada' |
| Facturacion: enviar por correo | `POST /api/ventasfacturas/sendemail` | `POST /api/ventas/facturas/enviar` | `facturacion.enviar` | no verificable | MANDA UN CORREO DE VERDAD con el PDF y el XML. En legacy era el UNICO endpoint del sistema que NO validaba el token |
| Facturacion: timbrar complemento de pago | `POST /api/ventasfacturas/complementopago` | `POST /api/ventas/facturas/complemento-pago` | `facturacion.timbrar / ventas.facturar` | no verificable | Idem: timbra de verdad |
| Facturacion: timbrar | `POST /api/ventasfacturas` | `POST /api/ventas/facturas` | `facturacion.timbrar / ventas.facturar` | no verificable | TIMBRA UN CFDI DE VERDAD ante el SAT: no se puede comparar dentro del ciclo de instantanea (serian DOS comprobantes fiscales reales y volver la base no los deshace). Lo que SI se probo sin base: el candado del key, la serie, el folio, las claves del SAT, el @Params posicional y el QR |
| Ventas: cancelar una remision | `POST /api/ventascancelacion` | `POST /api/ventas/cancelacion (ya existia)` | `ventas.cancelar` | no verificable | Ya estaba migrado. Revisado: los tres parametros coinciden, y la bandera NuevoEsquemaPagos de legacy esta COMENTADA (sp_n_CancelarNuevaVenta nunca se llama) |
| Ajustes de descuento: crear | `POST /api/ventasdescuentosajustes` | `POST /api/ventas/descuentos/ajustes` | `descuentos.ajustes.crear` | **OK** | ESCRITURA comparada: renglon IDENTICO, incluida la fecha. AQUI SALIO EL BUG DE LA FECHA (ver la seccion de hallazgos) |
| Ventas: devolucion | `POST /api/ventasdevolucion` | `POST /api/ventas/devolucion (ya existia)` | `ventas.devolucion` | no verificable | Ya estaba migrado. Revisado: sp_n_DevolucionVentaLuro NO EXISTE en Produccion_svr, asi que la rama EsLuro de legacy no se puede ejecutar aqui |
| Pagos de venta: capturar un abono | `POST /api/ventaspagos` | `POST /api/ventas/multipago (ya existia) + POST /api/ventas/pagos/nuevos` | `ventas.pagos.multi / cobros.pagos.editar` | no verificable | El esquema viejo ya estaba migrado (VentasMultiPago, una llamada por remision). Lo que FALTABA era la rama NuevoEsquemaPagos = 1 (sp_n_InsertarVentasNuevosPagos), y es la que se agrega: ahora es su propia ruta en vez de una bandera del cuerpo |
| Pagos de venta: declarar incobrable | `POST /api/ventaspagos/incobrable` | `POST /api/ventas/pagos/incobrable` | `cobros.pagos.incobrable` | **OK** | ESCRITURA comparada con una remision CON saldo: mismo pago insertado y mismos SaldoIncobrable/Abonos/Fecha Pago |
| Pagos de venta: abono en kilos | `POST /api/ventaspagos/kilos` | `POST /api/ventas/pagos/kilos` | `cobros.pagos.kilos` | no verificable | Necesita un cobro, y Cobros esta vacia en Tauro |
| Ventas: reasignar cliente o agente | `POST /api/ventasreasignacion` | `POST /api/ventas/reasignar` | `cobros.reasignar` | **OK** | ESCRITURA comparada: mismo cambio de agente y mismo renglon de bitacora |
| Pagos de venta: limpiar los de centro de servicio | `POST /api/ventaspagos/limpiarcs` | `POST /api/ventas/pagos/centro-servicio/limpiar` | `cobros.pagos.editar` | no verificable | Legacy manda los dos parametros HARDCODEADOS en 0: la operacion siempre fue GLOBAL y los campos del cuerpo nunca llegaron a la base. El nuevo no acepta ids, por eso |
| Ajustes de descuento: simular | `POST /api/ventasdescuentosajustes/simular` | `POST /api/ventas/descuentos/ajustes/simular` | `descuentos.ajustes.crear` | no verificable | sp_n_SimulaVentasDescuentosAjustes necesita un ajuste pendiente con detalle; el que hay no tiene IDVentasDetalle |
| Ventas: cambiar la nota de la remision | `POST /api/ventas/actualizar` | `POST /api/ventas/actualizar` | `cobros.pagos.editar` | **OK** | ESCRITURA comparada: misma ObservacionesPago. OJO: el SP escribe en ObservacionesPago, NO en Observaciones |
| Detalle de la remision: consultar | `POST /api/ventasdetalles/consultar` | `POST /api/ventas/detalles/consultar` | `ventas.pagos.ver / cobros.pagos.editar` | **OK** | comparador: ola4-ventas-detalles-consultar. El nuevo lee EsLuro con un SELECT y NO con sp_n_ConsultaConstantes |
| Cascos a credito: ajustar | `POST /api/ventasusadoscreditos/ajustar` | `POST /api/ventas/usados/credito/ajustar (ya existia)` | `ventas.usados` | no verificable | Ya estaba migrado, con la ramificacion por empresa (Zara vs no Zara) leyendo Constantes.EsquemaPago. Revisado: los 13 parametros coinciden |
| Cascos a credito: marcar pago por kilo | `POST /api/ventasusadoscreditos/kilos` | `POST /api/ventas/usados-credito/kilos` | `cobros.pagos.kilos` | no verificable | Necesita remisiones marcadas para pago por kilo; no hay en Tauro |
| Pagos de venta: aplicar un pago del esquema nuevo | `PUT /api/ventaspagos/aplicar` | `POST /api/ventas/pagos/nuevos/aplicar` | `cobros.pagos.aplicar` | no verificable | [Ventas Nuevos Pagos] esta vacia en Tauro |
| Pagos de venta: cancelar | `PUT /api/ventaspagos/cancelar` | `POST /api/ventas/pagos/cancelar (ya existia) + /api/ventas/pagos/nuevos/cancelar` | `ventas.pagos.ver / cobros.pagos.cancelar` | no verificable | El esquema viejo ya estaba migrado. Lo que falta y se agrega es sp_n_CancelarVentasNuevosPagos, en su propia ruta |
| Pagos de venta: cancelar un abono en kilos | `PUT /api/ventaspagos/kilos/cancelar` | `POST /api/ventas/pagos/kilos/cancelar` | `cobros.pagos.kilos` | no verificable | sp_n_CancelarVentasPagosKilos NO EXISTE en Produccion_svr. Se respeta la firma que usa legacy, incluida su rareza: NO manda IDVentaPagoKilo, que es el unico campo que su controlador valida |
| Pagos de venta: cancelar una transferencia bancaria | `PUT /api/ventaspagos/cancelar/transferencia` | `POST /api/ventas/pagos/transferencias/cancelar` | `cobros.transferencias.cancelar` | no verificable | No hay transferencias bancarias en Tauro. Es la escritura MAS LARGA de la ola: el SP toca cinco tablas y la bitacora |
| Pagos de venta: cancelar un pago con transferencia | `PUT /api/ventaspagos/cancelar/transferencia/pago` | `POST /api/ventas/pagos/transferencias/pago/cancelar` | `cobros.transferencias.cancelar` | no verificable | Idem. Los cuatro saldos a favor bajan como columnas, no pegados con pipe como en legacy |
| Folios de venta: consultar | `POST /api/ventasfolios/consultar` | `POST /api/ventas/consultas/folios` | `cobranza.ver / ventas.consultar` | no verificable | Legacy formatea las dos fechas SIN comprobar que vengan y revienta si faltan. El nuevo manda ademas @Presentacion y @IDsUsuario, que el SP declara y legacy no mandaba |
| Garantias: aceptar | `POST /api/garantias/aceptar` | `POST /api/ventas/garantias/aceptar` | `garantias.aceptar` | no verificable | El SP escribe en la base de OTRA empresa (UPDATE dinamico contra <BD_CS>Garantias) y no hay centro de servicio dado de alta en Tauro |
| Garantias: modificar | `POST /api/garantias/actualizar` | `POST /api/ventas/garantias/actualizar` | `garantias.editar` | no verificable | Es la via por la que Mac31 cancela de verdad. No comparable por HTTP: @GarantiasInfo lo armaba el CLIENTE de WinForms y ahora lo arma el servidor (mismo formato, probado en GarantiasBitacoraTests) |
| Garantias: cancelar | `POST /api/garantias/cancelar` | `POST /api/ventas/garantias/cancelar` | `garantias.cancelar` | no verificable | NINGUNA pantalla de Mac31 lo llama (su UrlCancelar nunca se usa). Firma verificada contra sys.parameters |
| Garantias: cancelar un registro previo | `POST /api/garantias/registrar/cancelar` | `POST /api/ventas/garantias/registros/cancelar` | `garantias.registro.cancelar` | no verificable | El registro recien creado no sobrevive al `volver` |
| Garantias: crear | `POST /api/garantias` | `POST /api/ventas/garantias` | `garantias.editar` | **OK** | ESCRITURA comparada: mismo renglon nuevo en Garantias |
| Garantias: registro previo | `POST /api/garantias/registrar` | `POST /api/ventas/garantias/registros` | `garantias.registro.editar` | **OK** | ESCRITURA comparada: mismo renglon nuevo en GarantiasRegistro |
| Excedentes de cascos: consultar | `POST /api/ventasusadosexcedentes/consultar` | `POST /api/ventas/usados/excedentes/consultar` | `usados.ver / ventas.usados` | **OK** | comparador: ola4-usados-excedentes-consultar |
| Excedentes de cascos: cancelar | `POST /api/ventasusadosexcedentes/cancelar` | `POST /api/ventas/usados/excedentes/cancelar` | `usados.excedentes.cancelar` | no verificable | Idem |
| Excedentes de cascos: crear | `POST /api/ventasusadosexcedentes` | `POST /api/ventas/usados/excedentes` | `usados.excedentes.editar` | no verificable | Hace falta un cobro, y Cobros esta vacia en Tauro |
| Cobros de dinero por cascos: APLICAR | `POST /api/ventascobrosdinerousados/actualizar` (esAplicacion = 1) | `POST /api/ventas/usados/cobros-dinero/aplicar` | `usados.cobros_dinero.aplicar` | no verificable | La mitad que MUEVE los saldos de los clientes, con permiso propio. Tabla vacia en Tauro |
---

## Qué quedó no verificable, y por qué

Las 66 no verificables son cinco motivos, no sesenta y seis:

| motivo | cuántas | se arregla con |
|---|---|---|
| **Tauro no usa esa pantalla**: `Cobros`, `CobrosDineroUsado`, `VentasCobrosUsados`, los cortes de centro de servicio, las transferencias bancarias y `[Ventas Nuevos Pagos]` están **vacías** | 29 | correrlo contra **Zaragoza (`MacZ`)**, que sí las usa. Es el mismo pendiente que ya dejó la ola 2 |
| **El procedimiento no existe en esta base**: `sp_n_ActualizarDescuentos`, `sp_n_EliminarDescuentos`, `sp_n_ConsultaDescuentos`, `sp_n_CancelarVentasPagosKilos`, `sp_n_DevolucionVentaLuro`, y la tabla `Ventas Pendiente` que usa `sp_n_ConsultaVentasPendiente`. **Legacy falla igual** | 7 | una base que los tenga. En los seis, los dos lados dan el mismo error de SQL Server |
| **Los cuerpos no son equiparables** porque el nuevo corrige un defecto de legacy: fechas que legacy no comprueba y hacen reventar un `Substring`, filtros que legacy lee de `dbo.Parametros` por posición, parámetros que legacy pisa con constantes, el `IDUsuario 0` | 14 | verificado contra la **fuente** con `sqlcmd` donde se pudo (ver `escrituras/contra-fuente-ola4.json`) |
| **El dato de prueba no sobrevive al `volver`**: la cobranza, el movimiento o el renglón que acabo de crear desaparece al restaurar la instantánea, así que el "modificar" y el "cancelar" de eso no se pueden encadenar | 9 | un caso de dos pasos dentro de la misma ventana de instantánea, o datos sembrados antes de crearla |
| **La operación es irreversible de verdad**: timbra un CFDI ante el SAT, lo cancela, manda un correo, o consume un folio de remisión | 7 | nada: no se puede. Lo que SÍ se probó sin base son el candado del key, la serie, el folio, las claves del SAT, el `@Params` y el QR (32 casos en `ReglasFacturacionTests`) |

De los **8 ya migrados** que entraban en el lote como `por-verificar` (captura de
pedidos, cancelación, devolución, multipago, ajuste de cascos a crédito), se
revisó parámetro por parámetro contra `sys.parameters` y contra el repositorio de
legacy, y coinciden. Dos notas de eso: `sp_n_EliminarPedido` recibe **solo**
`@IDUsuario` y borra TODO el borrador de ese usuario, y
`sp_n_DevolucionVentaLuro` **no existe** en esta base, así que la rama `EsLuro` de
legacy no se puede ejecutar aquí.

## Pendientes que quedan anotados

* **`registro.tsv` no se tocó.** Varios agentes escriben ese archivo al mismo
  tiempo y reescribirlo entero podría pisar sus filas. Las 100 filas hay que
  pasarlas con `ubicacion_nueva` = la ruta nueva de la tabla, `metodo_validacion`
  = `comparador` (35) o `revision-estatica` (66), y `resultado` = `OK` o
  `no verificable`.
* **Ligar `BancoPruebas` a un usuario de Mac31.** Es el pendiente más caro del
  banco: mientras su token lleve `idUsuario = 0`, ninguna escritura que dependa del
  usuario se puede comparar por HTTP, y hay dos procedimientos que con 0 **no hacen
  nada y contestan que sí**.
* **Volver a correr las 29 de Tauro-vacío contra Zaragoza (`MacZ`).** Son casi la
  mitad de las no verificables y el banco ya está escrito: solo hay que apuntar la
  cadena y los ids.
* **Las cuatro de `descuentos` que no tienen procedimiento.** Si de verdad se usan,
  hay que crear `sp_w_ActualizarDescuentos`, `sp_w_EliminarDescuentos` y
  `sp_w_ConsultaDescuentos`; si no, son código muerto y conviene decirlo en el
  tablero. El endpoint nuevo ya está escrito y empezará a funcionar solo.
* **`sp_n_InsertarDescuentos` crea el renglón en blanco.** El nombre del descuento
  no llega a la base porque el procedimiento no lo declara. Hace falta un
  `sp_w_InsertarDescuento` que lo reciba; mientras tanto el endpoint lo dice en su
  mensaje.
* **La composición del CFDI.** Los conceptos vienen en el request, no se leen de la
  remisión. El día que haya forma de probar un timbrado (una cuenta de pruebas de
  Facturapi) se puede mover esa composición al backend y comparar de verdad.
* **El orden del lote de pedidos es ordinal por texto** porque es el que ya produjo
  diez años de folios. Cambiarlo a numérico es una decisión de negocio, no técnica:
  hay que hacerla en los dos lados a la vez o no hacerla.
* **`sp_n_ConsultarVentasUsadosExcendentes` dice "Excendentes"** con una `n` de más,
  y así se llama en la base. El código nuevo escribe el nombre bien (`Excedente`)
  en todo salvo en el nombre del procedimiento y de sus parámetros, para que el día
  que se reescriba como `sp_w_` el error no le sobreviva.
* **Nada de `Legacy/` se tocó**, nada de la base se modificó y no se corrió ningún
  script. El marcador `instantanea-en-uso-5081` se creó al empezar la tanda de
  escrituras y se borró al terminar.
