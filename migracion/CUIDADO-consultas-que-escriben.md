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

| | por el nombre | leyendo el procedimiento |
|---|---|---|
| Solo leen | 210 | **102** |
| Pueden escribir | 278 | **349** |
| No se sabe (el SP no está en esta base) | 0 | **37** |

**Más de cien capacidades que decían leer, escriben.** Si esto no se hubiera
revisado, tarde o temprano alguien las habría ejecutado creyéndolas
inofensivas.

### Sobre el método

Se probaron tres formas y se quedó la tercera:

1. Deducirlo del **nombre** del procedimiento. Rápido y equivocado.
2. Traer el texto de cada procedimiento y analizarlo en Python. Lo correcto en
   teoría, pero **sqlcmd recorta las definiciones largas**, así que el análisis
   miraba solo el principio. Fallaba en silencio y en la dirección cara: así se
   le escaparon cuatro procedimientos que sí escriben, entre ellos
   `sp_n_CancelarBancoTransferencia`, que actualiza Ventas, Pagos y Clientes.
3. **Detectarlo dentro de SQL**, donde vive el texto entero, descontando las
   escrituras a tablas temporales. Solo cruza un 0 o un 1. Es la que quedó.

El `si` **sobre-reporta a propósito**. No distingue una escritura real de un
INSERT que quedó dentro de un bloque `/* */` con consultas de apunte, y los
`sp_n_` tienen montones de esos arriba — `sp_n_ConsultaClientes`, por ejemplo,
sale marcado por un `UPDATE Clientes ... WHERE IDCliente = 19222` que vive en
ese bloque de notas.

Ante la duda, el sesgo es decir "escribe": equivocarse así hace que alguien
pruebe con más cuidado del necesario; equivocarse al revés hace que ejecute
contra la base algo que creía inofensivo. Por eso **`no` significa comprobado y
`si` significa trátalo con cuidado**, no "escribe seguro".

## Qué hacer a partir de aquí

- Antes de una tanda de comparaciones, **mirar la fecha de operación**. Si está
  atrasada, dejar que ruede una vez a propósito y de forma controlada, y recién
  entonces comparar.
- Las 308 que escriben **solo se prueban dentro del ciclo de instantánea**
  (`comparador/instantanea.sh`).
- Las 37 con `?` son de bases que no están en esta máquina (Aviación). No se
  tratan como lecturas.
