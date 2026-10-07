# Ola 10 — Los defectos de código de la auditoría final

Lote: los hallazgos de código de `auditoria-final.md` (secciones 3.3, 4 y 5).
No son hallazgos: son trabajo. Todo arreglado, compilado y comprobado.

**Entorno.** Worktree aislado, rama `worktree-agent-aea05824061b3c395` sobre
`migracion-legacy` (`4658ddd`). Backend nuevo en `127.0.0.1:5093`
(`ASPNETCORE_ENVIRONMENT=Development` para poder leer `swagger.json` y comparar
contratos). Legacy en `:63479`. Base `LAP-JUAND` / `Produccion_svr` (Tauro).
`FechaOperacion` = 2026-10-07 = hoy, así que `sp_n_ConsultaConstantes` **no**
dispara el cierre del día y el banco se puede correr sin instantánea.

**Resultado global.** Pruebas: **413 → 449, todas en verde** (36 nuevas).
Banco de comparación: **135 de 135 OK, 0 diferencias**, con la fecha de
operación intacta durante la corrida.

**Una advertencia sobre el arranque.** El worktree nació 94 commits atrás de
`migracion-legacy` y le faltaban archivos que la auditoría cita (unidades
territoriales, el logger del repositorio de pedidos). Se rebasó a la punta antes
de tocar nada. Dos defectos del lote ya estaban resueltos ahí por otro agente
(ver §5 y la nota de §4).

---

## 1. Saldos en cero y crédito regalado

`Infrastructure/Repositories/VentasPedidoCapturaRepository.cs`

### Qué estaba mal

Tres recaídas del mismo archivo devolvían, al fallar, **el valor que más deja
pasar**. Ninguna lo decía.

**1.1 El `catch` vacío de los saldos.** `ConsultarSaldosClienteWebAsync` tenía

```csharp
catch (SqlException)
{
    // Se devuelve el renglon en ceros. Sin saldos la pantalla sigue
    // sirviendo para consultar; lo que no se hace es inventar cifras.
}
```

Inventaba la peor cifra posible. El renglón en ceros dice "este cliente no debe
nada", y esos dos ceros los lee `AgregarDisponibleAsync`:

```csharp
saldo["disponible"] = limite - saldoPendiente - saldoVencido;
```

O sea: **un error de SQL entregaba el límite de crédito completo como
disponible**. El vendedor ve saldo 0 y crédito al tope y le vende a quien no
puede pagar. Y ese mismo `disponible` viaja como `@Disponible` al capturar, que
es con lo que legacy decide si el pedido se va a "pendientes de autorizar": con
el número inflado, pedidos que requerían visto bueno pasaban directo. El
llamador **no podía distinguir "la consulta falló" de "no debe nada"**: los dos
casos eran el mismo renglón.

**1.2 La misma puerta, por el límite.** `ConsultarCreditoDisponibleAsync`
tragaba el `SqlException` y devolvía `null`, con el comentario *"es mejor un
límite conservador"*. **El estático no es el conservador: es el más alto.** El
caso que se midió al escribir esa función —cliente 5, estático 10,000 contra
5,000 calculado por `sp_n_ConsultaCreditoDisponible`— es exactamente este:
tragarse el error subía el disponible 5,000 pesos.

### Qué se hizo

Las dos recaídas se relanzan como `ConflictException` → **409**, con el motivo
escrito y un `LogError` que nombra el cliente. **Sin saldos no se captura.**
Negarse es la única respuesta honesta cuando lo que se pide es un número de
crédito y no se puede calcular.

Lo que **no** cambió, porque es un dato y no una falla:

* que el SP corra bien y devuelva **cero folios pendientes** sigue siendo saldo 0;
* que `sp_n_ConsultaCreditoDisponible` corra y devuelva **cero renglones** sigue
  cayendo al estático — eso ahora deja un `LogWarning` para que el número de la
  pantalla se pueda explicar después.

### Cómo se comprobó

El camino feliz, en vivo, **idéntico antes y después** (único cliente con el que
se puede medir: `sp_n_rptVentasSaldos` corre bien en esta base):

```
POST /api/ventas/pedidos/captura/clientes/buscar  {"IDCliente":5,"IDEmpresaCS":0}

ANTES   200  saldoPendiente 0, saldoVencido 0, limiteCredito 5000.0, disponible 5000.0
DESPUES 200  saldoPendiente 0, saldoVencido 0, limiteCredito 5000.0, disponible 5000.0
```

La rama de error **no se puede provocar en vivo sin tocar la base**, y la base no
se toca: `SqlException` no tiene constructor público, así que tampoco se puede
fabricar en una prueba unitaria. Queda cubierta por `Ola10FallarCerradoTests`,
que lee el propio fuente del repositorio y exige que esos tres `catch` ya no se
traguen el error, y que `ConflictException.StatusCode` sea 409 — el mismo estilo
estructural de `RutasSinChocarTests`.

---

## 2. El filtro por agente fallaba ABIERTO

`VentasPedidoCapturaRepository.ConsultarAgenteFiltroAsync`

### Qué estaba mal

Si `sp_w_ConsultaAgenteUsuario` no está desplegado —y en **Tauro no lo está**,
comprobado contra `sys.procedures`— el `catch` devolvía `0` con una advertencia
una vez por proceso. Y **`0` no significa "no se sabe": significa "este usuario
ve a TODOS los clientes"**. El repartidor amarrado a un agente veía el padrón
completo de la empresa, y la única señal era una línea en el registro del
servidor que nadie mira.

No era hipotético. Era el comportamiento de todos los días en esta base.

### Qué se hizo

Falla **cerrado**: `LogError` + `ConflictException` → **409** nombrando el script
que falta. Un filtro de seguridad que falla abierto es peor que no tener filtro,
porque el de arriba cree que está puesto.

Lo que no cambió: que el SP devuelva **0 filas** sigue siendo "este usuario no
está amarrado a ningún agente" y sigue viendo todos. Eso es un dato.

Queda escrita en la cabecera del archivo la regla que separa las dos recaídas que
viven ahí: **una recaída puede quitar información, nunca puede quitar un filtro**
(la de `sp_w_ConsultaExistenciaComprometida` sigue degradando y avisando, porque
lo que pierde es un dato de más).

### Cómo se comprobó

En vivo, con un usuario que **sí** tiene vínculo a Mac31 (`BancoPruebas` no lo
tiene: su `idUsuario` llega en 0 y el filtro ni se consulta, que es la razón por
la que esto nunca salió probando con esa cuenta):

```
POST /api/ventas/pedidos/captura/clientes/buscar   (token de ADMIN, idUsuario=1)

ANTES   200  ->  1042 clientes, de 15 agentes distintos
                 log: "el FILTRO POR AGENTE queda APAGADO (todos ven todos...)"

DESPUES 409  ->  {"code":"CONFLICTO",
                  "message":"No se puede resolver el filtro por agente de este
                             usuario, asi que no se devuelve el padron de
                             clientes. Falta desplegar sp_w_ConsultaAgenteUsuario
                             en esta base.",
                  "detail":"Could not find stored procedure 'sp_w_ConsultaAgenteUsuario'."}
```

---

## 3. Endpoints que recibían parámetros y los tiraban

### 3.1 `PATCH /usuarios/{id}/empresa` respondía 500 siempre

**Por qué.** `UserAdminService.UpdateEmpresaAsync` devolvía
`Task.FromException(new InvalidOperationException(...))`. Esa excepción **no
está en la lista de `ExceptionHandlingMiddleware`**, así que caía en el `catch`
general y salía como `500 "Error interno."` con cualquier cuerpo. En Swagger el
endpoint declaraba **solo 200**. Una capacidad retirada a propósito, disfrazada
de avería del servidor: el que llama no puede distinguir "esto no se hace aquí"
de "el backend está roto", y reintentar no sirve de nada.

**Qué se hizo.** La regla del modelo es una base = una empresa, así que mover a
alguien a otra empresa es un alta en **otra base**, no un PATCH. Pero eso se
contesta, no se revienta:

| petición | antes | después |
|---|---|---|
| `{"EmpresaId":1}` (la que ya tiene) | **500** | **200** con el usuario, sin tocar nada |
| `{"EmpresaId":7}` (otra) | **500** | **409** con el motivo y los dos ids |
| `{"EmpresaId":0}` | **500** | **400** |
| usuario inexistente | **500** | **404** |

No se borró la ruta ni el permiso `usuarios.empresa.editar`: la pantalla existe y
el campo se enseña; cambiar el 500 por un 404 sería igual de mudo.

**Comprobado en vivo**, los cuatro casos, y en `swagger.json`:
`resp: ['200']` → `resp: ['200','400','404','409']`. Más cinco pruebas.

### 3.2 El tablero aceptaba 11 filtros y no usaba ninguno

`Infrastructure/Repositories/DashboardVentasRepository.cs`

`ConsultarFiltrosAsync(DashboardVentasFiltroRequest request, ...)` **no
referenciaba `request` en ninguna línea**: el `SqlCommand` se armaba con cero
parámetros y devolvía 200 con los catálogos sin filtrar.

**No se implementaron los filtros porque no hay dónde.**
`sp_w_ConsultarDashboardVentasFiltros` **no declara ni un parámetro** —leído de
su script, `Querrys/Dashboard Ventas Zara/02_sp_w_ConsultarDashboardVentasFiltros.sql`—
y por diseño devuelve los ocho catálogos enteros: son los desplegables de la
pantalla, no un resultado filtrado. Filtrarlos exigiría cambiar el procedimiento,
y la base no se toca para esto.

**Se quitaron del contrato**, en los cinco niveles (controlador, interfaz de
servicio, servicio, interfaz de repositorio y repositorio). Se queda en POST y en
la misma ruta; un cuerpo que mande un front viejo se ignora sin romperse.

```
swagger.json  POST /api/dashboard/filtros/consultar
ANTES    body: DashboardVentasFiltroRequest  (11 propiedades)   resp: ['200','400']
DESPUES  body: SIN CUERPO                                       resp: ['200']
```

### 3.3 El `PUT` de unidades territoriales descartaba el cuerpo

`Infrastructure/Repositories/CatalogosAdminRepository.Estructurados.cs`

El PUT exigía `UnidadTerritorialRequest` (clave, idStatus, unidadTerritorial) y
el repositorio mandaba **solo `@IDUnidadTerritorial`** al SP. Mandar una clave
nueva era un no-op: quien llamaba creía haber renombrado la unidad.

Mandar solo el id **se queda, y es correcto**: Legacy hace exactamente lo mismo
(`CatUnidadTerritorialRepository.cs:174` manda un único `AddWithValue`, aunque su
cadena de depuración de al lado arme cinco valores) y
`sp_n_ActualizarCatUnidadTerritorial` **no existe en ninguna de las dos bases**,
así que inventar los otros cuatro parámetros sería apostar a una firma y romper
la instalación donde hoy funciona. Lo que no se sostiene es **seguir pidiendo el
cuerpo**: se quitó.

```
swagger.json  PUT /api/catalogos/unidades-territoriales/{id}
ANTES    body: UnidadTerritorialRequest
DESPUES  body: SIN CUERPO
```

En vivo los dos siguen dando el mismo 500 por el SP ausente
(`Could not find stored procedure 'dbo.sp_n_ActualizarCatUnidadTerritorial'`),
que es lo que también le pasa a Legacy aquí. Lo que cambió es lo que el contrato
promete.

### 3.4 `RecaudacionService` descartaba 8 filtros — y algo peor

`Application/Services/RecaudacionService.cs:28,38,49`

La auditoría acertó en el síntoma. Al mirarlo resultó **menos grave y más grave**
a la vez:

**Menos grave: las tres ramas eran inalcanzables.** Este servicio tiene **un
solo llamador** en todo el backend: `RecaudacionesController.Get(qr)`, el ticket
público por código QR, que llena tres campos (`IDRecaudacion`, `IDCaja`, `Dv1`) y
nunca entra por ellas. La consulta de recaudaciones con los treinta y un filtros
de la pantalla de Mac31 vive en `AviacionRecaudacionesController`
(`POST api/aviacion/recaudaciones/consultar`), que **ya los aplica todos juntos**
y lo documenta. Eran ramas copiadas de Legacy que solo servían para que alguien
las copiara mañana creyendo que filtran. **Se borraron.**

**Más grave: el `Dv1` se recibía y se tiraba, siendo el candado del único
endpoint anónimo del módulo.** El doc-comment del controlador promete que *"los
tres datos tienen que cuadrar entre sí"*. No cuadraban: el repositorio **no manda
`@Dv1`** al procedimiento (no existe tal parámetro), así que el tercer dato del
QR no decidía nada y bastaba con que el par `(IDRecaudacion, IDCaja)` existiera.
Ahora el dígito se compara contra el `DV1` del propio renglón y, si no cuadra, no
se entrega el ticket.

De paso queda corregido el defecto de Legacy en esa rama, el que el comentario de
la ola 5 ya había documentado: su `string.Join` de la lista vacía mandaba
`IDsRecaudacion = ""` con `IDRecaudacion = 0`, o sea el SP sin filtros y de vuelta
**todas** las recaudaciones de la base. Pedir una y recibir todas.

**No se puede comprobar en vivo aquí, y es legítimo:** `dbo.Recaudaciones` tiene
**0 renglones** en `Produccion_svr` (es módulo de Aviación) y la firma de
`sp_n_ConsultaRecaudaciones` en esta base es de **9 parámetros** contra los **31**
que manda el repositorio —igual que Legacy, que manda los mismos 31—, así que los
dos fallan idéntico. La decisión del dígito va con prueba unitaria, **siete
casos** (incluye `"07"` contra `7`, el espacio en blanco, el no-numérico y la base
sin columna `DV1`).

---

## 4. Las claves de permiso que no se siembran en una empresa nueva

`Infrastructure/Security/PermissionService.cs`

### La causa, que es la misma de las tres veces anteriores

Una clave que no está en `WPermiso` **no puede tener fila de concesión**, así que
su endpoint contesta **403 a todo el mundo menos al SuperAdmin**, que pasa antes
de mirar nada. Y el SuperAdmin es la única cuenta con la que se prueba en local:
el módulo se ve perfecto y está muerto para los usuarios de verdad.

`PermissionCatalogSeeds` es una lista **escrita a mano**. Las familias que
llegaron a `WPermiso` de Tauro por un script de despliegue nunca se anotaron ahí,
así que en una base de empresa **nueva** no entran.

### Lo que señaló la auditoría

`cascos_cambio.{ver,crear,cancelar}`, `app_movil.cascos_cambio.{ver,crear,cancelar}`
y `app_movil.ventas.usados`. Comprobado:

```
Produccion_svr (Tauro)  446 claves; cascos_cambio.* SÍ (por script), app_movil.ventas.usados NO
MacZ (Zaragoza)         223 claves; cascos_cambio.* CERO filas, app_movil.ventas.usados NO
```

Las otras tres que la auditoría metió en el mismo saco —`ventas.ver_modulo`,
`ventas.usados`, `ventas.usados.guardar`— **no van en la lista estática**, y esto
es la sección 2 del resumen aplicada: para `ventas.*` manda Mac31. Las siembra
`EnsureVentasPermissionsSyncedAsync` desde los botones de las tres formas de
Mac31, y **eso ya corre en el arranque** (lo arregló otro agente en `829d547`,
junto con `usuarios.password.ver` y `reportes.centros.*`). Declararlas en la lista
crearía dos dueños de la misma llave y la de Mac31 ganaría en el siguiente
cálculo. Hay una prueba que lo impide.

### Lo que encontró la prueba, y nadie había contado

Al escribir el candado salieron **12 claves más**, varias sin fila en **ninguna**
de las dos bases:

| clave | dónde se exige | consecuencia |
|---|---|---|
| `dashboardventas.ver` | los **12 endpoints** de `DashboardVentasController` | el **tablero de ventas completo** era visible solo para el SuperAdmin, **en las dos empresas** |
| `configuracionroltorneo.*` (4) | `ConfiguracionRolTorneoController:26-123` | 403 para todos; su gemela `generacionroltorneo.*` sí estaba sembrada |
| `tarimas.*` (4) | `TarimasController` | 403 para todos, en las dos bases |
| `permisos_modulos.{ver_modulo,activar}`, `permisos_roles.ver_modulo` | `PermisosWebController:48-114` | en una empresa nueva, **la primera pantalla que no se puede abrir es la que reparte permisos** |
| `asistencias.*`, `checador.*`, y **todo** `app_movil.*` | sus controladores | misma historia |

### Qué se hizo

Se siembran **49 claves** (las exigidas, más el `.ver_modulo` de cada familia
nueva, que es la llave con la que el módulo sale en el menú de inicio). Sembrar
no es conceder: solo crea la clave para poder repartirla.

Y **la prueba se queda**: `ClavesDePermisoSembradasTests` saca por reflexión toda
clave que decide en un controlador —por `perm:x|y` y por los arreglos de
`PermisoControllerBase`— y exige que alguien la siembre, o que esté en una familia
derivada de la base con el nombre de la rutina que la siembra escrito al lado.
Más una prueba que impide "arreglar" el hueco de `ventas.*` metiéndolas en la
lista estática.

### Cómo se comprobó

```
dbo.WPermiso en Produccion_svr

ANTES del arranque con el arreglo    446 filas
DESPUES                              462 filas

filas nuevas que NINGÚN script había creado:
  app_movil.ventas.usados
  configuracionroltorneo.{ver_modulo,ver,crear,editar,activar}
  dashboardventas.{ver_modulo,ver}
  tarimas.{ver_modulo,ver,crear,editar,estado.editar}
```

**MacZ se deja sin tocar a propósito.** Verificar ahí exigiría apuntar el backend
a la base de Zaragoza y escribirle 200+ filas de catálogo, y la empresa en curso
es Tauro (la que dice la cadena de conexión). La foto de MacZ queda como
evidencia del "antes"; el "después" es el mismo código que acaba de sembrar 16
filas en Tauro.

---

## 5. El caso del banco que se probaba vacío — ya estaba arreglado

`migracion/comparador/casos/ola3-huellas-usuarios.json`

**No era trabajo pendiente: otro agente lo cerró antes de que empezara.** El caso
sigue existiendo y ahora explica en su `_porque` por qué su rama está vacía
(`UsuariosHuellas` = 0 filas en Tauro, 12 en MacZ) y queda apuntado a MacZ; y
nació el caso hermano **`ola3-huellas-empleados.json`**, que compara la rama que
sí tiene datos.

Comprobado hoy, en vivo y contra la base:

```
EmpleadosHuellas                        409 renglones (79 empleados distintos)
UsuariosHuellas                           0 renglones

GET /api/huellas?origen=empleado&todas=true   200, 62 renglones  (el SP filtra por empleado activo)
GET /api/huellas?origen=usuario&todas=true    200,  0 renglones

banco:  OK  Huellas registradas de empleados   (legacy 200 / nuevo 200)
        OK  Huellas registradas de usuarios    (legacy 200 / nuevo 200)
```

La comparación de empleados incluye **la huella completa en base64** de los 62
renglones, con `mapa_campos` y `solo_campos` para los seis identificadores que el
DTO de Legacy declaraba a la vez. Ya no es un trámite.

---

## Lo que NO se tocó

* **`Legacy/`**: nada. Se leyó para decidir (`RecaudacionService`,
  `CatUnidadTerritorialRepository`, `ConsultarRecaudaciones.cs`,
  `Funciones.ConvertFtmDate`) y para confirmar que mandar solo el id en unidades
  territoriales es fiel al original.
* **La base**: no se creó, alteró ni borró ningún procedimiento, tabla ni dato
  para que algo pasara. Las 16 filas nuevas de `WPermiso` las escribió la propia
  siembra del arranque, que es la funcionalidad bajo prueba, en la tabla del web.
* **MacZ**: ni una escritura.

## Cierre

```
dotnet build ISL_Service.sln                 0 errores
dotnet test  ISL_Service.Tests               449 de 449 en verde  (413 antes)
python comparar.py casos/                    135 coinciden, 0 con diferencias, 135 casos
                                             fecha de operacion sin moverse
```

Commits (pequeños, en español, sin coautoría):

```
a4fe143  Un error de SQL regalaba el credito, y el filtro por agente fallaba abierto
f28c69c  El ticket por QR: tres ramas muertas que tiraban filtros, y un candado que no cerraba
1d722e3  PATCH /usuarios/{id}/empresa contestaba 500 a todo, con cualquier cuerpo
08745d7  Dos endpoints pedian datos que no iban a ningun sitio: fuera del contrato
dd6cc5b  La siembra de permisos: 49 claves que ninguna empresa nueva recibia
```
