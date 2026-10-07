# Auditoría independiente de la cobertura de la migración

**Fecha:** 2026-10-06 · **Auditor:** agente distinto del que implementó · **Modo:** sólo lectura
(no se tocó `Legacy/`, ni la base, ni el código del backend nuevo; no hay commits)

## Veredicto en una línea

El registro **no miente sobre lo que dice que existe**: ninguna fila marcada `hecho` resultó
apuntar a un endpoint inexistente, y las 112 comparaciones del banco pasan de verdad contra
Legacy. Pero el número de cobertura **está inflado por tres vías distintas**, y hay un hallazgo
que el banco de comparación es estructuralmente incapaz de ver:
**68 de las 117 capacidades «hechas» devuelven 403 a cualquier usuario que no sea SuperAdmin**,
porque están protegidas con permisos que no existen en el catálogo de la base.

---

## 0. Qué se ejecutó de verdad (para que esto sea auditable)

| Comprobación | Resultado |
|---|---|
| Fecha de operación antes y después | `2026-10-06` = hoy → `sp_n_ConsultaConstantes` **no** dispara el cierre del día. Seguro para comparar. |
| `comparar.py casos/` (112 casos) | **112 coinciden, 0 con diferencias.** No se ejecutó nada marcado `escribe = si`. |
| Backend nuevo | `http://127.0.0.1:5055`, `environment = Production` (lo dice `/whoami`) |
| Legacy | `http://localhost:63479`, responde 200 |
| Base | `LAP-JUAND` / `Produccion_svr` |

**La primera corrida se descartó, y conviene saber por qué.** La primera pasada dio 48 `DIF`,
todos con `nuevo 404`, y todos en los casos `ola2-*`/`ola3-*`. No era un fallo del backend:
había **dos procesos `ISL_Service` vivos a la vez** (uno arrancado a las 16:31 y otro a las
17:50) y el puerto 5055 cambió de dueño a media corrida. La corrida válida es la segunda.

> **Defecto del banco:** `comparar.py` vigila que la fecha de operación no se mueva, pero no
> vigila que el backend nuevo sea el mismo proceso al principio y al final. Una corrida puede
> salir roja entera por esto y hacer perder una tarde. Debería anotar la versión/arranque del
> backend al empezar y al terminar, igual que hace con la fecha.

---

## 1. La muestra, fila por fila

Se revisaron **las 117 filas** marcadas `hecho` (103) o `hecho-sin-comparar` (14) — no una
muestra de 25, porque las comprobaciones se pudieron automatizar y entonces no hay excusa para
mirar menos. Para cada fila:

1. **¿Existe la ruta nueva?** Se extrajeron las 338 rutas reales del backend nuevo parseando
   `[Route]` + `[HttpGet/Post/Put/Delete]` de `Controllers/`, y se cruzaron con `ubicacion_nueva`.
   → **Las 117 existen.** Cero filas apuntan a una ruta inventada.
2. **¿Usa los mismos stored procedures?** Se indexaron los 278 literales de SP del backend nuevo
   (`"sp_…"` y `"dbo.sp_…"`, descartando las menciones en comentarios — hay 29) y se cruzaron con
   la columna `stored_procedures`. → **116 de 117 coinciden.** La única excepción es
   `Consultar empleados`, y el registro ya la declara deliberada (ver §1.3).
3. **¿El permiso es coherente?** Se construyó el mapa ruta → permiso desde el código y se cruzó
   contra `WPermiso` de la base. → **aquí está el hallazgo gordo** (§2).
4. **¿Pasa su caso?** Se corrieron los 112 casos. → todos pasan, pero **27 no prueban nada o
   casi nada** (§1.2).

### 1.1 Resumen de veredictos

| Veredicto | Filas | Por qué |
|---|---|---|
| **CONFIRMADO** | 63 | caso corrido hoy contra Legacy, con renglones reales, coincide |
| **CONFIRMADO** (caso existe, el registro no lo nombra) | 13 | la capacidad viaja dentro de un payload mayor y `ubicacion_nueva` quedó abreviada con «…» |
| **CONFIRMADO (débil)** | 9 | coincide, pero sobre **un solo renglón** |
| **A MEDIAS** | 18 | el caso compara **lista vacía contra lista vacía** |
| **A MEDIAS** | 14 | nunca se comparó (`hecho-sin-comparar`, Legacy no responde) |
| **NO ESTÁ** | **0** | — |
| Total | 117 | |

**Filas abiertamente falsas: 0.** Ninguna fila dice `hecho` sobre un endpoint que no exista, y
ninguna reclama un stored procedure que no use. Eso hay que concedérselo al registro, y es más
de lo que esperaba encontrar.

### 1.2 El problema real: 18 `OK` que no prueban nada

Es el hallazgo más valioso sobre el registro. Dieciocho casos mandan la misma pregunta a los dos
backends, **los dos contestan una lista vacía**, el comparador no encuentra diferencias y escribe
`OK`. En el tablero eso queda **indistinguible de una comparación de 3.421 renglones**. Un
endpoint que hiciera `return new List<>()` sin tocar la base **pasaría exactamente igual**.

Los separé en tres grupos tirando de la base:

#### (a) Parámetros mal elegidos — hay datos, el caso no los pide. Los más graves.

| Caso | Lo que pide | Lo que hay en la base | Re-probado por mí |
|---|---|---|---|
| `ola3-cliente-log` | `IDCliente = 1` → 0 renglones | `[Clientes Log]` tiene **358 renglones** (el cliente 20323 tiene 8) | con `IDCliente=20323`: **8 renglones × 7 campos, coinciden** |
| `ola3-usuario-fechas` | `IDUsuario = 9` → 0 renglones | `UsuariosFechas` tiene filas para el **usuario 40** | con `IDUsuario=40`: **2 renglones, coinciden** |
| `ola3-huellas-usuarios` | sólo la rama `origen=usuario`, y `UsuariosHuellas` está vacía | `EmpleadosHuellas` tiene **409 renglones**; la rama `origen=empleado` jamás se probó | con `origen=empleado&todas=true`: **409 renglones, 0 diferencias** |

Los tres endpoints están **bien**. Lo que estaba mal era la prueba. Pero eso sólo se sabe
después de re-apuntarla: antes de hacerlo, los tres `OK` del tablero eran humo.

Detalle que vale la pena: al re-apuntar `ola3-usuario-fechas` a datos reales, la única diferencia
que salió fue `FechaFtm` — que el propio caso ya declara como diferencia intencional, pero que
**su lista `ignorar` no cubre**, porque con el resultado vacío nunca tuvo que cubrirla. Es la
firma de un caso que nunca vio un renglón.

#### (b) Tabla genuinamente vacía en esta base

Verificado con `COUNT(*)`: `Catalogo Galleros` = 0, `MC_Residentes` = 0, `MC_Proveedores` = 0,
`MC_ContratosConceptos` = 0, `ProveedoresApoderados` = 0, `ProveedoresDocumentos` = 0,
`ProveedoresDomicilios` = 0, `UsuariosHuellas` = 0. Son **10 filas**: los 5 casos de obra, los 3
de proveedor, galleros y huellas-usuario. Aquí la prueba no puede hacer más con esta base.

Pero el `OK` sigue sobrando. `ola-3-resto-lecturas.md` **sí lo dice** («tabla vacía», en 12
sitios); el `registro.tsv` —que según el `README.md` es «el tablero de la migración»— **no**:
pone `OK` a secas. Si el tablero es la fuente de verdad, **el tablero miente por omisión**. Hace
falta un valor distinto, del tipo `OK-vacio` o una columna `renglones_comparados`.

#### (c) No verificable con los datos locales

`ola3-asistencia-diaria` manda cuerpo vacío a Legacy y sin query al nuevo. Probé con las fechas
reales que existen (`2026-02-28`, `2026-02-27`, en los dos formatos) y **el propio stored
procedure devuelve 0 renglones**. Queda **sin verificar**. Para cerrarlo hace falta una base con
asistencias atadas a un `PeriodosTipoSueldoDias` de una fecha consultable — con la base local
(que llega a marzo 2026) no se puede.

Y 9 casos más comparan **un solo renglón**: `catalogo-colonias`, `grupo-a-almacenes`,
`grupo-b-ficha-cliente`, `grupo-b-domicilios-cliente`, `grupo-b-pendientes-imprimir`,
`grupo-d-usuario-almacenes`, `ola2-cobranza-validar-folio`, `ola2-concentrado-validar-folio`,
`ola3-catalogo-empresas-series`. No es humo, pero tampoco es cobertura.

### 1.3 Lo que sí resiste el escrutinio

- **El SP distinto de `Consultar empleados` está bien justificado.** El nuevo usa
  `sp_n_ConsultaEmpleadosTipoSueldo` en vez de `sp_n_ConsultaEmpleados`, y el caso explica por
  qué: el SP de Legacy no trae puesto ni sueldo, y **concatena `@Nombre` dentro de un `LIKE` sin
  escapar** (inyección SQL). Comprobé además que el parámetro `filtro` del endpoint nuevo **sí se
  usa** (`Application/Services/EmpleadosService.cs:19`, `AplicarFiltro` en memoria): no es un
  parámetro recibido y tirado, es el filtro movido fuera de SQL a propósito.
- **Los 13 «caso-no-trazable» tienen caso.** `repartidores`, `tipos de gasto`, `empresas`,
  `marcas`, `categorías`, `proveedores`, `usuarios`, `crédito disponible`, `domicilios`, los dos
  de usados y `login` viajan dentro de un payload mayor; sus casos son `grupo-a-*`, `grupo-b-*` y
  `grupo-e-*`, y pasan. El defecto es **documental**: `ubicacion_nueva` dice
  `…/remisiones/catalogos → repartidores` en vez de la ruta, así que no se puede cruzar
  automáticamente con el código. Cuesta poco arreglarlo y vale mucho.
- **Cero filas reclaman una comparación que no dejó rastro.** Busqué filas con
  `metodo_validacion = comparado contra Legacy` y `resultado = OK` sin archivo de caso: **no hay
  ninguna**. Las 14 sin caso están etiquetadas `hecho-sin-comparar` / `Legacy no responde`, que es
  honesto.
- **La autenticación del backend nuevo sí muerde.** Probé 11 rutas `hecho` sin token: **las 11
  devuelven 401**. Es una mejora real sobre Legacy, donde cualquier cadena no vacía en
  `Authorization` pasa.
- **La codificación no está rota.** Perseguí un `MODIFICACIÃ“N` que parecía mojibake del backend
  nuevo: los dos lados mandan los mismos bytes UTF-8 (`c3 93` = `Ó`). Falsa alarma mía, la anoto
  para que nadie la vuelva a perseguir.

### 1.4 Tabla completa de las 117 filas

Ordenada por fuerza de la evidencia: primero lo flojo.

| # | Capacidad | Legacy | Nuevo | Permiso | ¿Alcanzable? | SP igual | Caso | Veredicto | Evidencia |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Consultar asistencias por periodo | `/api/empleadosperiodoasistencias/diaria/consultar` | `/api/asistencias/diaria` | `asistencias.ver,empleados.sueldo.ver` | sí | si | `ola3-asistencia-diaria` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 2 | Consultar catalogo de galleros | `/api/catgalleros/consultar` | `/api/catalogos/galleros` | `catalogos.galleros.ver` | **NO** (403) | si | `catalogo-galleros` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 3 | Consultar conceptos de contrato (MC) | `/api/mccontratos/conceptos/consultar` | `/api/obra/contratos/conceptos` | `obra.contratos.ver` | **NO** (403) | si | `ola3-obra-contrato-conceptos` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 4 | Consultar consulta de cobros | `/api/cobros/centroservicio/consultar` | `/api/ventas/cobros/centro-servicio` | `cobranza.ver` | **NO** (403) | si | `ola2-cobros-centro-servicio` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 5 | Consultar consulta de pagos de venta | `/api/consultaventaspagos/kilos/consultar` | `/api/ventas/cobros/pagos-kilos` | `cobranza.ver` | **NO** (403) | si | `ola2-pagos-kilos` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 6 | Consultar cortes de caja | `/api/cortescs/ventas/canceladas/consultar` | `/api/cortes/centros-servicio/ventas-canceladas` | `cortes.cs.ver` | **NO** (403) | si | `ola3-corte-cs-ventas-canceladas` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 7 | Consultar el detalle de cobros de dinero y usados | `/api/ventascobrosdinerousados/consultar/detalle` | `/api/ventas/usados/consulta/cobros-dinero/detalle` | `ventas.usados` | sí | si | `ola2-usados-cobro-dinero-detalle` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 8 | Consultar el log de clientes | `/api/clientes/consultar/log` | `/api/clientes/{}/log` | `clientes.log.ver` | **NO** (403) | si | `ola3-cliente-log` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 9 | Consultar estimaciones de contrato (MC) | `/api/mccontratos/estimaciones/consultar` | `/api/obra/contratos/estimaciones` | `obra.contratos.ver` | **NO** (403) | si | `ola3-obra-contrato-estimaciones` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 10 | Consultar fechas permitidas por usuario | `/api/usuarios/fechas/consultar` | `/api/usuarios/fechas` | `usuarios.ver` | sí | si | `ola3-usuario-fechas` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 11 | Consultar grupos de contrato (MC) | `/api/mccontratos/grupos/consultar` | `/api/obra/contratos/grupos` | `obra.contratos.ver` | **NO** (403) | si | `ola3-obra-contrato-grupos` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 12 | Consultar huellas digitales | `/api/huellas/consultar` | `/api/huellas` | `empleados.huellas` | sí | si | `ola3-huellas-usuarios` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 13 | Consultar pedidos | `/api/pedidos/faltante/consultar` | `/api/ventas/consulta/pedidos-faltantes` | `-` | sí (sin permiso fino) | si | `grupo-e-pedidos-faltantes` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 14 | Consultar proveedores | `/api/mcproveedores/consultar` | `/api/obra/proveedores` | `obra.proveedores.ver` | **NO** (403) | si | `ola3-obra-proveedores` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 15 | Consultar proveedores | `/api/proveedores/apoderados/consultar` | `/api/proveedores/apoderados` | `proveedores.ver` | sí | si | `ola3-proveedor-apoderados` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 16 | Consultar proveedores | `/api/proveedores/domicilios/consultar` | `/api/proveedores/domicilios` | `proveedores.ver` | sí | si | `ola3-proveedor-domicilios` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 17 | Consultar proveedores | `/api/proveedores/documentos/consultar` | `/api/proveedores/documentos` | `proveedores.ver` | sí | si | `ola3-proveedor-documentos` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 18 | Consultar residentes (MC) | `/api/mcresidentes/consultar` | `/api/obra/catalogos/residentes` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-residentes` | **A MEDIAS** | el caso compara LISTA VACÍA contra LISTA VACÍA: el `OK` no prueba nada |
| 19 | Consultar catalogo de cajas (aviacion) | `/api/catcajas/consultar` | `/api/catalogos/cajas` | `catalogos.recaudaciones.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 20 | Consultar catalogo de conceptos (aviacion) | `/api/catconceptos/consultar` | `/api/catalogos/conceptos` | `catalogos.recaudaciones.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 21 | Consultar catalogo de documentos | `/api/catdocumento/consultar` | `/api/catalogos/documentos` | `catalogos.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 22 | Consultar catalogo de economicos (aviacion) | `/api/cateconomicos/auxiliares/consultar` | `/api/catalogos/economicos-auxiliares` | `catalogos.recaudaciones.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 23 | Consultar catalogo de unidades territoriales | `/api/catunidadterritorial/consultar` | `/api/catalogos/unidades-territoriales` | `catalogos.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 24 | Consultar consulta de cobros | `/api/cobrosdetalle/consultar` | `/api/ventas/cobros/folios-usados` | `cobranza.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 25 | Consultar consulta de cobros | `/api/cobrospagos/consultar` | `/api/ventas/cobros/pagos` | `cobranza.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 26 | Consultar cortes de caja | `/api/cortescs/cobros/consultar` | `/api/cortes/centros-servicio/cobros` | `cortes.cs.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 27 | Consultar el detalle de excedentes de usados | `/api/ventasusadosexcedentes/consultar/detalle` | `/api/ventas/usados/consulta/excedentes/detalle` | `ventas.usados` | sí | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 28 | Consultar el siguiente numero de cuenta de catalogo de conceptos (aviacion) | `/api/catconceptos/consultar/nextnumcuenta` | `/api/catalogos/conceptos/{}/siguiente-numero-cuenta` | `catalogos.recaudaciones.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 29 | Consultar formas y procesos (permisos) | `/api/catformas/consultar` | `/api/catalogos/formas` | `catalogos.formas.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 30 | Consultar formas y procesos (permisos) | `/api/catformasprocesos/consultar` | `/api/catalogos/formas-procesos` | `catalogos.formas.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 31 | Consultar gerentes (MC) | `/api/mcgerentes/consultar` | `/api/obra/catalogos/gerentes` | `obra.catalogos.ver` | **NO** (403) | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 32 | Consultar ventas/remisiones | `/api/ventasusadoscreditoslog/consultar` | `/api/ventas/usados/consulta/creditos/{}/log` | `ventas.usados` | sí | si | — | **A MEDIAS** | nunca se comparó (Legacy no responde: base de Aviación) |
| 33 | Consultar almacenes por usuario | `/api/usuarios/almacenes/consultar` | `/api/ventas/pedidos/captura/bootstrap` | `ventas.pedidos.crear` | sí | si | `grupo-a-tipos-usado`, `grupo-d-usuario-almacenes` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 34 | Consultar catalogo de almacenes | `/api/catalmacenes/consultar` | `/api/reportes/ventas/acumuladores-productos/catalogos` | `reportes.ver_modulo` | sí | si | `grupo-a-almacenes`, `grupo-a-categorias`, `grupo-a-empresas`, `grupo-a-grupos-categorias`, `grupo-a-marcas` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 35 | Consultar catalogo de colonias | `/api/catcolonias/consultar` | `/api/catalogos/colonias` | `catalogos.ver` | **NO** (403) | si | `catalogo-colonias` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 36 | Consultar catalogo de tipos de usado | `/api/cattipousado/consultar` | `/api/ventas/pedidos/captura/bootstrap` | `ventas.pedidos.crear` | sí | si | `grupo-a-tipos-usado`, `grupo-d-usuario-almacenes` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 37 | Consultar clientes | `/api/clientes/consultar` | `/api/ventas/pedidos/captura/clientes/buscar` | `ventas.pedidos.crear` | sí | si | `grupo-b-credito-disponible`, `grupo-b-domicilios-cliente`, `grupo-b-ficha-cliente` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 38 | Consultar consulta de cobranzas | `/api/cobranzas/validar` | `/api/ventas/cobranzas/validar-folio` | `cobranza.ver` | **NO** (403) | si | `ola2-cobranza-validar-folio` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 39 | Consultar consulta de concentrados | `/api/concentrados/validar` | `/api/ventas/concentrados/validar-folio` | `cobranza.ver` | **NO** (403) | si | `ola2-concentrado-validar-folio` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 40 | Consultar consulta de pedidos | `/api/consultaventaspedido/pendientes/imprimir/consultar` | `/api/ventas/consulta/pendientes-imprimir` | `-` | sí (sin permiso fino) | si | `grupo-b-pendientes-imprimir` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 41 | Consultar series por empresa | `/api/empresas/series/consultar` | `/api/catalogos/empresas-series` | `catalogos.ver` | **NO** (403) | si | `ola3-catalogo-empresas-series` | **CONFIRMADO (débil)** | caso corrido hoy, coincide, pero sólo 1 renglón |
| 42 | Consultar catalogo de empresas | `/api/empresas/consultar` | `…/acumuladores-productos/catalogos → empresas` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 43 | Consultar catalogo de grupos de categorias | `/api/catgruposcategorias/consultar` | `…/catalogos → categorias` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 44 | Consultar catalogo de repartidores | `/api/catrepartidores/consultar` | `…/remisiones/catalogos → repartidores` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 45 | Consultar catalogo de tipos de gasto | `/api/cattipogasto/consultar` | `…/remisiones/catalogos → tiposGasto` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 46 | Consultar categorias | `/api/categorias/consultar` | `…/catalogos?idGrupoCategoria=1 → subcategorias` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 47 | Consultar credito disponible del cliente | `/api/creditodisponible/consultar` | `(dentro de clientes/buscar)` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 48 | Consultar domicilios | `/api/domicilios/consultar` | `(dentro de clientes/buscar)` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 49 | Consultar mac de autenticacion | `/api/login/authtoken/app/mac` | `/api/sesion/login` | `-` | sí (sin permiso fino) | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 50 | Consultar marcas | `/api/catgruposcategorias/marcas/consultar` | `…/catalogos?idGrupoCategoria=1&idCategorias=1 → marcas` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 51 | Consultar proveedores | `/api/proveedores/consultar` | `…/remisiones/catalogos → proveedores` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 52 | Consultar usuarios | `/api/usuarios/consultar` | `…/remisiones/catalogos → usuarios` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 53 | Consultar ventas/remisiones | `/api/ventasusadoscreditos/actual/catalogo/consultar` | `…/usados/consultar → creditos` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 54 | Consultar ventas/remisiones | `/api/ventasusadoscreditos/anterior/consultar` | `…/usados/consultar → anteriores` | `-` | n/d | si | — | **CONFIRMADO** | viaja dentro de un payload mayor; `ubicacion_nueva` abreviada con «…», el caso existe pero el registro no lo nombra |
| 55 | Consultar asistencias por periodo | `/api/empleadosperiodoasistencias/consultar` | `/api/asistencias` | `asistencias.ver` | sí | si | `grupo-c-asistencias-rejilla` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 56 | Consultar bonificaciones de clientes | `/api/clientes/bonificacion/consultar` | `/api/clientes/{}/bonificaciones` | `clientes.ver` | **NO** (403) | si | `ola3-cliente-bonificaciones` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 57 | Consultar catalogo SAT de formas de pago | `/api/CatFEFormasPago/consultar` | `/api/catalogos/fe/formas-pago` | `catalogos.fe.ver` | **NO** (403) | si | `ola3-catalogo-fe-formas-pago` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 58 | Consultar catalogo SAT de metodos de pago | `/api/CatFEMetodosPago/consultar` | `/api/catalogos/fe/metodos-pago` | `catalogos.fe.ver` | **NO** (403) | si | `ola3-catalogo-fe-metodos-pago` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 59 | Consultar catalogo SAT de motivos de cancelacion | `/api/CatFEMotivosCancelacion/consultar` | `/api/catalogos/fe/motivos-cancelacion` | `catalogos.fe.ver` | **NO** (403) | si | `ola3-catalogo-fe-motivos-cancelacion` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 60 | Consultar catalogo SAT de regimenes fiscales | `/api/catFERegimenesFiscales/consultar` | `/api/catalogos/fe/regimenes-fiscales` | `catalogos.fe.ver` | **NO** (403) | si | `catalogo-fe-regimenes-fiscales` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 61 | Consultar catalogo SAT de relacion de facturas | `/api/catFERelacionFacturas/consultar` | `/api/catalogos/fe/relaciones-factura` | `catalogos.fe.ver` | **NO** (403) | si | `catalogo-fe-relaciones-factura` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 62 | Consultar catalogo SAT de usos de CFDI | `/api/catFEUsosCFDI/consultar` | `/api/catalogos/fe/usos-cfdi` | `catalogos.fe.ver` | **NO** (403) | si | `catalogo-fe-usos-cfdi` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 63 | Consultar catalogo de agentes | `/api/catagentes/consultar` | `/api/ventas/consulta/catalogos` | `-` | sí (sin permiso fino) | si | `catalogo-agentes` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 64 | Consultar catalogo de autos | `/api/catautos/consultar` | `/api/reportes/ventas/remisiones/catalogos` | `reportes.ver_modulo` | sí | si | `grupo-a-autos`, `grupo-a-proveedores`, `grupo-a-repartidores`, `grupo-a-tipos-gasto`, `grupo-a-usuarios` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 65 | Consultar catalogo de bancos | `/api/catbancos/consultar` | `/api/ventas/multipago/pantalla` | `ventas.pagos.multi` | sí | si | `grupo-a-bancos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 66 | Consultar catalogo de centros de servicio | `/api/catcentroservicio/consultar` | `/api/catalogos/centros-servicio` | `catalogos.ver` | **NO** (403) | si | `catalogo-centros-servicio` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 67 | Consultar catalogo de ciudades | `/api/catciudades/consultar` | `/api/catalogos/ciudades` | `catalogos.ver` | **NO** (403) | si | `catalogo-ciudades` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 68 | Consultar catalogo de condiciones de credito | `/api/CatCondicionesCredito/consultar` | `/api/catalogos/condiciones-credito` | `catalogos.ver` | **NO** (403) | si | `ola3-catalogo-condiciones-credito` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 69 | Consultar catalogo de estados | `/api/catestados/consultar` | `/api/catalogos/estados` | `catalogos.ver` | **NO** (403) | si | `catalogo-estados` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 70 | Consultar catalogo de grupos de cliente | `/api/catGruposCliente/consultar` | `/api/catalogos/grupos-cliente` | `catalogos.ver` | **NO** (403) | si | `catalogo-grupos-cliente` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 71 | Consultar catalogo de municipios | `/api/catmunicipios/consultar` | `/api/catalogos/municipios` | `catalogos.ver` | **NO** (403) | si | `catalogo-municipios` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 72 | Consultar catalogo de puestos | `/api/catpuestos/consultar` | `/api/catalogos/puestos` | `catalogos.ver` | **NO** (403) | si | `catalogo-puestos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 73 | Consultar catalogo de tipos de documento | `/api/catTiposDocumentos/consultar` | `/api/catalogos/tipos-documento` | `catalogos.ver` | **NO** (403) | si | `catalogo-tipos-documento` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 74 | Consultar catalogo de tipos de nota de credito | `/api/cattiponotascredito/consultar` | `/api/catalogos/tipos-nota-credito` | `catalogos.ver` | **NO** (403) | si | `catalogo-tipos-nota-credito` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 75 | Consultar catalogo de tipos de pago | `/api/cattipopago/consultar` | `/api/catalogos/tipos-pago` | `catalogos.ver` | **NO** (403) | si | `catalogo-tipos-pago` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 76 | Consultar catalogo de tipos de recordatorio | `/api/cattiporecordatorio/consultar` | `/api/catalogos/tipos-recordatorio` | `catalogos.ver` | **NO** (403) | si | `catalogo-tipos-recordatorio` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 77 | Consultar catalogo de tipos de sueldo | `/api/cattiposueldo/consultar` | `/api/catalogos/tipos-sueldo` | `catalogos.ver` | **NO** (403) | si | `catalogo-tipos-sueldo` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 78 | Consultar catalogo de unidades de medida | `/api/catunidadesmedida/consultar` | `/api/catalogos/unidades-medida` | `catalogos.ver` | **NO** (403) | si | `catalogo-unidades-medida` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 79 | Consultar centros de servicio del cliente | `/api/clientes/centroservicio/consultar` | `/api/clientes/{}/centros-servicio` | `clientes.ver` | **NO** (403) | si | `ola3-cliente-centros-servicio` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 80 | Consultar constantes del sistema | `/api/constantes/consultar` | `/api/ventas/consulta/catalogos` | `-` | sí (sin permiso fino) | si | `catalogo-agentes` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 81 | Consultar consulta de cobranzas | `/api/cobranzasdetalle/consultar` | `/api/ventas/cobranzas/{}/detalle` | `cobranza.ver` | **NO** (403) | si | `ola2-cobranza-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 82 | Consultar consulta de concentrados | `/api/concentrados/consultar` | `/api/ventas/cancelacion/verificar` | `ventas.cancelar` | sí | si | `grupo-d-concentrado-de-folio` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 83 | Consultar consulta de concentrados | `/api/concentradosdetalle/consultar` | `/api/ventas/concentrados/{}/detalle` | `cobranza.ver` | **NO** (403) | si | `ola2-concentrado-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 84 | Consultar consulta de pedidos | `/api/consultaventaspedido/consultar` | `/api/ventas/consulta/pedidos` | `-` | sí (sin permiso fino) | si | `grupo-b-consulta-pedidos-autorizados`, `grupo-b-consulta-pedidos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 85 | Consultar consulta de ventas/remisiones | `/api/consultaventas/consultar` | `/api/ventas/consulta/remisiones` | `-` | sí (sin permiso fino) | si | `grupo-b-consulta-remisiones` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 86 | Consultar datos de facturacion del cliente | `/api/clientes/facturacion/consultar` | `/api/clientes/facturacion` | `clientes.facturacion.ver` | **NO** (403) | si | `ola3-cliente-facturacion` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 87 | Consultar detalle de descuentos | `/api/descuentosdetalle/consultar` | `/api/ventas/descuentos/detalle` | `descuentos.ver` | **NO** (403) | si | `ola2-descuentos-detalle-marcas`, `ola2-descuentos-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 88 | Consultar el actual de cortes de caja | `/api/cortes/consultar/actual` | `/api/cortes/siguiente-fecha` | `cortes.ver` | **NO** (403) | si | `ola3-corte-siguiente-fecha` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 89 | Consultar el detalle de cobros de usados | `/api/ventascobrosusados/consultar/detalle` | `/api/ventas/usados/consulta/cobros/detalle` | `ventas.usados` | sí | si | `ola2-usados-cobro-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 90 | Consultar el detalle de cortes de caja | `/api/cortes/consultar/detalle` | `/api/cortes/{}/detalle` | `cortes.ver` | **NO** (403) | si | `ola3-corte-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 91 | Consultar el detalle de movimientos de usados | `/api/usadosmovimientos/consultar/detalle` | `/api/ventas/usados/consulta/movimientos/{}/detalle` | `ventas.usados` | sí | si | `ola2-usados-movimiento-detalle` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 92 | Consultar el log de detalle de descuentos | `/api/descuentosdetalle/consultar/log` | `/api/ventas/descuentos/detalle/log` | `descuentos.ver` | **NO** (403) | si | `ola2-descuentos-detalle-log` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 93 | Consultar el siguiente ID de usuarios | `/api/usuarios/consultar/nextid` | `/api/usuarios/siguiente-numero` | `usuarios.crear` | sí | si | `ola3-usuario-siguiente-numero` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 94 | Consultar empleados | `/api/empleados/consultar` | `/api/empleados` | `empleados.crear,empleados.sueldo.ver,empleados.ver` | sí | NO | `grupo-c-empleados-consultar`, `grupo-c-empleados-tiposueldo` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 95 | Consultar folios de venta | `/api/ventasfolios/sinasignar/consultar` | `/api/ventas/folios-sin-asignar` | `cobranza.ver` | **NO** (403) | si | `ola2-folios-sin-asignar` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 96 | Consultar los disponibles de catalogo de agentes | `/api/catagentes/disponible` | `/api/catalogos/agentes-disponibles` | `catalogos.ver` | **NO** (403) | si | `catalogo-agentes-disponibles` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 97 | Consultar marcas | `/api/catgruposcategorias/marcas/relacionar/consultar` | `/api/catalogos/grupos-categoria/marcas` | `catalogos.marcas.ver` | **NO** (403) | si | `catalogo-grupo-categorias-marcas` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 98 | Consultar marcas | `/api/marcas/consultar` | `/api/catalogos/marcas` | `catalogos.marcas.ver` | **NO** (403) | si | `ola3-catalogo-marcas` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 99 | Consultar pedidos | `/api/pedidos/consultar` | `/api/ventas/pedidos/captura/{}` | `ventas.pedidos.crear` | sí | si | `grupo-e-pedido-cascos-cargo`, `grupo-e-pedido-cascos-credito`, `grupo-e-pedido-detalles`, `grupo-e-pedido-totales` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 100 | Consultar periodos de nomina | `/api/periodostiposueldo/consultar` | `/api/asistencias/periodos` | `asistencias.ver` | sí | si | `grupo-c-periodos-nomina` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 101 | Consultar periodos de nomina | `/api/periodostiposueldodias/consultar` | `/api/asistencias/periodos/{}/dias` | `asistencias.ver` | sí | si | `grupo-c-periodo-dias` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 102 | Consultar prestamos a empleados | `/api/empleadosprestamo/consultar` | `/api/prestamos` | `prestamos.crear,prestamos.ver` | sí | si | `grupo-c-prestamos-pendientes`, `grupo-c-prestamos`, `grupo-c-prestamo-por-id` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 103 | Consultar productos | `/api/productos/consultar` | `/api/productos` | `productos.ver` | **NO** (403) | si | `ola3-productos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 104 | Consultar productos por almacen | `/api/almacenproductos/consultar` | `/api/ventas/pedidos/captura/productos/buscar` | `ventas.pedidos.crear` | sí | si | `grupo-e-almacen-productos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 105 | Consultar puestos (MC) | `/api/mcpuestos/consultar` | `/api/obra/catalogos/puestos` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-puestos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 106 | Consultar relacion categoria-marca | `/api/categoriamarcas/consultar` | `/api/catalogos/categorias/{}/marcas` | `catalogos.marcas.ver` | **NO** (403) | si | `catalogo-categorias-marcas` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 107 | Consultar saldos de venta | `/api/ventassaldos/consultar` | `/api/ventas/saldos/cliente/{}` | `-` | sí (sin permiso fino) | si | `grupo-b-ventas-saldos-cliente` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 108 | Consultar tipo de sueldo del empleado | `/api/empleadostiposueldo/consultar` | `/api/empleados` | `empleados.crear,empleados.sueldo.ver,empleados.ver` | sí | si | `grupo-c-empleados-consultar`, `grupo-c-empleados-tiposueldo`, `grupo-c-empleado-por-id` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 109 | Consultar tipos de contratacion (MC) | `/api/mctiposcontratacion/consultar` | `/api/obra/catalogos/tipos-contratacion` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-tipos-contratacion` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 110 | Consultar tipos de documento (MC) | `/api/mctiposdocumento/consultar` | `/api/obra/catalogos/tipos-documento` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-tipos-documento` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 111 | Consultar tipos de domicilio | `/api/tiposdomicilio/consultar` | `/api/catalogos/tipos-domicilio` | `catalogos.ver` | **NO** (403) | si | `ola3-catalogo-tipos-domicilio` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 112 | Consultar tipos de obra (MC) | `/api/mctiposobra/consultar` | `/api/obra/catalogos/tipos-obra` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-tipos-obra` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 113 | Consultar tipos de recurso (MC) | `/api/mctiposrecurso/consultar` | `/api/obra/catalogos/tipos-recurso` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-tipos-recurso` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 114 | Consultar unidades de medida (MC) | `/api/mcunidadesmedida/consultar` | `/api/obra/catalogos/unidades-medida` | `obra.catalogos.ver` | **NO** (403) | si | `ola3-obra-unidades-medida` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 115 | Consultar usuarios | `/api/usuarios/formas/procesos/consultar` | `/api/usuarios/formas-procesos` | `permisosweb.bootstrap` | sí | si | `ola3-usuario-formas-procesos` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 116 | Consultar ventas/remisiones | `/api/ventasusadoscargos/consultar` | `/api/ventas/usados/consultar` | `ventas.usados` | sí | si | `grupo-e-usados-cargos-totales`, `grupo-e-usados-cargos`, `grupo-e-usados-credito-actual-catalogo`, `grupo-e-usados-credito-actual-totales`, `grupo-e-usados-credito-anterior` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |
| 117 | Consultar versiones de las aplicaciones | `/api/aplicacionesactualizacion/consultar` | `/api/actualizaciones-aplicacion` | `aplicaciones.actualizaciones.ver` | **NO** (403) | si | `ola3-actualizaciones-aplicacion` | **CONFIRMADO** | caso corrido hoy, coincide con renglones reales |

---

## 2. El hallazgo que el banco no puede ver: 68 endpoints «hechos» dan 403

Los 112 casos se corren con el token de **`BancoPruebas`, que es `SuperAdmin`**. Y
`Infrastructure/Security/PermissionAuthorizationHandler.cs:28-32` hace esto:

```csharp
if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
{
    context.Succeed(requirement);
    return;
}
```

O sea: **el banco de comparación nunca ejecuta la capa de permisos.** Pasa por encima de ella en
los 112 casos. Y debajo hay esto:

| Medida | Valor |
|---|---|
| Claves de permiso usadas en `Controllers/` (policies `perm:…` + `ExigirPermisoAsync`) | **122** |
| Claves que existen en `dbo.WPermiso` (empresa 1) | 201 |
| Claves usadas en el código que **NO existen** en `WPermiso` | **48** |
| De esas 48, cuántas están al menos sembradas en `PermissionCatalogSeeds` | 34 |
| De esas 48, cuántas **no están ni sembradas** → nadie podrá tenerlas nunca | **14** |
| Filas `hecho` cuyo permiso **no existe** en `WPermiso` | **68 de 117** |

### Por qué esto es un 403 y no una curiosidad

1. `WConfiguracionEmpresa` dice `Activo = 1` para la empresa 1 → `PermissionsEnabled = true`.
2. Con `PermissionsEnabled = true`, el handler sólo concede si la clave está en
   `snapshot.Permissions`, y ese snapshot se arma en `LoadEffectivePermissionsAsync` haciendo
   `JOIN` de `WRolPermiso`/`WUsuarioPermiso` **contra `WPermiso`**
   (`PermissionService.cs:640-644, 687-688`). Una clave que no está en `WPermiso` **no puede
   tener fila de concesión**, así que no puede salir del snapshot.
3. La vía de rescate (`IsAllowedByLegacy`) **sólo corre cuando `PermissionsEnabled` es false**
   (`PermissionAuthorizationHandler.cs:58-61`). Y `CanUseLegacyVerAsModuleView` sólo aplica a
   claves que terminan en `.ver_modulo`. Ninguna de las 48 lo hace.
4. Las semillas en código **sólo llegan a `WPermiso` si alguien llama a
   `SyncPermissionCatalogAsync`** (`PermissionService.cs:1437`), que es una acción manual desde la
   pantalla de permisos. `BuildSnapshotAsync` **no la llama**. El propio código lo admite en un
   comentario (`PermissionService.cs:446-448`): *«no se corrió ningún script contra la base;
   entran a WPermiso cuando alguien sincronice el catálogo desde la pantalla de permisos»*.

**Comprobación directa contra la base:**

```sql
SELECT p.Clave, r.Codigo FROM WRolPermiso rp
JOIN WPermiso p ON p.Id=rp.WPermisoId AND p.EmpresaId=rp.EmpresaId
JOIN WRol    r ON r.Id=rp.WRolId      AND r.EmpresaId=rp.EmpresaId
WHERE p.Clave LIKE 'catalogos%' OR p.Clave LIKE 'obra%' OR p.Clave LIKE 'productos%';
-- 0 filas
SELECT COUNT(*) FROM WPermiso
WHERE Clave LIKE 'catalogos%' OR Clave LIKE 'obra%' OR Clave LIKE 'productos%';
-- 0
```

**Cero.** No hay una sola concesión, porque no hay una sola clave. Y hay usuarios reales no
SuperAdmin en `UsuarioWeb`: `NAOLANY`, `ADMIN`, `MARYORIS` (rol `User`), `PRUEBA`, `JAZMIN`
(rol `EMPLEADOS`). Para los cinco, los 68 endpoints son 403 hoy.

### Qué queda roto exactamente

| Familia de permiso | Endpoints `hecho` afectados | Qué se cae |
|---|---|---|
| `catalogos.ver` | 18 | ciudades, estados, municipios, colonias, puestos, grupos-cliente, tipos-pago, tipos-sueldo, tipos-documento, tipos-nota-crédito, tipos-recordatorio, unidades-medida, unidades-territoriales, tipos-domicilio, centros-servicio, condiciones-crédito, empresas-series, agentes-disponibles, documentos |
| `obra.catalogos.ver` / `obra.contratos.ver` / `obra.proveedores.ver` | 12 | **la ola 3 de obra entera** |
| `catalogos.fe.ver` | 6 | los 6 catálogos SAT (formas y métodos de pago, usos CFDI, regímenes, relaciones, motivos de cancelación) |
| `cobranza.ver` | 9 | cobranzas, concentrados, cobros, pagos-kilos, folios sin asignar |
| `catalogos.marcas.ver` | 3 | marcas y categoría-marca |
| `clientes.ver` / `clientes.log.ver` / `clientes.facturacion.ver` | 4 | bonificaciones, centros de servicio, bitácora, facturación |
| `cortes.ver` / `cortes.cs.ver` | 4 | cortes de caja |
| `catalogos.recaudaciones.ver` | 4 | catálogos de Aviación |
| `descuentos.ver` | 2 | detalle y bitácora de descuentos |
| `productos.ver`, `catalogos.galleros.ver`, `catalogos.formas.ver`, `aplicaciones.actualizaciones.ver` | 6 | productos, galleros, formas/procesos, versiones |

Las **14 claves que no están ni sembradas** (ésas no las arregla ningún sync; hacen falta
semillas nuevas): `tarimas.ver`, `tarimas.crear`, `tarimas.editar`, `tarimas.estado.editar`,
`configuracionroltorneo.{ver,crear,editar,activar}`, `dashboardventas.ver`, `checador.ver`,
`checador.huellas`, `checador.reportes`, `usuarios.password.ver`, `app_movil.ventas.usados`.
No son de la migración de Legacy, pero están igual de bloqueadas.

> **Esto no es un bug de código, es un paso de despliegue que falta.** Para 34 de las 48 claves
> basta sincronizar el catálogo desde la pantalla de permisos. Pero mientras no se haga,
> «migrado» significa «migrado para SuperAdmin», y eso no es lo que dice el tablero. **Y el banco
> de comparación no lo puede detectar nunca**, porque corre como SuperAdmin por construcción.
> El banco necesita un segundo token, de un rol normal, aunque sea para un puñado de casos.

### 2.1 Endpoints `hecho` sin permiso fino (8)

Sólo exigen sesión, ningún permiso: `/api/ventas/consulta/catalogos`,
`/api/ventas/consulta/remisiones`, `/api/ventas/consulta/pedidos`,
`/api/ventas/consulta/pendientes-imprimir`, `/api/ventas/consulta/pedidos-faltantes`,
`/api/ventas/saldos/cliente/{id}`, `/api/sesion/login`. Es la pantalla completa de Consultar
Ventas más los saldos de cualquier cliente por id (dato financiero), con la familia `ventas.*`
ya definida en `PermissionService.cs` y sin usar. En `login` es correcto.

---

## 3. Funcionalidad de Legacy que nadie contó

Dos resultados, y el primero va **en contra** de la hipótesis del encargo.

### 3.1 El eje de endpoints está exacto — 488 es 488

Verificado por cuatro caminos independientes (conteo de métodos públicos, conteo de atributos
`[Route]`, diff bidireccional de pares `(controlador, método)`, y comparación de rutas compuestas
resolviendo el grafo de constantes de `Mac3Servicios/Utils/EndPoints.cs`):

| Medida | Disco | TSV | |
|---|---|---|---|
| Clases de controlador con ≥1 acción | 130 | 130 | **igual** |
| Acciones HTTP | 488 | 488 | **igual** |
| Rutas mal resueltas / vacías / con placeholder | — | **0** | |
| Métodos públicos de acción sin inventariar | — | **0** | |

Mérito del parseo, además, porque no era fácil: en Legacy **no existe un solo atributo
`[HttpGet]`/`[HttpPost]`** (el verbo se infiere del prefijo del nombre del método) y las rutas
son constantes concatenadas, no literales. Acertó en 488 de 488.

Dos controladores están en disco y no en el inventario, y **están bien excluidos** porque no
exponen ninguna acción: `Legacy/MacServicios2/Mac3Servicios/Controllers/LogImpresionesWebController.cs`
(su único método está comentado con `////`) y
`Legacy/MacServicios2/Mac3Servicios/Controllers/Pedidos/PedidoUsadosCargoController.cs` (sólo
declara un campo). Son código muerto que sigue registrado en el ruteo.

### 3.2 Donde sí está incompleto

**(a) 27 stored procedures citados en el código Legacy y ausentes de toda fila del inventario.**
Lo verifiqué yo con un barrido propio: **474 SPs en el código**, 448 en la columna
`stored_procedures`. Los 27 huérfanos:

`sp_logimpresionesweb`, `sp_n_actualizar`, `sp_n_cancelarcobrodinerocasco`,
`sp_n_cancelarnuevaventa`, `sp_n_consultaalmacenproductoluro`,
`sp_n_consultaempleadostiposueldorecibotauro`, `sp_n_consultafacturas`, `sp_n_consultagarantias`,
`sp_n_consultaoperadornextid`, `sp_n_consultatablasaviacion`, `sp_n_eliminar_proyecto_documento`,
`sp_n_insertar`, `sp_n_insertarcaja`, `sp_n_insertarpedidodetalleluro`,
`sp_n_insertarsincronizacion`, `sp_n_procesarpedidoluro`,
`sp_n_rptconcentradocobroimprimirmultiple`, `sp_n_rptconcentradofoliosusadosmultiple`,
`sp_n_rptconcentradoimprimirmultiple`, `sp_n_rptconcentradousadosimprimirmultiple`,
`sp_n_rptmccontratosconceptosestimaciones`, `sp_n_rptventasinformacionmultiple`,
`sp_n_ventasdetalleluro`, `sp_rptcompradetalle`, `sp_rptcomprasusadoscargomultiple`,
`sp_rptcomprasusadoscreditomultiple`, `sp_rptventasinformacionmultiple`.

De ellos **13 están en código vivo** (8 alcanzables vía reportes; 4 inalcanzables desde ningún
endpoint) y 14 sólo en líneas comentadas. Ojo con `sp_n_insertar` y `sp_n_actualizar`: parecen
nombres truncados (todos los demás son `sp_n_Insertar<Entidad>`), y si alguien reactiva
`TemplateHtmlService` probablemente revienten.

**(b) `migracion/sp-de-codigo.txt` es circular y no sirve de control.** Lo comprobé: tiene 447
SPs, y el diff «en el archivo pero no en la columna del TSV» es **vacío**. Se generó **desde el
TSV, no desde el código**, a pesar de su nombre. Es **imposible** que detecte un hueco del TSV.
El barrido real del código da 474. Hay que regenerarlo desde el código o borrarlo, porque hoy da
una falsa sensación de verificación.

**(c) 145 payloads de reporte colapsados en 2 filas.** `Models/Service/ReportesServ/ReporteService.cs`
y `ReportesV2Serv/ReporteV2Service.cs` sacan el **nombre del stored procedure de la base** (la
columna `SP` del catálogo que lee `sp_ConsultaReportes`) y lo ejecutan. Ningún parser estático
puede enumerar eso, y el inventario lo reconoce honestamente con el marcador
`?dinamico(columna SP de sp_ConsultaReportes)`. Pero: de los 150 SPs del catálogo, **147 no
aparecen en ninguna fila**. Dos filas del inventario cubren 147 comportamientos distintos. Existe
`reportes-cobertura.tsv` aparte, pero el `registro.tsv` no lo enlaza, así que al contar
cobertura esas 2 filas pesan lo mismo que 2 catálogos de 3 renglones.

**(d) 8 filas con `tipo_token` invertido.** Verificado contra
`Mac3Servicios/Security/TokenValidationHandler.cs:40`: el handler sólo exime las URLs que
contienen `authtoken`, y la única de las 488 que lo contiene es `/api/login/authtoken/app/mac`.
Por tanto las 7 filas marcadas `ninguno` (las 6 de `FacturamaController` y
`/api/ventasfacturas/sendemail`) **sí exigen token**, y `LoginController.PostLoginAppAsync`,
marcada `Todos`, **es el único endpoint anónimo**. Están al revés.

**(e) Un segundo proyecto web, inventariado aparte.** `Legacy/MacReportes2/MacReportes` es
ASP.NET **MVC 5** (no Web API), con 8 acciones públicas en 4 controladores. **No está en
`inventario-legacy.tsv`** — pero sí en `inventario-macreportes.tsv` (10 filas). La cobertura es
correcta; lo que falta es que el `registro.tsv` sume los dos inventarios cuando se habla de
«cobertura de Legacy». Hay además una copia anidada obsoleta en
`MacReportes2/MacReportes/MacReportes/Controllers/` que duplica los 4 controladores: vale
confirmar cuál se despliega.

**(f) 12 archivos de lógica muerta que no hay que portar.** De 170 clases `*Service` de Legacy,
11 no las referencia ningún controlador ni ningún otro servicio (`FacturaService`,
`GarantiaService`, `TemplateHtmlService`, `VentasHtmlService`, `RepV2VentasAcumuladoresService`,
`ReporteInventarioAcumuladoresService`, `ReporteInventarioProductoService`,
`ReporteVentaProductosService`, `ReporteVentasAcumuladoresService`,
`ReporteVentasDescuentosService`, `ReporteVentasDescuentosAjustesService`,
`ReporteVentasUtilidadAcumuladoresAgentesService`), más el repositorio `ConsultaFacturasRepository`.

**(g) Superficie HTTP no-WebApi: ninguna.** Cero `.ashx`, `.svc`, `.asmx`, SignalR,
`IHttpHandler` o `ServiceContract` en `MacServicios2`. La ruta por convención `DefaultApi` de
`App_Start/WebApiConfig.cs` es inerte (las acciones con `[Route]` quedan fuera del ruteo por
convención), pero **no conviene heredarla** en el backend nuevo.

### 3.3 Dos agujeros de seguridad de Legacy que aparecieron de paso

No son de la migración, pero el backend nuevo **no debe heredarlos**, y conviene que estén
escritos:

1. **`TokenValidationHandler` nunca valida el JWT.** Define `LifetimeValidator(...)` y **jamás lo
   invoca**; `TryRetrieveToken` sólo comprueba que exista la cabecera `Authorization`. Cualquier
   cadena no vacía pasa: sin firma, sin caducidad, sin claims. Los 487 endpoints «protegidos» no
   lo están. (`COMO-HABLARLE-A-LEGACY.md` ya documenta la mitad de esto; esta es la otra mitad.)
   **El backend nuevo sí valida** — comprobado: 11 de 11 rutas devuelven 401 sin token.
2. **Los `[AllowAnonymous]` de Legacy son inertes y engañan al que lee.** Están en
   `ComprasController:12`, `LoginController:12`, `ReportesController:14`, `ReportesV2Controller:14`,
   `VentasController:33` y `LogImpresionesWebController:13`, pero el `DelegatingHandler` corre
   **antes** de los filtros de autorización y no mira el atributo.
3. **Credenciales de producción en claro en el repo.** El `Web.config` de `MacServicios2` tiene
   ~40 cadenas de conexión (una viva, el resto comentadas) con `sa` y contraseña, varias
   apuntando a `mactauro.ddns.net`.

---

## 4. Stubs y medias tintas del backend nuevo

### 4.1 Los 44 TODO/FIXME: el 97 % es ruido

| Marcador | Coincidencias | Veredicto |
|---|---|---|
| `TODO` en código fuente | 15 | **RUIDO** — las 15 son la palabra española «TODO/TODOS» en comentarios («TODO lo que escribe pasa por `sp_w_*`»). Ninguna es un marcador de tarea. |
| `FIXME` | **0** | no existe |
| `HACK` | **0** | no existe |
| `NotImplementedException` / `NotSupportedException` en código fuente | **0** | — |
| `NotImplementedException` en `ISL_Service.Tests/` | 56 | **RUIDO** — miembros no usados de dobles de prueba; es el patrón del proyecto |
| «pendiente» | ~70 | **RUIDO** salvo 2 — es vocabulario de dominio: `SaldoPendiente`, `ventas.pendientes.ver`, `"PENDIENTES DE IMPRIMIR"` |
| «temporal» | ~20 | **RUIDO** salvo 1 — `PasswordTemporal`, «tabla temporal», «carpeta temporal» |
| «por implementar», «provisional», «placeholder», «stub», «dummy», «workaround» | **0** | no existen |

**Los 3 pendientes reales:**

| Archivo:línea | Qué |
|---|---|
| `Application/Services/DashboardVentasReportService.cs:85` | `// Detalle (muestra) desactivado temporalmente…` → el reporte de dashboard **siempre** sale sin detalle, y `LoadDetalleAsync` (`:186`) es código muerto |
| `Controllers/VentasUsadosController.cs:29` | el GUARDAR de usados usa el permiso de **lectura** `ventas.usados`; falta `n_Procesos 5085`. Anotado por los autores |
| `Infrastructure/Repositories/VentasPedidoCapturaRepository.cs:297-300, 503-507` | depende de un script de base sin desplegar; mientras no esté, el filtro por agente **no filtra** (falla abierto). Deliberado y documentado |

### 4.2 Un controlador de andamiaje publicado sin candado — lo más grave

`Controllers/RecaudacionesController.cs` **no tiene ni `[Authorize]` de clase**. Verificado en
vivo, **sin token**:

```
GET    /api/Recaudaciones    -> 200  ["value1","value2"]
DELETE /api/Recaudaciones/5  -> 200
```

Es la plantilla de Visual Studio tal cual: `Get()` devuelve datos inventados sin tocar la base
(`:26-30`), y `Post`/`Put`/`Delete` (`:103`, `:109`, `:115`) tienen **cuerpo vacío y devuelven
200**. Un cliente que llame a `DELETE /api/Recaudaciones/5` recibe éxito y no se borra nada. Y
`GET /api/Recaudaciones/{qr}` (`:36`) **desencripta un QR y devuelve datos de recaudación sin
autenticación de ningún tipo**. Hoy las escrituras no escriben, pero el controlador está
publicado sin candado y el GET del QR sí entrega datos.

### 4.3 Dos endpoints de diagnóstico anónimos que filtran infraestructura

Verificado en vivo, sin token:

```
GET /dbcheck -> {"connected":true,"connection":"Main","server":"LAP-JUAND","database":"Produccion_svr"}
GET /whoami  -> {"companyId":"local","environment":"Production","corsAllowedOrigins":"","db":"Main"}
```

`Program.cs:469` y `Program.cs:483`, registrados después de `UseAuthorization` y sin
`RequireAuthorization`. En producción es reconocimiento de infraestructura regalado: nombre del
servidor SQL y de la base.

### 4.4 Escrituras sin permiso fino

| Controlador | Endpoints de escritura sin permiso | Qué permite |
|---|---|---|
| `Controllers/AlmacenCascosController.cs` | `:111` `POST salidas`, `:131` `POST entradas`, `:147` `POST …/kilos`, `:163` `PUT salidas/{id}`, `:179` `PUT entradas/{id}`, `:199` `POST …/cancelar` | sólo `[Authorize]` de clase: **cualquier usuario autenticado, de cualquier rol, puede crear, modificar y cancelar movimientos de casco** |
| `Controllers/TorneosController.cs` | `:54`, `:84`, `:115`, `:133`, `:147`, `:161`, `:173` (incl. `cerrar-vencidos`, masivo) | sólo comprueban que el token traiga un userId |
| `Controllers/TemporadasController.cs` | `:51`, `:79`, `:108`, `:126` | idem |
| `Controllers/RecaudacionesController.cs` | `:103`, `:109`, `:115` | anónimos (§4.2) |

No son de la migración de Legacy (son features nuevas sobre `sp_w_*`), pero están en el mismo
backend y es el mismo modelo de permisos.

### 4.5 `catch` que se tragan el error

De los 176 `catch` del proyecto, 63 no registran ni relanzan. La mayoría son fallbacks acotados y
documentados (`when (ex.Number == 2812)` = SP no desplegado, `== 208/207` = tabla inexistente):
eso es ruido deliberado y lo descarto. Lo que queda:

| Gravedad | Archivo:línea | Qué se traga |
|---|---|---|
| **Alta** | `Application/Services/UserAdminService.cs:73-77` | `catch` **sin filtro y sin log** alrededor de `UpsertUserOverridesAsync` al crear usuario. El bloque existe para dejar al usuario nuevo **sin overrides heredados**; si falla por cualquier otro motivo, el usuario se crea **con permisos residuales** y la API contesta 200. Es de seguridad. |
| Media | `Infrastructure/Repositories/VentasPedidoCapturaRepository.cs:297-301` | `return 0` = **sin filtro por agente**. Un filtro de datos que desaparece en silencio. |
| Media | `Infrastructure/Repositories/AppVersionRepository.cs:75` | `catch (SqlException) { return abierta; }` → el *gate* de versión mínima de la app móvil **falla abierto** sin log: nunca forzaría actualización. |
| Media | `Infrastructure/Security/PermissionService.cs:3053-3058` | `catch { return string.Empty; }` sin filtro, dentro de la validación de «permisos en módulos activos». |
| Baja | `Application/Security/RemisionTicket.cs:118`, `ReporteTicket.cs:125`, `ReporteUsadosTicket.cs:127` | devolver `null` es correcto (el llamador contesta 400), pero **un intento de pase falsificado no deja ningún rastro**. |
| Baja | `Utils/Encryptacion.cs:124`, `Infrastructure/Repositories/CentrosServicioRepository.cs:97`, `Controllers/VentasPedidoCapturaController.cs:231`, `…/ReportesVentasRepository.Rendering.cs:103` | `catch` amplios sin log. El de `:231` (bitácora del pedido) está bien argumentado, pero si la bitácora falla de forma sistemática, «Mis pedidos» del móvil deja de reconocer al dueño y nadie se enteraría. |
| Baja | `Controllers/ProveedoresPagosController.cs:47,64,80,96` | `return StatusCode(500, "… " + ex.Message)`: mensaje de excepción crudo al cliente. |

**Resultado negativo explícito:** ningún `catch` de un controlador devuelve `Ok(...)`; todos
devuelven 4xx/5xx. No hay ningún `return Ok(new { })` vacío en todo `Controllers/`.

### 4.6 Parámetros recibidos y tirados

Sólo 2 sitios (la paginación se comprobó y **funciona** en todos lados: `Page`/`PageSize` en
`DashboardVentasService.cs:114-136`, `Skip`/`Take` en `VentasPedidoCapturaRepository.cs:533`):

- `Application/DTOs/DashboardVentas/DashboardVentasReporteDtos.cs:9-10` —
  `IncluirDetalle` y `MaxDetalleFilas` se reciben del cliente y **no se leen en ninguna parte del
  proyecto**. El cliente puede pedir `IncluirDetalle=true, MaxDetalleFilas=5000` y recibe 200 con
  detalle vacío (consecuencia de §4.1).
- `Controllers/RecaudacionesController.cs:103,109,115` — `value` e `id` nunca se usan (§4.2).

### 4.7 Un literal fijo que hay que verificar

`Infrastructure/Repositories/ReportesVentasRepository.Catalogos.cs:250`:

```csharp
cmd.Parameters.AddWithValue("@IDEmpresaCS", 1);   // sp_n_ConsultaClientes
```

Es el **único** sitio del proyecto donde `@IDEmpresaCS` va con valor distinto de 0 — en
`VentasConsultaRepository.cs:140` el mismo parámetro va en `0` (= sin filtro), y sin comentario
que justifique la diferencia. Como cada empresa tiene su propia base (MacZ es Zaragoza,
Produccion_svr es Tauro), un `1` fijo filtra el catálogo de clientes por un centro de servicio
concreto: si en una de las dos bases ese id no es `1`, el combo de clientes del reporte sale
corto o vacío. **No lo pude cerrar** y hay que comprobarlo contra las dos bases.

**Resultado negativo explícito:** no hay ningún stored procedure con parámetros declarados y no
pasados, ni `AddWithValue` comentado, ni `SqlParameter` creado y no agregado. Los ~80
`AddWithValue("@X", 0)` son la convención «0 = sin filtro» de los `sp_n_*` y varios están
explicados en comentario.

---

## 5. El número honesto de cobertura

### Lo que dice el registro

| `estado` | Filas |
|---|---|
| `hecho` | 103 |
| `hecho-sin-comparar` | 14 |
| `por-verificar` | 17 |
| `parcial` | 136 |
| `falta` | 207 |
| `sin-sp` | 7 |
| `dinamico` | 4 |

→ **117 / 488 = 24,0 %**

### Lo que es

Tres correcciones, de menor a mayor:

**(1) El numerador baja.** De las 117, hay que quitar las que no están respaldadas por una
comparación con datos:

| | Filas |
|---|---|
| `hecho` con comparación sobre renglones reales | 85 |
| …de las cuales sólo 1 renglón | (9 de esas 85) |
| `hecho` con comparación **vacía contra vacía** | −18 (de las que yo rescaté 3 re-probándolas) |
| `hecho-sin-comparar` (nunca se comparó) | −14 |

→ respaldado por comparación real: **85 + 3 = 88** · **88 / 488 = 18,0 %**
Y si se exige más de un renglón: **79 / 488 = 16,2 %**

**(2) El denominador sube.** La superficie real de Legacy no es 488:

| Concepto | Filas |
|---|---|
| Endpoints Web API de `MacServicios2` | 488 (correcto, verificado) |
| Endpoints MVC de `MacReportes2` | +10 (inventariados aparte, no sumados) |
| Payloads de reporte dirigidos por base, hoy colapsados en 2 filas | +145 |
| **Superficie total a migrar** | **≈ 643** |

→ **88 / 643 = 13,7 %**

**(3) La corrección que más duele: cobertura *utilizable*.** De las 117 «hechas», sólo **29**
son alcanzables hoy por un usuario que no sea SuperAdmin. Las otras 68 dan 403.

→ cobertura utilizable por un rol normal: **29 / 488 = 5,9 %** · sobre 643: **4,5 %**

### Resumen

| Métrica | Valor |
|---|---|
| Lo que dice el registro | **24,0 %** |
| Respaldado por comparación con datos reales | **18,0 %** |
| Ídem, sobre la superficie real de Legacy (≈643) | **13,7 %** |
| Alcanzable hoy por un usuario no-SuperAdmin | **5,9 %** |

**¿Está inflado el número del registro?** Sí, pero no por mala fe: por tres decisiones de
medición que se pueden arreglar sin tocar código.

- El 24 % **no es una invención**: todo lo que dice que existe, existe, y pasa su comparación.
- Es **optimista en un factor ~1,3** porque cuenta 32 filas cuya evidencia es vacío-contra-vacío
  o inexistente, con la misma marca `OK` que una comparación de 3.421 renglones.
- Es **optimista en un factor ~1,3 más** porque el denominador ignora 155 comportamientos reales
  de Legacy (reportes dinámicos + MacReportes).
- Y es **optimista en un factor ~4** si lo que se quiere medir es «cuánto puede usar hoy una
  persona que entra al sistema», por los permisos que no existen en el catálogo.

---

## 6. Lo que no pude comprobar (y qué haría falta)

1. **Los 403 en vivo.** No tengo contraseña de ningún usuario no-SuperAdmin (`NAOLANY`, `ADMIN`,
   `MARYORIS`, `PRUEBA`, `JAZMIN`), y crear uno sería una escritura. La conclusión de §2 sale de
   código + base, que es concluyente, pero **no de una petición real**. Hace falta un token de
   rol `User` para cerrarlo del todo — y, ya que se tiene, para meterlo en el banco.
2. **`ola3-asistencia-diaria`.** El stored procedure devuelve 0 renglones para todas las fechas
   disponibles en esta base. Hace falta una base con asistencias consultables.
3. **`@IDEmpresaCS = 1`** (§4.7): hay que comprobar el catálogo de clientes del reporte contra
   MacZ **y** Produccion_svr.
4. **Las 136 filas `parcial` y las 17 `por-verificar`.** Fuera del encargo: no las auditó nadie,
   y cada una puede esconder lo mismo que §1.2.
5. **Las 349 capacidades que escriben.** Por regla no se ejecutó ninguna. Toda la validación de
   escrituras sigue pendiente, y es donde más duele equivocarse.

---

## 7. Qué arreglaría, por orden

1. **Sincronizar el catálogo de permisos** de la empresa 1 (34 claves), y **sembrar las 14 que
   no existen ni en código**. Sin esto, «migrado» significa «migrado para SuperAdmin».
2. **Meter un token de rol normal en el banco de comparación.** Hoy es ciego a la capa de
   permisos por construcción, y acaba de ocultar el hallazgo más grande de esta auditoría.
3. **Poner `[Authorize]` y permisos finos a `RecaudacionesController`, o borrarlo.** Es andamiaje
   de Visual Studio publicado; el `GET /{qr}` entrega datos sin autenticación.
4. **Cerrar `/whoami` y `/dbcheck`** o protegerlos.
5. **Distinguir en el tablero un `OK` con datos de un `OK` vacío.** Un valor `OK-vacio` o una
   columna `renglones_comparados`. Y re-apuntar los 3 casos de §1.2(a), que ya tienen datos
   disponibles.
6. **Permisos finos a las 6 escrituras de `AlmacenCascosController`** y a las 11 de
   Torneos/Temporadas.
7. **Arreglar el `catch` sin filtro de `UserAdminService.cs:73`.**
8. **Escribir la ruta real en `ubicacion_nueva`** en lugar de `…/x → y`, para que el tablero se
   pueda cruzar con el código automáticamente.
9. **Regenerar `sp-de-codigo.txt` desde el código** (474) o borrarlo: hoy es circular.
10. **Corregir las 8 filas de `tipo_token`** y enlazar `reportes-cobertura.tsv` desde el
    `registro.tsv`.
