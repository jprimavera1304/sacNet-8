# Sistema de componentes del Web nuevo (docs2)

> **Para qué es este documento.** Al Web nuevo le van a llegar muchos módulos a
> la vez, construidos por varios agentes. Esto fija cómo se arma uno para que
> salga igual que los demás: qué componente usar para cada cosa, qué se hereda
> gratis y qué sigue siendo responsabilidad de la pantalla.
>
> **Si vas a hacer un módulo nuevo, lee solo la sección [Cómo se arma un módulo
> nuevo](#4-cómo-se-arma-un-módulo-nuevo) y copia
> `Web/QR_Web/docs2/src/paginas/molde/`.** Lo demás es el porqué.
>
> El molde se abre en el navegador: `#/molde`.

---

## 1. Lo que ya se repetía (con números)

Se recorrieron las **14 pantallas** de `docs2/src/paginas/` contando cada patrón
y sus variantes.

El punto de partida es bueno: docs2 **ya tiene** un sistema de componentes
(`Button`, `Field`, `Select`, `DatePicker`, `Modal`, `Panel`, `Alert`,
`Checkbox`, `BarraSuperior`, `BarraDeAcciones`, `RejillaDePermisos`,
`Skeleton`…) y su README ya dice la regla correcta: *un botón no se puede
escribir a mano*.

**El problema es que esa regla solo cubre los ladrillos.** Un botón no se
escribe a mano; una tabla sí. Y una tabla es donde vive el 80% de una pantalla.

### Censo por patrón

| # | Patrón | Ocurrencias | Variantes | ¿Había componente? |
|---|---|---|---|---|
| 1 | Tablas `<table>` | **22** en 13 pantallas | 13 bloques de CSS, **166 reglas** | No |
| 2 | Estados vacíos / error / "no hay nada" | **28** | **18 nombres de clase** | No |
| 3 | Diálogos `<Modal>` | **38** en 17 archivos | 14 son la misma pregunta de "¿seguro?" | Solo el envase |
| 4 | Preguntas por un permiso | **116 líneas** | **4 formas** de contestarlas | No |
| 5 | Barras de filtros | **8** | 6 nombres de clase | No |
| 6 | Títulos de pantalla | 7 pantallas | **12 clases**, 4 tamaños de letra | No |
| 7 | Colores de pastilla de estatus | 9 pantallas | **26 clases**, 4 parejas sinónimas | Solo la forma (`Etiqueta`) |
| 8 | Pastilla de conteo ("12 módulos") | 3 pantallas | 3 definiciones idénticas | No |
| 9 | Campos de búsqueda | 5 pantallas | 5 clases, **ninguna con botón de borrar** | No |
| 10 | Menús de acciones (desbordamiento) | 1 (Ventas, a mano) | 1 | No |
| 11 | Tarjeta de pantalla (`__tarjeta`) | 9 pantallas | 9 definiciones, **3 radios distintos** | `Panel`, usado en **2 de 14** |
| 12 | Paginación / "ver más" | 1 (Ventas) | 1 | No |

**12 patrones duplicados.**

### Los tres hallazgos que importan más que el conteo

**1. Once de las trece tablas se usan arrastrando la pantalla de lado.**
`min-width` en la tabla (de 480 a 1100px) + `overflow: auto` en su caja. En una
ventana de 390px eso significa que para leer *una fila* hay que arrastrar a la
derecha y volver. Con 20 filas son 40 arrastres para leer la pantalla. Es el
peor defecto del front nuevo hoy, y no estaba anotado en ningún lado.

**2. Ocho de las trece tablas no se pueden ordenar por ninguna columna.** En
Ventas, con 200 remisiones, encontrar la más vieja es leerlas todas.

**3. Seis de los catorce diálogos de "¿seguro?" se pueden pulsar dos veces**,
porque su botón de confirmar no se apaga mientras la petición va en camino. Es
el mismo bug de doble envío que `<Button cargando>` resolvió en el Login,
reaparecido doce pantallas después — exactamente el mecanismo de deriva que el
README denuncia.

### Y dos que son el síntoma clásico

- **`Panel` existe y lo usan 2 de 14 pantallas.** Las otras nueve se escriben su
  propia `__tarjeta`, con tres radios distintos (18px, 20px, 24px). Es, palabra
  por palabra, el problema que `Panel` vino a resolver, repitiéndose encima de la
  solución.
- **Ninguna pantalla tiene un `<h1>`.** El primer encabezado del documento es el
  `<h2>` de `Panel`, que el lector de pantalla anuncia como "nivel 2" sin que
  haya un nivel 1.

---

## 2. Qué se formalizó

Ocho componentes nuevos, **4.385 líneas** en `docs2/src/componentes/`, todos con
su CSS al lado, exportados desde `index.ts`, con nombres en español y tipos
cerrados: **una variante inventada no compila.**

| Componente | Qué resuelve | Lo que ya no se puede escribir mal |
|---|---|---|
| `EncabezadoDePagina` | Título, subtítulo, migas, acción principal | 12 clases de título → una. Y pone el `<h1>` que faltaba |
| `TablaDeDatos` | La tabla entera | 22 tablas → una. **Se pliega a tarjetas en estrecho** |
| `BarraDeFiltros` | Búsqueda, chips, limpiar, conteo, cajón | 8 barras → una. La búsqueda ya trae su × |
| `Estado` | Los cinco huecos de una pantalla | 28 estados → cinco tipos cerrados |
| `MenuDeAcciones` | Los tres puntos, con desbordamiento | Reusa `flotante.ts`: no se recorta dentro de una tabla |
| `PastillaDeEstatus` | La pastilla **con el color adentro** | 26 clases de color → 6 tonos cerrados |
| `DialogoDeConfirmacion` | Preguntar antes de lo que no se deshace | **Los dos botones los pinta el componente** |
| `PuertaDePermiso` | Ocultar o apagar según el permiso | 4 formas sueltas → 4 modos de un tipo cerrado |

Más `medidas.ts` (`usarAnchoDelContenedor`), que es la pieza que hace posible lo
de las tarjetas.

### Las tres decisiones que vale la pena conocer

**La tabla se pliega por el ancho de su CONTENEDOR, no de la ventana.**
La misma `<TablaDeDatos>` puede vivir a pantalla completa, en media pantalla
(Ventas tiene su panel de saldos a la derecha) o dentro de un `<Modal>` de 560px.
En un monitor de 27", `window.innerWidth` dice 2560 en los tres casos. Se mide
con `ResizeObserver`. Y **no se pliega "a una tabla más chica": es otro árbol de
HTML** — una lista de fichas con título, estatus y datos rotulados, donde el
texto parte renglón en vez de recortarse.

**El umbral sale del número de columnas, no es un número fijo.** Era 720 para
todas, y mirarlo en el navegador mostró por qué está mal: una tabla de 7 columnas
a 780px *pasa* el umbral y es ilegible — los encabezados dicen "ALMAC…", las
fechas "06/03…" y la pastilla de estatus se parte por la mitad. Ahora es
`125px × columnas` (piso de 420), que es el ancho con el que una celda de 13px
enseña un dato normal sin recortarlo.

**Mientras los permisos cargan, la puerta se queda ABIERTA.** `puede(null, …)`
contesta `false`, o sea "no puedes". Si la puerta cerrara ahí, una pantalla en
modo `pantalla` le diría "no tienes permiso" a quien sí lo tiene durante el medio
segundo que tarda `/api/me`, y lo mandaría a pedir un permiso que ya tiene.
Cuatro pantallas de docs2 ya traían un comentario advirtiéndolo, cada una por su
cuenta; ahora se resuelve en un sitio.

---

## 3. Lo que encontró el navegador y el código no

Todo lo de abajo se descubrió mirando el molde en cuatro anchos, no leyendo. Vale
la pena anotarlo porque tres de los cinco eran bugs **ya presentes** en
componentes compartidos.

**1. `Checkbox` estiraba la página 900px.** Su `<input>` va `position: absolute`
para esconderse sin salirse del tabulador, y `.casilla` no era
`position: relative`. Un `absolute` sin ancestro posicionado se cuelga del
documento entero: en la lista de tarjetas, los inputs de las doce casillas
acababan con un `top` de hasta 1840px y el navegador estiraba la página hasta
ahí. El síntoma no se parecía en nada a la causa — una pantalla de alto fijo que
se recorría sola, con un hueco gris enorme debajo y ningún elemento visible ahí.
**En las pantallas viejas no se notaba por casualidad**: las casillas vivían
dentro de contenedores que ya tenían `position: relative` por otros motivos.
Arreglado en `Checkbox.css`, y beneficia a todas.

**2. La flecha de ordenar era un cuadrito vacío.** Se usó `⇅`, y la fuente de MAC
(Century Gothic) no trae ese glifo. Ahora se dibuja con `border`, que no depende
de ninguna fuente.

**3. La pastilla de estatus se cortaba en "Cancelad".** Heredaba el
`white-space: nowrap` de la celda de la tabla (que lo necesita, para recortar con
puntos) y el `overflow: hidden` la partía por la mitad. Ahora parte renglón.

**4. En un teléfono, la lista quedaba aplastada a cero de alto.** El acomodo de
"la pantalla no se recorre, se recorre la tabla por dentro" es el correcto en un
monitor y es lo contrario de lo correcto en un teléfono. Ver la regla 1 de abajo.

**5. El estado de pantalla completa no se centraba verticalmente**, porque el
`flex: 1` estaba en el elemento equivocado de los dos.

### Dos reglas de acomodo que ningún componente puede poner por ti

**1. En pantalla estrecha, la pantalla SÍ se recorre.**
El acomodo normal —`height: 100dvh`, `overflow: hidden`, y lo que se recorre es
la tabla por dentro— deja la barra de acciones siempre a la vista, que es lo
correcto en un monitor. En un teléfono la lista plegada a tarjetas crece hacia
abajo y con el alto fijo queda aplastada. Por debajo de 720px:

```css
@media (max-width: 720px) {
  .mimodulo         { height: auto; min-height: 100dvh; overflow: visible; }
  .mimodulo__cuerpo { min-height: 0; overflow: visible; }
  .mimodulo__tarjeta{ flex: 0 0 auto; min-height: 0;
                      border: 0; background: none; box-shadow: none; }
}
```

La barra de acciones se queda pegada abajo sola: eso lo hace
`BarraDeAcciones.css`, no tu pantalla.

**2. Dos zonas de scroll anidadas en un teléfono son una trampa** (el dedo
arrastra la de dentro cuando querías la de fuera). `TablaDeDatos` ya lo resuelve
sin preguntar: la lista de fichas se recorre **solo si su contenedor le pone un
techo**. Si sueltas el alto como dice la regla 1, no hay techo y se recorre la
página. Las dos salen de las mismas tres líneas de CSS.

---

## 4. Cómo se arma un módulo nuevo

**Copia `Web/QR_Web/docs2/src/paginas/molde/`** (tres archivos) y renómbrala.

1. `datosDelMolde.ts` → `nucleo/<modulo>.ts`: tipos, permisos **como constantes
   con nombre** (nunca cadenas sueltas en el JSX), formato (`pesos`, fechas) y
   las llamadas al API.
2. En la pantalla cambia **las columnas, los filtros y las acciones**. El armazón
   —encabezado, filtros, tabla, barra de abajo, diálogos— no se toca.
3. Da de alta la ruta en `nucleo/rutas.ts` (tipo `Pantalla`, `pantallaDelHash`,
   `rutaDePantalla`, `PANTALLAS` y, si abre desde el Inicio, `MODULOS_EN_DOCS2`)
   y en `App.tsx`. En producción además hace falta el renglón en
   `docs/staticwebapp.config.json`, o el servidor contesta 404.
4. Copia las dos reglas de acomodo de arriba a tu CSS.

### Las siete reglas que el molde demuestra

1. **No se escribe una tabla.** Se declaran `columnas` y se le pasan a
   `<TablaDeDatos>`. Si escribes un `<table>`, lo que falta es una prop del
   componente, no una tabla.
2. **No se escribe un estado vacío a mano.** `TablaDeDatos` ya pinta los cinco;
   solo hay que decirle `hayFiltro` para que distinga "no hay nada" de "nada casa
   con lo que pediste". Son dos mensajes con dos salidas distintas.
3. **Los permisos pasan por `<PuertaDePermiso>`**, con el modo que toque. Lo que
   el permiso no da no se pinta, y se enuncia abajo con `<SinPermisoPara>`. Lo
   que el permiso da pero el estado no permite se queda `tenue` y dice por qué.
   Un botón que desaparece sin explicación se lee como que el sistema se rompió.
4. **La acción principal va arriba** (`EncabezadoDePagina`) y **las que actúan
   sobre la selección van abajo** (`BarraDeAcciones`). Mezclarlas es como se
   acaba con catorce botones abajo y ninguno arriba.
5. **Lo que no se deshace pasa por `<DialogoDeConfirmacion>`, y el diálogo enseña
   qué se va a tocar** (`<ListaAConfirmar>`).
6. **Todo estado de carga se ve.** La tabla pinta su esqueleto con el encabezado
   de verdad (para que las columnas no se recoloquen); cada acción en camino
   lleva `cargando`, que pinta los puntitos **y** apaga el botón — las dos cosas
   juntas, o hay doble envío.
7. **Ningún color ni medida se escribe en el CSS de la pantalla.** Salen de
   `tokens.css` y de los componentes. El CSS de una pantalla es solo ACOMODO: el
   del molde son ~130 líneas útiles, contra las 565 de promedio de las trece que
   ya existían.

### La columna, que es lo único que de verdad se escribe

```tsx
{
  llave: "importe",
  etiqueta: "Importe",
  ancho: "14%",
  numerica: true,                       // letra tabular y a la derecha
  orden: (f) => f.importe,              // POR EL NÚMERO, no por el texto
  valor: (f) => pesos(f.importe),       // lo que se pinta
  papelEnTarjeta: "cuerpo"              // qué hace al plegarse
}
```

`orden` se pide aparte de `valor` a propósito: `valor` devuelve lo que se *pinta*
—muchas veces una pastilla o un importe ya formateado— y ordenar por eso da el
orden del texto, no del dato. `"$1,000.00"` va antes que `"$9.00"` si se comparan
como cadenas.

---

## 5. Qué pantallas se migraron

| Pantalla | CSS antes → después | Qué gana el usuario |
|---|---|---|
| **Módulos de la empresa** | 367 → 280 | Tarjetas en estrecho (tenía `min-width: 460px`), dos columnas ordenables, × en la búsqueda, chip del estatus, "Limpiar filtros", "Reintentar" |
| **Roles y permisos** | 353 → 246 | Tarjetas en estrecho (tenía `min-width: 560px`), tres columnas ordenables, × en la búsqueda, "Limpiar", "Reintentar", estado vacío con su acción |

**133 líneas de CSS borradas** entre las dos, y lo borrado no era CSS cualquiera:
eran los mismos 60 renglones de tabla escritos dos veces, palabra por palabra,
con el mismo comentario.

Lo más visible para quien las usa son dos cosas que no existían:

- **El chip del filtro puesto.** Módulos arranca filtrado en "Activos" y no había
  nada a la vista que lo dijera: se entraba, se veían nueve módulos, y los
  apagados simplemente no estaban. Ahora el chip lo dice y se quita de un clic.
- **"Reintentar" en el error.** Antes el error se pintaba en un `<td>` y la única
  salida era recargar la página entera.

Se comprobaron en el navegador antes y después, a 1600, 1400, 820 y 390–400px, en
claro y en oscuro.

### Lo que no se migró, y por qué

Las otras once pantallas se dejaron como están **a propósito**. Ventas (2.568
líneas), Cascos a cambio (2.407) y Nuevo pedido (1.736) no son "una tabla con
filtros": tienen semáforo de fila, panel lateral plegable, catorce botones con
desbordamiento y captura con lector de código. Migrarlas es un trabajo por
pantalla, con su propia verificación, y hacerlo a medias en esta tanda habría
dejado tres pantallas críticas a medio camino — que es peor que dejarlas
coherentes consigo mismas.

Módulos y Roles se eligieron porque son **exactamente** el caso que el sistema
modela (tabla + filtros + acciones + permisos) y porque son hermanas de Usuarios:
se abren una detrás de otra, y cualquier diferencia entre ellas se lee como "esta
es de otra época".

**El siguiente en migrar es Usuarios** (1.038 líneas, mismo patrón, ya comparte
las clases con estas dos). Después, las que tienen tabla simple: Préstamos,
Asistencias, Usados.

---

## 6. Dónde está cada cosa

| | |
|---|---|
| Los componentes | `docs2/src/componentes/` (uno por archivo, con su `.css` al lado) |
| La puerta de entrada | `docs2/src/componentes/index.ts` — las pantallas importan **siempre** de aquí |
| El porqué de cada uno | el comentario de arriba de su `.tsx`, y `componentes/README.md` |
| El molde | `docs2/src/paginas/molde/` — se abre en `#/molde` |
| Los tokens | `docs2/src/estilos/tokens.css` — los únicos colores, radios y sombras |

**La regla de siempre, un nivel más arriba:** si el CSS de una pantalla empieza a
definir colores, a estilizar botones **o a escribir una tabla**, ya se salió del
carril. Eso es un componente o un token que falta.
