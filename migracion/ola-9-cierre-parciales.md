# Ola 9 — las comparaciones que no probaban nada

Lote: las capacidades a medias o sin comprobar, y **las pruebas de comparación
que no prueban nada**. Backend nuevo en el puerto 5091, worktree aislado.

El lote cambió de forma a media jornada y conviene decirlo primero, porque
explica la tabla:

* **Los frentes 2 y 3 se quedaron sin filas.** Cuando se escribió el encargo el
  tablero tenía 17 `por-verificar` y 136 `parcial`. Al terminar esta ola tiene
  **0 y 0**: los otros tres agentes las cerraron mientras esto corría. El detalle
  de qué se encontró en cada una, antes de que se vaciaran, está en §3 y §4.
* **El frente 1 sí era mío y creció.** La auditoría había encontrado 18 casos que
  comparan lista vacía contra lista vacía. Barriendo los **135** casos del banco
  aparecieron **27**: las 18 de la auditoría más 9 que entraron después con las
  olas 4 y 5. Están todas tratadas.
* **De paso salió un fallo que tiraba dos endpoints**, y no era de los casos sino
  del código: §2.

---

## 1. La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar el log de clientes | `POST /api/clientes/consultar/log` | `GET /api/clientes/{id}/log` | `clientes.log.ver` | **CORREGIDO** | el caso pedía el cliente 1, que no tiene movimientos; ahora el 20323, con 8 de los 358 de `[Clientes Log]`. Al ver renglones salió DIF y se acotó con `solo_campos` |
| Consultar fechas permitidas por usuario | `POST /api/usuarios/fechas/consultar` | `GET /api/usuarios/fechas` | `usuarios.ver` | **CORREGIDO** | pedía el usuario 9, sin renglones; ahora el 40, el único que tiene. Salió DIF en `FechaFtm` y se cubrió el `ignorar` |
| Consultar huellas digitales (rama empleado) | `POST /api/huellas/consultar` | `GET /api/huellas?origen=empleado` | `empleados.huellas` | **CORREGIDO** | rama nunca probada: 62 renglones con la huella completa (~16 MB por lado). Sin `mapa_campos` el DIF era total |
| Consultar consulta de garantías (aceptar) | `POST /api/garantias/aceptar/consultar` | `POST /api/ventas/garantias/aceptar/consultar` | `garantias.ver` | **CORREGIDO** | el rango 2026 daba 0 de las 2.037 garantías; con 2024-2026 y la rama 2 son **924** |
| Consultar consulta de garantías (pendientes) | `POST /api/garantias/aceptar/consultar` | `POST /api/ventas/garantias/aceptar/consultar` | `garantias.ver` | **CORREGIDO** | caso NUEVO para la rama 1, que **borra el rango de fechas** que se le manda. 340 renglones |
| Consultar en tránsito de ventas/remisiones | `POST /api/consultaventas/consultar/transito` | `POST /api/ventas/consultas/transito` | `ventas.consultar` | **CORREGIDO** | con `IDCliente = 0` el SP filtra a clientes de centro de servicio y en Tauro hay 0 ventas a ellos; con el cliente 2844 son **306** |
| Registrar detalle de descuentos | `POST /api/descuentosdetalle` | `POST /api/ventas/descuentos/detalle/alta` | `descuentos.editar` | **CORREGIDO** | chocaba con la consulta de la ola 2 y ASP.NET contestaba **500 con cuerpo vacío a las dos**. Ver §2 |
| Consultar detalle de descuentos | `POST /api/descuentosdetalle/consultar` | `POST /api/ventas/descuentos/detalle` | `descuentos.ver` | **CORREGIDO** | volvió a responder sus 129 renglones; estaba caída por el choque de ruta |
| Consultar catálogo de galleros | `POST /api/catgalleros/consultar` | `GET /api/catalogos/galleros` | `catalogos.galleros.ver` | NO VERIFICABLE | `[Catalogo Galleros]` = 0 en **las dos** bases |
| Consultar asistencias por periodo | `POST /api/empleadosperiodoasistencias/diaria/consultar` | `GET /api/asistencias/diaria` | `asistencias.ver` | NO VERIFICABLE | el SP está clavado a `GETDATE()`; nadie checó hoy |
| Consultar pedidos faltantes | `POST /api/pedidos/faltante/consultar` | `GET /api/ventas/consulta/pedidos-faltantes` | — | NO VERIFICABLE | clavado a `GETDATE()`; MacZ tiene 618 renglones pero el último es de julio 2026 |
| Consultar consulta de pagos de venta (kilos) | `POST /api/consultaventaspagos/kilos/consultar` | `POST /api/ventas/cobros/pagos-kilos` | `cobranza.ver` | NO VERIFICABLE | `VentasPagosKilos` = 0 en las dos bases. **Y el SP declara `@IDCliente` y no lo usa** |
| Consultar cortes de caja (CS, canceladas) | `POST /api/cortescs/ventas/canceladas/consultar` | `GET /api/cortes/centros-servicio/ventas-canceladas` | `cortes.cs.ver` | NO VERIFICABLE | `CortesVentasCanceladasCS` = 0 en las dos bases |
| Consultar consulta de cobros (CS) | `POST /api/cobros/centroservicio/consultar` | `GET /api/ventas/cobros/centro-servicio` | `cobranza.ver` | APUNTADO A MacZ | `Cobros` = 0 aquí / **21.485** en MacZ |
| Consultar consulta de cobros | `POST /api/cobros/consultar` | `POST /api/ventas/cobranza/cobros/consultar` | `cobranza.ver` | APUNTADO A MacZ | `Cobros` = 0 aquí / 21.485 en MacZ |
| Consultar clientes de un cobro | `POST /api/cobrosclientes/consultar` | `GET /api/ventas/cobranza/cobros/{id}/clientes` | `cobranza.ver` | APUNTADO A MacZ | `CobrosDineroUsadoClientes` = 0 aquí / 5.025 en MacZ |
| Consultar cobros de dinero y usados | `POST /api/ventascobrosdinerousados/consultar` | `POST /api/ventas/usados/cobros-dinero/consultar` | `ventas.usados` | APUNTADO A MacZ | `CobrosDineroUsado` = 0 aquí / **13.664** en MacZ |
| Consultar el detalle de cobros de dinero y usados | `POST /api/ventascobrosdinerousados/consultar/detalle` | `GET /api/ventas/usados/consulta/cobros-dinero/detalle` | `ventas.usados` | APUNTADO A MacZ | igual que el anterior |
| Consultar cheques de cobros de dinero y usados | `POST /api/ventascobrosdinerousados/cheques/consultar` | `POST /api/ventas/usados/cobros-dinero/cheques/consultar` | `ventas.usados` | APUNTADO A MacZ | `CobrosDineroUsadoCheques` = 0 aquí / 341 en MacZ |
| Consultar cortes CS de cobros de dinero y usados | `POST /api/ventascobrosdinerousados/cortescs/consultar` | `POST /api/ventas/usados/cobros-dinero/cortes-cs/consultar` | `ventas.usados` | APUNTADO A MacZ | `CobrosDineroUsadoCortesCS` = 0 aquí / 294 en MacZ |
| Consultar excedentes de usados | `POST /api/ventasusadosexcedentes/consultar` | `POST /api/ventas/usados/excedentes/consultar` | `ventas.usados` | APUNTADO A MacZ | **la tabla NO está vacía (254) y aun así no devuelve nada**: un `INNER JOIN Cobros` la vacía. Ver §5 |
| Consultar huellas digitales (rama usuario) | `POST /api/huellas/consultar` | `GET /api/huellas?origen=usuario` | `empleados.huellas` | APUNTADO A MacZ | `UsuariosHuellas` = 0 aquí / 12 en MacZ. La rama de empleados ya quedó comparada |
| Consultar cobros de usados | `POST /api/ventascobrosusados/consultar` | `POST /api/ventas/usados/cobros/consultar` | `ventas.usados` | NO VERIFICABLE | `VentasCobrosUsados` = 0 en las dos bases |
| Consultar registros de garantía | `POST /api/garantias/registrar/consultar` | `POST /api/ventas/garantias/registros/consultar` | `garantias.ver` | NO VERIFICABLE | `GarantiasRegistro` = 0 en las dos bases |
| Consultar contratos (MC) | `POST /api/mccontratos/consultar` | `GET /api/obra/contratos` | `obra.contratos.ver` | NO VERIFICABLE | `MC_Contratos` = 0 en las dos bases |
| Consultar conceptos de contrato (MC) | `POST /api/mccontratos/conceptos/consultar` | `GET /api/obra/contratos/conceptos` | `obra.contratos.ver` | NO VERIFICABLE | módulo de obra sin datos en las dos bases |
| Consultar estimaciones de contrato (MC) | `POST /api/mccontratos/estimaciones/consultar` | `GET /api/obra/contratos/estimaciones` | `obra.contratos.ver` | NO VERIFICABLE | ídem |
| Consultar grupos de contrato (MC) | `POST /api/mccontratos/grupos/consultar` | `GET /api/obra/contratos/grupos` | `obra.contratos.ver` | NO VERIFICABLE | ídem |
| Consultar proveedores (MC) | `POST /api/mcproveedores/consultar` | `GET /api/obra/proveedores` | `obra.proveedores.ver` | NO VERIFICABLE | `MC_Proveedores` = 0 en las dos. Ojo: no es `Proveedores`, que sí tiene 52 |
| Consultar residentes (MC) | `POST /api/mcresidentes/consultar` | `GET /api/obra/catalogos/residentes` | `obra.catalogos.ver` | NO VERIFICABLE | `MC_Residentes` = 0 en las dos |
| Consultar apoderados de proveedor | `POST /api/proveedores/apoderados/consultar` | `GET /api/proveedores/apoderados` | `proveedores.ver` | NO VERIFICABLE | `ProveedoresApoderados` = 0 en las dos |
| Consultar documentos de proveedor | `POST /api/proveedores/documentos/consultar` | `GET /api/proveedores/documentos` | `proveedores.ver` | NO VERIFICABLE | `ProveedoresDocumentos` = 0 en las dos |
| Consultar domicilios de proveedor | `POST /api/proveedores/domicilios/consultar` | `GET /api/proveedores/domicilios` | `proveedores.ver` | NO VERIFICABLE | `ProveedoresDomicilios` = 0 en las dos |

> `CORREGIDO` aquí significa: el caso dejó de ser humo y ahora compara renglones
> reales. En los seis casos de datos, el endpoint nuevo **ya estaba bien**: lo que
> estaba mal era la prueba. En el séptimo y el octavo (descuentos) lo que estaba
> mal era el código, y se arregló.

---

## 2. El fallo que tiraba dos endpoints a la vez

No lo buscaba. Apareció al barrer el banco pidiéndole sólo al backend nuevo:
dos casos que estaban en verde contestaban **500 con `Content-Length: 0`**.

```
POST /api/ventas/descuentos/detalle   ->  500, cuerpo vacío
```

En el log:

```
AmbiguousMatchException: The request matched multiple endpoints. Matches:
  ISL_Service.Controllers.VentasDescuentosController.CrearDetalleAsync
  ISL_Service.Controllers.VentasDescuentosConsultaController.Detalle
```

Dos acciones declaradas en la misma ruta y el mismo verbo:

* `VentasDescuentosConsultaController.Detalle` — **la consulta**, migrada en la
  ola 2 y comparada contra Legacy (129 renglones, más un caso extra de 67);
* `VentasDescuentosController.CrearDetalleAsync` — **el alta**, de la ola 4.

Tres cosas que lo hacen peor de lo que suena:

1. **No se cae la nueva: se caen las dos.** La consulta, que llevaba días en
   verde y que el web ya llama, dejó de responder.
2. **ASP.NET no avisa al arrancar.** Avisa en la primera petición a esa ruta, y
   avisa con un 500 sin cuerpo: sin mensaje, sin código, sin nada que leer.
3. **La colisión no existe en ninguna de las dos ramas por separado.** Aparece
   sólo al juntarlas, que es justo donde nadie estaba mirando. Los dos reportes
   de ola —el de lecturas y el de escrituras— declaran la MISMA ruta nueva, cada
   uno con su `OK`, y los dos tenían razón en su worktree.

**Arreglo:** se movió el alta a `POST /api/ventas/descuentos/detalle/alta`. Se
movió el alta y no la consulta porque la consulta es anterior, está comparada y
es la que el web ya llama; el sufijo va a juego con sus hermanos del mismo
controlador (`detalle/eliminar`, `ajustes/status`). La tabla de
`ola-4-ventas-escrituras.md` quedó corregida.

**Para que no vuelva:** `ISL_Service.Tests/RutasSinChocarTests.cs` recorre por
reflexión las 706 acciones HTTP del ensamblado y falla si dos comparten verbo y
plantilla (normalizando `{id}` / `{idVenta}`, que para el ruteo son la misma
cosa, y dejando pasar `api/[controller]`, que el runtime sustituye). Se comprobó
que la prueba **sí falla** volviendo a poner la ruta mala: señala el choque con
los dos nombres. Cuesta 48 ms.

---

## 3. Las 18 comparaciones de la auditoría, una por una

De las 18, **tres se re-apuntaron a datos reales de `Produccion_svr`** y ahora
comparan de verdad. La auditoría las había identificado; aquí quedaron escritas
en el caso, corridas y con las diferencias que salieron resueltas.

### 3.1 `ola3-cliente-log` — el parámetro malo era `IDCliente = 1`

* **Lo que pedía:** cliente 1, que no tiene ningún movimiento.
* **Con qué se probó:** cliente **20323**, el que más tiene: 8 renglones de los
  358 de `[Clientes Log]`.
* **Qué apareció al probarla de verdad:** **sí, una diferencia.** Legacy reusa el
  `ClienteDTO` de ochenta propiedades para la bitácora y manda setenta y dos
  vacías que el procedimiento nunca devolvió (`agente`, `apellidoMaterno`,
  `calle`, `cascosCambio`…). El caso ya declaraba esa diferencia como
  intencional, pero nada la acotaba. Se añadió `solo_campos` con los **siete**
  campos que el procedimiento manda. Ahora OK.

### 3.2 `ola3-usuario-fechas` — el parámetro malo era `IDUsuario = 9`

* **Lo que pedía:** usuario 9, sin renglones en `UsuariosFechas`.
* **Con qué se probó:** usuario **40**, el único de la tabla (sus 2 renglones).
* **Qué apareció:** **sí, una diferencia**: `FechaFtm`. El caso ya la declaraba
  intencional, pero su lista `ignorar` **no la cubría** — porque con el resultado
  vacío nunca tuvo que cubrirla. Es la firma exacta de un caso que nunca vio un
  renglón. Se agregó a `ignorar`. Ahora OK.

### 3.3 `ola3-huellas-usuarios` — el parámetro malo era la RAMA, no el valor

* **Lo que pedía:** sólo `origen=usuario`, y `UsuariosHuellas` está vacía aquí.
* **Con qué se probó:** la rama `origen=empleado`, que **jamás se había probado**.
  `EmpleadosHuellas` tiene 409 renglones y el procedimiento devuelve **62** tras
  filtrar por empleado activo. Son ~16 MB de base64 por lado.
* **Qué apareció:** **sí, y era total.** El DTO de Legacy declara los **seis**
  identificadores de los tres orígenes a la vez
  (`IDUsuarioHuella`, `IDUsuario`, `IDEmpleadoHuella`, `IDEmpleado`,
  `IDClienteHuella`, `IDCliente`) y en esta rama cuatro salen en 0; el nuevo
  publica dos (`idHuella`, `idPersona`). Sin `mapa_campos` los renglones no se
  emparejan y el comparador reporta las 62 huellas como distintas. Con
  `mapa_campos` + `solo_campos`: **0 diferencias, huella por huella**.
* Va en un caso nuevo, `ola3-huellas-empleados`; el viejo se queda cubriendo la
  rama de usuario, apuntado a MacZ. La tercera rama, `origen=cliente`, no es
  verificable: `ClientesHuellas` = 0 en las dos bases.

### 3.4 Las otras quince

Ninguna tenía un parámetro malo: la tabla está realmente vacía. Las quince
llevan ahora en su `_porque` el `COUNT(*)` **de las dos bases**, contado hoy, y
la razón. Donde MacZ sí tiene datos quedan marcadas con `_base_con_datos` y la
instrucción de qué hace falta para correrlas; donde no hay datos en ninguna,
`_no_verificable_aqui: true`.

Lo que se encontró de paso, que no era el objetivo pero sí es trabajo:

* **`sp_n_ConsultarVentasPagosKilos` declara `@IDCliente` y no lo usa.** No
  aparece en ninguna línea del `WHERE` que arma: filtra por `IDVentaPagoKilo`,
  `IDCobro`, `IDVenta` y fecha. El `1534` del caso no filtraba nada **en ninguno
  de los dos lados**; con datos, los dos devolverían todos los pagos por kilo del
  rango. El caso lo dice ahora.
* **`pedidos-faltantes` y `asistencia-diaria` no son verificables ni con MacZ, y
  no es por la tabla: es por la fecha.** Los dos procedimientos están clavados a
  `CAST(GETDATE() as date)` y ninguno usa el parámetro de fecha que declara (el
  de asistencias es `varchar(1)`, un solo carácter). En MacZ hay 618 faltantes,
  pero el último es del 14-jul-2026.

---

## 4. Las 9 que entraron después de la auditoría

La auditoría contó 18 porque entonces el banco tenía 112 casos. Hoy tiene 135, y
el barrido encontró **9 casos nuevos con la misma enfermedad**, de las olas 4 y 5.
Dos de ellas eran rescatables y se rescataron (están en §1 como CORREGIDO); las
otras siete quedaron anotadas igual que las quince de §3.4:

| caso | tabla | Tauro | MacZ | qué se hizo |
|---|---|---|---|---|
| `ola4-garantias-aceptar-consultar` | `Garantias` | **2.037** | 24.854 | re-apuntado: 924 renglones |
| `ola4-ventas-transito-consultar` | `Ventas Detalle` | 255.813 ventas | — | re-apuntado: 306 renglones |
| `ola4-cobro-clientes` | `CobrosDineroUsadoClientes` | 0 | 5.025 | apuntado a MacZ |
| `ola4-cobros-consultar` | `Cobros` | 0 | 21.485 | apuntado a MacZ |
| `ola4-usados-cobros-dinero-consultar` | `CobrosDineroUsado` | 0 | 13.664 | apuntado a MacZ |
| `ola4-usados-cheques-consultar` | `CobrosDineroUsadoCheques` | 0 | 341 | apuntado a MacZ |
| `ola4-usados-cortes-cs-consultar` | `CobrosDineroUsadoCortesCS` | 0 | 294 | apuntado a MacZ |
| `ola4-usados-excedentes-consultar` | `…ExcendentesResumen` | **254** | 8.347 | apuntado a MacZ — ver §5 |
| `ola4-usados-cobros-consultar` | `VentasCobrosUsados` | 0 | 0 | no verificable |
| `ola4-garantias-registro-consultar` | `GarantiasRegistro` | 0 | 0 | no verificable |
| `ola5-obra-contratos` | `MC_Contratos` | 0 | 0 | no verificable |

Los dos rescates, comprobados contra el **mismo procedimiento** ejecutado directo
en la base (ver §6):

* `ola4-garantias-aceptar-consultar`: el estorbo no era el id, era el **rango**.
  Pedía `IDGarantia = 3997` con 01-01-2026 / 31-12-2026 y esa combinación da 0,
  teniendo 2.037 garantías en la tabla. Con la rama 2 (Aceptadas) y 2024-2026:
  **924 renglones, los mismos ids que el SP**. Y al mirarlo se vio que la rama 1
  (Pendientes) **borra el rango de fechas que se le manda**
  (`SELECT @FechaInicial = '', @FechaFinal = ''`): un filtro de fechas en esa
  pantalla no filtra nada. Eso merecía su propio caso, y lo tiene
  (`ola4-garantias-aceptar-pendientes`, 340 renglones, mismos ids que el SP).
* `ola4-ventas-transito-consultar`: tampoco eran las fechas. Con
  `@IDCliente = 0` el procedimiento agrega
  `AND Ven.IDCliente IN (SELECT IDCliente FROM CentrosServicio)`, y en Tauro hay
  10 centros de servicio y **cero ventas a sus clientes**: así devuelve vacío
  pase lo que pase con el rango. Con el cliente 2844 y la rama 2: **306
  renglones, mismos ids que el SP**.

---

## 5. Dos trampas que vale la pena no volver a pisar

**«La tabla tiene datos» no quiere decir «el procedimiento devuelve renglones».**
`VentasUsadosExcendentesResumen` tiene 254 renglones en Tauro y el endpoint
devuelve 0 con cualquier parámetro. La razón es un `INNER JOIN Cobros` y `Cobros`
está vacía: un INNER JOIN contra una tabla vacía tira **todos** los renglones.
Lo mismo le pasa a `sp_n_ConsultarVentasPagosKilos`. Al buscar datos para un caso
hay que mirar los INNER JOIN del SP, no sólo la tabla principal.

**Un `COUNT(*) > 0` tampoco basta si el SP filtra por `GETDATE()`.** Tres de los
casos (`pedidos-faltantes`, `asistencia-diaria`, y la primera lectura del día de
cualquier cosa) dependen del día de hoy, no de los datos. Con una base que llega
a marzo de 2026 no hay parámetro que los salve.

---

## 6. Lo que quedó BLOQUEADO y necesita tu permiso

**No se pudo correr el banco contra Legacy en esta ola, y no es por el código.**

```
Constantes.FechaOperacion = 2026-10-06      hoy = 2026-10-07
```

`sp_n_ConsultaConstantes` —que llaman 155 de las 488 capacidades de Legacy, casi
siempre como primer paso— **no es una consulta**: cuando la fecha de operación
está atrasada inserta en `FechaOperacionLog`, borra y rehace
`AlmacenProductoHist` del día anterior y rueda `Constantes.FechaOperacion` a hoy.
Es la rutina de cierre del día del negocio
(`CUIDADO-consultas-que-escriben.md`).

O sea: **la primera llamada a Legacy de hoy hace el cierre del día y rehace el
corte de inventario de ayer.** No se disparó. Dos razones:

1. es una escritura en la base, y el protocolo dice que eso se hace a propósito y
   de forma controlada, no de pasada dentro de una tanda de comparaciones;
2. hay otros tres agentes trabajando contra `Produccion_svr` ahora mismo, y
   rodar la fecha les cambia los datos a media tanda — exactamente la
   «diferencia que no es del backend sino del orden en que se preguntó» que el
   comparador vigila.

**Lo que hace falta de ti:** autorizar que la fecha de operación ruede una vez,
avisando a los cuatro frentes, o decir que se espere. En cuanto ruede, el banco
corre normal: los casos ya están escritos y apuntados a datos reales.

**Mientras tanto, lo que sí se comprobó, y cómo.** Para los tres casos
re-apuntados en §4 se ejecutó el **mismo stored procedure que llama Legacy**,
directo con `sqlcmd` y con los mismos parámetros, y se contrastó renglón por
renglón contra la respuesta del backend nuevo:

| caso | renglones del SP | renglones del nuevo | mismos ids |
|---|---|---|---|
| `ola4-garantias-aceptar-consultar` | 924 | 924 | sí |
| `ola4-garantias-aceptar-pendientes` | 340 | 340 | sí |
| `ola4-ventas-transito-consultar` | 306 | 306 | sí |

No es una comparación contra Legacy y no se cuenta como tal: Legacy podría
transformar algo entre el SP y el JSON. Pero sí prueba que el endpoint nuevo
devuelve **los renglones del procedimiento y no otros**, que es lo que el `OK`
vacío no probaba.

Los tres casos re-apuntados de §3 (`cliente-log`, `usuario-fechas`,
`huellas-empleados`) **sí** se corrieron contra Legacy, antes de que cambiara el
día, y los tres quedaron en OK.

---

## 7. Los frentes 2 y 3, y a quién se le devolvió qué

### 7.1 Las `por-verificar`

Cuando se tomó el lote eran 17; en el tablero quedaban **10**, y las diez caían
dentro de un módulo con dueño. Se devolvieron sin tocarlas, porque las diez son
**escrituras** y pelearse por el mismo camino de escritura con otro agente es la
forma más rápida de que nadie pueda comparar nada:

| capacidad | para quién |
|---|---|
| Eliminar / registrar detalle de pedido | ventas |
| Eliminar / actualizar pedidos | ventas |
| Actualizar concentrados de venta | ventas |
| Cancelar ventas/remisiones | ventas |
| Ajustar ventas/remisiones (usados crédito) | ventas |
| Cancelar / registrar recaudaciones (aviación) | obra y aviación |
| Registrar cortes de recaudaciones (aviación) | obra y aviación |

Al cerrar esta ola el tablero las tiene en **0**: los dueños las cerraron.

### 7.2 Las `parcial`

De las 136 del encargo quedaban **103**. Se clasificaron las 103 a máquina,
cruzando el SP «propio» de cada fila (descontando los compartidos:
`sp_n_ConsultaConstantes`, `sp_n_SincronizarDownload`, `sp_n_ConsultaParametros`,
`sp_n_ConsultaTemplateHtml`) contra los literales de SP del código nuevo, sin
contar los que están en comentarios:

| módulo | ya existía completa | existía a medias | no existía | dueño |
|---|---|---|---|---|
| aviación (cuentas, económicos, motivos de fianza, operadores, permisionarios, pólizas) | 0 | 0 | 47 | obra y aviación |
| ventas (cobranza, cobros, garantías, usados, facturas, pedidos, detalles) | 0 | 7 | 33 | ventas |
| obra / MC (contratos, convenios, proveedores) | 0 | 0 | 12 | obra y aviación |
| gasolinas | 0 | 0 | 3 | **sin dueño → se tomó** |
| nómina (recibos) | 1 | 0 | 0 | catálogos/inventario/nómina |

Resultado: **100 devueltas a sus dueños** y **3 tomadas** (gasolinas). El dato
útil de la clasificación es que `parcial` casi nunca significaba «a medias»:
**92 de 103 eran «no existía»** y estaban en `parcial` sólo porque compartían
`sp_n_ConsultaConstantes` con algo ya migrado. La única que sí estaba completa y
con el tablero desactualizado era «Consultar recibos de nómina».

**Las tres de gasolinas se implementaron y luego se descartaron**, y conviene
decirlo tal cual en vez de esconderlo: el módulo de gasolinas se escribió entero
aquí (controlador, servicio, repositorio, DTOs, permisos y 15 pruebas), y al ir a
sembrar los permisos aparecieron en `WPermiso` claves `gasolinas.*` que no eran
mías. El agente de obra y aviación ya lo había hecho y mejor: en su commit
«Gasolinas y cobranzas: los dos módulos del lote que sí se pueden comparar»,
**comparado contra Legacy con los 2.554 renglones**. Se borró la versión de aquí
y se tomó la suya. La lección es de coordinación, no de código: «sin dueño» se
comprobó contra el tablero, que ya estaba viejo, y no contra la rama.

---

## 8. Cierre

| | |
|---|---|
| Casos que pasaron de «vacío contra vacío» a comparación real | **6** (3 de la auditoría + 2 rescatados de la ola 4 + 1 caso nuevo de la rama que borra el rango) |
| Casos vacíos que quedan, todos con su `COUNT(*)` de las dos bases y su motivo | **25** — de ellos **9 apuntados a MacZ** y **16 no verificables en esta máquina** |
| `por-verificar` cerradas por mí | **0** (las 10 que quedaban eran de otros; hoy el tablero está en 0) |
| `parcial` resueltas por mí | **0 propias**; las 3 de gasolinas se implementaron y se descartaron por duplicado |
| `parcial` devueltas a otros agentes | **100** de 103, clasificadas una por una |
| Fallos de código encontrados y arreglados | **1**, que tiraba **2** endpoints comparados (choque de ruta) |
| Pruebas | **413** en verde (412 + la nueva de rutas, que se comprobó que falla cuando debe) |
| Casos del banco | **135** (134 + `ola4-garantias-aceptar-pendientes`) |

Lo que queda abierto, y es de quien decida:

1. **Rodar la fecha de operación** para poder volver a correr el banco contra
   Legacy (§6). Hoy bloquea a los cuatro frentes, no sólo a este.
2. **Las 9 comparaciones apuntadas a MacZ.** Para correrlas hacen falta dos cosas
   que hoy no se pueden tocar: la base de Legacy sale de su `Web.config`, y el
   usuario de pruebas del backend nuevo no existe en MacZ. Con una copia de la
   carpeta de Legacy fuera de `Legacy/` y un usuario de pruebas en MacZ se
   cierran las nueve.
3. **Distinguir en el tablero un `OK` con datos de un `OK` vacío.** Es la
   recomendación de la auditoría y sigue viva: 25 de 135 casos están en verde sin
   haber visto un renglón. Hoy eso sólo se sabe leyendo el `_porque` de cada
   caso; debería ser una columna.

### Por qué esta ola NO corrió `actualizar-registro.py`

A propósito, y es parte del punto 3. El script traduce veredictos con un
vocabulario fijo, y **ninguno de sus valores describe lo que son estas 25 filas**:

* `hecho` + «comparado contra Legacy» es lo que dicen hoy, y es lo que la
  auditoría llamó humo;
* `hecho-sin-comparar` significa «Legacy no puede responder porque le falta el SP
  o la tabla», y aquí **Legacy responde perfectamente**: responde una lista vacía
  porque no hay datos que devolver. Marcarlas así cambiaría una imprecisión por
  otra, y además movería el titular de 296 `hecho` a 280 por una razón equivocada.

Pasar estas 25 al tablero necesita primero el valor que no existe (`hecho-vacio`,
o una columna `renglones_comparados`). Hasta entonces la verdad vive en el
`_porque` de cada caso, que es donde se escribió, y en la tabla de §1. Inventar
un estado a mano habría dejado dos verdades, que es lo que `registro.tsv` existe
para evitar.
