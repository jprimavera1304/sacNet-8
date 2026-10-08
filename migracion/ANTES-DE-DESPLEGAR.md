# Antes de desplegar el backend nuevo: lo que hay que hacer en la base

No es codigo. Son cosas que, si se suben sin hacerlas, dejan pantallas en 403 o
en blanco **sin nada en la consola que lo explique**.

## 1. `clientes.ver` no la tiene nadie

`GET /api/catalogos/clientes` ya **exige** `perm:clientes.ver`. El cambio es
correcto —antes no pedia ningun permiso y cualquier autenticado se llevaba los
887 clientes de los 15 agentes— pero la clave hay que darla.

Medido en `MacZ` el 07-oct-2026:

| Rol | Usuarios activos | Con `clientes.ver` |
|---|---|---|
| SuperAdmin | 3 | 0 (pero la politica los exime) |
| Admin | 1 | **0** |
| User | 2 | **0** |
| CHECADOR | 1 | **0** |
| EMPLEADO | 1 | **0** |

La clave **si** esta dada de alta en `WPermiso`; lo que falta es asignarla.

Como se porta cada camino, leido en
`Infrastructure/Security/PermissionAuthorizationHandler.cs`:

- `rolLegacy = SuperAdmin` → pasa siempre, sin mirar permisos.
- Con el sistema de permisos **activo** y sin la clave → **403**.
- Con el sistema **inactivo** → cae en `IsAllowedByLegacy`, que para una clave
  que no esta en `LegacyPolicyMap` devuelve `true`. O sea que ese camino **falla
  abierto** y no rompe.

Asi que el riesgo real son los **5 usuarios que no son SuperAdmin** y que
tengan el sistema de permisos activo. El front viejo consume ese endpoint, asi
que lo que se cae no es una pantalla nueva: es una que hoy funciona.

**Antes de subir el backend: asignar `clientes.ver` a los roles que deban ver
clientes.** Y de paso es el momento de decidir si `User`, `CHECADOR` y
`EMPLEADO` deben verlos.

## 2. Las claves de modulo que el API todavia no publica

`GET api/modulos/disponibles` no devuelve para Zaragoza las claves de los
modulos nuevos, asi que **sus tarjetas no salen en el Inicio** aunque la
pantalla exista y funcione:

`clientes` · `productos` · `proveedores` · `inventario_ajustes` ·
`inventario_transito` · `catalogos` · `cobros` · `cobranzas` · `dinero_usados`

No es un defecto del front: el Inicio solo pinta lo que el API publica. Hay que
darlas de alta como modulos.

## 3. `staticwebapp.config.json`

Cada pantalla nueva necesita su renglon o **produccion contesta 404** aunque en
local funcione. Catalogos ya lo tiene (un solo renglon para los veinte, por eso
se hizo asi). Faltan las de cobranza, clientes, productos, proveedores e
inventario.

## 4. Los dos procedimientos nuevos, sin instalar

Los dos **crean** un objeto nuevo y no modifican ninguno existente:

- `Querrys/31 Constantes Solo Lectura/` — sin esto hay **seis sitios del backend
  que cierran el dia del negocio solo por leer** (ver
  `CUIDADO-consultas-que-escriben.md`).
- `Querrys/32 Reporte Autos Columna Mala/` — sin esto el reporte de Autos no
  sale, igual que no sale hoy desde Mac31.

## 5. Lo que NO esta decidido

- **La contraseña en mayusculas del web.** Parte el padron de usuarios web en
  dos. Esta en `seguridad-hallazgos.md` con los numeros.
- **Los cuatro catalogos sin permiso** (`bancos`, `repartidores`,
  `tipos-casco`, `tarimas`). Antes de exigirles una clave hay que contar quien
  la tiene, que es justo lo que este documento acaba de hacer con `clientes.ver`
  y lo que no se hizo cuando 68 capacidades se quedaron contestando 403.
