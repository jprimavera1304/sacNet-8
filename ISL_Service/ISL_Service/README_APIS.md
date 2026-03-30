# Documentación de APIs - ISL Service

API REST del servicio ISL. La mayoría de endpoints requieren autenticación JWT (`Authorization: Bearer {token}`).  
En desarrollo, Swagger está disponible en `/swagger`.

---

## Índice

1. [Autenticación y sesión](#1-autenticación-y-sesión)
2. [Usuario actual (Me)](#2-usuario-actual-me)
3. [Capacidades y permisos](#3-capacidades-y-permisos)
4. [Módulos](#4-módulos)
5. [Permisos Web / Capacidades (admin)](#5-permisos-web--capacidades-admin)
6. [Usuarios](#6-usuarios)
7. [Empresas](#7-empresas)
8. [Tarimas](#8-tarimas)
9. [Catálogos](#9-catálogos)
10. [Dashboard Ventas](#10-dashboard-ventas)
11. [Almac?n de Cascos](#11-almac?n-de-cascos)
12. [Personas (Proveedores)](#12-personas-proveedores)
13. [Recaudaciones](#13-recaudaciones)
14. [Proveedores de pagos](#14-proveedores-de-pagos)
15. [Temporadas y Torneos](#15-temporadas-y-torneos)
16. [Categorias Equipos](#16-categorias-equipos)
17. [Profesores](#17-profesores)
18. [Equipos](#18-equipos)
19. [Inscripciones Torneo](#19-inscripciones-torneo)
20. [Jornadas](#20-jornadas)
21. [ConfiguraciÃ³n Rol Torneo](#21-configuraci%C3%B3n-rol-torneo)
22. [Cheques](#22-cheques)
23. [Utilidades](#23-utilidades)

---

## 1. Autenticación y sesión

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `POST` | `/api/auth/login` | No | Inicio de sesión. Alternativa: `/api/sesion/login`. |
| `POST` | `/api/sesion/login` | No | Mismo login que arriba. |

**Body (login):** `application/json`

```json
{
  "usuario": "string",
  "contrasena": "string"
}
```

**Respuesta:** Objeto con token JWT y datos de usuario (por ejemplo `token`, `usuario`, `rol`, etc.).  
Usar el token en cabecera: `Authorization: Bearer {token}`.

---

## 2. Usuario actual (Me)

Base: `api/me`. **Requiere:** usuario autenticado.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/me` | Obtiene el perfil del usuario autenticado: id, usuario, rol, empresaId, permisos, companyKey, debeCambiarContrasena, etc. |
| `POST` | `/api/me/change-password` | Cambiar la contraseña del usuario actual. |

**Body (change-password):** `ChangePasswordRequest` (por ejemplo contraseña actual y nueva).  
**Respuesta GET:** `MeResponse` con datos de usuario y snapshot de permisos.

---

## 3. Capacidades y permisos

Base: `api/capacidades`. **Requiere:** usuario autenticado.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/capacidades` | Devuelve las capacidades/permisos del usuario actual: UserId, EmpresaId, RolLegacy, PermissionsEnabled, Permissions, PermissionsVersion. |

Si las capacidades no están habilitadas para el tenant, responde 404.

---

## 4. Módulos

Base: `api/modulos`. **Requiere:** usuario autenticado.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/modulos/disponibles` | Lista los módulos disponibles para el usuario según su empresa y permisos. |

**Query:**

- `scope` (opcional): `tenant` (por defecto) o `all`. Con `all` solo SuperAdmin puede ver todos los módulos; si no es SuperAdmin devuelve 403.

**Respuesta:** Objeto con `scope`, `companyKey` y `modules` (lista de módulos con capacidad de ver, etc.).

---

## 5. Permisos Web / Capacidades (admin)

Base: `api/permisos-web`. Algunas rutas tienen alias en `api/capacidades`. **Requiere:** usuario autenticado y políticas indicadas.

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `GET` | `/api/permisos-web/bootstrap` | `perm:permisosweb.bootstrap` | Bootstrap de permisos web: roles, permissions, rolePermissions, users, userOverrides. |
| `GET` | `/api/capacidades/bootstrap` | `perm:permisosweb.bootstrap` | Misma respuesta que arriba. |
| `GET` | `/api/permisos-web/roles/bootstrap` | `perm:permisos_roles.ver_modulo` | Bootstrap solo de roles y permisos por rol. |
| `GET` | `/api/permisos-web/catalogo` | `perm:permisos_modulos.ver_modulo` | Catálogo de permisos y módulos. |
| `PUT` | `/api/permisos-web/modulos/{moduleKey}/status` | `perm:permisos_modulos.activar` | Activa/desactiva un módulo por clave. |
| `PUT` | `/api/permisos-web/catalogo/modulos/{moduleKey}/status` | `perm:permisos_modulos.activar` | Igual que arriba. |
| `PUT` | `/api/permisos-web/modulos/status` | `perm:permisos_modulos.activar` | Cambia estado de módulo; módulo y estado en body. |
| `PUT` | `/api/permisos-web/roles/{roleCode}/permissions` | `perm:permisosweb.roles.editar` | Guarda permisos de un rol. |
| `PUT` | `/api/capacidades/roles/{roleCode}/permissions` | `perm:permisosweb.roles.editar` | Mismo que arriba. |
| `POST` | `/api/permisos-web/roles` | `perm:permisosweb.roles.editar` | Crea un nuevo rol. |
| `PUT` | `/api/permisos-web/usuarios/{userId}/overrides` | `perm:permisosweb.overrides.editar` | Guarda overrides de permisos de un usuario (allow/deny). |
| `PUT` | `/api/capacidades/usuarios/{userId}/overrides` | `perm:permisosweb.overrides.editar` | Mismo que arriba. |
| `POST` | `/api/permisos-web/catalogo/sync` | `perm:permisosweb.catalogo.editar` | Sincroniza catálogo de permisos (módulos opcionales en body). |
| `POST` | `/api/permisos-web/catalogo/permissions` | `perm:permisosweb.catalogo.editar` | Crea un permiso en el catálogo (key, name, description). |

Los bodies usan DTOs como `PermisosWebSetModuleStatusRequest`, `PermisosWebUpdateRolePermissionsRequest`, `PermisosWebCreateRoleRequest`, `PermisosWebUpdateUserOverridesRequest`, `PermisosWebSyncCatalogRequest`, `PermisosWebCreatePermissionRequest`.

---

## 6. Usuarios

Base: `api/usuarios`. **Requiere:** usuario autenticado y políticas por endpoint.

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `POST` | `/api/usuarios` | `perm:usuarios.crear` | Crea un usuario. Body: `CreateUserRequest`. |
| `GET` | `/api/usuarios` | `perm:usuarios.ver_modulo` | Lista usuarios del contexto (empresa). |
| `GET` | `/api/usuarios/roles` | `perm:usuarios.ver_modulo` | Lista catálogo de roles. |
| `GET` | `/api/usuarios/{id}` | `perm:usuarios.ver` | Obtiene un usuario por GUID. |
| `PUT` | `/api/usuarios/{id}` | `perm:usuarios.editar` | Actualiza usuario. Body: `UpdateUserRequest`. |
| `PATCH` | `/api/usuarios/{id}` | `perm:usuarios.editar` | Actualización parcial (mismo body). |
| `PUT` | `/api/usuarios` | `perm:usuarios.editar` | Actualiza usuario con id en body: `UpdateUserWithIdRequest`. |
| `PATCH` | `/api/usuarios/{id}/estado` | `perm:usuarios.estado.editar` | Cambia estado del usuario. Body: `UpdateUserEstadoRequest`. |
| `PUT` | `/api/usuarios/{id}/estado` | `perm:usuarios.estado.editar` | Mismo que arriba. |
| `PUT` | `/api/usuarios/estado` | `perm:usuarios.estado.editar` | Estado con id en body: `UpdateUserEstadoWithIdRequest`. |
| `PUT` | `/api/usuarios/{id}/inactivar` | `perm:usuarios.estado.editar` | Inactiva usuario (estado 2). Sin body. |
| `PUT` | `/api/usuarios/{id}/activar` | `perm:usuarios.estado.editar` | Activa usuario (estado 1). Sin body. |
| `POST` | `/api/usuarios/{id}/reset-password` | `perm:usuarios.password.reset` | Resetea contraseña del usuario. |
| `PATCH` | `/api/usuarios/{id}/empresa` | `perm:usuarios.empresa.editar` | Cambia empresa del usuario. Body: `UpdateUserEmpresaRequest`. |

`id` en rutas es un **GUID**.

---

## 7. Empresas

Base: `api/empresas`. **Requiere:** `perm:empresas.ver`.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/empresas` | Lista empresas según el usuario autenticado. |

---

## 8. Tarimas

Base: `api/tarimas`. Estatus: **1 = Activo**, **2 = Cancelado/Inactivo**. **Requiere:** JWT y políticas por acción.

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `GET` | `/api/tarimas` | `perm:tarimas.ver` | Lista tarimas con filtros opcionales. |
| `GET` | `/api/tarimas/{idTarima}` | `perm:tarimas.ver` | Obtiene una tarima por IdTarima (entero). |
| `POST` | `/api/tarimas` | `perm:tarimas.crear` | Crea una tarima. Usuario de auditoría del token. |
| `PUT` | `/api/tarimas/{idTarima}` | `perm:tarimas.editar` | Actualiza tarima (solo si está activa). |
| `PATCH` | `/api/tarimas/{idTarima}/status` | `perm:tarimas.estado.editar` | Cambia estatus (1 Activo, 2 Cancelado/Inactivo). |

**Query (GET list):**

- `idStatus`: opcional. NULL = todas, 1 = Activo, 2 = Cancelado/Inactivo.
- `estatus`: alternativa a idStatus. Valores: `activo` (1), `inactivo` o `cancelado` (2), `todos` (todas). Si se envía `idStatus`, tiene prioridad.
- `busqueda`: opcional. Filtro por nombre.

**Body crear:** `CreateTarimaRequest`: nombreTarima, idTipoCasco, numeroCascosBase, observaciones (opcional).  
**Body actualizar:** `UpdateTarimaRequest`: mismos campos.  
**Body status:** `UpdateTarimaStatusRequest`: idStatus (1 o 2).

**Códigos:** 200 OK, 201 Created, 400 Validación, 401/403 Auth, 404 No encontrada, 409 Duplicado (nombre + tipo casco) o no activa.

---

## 9. Catálogos

Base: `api/catalogos`. **Requiere:** usuario autenticado. Solo lectura para dropdowns.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/catalogos/tipos-casco` | Lista tipos de casco (TiposUsados) para dropdown en tarimas y almacén de cascos. |
| `GET` | `/api/catalogos/repartidores` | Lista repartidores ([Catalogo Repartidores]) para dropdown en almacén de cascos. |
| `GET` | `/api/catalogos/tarimas` | Lista tarimas (WTarima) activas para dropdown en almacén de cascos. |
| `GET` | `/api/catalogos/clientes` | Lista clientes (Clientes) para dropdowns administrativos. |
| `GET` | `/api/catalogos/bancos` | Lista bancos ([Catalogo Bancos]) para dropdowns administrativos. |

**Query (todos):**

- `status`: opcional. Filtro por IdStatus (ej. 1 = activos). NULL = todos.

**Respuestas:**
- tipos-casco: lista de `TipoCascoItemDto` (idTipoCasco, descripcion).
- repartidores: lista de `RepartidorItemDto` (idRepartidor, repartidor).
- tarimas: lista de `TarimaCatalogItemDto` (idTarima, nombreTarima, idTipoCasco, numeroCascosBase).

---

## 10. Dashboard Ventas

Base: `api/dashboard`. **Requiere:** usuario autenticado (JWT) y `perm:dashboardventas.ver`.

**Filtros globales (body):**

- `fechaInicial` (DATE)
- `fechaFinal` (DATE)
- `idEmpresa`, `idAlmacen`, `idAgente`, `idCliente`, `idProducto`, `idCategoria`, `idMarca`, `idTipoDocumento` (INT/null)
- `top` (INT, opcional, default 10) para endpoints Top

| M?todo | Ruta | Descripci?n |
|--------|------|-------------|
| `POST` | `/api/dashboard/filtros/consultar` | Devuelve cat?logos: empresas, almacenes, agentes, clientes, productos, categor?as, marcas, tiposDocumento. |
| `POST` | `/api/dashboard/kpis/consultar` | KPIs: ventaTotal, gananciaTotal, descuentoTotal, tickets, unidadesVendidas, ticketPromedio, margenPorcentaje. |
| `POST` | `/api/dashboard/ventas/serie-mensual/consultar` | Serie mensual: anio, mes, mesNombre, ventaTotal, gananciaTotal, tickets. |
| `POST` | `/api/dashboard/ventas/serie-semanal/consultar` | Serie semanal por mes (rangos fijos): semanaNumero, fechaInicioSemana, fechaFinSemana, ventaTotal, gananciaTotal, cantidadVendida. |
| `POST` | `/api/dashboard/top-productos/consultar` | Top productos (usa `top`). |
| `POST` | `/api/dashboard/top-clientes/consultar` | Top clientes (usa `top`). |
| `POST` | `/api/dashboard/top-categorias/consultar` | Top categor?as (usa `top`). |
| `POST` | `/api/dashboard/top-marcas/consultar` | Top marcas (usa `top`). |
| `POST` | `/api/dashboard/almacenes/consultar` | Ventas por almac?n. |
| `POST` | `/api/dashboard/agentes/consultar` | Ventas por agente. |
| `POST` | `/api/dashboard/detalle/consultar` | Detalle de ventas (tabla). |

**Notas:**

- Todos los filtros se env?an en el body; si no aplican, usar `null`.
- `fechaInicial` y `fechaFinal` son obligatorias.
- Los endpoints Top usan `top` (si no se env?a, default 10).
- La venta total usa ImporteConIvaRnd y la ganancia usa GananciaRnd (ya vienen calculadas en SP).

---

## 11. Almacén de Cascos

Base: `api/almacen-cascos`. **Requiere:** usuario autenticado (JWT). Usa tablas `WMovimientoCasco`, `WMovimientoCascoDetalle`, `WMovimientoCascoTarima`, `WConstantes`, `WTarima` y SP `sp_w_*`. Usuario de auditoría se toma del token.

**Convenciones:**
- **TipoMovimiento:** 1 = SALIDA, 2 = ENTRADA.
- **Estatus:** 1 = REGISTRADA, 2 = ACEPTADA, 3 = CANCELADA.
- **IdStatus en catálogos:** 1 = ACTIVO, 2 = CANCELADO.

| M?todo | Ruta | Descripci?n |
|--------|------|-------------|
| `GET` | `/api/almacen-cascos/movimientos` | Lista movimientos (cabecera) con nombres de repartidor entrega/recibe. |
| `GET` | `/api/almacen-cascos/movimientos/{idMovimiento}/detalle` | Detalle plano de un movimiento (l?neas por numeroTarima y tipo casco). |
| `GET` | `/api/almacen-cascos/movimientos/{idMovimiento}/tarimas` | Tarimas con kilos capturados para una ENTRADA. |
| `GET` | `/api/almacen-cascos/movimientos/{idMovimiento}/detalle-agrupado` | Detalle agrupado por tarima l?gica (`numeroTarima`) con l?neas por tipo casco. |
| `POST` | `/api/almacen-cascos/salidas` | Crea una SALIDA: cabecera + detalle. TotalTarimas/TotalPiezas se calculan en backend; TotalKilos = 0. |
| `POST` | `/api/almacen-cascos/entradas` | Acepta una ENTRADA desde una salida registrada (SP crea entrada, acepta salida, TotalKilos = 0). |
| `POST` | `/api/almacen-cascos/movimientos/{idMovimiento}/tarimas/{numeroTarima}/kilos` | Inserta o actualiza kilos por tarima en una ENTRADA. |
| `POST` | `/api/almacen-cascos/movimientos/{idMovimiento}/cancelar` | Cancela un movimiento (entrada o salida). Reglas validadas en SP. |

**Query GET movimientos:**

- `estatus`: opcional. NULL = todos, 1 = REGISTRADA, 2 = ACEPTADA, 3 = CANCELADA.
- `tipoMovimiento`: opcional. 1 = SALIDA, 2 = ENTRADA.
- `fechaInicio`, `fechaFin`: opcional. Filtro por rango de fechas (FechaCreacion).

**Notas:**

- En la respuesta, `totalTarimasConKilos` indica cu?ntas tarimas tienen kilos capturados.

**Body POST salidas:** `CreateSalidaRequest`

```json
{
  "idRepartidorEntrega": 1,
  "observaciones": "string opcional",
  "tarimas": [
    {
      "numeroTarima": 1,
      "lineas": [
        { "idTipoCasco": 1, "piezas": 10 },
        { "idTipoCasco": 2, "piezas": 5 }
      ]
    }
  ]
}
```

- `idRepartidorEntrega`: requerido, debe ser repartidor activo (status=1).
- `tarimas`: al menos una tarima lógica.
- Cada tarima: `numeroTarima` (>0, no repetido) y `lineas` (>=1).
- Cada línea: `idTipoCasco` activo y `piezas` (>0).

**Body POST entradas:** `CreateEntradaRequest`

```json
{
  "idMovimientoSalida": 1,
  "idRepartidorRecibe": 2,
  "observaciones": "string opcional",
  "detalle": [
    { "numeroTarima": 1, "idTipoCasco": 1, "piezas": 10 }
  ]
}
```

- `idMovimientoSalida`: salida ya registrada y con detalle.
- `idRepartidorRecibe`: repartidor activo.
- `detalle`: opcional; si se env?a, el backend valida que coincida exactamente con el detalle de la salida.

**Body POST kilos por tarima:** `GuardarKilosTarimaRequest`

```json
{
  "kilos": 12.3456
}
```

- `kilos`: requerido, > 0, hasta 4 decimales.

**Body POST cancelar:** `CancelarMovimientoRequest`

```json
{
  "motivoCancelacion": "string requerido"
}
```

**Códigos:** 200 OK, 201 Created, 400 Validación o regla de negocio, 409 Conflicto (ej. doble entrada por salida, ya cancelado, no se puede cancelar).

---

## 12. Personas (Proveedores)

Base: `api/Personas`. **Requiere:** usuario autenticado y políticas indicadas.

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `GET` | `/api/Personas/consultar` | `perm:proveedores.ver` | Consulta personas. Filtros: idPersona, idStatus (1 Activo, 2 Cancelado). |
| `POST` | `/api/Personas/insertar` | `perm:proveedores.crear` | Inserta persona. IDStatus se crea en 1 (Activo). Body: `PersonaInputDTO`. |
| `PUT` | `/api/Personas/actualizar` | `perm:proveedores.editar` | Actualiza persona. No modifica IDStatus. Body: `PersonaInputDTO`. |
| `PUT` | `/api/Personas/cancelar` | `perm:proveedores.estado.editar` | Cancela persona (IDStatus = 2). Body debe incluir IDPersona. |
| `PUT` | `/api/Personas/reactivar` | `perm:proveedores.estado.editar` | Reactiva persona (IDStatus = 1). Body debe incluir IDPersona. |

**Query (consultar):** `idPersona`, `idStatus` (opcionales).

---

## 13. Recaudaciones

Base: `api/Recaudaciones`. Endpoints principales por **ticket/QR** (cadena encriptada).

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `GET` | `/api/Recaudaciones` | No | Devuelve array placeholder ["value1","value2"]. |
| `GET` | `/api/Recaudaciones/{qr}` | No | Consulta recaudación por ticket QR. `qr`: cadena encriptada (soporta reemplazos @, CBA-_ABC, _, -, * para base64). Desencripta a IDRecaudacion\|IDCaja\|Dv1 y busca registros. |

**Respuestas GET {qr}:** 200 con lista de registros, 400 ticket inválido, 404 sin registros, 500 error interno.

Los métodos POST, PUT y DELETE están definidos pero sin lógica (vacíos).

---

## 14. Proveedores de pagos

Base: `api/ProveedoresPagos`. **Sin** atributo `[Authorize]` en el controlador (endpoints públicos o según configuración global).

| Método | Ruta | Descripción |
|--------|------|-------------|
| `POST` | `/api/ProveedoresPagos/consultar` | Consulta pagos de proveedores. Body: `ProveedorPagoInputDTO`. |
| `POST` | `/api/ProveedoresPagos/insertar` | Inserta un pago. Body: `ProveedorPagoInputDTO`. Respuesta: `{ IDProveedorPago: number }`. |
| `POST` | `/api/ProveedoresPagos/actualizar` | Actualiza un pago. Body: `ProveedorPagoInputDTO`. |
| `POST` | `/api/ProveedoresPagos/cancelar` | Cancela un pago. Body: `ProveedorPagoInputDTO`. |

Errores: 404 si no hay registros, 500 con mensaje de excepción.

---

## 15. Temporadas y Torneos

Base: `api/temporadas` y `api/torneos`. Requiere JWT. Usa SPs: `sp_w_ConsultarTemporadas`, `sp_w_InsertarTemporada`, `sp_w_ActualizarTemporada`, `sp_w_CancelarTemporada`, `sp_w_ConsultarTorneos`, `sp_w_InsertarTorneo`, `sp_w_ActualizarTorneo`, `sp_w_CancelarTorneo`, `sp_w_ActivarTorneo`, `sp_w_CerrarTorneo`, `sp_w_ReactivarTorneo`, `sp_w_CerrarTorneosVencidos`.

### Temporadas (`api/temporadas`)

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| `GET` | `/api/temporadas` | Lista temporadas. Filtros: `estado` (0/1/2) y `texto` (busqueda por nombre). |
| `GET` | `/api/temporadas/{id}` | Obtiene temporada por Id (GUID). |
| `POST` | `/api/temporadas` | Crea temporada. Body: `CreateTemporadaRequest` (`nombre`, `fechaInicio`, `fechaFin`). |
| `PUT` | `/api/temporadas/{id}` | Actualiza temporada activa. Body: `UpdateTemporadaRequest`. |
| `POST` | `/api/temporadas/{id}/cancelar` | Cancela temporada activa. Body opcional: `CancelTemporadaRequest` (`motivo`). |
| `POST` | `/api/temporadas/{id}/reactivar` | Reactiva temporada cancelada (estado 2) a activa (estado 1). |

### Torneos (`api/torneos`)

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| `GET` | `/api/torneos` | Lista torneos. Filtros: `temporadaId`, `estado` (0/1/2/3/4), `texto` (nombre/clave), `fechaInicio`, `fechaFin`. |
| `GET` | `/api/torneos/{id}` | Obtiene torneo por Id (GUID). |
| `POST` | `/api/torneos` | Crea torneo. Body: `CreateTorneoRequest` (`temporadaId`, `nombre`, `clave`, `fechaInicio`, `fechaFin`). |
| `PUT` | `/api/torneos/{id}` | Actualiza torneo. Body: `UpdateTorneoRequest`. |
| `POST` | `/api/torneos/{id}/activar` | Activa torneo (Borrador -> Activo). |
| `POST` | `/api/torneos/{id}/cerrar` | Cierra torneo (Activo -> Cerrado). |
| `POST` | `/api/torneos/{id}/cancelar` | Cancela torneo. Body opcional: `CancelTorneoRequest` (`motivo`). |
| `POST` | `/api/torneos/{id}/reactivar` | Reactiva torneo cancelado (puede quedar Activo o Cerrado segun fecha fin). |
| `POST` | `/api/torneos/cerrar-vencidos` | Cierra torneos activos vencidos. Query opcional: `fechaCorte`. |

Reglas de negocio principales:
- `nombre` temporada max 80.
- `nombre` torneo max 120, `clave` max 30.
- `fechaFin` no puede ser menor a `fechaInicio`.
- Para crear/actualizar torneo, la temporada debe estar activa.
- No se puede cancelar temporada si tiene torneos activos.

---

## 16. Categorias Equipos

Base: `api/categorias`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarCategorias`, `sp_w_InsertarCategoria`, `sp_w_ActualizarCategoria`, `sp_w_InhabilitarCategoria`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/categorias` | `perm:categorias.ver` | Lista categorias. Filtros opcionales: `estado` (1 activo, 2 inactivo), `texto` (busqueda por nombre). |
| `GET` | `/api/categorias/{id}` | `perm:categorias.ver` | Obtiene categoria por Id (GUID). |
| `POST` | `/api/categorias` | `perm:categorias.crear` | Crea categoria. Body: `CreateCategoriaRequest` (`nombre`). |
| `PUT` | `/api/categorias/{id}` | `perm:categorias.editar` | Actualiza categoria activa. Body: `UpdateCategoriaRequest` (`nombre`). |
| `POST` | `/api/categorias/{id}/inhabilitar` | `perm:categorias.activar` | Inhabilita categoria (estado 2). Body opcional: `InhabilitarCategoriaRequest` (`motivo`). |
| `POST` | `/api/categorias/{id}/habilitar` | `perm:categorias.activar` | Habilita categoria (estado 1). Sin body. |

Reglas:
- `nombre` requerido, max 120.
- `motivo` max 200.
- No permite nombre duplicado.
- No permite modificar categoria inactiva.

---

## 17. Profesores

Base: `api/profesores`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarProfesores`, `sp_w_InsertarProfesor`, `sp_w_ActualizarProfesor`, `sp_w_InhabilitarProfesor`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/profesores` | `perm:profesores.ver` | Lista profesores. Filtros opcionales: `estado` (1 activo, 2 inactivo), `texto` (nombre/telefono/correo). |
| `GET` | `/api/profesores/{id}` | `perm:profesores.ver` | Obtiene profesor por Id (GUID). |
| `POST` | `/api/profesores` | `perm:profesores.crear` | Crea profesor. Body: `CreateProfesorRequest` (`nombre`, `telefono`, `correo?`). |
| `PUT` | `/api/profesores/{id}` | `perm:profesores.editar` | Actualiza profesor activo. Body: `UpdateProfesorRequest` (`nombre`, `telefono`, `correo?`). |
| `POST` | `/api/profesores/{id}/inhabilitar` | `perm:profesores.activar` | Inhabilita profesor (estado 2). Body opcional: `InhabilitarProfesorRequest` (`motivo` opcional). |
| `POST` | `/api/profesores/{id}/habilitar` | `perm:profesores.activar` | Habilita profesor (estado 1). Sin body. |

Reglas:
- `nombre` requerido, max 120.
- `telefono` requerido, max 30.
- `correo` opcional, max 200.
- `motivo` opcional, max 200.
- No permite duplicado por `Nombre + Telefono`.
- No permite modificar profesor inactivo.

---

## 18. Equipos

Base: `api/equipos`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarEquipos`, `sp_w_InsertarEquipo`, `sp_w_ActualizarEquipo`, `sp_w_InhabilitarEquipo`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/equipos` | `perm:equipos.ver` | Lista equipos. Filtros opcionales: `estado` (1 activo, 2 inactivo), `categoriaId` (GUID), `diaJuego` (1 sabado, 2 domingo), `texto` (nombre/categoria/profesores). |
| `GET` | `/api/equipos/{id}` | `perm:equipos.ver` | Obtiene equipo por Id (GUID). |
| `POST` | `/api/equipos` | `perm:equipos.crear` | Crea equipo. Body: `CreateEquipoRequest` (`nombre`, `categoriaPredeterminadaId`, `diaJuegoPredeterminado`, `profesorTitularPredeterminadoId`, `profesorAuxiliarPredeterminadoId?`). |
| `PUT` | `/api/equipos/{id}` | `perm:equipos.editar` | Actualiza equipo activo. Body: `UpdateEquipoRequest` (mismos campos de create). |
| `POST` | `/api/equipos/{id}/inhabilitar` | `perm:equipos.activar` | Inhabilita equipo (estado 2). Body opcional: `InhabilitarEquipoRequest` (`motivo` opcional). |
| `POST` | `/api/equipos/{id}/habilitar` | `perm:equipos.activar` | Habilita equipo (estado 1). Sin body. |

Reglas:
- `nombre` requerido, max 120.
- `categoriaPredeterminadaId` requerido y activo.
- `diaJuegoPredeterminado` requerido (1 o 2).
- `profesorTitularPredeterminadoId` requerido y activo.
- `profesorAuxiliarPredeterminadoId` opcional y distinto al titular.
- `motivo` opcional, max 200.
- No permite nombre de equipo duplicado.

---

## 19. Inscripciones Torneo

Base: `api/inscripciones-torneo`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarInscripcionesTorneo`, `sp_w_InsertarInscripcionTorneo`, `sp_w_ActualizarInscripcionTorneo`, `sp_w_InhabilitarInscripcionTorneo`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/inscripciones-torneo` | `perm:inscripciones.ver` | Lista inscripciones. Filtros opcionales: `torneoId` (GUID), `categoriaId` (GUID), `estado` (1 activo, 2 inhabilitado), `texto` (torneo/equipo/categoria/profesores). |
| `GET` | `/api/inscripciones-torneo/{id}` | `perm:inscripciones.ver` | Obtiene inscripcion por Id (GUID). |
| `POST` | `/api/inscripciones-torneo` | `perm:inscripciones.crear` | Crea inscripcion. Body: `CreateInscripcionTorneoRequest` (`torneoId`, `equipoId`, `categoriaId?`, `diaJuego?`, `profesorTitularId?`, `profesorAuxiliarId?`). |
| `PUT` | `/api/inscripciones-torneo/{id}` | `perm:inscripciones.editar` | Actualiza inscripcion activa. Body: `UpdateInscripcionTorneoRequest` (`categoriaId?`, `diaJuego?`, `profesorTitularId?`, `profesorAuxiliarId?`). |
| `POST` | `/api/inscripciones-torneo/{id}/inhabilitar` | `perm:inscripciones.activar` | Inhabilita inscripcion (estado 2). Body opcional: `InhabilitarInscripcionTorneoRequest` (`motivo` opcional). |
| `POST` | `/api/inscripciones-torneo/{id}/habilitar` | `perm:inscripciones.activar` | Habilita inscripcion (estado 1). Sin body. |

Reglas:
- `torneoId` requerido y el torneo debe estar Activo.
- `equipoId` requerido y el equipo debe estar activo.
- `categoriaId` opcional: si no se envia, se usa la predeterminada del equipo.
- `diaJuego` opcional: si no se envia, se usa el predeterminado del equipo (1 o 2).
- `profesorTitularId` opcional: si no se envia, se usa el predeterminado del equipo.
- `profesorAuxiliarId` opcional y distinto al titular.
- `motivo` opcional, max 200.
- No permite duplicado de equipo en el mismo torneo y categoria.

---

## 20. Jornadas

Base: `api/jornadas`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarJornadas`, `sp_w_InsertarJornada`, `sp_w_ActualizarJornada`, `sp_w_InhabilitarJornada`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/jornadas` | `perm:jornadas.ver` | Lista jornadas. Filtros opcionales: `estado` (1 activo, 2 inhabilitado), `texto` (nombre/numero). |
| `GET` | `/api/jornadas/{id}` | `perm:jornadas.ver` | Obtiene jornada por Id (GUID). |
| `POST` | `/api/jornadas` | `perm:jornadas.crear` | Crea jornada. Body: `CreateJornadaRequest` (`numeroJornada`). |
| `PUT` | `/api/jornadas/{id}` | `perm:jornadas.editar` | Actualiza jornada activa. Body: `UpdateJornadaRequest` (`numeroJornada`). |
| `POST` | `/api/jornadas/{id}/inhabilitar` | `perm:jornadas.activar` | Inhabilita jornada (estado 2). Body: `InhabilitarJornadaRequest` (`motivo` requerido). |

Reglas:
- `numeroJornada` requerido, > 0.
- `motivo` requerido, max 200.
- No permite numero o nombre duplicado.
- No permite modificar jornada inactiva.
---

## 21. ConfiguraciÃ³n Rol Torneo

Base: `api/configuracion-rol-torneo`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarConfiguracionesRolTorneo`, `sp_w_ObtenerConfiguracionRolTorneo`, `sp_w_InsertarConfiguracionRolTorneo`, `sp_w_ActualizarConfiguracionRolTorneo`, `sp_w_InhabilitarConfiguracionRolTorneo`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/configuracion-rol-torneo` | `perm:configuracionroltorneo.ver` | Lista configuraciones con filtros. |
| `GET` | `/api/configuracion-rol-torneo/{id}` | `perm:configuracionroltorneo.ver` | Obtiene configuracion por Id (GUID). |
| `GET` | `/api/configuracion-rol-torneo/torneo/{torneoId}` | `perm:configuracionroltorneo.ver` | Obtiene configuracion activa por torneo. |
| `POST` | `/api/configuracion-rol-torneo` | `perm:configuracionroltorneo.crear` | Crea configuracion. |
| `PUT` | `/api/configuracion-rol-torneo/{id}` | `perm:configuracionroltorneo.editar` | Actualiza configuracion activa. |
| `POST` | `/api/configuracion-rol-torneo/{id}/inhabilitar` | `perm:configuracionroltorneo.activar` | Inhabilita configuracion. |

**Query (GET list):**

- `texto`: opcional. Busca por nombre/clave de torneo o temporada.
- `temporadaId`: opcional (GUID).
- `estado`: opcional (1 activo, 2 inhabilitado).

**Body POST crear:** `CreateConfiguracionRolTorneoRequest`

```json
{
  "torneoId": "GUID",
  "horaInicioPredeterminada": "08:00:00",
  "duracionPartidoMin": 50,
  "minutosEntrePartidos": 10,
  "numeroCanchas": 2,
  "observacionesPredeterminadas": "string opcional"
}
```

**Body PUT actualizar:** `UpdateConfiguracionRolTorneoRequest`

```json
{
  "horaInicioPredeterminada": "09:00:00",
  "duracionPartidoMin": 45,
  "minutosEntrePartidos": 5,
  "numeroCanchas": 3,
  "observacionesPredeterminadas": "string opcional"
}
```

**Body POST inhabilitar:** `InhabilitarConfiguracionRolTorneoRequest`

```json
{
  "motivo": "string requerido"
}
```

Reglas:

- Un solo registro por torneo.
- El torneo debe estar en estado Borrador/Activo para crear.
- `duracionPartidoMin` > 0.
- `minutosEntrePartidos` >= 0.
- `numeroCanchas` > 0.
- `observacionesPredeterminadas` max 500.
- `motivo` max 200 (requerido para inhabilitar).

---

## 22. Cheques

Base: `api/cheques`. Requiere JWT y permisos por accion. Usa SPs: `sp_w_ConsultarCheques`, `sp_w_ConsultarChequeDetalle`, `sp_w_InsertarCheque`, `sp_w_ActualizarCheque`, `sp_w_CambiarEstatusCheque`.

| Metodo | Ruta | Politica | Descripcion |
|--------|------|----------|-------------|
| `GET` | `/api/cheques` | `perm:cheques.ver` | Lista cheques. Filtros opcionales: `texto`, `idCliente`, `idBanco`, `estatusCheque` (1-4), `idStatus` (1-2), `fechaChequeInicio`, `fechaChequeFin`. |
| `GET` | `/api/cheques/{id}` | `perm:cheques.ver` | Obtiene detalle del cheque por Id (GUID), incluyendo historial. |
| `POST` | `/api/cheques` | `perm:cheques.crear` | Crea cheque. Body: `CreateChequeRequest` (`idCliente`, `idBanco`, `numeroCheque`, `monto`, `fechaCheque`, `observaciones?`, `responsableCobroId?`). |
| `PUT` | `/api/cheques/{id}` | `perm:cheques.editar` | Actualiza cheque en estatus Registrado. Body: `UpdateChequeRequest`. |
| `POST` | `/api/cheques/{id}/estatus` | `perm:cheques.activar` | Cambia estatus del cheque. Body: `CambiarEstatusChequeRequest` (`estatusChequeNuevo` 2/3/4, `motivo?`, `observaciones?`, `fechaMovimiento?`). |

Reglas:
- `numeroCheque` requerido, max 50.
- `monto` debe ser mayor a 0.
- `fechaCheque` requerida.
- `observaciones` max 500.
- `motivo` max 300.
- Solo se puede actualizar/cambiar estatus cuando el cheque esta en `Registrado`.
- Para `estatusChequeNuevo` 3 (Devuelto) o 4 (Cancelado), `motivo` es obligatorio.

---

## 23. Utilidades

Endpoints mínimos (Program.cs), sin autenticación JWT:

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/whoami` | Información de configuración: companyId, environment, corsAllowedOrigins, db (nombre de conexión usada). |
| `GET` | `/dbcheck` | Comprueba conexión a SQL Server. Devuelve connected, connection, server, database o error. |

---

## Resumen de autenticación

- **Login:** `POST /api/auth/login` o `POST /api/sesion/login` (sin token).
- **Cabecera en el resto:** `Authorization: Bearer {token}`.
- **Políticas:** Muchos endpoints usan políticas por permiso (ej. `perm:tarimas.ver`, `perm:usuarios.crear`). Sin permiso: 403.
- **Swagger:** En entorno Development, `/swagger` documenta la API y permite probar con JWT.

---

## Notas

- **ErrorController**, **DevController** y **AuthController** están comentados en el código; no exponen rutas activas.
- Rutas con `api/[controller]` resuelven a: Personas → `api/Personas`, Recaudaciones → `api/Recaudaciones`, ProveedoresPagos → `api/ProveedoresPagos`.
- Tarimas: validaciones en crear/actualizar (nombreTarima requerido, longitudes, idTipoCasco > 0, numeroCascosBase 1–99999, observaciones opcional max 500).



