# Migración de Legacy y MacReportes al backend nuevo

Carpeta de gobierno de la migración. **No es código de producción**: aquí viven
el inventario, el banco de comparación y el registro de lo que ya se movió.

## Qué hay

| Archivo | Para qué |
|---|---|
| `COMO-HABLARLE-A-LEGACY.md` | Cómo levantar Legacy local y autenticarse. Costó encontrarlo. |
| `inventario-legacy.tsv` | Todo lo que expone MacServicios2, método por método. |
| `inventario-macreportes.tsv` | Todo lo que expone MacReportes. |
| `inventario-nuevo.tsv` | Lo que ya tiene el backend nuevo hoy. |
| `registro.tsv` | El estado de cada capacidad. Es el tablero de la migración. |
| `comparador/` | La herramienta que decide si un endpoint quedó bien. |

## La regla que gobierna todo

Un endpoint **no está migrado porque compile ni porque se vea bien**. Está
migrado cuando el comparador manda la misma operación a los dos backends,
contra la misma base, y los datos coinciden.

La jerarquía de confianza, de más a menos:

1. Comparación ejecutada contra Legacy.
2. Prueba automatizada.
3. Revisión por un agente distinto del que implementó.
4. Inspección manual.
5. "Parece correcto" — **no cuenta**.

## Cómo se corre el banco

Con los dos backends levantados (ver `COMO-HABLARLE-A-LEGACY.md` para Legacy, y
`dotnet run --no-launch-profile --urls http://127.0.0.1:5055` para el nuevo):

```bash
cd migracion/comparador
MAC_SCRATCH=<carpeta con legacy.token y nuevo.token> python comparar.py casos/
```

Sale `OK` o `DIF` por caso. `DIF` dice en qué campo y qué valor tiene cada lado.

## Diferencias intencionales

El backend nuevo **no copia** a Legacy: tiene su propio contrato, sus permisos y
sus mejoras. Cuando una diferencia es a propósito, se escribe en el caso
(`diferencias_intencionales`) y el comparador la imprime cada vez.

Eso es deliberado: una diferencia o **coincide**, o está **anotada y
justificada**. No hay tercera opción, y nadie puede colar un cambio accidental
diciendo que fue a propósito.

## Lo que el comparador ya sabe perdonar

Porque son diferencias de forma, no de contenido:

- el envoltorio (`result` contra `data`);
- el orden de los renglones (hay SP sin `ORDER BY`);
- el nombre de los campos, vía `mapa_campos`;
- números como texto, `""` contra `null`, decimales, formatos de fecha;
- campos no deterministas (tokens, timestamps, folios recién generados).
