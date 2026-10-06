# Ola 3 — El resto de las lecturas (ni catálogos ni ventas)

Lote original: las filas de `registro.tsv` con `estado` en `falta`/`parcial`, `escribe = no` y ruta que
no empieza por `/api/cat`, quitando los módulos de ventas. Eran **72**.

**Quedaron 37.** La diferencia no es un recorte de alcance: es la corrección de la columna
`escribe` del registro. Ver [§ Los que se cayeron](#los-que-se-cayeron-y-por-qué).

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5075`.
Fecha de operación al empezar y al terminar: `2026-10-06` (no se movió).

## Lo que se hizo, en una frase

Seis módulos nuevos de **solo lectura** (`obra`, `clientes`, `productos`, `cortes`, `huellas`,
`actualizaciones-aplicacion`), siete catálogos complementarios colgados del prefijo que ya existe
(`api/catalogos`), y tres añadidos a módulos vivos (`asistencias`, `usuarios`, `proveedores`):
**37 GET** que llaman a los **mismos `sp_n_`** que llamaba MacServicios2, con los mismos parámetros, y
devuelven el sobre `{ ok, message, data }`. Ningún `sp_n_` se tocó; nada de la base se modificó.

| archivo | qué |
|---|---|
| `Application/DTOs/Obra/ObraDtos.cs` | DTOs de contratos y catálogos de obra |
| `Application/DTOs/CatalogosComplementos/CatalogosComplementosDtos.cs` | los siete catálogos que faltaban |
| `Application/DTOs/ClientesAnexos/ClientesAnexosDtos.cs` | pestañas de la ficha del cliente |
| `Application/DTOs/ProveedoresAnexos/ProveedoresAnexosDtos.cs` | pestañas de la ficha del proveedor |
| `Application/DTOs/Productos/ProductosDtos.cs` | producto con precios y existencias |
| `Application/DTOs/Cortes/CortesDtos.cs` | corte de caja y corte de centro de servicio |
| `Application/DTOs/Huellas/HuellasDtos.cs` | huella registrada (usuario / empleado / cliente) |
| `Application/DTOs/UsuariosAnexos/UsuariosAnexosDtos.cs` | formas-procesos y fechas de usuario |
| `Application/DTOs/ActualizacionesAplicacion/ActualizacionesAplicacionDtos.cs` | manifiesto del arrancador |
| `Application/DTOs/Asistencias/AsistenciasDtos.cs` | **+1 DTO** (`AsistenciaDiariaDto`) |
| `Application/Interfaces/I{Obra,CatalogosComplementos,ClientesAnexos,ProveedoresAnexos,Productos,Cortes,Huellas,UsuariosAnexos,ActualizacionesAplicacion}*.cs` | 13 contratos |
| `Application/Services/{Obra,CatalogosComplementos,ClientesAnexos,ProveedoresAnexos,Productos,Cortes,Huellas,UsuariosAnexos,ActualizacionesAplicacion}Service.cs` | normalización de filtros |
| `Infrastructure/Repositories/{Obra,CatalogosComplementos,ClientesAnexos,ProveedoresAnexos,Productos,Cortes,Huellas,UsuariosAnexos,ActualizacionesAplicacion}Repository.cs` | los 28 SP |
| `Infrastructure/Repositories/AsistenciasRepository.cs` | **+1 método** (asistencia diaria) |
| `Application/Services/AsistenciasService.cs` | **+1 método** |
| `Controllers/{Obra,CatalogosComplementos,ClientesAnexos,ProveedoresAnexos,Productos,Cortes,Huellas,UsuariosAnexos,ActualizacionesAplicacion}Controller.cs` | 9 controladores |
| `Controllers/AsistenciasController.cs` | **+1 acción** (`GET /diaria`) |
| `Program.cs` | 18 `AddScoped` |
| `Infrastructure/Security/PermissionService.cs` | 14 permisos nuevos en `PermissionCatalogSeeds` |
| `ISL_Service.Tests/Ola3NormalizacionFiltrosTests.cs` | 20 casos de prueba |

## La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Obra — gerentes | `POST /api/mcgerentes/consultar` | `GET /api/obra/catalogos/gerentes` | `obra.catalogos.ver` | no verificable | la tabla `MC_Gerentes` no existe en esta base; **Legacy falla igual** (500, mismo mensaje de SQL) |
| Obra — puestos | `POST /api/mcpuestos/consultar` | `GET /api/obra/catalogos/puestos` | `obra.catalogos.ver` | **OK** | 2 renglones; expone `esRepresentanteLegal`/`Tecnico` como filtro |
| Obra — residentes | `POST /api/mcresidentes/consultar` | `GET /api/obra/catalogos/residentes` | `obra.catalogos.ver` | **OK** | tabla vacía en Tauro; coinciden en el vacío |
| Obra — tipos de contratación | `POST /api/mctiposcontratacion/consultar` | `GET /api/obra/catalogos/tipos-contratacion` | `obra.catalogos.ver` | **OK** | 3 renglones |
| Obra — tipos de documento | `POST /api/mctiposdocumento/consultar` | `GET /api/obra/catalogos/tipos-documento` | `obra.catalogos.ver` | **OK** | 5 renglones; **`exacto` no aplica**: el SP ignora `@Identico` |
| Obra — tipos de obra | `POST /api/mctiposobra/consultar` | `GET /api/obra/catalogos/tipos-obra` | `obra.catalogos.ver` | **OK** | 3 renglones |
| Obra — tipos de recurso | `POST /api/mctiposrecurso/consultar` | `GET /api/obra/catalogos/tipos-recurso` | `obra.catalogos.ver` | **OK** | 2 renglones |
| Obra — unidades de medida | `POST /api/mcunidadesmedida/consultar` | `GET /api/obra/catalogos/unidades-medida` | `obra.catalogos.ver` | **OK** | 6 renglones; fuera `UnidadMedidaFtm` ("%PORCENTAJE", sin espacio) |
| Obra — proveedores | `POST /api/mcproveedores/consultar` | `GET /api/obra/proveedores` | `obra.proveedores.ver` | **OK** | tabla vacía; permiso aparte porque trae RFC, CURP, teléfono y correo |
| Obra — grupos de contrato | `POST /api/mccontratos/grupos/consultar` | `GET /api/obra/contratos/grupos` | `obra.contratos.ver` | **OK** | tabla vacía |
| Obra — conceptos de contrato | `POST /api/mccontratos/conceptos/consultar` | `GET /api/obra/contratos/conceptos` | `obra.contratos.ver` | **OK** | tabla vacía; ojo con los renglones sintéticos de cabecera de grupo |
| Obra — estimaciones de contrato | `POST /api/mccontratos/estimaciones/consultar` | `GET /api/obra/contratos/estimaciones` | `obra.contratos.ver` | **OK** | tabla vacía; **no se migra** el filtro `Contrato` (ver diferencia 9) |
| Tipos de domicilio | `POST /api/tiposdomicilio/consultar` | `GET /api/catalogos/tipos-domicilio` | `catalogos.ver` | **OK** | 2 renglones; Legacy no tenía DTO (devolvía `DataRow` crudos) |
| Condiciones de crédito | `POST /api/CatCondicionesCredito/consultar` | `GET /api/catalogos/condiciones-credito` | `catalogos.ver` | **OK** | 7 renglones; sin filtro por días (el SP recibe `@DiasCredito` y no lo usa) |
| Series de empresa | `POST /api/empresas/series/consultar` | `GET /api/catalogos/empresas-series` | `catalogos.ver` | **OK** | 1 renglón; `IDFacturama` es **texto**, no int |
| FE — formas de pago | `POST /api/CatFEFormasPago/consultar` | `GET /api/catalogos/fe/formas-pago` | `catalogos.fe.ver` | **OK** | 22 renglones; sin `idStatus` (el SP lo ignora) |
| FE — métodos de pago | `POST /api/CatFEMetodosPago/consultar` | `GET /api/catalogos/fe/metodos-pago` | `catalogos.fe.ver` | **OK** | 2 renglones (PUE/PPD) |
| FE — motivos de cancelación | `POST /api/CatFEMotivosCancelacion/consultar` | `GET /api/catalogos/fe/motivos-cancelacion` | `catalogos.fe.ver` | **OK** | 4 renglones |
| Marcas | `POST /api/marcas/consultar` | `GET /api/catalogos/marcas` | `catalogos.marcas.ver` | **OK** | 140 renglones; el `0` de la lista se descarta (ver diferencia 10) |
| Cliente — bonificaciones | `POST /api/clientes/bonificacion/consultar` | `GET /api/clientes/{id}/bonificaciones` | `clientes.ver` | **OK** | 8 renglones; uno por tipo de usado activo, exista o no la bonificación |
| Cliente — centros de servicio | `POST /api/clientes/centroservicio/consultar` | `GET /api/clientes/{id}/centros-servicio` | `clientes.ver` | **OK** | 10 renglones; `fecha` anulable (ver diferencia 5) |
| Cliente — bitácora | `POST /api/clientes/consultar/log` | `GET /api/clientes/{id}/log` | `clientes.log.ver` | **OK** | vacío en el cliente 1; **sin filtro por fecha** (el `WHERE` del SP está comentado) |
| Cliente — facturación | `POST /api/clientes/facturacion/consultar` | `GET /api/clientes/facturacion` | `clientes.facturacion.ver` | **OK** | 241 renglones; recupera los tres campos de auditoría que Legacy perdía |
| Proveedor — apoderados | `POST /api/proveedores/apoderados/consultar` | `GET /api/proveedores/apoderados` | `proveedores.ver` | **OK** | tabla vacía; filtro por **prefijo** |
| Proveedor — domicilios | `POST /api/proveedores/domicilios/consultar` | `GET /api/proveedores/domicilios` | `proveedores.ver` | **OK** | tabla vacía; todos los joins son INNER (colonia inválida = renglón invisible) |
| Proveedor — documentos | `POST /api/proveedores/documentos/consultar` | `GET /api/proveedores/documentos` | `proveedores.ver` | **OK** | tabla vacía; fuera `ArchivoFtm` (miente con NULL) |
| Productos | `POST /api/productos/consultar` | `GET /api/productos` | `productos.ver` | **OK** | 3.3 MB en Legacy; permiso propio porque trae precio y margen |
| Corte — detalle | `POST /api/cortes/consultar/detalle` | `GET /api/cortes/{id}/detalle` | `cortes.ver` | **OK** | 4 renglones en el corte 1 |
| Corte — siguiente fecha | `POST /api/cortes/consultar/actual` | `GET /api/cortes/siguiente-fecha` | `cortes.ver` | **OK** | sin parámetros: el SP pisa el `@IDCorte` que recibe |
| Corte CS — cobros | `POST /api/cortescs/cobros/consultar` | `GET /api/cortes/centros-servicio/cobros` | `cortes.cs.ver` | no verificable | el cursor del SP consulta `MacZCS7..10`, que no están en este servidor; **Legacy falla igual** |
| Corte CS — ventas canceladas | `POST /api/cortescs/ventas/canceladas/consultar` | `GET /api/cortes/centros-servicio/ventas-canceladas` | `cortes.cs.ver` | **OK** | tabla vacía; recupera `sumaAbonos`, que Legacy tiraba |
| Usuario — formas y procesos | `POST /api/usuarios/formas/procesos/consultar` | `GET /api/usuarios/formas-procesos` | `permisosweb.bootstrap` | **OK** | 368 renglones; es el mapa de seguridad de Mac31 |
| Usuario — siguiente número | `POST /api/usuarios/consultar/nextid` | `GET /api/usuarios/siguiente-numero` | `usuarios.crear` | **OK** | no reserva nada: `MAX+1` en el momento |
| Usuario — fechas de operación | `POST /api/usuarios/fechas/consultar` | `GET /api/usuarios/fechas` | `usuarios.ver` | **OK** | vacío para el usuario 9; tabla `UsuariosFechas`, **sin** espacio |
| Huellas | `POST /api/huellas/consultar` | `GET /api/huellas` | `empleados.huellas` | **OK** | tabla vacía; un origen por llamada (ver diferencia 11) |
| Asistencia del día | `POST /api/empleadosperiodoasistencias/diaria/consultar` | `GET /api/asistencias/diaria` | `asistencias.ver` (+ `empleados.sueldo.ver` para los nombres) | **OK** | nadie checó hoy; el interruptor de nombres **sale del body** (ver lógica escondida 1) |
| Actualizaciones de aplicación | `POST /api/aplicacionesactualizacion/consultar` | `GET /api/actualizaciones-aplicacion` | `aplicaciones.actualizaciones.ver` | **OK** | 63 archivos; Legacy devolvía `DataRow` crudos (1.1 MB para 63 renglones) |

**35 en verde, 2 no verificables.** En los dos, lo que no responde es **la base**: una tabla ausente
(`MC_Gerentes`) y cuatro bases de centro de servicio que no están en este servidor. Legacy contesta
exactamente el mismo error de SQL Server en los dos casos.

## Cómo se demuestra

```bash
cd migracion/comparador
NUEVO_BASE=http://127.0.0.1:5075 MAC_SCRATCH=<scratch> python comparar.py casos/
```

Los 35 verificables viven en `comparador/casos/ola3-*.json` y salen **OK**. Los 2 no verificables viven
en `comparador/casos/no-verificables/ola3-*.json`, con su `_porque`, igual que hizo la ola 1.

Pruebas automatizadas: `dotnet test ISL_Service/ISL_Service/ISL_Service.Tests/ISL_Service.Tests.csproj`
→ **110 pruebas, 0 fallos** (20 nuevas).

## Los que se cayeron, y por qué

A media entrega se corrigió la columna `escribe` del registro: antes se deducía del **nombre** del
procedimiento y ahora se lee su **cuerpo**. El hallazgo que lo destapó vale para un tercio del sistema:

> **`sp_n_ConsultaConstantes` no consulta: cierra el día.** Cuando `Constantes.FechaOperacion` está
> atrasada inserta en `FechaOperacionLog`, **borra y rehace `AlmacenProductoHist`** (el congelado de
> existencias del día anterior) y adelanta la fecha de operación del negocio. Y en toda llamada
> registra actividad del usuario.

Veintiuno de los endpoints de mi lote lo llamaban **solo para leer una columna** (`formatoFechaDB`) y
poder voltear el día y el mes del filtro de fechas. Abrir una pantalla de consulta no puede cerrar el
día, así que salieron del lote: **no se implementaron y no se ejecutaron contra la base.**

| se cayó | por qué |
|---|---|
| `/api/polizas/*` (7) | `escribe = si` (Constantes) y además los SP viven en `MacAviacion*`, que no está en esta máquina |
| `/api/recaudaciones/consultar`, `/consultar/cortes`, `/consultar/recurrentes` | `escribe = si` (Constantes). El `sp_n_ConsultaRecaudaciones` de esta base además es una versión vieja: declara **9** parámetros y el repositorio de Legacy le manda **31** |
| `/api/recaudaciones/consultar/nextid` | `escribe = ?`: el SP no está en esta base |
| `/api/cortes/consultar`, `/consultar/tipogastos`, `/api/cortescs/consultar` | `escribe = si` (Constantes) |
| `/api/consultacompras/consultar` | `escribe = si` (Constantes) |
| `/api/gasolinas/consultar` | `escribe = si` (Constantes) |
| `/api/bancostransferencias/consultar`, `/consultar/detalle` | `escribe = si` (Constantes) |
| `/api/cambiosvigencia/consultar` | `escribe = si` (Constantes) |
| `/api/mccontratos/consultar`, `/api/mcconvenios/consultar` | `escribe = si` (Constantes) |
| `/api/mccontratos/partidas/consultar`, `/periodos/consultar`, `/conceptos/estimaciones/consultar`, `/api/mcpartidas/consultar` | `escribe = ?`: los SP no están en esta base |
| `/api/proyectos/consultar`, `/api/servicios/consultar`, `/api/recordatorios/consultar`, `/api/sincronizacion/consultar` | `escribe = si` (Constantes); tres de los cuatro además tienen el SP ausente |
| `/api/impresoras/consultar` | `escribe = ?`: el SP no está en esta base |
| `/api/parametros/consultar` | **el SP escribe por su cuenta**: tras el `SELECT` hace `DELETE Parametros WHERE Fecha <= DATEADD(dd,-30,getdate())`. Es una purga, no una consulta |
| `/api/empleadostiposueldorecibos/consultar`, `/nomina/consultar` | **el SP escribe fuerte**: `sp_n_ConsultaEmpleadosTipoSueldoRecibo` hace `DELETE`/`INSERT EmpleadosRecibos`, `DELETE`/`INSERT EmpleadoPrestamosPagos` y `UPDATE EmpleadoPrestamos` con cursor. Calcula la nómina, no la consulta |
| `/api/empleadosprestamo/validar` | **el batch que descuenta los préstamos de la nómina**: `BEGIN TRAN`, inserta en `EmpleadoPrestamosPagos` y actualiza `EmpleadoPrestamos`. El nombre `/validar` engaña |
| `/api/periodostiposueldodias/validar` | igual: `INSERT PeriodosTipoSueldo`, `INSERT PeriodosTipoSueldoDias` (uno por día de la semana) y `UPDATE PeriodosTipoSueldo`. **Crea** los periodos de nómina |
| 6 rutas de alta/modificación que el registro dejó en `escribe = no` | `/api/bancostransferencias/cancelar`, `/api/CatCondicionesCredito` (POST y PUT), `/api/clientes/facturacion` (PUT), `/api/clientes/facturacion/mostrador`, `/api/tiposdomicilio` (PUT). Sus SP son `sp_n_Actualizar*`/`sp_n_Cancelar*`: el reclasificador no les encontró escritura (probablemente porque el SP falta o delega), pero su intención es escribir y este lote es de solo lectura |

**Hay trabajo recuperable ahí.** Veintiuno de esos endpoints no escriben por su propia consulta: solo
arrastran `sp_n_ConsultaConstantes` porque Legacy necesitaba `formatoFechaDB` para su truco de fechas.
El backend nuevo **no necesita ese truco**: recibe las fechas tipadas y puede leer
`Constantes.FormatoFechaDB` con un `SELECT TOP 1` que no escribe nada. Convertirlos en lecturas de
verdad es una entrega chica y de valor alto, pero es una **decisión de contrato**, no un detalle de
migración, y por eso no se tomó aquí.

## Lógica escondida en C# que se encontró

Lo que no estaba en el SP y se perdería en una migración descuidada:

1. **El interruptor que destapaba los nombres de la asistencia venía en el body.** En Tauro,
   `sp_n_ConsultaEmpleadosAsistenciaDiaria` tapa el nombre del empleado con dieciocho asteriscos si
   recibe `@MostrarAsistenciasNombres = 0`, y lo enseña con 1. Legacy dejaba ese 1 en el cuerpo de la
   petición: **cualquiera que supiera mandarlo veía la lista completa de quién llegó y a qué hora.** En
   el nuevo lo decide el permiso.
2. **`Funciones.ConvertFtmDate` + `formatoFechaDB`.** Es el único motivo por el que 21 endpoints
   llamaban al procedimiento que cierra el día: cuando la constante vale `"MM-dd-yyyy"`, Legacy hace
   `fecha.Substring(3,2) + "-" + Substring(0,2) + "-" + Substring(6,4)` sobre una cadena que **asume**
   en `dd-MM-yyyy`, y le pega `" 00:00:00"` / `" 23:59:59"`. No valida nada: con la fecha vacía lanza y
   el servicio contesta 400 con el stack trace completo. Ninguno de los 37 migrados lo necesita.
3. **"Si viene el ID se ignoran los demás filtros"** — y no filtrando, sino **reconstruyendo el DTO**.
   En estimaciones de obra eso era un bug con dientes: al recibir `Contrato` se tiraban `idContrato`,
   `idStatus` y `nombre`, y después el repositorio **ni mandaba** `Contrato` al SP (no existe ese
   parámetro). Buscar por nombre de contrato devolvía **todas** las estimaciones de todos los contratos.
   No se migró.
4. **Los porcentajes de descuento llegaban como cadena preconcatenada por el cliente.** El SP los parte
   por `~` y los aplica **en cascada, en el orden en que llegan**, así que el orden cambia el precio.
   Legacy pasaba la cadena del body tal cual: un error de formato se volvía un precio equivocado sin
   ningún error visible. El nuevo recibe una lista de números y arma la cadena con punto decimal
   invariante (con la cultura del servidor en español, `12.5` se habría escrito `"12,5"` y el SP habría
   leído dos descuentos).
5. **El `0` de `IDsMarca` vacía el catálogo.** `string.Join` sobre una lista con `[0]` daba `"0"`, y el
   SP de productos **no** tiene para marcas la guarda `<> '0'` que sí tiene para categorías: queda
   `IDMarca IN (0)` y devuelve cero renglones. El nuevo descarta el cero.
6. **`@ListaRoshfrans = 1` y `@EsCarga = 1` reinician el `WHERE` completo** dentro del SP, tirando el
   status, el id y las tres listas. Si llegan los dos, gana `esCarga` pero los cursores de descuento
   corren igual: se pedía la lista Roshfrans y se recibía la de carga con precios descontados, y nada
   avisaba. El nuevo contesta 400.
7. **Nombres de DTO desalineados con las columnas del SP** — campos que Legacy pedía y nunca recibía,
   porque el mapeo empareja por nombre exacto: `*Modifico` contra `*Modificacion` en facturación de
   cliente (quién cambió un RFC) y en centros de servicio; `UsuarioModifico` ausente del DTO de
   bonificaciones; `SumaAbonos` ausente del DTO de cortes (el total que la base calculaba y nadie veía);
   `DescripcionSat` y `EquipoModificacion` ausentes del DTO de productos.
8. **El valor mágico `-1` de las huellas** y el `IF` por origen: el SP emite **un result set por cada
   filtro que se cumple** y Legacy leía solo `Tables[0]`, así que pidiendo dos orígenes las huellas del
   segundo se perdían sin aviso; y con los tres en 0 no emite ninguno y Legacy contestaba 500. Además el
   **default de `@IDEmpleado` en el SP es `-1`**: omitirlo trae las huellas de toda la nómina.
9. **`ISNULL(..., '''')` que devuelve dos apóstrofos.** En bonificaciones de cliente, cuando no hay
   modificación el SP deja el literal `''` y Legacy lo pintaba tal cual en la pantalla.
10. **El `0` que no significa "todos"** en dos SP de corte CS: en cobros filtra
    `ISNULL(IDCobroDineroUsado,0) = 0` ("los que aún no están cobrados") y en ventas canceladas
    `venCan.IDCorte IS NULL`. Es al revés que en el resto del sistema, y está anotado en los dos
    endpoints.
11. **El apóstrofo.** Todos estos SP arman el `WHERE` concatenando y lo corren con `EXEC(@Sql)`: pasar el
    filtro como `SqlParameter` **no** protege, porque la inyección ocurre dentro. De los 37, Legacy solo
    lo saneaba en productos. El nuevo lo quita en todos, con prueba.
12. **Strings sin inicializar en los `InputDTO` de Legacy** → `AddWithValue(..., null)` →
    *"Parameter '@Marca' has no default value"*: un **500 por omitir un filtro opcional**. Pasaba en
    marcas, condiciones de crédito, series de empresa y los tres catálogos del SAT. El nuevo traduce el
    ausente a `""`, que es lo que estos SP entienden por "sin filtro" (comparan `IF @X <> ''`).

## Permisos nuevos

Catorce, todos en `PermissionCatalogSeeds` (`PermissionService.cs`). **Ninguno cae en las cuatro
familias que gobierna Mac31** (`ventas.*`, `reportes.*`, `empleados.*`, `prestamos.*`), así que lo que se
dé desde la pantalla de permisos del web sí se respeta.

| clave | para qué | por qué no se reusó otro |
|---|---|---|
| `obra.ver_modulo` | la tarjeta del módulo en el inicio | no existe en Mac31 |
| `obra.contratos.ver` | contratos, grupos, conceptos y estimaciones | son importes de obra pública |
| `obra.catalogos.ver` | los ocho catálogos de obra | desplegables del módulo |
| `obra.proveedores.ver` | proveedores de obra | el renglón trae RFC, CURP, teléfono y correo: es dato personal |
| `clientes.ver_modulo` | la tarjeta del módulo | — |
| `clientes.ver` | bonificaciones y centros de servicio | no `ventas.cliente.ver`: esa significa "ver a quién le vendo mientras capturo", no "ver su ficha" |
| `clientes.facturacion.ver` | datos fiscales | es dinero: dice qué RFC se le factura |
| `clientes.log.ver` | bitácora de cambios | es auditoría; quien atiende al cliente no la necesita y quien revisa sí |
| `productos.ver_modulo` | la tarjeta del módulo | — |
| `productos.ver` | catálogo con precios y existencias | no `catalogos.ver`: el renglón trae precio, precio anterior y la diferencia, o sea el margen |
| `cortes.ver_modulo` | la tarjeta del módulo | — |
| `cortes.ver` | corte y su detalle | — |
| `cortes.cs.ver` | cortes de centro de servicio | cruzan a las bases de los centros y los ve la matriz, no el centro |
| `aplicaciones.actualizaciones.ver` | manifiesto del arrancador | publica rutas del servidor: es de quien administra la instalación |

Y **dos endpoints no traen clave nueva a propósito**, porque inventarla no habría servido de nada:

* `GET /api/huellas` usa **`empleados.huellas`**, que ya existe y la **deriva el puente** del botón
  `btnHuellas` de la forma CONSULTAR EMPLEADOS de Mac31. Un `huellas.ver` nuevo de la familia
  `empleados.*` se habría borrado del snapshot en el siguiente recálculo, sin error y sin rastro (§ 2.8
  del resumen de arquitectura). Consecuencia asumida: las huellas de **clientes** quedan detrás del
  permiso de empleados, porque en Mac31 es un solo botón.
* `GET /api/asistencias/diaria` destapa los nombres con **`empleados.sueldo.ver`**, que sí está en la
  lista blanca del web (`EmpleadosWebPermissionSeeds`) y ya significa exactamente eso: "puede ver datos
  personales de la nómina".

Los tres `catalogos.*` que usa `CatalogosComplementosController` (`catalogos.ver`, `catalogos.fe.ver`,
`catalogos.marcas.ver`) **no se declaran aquí**: los siembra la ola 1. Dos filas con la misma clave
serían dos dueños de la misma llave.

Los permisos quedan **sembrados en código**, no en la base: entran a `WPermiso` cuando alguien llame a la
sincronización del catálogo desde la pantalla de permisos. No se corrió ningún script contra la base.

## Diferencias intencionales (las de fondo)

1. **POST → GET.** En Legacy eran POST porque su handler exigía el token dentro del JSON. Aquí el token
   va en la cabecera, así que una consulta vuelve a ser un GET.
2. **Fuera los `*Ftm`.** Los SP devuelven cada número y cada fecha **dos veces**: el valor y su texto ya
   formateado (`TotalConcepto` y `TotalConceptoFtm`). Eso lo armaba la base para las rejillas de
   WinForms. Un front que recibe el número lo formatea con la configuración regional del que mira la
   pantalla; uno que recibe el texto ya no puede. Se manda el valor.
   **Excepciones conservadas**, porque no son el formato de un número sino el dato: `direccion` de
   facturación (es el domicilio fiscal tal como se timbró), `folioFtm` de corte CS ("EFE-123", el folio
   como lo dice la gente), `apoderadoPuesto` (la cadena con la que se firma un contrato) y el `motivo` de
   las ventas canceladas.
3. **Fuera `@Formato`.** Es el interruptor "0 todos los campos / 1 Excel" de Mac31: con 1 el **mismo** SP
   devuelve otras columnas, con los encabezados en español y con espacios (`[Tipo de Gerente]`). El Excel
   lo arma el front con los mismos datos; dos formas de la misma respuesta son dos contratos que se
   desincronizan. En productos hay una razón extra: **`@Formato = 1` está roto en la base** (concatena un
   segundo `SELECT` después de la lista de columnas del primero, así que el `EXEC` falla).
4. **Fuera `result` y `mensaje` de cada renglón.** Eran el hueco de error de los SP de escritura; en una
   consulta salen 0/null siempre y el sobre ya dice si la llamada salió.
5. **Las fechas de un LEFT JOIN van anulables.** El DTO de Legacy declaraba `Fecha` como `DateTime` no
   anulable, así que un centro de servicio que el cliente nunca tuvo asignado salía con
   `0001-01-01T00:00:00`. El nuevo manda `null`. Un año 1 en una pantalla se lee como dato corrupto.
6. **Un DTO por forma, no por nombre de columna.** Los cuatro catálogos de obra con la forma
   (id, status, nombre, estatus) y los tres del SAT comparten un DTO cada grupo: lo único que cambiaba
   era el nombre de la columna (`IDTipoObra`/`TipoObra`) y la ruta ya dice de cuál se trata.
7. **Se declara el DTO donde Legacy devolvía `DataRow` crudos.** Catorce endpoints de este lote
   envolvían `GenericResponse(List<DataRow>)`: Newtonsoft no tiene converter para `DataRow`, así que lo
   que salía por el cable eran las propiedades del objeto (`itemArray`, `table`, `rowState`,
   `hasErrors`) y el cliente tenía que leer los valores **por posición**. En actualizaciones de
   aplicación eso costaba **1.1 MB para 63 renglones**, porque cada `DataRow` arrastra la tabla entera
   dentro de `Table`.
8. **`PrecioListaConIvaRnd` → `precioListaConIva`.** Ese "Rnd" es mentira: el SP multiplica por
   `1 + IVA/100` y ya. El nombre le diría al front que el número viene redondeado y que puede imprimirlo
   tal cual.
9. **No se migra el filtro `Contrato` de las estimaciones de obra.** Un filtro que **amplía** el
   resultado es peor que no tenerlo (ver lógica escondida 3).
10. **El `0` no viaja en las listas de ids.** Ver lógica escondida 5.
11. **Un origen por llamada en huellas**, y el `-1` no viaja por HTTP: el cliente dice `todas=true`. Si
    el `-1` viajara, un `id=-1` por error de un front se volvería "dame todas las huellas de la empresa"
    sin que nadie lo hubiera pedido.
12. **`sumaAbonos` va una vez, fuera de los renglones**, en ventas canceladas de corte.
13. **Se exige `idCliente`, `idCorte` o `idUsuario` donde el SP devolvía algo inútil o reventaba** — 400
    en vez de una lista vacía que se lee como "no hay nada", o en vez de un `EXEC(NULL)` de SQL.
14. **No se expone ningún filtro que el SP reciba y no use**: `@DiasCredito` en condiciones de crédito,
    `@IDStatus` en los tres catálogos del SAT, `@Identico` en tipos de domicilio y tipos de documento,
    `@IDCorte` en la siguiente fecha de corte, `@IDUsuario` en el siguiente número de usuario, `@Fecha`
    en la asistencia del día. Ofrecerlos sería mentir sobre lo que hace la base.
15. **Fuera `ArchivoFtm`** de documentos de proveedor: su `CASE` solo compara contra cadena vacía, así
    que con el archivo en `NULL` contesta `'SI'` y la pantalla ofrece descargar algo que no existe.
16. **Fuera `MesAno` y `UltimaModificacion`** de productos: el primero es el encabezado de la lista de
    precios impresa, calculado con `GETDATE()` **dentro de la base** (sería la fecha del servidor de
    base, no la del usuario); el segundo es `usuario - fecha - equipo` en una sola cadena, y las tres
    partes ya viajan por separado.
17. **Fuera las columnas que el DTO de Legacy declaraba y el SP nunca manda** (`IDGrupoCategoria`,
    `Categoria`, `Marcado` en marcas): publicar un campo que nunca trae dato es mentir sobre el contrato.

## Lo que el backend nuevo arregla

No son diferencias de contrato, son defectos que no se heredaron:

* **El apóstrofo, en 36 de los 37.** Legacy solo lo saneaba en productos (ver lógica escondida 11).
* **Los 500 por omitir un filtro opcional** en marcas, condiciones de crédito, series de empresa y los
  tres catálogos del SAT (ver lógica escondida 12).
* **Los campos de auditoría que nunca llegaban**: quién cambió un RFC, quién cambió una bonificación,
  desde qué máquina se cambió un precio, qué se timbra (`descripcionSat`) y el total de abonos de un
  corte (ver lógica escondida 7).
* **Las huellas del segundo origen** que se perdían, y el 500 cuando no se mandaba ninguno.
* **La lista de marcas vaciada por un `0`.**
* **El precio equivocado por un descuento mal delimitado** desde el cliente.
* **El `IDFacturama` que reventaba la consulta completa**: la columna es `varchar` y trae un token;
  declararla `int` hacía fallar **todo** el catálogo de series de empresa y **todo** el de productos con
  *"The input string '' was not in a correct format"*.
* **Los nombres de la asistencia del día**, que cualquiera destapaba con un campo en el body.

## Pendientes que quedan anotados

* **`registro.tsv` no se tocó.** Las 37 filas siguen en `estado = falta`/`parcial`. Varios agentes
  escriben ese archivo a la vez y reescribirlo entero podría pisar sus filas. Hay que pasarlas a `hecho`,
  con `ubicacion_nueva` = la ruta nueva de la tabla de arriba, `metodo_validacion` = `comparador`, y
  `resultado` = `OK` o `no verificable`.
* **Los 2 casos de `casos/no-verificables/`** hay que correrlos contra una base con el módulo de obra
  instalado (`Constantes.EsAlcaldiaMC = 1`) y contra un servidor que tenga las bases de los centros de
  servicio.
* **Los 21 endpoints recuperables** de la tabla de caídos: convertirlos en lecturas de verdad leyendo
  `Constantes.FormatoFechaDB` con un `SELECT TOP 1` en vez de llamar a `sp_n_ConsultaConstantes`. Es una
  entrega chica y de valor alto, pero cambia el contrato y hay que decidirlo.
* **Varios casos del comparador apuntan a `result.tablaLst`** (y no a `result.tablaLst.0.Table`) porque
  su tabla está vacía en Tauro: residentes de obra y grupos de contrato. Cuando esas tablas tengan datos
  hay que cambiarles el `camino_legacy`; cada caso lo dice en su `_porque`.
* **Si alguna pantalla necesita ESCRIBIR** en algo de estos módulos, no se agrega aquí: se crea su
  endpoint con su `sp_w_`, su permiso `.crear`/`.editar` y su firma de usuario del token. Los nueve
  controladores son de solo lectura y así está documentado adentro.
