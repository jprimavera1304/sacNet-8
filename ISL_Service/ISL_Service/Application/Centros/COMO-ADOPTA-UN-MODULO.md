# Cómo adopta un módulo los centros de servicio

Tres pasos. Ninguno toca permisos, cadenas de conexión ni nombres de bases.

## 1. Declararse — una línea

En `ModulosConCentros.cs`, dentro de `Registro`:

```csharp
["cortes"] = ModuloConCentros.SoloLectura("cortes"),
```

o, si además va a escribir fuera de la matriz, nombrando **una por una** las
operaciones que escriben:

```csharp
["ventas"] = ModuloConCentros.ConEscritura("ventas", "capturar_pedido", "cancelar"),
```

Mientras no esté en esa lista, el módulo con `X-Centro != 0` contesta **409** y
nunca datos de la matriz. Esa es la omisión y es a propósito.

## 2. Pedir la conexión en vez de armarla

El repositorio inyecta `IConexionDelCentro` y cambia su `GetConnection()` por:

```csharp
await using var conn = await _centro.AbrirParaLeerAsync("cortes", ct);
```

o, para escribir:

```csharp
await using var conn = await _centro.AbrirParaEscribirAsync("ventas", "capturar_pedido", ct);
```

La conexión llega **abierta y ya validada**: existe el centro, está activo, su
base pertenece a esta empresa, el usuario tiene `ver`/`operar` y el módulo está
declarado. Si algo falla sale una `ConflictException` que el middleware
convierte en 409 con el motivo en español
(`Infrastructure/Middleware/ExceptionHandlingMiddleware.cs:56`).

Con centro 0 (matriz, y es lo que pasa cuando no viene la cabecera) la cadena
se queda **tal cual**: es el camino de hoy, byte por byte.

## 3. Si el repositorio ya sabe recibir un nombre de base

Es el caso de `ReportesVentasRepository`, que tiene
`GetConnection(string? baseDelCentro)`. No hace falta cambiarle la forma de
abrir: se le pasa el nombre **ya validado**.

```csharp
var baseDelCentro = await _centro.BaseValidadaParaLeerAsync("reportes", idCentro, ct);
```

Devuelve `""` para la matriz, que es justo lo que `BaseDelCentro.Apuntar` ya
interpreta como "no toques la cadena". Es el camino de adopción gradual: el
repositorio deja de decidir y pasa a preguntar, sin reescribirse.

## Lo que un módulo NO tiene que hacer

- **No** leer `X-Centro`. La capa lo hace.
- **No** mirar permisos de centros. La capa lo hace.
- **No** llamar a `CentrosServicioRepository` para traducir Id → base.
- **No** armar `GetConnectionString("Main") ?? ...`. Para la base principal sin
  centros está `Infrastructure/Data/CadenaDeLaEmpresa.cs`, que es el mismo
  orden de fallbacks que las 275 copias de hoy.

## Las 275 copias

No se migran de golpe, a propósito: cada repositorio que las use sigue
funcionando igual mientras no se le toque, porque todas piden la base principal
y la base principal es el centro 0. Se adoptan de uno en uno, cuando a cada uno
le toque por otro motivo.
