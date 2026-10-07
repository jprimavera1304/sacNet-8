# Sistema de componentes de la app móvil

> **Para qué es este documento.** La app móvil tiene hoy cinco módulos y van a
> venir muchos más, construidos por varios agentes a la vez. Esto fija cómo se
> arma un módulo nuevo para que salga igual que los demás: qué componente usar
> para cada cosa, qué patrón hereda para tablet y qué se comprueba con texto
> grande.
>
> **Si vas a hacer un módulo nuevo, lee solo la sección [Cómo se arma un módulo
> nuevo](#cómo-se-arma-un-módulo-nuevo) y copia
> `shared_mobile/lib/ui/patrones/ejemplo/modulo_de_ejemplo.dart`.** Lo demás es
> el porqué.

---

## 1. Lo que ya se repetía (con números)

Se recorrieron los cinco módulos (`lib/modules/verticals/*`,
`shared_mobile/lib/features/{reportes,core_admin,home}`) contando cada patrón y
sus variantes.

El hallazgo de fondo: **ya existía un sistema de componentes** en
`shared_mobile/lib/ui/components/app_*.dart` (`AppRecordCard`, `AppEmptyState`,
`showAppBottomSheet`, `showAppConfirmDialog`, `AppSearchBar`, `elegirFecha`…),
**y había dos islas que no lo usaban**:

| Isla | Archivos | Usos del sistema |
|---|---|---|
| Migrada: `reportes` + `core_admin` | 13 | 227 |
| **Sin migrar: `lib/modules/verticals/*`** | 4 (5.440 líneas) | **4** |
| **Sin migrar: `features/home/src/**`** | 20 | **0** |

`repartidor_home.dart` —2.690 líneas, la pantalla más completa— usa **cero**
componentes compartidos. Y `features/home/src/` llegó a construir **su propio
mini-sistema paralelo**: `visual_tokens.dart`, `layout_widgets.dart`
(`_SurfaceCard`), `empty_widgets.dart` (`_EmptyCard`).

### Censo por patrón

| # | Patrón | Ocurrencias | Variantes | ¿Había componente? | ¿Lo usaban los verticals? |
|---|---|---|---|---|---|
| 1 | Listas con tarjeta | 24 listas | **5** | `AppRecordCard` | No (0 de 4) |
| 2 | Hojas inferiores | 8 llamadas | 3 | `showAppBottomSheet` | No (3 crudas) |
| 3 | Buscadores | 10 campos | 3 | `AppSearchBar` | No (2 a mano) |
| 4 | Filtros y chips | 4 hojas + 10 chips | **4** | `AppFilterChips` | No (2 clones + `_pill`) |
| 5 | Estados | ~22 bloques | **6** | `AppEmptyState`, `AppSkeletonList` | No |
| 6 | Confirmaciones | 6 `showDialog` / 5 `AlertDialog` | 3 | `showAppConfirmDialog` | **0 llamadas en todo el repo** |
| 7 | Chips de estatus | 3 renders | 3 | `AppStatusBadge` | No (2 paletas propias) |
| 8 | Menús de acciones | 1 popup + 1 hoja + botones en tarjeta | 3 | — | n/a |
| 9 | Campos de formulario | 7 campos / 5 `InputDecoration` | 4 | `AppFormInput` | No (2 crudos) |
| 10 | Fecha / hora | 3 selectores / 6 formateos | 1 / **3 formatos** | `elegirFecha` | Sí (el único caso bueno) |
| 11 | Compartir / archivos | 5 APIs nativas | 1 | `archivos_del_sistema.dart` | Sí (bien) |
| 12 | Plataforma (iOS vs Android) | 1 decisión | 1 | `app_dates.dart:22` | Sí, pero solo para fechas |
| 13 | Constantes de estilo | 223 hex + 133 radios + 290 `height:` | **4 paletas clonadas** | `AppUiColors` | No |
| 14 | Riesgo de texto grande | 28 `maxLines:1` / 35 `ellipsis` / 98 flex | 11 casos concretos | clamp global 2.0 | Global sí, layouts no |

**14 patrones duplicados.** Los que costaron algo de verdad:

1. **`_money` tres veces en DOS versiones distintas.** `mis_pedidos.dart:55` y
   `pedidos_autorizar.dart:58` ponen separador de miles;
   `repartidor_home.dart:50` no. El mismo pedido se veía `$1,234.00` en una
   pantalla y `$1234.00` en la otra.
2. **La paleta copiada cuatro veces.** `const _primary = Color(0xFF111E9B)` y
   `const _bg = Color(0xFFF4F6FB)` en los cuatro verticals — y ese `_bg` no es
   el `AppUiColors.background` del sistema (`0xFFF8FAFF`): dos fondos distintos
   según la pantalla.
3. **Cinco pantallas de error casi idénticas**: `_ErrorView`, `_ErrorBox`,
   `_ErrorBox` (otra vez), `_ErrorScreen`, `_StartupErrorView`. Y **tres copias
   de `_mensajeError`** más tres funciones equivalentes con otro nombre; la que
   faltaba volcaba el `SocketException` crudo, con URL y errno, en la pantalla
   del repartidor.
4. **`_atajoChip` y `_chipRango` son el mismo widget** con otro enum, en dos
   archivos (`mis_pedidos.dart:474`, `pedidos_autorizar.dart:456`).
5. **El chip de estatus con dos alphas distintos** (0.12 en "Mis pedidos", 0.25
   en "Pendientes"): el amarillo de una pantalla no era el de la otra, y el
   color es justo lo que la gente usa para decidir sin leer.
6. **`_toast` llamado 19 veces** en `repartidor_home.dart`, con `showAppSnack`
   ya escrito y sin usar.
7. **No existía "sin permiso".** En inicio se resolvía con un
   `StateError('Tu sesión no tiene permiso para Inicio.')` que acababa en la
   pantalla de error de red, **ofreciendo "Reintentar" para algo que reintentar
   no arregla**.

---

## 2. Lo que se formalizó

Todo en **`shared_mobile/lib/ui/patrones/`**, exportado por
`package:shared_mobile/shared_mobile.dart`. Nombres y comentarios en español,
cada archivo explica *por qué* es así.

### Cómo se reparte con `ui/components/app_*.dart`

**No hay dos sistemas.** Se dividen por altura:

- **`ui/components/app_*.dart` = las piezas.** Colores (`AppUiColors`), el botón
  (`AppButton`), la superficie (`AppSurface`), el campo de texto
  (`AppFormInput`), el selector (`AppSelect`), la rueda de fechas
  (`elegirFecha`). Ya las usan reportes y la administración de permisos. **No se
  tocaron**, salvo para añadir `elegirHora`.
- **`ui/patrones/` = las pantallas armadas con esas piezas.** La lista, el
  detalle, la hoja de filtros, los estados, el acomodo de tablet. Es la capa que
  faltaba. **No redefine ni un color.**

### Los componentes nuevos

| Archivo | Qué trae | Qué duplicación cierra |
|---|---|---|
| `medidas.dart` | `Espacio`, `Redondeo`, `kAnchoDeDosPaneles`, `kAnchoLegible`, `esAcomodoAmplio()`, `AnchoLegible` | 133 radios + 290 `height:` a mano; el `840` escondido en reportes |
| `formatos.dart` | `dinero`, `cantidad`, `fechaCorta`, `fechaLarga`, `horaCorta`, `fechaYHora`, `etiquetaDeRango`, `detalleDeRango`, `soloDia`, `mismoDia` | `_money` ×3 en 2 versiones; 6 formateos de fecha en 3 formatos |
| `estados.dart` | `EstadoDeVista` (6 estados), `mensajeDeError()`, `VistaDeEstado` + `.error` `.sinPermiso` `.vacio` `.sinResultados`, `AreaConEstado`, `CargandoLista` | 5 pantallas de error, 3 `_mensajeError`, 2 `_CenterFill`, ~22 bloques de estado |
| `chip_de_estatus.dart` | `TonoDeEstatus`, `colorDeTono()`, `tonoPorEstatus()`, `ChipDeEstatus` (+ `.deTexto`) | 3 renders del chip, 2 paletas de estatus, los dos alphas |
| `lista_de_tarjetas.dart` | `TarjetaDeRegistro`, `DatoDeTarjeta`, `ListaDeTarjetas<T>` | 24 listas en 5 variantes; `_pedidoCard` ×2; 3 `RefreshIndicator`; la paginación a mano |
| `tarjeta_de_detalle.dart` | `TarjetaDeDetalle`, `SeccionDeDetalle`, `RenglonDeDato` | Las tarjetas de detalle apiladas y siempre abiertas |
| `buscador.dart` | `Buscador` (con espera, limpiar, teclado de buscar) | 10 campos en 3 variantes; 2 implementaciones de debounce a mano |
| `hoja_de_filtros.dart` | `abrirHojaDeFiltros()`, `GrupoDeFiltro`, `OpcionDeFiltro`, `ChipsDeFiltroActivo`, `FiltroActivo` | 4 hojas, 10 chips en 4 formas; y el aviso de filtro activo, que no existía |
| `menu_de_acciones.dart` | `AccionDeRegistro`, `abrirMenuDeAcciones()`, `BotonDeAcciones` | `PopupMenuButton` + hoja que devuelve `String` + botones dentro de la tarjeta |
| `confirmacion.dart` | `confirmar()` (con variante destructiva), `avisar()` | 6 `showDialog` / 5 `AlertDialog` en 3 formas |
| `puerta_de_permisos.dart` | `PuertaDePermiso` (+ `.porClave`), `accionConPermiso()`, `SiTienePermiso` | El banner de texto, el bool `_puedeEditar`, el `StateError` |
| `campos.dart` | `CampoDeImporte`, `CampoDeFecha`, `CampoDeHora`, `CampoDeInterruptor`, `GrupoDeCampos` (+ reexporta `AppFormInput`/`AppSelect`) | 5 `InputDecoration` crudos; y la hora, que no existía |
| `acomodo_maestro_detalle.dart` | `AcomodoMaestroDetalle<T>`, `CuerpoDeModulo` | Las ~100 líneas de dos paneles + giro que solo tenía reportes |
| `pantalla_de_modulo.dart` | `PantallaDeModulo` | Cada pantalla con su Scaffold, su fondo y su barra |
| `plataforma.dart` | `esApple(context)`, `esAppleSinContexto` | La decisión de plataforma acoplada solo a fechas |
| `nativo.dart` | `origenDeCompartir()` + la regla escrita | El rectángulo del iPad, que había que recordar a mano |
| `ejemplo/modulo_de_ejemplo.dart` | El molde completo: modelo, repositorio, pantalla | — |

**32 clases y enums públicos + 23 funciones de nivel superior**, sin contar el
módulo de ejemplo. La única pieza añadida fuera de `patrones/` es `elegirHora()`
en `ui/components/app_dates.dart`, que va junto a `elegirFecha` para que la
decisión de plataforma siga viviendo en un solo archivo.

### Lo nativo, donde toca

La identidad visual es **una sola**: los mismos colores, las mismas palabras,
las mismas tarjetas en los dos sistemas. Lo que cambia es el **mecanismo que la
gente ya conoce de su teléfono**:

| Intención | iPhone / iPad | Android |
|---|---|---|
| Elegir fecha | rueda `CupertinoDatePicker` | calendario `showDatePicker` |
| Elegir hora | rueda, 24 h | `showTimePicker`, entrada por teclado |
| Confirmar | `CupertinoAlertDialog`, destructivo en rojo al final | `AlertDialog`, destructivo a la derecha |
| Acciones de un registro | `CupertinoActionSheet` con su "Cancelar" aparte | hoja inferior con renglones de icono y texto |
| Sí / no | interruptor de Apple | `Switch` de Material |
| Previsualizar un archivo | Vista rápida del sistema | visor propio (`pdfx`) |
| Compartir | `UIActivityViewController` **con el rectángulo de origen** | selector del sistema |

Dónde se decide: **`plataforma.dart`**, y se pregunta al tema
(`Theme.of(context).platform`) y no a `Platform.isIOS`. Dos razones: `Platform`
no existe en la web, y `Platform.isIOS` es una constante del proceso que una
prueba no puede fingir sin tocar `debugDefaultTargetPlatformOverride`, que es
estado global y se filtra a la prueba siguiente. Con el tema, una prueba monta
`MaterialApp(theme: ThemeData(platform: TargetPlatform.iOS))` y ya.

**La regla de los plugins nativos no se tocó, porque ya estaba bien:**
`shared_mobile` **no depende de plugins nativos**. Declara lo que necesita como
interfaz (`ServiciosDeArchivo`, `VisorDePdf`, el callback `abrirEnNavegador`) y
la app lo cumple en `lib/app/archivos_del_sistema.dart` con `path_provider`,
`share_plus`, `open_filex`, `flutter_file_dialog` y `pdfx`. Por eso las
pruebas de descargar, guardar, compartir y abrir un reporte corren **sin
emulador** y sin tocar un solo plugin. Un módulo
nuevo que importe `share_plus` dentro de `shared_mobile` rompe eso para todos:
ver `nativo.dart`, que lo deja escrito.

---

## 3. Cómo se arma un módulo nuevo

Copia `shared_mobile/lib/ui/patrones/ejemplo/modulo_de_ejemplo.dart`, cambia el
modelo y el repositorio, y borra `RepositorioDeEjemploEnMemoria`. Es una
pantalla que **de verdad corre**: se puede buscar, filtrar, abrir el detalle y
ejecutar una acción con permiso. Ningún botón sin handler.

El orden conviene ser este:

### 1. El modelo y el repositorio

```dart
class Pedido { /* … */ }

// Interfaz, no una clase con `http` adentro: así la prueba de la pantalla
// corre sin red, igual que `ServiciosDeArchivo`.
abstract class RepositorioDePedidos {
  Future<List<Pedido>> buscar({required String texto, required Estatus e});
}
```

### 2. Un solo estado, no tres bools

```dart
EstadoDeVista _estado = EstadoDeVista.cargando;
Object? _error;
```

Y en `initState`, **sin permiso no se pide nada**:

```dart
if (widget.puedeVer) {
  _cargar();
} else {
  _estado = EstadoDeVista.sinPermiso;
}
```

Esto es el arreglo de fondo: antes la pantalla se construía, pedía datos, el
servidor contestaba 403 y la persona veía un fallo de conexión por algo que no
era la red.

### 3. La carga decide el estado, y distingue vacío de sin resultados

```dart
_estado = filas.isNotEmpty
    ? EstadoDeVista.listo
    : (_hayFiltros ? EstadoDeVista.sinResultados : EstadoDeVista.vacio);
```

No son lo mismo y la salida es otra: con filtro, se quitan los filtros; sin
filtro, no hay nada que quitar.

### 4. Los filtros se eligen en una hoja y se muestran en chips

```dart
final elegido = await abrirHojaDeFiltros<Estatus>(
  context: context, titulo: 'Filtrar pedidos',
  onLimpiar: _hayFiltros ? _limpiarFiltros : null,
  contenido: (hoja, aplicar) => GrupoDeFiltro<Estatus>(/* … */),
);
```

Y arriba de la lista, `ChipsDeFiltroActivo`. **Esto es lo que faltaba:** antes,
al volver a la lista los filtros seguían puestos y la única forma de saberlo era
abrir la hoja otra vez. Más de un "no aparece mi pedido" era un filtro de fecha
olvidado.

### 5. Las acciones llevan su permiso

```dart
accionConPermiso(
  etiqueta: 'Autorizar',
  tienePermiso: puedeAutorizar && pedido.estatus == 'Pendiente',
  motivo: 'No tienes el permiso de autorizar',
  onEjecutar: () => _autorizar(pedido),
)
```

La acción **se queda a la vista, apagada y con su motivo**. Esconderla hace que
la persona crea que la app está rota ("a mi compañero sí le sale Autorizar").
Para una **pantalla** entera, `PuertaDePermiso`, que no construye lo de adentro.

### 6. Lo destructivo se confirma

```dart
final seguro = await confirmar(
  context,
  titulo: 'Cancelar PV-10482?',
  mensaje: 'No se puede deshacer.',
  etiquetaConfirmar: 'Cancelar pedido',
  etiquetaCancelar: 'Dejarlo asi',
  destructiva: true,
);
```

Cerrar por fuera cuenta como **cancelar**, nunca como aceptar.

### 7. La pantalla

```dart
PuertaDePermiso(
  tienePermiso: puedeVer, queSeNecesita: 'ver los pedidos',
  hijo: (context) => PantallaDeModulo(
    titulo: 'Pedidos', subtitulo: '…',
    buscador: Buscador(pista: 'Folio o cliente', onBuscar: …),
    filtros: ChipsDeFiltroActivo(…),
    cuerpo: AcomodoMaestroDetalle<Pedido>(
      elegido: _elegido, onElegir: …,
      lista: (context, enDosPaneles, elegir) => ListaDeTarjetas<Pedido>(
        elementos: _pedidos, estado: _estado, error: _error,
        onRefrescar: _cargar, onReintentar: _cargar,
        constructor: (context, pedido, _) => TarjetaDeRegistro(…),
      ),
      detalle: (context, pedido, enDosPaneles) => TarjetaDeDetalle(…),
    ),
  ),
)
```

### Lo que NO se hace

- **Nada de tablas de escritorio encogidas.** Un pedido trae cliente, fecha,
  estatus, piezas e importe; en un teléfono de 360 puntos eso son cinco columnas
  de 70 puntos y todo cortado. La tarjeta apila y crece a lo alto, que es la
  dimensión que en un teléfono sobra.
- **Nada de `const _primary = Color(...)`.** Está en `AppUiColors`.
- **Nada de `_money`, `_fechaCorta`, `_mensajeError` propios.** Están en
  `formatos.dart` y `estados.dart`.
- **Nada de `height:` fijo alrededor de texto.** Ver abajo.
- **Nada de `import 'package:share_plus/...'` dentro de `shared_mobile`.**

---

## 4. El patrón de tablet e iPad

Hay **dos preguntas distintas** y confundirlas ya costó una tablet entera:

| Pregunta | Qué se mide | Dónde | Para qué |
|---|---|---|---|
| ¿Qué aparato es? | **lado corto ≥ 480** | `lib/app/giro.dart` (`kLadoCortoDeTablet`) | Decidir si puede girar — no cambia al girarlo |
| ¿Cuánto ancho tengo *ahora*? | **ancho ≥ 840** | `medidas.dart` (`kAnchoDeDosPaneles`) | Decidir el acomodo — una tablet parada tiene el ancho de un teléfono grande |

El 480 ya estaba bien razonado y medido con `am get-config` (480 dp es donde
Android deja de decir `normal` y empieza a decir `large`; una Lenovo Tab M8 da
533 y con el corte de 600 se quedaba clavada de pie). **No se tocó.** Lo que se
hizo fue sacar el 840 de dentro del `build` de reportes y ponerlo donde el
siguiente módulo lo herede.

### Lo que hereda un módulo

- **`AcomodoMaestroDetalle<T>`** — angosto: lista, y tocar empuja el detalle
  como pantalla. Ancho: lista a la izquierda, detalle a la derecha.
- **`esAcomodoAmplio(context)`** — para decidir cualquier otra cosa.
- **`CuerpoDeModulo`** — tope de 640 puntos para un detalle o formulario suelto,
  centrado. Un formulario estirado 1.133 puntos obliga a barrer la cabeza de un
  lado a otro para leer un renglón.
- **`AnchoLegible`** — tope de 420 para una columna sola. Es el mismo que
  `test/tablet_layout_test.dart` ya comprueba en la clave de empresa y el login.

### Lo difícil es el giro, y queda resuelto

Una tablet pasa de 840 de ancho a 744 con solo pararla:

- **De acostada a parada**: el panel derecho ya no cabe → lo que se estaba
  viendo **se empuja** como pantalla. Si no, se pierde.
- **De parada a acostada**: la pantalla empujada **se cierra** y lo que traía
  pasa al panel derecho. Si no, queda tapando el acomodo que sí cabía.
- **La trampa**: al cerrar esa pantalla por giro, el `pop` dispara lo mismo que
  si la persona hubiera dado atrás. Sin marcar que el cierre fue por giro,
  **girar la tablet borraba la selección**. Es el arreglo que reportes ya tenía
  y que el acomodo hereda; hay dos pruebas que lo cuidan ("girar de parada a
  acostada NO pierde lo elegido" y "dar atrás de verdad sí suelta lo elegido").

La columna de la lista es de **320** y no de 300: con 300 y la letra al doble,
los títulos partían en cuatro líneas y el renglón se volvía un párrafo.

---

## 5. El patrón de texto grande

La app ya acota la escala de letra a **2.0** en `lib/main.dart:219`
(`textScaler.clamp(maxScaleFactor: 2.0)`). Eso es una red de seguridad, **no un
sustituto de acomodar bien los widgets** — lo dice el propio comentario de ahí.
Así que 2.0 es el peor caso **real** y es con el que se mide.

### Las reglas

1. **Ningún alto fijo alrededor de texto.** Donde hacía falta un cuadrado (la
   inicial del cliente, la burbuja del icono de un estado), **el lado sale de la
   escala de letra**: `MediaQuery.textScalerOf(context).scale(16) + 26`. El
   censo encontró nueve cuadros fijos de 30 a 58 puntos con un icono o una letra
   dentro: al doble, se salían de la caja.
2. **El título de una tarjeta tiene DOS líneas, no una.** Al doble, un nombre de
   cliente completo no cabe en una, y cortarlo deja `ABARROTES Y CRE…`, que no
   identifica a nadie.
3. **`ellipsis` siempre con `maxLines`.** Había 35 `ellipsis` contra 28
   `maxLines: 1`: siete recortes sin límite de líneas, que no recortan nada.
4. **Lo que se acumula en una fila va en `Wrap`.** El chip de estatus y las
   pastillas de datos bajan de renglón en vez de empujar la flecha fuera de la
   pantalla — que es exactamente lo que hacía en "Mis pedidos".
5. **El chip de estatus va junto a los datos, no junto al título.** Arriba era
   donde empujaba.
6. **Etiqueta y valor se parten en dos líneas cuando ya no caben de lado.**
   `RenglonDeDato` mide el ancho de verdad con un `TextPainter` y decide. Lo que
   había era un `maxLines: 1` que cortaba justo el dato que importa.
7. **El título de la barra puede partir en dos líneas.** "Pendientes de
   autorizar" al doble no cabe en una, y Material lo cortaba con puntos.
8. **Los errores de un campo tienen cuatro líneas** (`errorMaxLines: 4`): al
   doble, "La fecha no puede ser posterior a hoy" necesita tres.
9. **La silueta de carga crece con la letra.** Si midiera 90 puntos siempre, al
   doble la lista daría un brinco al llegar.

### Cómo se comprueba

En una prueba, **un desbordamiento es una excepción**, así que es medible y no
cuestión de ojo:

```dart
await tester.pumpWidget(aDoble(/* la pantalla */));
await tester.pumpAndSettle();
expect(tester.takeException(), isNull);
```

`test/patrones_texto_grande_test.dart` lo hace con la tarjeta, el chip, el
renglón de dato, el detalle y **el módulo de ejemplo entero** en un teléfono de
320 puntos de ancho al doble de letra. Y además comprueba que la tarjeta
**crece** (no que simplemente no reviente): al doble mide más de 1,4 veces lo
que mide al normal.

---

## 6. Qué se comprobó

- **`flutter analyze`**: limpio (0 avisos), igual que antes.
- **`flutter test`**: **308 pruebas pasan**. Eran 235 y ninguna se rompió; las
  **73 nuevas** están en:
  - `test/patrones_formatos_test.dart` — dinero con miles, el negativo, fechas,
    rangos "Hoy"/"Ayer", y los tonos de estatus.
  - `test/patrones_estados_test.dart` — los cinco estados, que "sin permiso" no
    ofrezca reintentar, que el contenido no se construya si no hay datos, que la
    puerta de permisos no construya lo de adentro, y que el módulo **no le pida
    nada al servidor sin permiso**.
  - `test/patrones_texto_grande_test.dart` — todo lo de la sección 5, al doble.
  - `test/patrones_tablet_y_nativo_test.dart` — el corte de 840 contra el de
    480, los dos paneles, **el giro sin perder la selección**, atrás sí
    soltándola, el tope de ancho, y la cara nativa (Cupertino en iOS, Material
    en Android, lo destructivo al final, la acción después de cerrar la hoja, y
    el rectángulo que el iPad exige).
  - `test/patrones_buscador_y_filtros_test.dart` — que ocho letras manden **una**
    búsqueda, los chips de filtro activo, y que lo plegado del detalle **ni se
    construya**.
- **Corriendo en el emulador Android** (`mac_gaslp_api36`, api 36). Comprobado
  a mano, con captura de cada paso:
  - La lista, con el chip de estatus en su color (ámbar pendiente, verde
    autorizado, rojo cancelado) y los importes con separador de miles.
  - **La letra al doble**: todo crece, el título parte en dos líneas, las
    pastillas bajan de renglón, nada se corta.
  - El detalle empujado como pantalla, con "Notas" plegada.
  - El menú de acciones (hoja de Material, lo destructivo en rojo al final) y
    la confirmación destructiva.
  - Los estados **sin permiso** y **error** (con "Reintentar"; el `StateError`
    crudo nunca llega a la pantalla).
  - **Tablet**: forzando la pantalla a 1000 dp de ancho (`adb shell wm size
    2400x1600 && wm density 240`), la lista queda a la izquierda con el
    renglón elegido marcado y el detalle a la derecha.
  - **El giro**: volviendo la pantalla a tamaño de teléfono con un pedido
    elegido, el detalle **se empuja solo** como pantalla propia con su flecha,
    sin perder la selección. El banco de pruebas
  (`lib/main_patrones.dart`,
  `flutter run -t lib/main_patrones.dart --flavor dev` — el sabor no es
  opcional, el modulo de Android declara `dev` y `prod`) trae
  interruptores para la letra al doble, los dos permisos y el fallo del
  servidor, para ver los cinco estados sin desconectar nada.

### Tres errores que se encontraron comprobando

1. `tonoPorEstatus('En revisión')` salía **verde**: el "SI" de la columna
   `Autorizado` del back se buscaba por contenido, y "revi**si**ón" lo lleva
   dentro. Ahora los de dos letras se comparan completos, y lo malo se evalúa
   antes que lo bueno porque "inactivo" lleva "activo" dentro.
2. `TarjetaDeRegistro` metía el **nombre entero** del cliente dentro del
   cuadrito de la inicial cuando se le pasaba `inicial` explícitamente.
3. **Solo corriéndolo en el emulador**: `VistaDeEstado` no traía fondo propio,
   y como `PuertaDePermiso` la devuelve **en lugar** de la pantalla entera —sin
   `Scaffold` debajo—, "Sin permiso" salía **sobre negro**. Ninguna prueba lo
   habría visto: el árbol era correcto, lo que faltaba era un color. Arreglado
   y con prueba que lo clava.

---

## 7. Lo que queda pendiente

Esto **formaliza** los patrones; **no migró** las pantallas que ya funcionan,
porque la consigna era no romper lo que funciona. Lo que falta, por orden de
retorno:

1. **`repartidor_home.dart` (2.690 líneas, 0 usos del sistema)** — el `_toast`
   ×19 a `showAppSnack` y las dos hojas de búsqueda a `Buscador` +
   `ListaDeTarjetas` es el cambio más grande con el menor riesgo.
2. **`mis_pedidos.dart` y `pedidos_autorizar.dart`** — `_pedidoCard` ×2 a
   `TarjetaDeRegistro`, `_atajoChip`/`_chipRango` a `GrupoDeFiltro`, los dos
   `_ErrorBox` a `VistaDeEstado.error`, `_money` a `dinero()`. Ojo: al unificar
   `_money` **cambia** lo que ve el usuario en captura de pedido (gana la
   versión con separador de miles).
3. **`features/home/src/**` (20 archivos, mini-sistema propio)** — `_SurfaceCard`
   → `AppSurface`, `_EmptyCard`/`_EmptyState` → `VistaDeEstado`, `_HomeColors` →
   `AppUiColors`.
4. **El `_bg` divergente**: los verticals usan `0xFFF4F6FB` y el sistema
   `0xFFF8FAFF`. Hay que decidir cuál es el fondo de la app; hoy depende de la
   pantalla.

---

## Referencia rápida

| Si necesitas… | Usa |
|---|---|
| Listar registros | `ListaDeTarjetas<T>` + `TarjetaDeRegistro` |
| Mostrar el detalle | `TarjetaDeDetalle` + `SeccionDeDetalle` + `RenglonDeDato` |
| Buscar | `Buscador` |
| Filtrar | `abrirHojaDeFiltros()` + `GrupoDeFiltro` + `ChipsDeFiltroActivo` |
| Cargando / vacío / sin resultados / error / sin permiso | `AreaConEstado`, `VistaDeEstado`, `CargandoLista` |
| Traducir una excepción | `mensajeDeError()` |
| Acciones de un registro | `BotonDeAcciones` + `AccionDeRegistro` / `accionConPermiso()` |
| Estatus con color | `ChipDeEstatus.deTexto('Pendiente')` |
| Confirmar / avisar | `confirmar(destructiva: …)` / `avisar()` |
| Proteger una pantalla | `PuertaDePermiso` |
| Un aviso corto | `showAppSnack()` |
| Campos | `AppFormInput`, `AppSelect`, `CampoDeImporte`, `CampoDeFecha`, `CampoDeHora`, `CampoDeInterruptor`, `GrupoDeCampos` |
| Fecha / hora / rango | `elegirFecha()`, `elegirHora()`, `elegirRangoDeFechas()` |
| Dinero y fechas en texto | `dinero()`, `cantidad()`, `fechaCorta()`, `fechaLarga()`, `horaCorta()`, `etiquetaDeRango()` |
| El armazón de la pantalla | `PantallaDeModulo` |
| Tablet | `AcomodoMaestroDetalle<T>`, `esAcomodoAmplio()`, `CuerpoDeModulo`, `AnchoLegible` |
| Saber si es Apple | `esApple(context)` |
| Compartir desde un botón (iPad) | `origenDeCompartir(llave)` |
| Espacios y radios | `Espacio.*`, `Redondeo.*` |
