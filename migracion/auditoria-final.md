# Auditoría final independiente — 7-oct-2026

Auditoría de solo lectura. No se tocó `Legacy/`, ni la base, ni el código del
backend. No se ejecutó ninguna capacidad marcada `escribe = si`. No hay commits.

**Entorno comprobado:** backend nuevo en `127.0.0.1:5055` (PID 151024, arrancado
00:04:55 desde `bin/Debug/net8.0/ISL_Service.dll` compilado a las 00:03:20 — es
decir, **construido del HEAD actual**, `5877a2b` en `migracion-legacy`, árbol
limpio). Legacy vivo en `:63479`. Bases `Produccion_svr` (886 procedimientos) y
`MacZ` (914).

---

## 0. Veredicto en una línea

El tablero dice **488 de 488 cerradas**. La cifra honesta es **79 % ejecutable y
44 % verificado**, sobre una superficie real de **~582 unidades**, no 488. Y hay
**dos filas que dicen `hecho` y están muertas**: un choque de rutas deja
`POST /api/ventas/descuentos/detalle` devolviendo **500 a todo**.

---

## 1. Las dos filas falsas (lo más importante de este informe)

### 1.1 Choque de rutas: dos capacidades muertas

`POST api/ventas/descuentos/detalle` está declarada **dos veces**:

| archivo:línea | método | para qué |
|---|---|---|
| `ISL_Service/Controllers/VentasDescuentosConsultaController.cs:58` | `Detalle` | **leer** el detalle del descuento |
| `ISL_Service/Controllers/VentasDescuentosController.cs:132` | `CrearDetalleAsync` | **agregar** un renglón al detalle |

Las dos cuelgan de `[Route("api/ventas/descuentos")]`. ASP.NET Core lanza
`AmbiguousMatchException` y responde **500 a cualquier petición**:

```
POST /api/ventas/descuentos/detalle  {"IDDescuento":53518}                          -> 500
POST /api/ventas/descuentos/detalle  {...los 6 campos completos...}                  -> 500
POST /api/ventas/descuentos/detalle  {"IDDescuento":0,...}                           -> 500
POST /api/ventas/descuentos/detalle/log {"IDDescuento":53518}                        -> 200  (el hermano sí sirve)
```

**No es culpa de la base.** El procedimiento corre perfecto y devuelve los 129
renglones, con `NULL` y con `''` en los tres filtros:

```
EXEC dbo.sp_n_ConsultaDescuentosDetalle @IDsMarca='',@IDsCategoria='',
     @IDsGrupoCategoria='',@IDDescuentoDetalle=0,@IDDescuento=53518,@IDVenta=0
-> IDDescuentoDetalle|IDDescuento|IDGrupoCategoria|...|Porcentaje
   0|53518|1|1|1|ACUMULADORES|ACUMULADOR|AMERICA|.0000   (y 128 más)
```

Lo que dice el tablero de esas dos filas:

| capacidad | estado | método | resultado | realidad |
|---|---|---|---|---|
| Consultar detalle de descuentos | `hecho` | **comparado contra Legacy** | **OK** | **500 siempre** |
| Registrar detalle de descuentos | `hecho-sin-comparar` | Legacy no responde | NO VERIFICABLE | **500 siempre** |

La fila de "Consultar" afirma haberse comparado contra Legacy con resultado OK
para un endpoint que **no ha podido responder 200 ni una vez** en la versión
entregada. Hay además **dos casos del banco** (`casos/ola2-descuentos-detalle.json`
y `casos/ola2-descuentos-detalle-marcas.json`) que hoy dan 500 y por tanto **no
pueden estar en verde**.

Barrido completo: son **las únicas dos** colisiones reales en 694 endpoints. El
otro choque aparente (`api/[controller]/insertar` entre `PersonasController` y
`ProveedoresPagosController`) es falso positivo: el token `[controller]` expande
distinto en cada clase.

### 1.2 Lo que NO resultó falso

Para ser justo: comprobé las 39 filas cuya `ubicacion_nueva` mi analizador no
encontraba y **ninguna es funcionalidad ausente**. Probadas en vivo sin token
(404 = la ruta no existe, 401 = existe):

```
PUT /api/catalogos/autos/1 ... /ciudades/1 ... /colonias/1 ... /empresas/1
    /empresas-series/1 /grupos-categoria/1 /municipios/1 /marcas/1
    /unidades-territoriales/1 /centros-servicio/1 /categorias-mac/1       -> 401 (existe)
POST /api/ventas/multipago                                               -> 404
POST /api/ventas/usados/credito/ajustar                                  -> 404
```

Las dos últimas existen con otro nombre (`/api/ventas/multipago/guardar` y
`POST /api/ventas/usados/guardar`, que llama a `AjustarAsync`). Son **errores de
anotación del tablero, no huecos**. En total **41 filas** tienen la
`ubicacion_nueva` mal escrita o puesta como texto descriptivo en vez de ruta
(13 no son ni una ruta: `'…/catalogos → categorias'`, `'(dentro de
clientes/buscar)'`). Eso no rompe nada, pero hace el tablero inauditable sin
rehacer el trabajo a mano, que es justo lo que acabo de hacer.

---

## 2. ¿Falta funcionalidad que nadie contó? La brecha de 488 vs ≈643

De los tres huecos que señaló la auditoría anterior, **dos están cerrados y uno
sigue abierto**:

| hueco señalado | estado hoy | evidencia |
|---|---|---|
| 27 procedimientos del código en ninguna fila | **CERRADO** | los 447 `sp_` que usa el código de Legacy aparecen todos en `registro.tsv`; intersección vacía |
| 8 filas con el tipo de token invertido | **CERRADO** | cruzando las 488 filas contra el verbo real (prefijo `Post`/`Put`/`Get`/`Delete` del método) y la ruta expandida desde `EndPoints.cs`: **0** desajustes (2 "no match" que son artefactos de mi expansión de constantes en `ParametrosController`) |
| 145 payloads de reporte en 2 filas | **SIGUE ABIERTO** | ver abajo |

### 2.1 El inventario de 488 sí está completo

Conté por mi cuenta la superficie de `Legacy/MacServicios2`: **131
controladores, 495 métodos públicos**. Los 7 de diferencia contra 488 son:

- `LogImpresionesWebController.PostLogImpresionesWeb` — **todo el cuerpo está
  comentado con `////`**. Correctamente excluido.
- `UsuariosFechasController` (2 métodos) — están en el tablero como
  `classUsuariosFechasController`, nombre distinto, capacidad cubierta.
- 4 métodos públicos sin `[Route]` en `CatCajas`, `CatOperadores`,
  `MCContratoEstimaciones`, `Recaudaciones`, `Sincronizacion`, `Ventas` — no son
  acciones enrutables.

Cruzando **método por método con la ruta expandida**: **0 métodos `[Route]` de
Legacy sin fila**. El inventario de capacidades de controlador es correcto.

### 2.2 Pero los reportes siguen colapsados en 2 filas

Estas dos filas, y nada más, representan todo el motor de reportes:

| capacidad | estado | metodo_validacion | resultado |
|---|---|---|---|
| Consultar reportes (motor v1) | `hecho` | *cubierto por el modulo equivalente* | HECHO |
| Consultar reportes (motor v2) | `hecho` | *cubierto por el modulo equivalente* | HECHO |

Detrás hay, según los propios artefactos del repo:

- `reportes-cobertura.tsv`: **229 reportes**, de los cuales **96 están en el menú
  vivo de Mac31** (60 nodos + 36 variantes).
- `ola-7-reportes.md`, fechado 6-oct 22:48 y honesto en el detalle: **75 de 99
  entregan documento (76 %)**. Es decir **~21-24 reportes del menú vivo de Mac31
  que el backend nuevo NO entrega**, y que el tablero cuenta como `hecho`.

Ninguna de las dos filas se comparó contra Legacy ejecutando: dicen *"cubierto
por el módulo equivalente"*. El `resultado: HECHO` de la segunda convive con
`sp_faltantes: sp_n_ConsultaEmpleadosTipoSueldoRecibo;…ReciboZara`.

Medido de otro modo: de los **150 procedimientos de reporte** del universo de
Legacy, el backend nuevo **nombra 4** en su código — el resto los resuelve
dinámicamente desde `dbo.Reportes`, lo cual es correcto por diseño, pero
significa que **ninguna herramienta estática puede decir qué reportes funcionan**:
solo generándolos uno por uno, que es lo que hizo la ola 7 y lo que el tablero
tiró a la basura al resumirlo en dos `HECHO`.

### 2.3 La aritmética de ≈643

La cifra de la auditoría anterior cuadra: `488 filas − 2 (reportes colapsados) +
96 (nodos vivos de reporte) + ~10 métodos borrosos ≈ 592-643`. **Mi superficie
honesta: ~582 unidades** contando solo el menú vivo de Mac31, **~715** contando
las 229 filas de `reportes-cobertura.tsv`.

---

## 3. "Migradas" que están a medias

### 3.1 92 capacidades cuelgan de procedimientos que no existen en ninguna base

Cruzando los procedimientos declarados en cada fila contra `sys.procedures` de
**las dos** bases de empresa:

```
387  filas: todos sus procedimientos existen en Produccion_svr   -> pueden ejecutar
 92  filas: al menos un procedimiento NO existe en NINGUNA base  -> no pueden ejecutar aquí
  6  filas sin procedimiento declarado
  3  codigo-muerto
```

**Dato a favor del trabajo:** el cruce estado × ejecutabilidad es
**perfectamente limpio**. Las 92 están **todas** en `hecho-sin-comparar`.
**Cero** filas marcadas `hecho` (comparadas) cuelgan de un procedimiento
inexistente. La clasificación por fila es disciplinada y honesta.

El problema no es la clasificación: es el **titular**. Esas 92 están escritas,
enrutadas, con permiso y documentadas, y **devolverán 500 el día que alguien las
llame**, porque los 88 `sp_n_` que les faltan no existen en `Produccion_svr` ni
en `MacZ` y **no hay script para crearlos en ningún sitio del repo** (0 de 88 —
son procedimientos propiedad de legacy). Reparto por módulo:

| módulo (controlador de Legacy) | filas |
|---|---|
| Pólizas (aviación) | 11 |
| Económicos / Cuentas / Operadores / Permisionarios / Conceptos / Motivos fianza / Cajas / Impresoras (aviación) | 39 |
| Obra: partidas, periodos, convenios, gerentes, conceptos | 19 |
| Proyectos | 4 |
| Recaudaciones (cortes, recurrentes, next-id, cancelar) | 5 |
| Descuentos (cabecera: consultar / actualizar / eliminar) | 3 |
| Centros de servicio · Unidades territoriales | 6 |
| Servicios (sueltos) · Sincronización | 4 |
| Ventas (cancelar pagos por kilo, devolución Luro) | 2 |

**Salvedad que no puedo cerrar aquí:** `prod` y local no tienen el mismo esquema.
Algunos de esos 88 podrían existir en producción. **No lo puedo comprobar** — me
falta un volcado de `sys.procedures` de la base de producción. Lo que sí puedo
decir es que **Legacy tampoco los tiene aquí**, así que Mac31 local falla igual.

### 3.2 41 procedimientos `sp_w_`/`sp_mo_` propios no están instalados en Tauro

El backend nuevo llama a 41 procedimientos propios que **no existen en
`Produccion_svr`**. 32 de ellos **sí existen en `MacZ`**. Los 41 **tienen script
de creación** en `D:\JuanD\GitHub\MAC\Querrys\` (360 archivos `.sql`), así que es
un pendiente de **despliegue**, no de trabajo. Pero mientras no se instalen, en
Tauro están caídos:

- **Todo el Dashboard de ventas** (9 procedimientos). Comprobado en vivo:
  ```
  POST /api/dashboard/filtros/consultar {"FechaInicial":"1900-01-01",...}
  -> 500  "Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasFiltros'."
  ```
- Checador web completo (comidas, movimientos, configuración, empleados).
- Captura de pedidos: `sp_w_ConsultaPedido`, `ActualizarPedido`,
  `InsertarPedidoDetalle`, `EliminarPedidoDetalle`, `EliminarPedidoBorrador`.
- Cheques, Tarimas, favoritos de módulo, `sp_mo_ConsultaMisPedidos` (móvil).

### 3.3 Lo que de verdad miente dentro del código

Hallazgos verificados en el fuente, ordenados por gravedad:

1. **Un error de SQL regala el crédito completo al cliente.**
   `Infrastructure/Repositories/VentasPedidoCapturaRepository.cs:1353` —
   `catch (SqlException) { }` **sin log**, dejando `saldoPendiente` y
   `saldoVencido` en cero. Esos ceros alimentan la línea 408 del mismo archivo:
   `saldo["disponible"] = limite - saldoPendiente - saldoVencido;`.
   El llamador **no puede distinguir "la consulta falló" de "no debe nada"**.
   Es el único degradado del archivo que no sigue el estándar del propio
   proyecto (log una vez por proceso, explicado en las líneas 18-35); los de las
   líneas 324 y 540 sí lo siguen.

2. **El filtro por agente falla ABIERTO.** Mismo archivo, 324-339: si
   `sp_w_ConsultaAgenteUsuario` no está desplegado —y en Tauro **no está**— se
   anota una vez y **todos ven los pedidos de todos**. Es un agujero de
   visibilidad de datos, no un detalle de rendimiento.

3. **El dashboard acepta 11 filtros y los tira.**
   `Infrastructure/Repositories/DashboardVentasRepository.cs:43` — el parámetro
   `request` **no se referencia en el cuerpo**; el `SqlCommand` se arma con cero
   parámetros. El endpoint devolvería 200 con los catálogos sin filtrar y el
   cliente no tiene forma de saber que su rango de fechas se ignoró.

4. **`PATCH /usuarios/{id}/empresa` siempre falla con 500.**
   `Application/Services/UserAdminService.cs:302` devuelve
   `Task.FromException(new InvalidOperationException(...))`, que el middleware
   convierte en **500**, no en 501/400. Está enrutado, con permiso
   (`usuarios.empresa.editar`) y documentado: una capacidad retirada disfrazada
   de error de servidor.

5. **`RecaudacionService.cs:28,38,49`** reconstruye el DTO de petición desde cero
   en tres de cuatro ramas, **descartando silenciosamente** `FechaInicial`,
   `FechaFinal`, `IDStatus`, `IDConcepto`, `IDCuenta`, `IDEconomico`,
   `IDPermisionario`, `IDOperador`. Es exactamente el antipatrón que
   `VentasCobranzaConsultaService.cs:14-21` documenta como *"LO QUE AQUÍ NO SE
   COPIA DE LEGACY"*.

6. **`CatalogosAdminRepository.Estructurados.cs:104`** —
   `ActualizarUnidadTerritorialAsync` **descarta todo el cuerpo** y manda solo el
   id. El `PUT` de un nombre nuevo es un no-op. (Honesto en el comentario, y
   `LeerResultado` devuelve `-1`, así que no finge éxito.)

7. **`UserRepository.cs:103`** — el `catch (SqlException) when (ex.Number is 2812
   or 208)` baja a autenticación "solo WEB". El error 208 puede venir **de dentro**
   del procedimiento de login de legacy, no solo de su ausencia: una tabla roída
   degrada la autenticación en silencio y sin log.

8. **`ResultadoLegacyDtos.cs:60`** — `Hecho => Resultado is null ||
   Resultado.Value > 0`. Todo procedimiento de escritura que no devuelva columna
   `result` se reporta como **hecho** por omisión. Está documentado como
   intencional, pero para toda esa familia de endpoints "la llamada llegó a la
   base" y "la operación ocurrió" son el mismo valor.

9. **`VentasMultiPagoService.cs:873`** — tras una excepción,
   `resp.Guardo = resp.Aplicadas > 0`: un multipago **parcialmente aplicado**
   reporta `Guardo = true`.

**Lo que resultó limpio y hay que decirlo:** **cero** `NotImplementedException`
en código de producción (los ~90 son dobles de prueba). **Cero** marcadores
`TODO:`/`FIXME`/`HACK`. Los 24 aciertos de "TODO" son el español *todos*. **Cero**
endpoints que devuelvan 501 o "no migrado". Módulos enteros (Aviación, Sueltos
8/9, Obra-contratos) están **documentados en prosa** como no verificables, no
escondidos.

---

## 4. ¿Las comparaciones prueban algo? 29, no 18

Repliqué el armado de petición de `comparar.py` (incluido el `query`, que al
principio se me pasó) y pedí **el lado nuevo de los 156 casos**. No llamé a
Legacy: `Constantes.FechaOperacion = 2026-10-06` y hoy es **2026-10-07**, así que
la primera llamada a Legacy hubiera disparado `sp_n_ConsultaConstantes`, o sea
**el cierre del día del negocio** — borrar y rehacer `AlmacenProductoHist` y
rodar la fecha de operación. El backend nuevo evita ese procedimiento a
propósito (lee `Constantes` con `SELECT`), así que pedirle a él es inofensivo.

```
casos verificables: 133   no-verificables: 23
status del lado nuevo: 131 x 200,  2 x 500
```

| | |
|---|---|
| casos verificables que devuelven **lista vacía** con 200 | **29** |
| + uno que devuelve `null` (`grupo-d-concentrado-de-folio`) | **30** |
| casos verificables que hoy dan **500** | **2** (los de descuentos/detalle) |

El `ok` de `comparar.py` es `not diffs and est_leg == 200 and est_nue in (200,204)`.
Con las dos listas vacías, `diffs` está vacío y **el caso pasa**. Esos 29 `OK`
pasarían igual si el endpoint devolviera siempre vacío. **El problema no se
arregló: creció de 18 a 29.**

**Diez de ellos están citados por nombre en filas que dicen `hecho` /
"comparado contra Legacy" / `OK`:**

```
ola4-cobro-clientes                 -> /api/cobrosclientes/consultar
ola4-cobros-consultar               -> /api/cobros/consultar
ola4-garantias-aceptar-consultar    -> /api/garantias/aceptar/consultar
ola4-garantias-registro-consultar   -> /api/garantias/registrar/consultar
ola4-usados-cheques-consultar       -> /api/ventascobrosdinerousados/cheques/consultar
ola4-usados-cobros-consultar        -> /api/ventascobrosusados/consultar
ola4-usados-cobros-dinero-consultar -> /api/ventascobrosdinerousados/consultar
ola4-usados-cortes-cs-consultar     -> /api/ventascobrosdinerousados/cortescs/consultar
ola4-usados-excedentes-consultar    -> /api/ventasusadosexcedentes/consultar
ola4-ventas-transito-consultar      -> /api/consultaventas/consultar/transito
```

En honor a la verdad, la mayoría son **tablas genuinamente vacías en Tauro**
(`MC_Contratos` 0, `ProveedoresDomicilios` 0, `ProveedoresApoderados` 0,
`MC_Residentes` 0, `[Catalogo Galleros]` 0) y los `observaciones` lo dicen. Pero
hay al menos un caso donde **el banco eligió el parámetro vacío teniendo datos a
mano**: `ola3-huellas-usuarios` pide `origen=usuario` y `UsuariosHuellas` tiene
**0** filas, mientras `EmpleadosHuellas` tiene **409**, y el endpoint sí responde
bien con `origen=empleado`. Un caso que compara donde no hay nada, pudiendo
comparar donde hay 409 renglones, no es una prueba: es un trámite.

---

## 5. Permisos: el arreglo quedó a medias

Extraje todas las claves con forma de permiso del fuente (345 candidatas) y las
crucé contra `dbo.WPermiso`.

**En `Produccion_svr` (434 claves sembradas, todas EmpresaId = 1) faltan 6:**

| clave | dónde se exige | consecuencia |
|---|---|---|
| `usuarios.password.ver` | `Controllers/UsuariosController.cs:16` | **403 para todos menos SuperAdmin.** No está en `PermissionCatalogSeeds` ni en `WPermiso`; `usuarios.password.reset` sí está. Ver la contraseña de otro es inalcanzable para quien no sea SuperAdmin, para siempre. |
| `reportes.centros.cambiar` | `Application/Security/PermisosDeCentros.cs:22` | el selector de centros **no se ve**, y todo sale de la base principal |
| `app_movil.ventas.usados` | `Controllers/VentasUsadosConsultaController.cs:49` | el canal móvil no puede tener su clave propia (se salva porque se acepta `ventas.usados`, que sí está) |
| `reportes.centros`, `reportes.legacy`, `reportes.web` | `PermissionService.cs:2762,2805` | son nombres de **módulo**, no claves. Falso positivo. |

Y además: `WPermiso` **no tiene ni una** clave `reportes.centros.*`, aunque
`dbo.CentrosServicio` existe con **10 centros activos**. Son **11 claves
ausentes** (`cambiar` + una por centro).

**La causa es estructural y es exactamente el bug que se dio por cerrado.**
`CatalogoDePermisosHostedService` llama al arrancar a
`SyncPermissionCatalogAsync`, que siembra **solo** la lista estática
`PermissionCatalogSeeds` (268 claves). Las siembras de centros y de reportes
viven en `EnsureCentrosPermissionsSyncedAsync` y
`EnsureLegacyReportPermissionsSyncedAsync`, y esas **solo se llaman desde
`GetPermisosWebBootstrapAsync`** (`PermissionService.cs:1077`), es decir **cuando
alguien abre la pantalla de permisos** — precisamente lo que el comentario de
cabecera del servicio dice que se arregló:

> *"Hasta ahora solo llegaba a `WPermiso` si una persona entraba a la pantalla de
> permisos y pulsaba sincronizar. […] un modulo podia estar escrito, probado y
> comprobado contra Legacy... y estar muerto para los usuarios de verdad."*

**En `MacZ` (Zaragoza) el agujero es ancho:** faltan **239** de las 345 claves.
De esas, **227 las sembraría el arranque**; **12 no**:

```
app_movil.cascos_cambio.{ver,crear,cancelar}   cascos_cambio.{ver,crear,cancelar}
app_movil.ventas.usados                        ventas.usados.guardar
ventas.ver_modulo                              reportes.{centros,legacy,web}  (módulos)
```

O sea: **desplegar este backend contra una base de empresa nueva deja Cambio de
Cascos y Usados contestando 403 a todo el que no sea SuperAdmin** hasta que
alguien abra la pantalla de permisos. El arreglo cubre 268 claves de 280 que
hacen falta; las 12 restantes y las 11 de centros siguen dependiendo de que un
humano entre a una pantalla.

---

## 6. Dependencias hacia el sistema viejo

**No queda ninguna dependencia dura de que MacServicios2, MacReportes2 o Mac31
estén encendidos.** Verificado:

- **Una sola** llamada HTTP saliente en todo el backend: Facturapi
  (`Infrastructure/Facturacion/FacturapiCliente.cs:47`, `https://www.facturapi.io/v2`).
  Un solo `HttpClient` registrado (`Program.cs:292`). Sin `WebClient`, sin
  RestSharp. **`63479` no aparece en ningún `.cs`** — solo en el arnés de
  comparación de `migracion/`, que no se compila ni se publica.
- **Cero** referencias a ensamblados de legacy: `ISL_Service.csproj` solo tiene
  `PackageReference`; ni un `<Reference Include=`, ni un `<HintPath>`. No hay
  `Mac3Servicios.dll`, ni `MacReportes*`, ni `CrystalDecisions*` en todo
  `sacNet-8`.
- **Los reportes se renderizan en proceso.** Cero Crystal / ReportViewer / SSRS /
  `.rdl` / `.rdlc` / `.rpt`. El PDF lo hace un `wkhtmltopdf` empaquetado
  (`Assets/Wkhtmltopdf/`, copiado al output por el csproj); el único
  `Process.Start` del código. Excel con ClosedXML. Las plantillas HTML salen de
  **la base** (`Template_Html`), no de disco.
- **Ni una ruta UNC, ni una unidad mapeada, ni un literal `C:\`/`D:\` en C#.**
  Todos los `C:\Mac21\…` del fuente están dentro de comentarios.
- `Mac3SqlServerConnector.cs` es un eco de nombre: envoltorio de `SqlConnection`.
  Su constructor `(int NumCaja)` y su `ConnectionStrings["Mac3"]` están muertos.

**Lo que sí queda, y hay que decirlo:**

| qué | dónde | tipo |
|---|---|---|
| El esquema de legacy: los `sp_n_*`, `Template_Html`, `Constantes`, `Parametros`, `Reportes`. **No hay un solo `CREATE PROCEDURE sp_n_*` en el repo** — viven solo en la base. | transversal | **DURA** (presencia, no proceso) |
| `File.Exists(pdf)` / `File.Exists(xml)` del CFDI, con la ruta que da la base | `Application/Services/VentasFacturacionService.cs:361-363` | **DURA** sobre el **sistema de archivos** donde legacy dejó el papel; sin él, 409 al enviar por correo |
| Carpetas de imágenes de la máquina de legacy (`Constantes.PathImagenes`) | `RemisionHtmlBuilder.cs:157`, `RemisionImpresionRepository.cs:285`, `CascosCambioService.cs:247` | **blanda**: hay fallback y `--load-error-handling ignore`; el reporte sale sin logo |
| `Logo_Mac_WM.png` (marca de agua de la remisión) **no está empaquetado** | `Assets/Logos/` | blanda, cosmética |
| Servidor SQL central de Aviación, leído de `Constantes.AviacionDBServer` | `Infrastructure/Data/ConexionDeAviacion.cs:120-133` | DURA **solo para Aviación**; vacío en Tauro → 409 limpio |

Hallazgo de higiene, no de legacy: credencial de producción en el fuente
(`Utils/…`→`Mac3SqlServerConnector.cs:46-47`, `sa`/`Hope5y2k`) y contraseñas +
llave JWT en claro en `appsettings.json`.

---

## 7. La muestra: 47 filas

Muestra aleatoria con semilla fija (`20261007`), estratificada: **28 `hecho` + 16
`hecho-sin-comparar` + las 3 `codigo-muerto`**, repartida sobre todas las olas
(catálogos, ventas, cortes, inventario, nómina, obra, aviación, reportes,
seguridad). Cada fila se comprobó en cuatro ejes: ¿existe la ruta nueva?,
¿coinciden los procedimientos?, ¿existe el procedimiento en la base?, ¿corre su
caso?

**Resultado: 0 de 47 resultaron falsas en el sentido fuerte** (ninguna de la
muestra está demostrablemente rota). **17 de 47 llevan algún defecto:**

| defecto | filas |
|---|---|
| cuelga de un `sp_n_` que no existe en ninguna de las dos bases (aviación, obra, proyectos, servicios, centros, unidades territoriales) | 14 |
| `ubicacion_nueva` no es una ruta o apunta a una que no existe | 5 |
| cuenta como `sp_faltantes` un procedimiento **sustituido a propósito** (`sp_n_InsertarEmpleadosAsistencia` → `sp_w_GuardarAsistenciaDia`) | 1 |

Las 3 `codigo-muerto` las verifiqué en el fuente de Legacy y **están bien
clasificadas**: `CatFormaProcesoRepository.cs:30-31` llama de verdad a
`sp_n_InsertarDiaFestivo` / `sp_n_ActualizarDiaFestivo` — un copy-paste de
Legacy, no un error del inventario.

Es un buen resultado para la muestra. **Las dos filas falsas no las encontró el
muestreo, sino el barrido sistemático de colisiones de ruta.** Vale la pena
decirlo: con 47 filas de 488 la probabilidad de caer sobre 2 filas malas era de
~18 %. **Muestrear no basta aquí; hay que barrer.**

---

## 8. Mi número honesto de cobertura

### Lo que el tablero cuenta mal

El titular de 488/488 descansa en tres equivalencias que no se sostienen:

1. **`hecho` ≠ comparado.** De las 296 `hecho`, solo **249** dicen *"comparado
   contra Legacy"*. Otras **36** tienen `metodo_validacion` **vacío** y **11**
   dicen *"cubierto por el modulo equivalente"*, que es una afirmación de
   arquitectura, no una medición.
2. **`hecho-sin-comparar` ≠ funciona.** Las 189 incluyen las **92** cuyo
   procedimiento no existe en ninguna base: escritas, enrutadas y **condenadas a
   500**.
3. **2 filas ≠ 96 reportes.**

### Los números

Sobre la superficie honesta de **~582 unidades** (488 capacidades − 2 filas de
reporte + 96 nodos vivos de reporte):

| | unidades | % |
|---|---|---|
| **Comparado contra Legacy con datos reales** (249 filas − 10 vacuas + ~20 reportes comparados en la ola 7) | **~259** | **44 %** |
| **Implementado y ejecutable hoy en Tauro** (387 filas − 2 muertas por colisión + 75 reportes) | **~460** | **79 %** |
| Implementado pero inejecutable en cualquiera de las dos bases (92 capacidades + ~21 reportes) | ~113 | 19 % |
| Demostrablemente muerto en el código entregado | 2 | 0.3 % |

**Cobertura de procedimientos.** El tablero dice **451 de 594**. Mi cuenta:
**443 de 594** ejecutados por el backend nuevo — y el desglose importa más que el
total:

```
de los 447 sp_ del CODIGO de Legacy:      442 ejecutados por el nuevo  (99 %)
de los 150 sp_ de REPORTES:                 4 nombrados en el codigo   (resueltos desde dbo.Reportes)
sp_ del codigo que el nuevo no toca:        5  -> los 5 son sustituciones deliberadas y correctas
                                               (sp_w_InsertarEmpleado, sp_w_GuardarAsistenciaDia,
                                                sp_n_ConsultaEmpleadosTipoSueldo; sp_n_DevolucionVentaLuro
                                                no existe en la base)
```

La cobertura **del código** de Legacy es excelente. La de **reportes** es
indeterminable estáticamente y, medida a mano por la ola 7, es **76 %**.

---

## 9. La lista exacta de lo que falta

**Roto en el código entregado (hay que arreglarlo, no es de la base):**

1. `POST /api/ventas/descuentos/detalle` — colisión de ruta, **dos capacidades
   muertas**: consultar el detalle del descuento y agregarle un renglón.
   `VentasDescuentosConsultaController.cs:58` vs `VentasDescuentosController.cs:132`.
2. `PATCH /usuarios/{id}/empresa` — 500 permanente en vez de 501/400.
3. `POST /api/dashboard/filtros/consultar` — ignora los 11 filtros del cuerpo
   (`DashboardVentasRepository.cs:43`).
4. `PUT` de unidades territoriales — descarta el cuerpo, es un no-op
   (`CatalogosAdminRepository.Estructurados.cs:104`).
5. `RecaudacionService.cs:28,38,49` — descarta 8 filtros de la petición.
6. `VentasPedidoCapturaRepository.cs:1353` — un error de SQL **regala el límite
   de crédito completo**, sin log.
7. `VentasPedidoCapturaRepository.cs:324` — el filtro por agente **falla abierto**.

**Falta de permiso (403 para todos menos SuperAdmin):**

8. `usuarios.password.ver` — no está en las semillas ni en `WPermiso`.
9. `reportes.centros.cambiar` + las 10 claves `reportes.centros.<numero>.ver` —
   la siembra existe pero **no se llama al arrancar**.
10. En base de empresa nueva: `cascos_cambio.*` (3), `app_movil.cascos_cambio.*`
    (3), `ventas.usados.guardar`, `ventas.ver_modulo`, `app_movil.ventas.usados`.

**Falta de despliegue en Tauro (los scripts existen en `MAC/Querrys`):**

11. 41 `sp_w_`/`sp_mo_`, entre ellos **los 9 del Dashboard de ventas**, los 6 del
    Checador web, los 5 de captura de pedidos, Cheques, Tarimas, favoritos de
    módulo y `sp_mo_ConsultaMisPedidos` (móvil).

**Reportes del menú vivo de Mac31 que no entregan documento (de `ola-7` +
`reportes-cobertura.tsv`):**

12. `1375` / `1380` Estado de Cuenta DINERO y CASCOS — **defecto de la base**:
    `sp_n_rptVentasEstadoCuentaGlobales` llama al interno con 6 argumentos y el
    interno recibe 1. Roto en las dos empresas. Lo tiene que arreglar quien
    mantiene legacy.
13. `1410` / `1415` / `1420` Centros (remisiones, pagos entradas/salidas,
    garantías) — faltan las bases `MacZCS*` en este servidor.
14. `72` Pagos Detallado Centro de Servicio — cableado, solo se elige con
    `EsCentroServicio = 1`; estas dos bases valen 0.
15. `493`, `106`, `1360` — defectos de los procedimientos en la base.
16. `1426`, `1428`, `1429`, `1430`, `1432`, `1455` — su `spV2` no existe en la
    base.

**88 `sp_n_` sin existencia en ninguna base ni script en el repo** → las 92
capacidades de Aviación, Obra (partidas/periodos/convenios/gerentes), Proyectos,
Sueltos, cabecera de Descuentos, Centros de servicio, Unidades territoriales y
cortes de Recaudaciones. **No puedo determinar si existen en producción**; me
falta un volcado de `sys.procedures` de allá.

---

## 10. Veredicto: ¿puede un Web o un Móvil nuevo vivir solo de este backend?

# NO — todavía no. Pero está cerca, y el "no" es corto y nombrable.

**Por qué no:**

1. **La pantalla de Descuentos no se puede construir.** Su consulta de detalle y
   su alta de renglón devuelven 500 a todo por una colisión de rutas. Y la
   cabecera (`sp_n_ConsultaDescuentos`) no existe en ninguna base. El módulo
   entero está inalcanzable.
2. **Faltan 21-24 reportes del menú vivo de Mac31.** Un Web que no entrega el
   Estado de Cuenta DINERO/CASCOS ni los reportes de Centros no reemplaza a
   Mac31 para quien los usa. Seis de esos son defectos heredados de la base y
   tres son bases que no están aquí: trabajo de otros, pero **el hueco es del
   mismo tamaño para el usuario**.
3. **Dos features están muertas por permiso**: ver la contraseña de otro usuario
   y todo el selector de centros (11 claves). Contestan 403 a todo el que no sea
   SuperAdmin. Es el mismo bug que se declaró cerrado.
4. **En Tauro faltan 41 procedimientos propios por instalar**, y con ellos se
   cae el Dashboard completo, el Checador web y la captura de pedidos. Es
   despliegue, no desarrollo — pero hoy, contra esta base, no funciona.
5. **Aviación y Obra-contratos están escritas y no pueden ejecutar** (88
   procedimientos ausentes). Legacy tampoco puede aquí, así que puede que no sea
   deuda de esta migración; **no lo puedo afirmar sin el esquema de producción**.
6. **El filtro por agente falla abierto** y **un error de SQL regala el crédito**.
   Un Web nuevo heredaría los dos como agujeros propios.

**Por qué está cerca, y qué sí me consta:**

- **No hay ninguna dependencia de que legacy esté encendido.** Ni una llamada
  HTTP, ni un DLL, ni Crystal, ni una ruta UNC. Los reportes se renderizan en
  proceso con `wkhtmltopdf` empaquetado. Eso es un logro grande y está
  comprobado, no supuesto.
- **442 de los 447 procedimientos del código de Legacy están cableados**, y los 5
  restantes son sustituciones deliberadas y correctas.
- **El inventario de 488 capacidades de controlador es completo.** Lo conté por
  mi cuenta: 0 métodos `[Route]` de Legacy sin fila. Dos de los tres huecos que
  señaló la auditoría anterior están cerrados.
- **La clasificación por fila es honesta donde importa**: cero filas marcadas
  `hecho` cuelgan de un procedimiento inexistente, y los `observaciones`
  documentan los bloqueos con nombre y causa.
- **694 endpoints** y una sola colisión de ruta en todo el backend.

**Lo que el tablero debería decir, y no dice:** no "488 de 488 cerradas", sino
*"~460 de ~582 ejecutables, ~259 verificadas contra Legacy, 2 muertas, 21-24
reportes pendientes, 41 procedimientos por desplegar y 11 claves de permiso sin
sembrar."* Esa frase es más larga y es la que sirve para decidir.

---

## 11. Lo que no pude comprobar, y qué me falta

| no comprobado | qué hace falta |
|---|---|
| Si los 88 `sp_n_` ausentes existen en **producción** | un volcado de `sys.procedures` de la base de producción de Tauro y de Zaragoza |
| **Reproducir las 296 comparaciones** del tablero | `Constantes.FechaOperacion` está en 2026-10-06 y hoy es el 07: la primera llamada a Legacy dispara el cierre del día. Hace falta dejarlo rodar una vez a propósito (y yo tengo prohibido escribir) y recién entonces comparar |
| Las **349 escrituras** | solo se prueban dentro del ciclo de instantánea; tengo prohibido ejecutar `escribe = si`. El repo conserva un solo resultado de escrituras (`comparador/escrituras/resultado-ola4.json`) |
| Aviación y centros (`MacZCS*`) | esas bases no están en esta máquina |
| Si la columna `Archivo` del CFDI resuelve a una ruta alcanzable desde el VPS | una comprobación en el entorno de producción |
| Las **412 pruebas unitarias** que dice el tablero | no las corrí: compilar y ejecutar habría recompilado el backend que estaba sirviendo mis mediciones, y habría invalidado todo lo anterior |
