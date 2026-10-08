# Reglas de la mision del frontend nuevo

Las lee todo agente antes de tocar un archivo. No se negocian.

## Que se construye

El frontend nuevo completo de MAC: Web, Android, iOS, tablets Android, iPad.
`Legacy/Mac31` es la REFERENCIA FUNCIONAL: lo que hace, no como se ve.
El backend nuevo (`sacNet-8/ISL_Service`) es el API.

**Es UN solo sistema configurable, no dos apps.** Lo que cambia entre empresas
sale de modulos, permisos y configuracion. Nunca de `if (empresa == "Zaragoza")`.
Si hace falta una diferencia de empresa, se expresa como capacidad con nombre
(`permiteCascosACambio`), no como el nombre de la empresa.

## Prioridad

1. **Zaragoza completo.** 2. Despues Tauro. 3. Aviacion queda para despues.
Tu hoja de alcance (`docs-frontend/alcance/<tu-grupo>.tsv`) ya viene filtrada a
Zaragoza. La columna `aplica_tauro` te dice si ademas aplica alla: si aplica,
no lo rompas, pero no te detengas a verificarlo en Tauro.

## Lo que NO se entrega

- Sin placeholders. Sin "proximamente". Sin botones muertos.
- Sin pantallas a medias: una pantalla se entrega entera o no se empieza.
- Sin datos inventados en la UI. Si el endpoint no da un campo, no se pinta.
- Los mocks, si hacen falta, viven **solo en la capa de servicio**, nunca en la
  pantalla, y quedan anotados.

## Componentes: no se escribe un boton a mano

Ya existen DOS sistemas de componentes y son obligatorios:

- Web: `Web/QR_Web/docs2/src/componentes/` — lee su `README.md` y abre el molde
  en `#/molde`. Copia `docs2/src/paginas/molde/` para empezar un modulo.
- Movil: `Movil/mac-mac-mobile/shared_mobile/lib/ui/patrones/` — el molde es
  `ui/patrones/ejemplo/modulo_de_ejemplo.dart`.

Si te falta una pieza, **se agrega al sistema** y la usan todos. No se hace una
version local. Un color, un margen o un tamano de letra nuevo dentro de una
pantalla es un error: salen de los tokens.

## Permisos

Al grano fino: cada accion detras de su clave de permiso, con la puerta del
sistema (`PuertaDePermiso`). La columna `permiso_legacy` de tu hoja dice cual.
Mientras los permisos cargan la puerta se queda ABIERTA (no se le dice "no
tienes permiso" a quien si lo tiene durante el medio segundo de `/api/me`).

## Documentos y reportes

Todo modulo que genere un documento (PDF, remision, ticket, corte, estado de
cuenta) **tiene que generarse de verdad y compararse contra Legacy**: datos,
totales, columnas, encabezados, logos, saltos de pagina. No basta con que la
peticion devuelva 200. Se abre el PDF y se mira.

## Verificar: en navegador y emulador de verdad

No se entrega nada "verificado leyendo el codigo".

- Web: levanta tu propio `vite` en el puerto que te toco y abrelo en un
  navegador real. Comprueba a **1600, 1400, 820 y 390 px**, en claro y oscuro,
  con el texto forzado a 20px, y los cinco estados (cargando, vacio, error, sin
  permiso, con datos).
- Movil: emulador real. Telefono y tablet. Patrones nativos de cada plataforma.
- El navegador y el emulador son **compartidos** con otros agentes. Abre tu
  pestana, mira, cierrala. Si algo te la quito, reintenta sin pelear.

## Repos: donde se puede escribir y donde no

- **`Legacy/` NUNCA se modifica y NUNCA se commitea.** Es lectura y ejecucion.
  Esta regla es absoluta: ni un archivo, ni un commit, ni siquiera un formateo.
- **No se modifica la base para que algo pase.** Ni procedimientos, ni datos.
- **Nada contra produccion.** Solo local.
- Los procedimientos `sp_n_` de legacy no se editan: se crean `sp_w_`/`sp_mo_`.

## Commits

Pequenos y seguidos, en espanol, explicando **por que** y que se vio al
mirarlo, no solo que se toco. **Sin coautoria y sin linea de sesion.**
Pon en escena solo tus rutas (`git add <tus-archivos>`), nunca `git add -A`:
hay otros agentes en el mismo arbol.

**Y comprueba que no te dejaste un archivo NUEVO fuera.** Poner en escena solo
algunas rutas tiene una trampa, y ya costo dos veces: los archivos que CREAS no
los conoce nadie, asi que si no los nombras se quedan sueltos. El consumidor si
se commitea y lo consumido no, y entonces **la rama no compila desde un clon
limpio aunque en tu arbol compile**. Peor: el error apunta al consumidor, no al
archivo que falta (`CS0246: no se encontro 'IClientesConsultaService'`), asi que
no se parece a su causa.

Antes de cada commit:

```bash
git status --porcelain | grep '^??'     # ninguno tuyo deberia salir aqui
```

Y antes de entregar, comprueba que compila de verdad lo que subiste, no lo que
tienes:

```bash
git worktree add --detach /tmp/limpio HEAD && cd /tmp/limpio && <compila>
```

## Si encuentras un error

**Encontrar un error es trabajo, no un hallazgo que reportar.** Arreglalo. Si
esta fuera de tu alcance, arreglalo igual si es pequeno, y si es grande dejalo
escrito en tu reporte con lo que medidiste.

Ante la duda: **se migra**. No reduzcas el alcance y no te detengas a preguntar.

## Tu reporte final

Que entregaste, que pantallas quedan completas, que viste en el navegador o el
emulador que el codigo no decia, que endpoints faltaban en el backend, y que
quedo sin hacer con el porque medido.
