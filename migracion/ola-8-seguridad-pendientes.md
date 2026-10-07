# Ola 8 — Agujeros de seguridad y pendientes reales del backend nuevo

Lo que la auditoría (`auditoria-cobertura-legacy.md`, §4) dejó señalado con
archivo y línea, cerrado. Nada de `Legacy/` se tocó y nada de la base se modificó:
las formas y procesos de Mac31 que se usan aquí ya estaban dados de alta, y los
permisos nuevos llegan solos al arrancar desde `PermissionCatalogSeeds`.

- **Base**: `LAP-JUAND` / `Produccion_svr` (Tauro).
- **Backend comprobado en**: `http://127.0.0.1:5089`.
- **Token**: `POST /api/sesion/login` con `BancoPruebas`.
- **Pruebas**: 90 antes, **151 después**, 0 fallos. Las 61 nuevas son de esta ola.
- **Compila**: 0 errores, 0 advertencias.

---

## 1. `RecaudacionesController` estaba abierto de par en par

Era el andamio de Visual Studio, publicado y sin candado.

### Antes / después, sin token

| Petición | Antes | Después |
|---|---|---|
| `GET /api/Recaudaciones` | **200** `["value1","value2"]` | **404** |
| `POST /api/Recaudaciones` | **200** (y no hacía nada) | **404** |
| `PUT /api/Recaudaciones/5` | **200** (y no hacía nada) | **405** |
| `DELETE /api/Recaudaciones/5` | **200** (y no borraba nada) | **405** |
| `GET /api/Recaudaciones/{qr}` | 200 / 400 anónimo | 200 / **400** anónimo (igual, a propósito) |

Los cuatro métodos de plantilla se **borraron**. No eran un pendiente: eran
andamio. La clase conserva `[Authorize]` para que lo que se agregue mañana nazca
cerrado.

### El `GET /{qr}` se queda ANÓNIMO, y este es el porqué

Lo averigüé leyendo el módulo del web, `Web/QR_Web/docs/modules/custom/aviacion/qr/`:

- `js/main.js:190` llama con `fetch(url, { method: "GET" })`, **sin cabecera
  `Authorization`**.
- Esa pantalla **no tiene sesión que adjuntar**: su `html/login.html` es una
  maqueta —`js/login.js` dice literalmente «Aquí después conectas tu API real de
  login» y simula el acceso— y el enlace «Iniciar sesión» está **oculto**
  (`is-hidden`) en el `index.html`.
- Quien la abre es el ciudadano al que se le acaba de entregar el ticket de papel:
  escanea el QR con la cámara del teléfono y cae en la pantalla.

Exigir token ahí apagaría la verificación de tickets para todo el mundo. Y
comprobar un ticket que ya tienes en la mano no es un dato reservado: es el mismo
papel, leído de la fuente.

### Lo que sí se cerró del QR

1. **El pase no es un número adivinable.** No entra un `IDRecaudacion`
   secuencial: entra la cadena cifrada completa (`IDRecaudacion|IDCaja|Dv1`). Sin
   la llave no se fabrica, y los tres datos tienen que cuadrar entre sí. (Esta
   parte ya estaba bien: es distinto del caso de `ReportesV3W`, que sigue
   pendiente en `seguridad-hallazgos.md`.)
2. **Ya no entrega más datos de los necesarios.** Devolvía `RecaudacionDTO`, más
   de 60 campos. Ahora devuelve `RecaudacionPublicaDTO`, los **16** que la
   pantalla pinta. Lo más importante que se quitó:
   - **`DV1` y `DV2`** — los dígitos verificadores con los que se arma el propio
     código QR. Devolverlos a quien solo escaneó un ticket es regalarle el
     material para fabricar otros.
   - El plan de cuentas (`NumCuenta`, `NombreCuenta`, `ImporteCuenta`).
   - Todos los ids internos, `Observaciones`, `TipoPago`,
     `MontoTransferencia`, `FolioLLanta`, `html`, los datos del corte.

Hay cuatro pruebas que lo fijan: que la clase sigue con `[Authorize]`, que solo
hay **un** `[AllowAnonymous]`, que los métodos de plantilla no vuelven, y que el
DTO público no lleva ninguno de los 24 campos prohibidos (ni deja de llevar los
que la pantalla necesita).

---

## 2. `/dbcheck` y `/whoami` anónimos

| Petición | Antes | Después sin token | Después con token |
|---|---|---|---|
| `GET /whoami` | **200** `{"companyId":"local","environment":"Production",…}` | **401** | **200** |
| `GET /dbcheck` | **200** `{"server":"LAP-JUAND","database":"Produccion_svr"}` | **401** | **200** |

### Lo que decidí, y por qué no «solo en Development»

**Piden token y siguen existiendo en los dos entornos.**

Dejarlos solo en Development era la otra opción y la descarté: para lo que sirven
—comprobar que una Web App de Azure quedó apuntando a la empresa y a la base
correctas **después de un despliegue**— hay que poder llamarlos *en producción*.
Si solo existen en local no comprueban nada de lo que se despliega, y con cuatro
sitios en SWA y una base por empresa eso no es un lujo.

Basta con estar autenticado, **sin permiso fino**: no revelan datos de nadie, y
quien tiene que comprobar un despliegue no siempre administra el sistema de
permisos.

---

## 3. Los tres pendientes reales de código

### 3.1 `DashboardVentasReportService.cs:85` — el detalle estaba apagado

`IncluirDetalle` y `MaxDetalleFilas` se recibían del cliente y se tiraban;
`LoadDetalleAsync` era código muerto. Pedir `IncluirDetalle=true` devolvía **200
con el detalle vacío** y sin avisar.

**El motivo de haberlo apagado era real, y estaba en otro sitio.**
`DashboardVentasService.ConsultarVentasDetalleAsync` trae la consulta **completa**
del año y pagina en memoria (`DashboardVentasService.cs:134`), así que el costo es
una consulta pesada **por año**, no una por página. Lo que costaba era pedirla
*siempre*: el reporte la pagaba aunque nadie hubiera pedido detalle.

Terminado así:

- El detalle se consulta **solo si `IncluirDetalle`** viene en `true`.
- `MaxDetalleFilas` se respeta y se **acota**: 500 por omisión, **5000 de tope
  duro por año**. Un `0` no es «sin límite» y un `999999` tampoco; eso es lo que
  separa un reporte de un volcado de la base por HTTP.
- Cuando se recorta, el reporte **lo dice**: `DetalleTruncado` y
  `DetalleTotalRegistros`, que los dos renderizadores ya imprimían
  (`DashboardVentasPdfReportRenderer.cs:230`, el PDF además muestra una muestra de
  30 filas; el Excel las escribe todas).
- **La trampa que había que ver**: `IncluirDetalle` y el tope entran ahora en
  `BuildRequestCacheKey`. Sin eso, el primer reporte sin detalle se le serviría
  también a quien sí lo pidió, y al contrario.

Cinco pruebas, incluida la de la caché.

### 3.2 `VentasUsadosController.cs:29` — guardar usaba el permiso de leer

El GUARDAR exigía `ventas.usados`, que es el permiso de **abrir** la pantalla.
Quien podía mirar podía escribir.

El pendiente decía que usar el permiso correcto «obligaría a tocar el mecanismo
entero» porque el puente de permisos heredados estaba amarrado a una forma por
módulo. **Ya no es cierto**: el puente se parametrizó cuando se hizo el de la
pantalla de pagos (`LegacyModuleBinding` + `VentasPagosBinding`). Esta es la
**tercera** forma del mismo módulo, igual que la segunda. No hubo mecanismo que
tocar.

Verificado en las **dos** empresas, idénticas hasta el número:

```
IDForma 4024 | VentasUsados | CONSULTA DE VENTAS USADOS | IDProceso 5085 | btnGuardar
```

…tanto en `Produccion_svr` (Tauro) como en `MacZ` (Zaragoza). Y sin choque de
`IDProceso` con las otras dos formas de ventas (consulta usa 1..18, 1031, 3063,
5096, 5151-5154, 5196 y 53xx; pagos 1032 y 1033).

Así que `ventas.usados.guardar` es **el mismo permiso que Mac31 ya pide** para ese
botón: a quien lo tiene encendido allá le llega solo, sin doble captura, y a quien
no, el web deja de dejarle guardar. Igual que legacy. **Nada de la base se
modificó**: la forma y el proceso ya estaban dados de alta.

Comprobado: `POST /api/ventas/usados/guardar` sin token **401**, con token llega
al controlador (**400** por body vacío), no 403.

### 3.3 `VentasPedidoCapturaRepository.cs:297, 503` — las recaídas silenciosas

Dependen de scripts de base sin desplegar. **La recaída se queda** (no se toca la
base, y un script pendiente no debe dejar a un repartidor sin poder capturar),
pero deja de ser invisible.

La de `sp_w_ConsultaAgenteUsuario` **apaga un filtro de datos**: sin él, un
usuario que debería ver solo sus pedidos los ve todos. Falla **abierto**, y una
recaída que relaja un filtro sin dejar rastro es indistinguible de no tener
filtro. Comprobé que en esta base **los dos SPs no existen**, o sea que hoy el
filtro está apagado y en silencio.

Ahora cada recaída deja una advertencia en el registro, **una sola vez por
proceso** (el script no se despliega a medias: o está o no está, y una línea por
petición ahogaría el registro sin decir nada nuevo).

---

## 4. `UserAdminService.cs:73` — el `catch` sin filtro ni registro

El bloque limpia los overrides del usuario nuevo para que empiece con los permisos
efectivos de su rol. El `catch` existe por un motivo bueno —en una base sin el
esquema de capacidades no hay overrides que limpiar, y eso no debe impedir dar de
alta a nadie— pero **se comía todo**: si fallaba por un error de la base, un
interbloqueo o un permiso de SQL, el usuario quedaba creado **con los overrides
que hubiera** y la API contestaba **200**.

| Caso | Antes | Después |
|---|---|---|
| Esquema de capacidades ausente | 200, en silencio | **200**, con advertencia en el registro |
| Cualquier otro fallo | **200 con permisos residuales** | **se registra y se relanza** |

Crear a medias un usuario con permisos que nadie le dio es peor que fallar:
fallar se ve. Dos pruebas.

---

## 5. Las 11 escrituras sin permiso fino

Estaban solo con `[Authorize]` de clase, o comprobando nada más que el token
trajera un `userId`.

### Las claves no se inventaron: ya las pedía el front

Están desde hace tiempo en `Web/QR_Web/docs/shared/js/core/module-action-flags.js`,
que es con lo que el web decide si enciende o apaga cada botón:

```
almacen_cascos.{ver_modulo, ver, crear, editar, cancelar}
temporadas.{ver_modulo, ver, crear, editar, cancelar}
torneos.{ver_modulo, ver, crear, editar, cancelar}
```

O sea: **el web ya apagaba los botones por estos permisos y el backend no los
conocía**. Las dos mitades de la aplicación opinaban distinto sobre la misma
persona — justo lo que el proyecto ya evitó en `PermisoControllerBase`.

Se declaran **tal cual** —con guion bajo en `almacen_cascos` incluido, porque
cambiarles una letra las vuelve otras claves— en `PermissionCatalogSeeds`, con su
reparto por rol en `LegacyPolicyMap` (ver con `User`, escribir solo `Admin`, igual
que los módulos hermanos) para las instalaciones sin el modelo de capacidades.

Ojo con el nombre: es **`cancelar`**, no `activar` como en los módulos deportivos
hermanos. Es lo que dice el front, y además describe mejor lo que hacen estas
pantallas: un movimiento de casco no se activa ni se desactiva, se cancela.

### El reparto

| Endpoint | Permiso |
|---|---|
| `POST api/almacen-cascos/salidas` | `almacen_cascos.crear` |
| `POST api/almacen-cascos/entradas` | `almacen_cascos.crear` |
| `POST api/almacen-cascos/movimientos/{id}/tarimas/{n}/kilos` | `almacen_cascos.editar` |
| `PUT api/almacen-cascos/salidas/{id}` | `almacen_cascos.editar` |
| `PUT api/almacen-cascos/entradas/{id}` | `almacen_cascos.editar` |
| `POST api/almacen-cascos/movimientos/{id}/cancelar` | `almacen_cascos.cancelar` |
| `POST api/temporadas` | `temporadas.crear` |
| `PUT api/temporadas/{id}` | `temporadas.editar` |
| `POST api/temporadas/{id}/cancelar` | `temporadas.cancelar` |
| `POST api/temporadas/{id}/reactivar` | `temporadas.cancelar` |
| `POST api/temporadas/cerrar-vencidas` *(nuevo, ver §6)* | `temporadas.cancelar` |
| `POST api/torneos` | `torneos.crear` |
| `PUT api/torneos/{id}` | `torneos.editar` |
| `POST api/torneos/{id}/cancelar` | `torneos.cancelar` |
| `POST api/torneos/{id}/activar` | `torneos.cancelar` |
| `POST api/torneos/{id}/cerrar` | `torneos.cancelar` |
| `POST api/torneos/{id}/reactivar` | `torneos.cancelar` |
| `POST api/torneos/cerrar-vencidos` | `torneos.cancelar` |

Las **lecturas** se dejan con token y sin permiso fino: cerrarlas también apagaría
pantallas que hoy funcionan, y el agujero estaba en las escrituras.

### Cómo se comprobó, y el límite honesto de la comprobación

En vivo, los 17 endpoints de la tabla: **sin token 401**; **con token** llegan al
controlador (400 por body vacío, o 500 donde el `sp_w_*` del módulo no existe en
esta base, que es Tauro y no Zaragoza/ISL) — **ninguno 403**, o sea que el candado
no rompió el camino bueno.

**El 403 del que NO tiene el permiso no se puede probar con la cuenta local.** La
única con la que se entra aquí es `BancoPruebas`, que es **SuperAdmin**, y el
SuperAdmin pasa todo por diseño (`PermissionAuthorizationHandler`, primer `if`).
Los demás usuarios web de esta base (`ADMIN`, `MARYORIS`, `NAOLANY`…) no tienen
hash web y su login cae al conector de legacy, que en local no conecta («Login
failed for user 'sa'»). No forcé un token a mano a propósito.

Así que ese caso va cubierto con pruebas sobre el mecanismo real
(`PermissionAuthorizationHandler` + `PermissionPolicyProvider`), no sobre un
doble: para las 9 claves de escritura, **sin el permiso no pasa, con el permiso
pasa**, y **tener `crear` no da `cancelar`**. Más una prueba que fija el permiso
de cada una de las 18 escrituras y otra que exige que **ninguna** escritura de
esos tres controladores se quede sin candado — para que una acción nueva no pueda
colarse.

---

## 6. Lecturas que escribían sin decirlo

### `GET api/temporadas` y `GET api/torneos`

Ejecutaban `sp_w_CerrarTemporadasVencidas` / `sp_w_CerrarTorneosVencidos` en la
misma transacción **antes** de listar, siempre y sin decirlo. Abrir la pantalla
cambiaba datos; un reintento o un prefetch del navegador se volvían escrituras; y
lo disparaba también quien solo tenía permiso de mirar.

**Lo que averigüé antes de decidir:** no se puede simplemente quitar. Es **lo
único** que cierra los vencidos. El endpoint explícito de torneos existe, pero
**el front no lo llama nunca** — comprobado: `cerrar-vencidos` no aparece en
ningún archivo de `Web/QR_Web` — y el de temporadas no existía. Quitar el efecto
dejaría temporadas y torneos vencidos en «activo» para siempre.

**Lo que decidí: separar el efecto, no borrarlo.**

1. El cierre **solo corre si el usuario puede cerrar** (`temporadas.cancelar` /
   `torneos.cancelar`). Quien solo lee pasa por `ConsultarTemporadasAsync` /
   `ConsultarTorneosAsync`, que **no escriben nada** y ya existían. Los que leen
   leen; los que cierran cierran.
2. Cuando corre, la respuesta **lo declara** en la cabecera `X-Efecto-Lateral`
   (comprobado en vivo: `X-Efecto-Lateral: cerrar-torneos-vencidos`).
3. El cierre de temporadas tiene ya **puerta propia**:
   `POST api/temporadas/cerrar-vencidas`, gemela de la de torneos.
4. Queda escrito en los dos métodos del repositorio, que se llaman
   «`Consultar…`» y escriben.

Efecto secundario bueno: en esta base, donde esos `sp_w_*` no existen, un usuario
sin el permiso de cerrar ahora recibe la lista en vez de un 500.

### `POST api/generacion-rol-torneo/{id}/previsualizar-partidos`

Llama al **mismo** procedimiento que `generar-partidos`, solo con
`confirmarEstado: false`: los partidos **se escriben de verdad**. El nombre invita
a pensar que es una vista previa en memoria y no lo es.

**Se queda así, y queda explicado el porqué**, por dos razones:

1. Es un **POST**, no un GET. El verbo ya avisa de que la petición no es segura:
   no hay reintento del navegador ni prefetch que la dispare sola, que es el
   peligro real de esconder una escritura.
2. Ya lleva **permiso propio y distinto** del de confirmar
   (`generacionroltorneo.editar` frente a `.activar`): quien puede previsualizar
   no necesariamente puede dejar la generación cerrada.

Lo que faltaba era decirlo. Queda dicho.

---

## 7. Lo que encontré de camino y NO estaba en el lote

- **El front gatea botones con permisos que el backend no conocía.** Las 15 claves
  de `almacen_cascos` / `temporadas` / `torneos` llevaban tiempo en
  `module-action-flags.js` sin existir en el catálogo del backend. No era solo que
  faltara el candado: es que las dos mitades de la aplicación no hablaban del
  mismo modelo. Ahora sí.
- **El puente de permisos heredados ya aceptaba varias formas por módulo.** El
  pendiente de `VentasUsados` daba por imposible algo que `VentasPagosBinding`
  había resuelto después de que se escribiera la nota. Vale releer los pendientes
  viejos cuando el mecanismo del que se quejan ha cambiado.
- **El filtro por agente está apagado en esta base, en silencio.** Los dos
  `sp_w_*` de los que depende no existen en `Produccion_svr` local. Ahora se nota
  en el registro, pero **el script sigue pendiente de desplegar** y mientras no
  esté, el filtro no filtra.
- **La página pública del QR tiene un login maqueta.** `html/login.html` +
  `js/login.js` simulan un acceso válido con cualquier usuario y contraseña y
  redirigen a `/qr`. Hoy es inofensivo porque el enlace está oculto y la página no
  protege nada, pero si algún día se le cuelga una API real hay que acordarse de
  que el formulario ya existe y no valida nada.
- **Lo que sigue pendiente de `seguridad-hallazgos.md` y no es de este lote**:
  `GET api/reportes/ventas/ReportesV3W` anónimo con `psp` secuencial (el arreglo
  es aplicarle el pase firmado de `RemisionTicket`), y las credenciales en el
  repo, que no se cierran con un commit sino rotándolas.
