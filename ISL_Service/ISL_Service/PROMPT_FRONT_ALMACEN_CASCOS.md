# Prompt: Frontend módulo Almacén de Cascos

Usa este texto como prompt para implementar el frontend que consume las APIs del módulo **Almacén de Cascos**. El backend es una API REST ASP.NET Core; todas las rutas requieren **JWT** en cabecera: `Authorization: Bearer {token}`.

---

## Contexto

- **Base URL API:** la que uses en el proyecto (ej. `https://api.ejemplo.com` o `http://localhost:5xxx`).
- **Autenticación:** usuario ya logueado; el token se envía en todas las peticiones a `/api/almacen-cascos` y `/api/catalogos` (repartidores, tarimas, tipos-casco).
- **Convenciones de negocio:**
  - **TipoMovimiento:** `1` = SALIDA, `2` = ENTRADA.
  - **Estatus movimiento:** `1` = REGISTRADA, `2` = ACEPTADA, `3` = CANCELADA.
  - **Catálogos (IdStatus):** `1` = ACTIVO, `2` = CANCELADO. Para dropdowns usar siempre `status=1`.

---

## Catálogos (para formularios y dropdowns)

Cargar al iniciar el módulo o cuando se necesiten. Base: `GET /api/catalogos/...` con query `?status=1`.

| Uso en UI | Método | Ruta | Query | Respuesta |
|-----------|--------|------|-------|-----------|
| Dropdown repartidor entrega/recibe | GET | `/api/catalogos/repartidores` | `?status=1` | `{ idRepartidor: number, repartidor: string }[]` |
| Dropdown tipo de casco | GET | `/api/catalogos/tipos-casco` | `?status=1` | `{ idTipoCasco: number, descripcion: string }[]` |
| Dropdown tarimas (para detalle de salida) | GET | `/api/catalogos/tarimas` | `?status=1` | `{ idTarima: number, nombreTarima: string, idTipoCasco: number, numeroCascosBase: number }[]` |

---

## Endpoints Almacén de Cascos

Base: `/api/almacen-cascos`. Todas las peticiones con JWT.

### 1) Listar movimientos

- **Método:** `GET`
- **Ruta:** `/api/almacen-cascos/movimientos`
- **Query (todos opcionales):**
  - `estatus`: number | null — 1 REGISTRADA, 2 ACEPTADA, 3 CANCELADA; si no se envía = todos.
  - `tipoMovimiento`: number — 1 SALIDA, 2 ENTRADA.
  - `fechaInicio`: string (fecha ISO o YYYY-MM-DD).
  - `fechaFin`: string (fecha ISO o YYYY-MM-DD).

- **Respuesta 200:** array de movimientos (cabecera), ejemplo de item:

```ts
{
  idMovimiento: number;
  tipoMovimiento: number;        // 1 SALIDA, 2 ENTRADA
  idMovimientoSalida: number | null;
  estatus: number;               // 1 REGISTRADA, 2 ACEPTADA, 3 CANCELADA
  idRepartidorEntrega: number | null;
  idRepartidorRecibe: number | null;
  totalTarimas: number;
  totalPiezas: number;
  totalKilos: number;
  observaciones: string | null;
  motivoCancelacion: string | null;
  usuarioCreacion: string | null;
  fechaCreacion: string | null;  // ISO
  usuarioCancelacion: string | null;
  fechaCancelacion: string | null;
  entregaNombre: string | null;  // nombre repartidor entrega
  recibeNombre: string | null;   // nombre repartidor recibe
}
```

---

### 2) Detalle de un movimiento

- **Método:** `GET`
- **Ruta:** `/api/almacen-cascos/movimientos/{idMovimiento}/detalle`
- **Parámetro de ruta:** `idMovimiento` (number).

- **Respuesta 200:** array de líneas de detalle:

```ts
{
  idDetalle: number;
  idMovimiento: number;
  idTarima: number;
  idTipoCasco: number;
  numeroTarima: number;
  piezas: number;
  nombreTarima: string | null;
  tipoCascoDescripcion: string | null;
}[]
```

---

### 3) Crear SALIDA

- **Método:** `POST`
- **Ruta:** `/api/almacen-cascos/salidas`
- **Body (JSON):**

```ts
{
  idRepartidorEntrega: number;   // requerido, debe ser id de repartidor activo
  observaciones?: string;
  detalle: {
    idTarima: number;            // tarima activa
    numeroTarima: number;         // > 0
    piezas: number;              // > 0
  }[];
}
```

- **Validaciones front (recomendadas):** `detalle.length >= 1`, cada item con `idTarima` > 0, `numeroTarima` > 0, `piezas` > 0.
- **Respuesta 201:** `{ idMovimiento: number }`. Crear cabecera + detalle en backend; totales se calculan allí.
- **Errores:** 400 si validación o repartidor/tarima no activos; mensaje en body (ej. `{ message: string }`).

---

### 4) Aceptar ENTRADA (desde una salida registrada)

- **Método:** `POST`
- **Ruta:** `/api/almacen-cascos/entradas`
- **Body (JSON):**

```ts
{
  idMovimientoSalida: number;    // salida ya registrada y con detalle
  idRepartidorRecibe: number;    // repartidor activo
  kilos: number;                 // > 0, hasta 4 decimales
  observaciones?: string;
}
```

- **Respuesta 200:** ej. `{ message: "Entrada aceptada." }`.
- **Errores:** 400 si salida sin detalle o no registrada, repartidor no activo, kilos ≤ 0. **409** si ya existe una entrada para esa salida (doble entrada).

---

### 5) Cancelar movimiento

- **Método:** `POST`
- **Ruta:** `/api/almacen-cascos/movimientos/{idMovimiento}/cancelar`
- **Parámetro de ruta:** `idMovimiento` (number).
- **Body (JSON):**

```ts
{
  motivoCancelacion: string;     // requerido, no vacío
}
```

- **Respuesta 200:** ej. `{ message: "Movimiento cancelado." }`.
- **Errores:** 400 si motivo vacío o regla de negocio. **409** si ya está cancelado o no se puede cancelar (ej. salida con entrada aceptada).

---

## Flujos sugeridos en la UI

1. **Listado de movimientos**
   - Pantalla con tabla/cards de movimientos.
   - Filtros: estatus (dropdown: Todos / REGISTRADA / ACEPTADA / CANCELADA), tipo (SALIDA / ENTRADA), rango de fechas (fechaInicio, fechaFin).
   - Llamar `GET /api/almacen-cascos/movimientos` con query params. Mostrar columnas: idMovimiento, tipo (SALIDA/ENTRADA), estatus (REGISTRADA/ACEPTADA/CANCELADA), entregaNombre, recibeNombre, totalTarimas, totalPiezas, totalKilos, fechaCreacion.
   - Acción por fila: “Ver detalle” → navegar a detalle o abrir modal.

2. **Detalle de un movimiento**
   - Ruta tipo `/almacen-cascos/movimientos/:id` o modal.
   - Llamar `GET /api/almacen-cascos/movimientos/{id}/detalle` y mostrar tabla de líneas (nombreTarima, tipoCascoDescripcion, numeroTarima, piezas).
   - Si estatus = REGISTRADA y es SALIDA: mostrar botón “Aceptar entrada” (ir al flujo 4).
   - Si no está cancelado: mostrar botón “Cancelar” que pida motivo y llame a flujo 5.

3. **Alta de SALIDA**
   - Formulario: dropdown repartidor entrega (desde `GET /api/catalogos/repartidores?status=1`), observaciones (opcional), tabla/grid de detalle.
   - Detalle: por fila, dropdown tarima (desde `GET /api/catalogos/tarimas?status=1`), numeroTarima (número), piezas (número). Botón “Agregar línea” y validar al menos una línea con valores > 0.
   - Enviar `POST /api/almacen-cascos/salidas` con el body indicado. En 201, mostrar idMovimiento y redirigir al listado o al detalle del movimiento creado.

4. **Aceptar ENTRADA (desde una salida)**
   - Desde el detalle de una SALIDA en estatus REGISTRADA, botón “Aceptar entrada”.
   - Formulario/modal: dropdown repartidor recibe (`/api/catalogos/repartidores?status=1`), campo kilos (numérico, > 0, 4 decimales), observaciones opcional. idMovimientoSalida = id del movimiento actual.
   - Enviar `POST /api/almacen-cascos/entradas`. En 200, actualizar vista (recargar listado/detalle). En 409 mostrar mensaje “Ya existe una entrada para esta salida”.

5. **Cancelar movimiento**
   - Botón “Cancelar” en listado o detalle. Modal con campo obligatorio “Motivo de cancelación”.
   - Enviar `POST /api/almacen-cascos/movimientos/{idMovimiento}/cancelar` con `{ motivoCancelacion }`. En 200, recargar. En 409 mostrar mensaje del backend (ej. “Ya está cancelado” o “No se puede cancelar”).

---

## Manejo de errores

- **400:** Mostrar `response.body.message` (o `detail` si existe) al usuario.
- **409:** Mensaje de conflicto (doble entrada, ya cancelado, no se puede cancelar); mostrar texto del backend.
- **401:** Redirigir a login o renovar token.
- **403:** Sin permiso; mensaje acorde.

Si el backend devuelve JSON con `message` y opcionalmente `detail`, usarlos en toasts o mensajes bajo el formulario.

---

## Resumen de URLs para el front

```
GET  /api/catalogos/repartidores?status=1
GET  /api/catalogos/tipos-casco?status=1
GET  /api/catalogos/tarimas?status=1

GET  /api/almacen-cascos/movimientos?estatus=&tipoMovimiento=&fechaInicio=&fechaFin=
GET  /api/almacen-cascos/movimientos/{idMovimiento}/detalle
POST /api/almacen-cascos/salidas
POST /api/almacen-cascos/entradas
POST /api/almacen-cascos/movimientos/{idMovimiento}/cancelar
```

Implementa el módulo de Almacén de Cascos consumiendo estas APIs: listado con filtros, detalle de movimiento, alta de salida (con detalle desde catálogo de tarimas), aceptar entrada desde una salida registrada, y cancelar movimiento con motivo. Usa los catálogos para repartidores, tarimas y tipos de casco donde corresponda.
