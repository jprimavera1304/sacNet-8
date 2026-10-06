# Hay "consultas" que escriben en la base

Esto cambia cómo se prueba todo. Léelo antes de ejecutar nada contra la base.

## `sp_n_ConsultaConstantes` no consulta: cierra el día

El nombre dice "Consulta". El cuerpo hace esto, leído de la base, no supuesto:

```sql
IF ( CAST(@FechaOperacion as date) < CAST(GETDATE() as date) )
BEGIN
    INSERT INTO FechaOperacionLog (...)          -- apunta el cambio de fecha
    DELETE AlmacenProductoHist WHERE Fecha = @FechaAnterior
    INSERT INTO AlmacenProductoHist (...)        -- "freeze del inventario"
    UPDATE Constantes SET FechaOperacion = hoy   -- rueda la fecha de operación
END
```

Y además, en todas las llamadas, registra actividad del usuario
(`UsuariosActividad`) y puede crear su renglón en `[Usuarios Fechas]`.

O sea: **es la rutina de cierre del día, y la dispara el primero que toque el
sistema cada mañana.** Congela las existencias del día anterior y adelanta la
fecha de operación del negocio.

**155 de las 488 capacidades de Legacy la llaman**, casi siempre como primer
paso para leer los parámetros de la empresa. Un tercio del sistema.

## Por qué importa para esta migración

1. **Una prueba "de solo lectura" puede mover la fecha de operación del negocio
   y rehacer el corte de inventario.** Si alguien corre comparaciones un lunes
   contra una base que se quedó en viernes, el primer endpoint que toque hace el
   cierre. No es hipotético: pasa a la primera llamada.

2. **Rompe la comparación en silencio, y de la peor forma.** Legacy y el backend
   nuevo llaman al mismo procedimiento. El que corra primero hace el cierre; el
   segundo ya encuentra la fecha movida y lee otros datos. La diferencia que
   salga no es del backend: es del orden en que se preguntó.

3. Por eso el banco de comparación **avisa** cuando la fecha de operación cambia
   durante una corrida (ver abajo), y por eso las pruebas que escriben van
   dentro del ciclo de instantánea.

## Estado de hoy

```
fecha_operacion = 2026-10-06    hoy = 2026-10-06    -> no dispara
```

Hoy ya rodó, así que ahora mismo está inofensivo. **Mañana no.**

## La clasificación del tablero se rehízo por esto

La primera pasada dedujo si una capacidad escribe **por el nombre** del
procedimiento (`Consulta*` lee, `Actualizar*` escribe). Casi siempre acierta, y
cuando falla es peligroso. El caso que lo destapó:

> `/api/empleadosprestamo/validar` estaba marcado como lectura.
> `sp_n_ValidaEmpleadosPrestamos` abre `BEGIN TRAN`, inserta en
> `EmpleadoPrestamosPagos` y actualiza `EmpleadoPrestamos`. **Es el batch que
> descuenta los préstamos de la nómina.**

Ahora lo decide el cuerpo del procedimiento, leído de `sys.sql_modules`
(`clasificar-escrituras.py`). Resultado:

| | antes | ahora |
|---|---|---|
| Solo leen | 210 | **143** |
| Escriben | 278 | **308** |
| No se sabe (el SP no está en esta base) | 0 | **37** |

**86 capacidades que decían leer, escriben.** Si esto no se hubiera revisado,
tarde o temprano alguien las habría ejecutado creyéndolas inofensivas.

### Sobre el método

Se probaron tres formas y se quedó la tercera:

1. Traer el texto de los ~600 procedimientos en un solo `SELECT`: **no sirve**,
   sqlcmd parte y recorta las líneas largas.
2. Detectarlo dentro de SQL con `LIKE '%INSERT %'`: robusto de transportar pero
   **demasiado tosco** — marcaba 138 lecturas como escrituras (cuenta un INSERT
   a una tabla temporal, o dentro de un comentario). Una señal que se equivoca
   tanto no la mira nadie.
3. Pedirlos uno por uno y analizarlos con cuidado, descartando `#temporales` y
   variables de tabla. Son ~600 viajes y un par de minutos. **Es la que quedó.**

Ante la duda, el sesgo es decir "escribe": equivocarse así hace que alguien
pruebe con más cuidado del necesario; equivocarse al revés hace que ejecute
contra la base algo que creía inofensivo.

## Qué hacer a partir de aquí

- Antes de una tanda de comparaciones, **mirar la fecha de operación**. Si está
  atrasada, dejar que ruede una vez a propósito y de forma controlada, y recién
  entonces comparar.
- Las 308 que escriben **solo se prueban dentro del ciclo de instantánea**
  (`comparador/instantanea.sh`).
- Las 37 con `?` son de bases que no están en esta máquina (Aviación). No se
  tratan como lecturas.
