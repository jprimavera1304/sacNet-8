# Cómo se migra y se comprueba una operación que ESCRIBE

Obligatorio para todo lo que modifique datos: cobrar, cancelar, autorizar,
facturar, devolver, mover inventario, generar folios, dar de alta, modificar.

## Por qué no sirve el método de las consultas

Comparar una consulta es fácil: se pregunta dos veces y se miran las dos
respuestas. Una escritura no: **la primera llamada cambia la base**, así que la
segunda ya no arranca del mismo sitio y lo que salga no significa nada.

Hay que poder volver atrás. Para eso están las instantáneas.

## El ciclo

```bash
cd migracion/comparador

./instantanea.sh crear          # una sola vez por tanda

# --- lado Legacy ---
./instantanea.sh volver         # estado limpio
curl ... http://localhost:63479/api/loquesea      # ejecutar en Legacy
# anotar: la respuesta HTTP + el estado de la base (ver abajo)

# --- lado nuevo ---
./instantanea.sh volver         # MISMO estado limpio
curl ... http://127.0.0.1:50XX/api/loquesea       # ejecutar en el nuevo
# anotar: la respuesta HTTP + el estado de la base

./instantanea.sh volver         # dejar limpio al terminar
```

Las dos llamadas salen **exactamente del mismo estado**. Eso es lo único que
hace honesta la comparación.

> `volver` necesita la base en exclusiva, así que tira las conexiones de los dos
> backends. Reconectan solos en la siguiente petición; no hay que reiniciar
> nada.

## Qué se compara: DOS cosas, no una

1. **La respuesta HTTP.** Con el comparador de siempre.
2. **El efecto en la base.** Esto es lo que de verdad importa en una escritura,
   y el comparador no lo ve. Antes y después de cada lado, saca una foto de las
   tablas que toca:

```sql
SELECT COUNT(*), SUM(Importe), MAX(IDVenta) FROM dbo.[Ventas Pagos] WHERE ...
```

Y compara las dos fotos. Si Legacy deja tres renglones y el nuevo deja dos, da
igual que la respuesta HTTP sea idéntica: **está mal**.

Qué mirar en la foto, según el caso: renglones insertados, campos modificados,
el folio que se consumió, el estatus que cambió, la bitácora que se escribió, y
los totales.

## Antes de ejecutar nada

1. **Lee `CUIDADO-consultas-que-escriben.md`.** Hay "consultas" que escriben.
2. **Mira la fecha de operación** y vuelve a mirarla al terminar:
   ```bash
   sqlcmd -S LAP-JUAND -d Produccion_svr -U sa -P 123 -C -W -I \
     -Q "SELECT FechaOperacion FROM dbo.Constantes"
   ```
   Si se movió a media tanda, `sp_n_ConsultaConstantes` hizo el cierre del día y
   **los resultados no valen**: restaura y repite.
3. **Comprueba que el backend sea el mismo** al principio y al final. Si alguien
   recompila y reinicia a media tanda, las diferencias que salgan no son del
   código que creías. Ya pasó una vez y costó una tarde.

## Lo que NO se hace nunca

- **No se ejecuta nada contra producción.** Solo la base local.
- **No se modifica la base para que una prueba pase.** Ni procedimientos, ni
  tablas, ni datos. Si no coincide, el que está mal es el backend nuevo.
- **No se toca `Legacy/`.** Es referencia de lectura y ejecución.
- No se deja la instantánea colgando: `soltar` al terminar la jornada.

## Cuando no se puede comparar

Pasa, y es legítimo. Casos reales ya vistos:

- el procedimiento no existe en esta base (módulos de Aviación);
- la tabla no existe;
- Legacy revienta por un defecto suyo;
- la operación manda correo o timbra un CFDI de verdad.

En ese caso: **implementa igual**, deja el caso en
`comparador/casos/no-verificables/` con un `_porque` que explique qué falta,
escribe pruebas unitarias de la lógica que sí se puede probar, y que otro
agente lo revise estáticamente contra Legacy.

Lo que **no** es aceptable es no implementar algo porque no se podía probar.

## Orden recomendado dentro de un módulo

1. Las altas (crean el dato con el que se prueba lo demás).
2. Las modificaciones.
3. Las cancelaciones y bajas.
4. Lo que mueve dinero o inventario, al final y con más cuidado.

Así cada prueba tiene con qué trabajar sin depender de datos que ya estaban.
