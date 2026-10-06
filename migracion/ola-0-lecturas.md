# Ola 0 — las lecturas que ya existen en los dos lados

44 capacidades de `registro.tsv` con `estado = por-verificar` y `escribe = no`.
Todas se probaron **ejecutando** las dos puntas contra la MISMA base
(`LAP-JUAND` / `Produccion_svr`) con el comparador de `comparador/`.

Resultado: **30 COINCIDE, 1 CORREGIDO, 5 INTENCIONAL, 8 FALTA**.
64 casos en `comparador/casos/`, los 64 en OK.

Para reproducir:

```bash
cd /d/JuanD/GitHub/MAC/sacNet-8/migracion/comparador
MAC_SCRATCH="<scratchpad>" python comparar.py casos/
```

## Tabla

| capacidad | ruta legacy | ruta nueva | veredicto | qué pasó |
|---|---|---|---|---|
| Catálogo de agentes | POST /api/catagentes/consultar | GET /api/ventas/consulta/catalogos → `agentes` | COINCIDE | caso semilla; a Legacy se le pide `IDStatus=1` porque el nuevo ya filtra a activos |
| Catálogo de almacenes | POST /api/catalmacenes/consultar | GET /api/reportes/ventas/acumuladores-productos/catalogos → `almacenes` | COINCIDE | mismos 4 parámetros al SP |
| Catálogo de autos | POST /api/catautos/consultar | GET /api/reportes/ventas/remisiones/catalogos → `autos` | COINCIDE | 13 = 13 |
| Catálogo de empresas | POST /api/empresas/consultar | …/acumuladores-productos/catalogos → `empresas` | COINCIDE | el nuevo publica `NombreCorto`, que es lo que pinta Mac31 |
| Grupos de categorías | POST /api/catgruposcategorias/consultar | …/catalogos → `categorias` | COINCIDE | renombrado a `categorias`/`subcategorias` en el contrato nuevo |
| Categorías | POST /api/categorias/consultar | …/catalogos?idGrupoCategoria=1 → `subcategorias` | COINCIDE | grupo fijado a 1 en los DOS lados |
| Marcas | POST /api/catgruposcategorias/marcas/consultar | …/catalogos?idGrupoCategoria=1&idCategorias=1 → `marcas` | COINCIDE | mismo separador `~` en los dos |
| Catálogo de repartidores | POST /api/catrepartidores/consultar | …/remisiones/catalogos → `repartidores` | COINCIDE | 20 = 20 |
| Tipos de gasto | POST /api/cattipogasto/consultar | …/remisiones/catalogos → `tiposGasto` | COINCIDE | 55 = 55 |
| Proveedores | POST /api/proveedores/consultar | …/remisiones/catalogos → `proveedores` | COINCIDE | los mismos 11 parámetros |
| Usuarios | POST /api/usuarios/consultar | …/remisiones/catalogos → `usuarios` | COINCIDE | Legacy manda las contraseñas EN CLARO; el nuevo entrega `{id,nombre}` |
| Catálogo de bancos | POST /api/catbancos/consultar | POST /api/ventas/multipago/pantalla → `bancos` | COINCIDE | 30 = 30; sin ruta propia, viaja dentro de la pantalla |
| Tipos de usado (cascos) | POST /api/cattipousado/consultar | GET /api/ventas/pedidos/captura/bootstrap → `pedido.cascosCargo` | COINCIDE | 8 = 8; sin ruta propia |
| Consulta de ventas/remisiones | POST /api/consultaventas/consultar | POST /api/ventas/consulta/remisiones | COINCIDE | 22 renglones, 130 columnas iguales |
| Consulta de pedidos | POST /api/consultaventaspedido/consultar | POST /api/ventas/consulta/pedidos | **CORREGIDO** | a los pedidos viejos de una venta se les pintaban las fechas del pedido nuevo (ver abajo) |
| Pendientes de imprimir | POST /api/consultaventaspedido/pendientes/imprimir/consultar | POST /api/ventas/consulta/pendientes-imprimir | COINCIDE | el contador da 10 en los dos |
| Constantes del sistema | POST /api/constantes/consultar | — (viajan dentro de /api/ventas/consulta/catalogos) | INTENCIONAL | Legacy devuelve un `DataRow` crudo de ADO.NET (`RowError`, `RowState`, `ItemArray`), no un contrato. El nuevo expone solo lo que las pantallas usan; verificado contra la tabla `Constantes` con sqlcmd |
| Saldos de venta | POST /api/ventassaldos/consultar | GET /api/ventas/saldos/cliente/{id} | COINCIDE | 3 renglones idénticos |
| Cobranza pendiente y vencida | POST /api/cobranzapendientesvencidos/consultar | — | **FALTA** | `sp_n_rptVentasSaldosPrincipal` no se usa en ninguna parte del nuevo. Lo más cercano genera un PDF, y a propósito llama al SP de adentro porque el `Principal` truena en esta base (error 213) |
| Clientes (selector) | POST /api/clientes/consultar | GET /api/reportes/ventas/acumuladores-productos/clientes | COINCIDE | los 4 campos del selector |
| Clientes (ficha) | POST /api/clientes/consultar | POST /api/ventas/pedidos/captura/clientes/buscar | COINCIDE | 69 columnas, cero diferencias de valor |
| Domicilios | POST /api/domicilios/consultar | (dentro de clientes/buscar) | COINCIDE | 1 domicilio igual |
| Crédito disponible | POST /api/creditodisponible/consultar | (dentro de clientes/buscar) | COINCIDE | límite, sumas y fechas iguales |
| Consultar empleados | POST /api/empleados/consultar | GET /api/empleados?idStatus=1 | INTENCIONAL | SP distinto a propósito (`sp_n_ConsultaEmpleadosTipoSueldo`, porque `sp_n_ConsultaEmpleados` no trae puesto); mismos 25 empleados y mismos valores |
| Tipo de sueldo del empleado | POST /api/empleadostiposueldo/consultar | GET /api/empleados, GET /api/empleados/{id} | COINCIDE | mismo SP, 25 renglones idénticos sin un solo mapeo |
| Asistencias por periodo | POST /api/empleadosperiodoasistencias/consultar | GET /api/asistencias?idPeriodoTipoSueldo=… | COINCIDE | identidad + 28 campos de día + 3 totales |
| Periodos de nómina | POST /api/periodostiposueldo/consultar | GET /api/asistencias/periodos | INTENCIONAL | mismo SP; el nuevo entrega 6 de 13 campos y renombra `PeriodoTipoSueldo`→`descripcion` |
| Días del periodo | POST /api/periodostiposueldodias/consultar | GET /api/asistencias/periodos/{id}/dias | INTENCIONAL | mismo SP; el nuevo entrega solo `{id, fecha}` |
| Préstamos a empleados | POST /api/empleadosprestamo/consultar | GET /api/prestamos, GET /api/prestamos/{id} | COINCIDE | 85 renglones idénticos, incluida la traducción `pendientes` → `@Pagados=2` |
| Validar préstamos a empleados | POST /api/empleadosprestamo/validar | — | **FALTA (a propósito)** | **La fila está mal clasificada: ESCRIBE.** `sp_n_ValidaEmpleadosPrestamos` abre `BEGIN TRAN`, inserta en `EmpleadoPrestamosPagos` y actualiza `EmpleadoPrestamos`. Es el batch semanal de nómina; no se ejecutó. Su ausencia está documentada en `PrestamosController.cs:20` |
| Recaudaciones (aviación) | POST /api/recaudaciones/consultar | — | **FALTA** | el nuevo solo resuelve UN ticket por QR cifrado; no existe la consulta con filtros. Además `Recaudaciones` tiene 0 filas en esta base (es Tauro, no aviación) |
| Cortes de recaudaciones | POST /api/recaudaciones/consultar/cortes | — | **FALTA** | el SP está declarado en `RecaudacionRepository.cs:31` y nunca se usa: cadena muerta |
| Recurrentes de recaudaciones | POST /api/recaudaciones/consultar/recurrentes | — | **FALTA** | igual, `RecaudacionRepository.cs:29` |
| Siguiente ID de recaudaciones | POST /api/recaudaciones/consultar/nextid | — | **FALTA** | igual, `RecaudacionRepository.cs:30`. `inventario-nuevo.tsv` atribuye los tres a `GET api/Recaudaciones/{qr}`: es falso, los recogió por el nombre del campo |
| Validar folio contra concentrado | POST /api/concentrados/validar | — | **FALTA** | `sp_n_ConsultarConcentradoFolio` solo vive dentro de `POST /api/ventas/cancelacion`, que escribe. No hay lectura equivalente |
| Consultar concentrados | POST /api/concentrados/consultar | POST /api/ventas/cancelacion/verificar | COINCIDE (el solape) | `Verificar` es de solo lectura (comprobado en el servicio y el repo antes de llamarlo). Los dos dan el folio de concentrado 35957 para la venta 369643. La pantalla de consulta general de concentrados no existe |
| Parámetros por usuario/equipo | POST /api/parametros/consultar | — | **FALTA** | en el nuevo `sp_n_ConsultaParametros` es un helper privado que rehidrata UN reporte por `@ID` y rechaza lo que no sea Acumuladores y Productos. Legacy consulta por `IDUsuario`+`NombreEquipo` |
| Almacenes por usuario | POST /api/usuarios/almacenes/consultar | GET /api/ventas/pedidos/captura/bootstrap | COINCIDE (no concluyente) | mismo SP, mismo `@IDUsuario`. Reserva anotada en el caso: `BancoPruebas` tiene `IDUsuario` legacy = 0 y el nuevo cae a su respaldo documentado; como en esta base hay un solo almacén activo, los dos caminos dan el mismo renglón y la prueba no los distingue |
| Consultar mac de autenticación | POST /api/login/authtoken/app/mac | POST /api/sesion/login | INTENCIONAL | el nuevo NO usa `sp_n_ConsultaUsuarios`: va por la tabla web con BCrypt, y valida firma y caducidad del JWT (lo que Legacy no hace). No comparable por datos: la tabla `Usuarios` de esta base no tiene columna `CorreoElectronico`, que es lo que exige el login legacy |
| Productos por almacén | POST /api/almacenproductos/consultar | POST /api/ventas/pedidos/captura/productos/buscar | COINCIDE | 3543 renglones idénticos en los 33 campos del DTO de Legacy |
| Consultar pedido | POST /api/pedidos/consultar | GET /api/ventas/pedidos/captura/{idPedido} | COINCIDE | las 4 tablas del SP, campo por campo. `sp_w_ConsultaPedido` no existe en esta base, así que el fallback hace que los dos corran el mismo SP |
| Pedidos faltantes | POST /api/pedidos/faltante/consultar | GET /api/ventas/consulta/pedidos-faltantes | COINCIDE (cobertura débil) | mismos parámetros, los dos vacíos: el SP filtra a `GETDATE()` y la base local llega a marzo 2026. Anotado en el caso |
| Usados a cargo | POST /api/ventasusadoscargos/consultar | POST /api/ventas/usados/consultar → `cargos` | COINCIDE | renglones y totales idénticos |
| Usados crédito actual + catálogo | POST /api/ventasusadoscreditos/actual/catalogo/consultar | …/usados/consultar → `creditos` | COINCIDE | `@Accion=2` en los dos lados |
| Usados crédito anterior | POST /api/ventasusadoscreditos/anterior/consultar | …/usados/consultar → `anteriores` | COINCIDE | `@Accion=1` en los dos lados |

## El bug corregido

**`Infrastructure/Repositories/VentasConsultaRepository.cs`, `EnrichPedidoTimelineAsync`.**

El CTE agrupaba `PARTITION BY Ped.IDVenta ORDER BY Ped.IDPedido DESC`, se quedaba
con `rn = 1` e indexaba el resultado **por venta**. Pero
`POST /api/ventas/consulta/pedidos` devuelve **un renglón por PEDIDO**, y una
venta puede tener varios: a todos los pedidos de la venta se les pintaba la línea
de tiempo del pedido MÁS RECIENTE. Afectaba `FechaImpresoFtm`,
`FechaProcesoFtm` y `FechaAutorizoFtm`.

Venta 369645, pedidos 193308 y 195016:

```
LEG 369645 193308  01-03-2026 14:58   <- la suya
NUE 369645 193308  13-08-2026 14:58   <- MAL, la del otro pedido
```

Pasaba desapercibido cuando la venta tiene un solo pedido, que es el caso normal;
por eso la consulta con estatus 1 salía bien y solo estatus 2 lo delataba.

Arreglo: el SELECT ya no filtra `rn = 1`, devuelve también `IDPedido` y `rn`, y se
llenan dos índices: `timelinePorPedido` siempre y `timelinePorVenta` solo con
`rn = 1`. Al asignar se busca primero por `IDPedido` y el índice por venta queda
como respaldo para la consulta de remisiones, que da un renglón por venta y no
trae `IDPedido`.

Lo comprueba `comparador/casos/grupo-b-consulta-pedidos-autorizados.json`, que
pasó de DIF a OK sin tocar el caso.

## Lo que el backend nuevo arregla de Legacy sin quererlo

Vale la pena que quede escrito: en varios sitios la diferencia era Legacy
perdiendo el dato, y gana la base.

- **Columnas que Legacy tira por un nombre mal escrito.** `Funciones.DataTableToList`
  y `Funciones.GetItem` emparejan **sensibles a mayúsculas**
  (`Legacy/.../Utils/Funciones.cs:123`). De ahí: `IDagente` (el DTO dice
  `IDAgente`) sale 0 en remisiones; `IDDomicilio` (el DTO dice `IDDomilicio`, sin
  la c) sale 0 en pedidos; `CalleNumero` (el DTO dice `CalleConNumero`) sale null
  en domicilios; y `StockMaximo` de productos por almacén sale **siempre 0**
  porque el SP nombra la columna `stockMaximo`. Comprobado en la base: hay
  `[Stock Maximo] = 5` en 236 de 3543 renglones.
- **Fechas nulas convertidas en el año 1.** Los DTO declaran `DateTime` no
  anulable, así que los NULL salen `0001-01-01T00:00:00`: un préstamo vivo se ve
  cancelado en el año 1 (927 de 1017 préstamos con `[Fecha Cancelacion]` NULL).
- **Contraseñas en claro.** `/api/usuarios/consultar` devuelve `Contrasena`,
  `ContrasenaDesc` y `ContrasenaAutorizacion` sin cifrar. El nuevo no.
- **`RecaudacionService.findAsync` pierde el `IDRecaudacion`** y con él el
  `@IDCaja`, y devuelve TODO. El nuevo parte la consulta en dos ramas y conserva
  los dos.
- **`ConsultaConcentradosService.findConcentradosAsync`** tiene un copy-paste
  roto: el `if` y el `else if` comparan la MISMA condición, así que la rama "por
  folio de venta" es inalcanzable.

## Trampas al escribir los casos (para la siguiente ola)

1. **`clave_normal` del comparador aplasta dos columnas en una.** Legacy manda
   `Usuario` y `Nombre`; las dos normalizan a `nombre` y gana la última del JSON.
   Hay que renombrar la que no se compara a un nombre que `solo_campos` descarte.
2. **Legacy rompe el rango de fechas si no le mandas `FolioInicial: ""`.** El DTO
   nace con `FolioInicial = "0"`, entra a la rama de folio, tira las fechas y
   barre la tabla: la respuesta pasó de 76 KB a **565 MB**. Y espera
   `dd-MM-yyyy` de entrada, mientras el nuevo acepta ISO.
3. **Varios catálogos legacy truenan con `ArgumentNullException`** si no les
   mandas las listas (`IDGrupoCategoriaLst`, `IDCategoriaLst`, `IDMarcaLst`): el
   servicio hace `string.Join` sobre ellas y sobreescribe la cadena de texto.
4. **El nuevo normaliza el parámetro ausente a algo que no es cero.**
   `GET /api/asistencias/periodos` sin `idTipoSueldo` entiende 1 (NOMINA) y
   recorta a `top=52`. Con `IDTipoSueldo=0` sin tope, Legacy da 540 renglones: la
   diferencia es culpa de la pregunta.
5. **Un endpoint llamado `/validar` puede escribir.** El verbo no dice nada; hay
   que leer el SP.
6. **`inventario-nuevo.tsv` atribuye SP de más.** Dice que
   `remisiones/catalogos` usa `sp_n_ConsultaAlmacenes` (no lo usa, devuelve
   `almacenes: []`) y que `GET api/Recaudaciones/{qr}` usa los tres SP de cortes,
   recurrentes y nextid (no los usa: están declarados y muertos).

## Suelto que conviene atender, fuera de este lote

`Controllers/RecaudacionesController.cs` **no tiene `[Authorize]`** en ningún
nivel y `Program.cs:259` es `AddAuthorization()` sin `FallbackPolicy`: todo el
controlador es anónimo. Comprobado sin token: `GET /api/Recaudaciones` responde
`200 ["value1","value2"]`. Que el QR sea anónimo puede ser deliberado (lo escanea
quien no inició sesión), pero los andamios `Get()`, `Post()`, `Put()`, `Delete()`
del scaffold son código muerto expuesto.

## Datos que usan los casos

Si se refresca la base local hay que mover estos ids: periodo **9521** (semana 10
de 2026), empleado **4199**, préstamo **9005**, almacén **1** (ACUM TAURO, 3543
productos), pedido **195033**, venta **369614** / folio 135317, venta **369643** /
folio 135346, venta **369645** (dos pedidos, la que delata el bug), venta
**370852** para bancos. Las ventas 370850 y 370851 están canceladas y el nuevo
las rechaza con 409.
