# Dónde va la migración

Documento de relevo. Si otra sesión toma el trabajo, empieza aquí.

**Rama:** `migracion-legacy` en `sacNet-8`. Nunca se trabaja sobre `main`.

## La misión

Mover **toda** la funcionalidad de `Legacy/MacServicios2` y `Legacy/MacReportes2`
al backend nuevo, para que Web y Móvil puedan vivir solo de él.

No se recorta el alcance. Ante la duda, se migra. Un error encontrado **es
trabajo**, no un hallazgo que reportar.

Mac31 puede seguir usando sus servicios viejos: la pregunta de la misión no es
si se puede apagar Legacy, sino si **el backend nuevo ya tiene toda la
capacidad**.

## El tablero

`registro.tsv` es la única verdad del avance. Se actualiza con
`actualizar-registro.py <reporte-de-ola>.md`, que lee la tabla del reporte de
cada ola y empareja por la ruta de Legacy. **No se edita a mano.**

| Estado | Qué significa |
|---|---|
| `hecho` | migrado y comparado contra Legacy ejecutando |
| `hecho-sin-comparar` | migrado; Legacy no puede responder (le falta el SP o la tabla) |
| `por-verificar` | existe en los dos lados y nadie lo comprobó |
| `parcial` | algunos de sus procedimientos ya se usan, otros no |
| `falta` | sin empezar |

`escribe` lo calcula `clasificar-escrituras.py` leyendo los procedimientos.
**No se deduce del nombre**: así se escapó el batch que descuenta los préstamos
de la nómina, que se llama `/validar`.

## Lo que hay que leer antes de tocar nada

| Archivo | Por qué |
|---|---|
| `CUIDADO-consultas-que-escriben.md` | hay "consultas" que escriben; una de ellas hace el cierre del día |
| `PROTOCOLO-ESCRITURAS.md` | cómo se prueba algo que modifica datos |
| `COMO-HABLARLE-A-LEGACY.md` | levantar Legacy y autenticarse (cuesta encontrarlo) |
| `inventario-nuevo-resumen.md` | la arquitectura y el estilo del backend nuevo |
| `seguridad-hallazgos.md` | los agujeros encontrados y cuáles quedan |

## Cómo se levanta todo

```bash
# Legacy (solo lectura; no se le toca un archivo)
"/c/Program Files/IIS Express/iisexpress.exe" \
  /path:"D:\JuanD\GitHub\MAC\Legacy\MacServicios2\Mac3Servicios" \
  /port:63479 /clr:v4.0

# Backend nuevo
cd sacNet-8/ISL_Service/ISL_Service
dotnet build ISL_Service.csproj
dotnet run --no-build --no-launch-profile --urls "http://127.0.0.1:5055"
```

Base: `LAP-JUAND` / `Produccion_svr`, `sa`/`123`. En Git Bash, prefijo
`MSYS_NO_PATHCONV=1` para `sqlcmd`.
Usuario de pruebas: `BancoPruebas` / `Banco2026` (SuperAdmin, creado para esto).

**Ojo con el puerto**: si hay dos backends vivos, las comparaciones mienten. Ya
costó una tarde. Comprueba con `netstat -ano | grep :5055` antes de culpar al
código.

## Comprobar

```bash
cd migracion/comparador
MAC_SCRATCH=<carpeta con legacy.token y nuevo.token> python comparar.py casos/
```

El comparador vigila que la fecha de operación no se mueva a media corrida. Si
se movió, los resultados **no valen**.

## Lo que ya está cerrado

- 117 capacidades migradas y comprobadas; 112 casos de comparación en verde.
- Reportes: **75 de los 99** del menú vivo de Mac31.
- El Estado de Cuenta pasó de >400 s (502 seguro en Azure) a 3.5 s.
- Las claves de permiso nuevas **llegan solas a la base al arrancar**. Antes no,
  y eso dejaba 68 capacidades contestando 403 a todo el que no fuera
  SuperAdmin: migradas y muertas a la vez.
- Agujeros cerrados: pagos a proveedores sin autenticación, reportes visibles
  adivinando un número, nombres de empleados destapables desde el cuerpo.
- CI ahora corre las pruebas, y antes de publicar.

## Lo que falta

Mira el tablero, que es lo vivo. Al escribir esto: **207 sin empezar, 136 a
medias, 17 por verificar**, y de **349 escrituras solo 17 cerradas**.

Hay cuatro frentes trabajando en worktrees aislados (ventas 5081, obra y
aviación 5083, catálogos/inventario/nómina 5085, seguridad 5089). Cada uno
entrega su `ola-N-*.md` y de ahí se actualiza el tablero.

## Bloqueos reales (no excusas)

- **Las bases de Aviación y las de los centros (`MacZCS*`) no están en esta
  máquina.** Se implementa igual deduciendo el contrato de Legacy, se prueba lo
  que se pueda sin base, y el caso queda en `comparador/casos/no-verificables/`
  con su porqué. *No verificable aquí* es aceptable; *no implementado porque no
  pude probarlo* no lo es.
- **`sp_n_rptVentasEstadoCuentaGlobales` está roto en la base**, en las dos
  empresas: llama a otro procedimiento con 6 argumentos y ese recibe 1. Bloquea
  dos reportes y lo tiene que arreglar quien mantiene legacy.
- **Credenciales en el repo** (`Hope5y2k`, la llave del JWT). No se arregla con
  un commit: hay que rotarlas y sacarlas a configuración del servidor.

## Diferencias intencionales

El backend nuevo **no copia** a Legacy: contrato `{ok, message, data}`, permisos
propios, fechas ISO, DTOs sin los textos preconcatenados ni la auditoría
embebida, y `null` donde Legacy inventaba `0` o `0001-01-01`. Cada una se anota
en el `diferencias_intencionales` de su caso, y el comparador las imprime.

La regla: una diferencia o **coincide**, o está **escrita y justificada**. No
hay tercera opción.
