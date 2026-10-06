# Ola 2 — Las consultas de ventas que faltaban (solo lectura)

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5073`.

## El lote cambió a media tarea, y por qué

El lote asignado eran las 43 filas de `registro.tsv` con `estado` en `falta` o
`parcial`, `escribe = no` y módulo de ventas. A mitad del trabajo el coordinador
**rehizo la columna `escribe`**: antes se deducía del NOMBRE del procedimiento y
ahora se lee el cuerpo desde `sys.sql_modules`
(ver `CUIDADO-consultas-que-escriben.md`). El hallazgo que lo cambia todo:

> `sp_n_ConsultaConstantes` **no consulta**. Cuando la fecha de operación está
> atrasada hace el cierre del día: borra y rehace `AlmacenProductoHist` y
> adelanta `Constantes.FechaOperacion`.

Y esa llamada es el **primer paso** de casi todas las pantallas "consultar" de
legacy. Con la columna corregida, el mismo filtro del lote devuelve **20** filas
en vez de 43: las que se cayeron son cabeceras de pantalla que empiezan por
`sp_n_ConsultaConstantes` (cobros, cobranzas, garantías ×3, usados ×6,
inventario de pedidos, ajustes de inventario, consulta de ventas ×6, folios…).
**No se implementaron ni se ejecutaron contra la base**, como se indicó.

De esas 20, cuatro resultaron **escribir de verdad** aunque el registro las
diera como lectura. Se leyeron sus cuerpos y la escritura está dentro de SQL
dinámico, que es por donde se le escapó al clasificador:

| ruta legacy | procedimiento | qué escribe |
|---|---|---|
| `/api/ventaspagos/limpiarcs` | `sp_n_LimpiarCSVentasPagos` | `UPDATE [Ventas Pagos]` (dos veces, dentro de `BEGIN TRAN`) |
| `/api/ventaspagos/aplicar` | `sp_n_AplicarVentasNuevosPagos` | `UPDATE [Ventas Nuevos Pagos]` |
| `/api/garantias/aceptar` | `sp_n_GarantiasAceptar` | `UPDATE <BD_CS>Garantias` armado como texto |
| `/api/ventaspagos/cancelar/transferencia` | `sp_n_CancelarBancoTransferencia` | `UPDATE` a `BancoTranferencias(Movimientos)`, `Ventas`, `[Ventas Pagos]`, `[Ventas Pagos Usados]`, `Clientes` + `INSERT` a `CobrosDineroUsadoClientesLog` |

Quedan **16 endpoints de lectura de verdad**, y son los que se entregan.

## Lo que se hizo, en una frase

Tres módulos nuevos (`VentasCobranzaConsulta`, `VentasUsadosConsulta`,
`VentasDescuentosConsulta`) con **16 endpoints** que llaman a los **mismos
`sp_n_`** que llamaba MacServicios2, con los mismos parámetros, y devuelven el
sobre `{ ok, message, data }` con `data.rows`. Ningún `sp_n_` se tocó; nada de la
base se modificó; no se corrió ningún script.

| archivo | qué |
|---|---|
| `Application/DTOs/ConsultasLegacy/ConsultaFilasResponse.cs` | el contrato de salida común (`rows` + `total`) |
| `Infrastructure/Data/FiltrosLegacy.cs` | fecha, folio, listas de ids y apóstrofo — público para poder probarlo |
| `Infrastructure/Repositories/ConsultaLegacyRepositoryBase.cs` | conexión, ejecución del SP y lectura de la tabla |
| `Application/DTOs/VentasCobranza/VentasCobranzaDtos.cs` | los 5 request de la cobranza |
| `Application/Interfaces/IVentasCobranzaConsulta{Repository,Service}.cs` | contratos |
| `Application/Services/VentasCobranzaConsultaService.cs` | normaliza `Formato` y limpia listas |
| `Infrastructure/Repositories/VentasCobranzaConsultaRepository.cs` | 9 SP de cobranza |
| `Controllers/VentasCobranzaConsultaController.cs` | 9 endpoints |
| `Application/Interfaces/IVentasUsadosConsulta{Repository,Service}.cs` | contratos |
| `Application/Services/VentasUsadosConsultaService.cs` | — |
| `Infrastructure/Repositories/VentasUsadosConsultaRepository.cs` | 5 SP de usados |
| `Controllers/VentasUsadosConsultaController.cs` | 5 endpoints |
| `Application/DTOs/VentasDescuentos/VentasDescuentosDtos.cs` | los 2 request de descuentos |
| `Application/Interfaces/IVentasDescuentosConsulta{Repository,Service}.cs` | contratos |
| `Application/Services/VentasDescuentosConsultaService.cs` | limpia las tres listas de ids |
| `Infrastructure/Repositories/VentasDescuentosConsultaRepository.cs` | 2 SP de descuentos |
| `Controllers/VentasDescuentosConsultaController.cs` | 2 endpoints |
| `Program.cs` | seis `AddScoped` |
| `Infrastructure/Security/PermissionService.cs` | dos permisos nuevos en `PermissionCatalogSeeds` |
| `ISL_Service.Tests/FiltrosLegacyTests.cs` | 32 casos de prueba |

## La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Cobranza: validar folio de venta | `POST /api/cobranzas/validar` | `POST /api/ventas/cobranzas/validar-folio` | `cobranza.ver` | **OK** | 1 renglón, 54 campos; el `IDUsuarioActual` ahora sale del token |
| Cobranza: detalle | `POST /api/cobranzasdetalle/consultar` | `GET /api/ventas/cobranzas/{id}/detalle` | `cobranza.ver` | **OK** | 3 renglones, 74 campos |
| Concentrado: validar folio de venta | `POST /api/concentrados/validar` | `POST /api/ventas/concentrados/validar-folio` | `cobranza.ver` | **OK** | 1 renglón, 49 campos |
| Concentrado: detalle | `POST /api/concentradosdetalle/consultar` | `GET /api/ventas/concentrados/{id}/detalle` | `cobranza.ver` | **OK** | 3 renglones, 62 campos |
| Cobros de centro de servicio | `POST /api/cobros/centroservicio/consultar` | `GET /api/ventas/cobros/centro-servicio` | `cobranza.ver` | **OK** | `Cobros` está vacía en Tauro; coinciden en el vacío |
| Cobro: folios y cascos | `POST /api/cobrosdetalle/consultar` | `POST /api/ventas/cobros/folios-usados` | `cobranza.ver` | no verificable | Legacy pierde los nombres de columna (ver abajo). Verificado contra el SP: 3 × 118 idénticos |
| Cobro: pagos aplicados | `POST /api/cobrospagos/consultar` | `POST /api/ventas/cobros/pagos` | `cobranza.ver` | no verificable | lo mismo, y además vacío en Tauro |
| Pagos por kilo | `POST /api/consultaventaspagos/kilos/consultar` | `POST /api/ventas/cobros/pagos-kilos` | `cobranza.ver` | **OK** | vacío en Tauro; el nuevo **arregla** el rango de fechas |
| Folios sin asignar | `POST /api/ventasfolios/sinasignar/consultar` | `GET /api/ventas/folios-sin-asignar` | `cobranza.ver` | **OK** | 285 renglones, 27 campos |
| Usados: detalle de movimiento | `POST /api/usadosmovimientos/consultar/detalle` | `GET /api/ventas/usados/consulta/movimientos/{id}/detalle` | `ventas.usados` | **OK** | 8 renglones; 18 columnas de cabecera fuera (null vs 0) |
| Usados: detalle de cobro | `POST /api/ventascobrosusados/consultar/detalle` | `GET /api/ventas/usados/consulta/cobros/detalle` | `ventas.usados` | **OK** | 8 renglones, 28 campos |
| Usados: detalle de cobro de dinero | `POST /api/ventascobrosdinerousados/consultar/detalle` | `GET /api/ventas/usados/consulta/cobros-dinero/detalle` | `ventas.usados` | **OK** | `CobrosDineroUsado` vacía en Tauro |
| Usados: detalle de excedente | `POST /api/ventasusadosexcedentes/consultar/detalle` | `GET /api/ventas/usados/consulta/excedentes/detalle` | `ventas.usados` | no verificable | Legacy pierde los nombres. Verificado contra el SP: 8 × 28 idénticos |
| Usados: bitácora de créditos | `POST /api/ventasusadoscreditoslog/consultar` | `GET /api/ventas/usados/consulta/creditos/{idVenta}/log` | `ventas.usados` | no verificable | lo mismo. Verificado contra el SP: 1 × 22 idénticos |
| Descuentos: detalle | `POST /api/descuentosdetalle/consultar` | `POST /api/ventas/descuentos/detalle` | `descuentos.ver` | **OK** | 129 renglones; + un caso extra con 2 marcas (67) que prueba la virgulilla |
| Descuentos: bitácora | `POST /api/descuentosdetalle/consultar/log` | `POST /api/ventas/descuentos/detalle/log` | `descuentos.ver` | **OK** | 11 renglones, 16 campos |

**13 casos en verde, 4 no verificables** (sobre 16 capacidades: el detalle de
descuentos lleva dos casos). En los 4, lo que no responde es **Legacy**.

## Cómo se demuestra

```bash
cd migracion/comparador
NUEVO_BASE=http://127.0.0.1:5073 MAC_SCRATCH=<scratch> python comparar.py casos/
```

Los 13 verificables son `comparador/casos/ola2-*.json` y salen **OK**:

```
13 coinciden, 0 con diferencias, 13 casos
```

La fecha de operación se miró antes y después de cada tanda y **no se movió**
(`2026-10-06`, igual que hoy), así que los dos lados partieron del mismo estado.

Los 4 no verificables viven en `comparador/casos/no-verificables/` con su
`_porque`. El motivo es siempre el mismo y es de Legacy:

> Legacy no mapea esas tablas a un DTO: hace `ds.Tables[0].AsEnumerable().ToList()`
> y responde `new GenericResponse(List<DataRow>)`. Al serializar un `DataRow`,
> Json.NET no escribe las columnas, escribe las propiedades del objeto:
> `{ RowError, RowState, Table, ItemArray, HasErrors }`. **Los nombres de las
> columnas se perdieron**; solo queda `ItemArray`, un arreglo posicional. El
> comparador compara por nombre, así que no hay nada que comparar.

Para esos cuatro se verificó contra la **fuente**: se ejecutó el mismo
procedimiento con `sqlcmd` y se contrastó columna por columna contra la respuesta
del backend nuevo. Resultado: mismo número de renglones, mismas columnas y mismos
valores; la única diferencia es el formato del `datetime`
(`2026-02-28 10:45:35.853` del cliente de SQL contra `2026-02-28T10:45:35.853` del
JSON — el mismo instante).

| endpoint | SP | renglones × columnas | valores distintos |
|---|---|---|---|
| `cobros/folios-usados` | `sp_n_ConcentradoFoliosUsados 133102,0,0,0,0` | 3 × 118 | 6, todas de formato de fecha |
| `cobros/pagos` | `sp_n_ConsultaVentasCobroPagos 133102,0,0,0` | 0 × 17 | los dos vacíos |
| `usados/excedentes/detalle` | `sp_n_ConsultarVentasUsadosExcendentesDetalle 4250,0,0,0` | 8 × 28 | 8, todas de formato de fecha |
| `usados/creditos/log` | `sp_n_VentasUsadosCreditoLog 369664` | 1 × 22 | 1, de formato de fecha |

Pruebas automatizadas:
`dotnet test ISL_Service/ISL_Service/ISL_Service.Tests/ISL_Service.Tests.csproj`
→ **122 pruebas, 0 fallos** (32 nuevas).

## Permisos nuevos

Dos, los dos en `PermissionCatalogSeeds` (`PermissionService.cs`):

| clave | para qué | por qué no se reusó otro |
|---|---|---|
| `cobranza.ver` | cobros, cobranzas, concentrados y folios sin asignar | ver abajo |
| `descuentos.ver` | catálogo de descuentos y su bitácora | un descuento es **dinero**, no un desplegable: no cabe en `catalogos.ver` de la ola 1, igual que allá se separó `catalogos.galleros.ver` porque el renglón traía saldo |

El tercer grupo, los detalles de usados, **no estrena permiso**: reusa
`ventas.usados`, que sí lo manda Mac31 (es `btnUsados` de la forma
CONSULTA DE VENTAS, ya mapeado en `VentasLegacyPermissionMap`). Ahí reusar es lo
correcto: quien puede abrir los usados de una remisión es exactamente quien debe
poder ver su detalle y su bitácora, y así hay **una sola verdad** —la de Mac31—
en vez de dos interruptores que hay que acordarse de encender juntos. Se acepta
además `app_movil.ventas.usados`, por la razón de siempre: el teléfono se presta
y la computadora de la oficina no.

### Por qué `cobranza.*` y `descuentos.*` son familias nuevas

Esto es la trampa de la sección 2.8 del resumen del inventario, y se decidió con
cuidado:

1. **No se inventó un `ventas.cobranza.ver`.** Para la familia `ventas.*` manda
   Mac31: al construir el snapshot se BORRA todo lo que empiece por `ventas.` y
   no esté en `VentasWebPermissionSeeds`, y se recalcula desde los botones de la
   forma CONSULTA DE VENTAS. Una clave nueva ahí se guarda en `WUsuarioPermiso`
   y **desaparece en el siguiente cálculo**, sin error y sin rastro. Es la misma
   cicatriz que ya dejó "pendientes de autorizar".
2. **No se reusó `ventas.consultar`.** Es `btnBuscar` de la pantalla Consultar
   ventas. La cobranza son TRES pantallas distintas de Mac31 (Cobranza,
   Concentrado y Cobro) y el catálogo de descuentos es otra. Reusarlo ataría
   cosas que la gente necesita por separado: hay quien cobra y no consulta
   ventas.
3. **No se amarraron al puente de permisos heredados.** `LegacyModuleBinding` va
   por UNA forma por módulo y la cobranza son tres; amarrarlo obligaría a
   cambiar el puente, que es la pieza que más rompe de ese archivo.

Las dos claves quedan **sembradas en código**, no en la base: entran a `WPermiso`
cuando alguien sincronice el catálogo desde la pantalla de permisos. No se corrió
ningún script.

## Diferencias intencionales (las de fondo)

1. **Renglones crudos en vez de DTO tipado.** Legacy mete cada tabla en un DTO y
   `DataTableToList` empareja por nombre **exacto**, así que (a) lo que el SP
   devuelve y el DTO no declara se **tira** y (b) lo que el DTO declara y el SP
   no devuelve sale en `0`/`null` como relleno. El nuevo pasa el renglón del SP.
   Cada caso lo anota con la lista exacta de campos de cada lado.
2. **POST → GET** donde la consulta es por ids. En Legacy eran POST porque su
   handler exigía el token dentro del JSON; aquí va en la cabecera.
3. **`null` en vez de `0` y de `0001-01-01`.** Donde el SP devuelve NULL, el DTO
   no anulable de Legacy entrega `0` o `0001-01-01T00:00:00`. Mac31 pinta esas
   celdas **en blanco** (su rejilla está ligada al `DataTable`), así que `null`
   es el valor fiel. Un `0` en `TotalPagar` se lee como "no se pagó nada", que no
   es lo mismo que "este renglón no tiene pago".
4. **La fecha se normaliza a ISO y ya no se lee `Constantes`.** Legacy voltea
   `dd-MM-yyyy` a `MM-dd-yyyy` con `Substring` de posiciones fijas según
   `formatoFechaDB`. Aquí se manda `yyyy-MM-dd HH:mm:ss`, que SQL Server entiende
   igual sea cual sea el `DATEFORMAT`. **Esto es lo que mantiene la ola de solo
   lectura**: no hace falta llamar a `sp_n_ConsultaConstantes`, que es quien
   cierra el día.
5. **Se exige al menos un filtro** donde el SP barre la tabla entera con todo en
   cero: cobros de centro de servicio, folios y cascos, pagos del cobro, los
   cuatro detalles de usados, los folios sin asignar y la bitácora de descuentos.
6. **El relleno de los `char(n)` se recorta.** Varias columnas son `char` fijo y
   SQL Server devuelve el relleno dentro; en Mac31 no se veía porque el control
   de WinForms lo recortaba al pintar.
7. **`IDUsuarioActual` sale del token, no del cuerpo.** No es cosmético: en los
   dos "validar folio" ese id decide la columna `ValidoAlmacen` ("NO TIENE ACCESO
   A ESE ALMACÉN") contra `UsuariosAlmacenes`. En Legacy viaja en el body, así
   que cualquiera podía pedir la validación con el id de otro y saltarse el
   candado.
8. **El rango de fechas de pagos por kilo sí se respeta.** Legacy es el único de
   la familia que pasa las fechas **sin convertir** (`findKilosAsync`), así que
   con `formatoFechaDB = "MM-dd-yyyy"` un filtro "del 1 de febrero" le llega como
   "del 2 de enero". El caso se compara sin rango a propósito, para que esa
   diferencia no tape lo que sí tiene que coincidir.
9. **No se expone el rango de fechas de la bitácora de descuentos**, aunque el SP
   lo acepte: Legacy manda solo tres de sus cinco parámetros y ofrecer un filtro
   que esa pantalla nunca tuvo sería ofrecer algo que nadie ha probado.
10. **`Formato` / `Presentacion` se acota a 0, 1 y 2** (el `enumPresentacion` de
    Mac31). Cualquier otro número cae a 0 en vez de bajar al procedimiento.

## Lo que el backend nuevo arregla

No son diferencias de contrato, son defectos de Legacy que no se heredaron:

* **El detalle de descuentos revienta en Legacy.** `DescuentoDetalleInputDTO`
  tiene el constructor vacío, así que las tres listas llegan `null` y
  `string.Join(null)` lanza `ArgumentNullException`: el endpoint contesta 400 con
  el stack. Para compararlo hay que mandarle las tres listas vacías a mano. El
  nuevo las inicializa.
* **Dos campos que Legacy pierde al validar un folio de cobranza.**
  `ConsultaCobranzasFolioDTO` escribe `Saldofavor` y `SaldofavorCascos` con `f`
  minúscula mientras el SP devuelve `SaldoFavor` y `SaldoFavorCascos`:
  `DataTableToList` no los encuentra y Legacy entrega `0`.
* **`IDVenta` en los folios sin asignar.** El DTO de Legacy lo declara y el SP no
  lo devuelve, así que sale `0` en los 285 renglones. Por eso el caso ordena por
  `Folio`: ordenar por un campo que siempre vale 0 deja las dos listas al azar.
* **Dos ramas muertas por copiar la condición.** En
  `ConsultaCobranzasService.cs:62` y `ConsultaCobrosService.cs:61` el `else if`
  repite la condición del `if` (`FolioInicialCobranza != "0"` en las dos), así
  que la rama "buscar por folio de venta" nunca corre. Aquí no se hereda porque
  cada endpoint recibe solo los campos que su SP usa.
* **El reset del DTO.** Legacy, al llenar un filtro de folio, reconstruye el DTO
  desde cero y con eso **tira todos los demás filtros, incluido
  `IDUsuarioActual`** (el del candado de almacén). Aquí no hace falta imitarlo.

## Pendientes que quedan anotados

* **`registro.tsv` no se tocó.** Varios agentes escriben ese archivo al mismo
  tiempo y reescribirlo entero podría pisar sus filas. Hay que pasar las 16 filas
  a `hecho`, con `ubicacion_nueva` = la ruta nueva de la tabla de arriba,
  `metodo_validacion` = `comparador` y `resultado` = `OK` o `no verificable`.
* **Las cabeceras que empiezan por `sp_n_ConsultaConstantes`** siguen en
  `falta`/`parcial`. Son la mitad más visible de esas pantallas (la lista que se
  ve al pulsar Buscar) y hay que hacerlas **dentro del ciclo de instantánea**,
  nunca a pelo. La buena noticia: el `FiltrosLegacy` de esta ola ya resuelve el
  único motivo por el que legacy llamaba a ese procedimiento en una consulta
  (leer `formatoFechaDB`), así que el backend nuevo puede hacer esas consultas
  **sin llamarlo** y por lo tanto sin disparar el cierre del día. Es el camino
  recomendado.
* **Cuatro endpoints quedaron fuera por escribir** aunque `registro.tsv` los dé
  como lectura (tabla del principio). Hay que corregirles la columna `escribe`:
  el clasificador no ve el DML que va dentro de SQL dinámico.
* **`/api/descuentos/consultar` no se migró**: llama a `sp_n_ConsultaDescuentos`,
  que **no existe** en esta base (hay `sp_Descuentos`, pero no ése). Legacy falla
  igual, así que no hay contra qué comparar.
* **`/api/descuentos/simular` tampoco**: `sp_n_SimularDescuento` hace
  `INSERT INTO Descuentos` cuando no encuentra el renglón de simulación de ese
  usuario y equipo. Es escritura, no lectura.
* **`/api/ventasfacturas/sendemail` tampoco**: además de que su capacidad quedó
  en `escribe = si`, **manda un correo de verdad** (`smtp.Send`, con el PDF y el
  XML adjuntos) y crea un directorio en disco. No es una consulta.
* **Tres casos coinciden en el vacío** porque Tauro no usa esas pantallas
  (`Cobros`, `CobrosDineroUsado` y los pagos por kilo están vacías). Hay que
  volver a correrlos contra **Zaragoza (`MacZ`)**, que sí las usa.
* **`sp_n_ConsultarVentasUsadosExcendentesDetalle` dice "Excendentes"** con una
  `n` de más, y así se llama en la base. El código nuevo escribe el nombre bien
  (`Excedente`) en todo salvo en el nombre del SP y del parámetro, para que el
  día que se reescriba como `sp_w_` el error no le sobreviva.
