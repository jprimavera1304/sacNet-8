# Inventario del backend NUEVO — `sacNet-8/ISL_Service/ISL_Service`

Resumen de lectura del código tal como está hoy (6-oct-2026). Todo lo que sigue sale de
leer los archivos; donde hay una afirmación, va la ruta y el número de línea.

Tamaños reales: **44 archivos de controlador** (3 están comentados por completo), **242 acciones
HTTP** + 2 endpoints minimal API en `Program.cs`, **37 servicios**, **63 repositorios**
(`ReportesVentasRepository` son 7 archivos `partial`), **74 interfaces**, **2 entidades de dominio**.
El detalle acción por acción está en `inventario-nuevo.tsv` (248 filas).

---

## 1. Arquitectura real

### 1.1 Carpetas

Es un **único proyecto web** (`ISL_Service.csproj`, `net8.0`) con las capas separadas por
carpetas, no por ensamblados:

```
ISL_Service/
  Program.cs                     arranque + TODO el registro de DI (443 líneas)
  Controllers/                   44 controladores
  Application/
    DTOs/<Modulo>/               un folder por módulo (41 folders)
    Interfaces/                  I<X>Service e I<X>Repository (74 archivos)
    Services/                    37 servicios
    Security/                    CurrentUser, PermissionRequirement, PermisosDeCentros,
                                 RemisionTicket, ReporteUsadosTicket, PasswordGenerator
    Exceptions/                  AppException + NotFound/Conflict/Unauthorized
    Models/                      PermissionSnapshot, RoleCatalogItem, WebLoginFallbackResult
    Reportes/                    ReportesCatalogo (catálogo de 53 reportes y su permiso)
  Infrastructure/
    Data/                        AppDbContext, BaseDelCentro, Mac3SqlServerConnector
    Repositories/                63 archivos
    Security/                    JwtTokenGenerator, PermissionService (3139 líneas),
                                 PermissionPolicyProvider, PermissionAuthorizationHandler,
                                 CurrentUserAccessor
    Reports/                     constructores de HTML, wkhtmltopdf, renderers PDF/XLSX
    Middleware/                  ExceptionHandlingMiddleware
    Services/                    AutorizarPedidosAsyncCoordinator
  Domain/Entities/               Usuario.cs, EmpresaWeb.cs  (SOLO estas dos)
  Utils/                         Encryptacion, Funciones, Variables (heredado de legacy)
  Assets/Logos/                  Logo_Mac.png, Logo_Zaragoza.png, tauro_logo.jpg
  Assets/Wkhtmltopdf/            wkhtmltopdf.exe + 20 DLLs de runtime de VC/UCRT
  ISL_Service.Tests/             proyecto de pruebas ANIDADO dentro del proyecto web
```

Nota de build: por estar anidado el proyecto de tests, `ISL_Service.csproj:23` tiene que
excluirlo a mano de los globs del SDK Web; el comentario del archivo explica que sin eso un
build incremental pasaba de ~3.7 s a ~11.7 min.

### 1.2 Acceso a datos: **ADO.NET a pelo + stored procedures**

No hay Dapper. EF Core está referenciado y configurado, pero **casi no se usa**:

* `Program.cs:99-100` registra `AppDbContext` con `UseSqlServer`.
* `AppDbContext` solo mapea dos entidades: `Usuario` → `dbo.UsuarioWeb` y `EmpresaWeb` →
  `dbo.EmpresaWeb` (`Infrastructure/Data/AppDbContext.cs:41-68`).
* Los únicos consumidores LINQ/EF son `EmpresaRepository` y partes de `UserRepository`
  (`_db.Usuarios.AsNoTracking()`, `Infrastructure/Repositories/UserRepository.cs:33`).
* Todo lo demás es `Microsoft.Data.SqlClient` directo: `new SqlConnection` →
  `new SqlCommand(nombreDelSP, conn) { CommandType = CommandType.StoredProcedure }` →
  `SqlDataReader` o `DataTable`, y mapeo a mano campo por campo.

El patrón canónico del repositorio (reproducirlo tal cual en código nuevo) es
`Infrastructure/Repositories/TarimaRepository.cs:15-45`:

```csharp
public class TarimaRepository : ITarimaRepository
{
    private readonly IConfiguration _configuration;
    private const string SpConsultar  = "dbo.sp_w_ConsultarTarimas";
    private const string SpInsertar   = "dbo.sp_w_InsertarTarima";
    // ...
    private SqlConnection GetConnection()
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");
        var connector = new Mac3SqlServerConnector(cs);
        return connector.GetConnection;
    }
```

Los nombres de SP van en `private const string Sp*` arriba de la clase. Hay dos fuentes de
conexión en el proyecto y conviven:

1. `IConfiguration.GetConnectionString(...)` + `Mac3SqlServerConnector` — la mayoría de los
   repositorios.
2. `_db.Database.GetConnectionString()` — `PermissionService`, `UserRepository`
   (`Infrastructure/Security/PermissionService.cs:467`).

`Mac3SqlServerConnector` (`Infrastructure/Data/Mac3SqlServerConnector.cs`) es herencia de
legacy y trae dos trucos vivos: si la cadena contiene `Aviacion` la desencripta con
`Utils/Encryptacion`, y si contiene `@Info` lo sustituye por `User Id=sa; Password=Hope5y2k`
(línea 46 — credencial en claro en el repositorio).

Hay **158 nombres de stored procedure distintos** referenciados, en tres familias:

| prefijo | quién lo escribió | se puede tocar |
|---|---|---|
| `sp_n_` (y `sp_ConsultaReportes`) | legacy / Mac31 | **NO**: intocables |
| `sp_w_` | este backend (web) | sí |
| `sp_mo_` | la app móvil (solo `sp_mo_ConsultaMisPedidos`) | sí |

Hay un patrón de **fallback de SP** en captura de pedidos: se intenta el `sp_w_` propio y si
no existe se usa el `sp_n_` de legacy (`VentasPedidoCapturaRepository.cs:789-790`,
`824-825`, `851-852`, `973-974`, `991-992`). Y un patrón de **SP opcional**: se atrapa el
`SqlException` de "procedimiento no existe" y se sigue sin él
(`VentasPedidoCapturaRepository.cs:297` y `503`, `IsMissingStoredProcedure`).

### 1.3 SQL inline

Existe y es legítimo donde el SP no alcanza. Archivos con SQL escrito a mano:
`PermissionService`, `UserRepository`, `AlmacenCascosRepository`, `CatalogosRepository`,
`CategoriasRepository`, `CentrosServicioRepository`, `EquiposRepository`,
`InscripcionesTorneoRepository`, `PersonasRepository`, `PrestamosRepository`,
`ProfesoresRepository`, `VentasCancelacionRepository`, `VentasConsultaRepository`,
`VentasPedidoCapturaRepository`, `VentasSaldosRepository`.

Dos casos repetidos que conviene conocer:

* **No hay SP de "habilitar"** en Categorías, Equipos, Inscripciones y Profesores: el
  `habilitar` se hace con `UPDATE` inline y `CommandType.Text`
  (`CategoriasRepository.cs:128`, `EquiposRepository.cs:148`,
  `InscripcionesTorneoRepository.cs:153`, `ProfesoresRepository.cs:142`).
* **Degradación cuando la tabla no existe en esa base**: primero `SELECT OBJECT_ID(...)` o
  `SELECT COUNT(*) FROM sys.tables` y si no está se devuelve lista vacía
  (`CatalogosRepository.cs:94`, `CentrosServicioRepository.cs:70-74`).

Todo el SQL inline que vi va parametrizado (`SqlParameter` / `AddWithValue`), incluido el
`IN (...)` de listas de ids que se construye con parámetros numerados
(`VentasCancelacionRepository.cs:79`).

### 1.4 Registro de servicios

**Todo a mano en `Program.cs:104-220`**, línea por línea, sin escaneo de ensamblado ni
módulos de registro. Alcance:

* `AddScoped` para prácticamente todos los servicios y repositorios.
* `AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>` (`:119`),
  `AddSingleton<ICurrentUserAccessor, CurrentUserAccessor>` (`:121`),
  `AddSingleton<IAutorizarPedidosAsyncCoordinator, AutorizarPedidosAsyncCoordinator>` (`:168`).
* `AddHostedService<ReportEngineWarmupHostedService>` (`:159`) — calienta QuestPDF y ClosedXML
  2 s después del arranque.
* Dos implementaciones de la **misma** interfaz registradas a propósito, para resolverlas por
  `IEnumerable<>`: `IDashboardVentasReportRenderer` → PDF y XLSX (`:157-158`), que
  `DashboardVentasReportService` convierte en diccionario por `Formato`.
* Tres registros **como clase concreta** (sin interfaz), herencia de la primera época:
  `RecaudacionRepository`/`RecaudacionService` (`:104-105`) y
  `ProveedoresPagosRepository`/`ProveedoresPagosService` (`:107-108`).

### 1.5 Manejo de errores

Un solo middleware, `Infrastructure/Middleware/ExceptionHandlingMiddleware.cs`, montado en
`Program.cs:397` **antes** de `UseAuthentication/UseAuthorization`. Traducción:

| excepción | status | cuerpo |
|---|---|---|
| `AppException` (base propia) | `ex.StatusCode` | `{ code, message, detail }` |
| `AuthenticationException` | 401 | `{ message }` |
| `UnauthorizedAccessException` | 403 | `{ message }` |
| `KeyNotFoundException` | 404 | `{ message }` |
| `ArgumentException` | 400 | `{ message }` |
| `ArgumentException` que huele a cadena de conexión | 500 | `{ message: "Error de configuración…" }` |
| `SqlException` con `Number` 50000–99999 | 400 | `{ message: ex.Message, detail }` — son los `RAISERROR` de los SP |
| `SqlException` otro número | 500 | `{ message: "Error de base de datos.", detail: ex.Message }` |
| cualquier otra | 500 | `{ message: "Error interno.", detail: ex.Message }` |

Las excepciones de dominio viven en `Application/Exceptions/`: `NotFoundException` (404,
code `NO_ENCONTRADO`), `ConflictException` (409, code `CONFLICTO`), `UnauthorizedException`.
El contrato clave para la migración: **un `RAISERROR` de severidad 16 en un SP sale al
cliente como 400 con su texto literal**, y de eso dependen pantallas enteras (devolución,
cancelación, multipago).

Dos fugas de información que hay que conocer: el `detail` con `ex.Message` en los 500, y
`/dbcheck` (`Program.cs:418-437`), anónimo, que devuelve servidor y base y en el catch
devuelve `Results.Problem(ex.Message)`.

### 1.6 Forma de la respuesta JSON — **hay DOS convenciones y no están unificadas**

**A) El sobre `{ ok, message, data }`** — la convención nueva, la que usan los módulos
recientes (ventas, checador, empleados, asistencias, préstamos, cascos a cambio, captura de
pedidos, almacén de cascos parcialmente):

```csharp
// Controllers/VentasDevolucionController.cs:52-53
var data = await _service.VistaAsync(idVenta, ct);
return Ok(new { ok = true, message = "Devolución lista.", data });
```

Variantes vistas: se añaden campos hermanos cuando hace falta
(`Controllers/AsistenciasController.cs:108` → `{ ok, message, data, total, milisegundos }`,
`Controllers/EmpleadosController.cs:60` → `{ ok, message, data, puedeVerSueldo }`).

Importante: en estos módulos **200 no significa éxito del negocio**. `ok` es "la llamada se
atendió"; si el negocio falló, lo dice el payload:
`data.ok` en devolución, `data.cancelado` en pagos, `data.guardo` /
`data.requiereConfirmacion` en multipago, `data.Fallidas` por folio en cancelación.

**B) El DTO desnudo** — los módulos tipo CRUD (categorías, profesores, equipos,
inscripciones, jornadas, temporadas, torneos, cheques, tarimas, dashboard, reportes,
usuarios, personas): `return Ok(lista)`, `return Ok(dto)`,
`return CreatedAtAction(nameof(GetById), new { id = created.Id }, created)`, y el error como
`return NotFound(new { message = "La categoria no existe." })`.

En **ambas** convenciones los errores que devuelve el controlador a mano usan
`{ message }` (y los de permiso, `{ ok = false, message }`). No hay `ProblemDetails`.

Cabeceras: lo sensible lleva `Response.Headers["Cache-Control"] = "no-store"` (saldos,
reportes, remisión), y el dashboard lleva `"public, max-age=60"`.

---

## 2. Autenticación y permisos

### 2.1 Emisión del token

`Infrastructure/Security/JwtTokenGenerator.cs`. HMAC-SHA256 con `Jwt:Key`, **expira en 4
horas** (`:44`). Claims emitidos (`:29-41`):

```csharp
new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),   // Guid de dbo.UsuarioWeb
new("username", user.UsuarioNombre),
new(ClaimTypes.Role, user.Rol),
new("rolLegacy", user.Rol),
new("empresaId", user.EmpresaId.ToString()),
new("companyKey", normalizedCompanyKey),                // minúsculas, obligatorio
// y solo si existe el vínculo con Mac31:
new Claim("idUsuario", user.LegacyUserId.ToString())
```

`companyKey` vacío lanza `InvalidOperationException` al emitir (`:26-27`).
**`idUsuario` es el puente a legacy**: es el `IDUsuario` de `dbo.Usuarios` de Mac31, y es lo
que se escribe como firma en todo lo que toca legacy. Si falta, llega como 0.

### 2.2 Validación

`Program.cs:234-257`. Valida issuer, audience, firma y vigencia, `ClockSkew` 1 minuto, y
—dato que importa— `NameClaimType = "sub"` (`:250`), para que `User.Identity.Name` sea el id.

### 2.3 Login

`Application/Services/UserService.cs:32-90`, con `UserRepository.LoginWithFallbackAsync` →
`dbo.sp_WebLogin_FallbackLegacy`. Dos pasadas:

1. Login web sin hash.
2. Si falla **y** la empresa no es `isl`, se genera un BCrypt del password y se vuelve a
   llamar al SP para **promover** al usuario que solo existía en legacy a `dbo.UsuarioWeb`.

Estados: `1` activo, `2` inactivo ("Usuario inactivo."), `3` bloqueado. Si la fuente es
`WEB` se verifica con `BCrypt.Verify`. El refresh token (`RefreshLifetime` = 60 días,
rolling) **solo se emite si `IncluirRefresh = true`**: lo pide la app móvil, el web no.
`companyKey` se cachea 5 minutos en `IMemoryCache`.

### 2.4 Políticas `perm:...`

El mecanismo entero son tres archivos pequeños:

* **`PermissionPolicyProvider`** (`Infrastructure/Security/PermissionPolicyProvider.cs`),
  registrado como `IAuthorizationPolicyProvider` singleton. Cualquier policy cuyo nombre
  empiece por `perm:` (`PolicyPrefix`, `:9`) se construye al vuelo; lo demás cae al provider
  por defecto. **El prefijo acepta OR con `|`** (`:24-25`):

  ```csharp
  // "perm:a" exige a. "perm:a|b" exige a O b (basta con uno).
  var permissions = policyName[PolicyPrefix.Length..]
      .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  ```

  El caso real que lo motiva es la captura de pedidos:
  `[Authorize(Policy = "perm:ventas.pedidos.crear|app_movil.pedidos")]`
  (`Controllers/VentasPedidoCapturaController.cs:111`).

* **`PermissionRequirement`** (`Application/Security/PermissionRequirement.cs`): lista de
  permisos aceptados; con tener **uno** alcanza.

* **`PermissionAuthorizationHandler`** (`Infrastructure/Security/PermissionAuthorizationHandler.cs`):
  1. `rolLegacy == "SuperAdmin"` → pasa sin consultar nada (`:28-32`).
  2. Pide el snapshot a `IPermissionService`.
  3. Si `snapshot.PermissionsEnabled`, compara **sin distinguir mayúsculas** y además aplica
     un atajo: un permiso `x.ver` satisface `x.ver_modulo` (`CanUseLegacyVerAsModuleView`,
     `:64-72`).
  4. Si el tenant **no** tiene el modelo de permisos, cae a `IsAllowedByLegacy(rol, permiso)`,
     que es un diccionario estático rol→permisos y que **devuelve `true` cuando el permiso no
     está en el mapa** (`PermissionService.cs:395-405`). O sea: permiso desconocido = permitido.

### 2.5 La vía alterna: comprobación a mano

**No todos los endpoints usan `[Authorize(Policy=...)]`.** Hay tres estilos vivos y conviene
no inventar un cuarto:

1. **Policy declarativa** `[Authorize(Policy = "perm:x.y")]` — módulos CRUD, dashboard,
   reportes, usuarios, permisos web, personas.
2. **`PermisoControllerBase`** (`Controllers/PermisoControllerBase.cs`) — Empleados,
   Asistencias, Préstamos, CascosCambio, VentasCancelación/Devolución/Pagos/MultiPago/Usados,
   ReportesVentas. Su propio doc-comment explica por qué: *"el modelo de permisos es POR
   EMPRESA y vive en la base: una política estática no sabe si el tenant siquiera lo tiene
   prendido"*. Da:
   * `ExigirPermisoAsync(string[] permisos, ct)` → `IActionResult?` (null = pasa).
   * `TienePermisoAsync(...)` → `bool`, **no bloquea**: se usa para recortar campos de dinero
     del DTO (`EmpleadosController.cs:58-60`, sueldos).
   * `LeerPermisosAsync(ct)` → un **solo** snapshot para decidir decenas de permisos (el menú
     de reportes son 53; llamar 53 veces preguntaría 53 veces).
   * `IdUsuarioLegacy()` y `Equipo()` — la firma que se escribe en legacy, **siempre del
     token, nunca del body**.
   * Compara además **sin separadores**: `ventas.cancelar` == `ventas_cancelar` ==
     `"Ventas Cancelar"` (`SinSeparadores`, `:131-140`), a juego con la función `firma` del
     front (`nucleo/permisos.ts`).
3. **Copias a mano** anteriores a la clase base: `ChecadorController.cs:49`,
   `VentasPedidosController.cs:62`, `UsuariosController.cs:42`. Son equivalentes salvo
   detalles; `UsuariosController` es el único que, cuando el tenant no tiene permisos, **no**
   deja pasar a todos sino solo a Admin/SuperAdmin, porque su endpoint devuelve contraseñas
   en claro (`:70-95`).

En los tres casos, cuando el tenant no tiene el modelo de permisos **se deja pasar** (salvo
la excepción citada). Eso es deliberado y está documentado en el código.

### 2.6 Dónde vive el catálogo de permisos

En **dos** lugares, y hay que entender los dos:

* **En la base**: `dbo.WPermiso` (clave, nombre, descripción), `dbo.WRol`,
  `dbo.WRolPermiso` (rol→permiso), `dbo.WUsuarioPermiso` (override por usuario, con
  `Tipo` allow/deny), `dbo.WModulo` (módulo prendido/apagado) y `dbo.WConfiguracionEmpresa`
  (el interruptor general, clave `autorizacion.capacidades`, `PermissionService.cs:20`).
  Todo por `EmpresaId`.
* **En el código**, como semilla: `PermissionService.PermissionCatalogSeeds`
  (`Infrastructure/Security/PermissionService.cs:326-393`) y
  `Application/Reportes/ReportesCatalogo.cs` para los 53 reportes.

`PermissionService` tolera esquemas distintos entre bases: `GetSchemaAsync` descubre el
nombre real de cada columna (`:1739-1743`): `WRolPermiso.WRolId` **o** `RolId`,
`WPermiso.Id`/`WPermisoId`/`PermisoId`, `WUsuarioPermiso.UsuarioWebId`/`UsuarioId`/`UserId`/
`WUsuarioId`. Ese descubrimiento se cachea (`permissions:schema:v2`).

### 2.7 Cómo se resuelven los permisos efectivos

`LoadEffectivePermissionsAsync` (`PermissionService.cs:1561-1612`) es **un solo CTE**:

```sql
;WITH RolePermisos AS (   -- WRol -> WRolPermiso -> WPermiso, por EmpresaId y Codigo del rol
), Permits AS (           -- WUsuarioPermiso con LOWER(Tipo) IN ('permit','allow','a')
), Denies AS (            -- WUsuarioPermiso con LOWER(Tipo) IN ('deny','block','d')
), PermisosUnidos AS ( SELECT Clave FROM RolePermisos UNION SELECT Clave FROM Permits )
SELECT pu.Clave FROM PermisosUnidos pu
LEFT JOIN Denies d ON d.Clave = pu.Clave
WHERE d.Clave IS NULL ORDER BY pu.Clave;
```

Es decir: **rol ∪ allow del usuario − deny del usuario**. Y después se quitan los permisos
de módulos apagados (`:1646-1660`), con una regla que tiene trampa y por eso es pública y
está probada (`EsDeModuloApagado`, `:1664-1672`): el módulo es lo que va **antes del primer
punto** y se compara **completo**, no con "empieza con" — si no, apagar `reportes` apagaría
`reportesx` y apagar `ventas` se llevaría `ventas_moviles`.

Decisión de diseño importante: **apagar un módulo ya no borra filas**. Antes apagar un módulo
borraba `WRolPermiso` y `WUsuarioPermiso` (era lo único que de verdad quitaba el acceso) y
no se podía deshacer; ahora el filtro se aplica al leer, así que es reversible
(el comentario largo está en `PermissionService.cs:1646-1660`).

**El snapshot ESCRIBE.** `BuildSnapshotAsync` (`:1414-1480`) llama a
`EnsureLegacyReportPermissionsSyncedAsync`, `EnsureVentasPermissionsSyncedAsync` y
`EnsureLegacyModulePermissionsSyncedAsync` (Empleados y Préstamos), que siembran
`WPermiso`/`WModulo` y pueden llamar a `dbo.sp_n_ActualizarUsuarioFormaProceso`
(`:2327`, `:2599`) — o sea que **calcular permisos puede escribir en legacy**.

### 2.8 El puente con Mac31: qué manda legacy y qué manda el web

Esta es la parte más delicada y la que más rompe. Para cuatro familias de permisos —
`reportes.*`, `ventas.*`, `empleados.*`, `prestamos.*` — **manda Mac31**: al leer se BORRA del
snapshot lo que diga el web y se recalcula desde los botones de la forma de legacy
(`n_Procesos` / `UsuarioFormaProceso`). El mapeo es explícito, botón por botón:

```csharp
// Infrastructure/Security/PermissionService.cs:67-92
private const string VentasLegacyForm = "CONSULTA DE VENTAS";
["btnBuscar"]          = ("ventas.consultar",        "Ventas - Consultar"),
["btnNuevoPedido"]     = ("ventas.pedidos.crear",    "Ventas - Crear pedido"),
["btnCancelarVenta"]   = ("ventas.cancelar",         "Ventas - Cancelar"),
["btnMultiPago"]       = ("ventas.pagos.multi",      "Ventas - Multi pago"),
// ... 23 botones
```

Las formas de legacy se buscan **por su Descripción**, no por su nombre: `"CONSULTA DE
VENTAS"` (`:66`), `"CONSULTAR EMPLEADOS"` (`:122`), `"CONSULTA DE VENTAS PAGOS"` (`:199`),
`"CONSULTAR PRÉSTAMOS"` (`:218`, con la É acentuada de la columna varchar).

La consecuencia práctica: **si un permiso de esas cuatro familias no está en la lista blanca
del web, darlo desde la pantalla de permisos NO sirve de nada** — se guarda en
`WUsuarioPermiso` y desaparece en el siguiente cálculo, sin error y sin rastro. Las listas
blancas son `VentasWebPermissionSeeds` (`:101-110`), `EmpleadosWebPermissionSeeds`
(`:155-159`), `ReportesWebPermissionKeys` (`:45-46`) y el prefijo de centros
(`PermisosDeCentros.Prefijo`, `:57`). Cada una tiene su cicatriz documentada arriba.

### 2.9 Convenciones de nombres de permiso

* Forma general **`modulo.accion`**, todo en minúsculas y con puntos:
  `categorias.crear`, `ventas.pagos.cancelar`, `usuarios.estado.editar`.
* **`modulo.ver_modulo`** es la llave del WEB que decide si la tarjeta sale en el inicio. No
  existe en Mac31. `ModulosController` la compara **exacta** contra el snapshot (ahí no
  aplica el atajo `.ver` → `.ver_modulo` del handler).
* El canal móvil tiene permisos **separados**, con prefijo `app_movil.`:
  `app_movil.empleados.ver`, `app_movil.pedidos`, `app_movil.cascos_cambio.crear`. La razón
  está escrita: *"el teléfono se presta y la computadora de la oficina no"*.
* Reportes: **`reportes.{grupo}.{clave}.ver`**, donde grupo ∈ {ventas, compras, inventario,
  administracion} (`Application/Reportes/ReportesCatalogo.cs:29-32`), con excepciones
  documentadas una por una porque la fila de `WPermiso` es más vieja que el texto del nodo en
  legacy.
* Centros de servicio: `reportes.centros.cambiar` (el interruptor del selector) y
  `reportes.centros.{Numero}.ver` por centro — se usa el **Número** (2004 = ORIENTE), no el
  `IDCentro`, porque es lo que la gente reconoce y no cambia entre bases
  (`Application/Security/PermisosDeCentros.cs:31`).
* Rol: el claim `rolLegacy`/`ClaimTypes.Role` con valores `SuperAdmin`, `Admin`, `User` y los
  roles por empresa de `dbo.WRol`. `SuperAdmin` pasa todo; `CurrentUser.IsSuperAdmin` además
  trata `EmpresaId == 5` como superadmin (`Application/Security/CurrentUser.cs:28-30`).

### 2.10 Endpoints sin candado (inventario de riesgo)

* **Anónimos a propósito, con pase firmado**: `/remision` y
  `api/ventas/consulta/reporte/pdf` (`RemisionTicket`), `/usados` (`ReporteUsadosTicket`).
  Los dos validan un HMAC con `Jwt:Key` y vigencia corta. El motivo: una pestaña nueva del
  navegador no manda el header `Authorization`.
* **Anónimo SIN pase**: `api/reportes/ventas/ReportesV3W` (y sus alias `ReportesV2` y
  `acumuladores-productos/pantalla`), `Controllers/ReportesVentasController.cs:282-285`. Solo
  pide `psp` (el id de parámetros) y `c`. Quien adivine un `psp` ve el reporte.
* **Anónimo a propósito**: `api/app/version` (la app pregunta antes del login),
  `api/Recaudaciones/{qr}` (ticket por QR, autoriza el propio QR cifrado).
* **Sin `[Authorize]` y escribiendo**: `ProveedoresPagosController` completo (4 endpoints,
  incluidos insertar/actualizar/cancelar) y `RecaudacionesController`.
* **Minimal API anónimos**: `/whoami` y `/dbcheck` (`Program.cs:404` y `:418`).
* `SesionController` no lleva ni `[Authorize]` ni `[AllowAnonymous]`: funciona porque no hay
  fallback policy global (`Program.cs:259` es un `AddAuthorization()` sin opciones).

---

## 3. Reportes: cómo está hecho hoy

Hay **tres motores distintos**, y no es accidente.

### 3.1 Reportes legacy (los 53 del módulo Reportes) — HTML de plantilla + wkhtmltopdf

El flujo tiene **dos llamadas** y eso es parte del diseño:

1. `POST .../generar` **no genera nada**: resuelve el `IDReporte`, arma los parámetros al
   estilo legacy, los **guarda en la base** con `sp_n_ActualizarParametro` y devuelve la URL
   del documento (`ReportesVentasRepository.cs:116-137`). La URL se arma con
   `Request.Scheme`/`Host`/`PathBase`, no con configuración, para que valga igual en local,
   detrás de proxy y en producción (`ReportesVentasController.cs:447-453`):

   ```csharp
   return $"{Request.Scheme}://{Request.Host}{pathBase}/api/reportes/ventas/ReportesV3W?psp={psp}{centro}";
   ```

2. `GET .../ReportesV3W?psp=&c=` entrega el documento. Pasos
   (`ReportesVentasRepository.Rendering.cs` + `.cs:355-440`):
   * `sp_n_ConsultaParametros` → el renglón guardado.
   * `sp_ConsultaReportes` → la **configuración del reporte**: `NombreReporte`,
     `StoredProcedure` (el SP que trae los datos) y `Aspx` (qué formato de plantilla usar).
   * si la salida guardada es Excel (`3`): `LegacyPlainXlsxReportRenderer` con **ClosedXML**,
     usando una lista de columnas tipada por reporte
     (`ReportesVentasRepository.Constants.cs`, p.ej. `VentaAcumuladoresExcelColumns`).
   * si no: `sp_n_ConsultaTemplateHtml` → plantillas, `sp_n_ConsultaConstantes` → logos y
     rutas, se ejecuta el `StoredProcedure` del reporte y se sustituyen los `#Tags#` del
     HTML (`#Logo#`, `#Titulo#`, `#Detalle#`, `#Totales#`, `#NumPagina#`, `#TotalPaginas#`,
     `#fechaInicial#`, `#ParametrosTexto#`, `#fechaImpresion#`, `#LogoWM#`…). Hay tres
     generadores según `config.Aspx`: normal, `3columnas` y listas de precios Zaragoza
     (`ReportesVentasRepository.cs:496-510`).
   * el HTML se convierte a PDF con `WkhtmltopdfHtmlPdfRenderer.RenderAsync`.

**Resolución de plantillas**: viven en la tabla `Template_Html` de la base, no en el código.
Eso es intencional en legacy (el formato se ajusta sin recompilar) y aquí se respetó: *"así
el web y Mac31 imprimen el MISMO papel aunque alguien ajuste una plantilla mañana"*
(`Infrastructure/Reports/RemisionPlantillas.cs:1-22`).

**Resolución de logos / "fuentes"** (`ReportesVentasRepository.Rendering.cs:60-105`): de
`sp_n_ConsultaConstantes` salen `PathImagenes` + `LogoMacReportes` y `LogoMacWM`. Con eso:

```csharp
private static string ResolveLogoForHtml(string logo)
{
    if (trimmed.StartsWith("data:")) return trimmed;            // ya es data-uri
    var localLogo = TryImageFileToDataUri(trimmed);             // archivo del servidor
    if (!string.IsNullOrWhiteSpace(localLogo)) return localLogo;
    return FallbackLogoDataUri.Value;                           // Assets/Logos/Logo_Zaragoza.png
}
```

El logo se **incrusta como data-URI base64** en el HTML (wkhtmltopdf no necesita salir a
buscarlo). El fallback es `Assets/Logos/Logo_Zaragoza.png`
(`ReportesVentasRepository.Constants.cs:98`) y es `Lazy<string>`, se lee una vez.
El porqué de empaquetar los logos está en `ISL_Service.csproj:55-66`: en Mac31 los reportes
los leen de `C:\Mac21\imagenes\` / `C:\MacZ\imagenes\`, pero este backend corre en Azure
donde esa carpeta no existe y el reporte salía **sin logo**; se empaquetan con **el mismo
nombre de archivo** que tienen allá para que la búsqueda sea directa y no haya tabla de
equivalencias.

De tipografías no hay nada embebido: el HTML declara familias del sistema
(`Tahoma, Verdana, Arial, sans-serif` en `UsadosLegacyHtmlBuilder.cs:120-121`,
`'Segoe UI', Tahoma, Arial` en `UsadosHtmlBuilder.cs:381`). Lo único que se controla aparte
es la fuente del **pie**, porque wkhtmltopdf dibuja el pie con su propia tipografía (Arial) y
no con la del documento: `Opciones.PieFuente` → `--footer-font-name`
(`WkhtmltopdfHtmlPdfRenderer.cs:90`, `:206-209`).

**El renderer de PDF** (`Infrastructure/Reports/WkhtmltopdfHtmlPdfRenderer.cs`) merece
leerse entero antes de tocar reportes. Es un `static class` que lanza el ejecutable
empaquetado:

* `private const string WkhtmltopdfPath = "Assets/Wkhtmltopdf/wkhtmltopdf.exe"` (`:7`), resuelto
  contra `AppContext.BaseDirectory`. Es un **proceso externo de Windows con 20 DLLs** al lado:
  esto ata el backend a Windows / App Service Windows.
* Escribe el HTML a `%TEMP%/isl-reportes/{guid}.html`, corre el exe, lee el PDF y borra los
  temporales en un `finally`.
* Argumentos: `--quiet`, `--title` (sin él la pestaña del navegador decía literalmente "pdf",
  porque wkhtmltopdf ignora el `<title>` del documento), `--enable-local-file-access`,
  `--orientation` (default `Landscape`), márgenes **4/6/2/8 mm por omisión** — que son los de
  la remisión y no se tocan.
* `Opciones` (record opcional) permite por reporte: márgenes propios, `--image-dpi` +
  `--image-quality 100` (el default 600 dpi dejaba el logo lavado), pie real de página
  `--footer-left/--footer-right` con `[page]`/`[topage]`, `--footer-line`, y un
  **encabezado repetido en cada hoja** vía `--header-html` (se recibe el HTML ya armado y el
  renderer lo escribe a archivo, porque la opción pide una ruta). Las marcas `[page]` y
  `[topage]` **solo valen en encabezado y pie**; en el cuerpo se imprimen literales.

**Multi-empresa en reportes y el sello de centro**: si la petición trae `idCentro > 0`, la
conexión se reapunta a la base de ese centro (`BaseDelCentro`, §4) y el reporte se **sella**:
`Infrastructure/Reports/SelloDeCentro.cs` inserta `CENTRO: X` en el encabezado del HTML ya
armado, junto a "Impresión:", y añade `- Centro X` al nombre del reporte. Legacy no lo tiene
porque allá cada centro es una instalación distinta; con un selector el mismo reporte de dos
centros se vería idéntico con números distintos. Si la plantilla no trae el ancla
"Impresión:", pone un aviso naranja arriba: *"vale más verlo feo que no verlo"*. Solo sella
cuando **no** es la matriz.

### 3.2 La remisión de ventas — HTML propio + wkhtmltopdf

`Application/Services/RemisionImpresionService.cs` +
`Infrastructure/Repositories/RemisionImpresionRepository.cs` +
`RemisionHtmlBuilder` / `RemisionZaragozaHtmlBuilder` / `RemisionFormatoLegacy` /
`RemisionNumeroALetra`.

Antes este papel lo pintaba **MacReportes** (el servidor de reportes viejo) y había que
mantener esa aplicación encendida; ahora se arma aquí llamando a los mismos `sp_n_`:
`sp_n_VentasInformacion`, `sp_n_VentasDetalle`, `sp_n_VentasUsadosCargo`,
`sp_n_VentasUsadosCredito`, `sp_n_VentasDevolucion`, `sp_n_ConsultaTemplateHtml`,
`sp_n_ConsultaConstantes`.

Dos juegos completos de plantillas en `Template_Html` (`IDTipoTemplateHtml 1001`): sin
prefijo = primera impresión, con prefijo **`reimp_`** = reimpresión. Cuál se usa lo decide la
columna `Reimpresion` de `sp_n_VentasInformacion`. Si falta **una** plantilla se lanza
excepción con su nombre, a propósito: *"una plantilla que no está deja un pedazo entero fuera
y una remisión con menos conceptos de los que se entregaron no la nota nadie hasta que el
cliente reclama"* (`RemisionPlantillas.cs:40-47`).

**Zaragoza imprime otro reporte**, no la remisión de Tauro con otro logo: va por
`sp_n_rptVentasRemisionSinPrecio` + `RemisionZaragozaHtmlBuilder`
(`RemisionImpresionRepository.ConsultarDatosVentaZaragozaAsync`).

Los márgenes y la orientación se copiaron de `ReportesController.MostrarPdf` de
`Legacy/MacReportes2` para que el papel salga idéntico (`RemisionImpresionService.cs:21-27`),
y el nombre de archivo de descarga imita el de MacReportes: `tauro_<folio>_<cliente>`
(`:183`).

Flujo de autorización en dos tiempos (porque el navegador obliga): `POST reporte/preparar`
contesta todas las preguntas de Mac31 (¿pide contraseña?, ¿es reimpresión?, ¿hay bloqueos?)
→ `POST reporte` revalida todo y **firma un pase** (`Application/Security/RemisionTicket.cs`,
HMAC con `Jwt:Key`) → `GET /remision?t=...` entrega el PDF. En ese GET anónimo el usuario y
el equipo salen **del pase**, no de la petición: si se usara `GetUsername` se escribiría en la
venta el nombre de la máquina del **servidor**.

### 3.3 Dashboard de ventas — QuestPDF / ClosedXML (sin HTML)

El único reporte que **no** pasa por wkhtmltopdf.
`DashboardVentasReportService` resuelve el renderer por formato desde
`IEnumerable<IDashboardVentasReportRenderer>`:

* `DashboardVentasPdfReportRenderer` → **QuestPDF** (`QuestPDF.Settings.License =
  LicenseType.Community`, `:18`; `Document.Create(...).GeneratePdf()`, `:311`).
* `DashboardVentasXlsxReportRenderer` → **ClosedXML** (`XLWorkbook`, hojas Resumen + por año).

`formato` acepta `pdf` (default), `xlsx` y el alias `excel`; cualquier otra cosa es
`ArgumentException` → 400. `POST reporte/prewarm` genera y tira el resultado para calentar.
Y `ReportEngineWarmupHostedService` (`Program.cs:159`) calienta los dos motores 2 s después
del arranque, en un `Task.Run` que se traga los errores.

### 3.4 Cascos a cambio — HTML propio + wkhtmltopdf, con pase

`UsadosLegacyHtmlBuilder` / `UsadosHtmlBuilder` + `WkhtmltopdfHtmlPdfRenderer`, mismo patrón
de pase firmado (`ReporteUsadosTicket`) y ruta corta anónima `/usados?t=...`. El logo y el
nombre de empresa salen de `SELECT TOP 1 PathImagenes, LogoMacReportes, FacturasCorreoNombre
FROM dbo.Constantes` (`CascosCambioRepository.cs:109`) con el mismo fallback a `Assets/Logos`
(`CascosCambioService.cs:236`).

---

## 4. Cadenas de conexión y multi-empresa

### 4.1 Una cadena, cuatro nombres

`Program.cs:79-96` elige la **primera no vacía** de, en este orden:
`ConnectionStrings:Main` → `Mac3` → `Local` → `Default`. Si no hay ninguna, el arranque
lanza `InvalidOperationException`. El mismo orden está **repetido a mano** en cada
repositorio que no usa el `DbContext` (`GetConnection()`).

`appsettings.json` tiene hoy **una sola** `Main` activa y las demás comentadas, y así se
cambia de empresa en local:

```jsonc
// ISL_Service/ISL_Service/appsettings.json:46-57
"ConnectionStrings": {
  //SL
  //"Main": "Server=LAP-JUAND;Database=SportsLeague;…"
  //Tauro
  "Main": "Server=LAP-JUAND;Database=Produccion_svr;User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;"
  //Zaragoza
  //"Main": "Server=LAP-JUAND;Database=MacZ;…"
  //Wahub7
  //"Main": "Server=LAP-JUAND;Database=Wahub;…"
}
```

(El archivo trae además un bloque comentado al principio con cadenas de producción y
contraseñas. `appsettings.json:11` y `:37` tienen la `Jwt:Key` en claro.)

### 4.2 Multi-empresa: **una instalación por empresa**

El modelo no es multi-tenant dentro del proceso: **cada empresa es un App Service con su
propia cadena**. El pipeline lo confirma — `.github/workflows/main.yml` hace **un build** y
**cuatro deploys** del mismo artefacto: `sac26`, `mac-tauro`, `mac-sportsleague`,
`mac-zaragoza` (líneas 82, 110, 138, 166).

Dentro del proceso, "qué empresa soy" se responde por tres vías distintas:

1. **`companyKey`** — `dbo.EmpresaWeb(Id = 1).Clave`, normalizada a minúsculas. Lo lee
   `EmpresaRepository.GetCompanyKeyAsync` con `EMPRESA_ID_FIJA = 1` y **lanza excepción si
   está vacía**; viaja en el JWT y en `/api/me`. Valores vistos: `isl`, `zara`.
2. **`empresaId`** (claim) — todas las tablas `W*` de permisos filtran por `EmpresaId`.
3. **`Constantes.Funcionalidad` / `EsCentroServicio`** de legacy — las dos variables que
   Mac31 lee al arrancar, y que aquí deciden reglas de negocio: qué SP de ajuste de usados se
   usa (`sp_n_VentasUsadosCreditoAjusteZara` vs `...Ajuste`,
   `VentasUsadosRepository.cs:233`), si la impresión pide contraseña
   (`VentasImpresionReglas`), si aplica el filtro de aceites
   (`VentasPedidoCapturaRepository.cs:602`).

`CompanyId` de la configuración solo lo usa `/whoami`; no entra en ninguna decisión.

### 4.3 Varias bases en la misma petición: `BaseDelCentro`

Lo usan **solo los reportes**. `Infrastructure/Data/BaseDelCentro.cs` reescribe la cadena
cambiando únicamente el catálogo:

```csharp
// BaseDelCentro.cs:32-45
public static string Apuntar(string cadenaOriginal, string? baseDeDatos)
{
    var destino = (baseDeDatos ?? "").Trim();
    if (destino.Length == 0) return cadenaOriginal;
    if (!EsNombreDeBaseValido(destino))
        throw new ArgumentException($"Nombre de base no valido: '{destino}'.", nameof(baseDeDatos));
    var builder = new SqlConnectionStringBuilder(cadenaOriginal) { InitialCatalog = destino };
    return builder.ConnectionString;
}
```

Tres decisiones a respetar: se usa `SqlConnectionStringBuilder` y **no** reemplazo de texto
(la cadena puede decir `Database` o `Initial Catalog`, y un `;` dentro del nombre crearía
opciones nuevas); el nombre de la base **nunca viene del cliente** (el front manda un
`IDCentro` y `CentrosServicioRepository` lo traduce contra `dbo.CentrosServicio`); y aun así
se valida con lista blanca (solo letras, números y `_`, máx. 128). Está probado en
`BaseDelCentroTests.cs`.

El catálogo de centros degrada abierto: si `dbo.CentrosServicio` no existe o el `SELECT`
truena, se devuelve lista vacía, el selector no se pinta y los reportes de la principal
siguen igual (`CentrosServicioRepository.cs:60-105`).

Otra base más: `CascosCambio:BaseContraparte` + `CascosCambio:EmpresaContraparte`
(`CascosCambioService.cs:324-325`) para sincronizar los precios de la otra empresa.

### 4.4 CORS

`Program.cs:262-374`, una sola política `"DevCors"`. Orígenes de `AllowedOrigins` (lista por
comas, variable de aplicación en Azure), más —solo en Development— cualquier puerto de
loopback (`EsDeEstaMaquina`, `:335-342`), más los previews de Azure Static Web Apps por
prefijo (`IsAllowedAzurePreviewOrigin`, `:348-361`, con cuatro prefijos por defecto en el
código). El comentario de `:318-334` cuenta por qué se añadió el loopback: el front se mudó
de puerto, `/api/me` murió por CORS y la página volvía al inicio **sin un solo error visible**.

---

## 5. Convenciones que el código nuevo DEBE respetar

### 5.1 Idioma

**Español, sin acentos, en todo lo que es de negocio**: controladores (`ConsultarAsync`,
`CrearAsync`, `InhabilitarAsync`, `CambiarStatusAsync`), DTOs (`IdTarima`, `NombreTarima`,
`NumeroCascosBase`), rutas (`api/cascos-cambio`, `api/generacion-rol-torneo`), permisos.
**Inglés solo** en lo que es puro andamiaje de ASP.NET o heredado: `Program.cs`, `GetById`,
`List`, `Create`, `Update`, `IJwtTokenGenerator`, `PermissionService`, `CapabilitiesResponse`.
Los comentarios son **todos en español**. Mezclar es la norma del proyecto, no un descuido —
pero si un módulo nuevo es de negocio, va en español.

Importante: los **nombres de miembro** evitan acentos (`Inhabilitar`, `Cancelacion`), pero
los **textos para el usuario** sí los llevan (`"La categoria no existe."` vs
`"Token inválido."`, `"Devolución lista."`). No está uniformado.

### 5.2 Estructura de carpetas para un módulo nuevo

Para un módulo `Foo` hay que crear, con estos nombres exactos:

```
Application/DTOs/Foo/FooDto.cs, CreateFooRequest.cs, UpdateFooRequest.cs, InhabilitarFooRequest.cs
Application/Interfaces/IFooService.cs, IFooRepository.cs
Application/Services/FooService.cs
Infrastructure/Repositories/FooRepository.cs
Controllers/FooController.cs
Program.cs: builder.Services.AddScoped<IFooRepository, FooRepository>();
            builder.Services.AddScoped<IFooService, FooService>();
```

Siempre interfaz + implementación y siempre `AddScoped` (salvo que de verdad necesite estado
compartido). Un DTO por archivo cuando es CRUD; los módulos grandes agrupan en un solo
archivo `FooDtos.cs` con comentario de bloque arriba (`Application/DTOs/VentasMultiPago/VentasMultiPagoDtos.cs`).

### 5.3 Forma de los DTO

```csharp
// Application/DTOs/Tarima/TarimaDto.cs
namespace ISL_Service.Application.DTOs.Tarima;   // file-scoped namespace

/// <summary>
/// DTO de lectura para una tarima.
/// IdStatus: 1 = Activo, 2 = Cancelado.
/// </summary>
public class TarimaDto
{
    public int IdTarima { get; set; }
    public string NombreTarima { get; set; } = string.Empty;   // string NO nulo: inicializado
    public string? Observaciones { get; set; }                 // nulo de verdad: anotado
    /// <summary>1 = Activo, 2 = Cancelado</summary>
    public int IdStatus { get; set; }
    public DateTime? FechaCancelacion { get; set; }
}
```

Reglas:
* `class` con `{ get; set; }` — **no** `record`, **no** `init`. (Los `record` se usan solo para
  tipos internos: `PermissionSeed`, `LegacyModuleBinding`, `ReporteCatalogoItem`,
  `PermisosDelUsuario`.)
* `Request` para entrada (`CreateFooRequest`, `UpdateFooRequest`), `Dto` o `Response` para
  salida.
* Serialización por defecto de `System.Text.Json` → **camelCase** en el JSON. No hay
  `[JsonPropertyName]` ni política configurada.

### 5.4 Nulos

`<Nullable>enable</Nullable>` y `<ImplicitUsings>enable</ImplicitUsings>`
(`ISL_Service.csproj:5-6`). Idioma del proyecto:

* `string` que siempre viene → `= string.Empty`. `List<T>` → `= new()`.
* Lo opcional se marca `?` y se comprueba.
* El body nulo se defiende en el controlador: `if (request is null) return BadRequest(new { message = "Body requerido." });`
  o se sustituye por un objeto vacío: `request ?? new VentasConsultaRequest()`.
* Patrón para la query opcional: `string? texto` + `busqueda?.Trim()` en el service
  (`TarimaService.cs:19`).
* En los lectores de `SqlDataReader` se usan helpers locales (`ReadInt`, `Celda`,
  `reader.IsDBNull(...)`), nunca cast directo.
* `!` (null-forgiving) solo donde ya se validó antes (`JwtTokenGenerator.cs:17-19`, con el
  `throw` del arranque de `Program.cs:227-232` como garantía).

### 5.5 Controladores

* `[ApiController]` + `[Route("api/kebab-case")]` en la clase. `[Route("api/[controller]")]`
  solo aparece en los tres controladores viejos (Personas, ProveedoresPagos, Recaudaciones) —
  **no imitar**.
* `[Authorize]` en la clase; la policy fina en el método.
* Constructor con inyección por interfaz y campos `private readonly _service`.
* `CancellationToken ct` como **último parámetro** de cada acción, propagado hasta el
  `SqlCommand`.
* `[ProducesResponseType(...)]` por cada status posible (está en casi todos; Swagger lee los
  `///` del XML, `ISL_Service.csproj:7`).
* Las validaciones de forma (requerido, longitud, rangos) se hacen **en el controlador**, con
  métodos `private static object? ValidateCreate(...)` que devuelven `new { message = "..." }`
  o `null` (`CategoriasController.cs:133-148`). Las reglas de negocio van en el service.
* El `Trim()` de los campos de texto se hace en el controlador, construyendo un request nuevo
  (`TarimasController.cs:96-102`).
* **La identidad NUNCA viene del body.** `IdUsuarioLegacy()`, `Equipo()`,
  `GetUsername(User, Environment.MachineName)`. Esta frase está repetida literalmente en una
  docena de controladores y es la regla más explícita del proyecto: *"si viajara en el body,
  cualquiera podría cancelar un cobro a nombre de otro"*.

### 5.6 Estilo de comentarios

Es el rasgo más característico del código y hay que sostenerlo. Dos formas:

* `/// <summary>` XML para la API pública y Swagger, con `<param>`, `<response code="409">`
  y `<remarks>`.
* **Bloques `/* ... */` en MAYÚSCULAS de título** que explican *por qué*, con la evidencia:
  el síntoma que se vio, en qué base se comprobó, qué archivo de legacy lo demuestra, qué se
  descartó y por qué. Ejemplo representativo
  (`Infrastructure/Security/PermissionService.cs:27-44`):

  ```
  /*
    LAS CLAVES DE REPORTES QUE SON DEL WEB Y NO DE MAC31.
    ...
    Comprobado en produccion de Zaragoza: RICARDO tenia `reportes.ver_modulo`
    con Tipo=allow en la base y aun asi la casilla salia apagada, porque en
    Mac31 tiene CERO reportes encendidos y lo de Mac31 sustituia a lo del web.
  */
  ```

  Otros del mismo corte que vale leer antes de tocar su zona:
  `ISL_Service.csproj:10-22` (el build de 11 min), `Program.cs:318-334` (el CORS que
  devolvía al inicio sin error), `BaseDelCentro.cs:8-25`, `SelloDeCentro.cs:5-33`,
  `WkhtmltopdfHtmlPdfRenderer.cs:9-21` y `:71-87`, `PermisoControllerBase.cs:7-32`,
  `PrestamosController.cs:9-24` (las dos cosas de legacy que **no** se exponen a propósito).

Regla implícita: cuando algo se dejó peor de lo ideal, se escribe **por qué** y se anota como
pendiente en el comentario (p. ej. `VentasUsadosController.cs:24-31`, el permiso del botón
Guardar que no se usa porque el puente está amarrado a una forma por módulo).

### 5.7 Nombres de SP nuevos

Se crea `sp_w_` (web) o `sp_mo_` (móvil); **nunca se edita un `sp_n_`**. Verbo en español al
principio: `sp_w_Consultar*`, `sp_w_Insertar*`, `sp_w_Actualizar*`, `sp_w_Cancelar*`,
`sp_w_Inhabilitar*`, `sp_w_Guardar*`, `sp_w_Registrar*`. Los de administración de permisos
usan otro orden, con sufijo de acción: `sp_w_Permiso_Guardar`,
`sp_w_RolPermisos_ReemplazarPorClaves`, `sp_w_Modulo_SincronizarDesdePermiso`,
`sp_w_UsuarioModuloFavorito_Toggle`.

### 5.8 Estados

Convención en toda la base nueva: **`IdStatus` 1 = Activo, 2 = Cancelado/Inactivo** (así lo
anuncia incluso la descripción de Swagger, `Program.cs:40`). En almacén de cascos hay un
tercer valor: 1 REGISTRADA, 2 ACEPTADA, 3 CANCELADA. En usuarios: 1 activo, 2 inactivo,
3 bloqueado. **Nada se borra físicamente**: baja lógica con motivo, usuario y fecha.

---

## 6. El proyecto de pruebas

* **Ubicación**: `ISL_Service/ISL_Service/ISL_Service.Tests/` — **anidado dentro** del
  proyecto web (de ahí las exclusiones de `ISL_Service.csproj:23` y `:50-51`).
* **Framework**: **xUnit** 2.5.3 + `Microsoft.NET.Test.Sdk` 17.8.0 +
  `xunit.runner.visualstudio` + `coverlet.collector`, versiones centralizadas en
  `Directory.Packages.props`. Extras: `Microsoft.EntityFrameworkCore.InMemory` y
  `...Sqlite` (los usa `CompanyKeyTests` para montar un `AppDbContext` de prueba).
  `<Using Include="Xunit" />` global, así que los archivos no importan nada.
  **No hay librería de mocks**: los dobles son clases escritas a mano que implementan la
  interfaz y lanzan `NotImplementedException` en lo que no se usa.
* **Cómo se corre**: `dotnet test ISL_Service/ISL_Service/ISL_Service.Tests/ISL_Service.Tests.csproj`,
  o `dotnet test ISL_Service/ISL_Service.sln` (el `.sln` **sí** incluye los dos proyectos).
* **Cuántas hay hoy**: **10 archivos**, **57 métodos de prueba** (49 `[Fact]` + 8 `[Theory]`)
  que dan **90 casos** contando los 41 `[InlineData]`.

| archivo | casos | qué cubre |
|---|---|---|
| `ChecadorServiceTests.cs` | 16 | normalización de tipo/origen, huella 0 → NULL, recortes, validación de rangos de fecha |
| `ReportesCatalogoTests.cs` | 8 | que el catálogo tenga los 53 reportes con clave única, 4 grupos, ningún permiso repetido, ninguno exigiendo el permiso maestro |
| `TarimaServiceTests.cs` | 6 | `CambiarStatusAsync` (status inválido, ya en ese status, no existe) |
| `AlmacenCascosServiceTests.cs` | 5 | `GuardarKilosTarimaAsync`: inserta y recalcula, rechaza salidas/cancelados/tarima inexistente |
| `ModulosApagadosTests.cs` | 5 | `EsDeModuloApagado`, incluido que apagar `ventas` no apague `ventas_moviles` |
| `SelloDeCentroTests.cs` | 5 | que sin centro el reporte salga igual, dónde se inserta el sello, escape de HTML |
| `BaseDelCentroTests.cs` | 4 | `Apuntar` cambia solo el catálogo, nombre raro no pasa |
| `CompanyKeyTests.cs` | 4 | `companyKey` en login, en el JWT y en `/api/me`; error si falta la clave |
| `ReportesVerModuloNoSeBorraTests.cs` | 3 | que `reportes.ver_modulo` lo mande el web y no lo borre el puente de legacy |
| `RemisionZaragozaHumoTest.cs` | 1 | humo: el papel de Zaragoza se arma completo |

**Nada de esto prueba controladores, repositorios ni SQL.** Son pruebas de reglas puras y de
dos servicios. **La cobertura de los 242 endpoints es prácticamente nula** y el CI **no
ejecuta las pruebas**: `.github/workflows/main.yml` hace `dotnet restore/build/publish` sobre
`PROJECT_PATH` (el `.csproj` web), nunca `dotnet test`.

---

## 7. TODO / FIXME / stubs / endpoints muertos

No hay ni un `TODO:` ni un `FIXME` ni un `NotImplementedException` en el código de
producción. Lo que sí hay:

**Archivos completamente comentados** (no registran rutas; borrarlos no cambia nada):
* `Controllers/AuthController.cs` — lo reemplazó `SesionController`.
* `Controllers/DevController.cs` — generaba un hash BCrypt desde el navegador.
* `Controllers/ErrorController.cs` — lo reemplazó `ExceptionHandlingMiddleware`.

**Acciones muertas de plantilla de Visual Studio**, en `RecaudacionesController`:
* `GET api/Recaudaciones` → devuelve `new string[] { "value1", "value2" }`.
* `POST api/Recaudaciones`, `PUT api/Recaudaciones/{id}`, `DELETE api/Recaudaciones/{id}` →
  cuerpo vacío, `void`. Están publicadas y **sin `[Authorize]`**.

**Código muerto dentro de clases vivas**:
* `Mac3SqlServerConnector(int NumCaja)` (`:51-58`) construye la cadena a partir de
  `ConnectionStringServer = ""`, así que produce una cadena inservible; nadie lo llama.
* `AppDbContext(string connectionString)` + `CreateConnection()` (`:12-20`): ningún
  consumidor; la conexión real se toma de `_db.Database.GetConnectionString()`.
* `ISL_Service.http` apunta a `/weatherforecast/`, endpoint que no existe.

**Pendientes anotados en comentarios** (no marcados como TODO):
* `VentasUsadosController.cs:24-31` — Mac31 tiene un permiso aparte para el botón GUARDAR
  (`n_Procesos` 5085, forma `VentasUsados`) que **aquí no se usa**, porque el puente de
  permisos heredados está amarrado a UNA forma por módulo. Hoy quien abre la pantalla puede
  guardar.
* `PermisoControllerBase.cs:14-19` — `ChecadorController` y `VentasPedidosController` siguen
  con su copia del método de permisos; se dejaron sin migrar a propósito.
* `PrestamosController.cs:9-24` — dos operaciones de legacy **deliberadamente no expuestas**:
  el abono manual (`sp_n_InsertarEmpleadoPrestamosPagos`, que hace `SET Abonos = @Abono` y
  pisa el acumulado dejando el préstamo descuadrado) y el batch semanal
  (`sp_n_ValidaEmpleadosPrestamos`, que descuenta de la nómina).
* `ReportesCatalogo` — cinco de los 53 reportes tienen `Permiso = null`; a propósito **no se
  bloquean**.
* `ISL_Service.csproj:46` — `<Folder Include="Application\DTOs\PagoProveedor\" />`: carpeta
  declarada que no tiene archivos (los DTOs reales están en `DTOs/ProveedoresPagos`).

**Deudas de seguridad para la lista** (no son TODOs, son hechos del código):
* `ProveedoresPagosController` — 4 endpoints sin `[Authorize]`, tres de ellos escriben.
* `api/reportes/ventas/ReportesV3W` — anónimo y **sin pase firmado**, solo con `psp`.
* `/dbcheck` — anónimo, revela servidor y base.
* `Mac3SqlServerConnector.cs:46` — contraseña `Hope5y2k` en el código.
* `appsettings.json` — `Jwt:Key` y contraseñas de base en claro, versionadas.

---

## 8. Dependencias hacia Legacy y MacReportes

**No hay NINGUNA dependencia por red.** Lo verifiqué: cero `HttpClient`, cero
`AddHttpClient`, cero URL de MacReportes o de Mac31 en el código. Todas las menciones a
`MacReportes` son comentarios que explican de dónde se copió el comportamiento.

El acoplamiento con legacy es **por base de datos**, y es profundo:

1. **Ejecutar sus stored procedures**: ~100 SP `sp_n_` + `sp_ConsultaReportes`.
2. **Leer sus tablas con SQL inline**: `dbo.Usuarios`, `dbo.Clientes`,
   `[Catalogo TiposUsados]`, `[Catalogo Repartidores]`, `[Catalogo Bancos]`,
   `dbo.Constantes`, `dbo.CentrosServicio`, `Template_Html`, `n_Procesos` /
   `UsuarioFormaProceso` (a través de `PermissionService`).
3. **ESCRIBIR en sus tablas**: `UserRepository` hace `UPDATE dbo.Usuarios` desde el web
   (`UserRepository.cs:538` y `:616`) y `sp_WebUsuario_Upsert` crea/actualiza el usuario de
   Mac31. `PermissionService` llama a `dbo.sp_n_ActualizarUsuarioFormaProceso` (`:2327`,
   `:2599`) — guardar permisos desde el web toca los permisos de Mac31.
4. **Depender de su configuración de empresa**: `Constantes.Funcionalidad` y
   `EsCentroServicio` cambian qué SP se usa y qué reglas aplican.
5. **Depender de sus plantillas**: `Template_Html` es la fuente del formato de remisión y de
   los 53 reportes. Si alguien ajusta una fila, cambia el papel del web.
6. **Depender de sus rutas de archivo**: `Constantes.PathImagenes` + `LogoMacReportes`
   apuntan a `C:\Mac21\imagenes\` / `C:\MacZ\imagenes\`; cuando no existen (Azure), se cae a
   `Assets/Logos` con el **mismo nombre de archivo**.
7. **Reimplementar MacReportes**: la remisión ya no se delega en él; se arma aquí con sus
   mismos `sp_n_`, sus mismas plantillas, sus mismos márgenes (4/6/2/8 mm, Portrait) y su
   mismo nombre de archivo de descarga. Lo dice explícitamente
   `Application/Services/RemisionImpresionService.cs:10-27` y
   `Application/DTOs/VentasConsulta/VentasConsultaDtos.cs:95-101`.
8. **Puente de permisos**: `PermissionService` deriva cuatro familias de permisos
   (`reportes.*`, `ventas.*`, `empleados.*`, `prestamos.*`) de los botones de las formas de
   Mac31, buscadas **por Descripción**.
9. **Código copiado de legacy**: `Utils/Encryptacion.cs`, `Utils/Funciones.cs`,
   `Utils/Variables.cs`, `Infrastructure/Data/Mac3SqlServerConnector.cs` y
   `Infrastructure/Security/CurrentUserAccessor.cs` (este último "copiado TAL CUAL de
   `Shared.Backend.Core` (repo shared-back), sin cambiarle una línea").

Dependencia de plataforma que vale contar aquí: **wkhtmltopdf.exe es un proceso de Windows**
empaquetado con 20 DLLs de VC/UCRT en `Assets/Wkhtmltopdf/`. Mientras los reportes dependan
de él, el backend no es portable a Linux.
