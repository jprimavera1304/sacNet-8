# Prompt: Frontend modulo Almacen de Cascos

Usa este texto como prompt para implementar el frontend que consume las APIs del modulo **Almacen de Cascos**. Todas las rutas requieren JWT en cabecera: `Authorization: Bearer {token}`.

---

## Contexto

- Base URL API: la del proyecto.
- Autenticacion: usuario logueado, enviar token en `/api/almacen-cascos` y `/api/catalogos`.
- Convenciones:
  - `tipoMovimiento`: `1` SALIDA, `2` ENTRADA.
  - `estatus`: `1` REGISTRADA, `2` ACEPTADA, `3` CANCELADA.
  - Catalogos: usar `status=1` para activos.

---

## Catalogos

| Uso | Metodo | Ruta | Query | Respuesta |
|---|---|---|---|---|
| Repartidor entrega/recibe | GET | `/api/catalogos/repartidores` | `?status=1` | `{ idRepartidor, repartidor }[]` |
| Tipo de casco | GET | `/api/catalogos/tipos-casco` | `?status=1` | `{ idTipoCasco, descripcion }[]` |
| Tarimas (legacy) | GET | `/api/catalogos/tarimas` | `?status=1` | `{ idTarima, nombreTarima, idTipoCasco, numeroCascosBase }[]` |

---

## Endpoints Almacen

Base: `/api/almacen-cascos`

### 1) Listar movimientos

- `GET /api/almacen-cascos/movimientos`
- Query opcional: `estatus`, `tipoMovimiento`, `fechaInicio`, `fechaFin`

### 2) Detalle de movimiento

- `GET /api/almacen-cascos/movimientos/{idMovimiento}/detalle` (plano)
- `GET /api/almacen-cascos/movimientos/{idMovimiento}/detalle-agrupado` (recomendado para UI)

Respuesta agrupada:

```ts
{
  idMovimiento: number;
  tarimas: {
    numeroTarima: number;
    lineas: {
      idDetalle: number;
      idTarima: number | null;
      idTipoCasco: number;
      tipoCascoDescripcion: string | null;
      piezas: number;
    }[];
  }[];
}
```

### 3) Crear SALIDA

- `POST /api/almacen-cascos/salidas`

```ts
{
  idRepartidorEntrega: number;
  observaciones?: string;
  tarimas: {
    numeroTarima: number;   // > 0, no repetido
    lineas: {
      idTipoCasco: number;  // activo
      piezas: number;       // > 0
    }[];
  }[];
}
```

Validar en front:
- `tarimas.length >= 1`
- `numeroTarima` unico
- cada tarima con `lineas.length >= 1`
- cada linea con `idTipoCasco > 0` y `piezas > 0`

### 4) Aceptar ENTRADA

- `POST /api/almacen-cascos/entradas`

```ts
{
  idMovimientoSalida: number;
  idRepartidorRecibe: number;
  kilos: number;            // > 0, 4 decimales
  observaciones?: string;
  detalle?: {
    numeroTarima: number;
    idTipoCasco: number;
    piezas: number;
  }[];                      // opcional: valida contra salida
}
```

### 5) Cancelar movimiento

- `POST /api/almacen-cascos/movimientos/{idMovimiento}/cancelar`

```ts
{ motivoCancelacion: string }
```

---

## Flujo UI requerido

1. En alta de salida, capturar `numero de tarimas`.
2. Si usuario pone 3, crear Tarima 1, Tarima 2, Tarima 3 por default.
3. Permitir agregar mas tarimas.
4. En cada tarima, permitir agregar 1..N líneas (`idTipoCasco + piezas`).
5. Enviar payload del endpoint de salidas con estructura `tarimas -> lineas`.
6. Para detalle/edicion, consumir `detalle-agrupado`.

---

## URLs resumen

```txt
GET  /api/catalogos/repartidores?status=1
GET  /api/catalogos/tipos-casco?status=1

GET  /api/almacen-cascos/movimientos?estatus=&tipoMovimiento=&fechaInicio=&fechaFin=
GET  /api/almacen-cascos/movimientos/{idMovimiento}/detalle
GET  /api/almacen-cascos/movimientos/{idMovimiento}/detalle-agrupado
POST /api/almacen-cascos/salidas
POST /api/almacen-cascos/entradas
POST /api/almacen-cascos/movimientos/{idMovimiento}/cancelar
```
