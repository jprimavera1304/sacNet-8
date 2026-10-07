# Auditoría de cierre independiente — 7-oct-2026

Auditoría de solo lectura. No se tocó `Legacy/`, ni el código del backend, ni se
hizo ningún commit. No se ejecutó ninguna capacidad marcada `escribe = si` contra
datos vivos. Las únicas escrituras posibles habrían sido en Legacy, y se evitaron
comprobando antes que la fecha de operación ya había rodado (ver abajo).

**Entorno comprobado:**
- Backend nuevo en `127.0.0.1:5055`, arrancado desde `bin/Debug/net8.0/ISL_Service.dll`
  **compilado a las 02:22 del HEAD actual** `0b9a7d8` en `migracion-legacy`, árbol
  limpio (`git status` vacío). El binario ES del HEAD: el commit y el dll tienen la
  misma marca de tiempo. No medí un commit viejo.
- Legacy vivo en `:63479`, respondiendo con datos reales.
- Bases `Produccion_svr` (Tauro, 917 procedimientos) y `MacZ` (Zaragoza, 945).
- `Constantes.FechaOperacion` = **2026-10-07 = hoy** en Produccion_svr antes de
  empezar, así que las llamadas a Legacy NO dispararon el cierre del día. (MacZ está
  en 2026-10-02; no se generó nada contra MacZ.)
- Las 465 pruebas corren verdes; el banco de comparación, 135 de 135.

---

## 0. Veredicto en una línea

Lo más valioso primero: **los seis defectos de la auditoría anterior están
realmente cerrados** — lo comprobé uno por uno, ejecutando. Pero la afirmación de
"488 capacidades implementadas, todo cerrado" sigue sin sostenerse por las **mismas
dos razones estructurales de siempre** (92 capacidades cuelgan de procedimientos que
no existen en ninguna base; los reportes del menú vivo entregan 76 de 99), y **este
pase encontró cosas que la anterior no vio**: un portillo de SuperAdmin cableado a
la empresa 5, el aislamiento entre empresas que depende de una llave JWT compartida,
IDOR en la consulta de ventas/pedidos, una invalidación de caché de permisos que no
invalida nada para los demás usuarios, y la recuperación de ids con `MAX()` que entre
dos usuarios simultáneos entrega el renglón del otro.

Mi número honesto de cobertura **ejecutable hoy**: **~79 %**. Comparado de verdad
contra Legacy con datos: **~48 %**.

---

## 1. Los arreglos de la auditoría anterior: TODOS cerrados (comprobado ejecutando)

| defecto anterior | estado | evidencia ejecutada |
|---|---|---|
| `POST /api/ventas/descuentos/detalle` daba 500 por colisión de rutas | **CERRADO** | Hoy responde **200** con los 129 renglones del descuento 53518. El barrido de colisiones sobre los **709 endpoints** no encuentra ninguna `AmbiguousMatchException`: la única plantilla declarada dos veces (`POST /api/auth/login`) está en `AuthController.cs`, que está **enteramente comentado**, y la ruta la sirve `SesionController`; probada, da 200. |
| `catch (SqlException){}` de saldos regalaba el crédito completo | **CERRADO** | `VentasPedidoCapturaRepository.cs:1425` ya no está vacío: hace `_logger.LogError(...)` y **relanza** `ConflictException` (409). Si `sp_n_rptVentasSaldos` falla, la captura se rechaza en vez de inventar saldo 0. El renglón en ceros solo sobrevive para el caso real de "cero folios pendientes". |
| el filtro por agente fallaba ABIERTO | **CERRADO** | `VentasPedidoCapturaRepository.cs:327` ahora **falla cerrado**: si `sp_w_ConsultaAgenteUsuario` no está desplegado, lanza 409 con el nombre del script que falta, en vez de devolver 0 ("ve a todos"). |
| `PATCH /usuarios/{id}/empresa` daba 500 siempre | **CERRADO** | Probado en vivo: empresa 0 → **400**, guid inexistente → **404**, misma empresa → 200 idempotente, otra empresa → 409. Ya no es un 500 ciego. `UserAdminService.cs:342`. |
| 49 claves de permiso / 11 de centros sin sembrar (403 a todos menos SuperAdmin) | **CERRADO (en Tauro)** | Crucé **las 253 claves** que exigen los `Controllers/` contra `dbo.WPermiso`: **Produccion_svr, 0 faltan** (incluidas `usuarios.password.ver`, `reportes.centros.cambiar` y las 11 `reportes.centros.*`, que la anterior daba por ausentes — están). `SembrarCatalogoCompletoAsync` ahora llama **al arrancar** a `EnsureCentrosPermissionsSyncedAsync` y `EnsureLegacyReportPermissionsSyncedAsync` (`PermissionService.cs:2070-2074`), y un test lo fija. El backend registró "Catalogo de permisos sembrado" al bootear. **MacZ muestra 168 claves ausentes, pero las 168 tienen sembrador** (semilla estática o familia derivada): es porque ninguna instancia ha arrancado contra MacZ (el arnés apunta a Tauro), no un hueco de código. |
| el `Dv1` del QR se recibía y se tiraba | **CERRADO** | `RecaudacionService.cs:107` ahora compara el `Dv1` del QR contra el `DV1` del renglón (`FiltrarPorDigitoVerificador`) y, si no cuadra, no entrega el ticket. Salvedad honesta y escrita: en bases donde la columna `DV1` no existe (p. ej. Produccion_svr) la comprobación no puede correr y se deja pasar, igual que antes; comprobé que ni Produccion_svr ni MacZ tienen columna `DV1` en sus tablas, así que **hoy este candado solo protege donde la columna exista**. El DTO público ya no expone `DV1`/`DV2`. |

**Veredicto de la sección: no encontré ni un solo defecto anterior reabierto.** La
colisión de rutas, el crédito regalado, el filtro abierto, el 500 del PATCH, las
claves de permiso y el dígito del QR están todos arreglados y lo comprobé
ejecutando, no leyendo.

---

## 2. Lo nuevo que encontré (lo que la auditoría anterior no miró)

### 2.1 ALTO — Portillo de SuperAdmin cableado a la empresa 5

`Application/Security/CurrentUser.cs:29`:

```csharp
public static bool IsSuperAdmin(ClaimsPrincipal user)
    => string.Equals(GetRol(user), "SuperAdmin", ...) || GetEmpresaId(user) == 5;
```

Cualquier usuario cuyo `empresaId` sea **5** es SuperAdmin, sin que nadie se lo
conceda. `IsSuperAdmin` gobierna el cortocircuito de **todas** las comprobaciones de
permiso (`PermisoControllerBase.cs:92`), el bypass de `EnsureActorCanManageTarget`
(`UserAdminService.cs:389` — administrar/resetear/**ver la contraseña** de cualquier
usuario) y la lista de empresas (`EmpresaService.cs:22`). Hoy está inerte:
`UsuarioWeb.EmpresaId` es 1 en las dos bases. Pero en **MacZ la empresa 5 es `CS3`,
un centro de servicio real**: el día que a un usuario se le ponga `EmpresaId = 5`,
se vuelve administrador total de esa instalación. Es una constante mágica que no
debería decidir un rol.

### 2.2 ALTO — El aislamiento entre empresas depende de una llave JWT compartida en el repo

`appsettings.json:37-41` trae `Jwt:Key`, `Issuer` y `Audience` **en claro y iguales
para todos los despliegues**. `Program.cs` valida firma, emisor y audiencia — las
tres constantes compartidas. El claim `companyKey` que emite el token
(`JwtTokenGenerator.cs:36`) **no se compara en ningún punto del pipeline** contra la
empresa de la instalación que recibe la petición: su único consumidor es
`ModulosController.cs:29` para armar la lista de módulos; ni `PermisoControllerBase`
ni el handler de autorización lo miran. Consecuencia: **un token emitido por una
instalación es válido tal cual en otra**, y como `SuperAdmin` cortocircuita antes de
tocar la base, el `sub` ni se comprueba contra la base destino. No pude verificar si
en Azure `Jwt:Key` se sobreescribe por variable de entorno y es distinta por sitio
— **de eso depende que esto sea explotable hoy o solo deuda latente**. El arreglo
correcto es amarrar el token a la empresa (rechazar en el pipeline todo `companyKey`
que no sea el de esta instalación) y sacar la llave del repo con una distinta por
despliegue. Mientras no esté, el aislamiento Tauro/Zaragoza es una convención de
despliegue, no un control.

Los mismos pases firmados anónimos (remisión, usados, reportes) se firman con esa
misma llave (`VentasConsultaController.cs:175`, `CascosCambioController.cs:287`,
`ReportesVentasController.cs:356`), así que un pase vale en cualquiera de las cuatro
empresas.

### 2.3 ALTO — IDOR: el actor sale del cuerpo, no del token, en la consulta de ventas/pedidos

`VentasConsultaService.cs:84` (`Prepare`) solo rellena `IDUsuarioActual` desde el
token **si el cuerpo lo trae en 0** — el cuerpo gana. Y en
`VentasConsultaRepository.cs:73`, `@IDUsuario` se arma **directo de `request.IDUsuario`**.
`@IDUsuarioActual` es el que decide el candado de almacén (lo dice el propio código
en `VentasCobranzaConsultaService.cs:18`). Mandando el id de otro usuario en el
cuerpo (y, para `/pedidos`, omitiendo `SoloMisPedidos`), se leen las ventas y los
pedidos de otro. El backend corrigió este patrón en cobranzas ("IDUsuarioActual sale
del token, no del cuerpo") pero **no aquí**. El mismo patrón aparece en escrituras
de pedidos (`VentasPedidosRepository.cs:79-92`: el token es el último recurso para
`IdUsuarioProcesar`/`EquipoProcesar`) y en pagos a proveedores y personas, donde el
`@IDUsuario` de autoría se **exige** del cuerpo (`ProveedoresPagosService.cs:40`,
`PersonasRepository.cs:71`): autoría falsificable.

### 2.4 ALTO — Invalidar los permisos de un rol no invalida la caché de los demás usuarios

`PermissionService` es **Scoped** pero `IMemoryCache` es **Singleton**; el diccionario
`_observedUserCacheKeys` que lleva la cuenta de qué claves hay que limpiar es de
**instancia** (`PermissionService.cs:386`), así que nace vacío en cada petición.
Cuando un admin cambia los permisos de un rol, `InvalidateEmpresaPermissionCache`
(`:1977`) recorre un diccionario que solo contiene, como mucho, la clave del propio
admin: **no quita ni una clave de los demás**. Un usuario con sesión abierta sigue
con los permisos viejos hasta 5 min (TTL activo) o hasta **30 min** por el camino
"stale" si la base falla al reconstruir. Quitar un permiso no surte efecto a tiempo;
concederlo tampoco. Además `InvalidateUserPermissionCache` compensa con una lista
fija de seis nombres de rol: cualquier `RolCodigo` propio del tenant fuera de esa
lista **nunca** se invalida.

### 2.5 ALTO/MEDIO — Ids recuperados con `MAX()` tras el INSERT: entre dos usuarios, te llevas el del otro

Varios `sp_n_` recuperan el id del renglón recién creado con `SELECT MAX(...)` en vez
de `SCOPE_IDENTITY()`, a veces en otra conexión y sin transacción:

- **Reportes de ventas** (`ReportesVentasRepository.LegacyParams.cs`): el web manda
  `@NombreEquipo = "WEB"` fijo para todos, y `sp_n_ActualizarParametro` recupera el
  `IDParametros` con `MAX() WHERE NombreEquipo=@NombreEquipo AND IDUsuario=@IDUsuario`
  (leído del cuerpo del SP). Para usuarios **solo-web sin id legacy** la llave
  efectiva es `("WEB", 0)` para todos: dos usuarios pidiendo un reporte a la vez, y
  el `MAX()` le entrega a uno el `psp` del otro → **abre el PDF con los filtros del
  otro cliente/agente**. El pase firmado se firma sobre el psp equivocado, no salva
  nada. (El propio código ya documenta el riesgo para dos pestañas del mismo usuario
  en `VentasConsultaService.cs:100`.)
- **Alta de usuarios de Mac31** (`SeguridadMacRepository.cs:84`:
  `SELECT ISNULL(MAX(IDUsuario),0) FROM dbo.Usuarios`) y los catálogos
  (`MaestrosRepository.cs:84`, `CatalogosAdminRepository.cs:128`): dos capturistas a
  la vez y a uno se le contesta el id del otro; la pantalla sigue y le escribe
  almacenes y permisos **al usuario del otro**. Enlaza con la regla conocida de "el
  web escribe en legacy".
- **Consecutivos** `*NextID` (`UsuariosAnexosRepository.cs`, recaudaciones,
  aviación): `MAX(Num)+1` sin reservar → dos altas simultáneas, mismo número.

### 2.6 MEDIO — Escrituras multi-SP sin transacción (coincide con lo que Legacy ya tenía, pero migrado tal cual)

El backend nuevo es estrictamente mejor que Legacy aquí (16 sitios transaccionales
donde Legacy tenía 0), pero **ningún método envuelve dos `sp_n_` de escritura
distintos en una transacción**, y `SpLegacyRepositoryBase` abre una conexión por
llamada, así que dos llamadas = dos conexiones = transacción imposible sin rediseño.
Los peores:

1. **Complemento de pago** (`VentasFacturacionService.cs:137-194`): se timbra el CFDI
   ante el SAT (irreversible) y luego un bucle de `sp_n_InsertarVentaFactura` por
   documento, sin transacción. Si falla el 2.º de 3: el pago existe ante el SAT, pero
   Mac31 cree que 2 facturas siguen sin abono → se re-cobra o re-timbra. Solo se
   loguea el huérfano, no se revierte.
2. **Timbrado normal** (`:443-453`): mismo patrón, doble facturación de las remisiones
   no escritas.
3. **Cancelación de remisiones** (`VentasCancelacionService.cs:97-99`): quita del
   concentrado y luego cancela, dos SP por venta, dos conexiones. Si falla el segundo,
   la remisión sale del concentrado pero **sigue viva y por cobrar**, invisible para
   el cobrador — justo lo que el comentario del propio método jura no hacer.
4. **Carga de estado de cuenta bancario** (`OperacionMacRepository.Operaciones.cs:307`):
   el único con transacción, pero el `sp_n_InsertarBancoTransferencia` hace
   `ROLLBACK TRAN` desnudo que revienta la transacción externa de C# → al rechazar un
   movimiento, el usuario recibe 500 en vez del mensaje construido.

### 2.7 MEDIO — El middleware de excepciones filtra SQL en Production

`ExceptionHandlingMiddleware.cs` no tiene **ni un `IsDevelopment()`**: devuelve
`detail = ex.Message` crudo siempre. Comprobado en vivo (entorno Production): pedir
el dashboard sin su SP desplegado contesta
`{"message":"Error de base de datos.","detail":"Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasKpis'."}`.
Se filtran nombres de procedimiento, columnas, y en errores de red el nombre de
servidor y base. Agravado por `ProveedoresPagosService`/`RecaudacionService` que hacen
`throw new Exception(ex.Message)` (pierden el tipo `SqlException` y caen al catch
genérico) y por `ProveedoresPagosController` que devuelve `"Error en la aplicación: "+ex.Message`.
(Swagger, en cambio, está bien: gated por `IsDevelopment()`, 404 en Production.)

### 2.8 MEDIO — Dato de empresa cableado: `@IDEmpresaCS = 1` y el fallback de contraparte

- `ReportesVentasRepository.Catalogos.cs:250`: el catálogo de clientes del módulo de
  reportes pasa `@IDEmpresaCS = 1` a mano (los demás filtros van en 0). El SP sí
  filtra por ese parámetro (comprobado en `sys.sql_modules`). En Zaragoza recorta la
  lista a la empresa 1 y deja fuera CS1..CS5 y LURO, sin error. En Tauro la empresa 1
  está inactiva. Dato equivocado silencioso.
- `CascosCambioService.cs:327`: si falta la config, la contraparte por defecto es
  `MacZ`/`zaragoza`, y esas claves **no están en `appsettings.json`** → salvo que
  Azure las inyecte, Zaragoza concilia cascos contra sí misma.
- `CurrentUser.cs:20`: `GetEmpresaId` hace `int.Parse(val)` sin `TryParse`; un claim
  no numérico tiraría la petición. Menor.

### 2.9 BAJO — estado mutable muerto y riesgos latentes

`GenericHttpResponse.cs:8` tiene propiedades `static` con setter público (fuga entre
usuarios de libro), pero está **muerto** (nadie lo usa, `Response` devuelve `null!`):
conviene borrarlo antes de que alguien lo "aproveche". `Utils/Variables.cs` tiene
`static` que deberían ser `const` pero nadie reasigna. `AutorizarPedidosAsyncCoordinator`
(singleton) no ata la operación a su dueño (`GetStatus` por `operationId` sin
comprobar propiedad), no poda `_ops` (fuga de memoria) y no deduplica.

**Limpio y hay que decirlo:** cero dependencia de que Legacy esté encendido (una sola
llamada HTTP saliente, a Facturapi; cero DLLs de legacy, cero Crystal/SSRS, PDF con
`wkhtmltopdf` empaquetado); los alcances de DI son correctos (solo 3 singletons, los
tres sin estado por-petición); las claves de caché incluyen la dimensión que importa
(agente, filtros) salvo donde da igual (metadatos de esquema por instalación); los
objetos cacheados se clonan antes de mutar.

---

## 3. Lo que sigue sin cerrar, por las razones estructurales de siempre

Esto no es nuevo — la auditoría anterior ya lo dijo y lo confirmo con mis números:

- **92 capacidades cuelgan de 88 `sp_n_` que no existen en NINGUNA de las dos bases**
  (Aviación, Obra partidas/periodos/convenios, Proyectos, cabecera de Descuentos,
  Unidades territoriales, cortes de Recaudaciones). Todas están, limpiamente, en
  `hecho-sin-comparar`; **cero** filas `hecho` cuelgan de un SP inexistente. Pero
  devolverán 500 el día que se llamen, y no hay script para crearlos en el repo (son
  propiedad de legacy). No puedo afirmar si existen en producción; me falta el volcado
  de `sys.procedures` de allá.
- **96 `sp_w_`/`sp_mo_` propios no están en Produccion_svr** (Tauro); 33 sí están en
  MacZ. Entre ellos **los 9 del Dashboard de ventas** (comprobado en vivo: 500
  "Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasKpis'"), el
  Checador web, la captura de pedidos (`sp_w_ConsultaPedido`, `InsertarPedidoDetalle`…),
  Cheques, Tarimas, Torneos, favoritos de módulo y `sp_mo_ConsultaMisPedidos` (móvil).
  **Los 96 tienen script en `MAC/Querrys`** (100 %): es despliegue, no desarrollo —
  pero hoy, contra Tauro, se caen.
- **Reportes: 76 de 99 del menú vivo entregan documento.** Verifiqué las afirmaciones
  SQL de la ola 11: `sp_n_rptVentasEstadoCuenta` tiene **1 parámetro** en las dos bases
  y su envolvente lo llama con 6 (1375/1380 rotos en Legacy también); los 6 spV2 de
  Aviación (1426/1428/1429/1430/1432/1455) **no existen en ninguna base**; los 3 ids
  fantasma (240/76/1242) no tienen renglón en `dbo.Reportes`; `EsCentroServicio = 0`
  en las dos bases; no hay bases `MacZCS*` en el servidor (47 bases, ninguna). Los 23
  que faltan son **deuda de la base o infraestructura ausente, no trabajo pendiente de
  esta migración** — pero el hueco para el usuario es del mismo tamaño.

---

## 4. ¿Las comparaciones prueban algo? 25 comparan listas vacías

El banco: **135 casos, 135 OK, 0 diferencias.** Pero el `ok` de `comparar.py` es
`not diffs and est_leg==200 and est_nue in (200,204)`, así que **un caso que compara
vacío contra vacío pasa sin probar nada**. Conté los renglones de cada caso: **25 de
los 135 salen OK con 0 renglones** (la auditoría anterior contó 29-30 de 133; bajó,
no subió). La mayoría son tablas genuinamente vacías en Tauro y los `_porque` lo
documentan, pero al menos uno es un trámite: `ola3-huellas-usuarios` compara
`UsuariosHuellas` (0 filas en Tauro) teniendo `EmpleadosHuellas` con **409** al lado;
el propio caso admite que solo vale apuntado a MacZ (12 filas), donde no se puede
correr aquí. **En números honestos: de los 135 "en verde", ~110 comparan datos
reales; 25 comparan el vacío.**

De las **349 escrituras**, solo **16** tienen evidencia de haberse comparado en
instantánea (misma huella en la base antes/después); las otras 188 marcadas "comparado
contra Legacy" lo que compararon fue la **respuesta** del endpoint, no el efecto en la
base. El único artefacto de escrituras con efecto-en-base verificado es la ola 4 (17
casos, todos `mismo_efecto_en_base: true`). Las 143 escrituras restantes están en
`hecho-sin-comparar`.

---

## 5. Mi número honesto de cobertura

El tablero dice **296 `hecho` + 189 `hecho-sin-comparar` + 3 `codigo-muerto` = 488,
todo cerrado**. Desglose real:

- De las 296 `hecho`: **249 dicen "comparado contra Legacy"**, 36 tienen
  `metodo_validacion` **vacío** (aunque su `resultado` suele traer evidencia: "3543
  renglones idénticos", etc.), y 11 dicen "cubierto por el módulo equivalente" — que
  es arquitectura, no medición (ahí viven los dos renglones que resumen **todo** el
  motor de reportes).
- De las 189 `hecho-sin-comparar`: **92 cuelgan de SP que no existen en ninguna base**.

Sobre la superficie honesta (~582 unidades: 488 − 2 filas de reporte + 96 nodos vivos
de reporte):

| | unidades | % |
|---|---|---|
| **Comparado contra Legacy con datos reales** (~249 filas − 25 vacías + ~20 reportes de la ola 11 con importes verificados) | **~244** | **~42 %** |
| **Implementable/ejecutable hoy en Tauro** (389 filas ejecutables + 76 reportes; descontando 92 sin SP y los 96 sp_w_ sin desplegar que caen) | **~460** | **~79 %** |
| Implementado pero inejecutable aquí (92 sin SP + ~23 reportes) | ~115 | ~20 % |
| Código muerto bien clasificado | 3 | 0.5 % |

**Cobertura de procedimientos:** de los 447 `sp_` del código de Legacy, **442 los
ejecuta el backend nuevo** (los 5 restantes son sustituciones deliberadas:
`sp_w_InsertarEmpleado`, `sp_w_GuardarAsistenciaDia`, etc., y `sp_n_DevolucionVentaLuro`
no existe en la base). De los 150 de reportes, 4 nombrados (el resto se resuelven
dinámicamente desde `dbo.Reportes`). La cobertura **del código** es excelente; la de
reportes es 76 %.

**El titular honesto no es "488 de 488".** Es: *"~460 de ~582 ejecutables hoy en
Tauro, ~244 comparadas de verdad contra Legacy (25 casos del banco comparan vacío),
92 capacidades condenadas a 500 por SP que no existen, 96 procedimientos propios por
desplegar en Tauro, 23 reportes que no entregan documento (deuda de base/infra), y un
puñado de defectos de seguridad y concurrencia nuevos."*

---

## 6. Veredicto: ¿puede un Web o un Móvil nuevo vivir SOLO de este backend?

# NO — todavía no, pero el "no" es corto y nombrable.

**Lo que impide el SÍ hoy:**

1. **Despliegue (no desarrollo):** faltan 96 `sp_w_`/`sp_mo_` en Tauro — con ellos se
   cae el Dashboard completo, el Checador web, la captura de pedidos y "Mis pedidos"
   del móvil. Los scripts existen en `MAC/Querrys`; es correr el despliegue.
2. **Aislamiento entre empresas (seguridad):** mientras la llave JWT sea compartida y
   el token no se amarre a la empresa, y mientras `empresaId == 5` conceda SuperAdmin,
   un Web/Móvil multi-empresa no es seguro. Son pocas líneas, pero son de bloqueo.
3. **IDOR en consulta de ventas/pedidos y autoría falsificable en pagos:** un Web nuevo
   los heredaría como agujeros propios. Hay que tomar el actor del token, no del cuerpo.
4. **Faltan 23 reportes del menú vivo** (Estado de Cuenta DINERO/CASCOS, Centros,
   Auxiliares de Aviación): 16 están rotos también en Legacy y 7 son infra ausente, así
   que el trabajo es de otros, pero el usuario que los usa no tiene reemplazo.
5. **92 capacidades de Aviación/Obra/Proyectos están escritas y no ejecutan** aquí
   (SP ausentes); Legacy tampoco puede aquí, así que puede no ser deuda de la
   migración — no lo puedo afirmar sin el esquema de producción.

**Lo que ya consta a favor, y es mucho:**

- **Cero dependencia de que Legacy esté encendido.** Verificado: una sola llamada HTTP
  saliente (Facturapi), cero DLLs/Crystal/SSRS/UNC, reportes en proceso con
  `wkhtmltopdf` empaquetado.
- **Los seis defectos de la auditoría anterior están cerrados**, comprobado ejecutando.
- **442 de 447 procedimientos del código de Legacy cableados**; el inventario de
  capacidades es completo; una sola plantilla de ruta duplicada en 709 endpoints, y es
  código comentado.
- **465 pruebas verdes, 135 casos verdes** — con la salvedad de que 25 de esos casos
  comparan listas vacías.

**Para que sea SÍ, en orden:** desplegar los 96 `sp_w_`/`sp_mo_` en Tauro → amarrar el
token a la empresa y sacar la llave JWT del repo + quitar el `empresaId==5` → tomar
`@IDUsuario`/`@IDUsuarioActual` y la autoría del token, no del cuerpo → mover la
invalidación de caché de permisos a estado compartido → cambiar los `MAX()` por
`SCOPE_IDENTITY()` o leerlos dentro de la misma transacción → tapar el `detail`
crudo de SQL en Production → resolver los 23 reportes (base/infra, no código) y
conseguir el esquema de producción para saber el destino real de las 92 de Aviación/Obra.

---

## 7. Lo que no pude comprobar, y qué me falta

| no comprobado | qué hace falta |
|---|---|
| Si los 88 `sp_n_` y los 96 `sp_w_` ausentes existen en **producción real** | volcado de `sys.procedures` de la producción de Tauro y Zaragoza |
| Si `Jwt:Key` se sobreescribe por App Setting en Azure y es distinta por sitio | las App Settings reales de los 4 despliegues; de eso depende que la §2.2 sea explotable hoy o latente |
| Las **349 escrituras** con efecto-en-base | solo se prueban en instantánea; prohibido ejecutar `escribe = si`. El repo conserva ola4 (17) y ola5 |
| Reproducir las 296 comparaciones del tablero contra escrituras | ídem |
| Aviación y centros `MacZCS*` | esas bases no están en esta máquina |
| El cuerpo de los `sp_n_` que reciben `@IDUsuarioActual` | para medir el alcance exacto de la fuga de §2.3 leí los comentarios, no todos los SP |
| Si CADA endpoint de costos aplica el recorte por permiso | no lo recorrí uno por uno |
