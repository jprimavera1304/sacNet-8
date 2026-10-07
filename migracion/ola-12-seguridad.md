# Ola 12 — Los hallazgos de la auditoría de cierre, arreglados

Esta ola no migra nada nuevo: toma los hallazgos de `auditoria-cierre.md` y los
cierra. Cada apartado dice **qué estaba mal**, **qué se hizo** y **cómo se
comprobó**. Donde algo no se puede arreglar sin tocar `Legacy/` o la base, se
dice, con lo que haría falta.

**Entorno de la comprobación**

- Backend nuevo compilado del HEAD de esta rama, corriendo en `127.0.0.1:5097`,
  entorno **Production** (`GET /whoami` →
  `{"companyId":"local","environment":"Production",...}`). Que sea Production
  importa: dos de los hallazgos sólo se ven ahí.
- Legacy vivo en `localhost:63479`, misma base.
- Bases `LAP-JUAND` / `Produccion_svr` (Tauro, la que apunta `appsettings.json`)
  y `MacZ` (Zaragoza), sa/123.
- **508 pruebas verdes** (eran 472; esta ola añade 36) y **135 de 135 casos del
  banco de comparación** contra Legacy, sin diferencias.
- No se ejecutó ninguna capacidad marcada `escribe = si` contra datos vivos. Lo
  que necesitaba demostración sobre escrituras se demostró leyendo el cuerpo de
  los procedimientos en la base y con un experimento en `tempdb` sobre tablas
  temporales.

---

## 1. La autoría se podía falsificar (IDOR) — CERRADO

### Lo primero: los dos parámetros de usuario NO son lo mismo

Antes de tocar nada se leyeron los dos procedimientos en la base, para no
adivinar cuál era identidad y cuál filtro:

| parámetro | qué es | evidencia |
|---|---|---|
| `@IDUsuarioActual` | **QUIÉN PREGUNTA.** Es un control de seguridad: el candado de almacén. | `sp_n_ConsultaVentasPedidos`, líneas 133-134 de su definición: `AND Ped.IDAlmacen IN (SELECT IDAlmacen FROM UsuariosAlmacenes WHERE IDUsuario = @IDUsuarioActual AND IDStatus = 1)` |
| `@IDUsuario` | **DE QUIÉN QUIERO VER.** Filtro legítimo de pantalla. | `sp_n_ConsultaVentas` líneas 565-566, con el comentario del propio procedimiento: `-- Busqueda por IDUsuario`. Y `sp_n_ConsultaVentasPedidos` línea 129: `AND Usu.IDUsuario IN (SELECT Valor FROM fn_RegresaTablaUsuarios(@IDUsuario))` |

### Qué estaba mal

- **`VentasConsulta`**: `Prepare` sólo rellenaba `IDUsuarioActual` desde el token
  **si el cuerpo lo traía en 0**. O sea: el cuerpo ganaba. Mandando el id de otro
  se consultaba con el candado de otro; mandando 0, **sin candado**.
- **`@IDUsuario` sin permiso**: era un filtro libre. Combinado con omitir
  `SoloMisPedidos`, un cliente móvil veía los pedidos de todos.
- **Pedidos (autorizar)**: `VentasPedidosRepository` tomaba
  `IdUsuarioProcesar ?? IdUsuarioAutorizar ?? IdUsuario ?? token`. El token era
  el **último** recurso, y esos ids quedan escritos en
  `Pedidos.IDUsuarioProceso` / `IDUsuarioAutorizo`.
- **Pagos a proveedores**: los seis campos de auditoría venían del cuerpo y el
  servicio los **exigía** ("IDUsuario es requerido."). Es dinero que sale.
- **Personas**: `IDUsuario` y `Equipo` también se exigían del cuerpo.

### Qué se hizo

| archivo | cambio |
|---|---|
| `Application/Services/VentasConsultaService.cs` | `Prepare` pone `IDUsuarioActual = idUsuarioToken` **siempre**; lo que venga en el cuerpo se descarta. Nuevo `AcotarFiltroDeUsuario`. |
| `Controllers/VentasConsultaController.cs` | Ahora hereda de `PermisoControllerBase`. `ResolverAlcanceAsync`: con `ventas.consultar` (o `ventas.ver_modulo`, o SuperAdmin, o instalación sin permisos) el filtro `@IDUsuario` se respeta tal cual; sin él, pedir los de otro da **403** y no pedir a nadie se acota a los propios. |
| `Application/Interfaces/IVentasConsultaService.cs` | Las firmas llevan `puedeVerDeOtros`. **Se reemplazaron, no se añadieron**: las versiones sin el parámetro ya no existen, así que no se puede volver a llamar a la insegura por descuido. |
| `Application/DTOs/VentasPedidos/AutorizarPedidosRequest.cs` | **Borrados** los seis campos de identidad. Un cliente que los siga mandando no se rompe (System.Text.Json ignora lo que no existe); simplemente deja de servirle. |
| `Infrastructure/Repositories/VentasPedidosRepository.cs` | Quien procesa y quien autoriza = el token, punto. |
| `Infrastructure/Services/AutorizarPedidosAsyncCoordinator.cs` | `CloneRequest` ya no clona identidad (no hay qué clonar). |
| `Controllers/ProveedoresPagosController.cs` | `SellarAutoria()` pisa los seis campos con `IdUsuarioLegacy()` y `Equipo()`. |
| `Controllers/PersonasController.cs` | `SellarAutoria()` en las cuatro escrituras (insertar, actualizar, cancelar, reactivar). |
| `Application/Services/ProveedoresPagosService.cs` | Fuera las validaciones que pedían la autoría al cliente. |

### Cómo se comprobó

**En vivo** (`:5097`, token de `BancoPruebas`, SuperAdmin, `idUsuario` 0):

```
POST /api/ventas/consulta/remisiones  {"fechaEmisionInicial":"2026-09-19","fechaEmisionFinal":"2026-09-19"}
  -> total 62
POST /api/ventas/consulta/remisiones  {... ,"IDUsuario":22}
  -> total 15
```

Y en la base, `SELECT COUNT(*) FROM Ventas WHERE CAST([Fecha de Emision] AS date)='2026-09-19' AND IDUsuario=22`
da **15**. Es decir: el filtro `@IDUsuario` sigue siendo un filtro y funciona —
no se rompió una función de pantalla al cerrar un agujero.

**Pruebas** (`Ola12IdentidadDelTokenTests.cs`, 14 casos):

- `IDUsuarioActual` sale del token aunque el cuerpo mande otro id.
- `IDUsuarioActual` **no se puede apagar mandando 0**.
- Con permiso, `@IDUsuario` se respeta; sin permiso, pedir los de otro → 403 y el
  servicio **no llega a llamarse**.
- Sin permiso y sin filtro (el caso de omitir `SoloMisPedidos`) → acotado a los
  propios.
- `AutorizarPedidosRequest` no tiene ninguna de las seis propiedades de
  identidad (prueba por reflexión: si alguien las vuelve a poner, falla).
- Pagos a proveedores y Personas: lo que llega al repositorio lleva el id y el
  equipo del token, no los del cuerpo.

### Lo que NO se pudo cerrar, y qué haría falta

En **`sp_n_ConsultaVentas`** el candado de almacén por `@IDUsuarioActual` está
**comentado dentro del propio procedimiento** (líneas 547-548 de su definición,
igual en las dos bases):

```sql
--IF @IDUsuarioActual <> 0
--  SELECT @SqlWhere = @SqlWhere + 'AND C.IDAlmacen IN (SELECT IDAlmacen FROM UsuariosAlmacenes WHERE IDUsuario = ...
```

Así que ahí ese parámetro hoy **no recorta nada**: pasarlo bien es correcto, pero
todavía no cambia el resultado. Visto de otro modo: en remisiones y pagos no
había escalada que cerrar, porque no había candado que saltarse. Para que lo
haya hace falta un `sp_w_ConsultaVentas` envolvente que aplique el recorte —
cambio en la base, y los `sp_n_` no se editan.

---

## 2. Un permiso revocado seguía vivo hasta media hora — CERRADO

### Qué estaba mal

`PermissionService` es **Scoped** (instancia nueva por petición), `IMemoryCache`
es **Singleton**, y el diccionario del que colgaba toda la invalidación
—`_observedUserCacheKeys`— era **de instancia**. Nacía vacío en cada petición.

Resultado: al cambiar los permisos de un rol, `InvalidateEmpresaPermissionCache`
recorría un diccionario que como mucho tenía la clave del propio administrador.
**No quitaba ni una clave de los demás.** Quien ya tenía el permiso lo
conservaba 5 minutos (TTL activo) y hasta **30** por el camino "stale".
Conceder un permiso tampoco surtía efecto a tiempo.

Y encima `InvalidateUserPermissionCache` compensaba con una lista fija de seis
nombres de rol. La clave de caché incluye el `RolCodigo` del tenant, así que
cualquier rol propio fuera de esa lista **nunca** se invalidaba.

### Qué se hizo

`Infrastructure/Security/PermissionService.cs`: la invalidación ya no es una
lista de claves que hay que acertar, son **tokens de expiración**.

- `_invalidadores`: `ConcurrentDictionary<string, CancellationTokenSource>`
  **estático** — estático para igualar la vida de la `IMemoryCache`, con el
  mismo razonamiento que el `_candados` que ya existía en el archivo.
- Dos ámbitos: `perm:{empresaId}` y `perm:{empresaId}:{userId:N}`.
- Cada renglón de caché (el vigente **y el "stale"**) se guarda con
  `OpcionesDeCache`, que le cuelga los dos tokens.
- Invalidar = cancelar el token. La `IMemoryCache` tira de golpe todos los
  renglones colgados de él, **de cualquier petición y de cualquier rol**.
- Fuera `_observedUserCacheKeys`, `TrackObservedUserCacheKey` y la lista de seis
  roles: ya no hacen falta y eran el defecto.

El `CancellationTokenSource` cancelado **no se libera** a propósito: todavía hay
renglones apuntando a su token y liberarlo haría que la caché reventara al
consultarlo. Cancelado y sin referencias lo recoge el GC.

### Cómo se comprobó

`Ola12AislamientoYFugasTests.cs`:

- `_observedUserCacheKeys` **ya no existe** (reflexión sobre los campos de
  instancia). Si alguien lo vuelve a poner, falla.
- `_invalidadores` existe y **es estático**.
- **De comportamiento**, contra una `MemoryCache` real y usando los métodos
  reales por reflexión: un renglón guardado desde "la petición A" con un
  `RolCodigo` propio del tenant (`ROL_PROPIO_DEL_TENANT`, justo el que la lista
  de seis nunca alcanzaba) **muere** cuando "la petición B" invalida — tanto por
  ámbito de usuario como de empresa.
- Y no se pasa de largo: invalidar a un usuario **no** tira la caché de otro.
  Si la tirara, cada cambio de permiso reconstruiría todo y la caché perdería el
  sentido.

---

## 3. Dos usuarios a la vez se pisaban (`SELECT MAX(id)` tras el INSERT) — CERRADO los que se podían

### El mecanismo, y por qué `SCOPE_IDENTITY()` no sirve aquí

El INSERT ocurre **dentro** del procedimiento, que es otro ámbito. Comprobado en
`tempdb`, con `EXEC()` imitando la frontera de ámbito de un SP:

```
SCOPE_IDENTITY (otro ambito, como un SP) = NULL
@@IDENTITY (misma sesion) = 1
```

Así que lo correcto es `@@IDENTITY` **en la misma conexión** del `EXEC`:
`@@IDENTITY` es **por sesión**, no global —a diferencia de `IDENT_CURRENT`, que
tendría exactamente el mismo defecto que el `MAX()`—, así que otro usuario
insertando al mismo tiempo no lo mueve.

### 3.1 Alta de usuarios de Mac31

`Infrastructure/Repositories/SeguridadMacRepository.cs`. Antes: llamar al SP en
una conexión, cerrarla, abrir **otra** y `SELECT ISNULL(MAX(IDUsuario),0) FROM
dbo.Usuarios`. Con dos capturistas a la vez, a uno se le contestaba el id del
otro — y la pantalla sigue y le escribe **almacenes y permisos al usuario
equivocado**.

Ahora: una sola conexión, `@@IDENTITY`, **y se verifica** que el renglón leído es
el del usuario que se pidió (`WHERE IDUsuario = @@IDENTITY AND Usuario = @Usuario`);
si no cuadra, se contesta 0 en vez de un id ajeno. Es seguro porque el propio
`sp_n_InsertarUsuario` rechaza nombres repetidos ("EL USUARIO YA ESTA
REGISTRADO").

Comprobado en la base: `dbo.Usuarios.IDUsuario` **es IDENTITY** y la tabla **no
tiene disparadores** en ninguna de las dos bases (`sys.triggers` vacío) — lo
único que podría hacer que `@@IDENTITY` apuntara a otra tabla.

### 3.2 Catálogos y maestros

`CatalogosAdminRepository` (nuevo `InsertarYLeerIdAsync` + `IdRecienInsertadoAsync`)
y `MaestrosRepository` (`InsertarConUltimoIdAsync`): mismo patrón. Cubre las
altas de **categorías, marcas, autos, empresas, series, agentes, almacenes,
bancos, grupos de categorías, unidades de medida, tipos de gasto, condiciones de
crédito, galleros, repartidores, días festivos, clientes, productos, proveedores
y domicilios de proveedor**.

Comprobado en `sys.columns`: las 18 tablas revisadas tienen su llave como
IDENTITY. Si alguna no lo fuera, `@@IDENTITY` sale NULL: se cae al `MAX()` de
antes **y queda escrito en el registro del servidor** diciendo qué haría falta
(que el procedimiento devuelva el id, o un `sp_w_` envolvente). No se esconde.

### 3.3 Reportes: el peor de todos, porque es ver el reporte de otro

`sp_n_ActualizarParametro` recupera el id del renglón que acaba de insertar así
(copiado de su definición en la base):

```sql
SELECT @IDParametros = MAX(IDParametros) FROM Parametros
WHERE  NombreEquipo = @NombreEquipo AND @IDUsuario = IDUsuario
```

Y el backend mandaba `@NombreEquipo = "WEB"` para todos. Comprobado en vivo en
`Produccion_svr`:

```
NombreEquipo  IDUsuario  IDReporte  Param1
WEB           0          1495       WEB[0
WEB           0          1490       WEB[0
WEB           0          1485       WEB[0
```

**La llave del `MAX()` era la misma para todo el mundo.** Dos personas pidiendo
un reporte en el mismo instante y una abría el PDF con los filtros de la otra:
su cliente, su agente, sus importes. El pase firmado se firma sobre el id
equivocado, así que no salvaba nada. No hace falta mala intención: bastan dos
pestañas.

**Qué se hizo:** la llave la elegimos nosotros. `@NombreEquipo` lleva ahora un
sufijo único por llamada (`EquipoUnicoPorLlamada` en
`ReportesVentasRepository.LegacyParams.cs`, y lo mismo en
`VentasFacturacionRepository.GuardarParametrosAsync`), así que el `MAX()` se
calcula sobre un conjunto de **un solo renglón: el propio**. Un punto en cada
sitio, cubre las 8 llamadas de reportes y la de facturación.

Por qué es seguro, comprobado:

- **Nadie lee `Parametros` por `NombreEquipo`.** Los reportes reciben el
  `IDParametros` y lo resuelven con `sp_n_ConsultaParametros`; la única línea que
  leía `NombreEquipo` en los `sp_n_rpt*` está comentada (visto en
  `sp_n_rptVentasFacturas`).
- **Cabe**: la columna es `varchar(1000)` y el `#ParamIds` de los reportes la
  declara `varchar(400)`; el sufijo deja el nombre en 36 caracteres y uno largo
  se recorta. Hay una prueba que lo fija.
- **El texto que SÍ se imprime no se toca**: `Param1 = "WEB[<idUsuario>"` lo
  sigue armando `BuildLegacyUsuarioParam` con el nombre limpio, sin sufijo.

### Cómo se comprobó

`Ola12ConcurrenciaYPasosTests.cs`: dos llamadas nunca comparten nombre; mil
llamadas dan mil nombres distintos; el nombre original sigue delante (para poder
depurar la tabla `Parametros`); nunca se pasa de 400 caracteres; sin nombre se
usa `WEB`.

### Lo que NO se pudo cerrar: los consecutivos `*NextID`

`UsuariosAnexosRepository.ConsultarSiguienteNumeroAsync` y los
`ConsultarSiguienteNumeroCuentaAsync` de aviación y catálogos generales son
**lecturas**: llaman a un `sp_n_` que hace `MAX(Num)+1` y devuelven el número
sugerido para que la pantalla lo rellene. No reservan nada, así que dos altas
simultáneas pueden proponer el mismo número.

Eso **no se puede cerrar sin tocar la base**: haría falta una `SEQUENCE`, una
tabla de reserva, o que el procedimiento de alta asignara el número él mismo en
su propia transacción. Dato que baja el riesgo: `NumUsuario` ya no es único en
los datos reales (67 usuarios, **10** valores distintos de `NumUsuario` en
Produccion_svr), así que el sistema no lo trata como llave.

---

## 4. Escrituras de varios pasos sin transacción — ACOTADO, y se dice por qué no se envuelven

### El impedimento es común a los cuatro casos, y está en la base

Se consultó `sys.sql_modules` de los procedimientos implicados:

| procedimiento | `BEGIN TRAN` | `ROLLBACK TRAN` | usa `@@TRANCOUNT` |
|---|---|---|---|
| `sp_n_InsertarVentaFactura` | sí | sí | **no** |
| `sp_n_ActualizarConcentradoDetalle` | sí | sí | **no** |
| `sp_n_CancelarVenta` | sí | sí | **no** |
| `sp_n_InsertarBancoTransferencia` | sí | sí | **no** |
| `sp_n_ConsultarConcentradoFolio` | no (sólo lee) | — | — |

Un `ROLLBACK TRAN` **desnudo** dentro de una transacción abierta desde C# no
deshace "lo del procedimiento": deshace **la nuestra**, y después el
procedimiento regresa normal. Al intentar el commit, C# revienta y el usuario
recibe un **500** en vez del mensaje de negocio.

O sea: envolverlas en una transacción, hoy, **empeora** las cosas. Para hacerlo
bien hace falta un `sp_w_` envolvente que use `SAVE TRAN` y `@@TRANCOUNT`. Eso es
cambio en la base y los `sp_n_` no se editan.

### 4.1 Complemento de pago — se timbra ante el SAT y después se escribe

`Application/Services/VentasFacturacionService.cs`.

**Lo que pasaba, y es peor de lo que decía la auditoría.** El bucle insertaba un
renglón por documento pagado y, si uno fallaba, **seguía** con los demás. Y había
un segundo defecto encadenado: `RegistrarSiQuedoHuerfano` dejaba el aviso en
`timbrada.Mensaje` y **las dos líneas siguientes lo pisaban** con el mensaje del
propio insert, así que la siguiente iteración exitosa borraba la única señal. El
controlador contestaba **siempre 200 con `ok:true` y "Complemento de pago
timbrado."**. El usuario no se enteraba nunca.

Además, la parte irreversible —el timbrado ante el SAT— no entra en ninguna
transacción de SQL Server, así que una transacción no evitaría el desfase: sólo
cambiaría qué renglones quedan.

**Qué se hizo:** cortar en el primer fallo —no seguir ensuciando— y contestar
**409** nombrando el UUID, el id de Facturapi, el `IDParametros` y exactamente
**qué documentos SÍ quedaron escritos y cuál falló**, para que alguien lo termine
a mano. Un error ruidoso y accionable en vez de un 200 que miente.

### 4.2 Timbrado normal

Mismo patrón, mismo corte (`GuardarAsync`, las dos ramas: el renglón único y el
bucle por remisión). Aquí el daño es concreto: una remisión que no se escribe
queda, para Mac31, **sin facturar**, y se puede volver a facturar → duplicado.

### 4.3 Cancelación de remisiones

`Application/Services/VentasCancelacionService.cs`. Quitaba del concentrado y
luego cancelaba, **sin mirar el resultado**. Si el segundo paso fallaba, la
remisión salía del concentrado pero seguía **viva y por cobrar**: fuera de la
lista del cobrador, o sea invisible. Dinero que nadie va a buscar. Y el bucle
seguía con las demás, acumulando casos.

Ahora: si una venta ya salió del concentrado y su cancelación **no** prosperó, se
registra como error del servidor (con idVenta, folio, concentrado, usuario y el
mensaje de la base) y se contesta **409** diciendo en qué estado quedó, cuáles se
cancelaron antes de parar y que las demás no se tocaron.

**Lo que no cambia a propósito:** si la venta no estaba en ningún concentrado,
una cancelación que no prospera no deja nada colgando; se reporta como fallida y
el lote sigue, igual que antes. El corte es sólo para el caso que deja dinero
suelto.

### 4.4 Carga de estado de cuenta bancario — el 500 que tapaba el mensaje

`Infrastructure/Repositories/OperacionMacRepository.Operaciones.cs:307`. Era el
único sitio con transacción, y el `ROLLBACK TRAN` desnudo del SP la deshacía
antes de que llegáramos a `tx.RollbackAsync`, que lanzaba *"This SqlTransaction
has completed"*. Esa excepción se llevaba por delante el mensaje que el método
acababa de construir ("Movimiento 250: ...") y el usuario recibía un **500 pelado
al rechazar UN movimiento de su archivo**.

Ahora el rollback va protegido: si el procedimiento ya deshizo la transacción, se
ignora ese fallo concreto —el efecto en la base es el mismo, nada queda escrito,
que es lo que se buscaba— y el aviso bueno llega al usuario.

### Cómo se comprobó

`Ola12ConcurrenciaYPasosTests.cs`, con un repositorio de cancelación falso:

- Si sale del concentrado y no se cancela → `ConflictException` con el texto
  operable, **y el lote se corta**: una sola salida del concentrado y un solo
  intento de cancelar, aunque se pidieran tres remisiones.
- Sin concentrado, una cancelación fallida **no** corta el lote (3 fallidas, 3
  intentos, 0 salidas del concentrado).
- Cuando todo sale bien, se cancelan las tres.

---

## 5. El middleware filtraba SQL crudo en producción — CERRADO

### Qué estaba mal

`ExceptionHandlingMiddleware` no tenía **ni un `IsDevelopment()`**: devolvía
`detail = ex.Message` crudo siempre. En Production eso regala nombres de
procedimiento, de columna, y en errores de red el nombre del servidor y de la
base.

### Qué se hizo

Se le inyecta `IHostEnvironment`. El `detail` técnico sólo se escribe en
**Development**; en cualquier otro entorno el cliente recibe el mensaje genérico
más un **`traceId`** (el `TraceIdentifier` de la petición), y el mensaje completo
del motor se **registra del lado del servidor con ese mismo traceId** para poder
cruzarlo.

Lo que **no** se tapa, a propósito:

- `AppException.Detail`: lo redacta el propio backend para el usuario (p. ej.
  "falta desplegar `sp_w_ConsultaAgenteUsuario`"), no viene del motor.
- Los `RAISERROR` de negocio (números 50000-99999) siguen llegando como 400 con
  su texto: son mensajes escritos a propósito en los SP.

De paso se quitaron las dos cosas que lo rodeaban y lo agravaban:

- `ProveedoresPagosService` y `RecaudacionService` hacían
  `catch (SqlException ex) { throw new Exception(ex.Message); }` — diez veces. No
  atrapaban nada: relanzaban el mismo texto **perdiendo el tipo**, y el tipo era
  lo único que importaba, porque el middleware mira `SqlException` para distinguir
  un `RAISERROR` de negocio de un error del motor y para decidir si el detalle se
  enseña. Convertido a `Exception`, todo caía al catch genérico: los mensajes de
  negocio de los SP se perdían y los nombres del motor se devolvían.
- `ProveedoresPagosController` devolvía `"Error en la aplicación: " + ex.Message`
  en sus cuatro acciones. Fuera los cuatro `catch`: la excepción llega al
  middleware, que ya decide.

### Cómo se comprobó

**En vivo**, entorno Production, pidiendo el dashboard cuyo `sp_w_` no está
desplegado en Tauro:

```
POST /api/dashboard/kpis/consultar
{"message":"Error de base de datos.","detail":null,"traceId":"0HNP4AN4HHTEH:00000001"}
HTTP 500
```

Y en el registro del servidor, con el mismo traceId:

```
SqlException (traceId 0HNP4AN4HHTEH:00000001): Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasKpis'.
```

Antes, ese nombre de procedimiento venía **en la respuesta**. El dato no se
pierde: cambia de lado.

**Pruebas** (`Ola12AislamientoYFugasTests.cs`): en Production el `detail` es
null, hay traceId y el nombre del procedimiento **no aparece en ninguna parte**
de la respuesta; en Development sí viaja; y el `detail` de una `AppException`
sigue saliendo en Production.

---

## 6. `@IDEmpresaCS = 1` cableado — CERRADO

### Qué estaba mal

`ReportesVentasRepository.Catalogos.cs:250` pasaba `@IDEmpresaCS = 1` a mano al
catálogo de clientes de reportes — el único filtro de ese método que no iba en 0.
Y no es "0 = todas": el procedimiento lo aplica **siempre** como igualdad cuando
`@IDCliente` es 0 (línea 126 de `sp_n_ConsultaClientes`:
`AND C.IDEmpresaCS = @IDEmpresaCS`). El propio SP documenta los valores:
`0 - Tauro, 1 - Zaragoza, 7 - Luro`.

El número correcto está en la base, en `Constantes.IDEmpresaCS`, que es "qué
empresa es esta instalación":

```
Produccion_svr (Tauro)    -> IDEmpresaCS = 0
MacZ           (Zaragoza) -> IDEmpresaCS = 1
```

### Lo que de verdad pasaba (corrige a la auditoría)

La auditoría decía que en Zaragoza recortaba la lista. Con los datos locales no
es eso: **en Zaragoza daba la casualidad de estar bien** (las 1120 filas de
`Clientes` tienen `IDEmpresaCS = 1`) y **en Tauro el catálogo volvía VACÍO**,
porque sus 4319 clientes tienen `IDEmpresaCS = 0`:

```
0 -> IDEmpresaCS=1 (lo cableado)
1 -> IDEmpresaCS=0 (Constantes de Tauro)
```

(conteo de `Clientes` con `Numero='301'`). Era el 1 de Zaragoza aplicado también
a Tauro.

### Qué se hizo

El parámetro sale de `Constantes.IDEmpresaCS`, leído con
`ConsultarConstantesAsync`, que ya existía en el mismo repositorio. Se deja un
parámetro opcional por si algún día hay que pedir el padrón de otra empresa;
quien no lo pasa obtiene el de la instalación que está contestando.

### Cómo se comprobó

**En vivo**, en Tauro:

```
GET /api/reportes/ventas/acumuladores-productos/clientes?numero=301
[{"idCliente":629,"numero":301,"nombreCliente":"BERNAL NOGUEZ DAMIAN JULIO","nombre":"DAMIAN JULIO"}]
```

Antes del arreglo esa misma llamada devolvía `[]` en Tauro, y el conteo de arriba
dice por qué.

---

## 7. El aislamiento entre empresas — CERRADO (la parte que es código)

### Qué estaba mal

El token trae un claim `companyKey` con la clave de la instalación que lo emitió
(`JwtTokenGenerator.cs:36`). Ese claim **no se comparaba contra nada** en el
pipeline: su único consumidor era `ModulosController`, y sólo para armar la lista
de módulos. Ni `PermisoControllerBase` ni el handler de autorización lo miraban.

Lo que sí se valida —firma, emisor y audiencia— son las **tres constantes
compartidas** de la sección `Jwt` de `appsettings.json`. Así que un token emitido
al entrar en Tauro era un token perfectamente válido para pedirle datos a
Zaragoza. Y como el rol viaja en el token, el `sub` ni se comprobaba contra la
base de destino.

El aislamiento entre empresas era una **convención de despliegue**, no un
control.

### Qué se hizo

Nuevo `Infrastructure/Middleware/CompanyKeyMiddleware.cs`, registrado en
`Program.cs` **entre `UseAuthentication` y `UseAuthorization`**: necesita el
`ClaimsPrincipal` ya armado, y tiene que cortar antes de que cualquier política
de permisos lea el rol del token.

Compara el `companyKey` del token con el de esta instalación
(`dbo.EmpresaWeb(Id=1).Clave`, vía `IEmpresaRepository.GetCompanyKeyAsync` — **lo
mismo que se usa al emitirlo**, y en la misma entrada de caché `"companyKey"` que
ya usaba `UserService`, para que la mitad que firma y la que comprueba no puedan
opinar distinto).

Decisiones:

- Sólo mira peticiones **autenticadas**. Lo anónimo a propósito —el PDF de la
  remisión, el login— no trae token y pasa de largo: ahí lo que autoriza es el
  pase firmado, y eso no se toca.
- Token **sin** el claim: se rechaza. El generador no emite ninguno sin él
  (lanza si viene vacío), así que un token sin claim es un token que no salió de
  aquí.
- Si no se puede resolver la clave de la instalación (la base no contesta), se
  **falla cerrado** con 503. Un candado de aislamiento que se abre cuando la base
  se cae no es un candado.
- Un choque se registra con las dos claves, porque casi siempre significa una de
  dos cosas: alguien reusando un token de otra instalación, o un despliegue
  apuntando a la base equivocada. Las dos hay que verlas.

### Cómo se comprobó

**En vivo.** Se firmó un token a mano con la llave del repo (que es justo el
problema), rol `SuperAdmin`, emisor y audiencia correctos, variando sólo el
`companyKey`, contra una instalación cuyo `companyKey` es `tauro`:

```
companyKey=tauro     -> 200  {"scope":"tenant","companyKey":"tauro","modules":[...]}
companyKey=zaragoza  -> 401  {"ok":false,"message":"Tu sesion es de otra empresa. Inicia sesion en esta."}
sin claim            -> 401  {"ok":false,"message":"Token sin empresa. Vuelve a iniciar sesion."}
anónimo (/remision)  -> 400  "El enlace del reporte no es valido o ya vencio."  (no 401: sigue pasando)
```

El primer renglón es la prueba del "antes": el token forjado **es aceptado** si
dice `tauro`, o sea que la firma, el emisor y la audiencia no paran a nadie. Lo
único que detiene al de `zaragoza` es el candado nuevo.

**Pruebas** (`Ola12AislamientoYFugasTests.cs`): misma empresa pasa; otra empresa
→ 401 y **no llega al endpoint**; la comparación no distingue mayúsculas (un
token viejo no tiene por qué venir normalizado); sin claim → 401; sin token →
pasa; si no se puede resolver la empresa → 503.

### La llave del JWT: NO se tocó, y es del usuario

`appsettings.json` trae `Jwt:Key`, `Issuer` y `Audience` **en claro y en el
repo**. Eso **no se cierra con código**: se cierra **rotando la llave** y
poniendo **una distinta por despliegue** (App Setting de cada Web App de Azure,
`Jwt__Key`). Rotarla **invalida todas las sesiones abiertas** de ese sitio, así
que es una decisión suya y hay que elegir el momento.

Lo que cambia con este candado: aun con la llave compartida, un token ajeno ya no
sirve en otra instalación. El agujero pasa de "cualquiera con el repo entra en
cualquier empresa" a "cualquiera con el repo puede forjar un token **de su propia
empresa**" — que sigue siendo grave y sigue necesitando la rotación.

Queda pendiente, a nombre del usuario:

1. Rotar `Jwt:Key` (una nueva, larga y distinta por cada uno de los 4 sitios).
2. Sacarla de `appsettings.json` y dejarla sólo en App Settings.
3. Avisar: al rotar, todo el mundo tiene que volver a entrar.

---

## Lo que se encontró de camino y NO se arregló

| hallazgo | por qué no se tocó |
|---|---|
| `AutorizarPedidosAsyncCoordinator.GetStatus(operationId)` no comprueba de quién es la operación, no poda `_ops` (fuga de memoria) y no deduplica. | Es un IDOR de la misma familia que el hallazgo 1, pero no estaba en este lote y necesita atacante con el GUID de la operación. Conviene cerrarlo: atar la operación a su `sub` y podar por antigüedad. |
| `GenericHttpResponse.cs:8` tiene propiedades `static` con setter público (fuga entre usuarios de libro) pero está **muerto**. | No estaba en el lote. Borrarlo es lo correcto antes de que alguien lo "aproveche". |
| `CascosCambioService.cs:327`: si falta la configuración, la contraparte por defecto es `MacZ`/`zaragoza`, y esas claves **no están** en `appsettings.json`. | Es dato de despliegue (hay que saber si Azure las inyecta), no código. |
| `ReportesVentasRepository.LegacyParams.cs:1162` fuerza `IDEmpresaCS` a 1 cuando `Constantes` dice 0, pero para un **parámetro de reporte** (`Param15` de las listas de precios), no para un filtro de catálogo. | Puede ser deliberado (el reporte espera 1..N, no 0). Tocarlo sin saber qué imprime el 1244 cambiaría papel que hoy sale bien. Queda señalado. |
| Los `*NextID` (`MAX(Num)+1`). | No se puede cerrar sin cambio en la base. Detalle en §3. |
| El candado de almacén comentado dentro de `sp_n_ConsultaVentas`. | Es la base. Detalle en §1. |

### Un cambio de conducta que hay que saber

`ProveedoresPagosService` lanzaba `Exception` para las validaciones de forma
("IDProveedorPago es requerido"), que el middleware traduce a **500**. Ahora son
`ArgumentException` → **400**. Es lo correcto —un dato que falta no es un error
del servidor— pero cambia el código de respuesta de esas cuatro acciones.

---

## Qué hay que desplegar

Sólo **el backend** (`ISL_Service`), empresa **Tauro** (es la base a la que
apunta el local). No hay script de base en esta ola: **no se modificó nada de la
base ni de `Legacy/`**. El front y el AAB del móvil no cambian.

Después del despliegue, y como tarea suya aparte: **rotar `Jwt:Key`** (§7), que
es lo único de estos siete hallazgos que no se cierra con código.
