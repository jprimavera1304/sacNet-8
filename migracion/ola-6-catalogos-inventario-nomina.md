# Ola 6 — Las escrituras de catálogos, inventario/compras, nómina y los sueltos

Lote: las 146 filas de `registro.tsv` en `falta`/`parcial`/`por-verificar` que no son de
ventas, obra ni aviación. Altas, modificaciones y bajas de los catálogos; productos,
proveedores, almacenes, cascos, compras, ajustes de inventario; empleados, préstamos,
periodos, asistencias, recibos; usuarios y permisos.

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5085`.
Fecha de operación al empezar y al terminar: `2026-10-06` (no se movió).

## Lo que se hizo, en una frase

Siete módulos nuevos con **~150 endpoints de escritura** que llaman a los **mismos
`sp_n_`** que llamaba MacServicios2, con los mismos parámetros, dentro de la arquitectura
del backend nuevo. Ningún `sp_n_` se tocó; nada de la base se modificó para que una prueba
pasara.

| archivo | qué |
|---|---|
| `Infrastructure/Repositories/BajaLogicaLegacy.cs` | **la pieza central**: la baja lógica que lee el renglón antes de escribirlo |
| `Application/DTOs/CatalogosAdmin/`, `Interfaces/ICatalogosAdmin*`, `Services/CatalogosAdminService.cs`, `Repositories/CatalogosAdminRepository{,.Simples,.Estructurados}.cs`, `Controllers/CatalogosAdminController{,.Estructurados}.cs` | los 21 catálogos de Mac31 |
| `DTOs/Maestros/`, `IMaestros*`, `MaestrosService.cs`, `MaestrosRepository{,.Cliente,.ProductoProveedor}.cs`, `MaestrosControllerBase.cs`, `Controllers/{Clientes,Productos,Proveedores}AdminController.cs` | cliente, producto y proveedor |
| `DTOs/OperacionMac/`, `IOperacionMac*`, `OperacionMacService.cs`, `OperacionMacRepository{,.Operaciones}.cs`, `CorteInfoLegacy.cs`, `Controllers/{Compras,CortesAdmin,BancosTransferencias,CambiosVigencia}Controller.cs` | compras, inventario en tránsito, cortes, transferencias, cambios de vigencia |
| `DTOs/SeguridadMac/`, `ISeguridadMac*`, `SeguridadMacService.cs`, `SeguridadMacRepository.cs`, `Controllers/{UsuariosMac,ConfiguracionMac}Controller.cs` | usuarios de Mac31, huellas, constantes, parámetros |
| `DTOs/Nomina/`, `INominaBatch*`, `NominaBatchService.cs`, `NominaBatchRepository.cs`, `Controllers/NominaBatchController.cs` | los tres batches de nómina |
| `DTOs/Sueltos/`, `ISueltos*`, `SueltosService.cs`, `SueltosRepository.cs`, `Controllers/SueltosController.cs` | proyectos, recordatorios, servicios, sincronización |
| `Utils/Funciones.cs` | **un defecto arreglado**: `IDAlgo` salía al JSON como `205Algo` |
| `Program.cs` | 14 `AddScoped` |
| `Infrastructure/Security/PermissionService.cs` | 61 permisos nuevos en `PermissionCatalogSeeds` |
| `ISL_Service.Tests/Ola6EscriturasTests.cs` | 18 casos de la lógica pura |

## Lo primero, porque vale más que la tabla: la baja dejaba de ser lógica

Los `sp_n_Actualizar*` de legacy hacen un UPDATE que pisa **todas** las columnas, no solo
el estatus. El `DELETE` de MacServicios2 llamaba a ese mismo procedimiento con lo que
trajera el cuerpo, y sus controladores **solo exigían el id**. Consecuencia, comprobada
leyendo los UPDATE de 18 procedimientos y confirmada contra la base:

* dar de baja un **cliente** lo dejaba sin nombre, sin teléfonos y sin límite de crédito,
  con las ventas viejas apuntando a un renglón vaciado;
* dar de baja un **producto** lo dejaba con precio 0, stock 0 y sin categoría;
* dar de baja un **usuario de Mac31** le dejaba la contraseña **vacía** — y si alguien lo
  reactivaba, entraba sin contraseña;
* en **agentes** no daba de baja *nada*: el procedimiento se devuelve antes por su propio
  chequeo de número repetido contra el agente 0, así que el botón no hacía nada.

Aquí la baja es `POST {id}/inhabilitar` **sin cuerpo**: el backend **lee** las columnas que
el UPDATE va a pisar y se las devuelve al procedimiento junto con `IDStatus = 2`. Lo único
que cambia es el estatus, y está probado antes/después para catálogos, cliente, producto,
proveedor y usuario. Y hay `habilitar`, porque una baja sin vuelta es un borrado con otro
nombre.

Esa lógica vive en un solo sitio (`BajaLogicaLegacy`) para arreglarla una vez.

## La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Agentes - alta | `POST /api/catagentes` | `POST /api/catalogos/agentes` | `catalogos.crear` | **OK** | numero y nombre; el numero repetido lo rechaza la base |
| Agentes - baja | `DELETE /api/catagentes` | `POST /api/catalogos/agentes/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar`. En Legacy el DELETE de agentes no daba de baja NADA (el SP se devuelve por el chequeo de numero repetido contra el agente 0) |
| Agentes - modificacion | `PUT /api/catagentes` | `PUT /api/catalogos/agentes/{id}` | `catalogos.editar` | **OK** | numero y nombre; el numero repetido lo rechaza la base |
| Almacenes - alta | `POST /api/catalmacenes` | `POST /api/catalogos/almacenes` | `catalogos.crear` | **OK** |  |
| Almacenes - baja | `DELETE /api/catalmacenes` | `POST /api/catalogos/almacenes/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Almacenes - modificacion | `PUT /api/catalmacenes` | `PUT /api/catalogos/almacenes/{id}` | `catalogos.editar` | **OK** |  |
| Asistencias - borrar dia | `DELETE /api/empleadosperiodoasistencias` | `DELETE /api/asistencias/dia` | `asistencias.editar` | **OK** | ya estaba migrado (sp_w_EliminarAsistenciaDia) |
| Asistencias - guardar dia | `POST /api/empleadosperiodoasistencias` | `PUT /api/asistencias/dia` | `asistencias.editar` | **OK** | ya estaba migrado (sp_w_GuardarAsistenciaDia) |
| Autos - alta | `POST /api/catautos` | `POST /api/catalogos/autos` | `catalogos.autos.crear` | **OK** | cuatro fechas (verificacion y vigencia del seguro) como fecha, no como cadena |
| Autos - modificacion | `PUT /api/catautos` | `PUT /api/catalogos/autos/{id}` | `catalogos.autos.editar` | **OK** | con `tieneSeguro` la vigencia es obligatoria |
| Bancos - alta | `POST /api/catbancos` | `POST /api/catalogos/bancos` | `catalogos.crear` | **OK** |  |
| Bancos - baja | `DELETE /api/catbancos` | `POST /api/catalogos/bancos/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Bancos - modificacion | `PUT /api/catbancos` | `PUT /api/catalogos/bancos/{id}` | `catalogos.editar` | **OK** |  |
| Bitacora de contrasenas de autorizacion | `POST /api/catformasprocesos/contrasena` | `POST /api/contrasenas-capturadas` | `seguridad.bitacora.registrar` | **OK** | devuelve el id, que Legacy leia y tiraba |
| Cambios de vigencia - alta | `POST /api/cambiosvigencia` | `POST /api/cambios-vigencia` | `cambiosvigencia.crear` | **OK** | los productos como lista de objetos; NumProductos lo cuenta el backend |
| Cambios de vigencia - cancelacion | `PUT /api/cambiosvigencia/cancelar` | `POST /api/cambios-vigencia/{id}/cancelar` | `cambiosvigencia.cancelar` | **OK** | no se pide motivo: el SP no lo guarda (Legacy lo recibia y lo tiraba). Se deja de llamar a sp_n_ConsultaConstantes para nada |
| Cambios de vigencia - consulta | `POST /api/cambiosvigencia/consultar` | `GET /api/cambios-vigencia` | `cambiosvigencia.ver` | **OK** | con id se ignoran los demas filtros |
| Cambios de vigencia - modificacion | `PUT /api/cambiosvigencia` | `PUT /api/cambios-vigencia/{id}` | `cambiosvigencia.editar` | **OK** | un producto por llamada: asi lo recibe el SP |
| Cambios de vigencia - regresar | `PUT /api/cambiosvigencia/regresar` | `POST /api/cambios-vigencia/{id}/regresar` | `cambiosvigencia.editar` | **OK** | igual que cancelar |
| Catalogo formas-procesos - alta | `POST /api/catformasprocesos` | — no se migra | — | no migrado | no existe la capacidad: el POST de Legacy llama a **sp_n_InsertarDiaFestivo** mandandole solo usuario y equipo, o sea que inserta un dia festivo EN BLANCO, y el IDForma no se manda a ninguna parte. No hay ningun sp_n_*FormaProceso en la base |
| Catalogo formas-procesos - modificacion | `PUT /api/catformasprocesos` | — no se migra | — | no migrado | mismo caso: llama a **sp_n_ActualizarDiaFestivo** y le pasa el MISMO IDForma en @IDDiaFestivo, @IDStatus y @DiaFestivo a la vez. Lo que si existe (asignar un proceso a un usuario) esta migrado arriba |
| Categorias de producto de Mac31 - alta | `POST /api/categorias` | `POST /api/catalogos/categorias-mac` | `catalogos.marcas.crear` | **OK** | la columna se llama `Tipo`, no IDGrupoCategoria; la ruta es categorias-mac porque api/categorias es de torneos (Guid) |
| Categorias de producto de Mac31 - baja | `DELETE /api/categorias` | `POST /api/catalogos/categorias-mac/{id}/inhabilitar` | `catalogos.marcas.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Categorias de producto de Mac31 - modificacion | `PUT /api/categorias` | `PUT /api/catalogos/categorias-mac/{id}` | `catalogos.marcas.editar` | **OK** | la columna se llama `Tipo`, no IDGrupoCategoria; la ruta es categorias-mac porque api/categorias es de torneos (Guid) |
| Centros de servicio - alta | `POST /api/catcentroservicio` | `POST /api/catalogos/centros-servicio` | `catalogos.territorial.crear` | no verificable | el SP no esta en Produccion_svr |
| Centros de servicio - baja | `DELETE /api/catcentroservicio` | `PUT /api/catalogos/centros-servicio/{id} con idStatus=2` | `catalogos.territorial.editar` | no verificable | su SP de modificacion ya recibe @IDStatus; el SP no esta aqui |
| Centros de servicio - modificacion | `PUT /api/catcentroservicio` | `PUT /api/catalogos/centros-servicio/{id}` | `catalogos.territorial.editar` | no verificable | el SP no esta aqui |
| Ciudades - alta | `POST /api/catciudades` | `POST /api/catalogos/ciudades` | `catalogos.territorial.crear` | **OK** | la clave del estado es TEXTO de dos posiciones |
| Ciudades - baja | `DELETE /api/catciudades` | — no se expone | — | no migrado | [Catalogo Ciudades] NO tiene IDStatus: el DELETE de Legacy solo vaciaba el nombre |
| Ciudades - modificacion | `PUT /api/catciudades` | `PUT /api/catalogos/ciudades/{idCiudad}` | `catalogos.territorial.editar` | **OK** | el id va como texto: IDCiudad es varchar |
| Cliente - alta de domicilio fiscal | `POST /api/clientes/facturacion` | `POST /api/clientes/facturacion` | `clientes.facturacion.editar` | **OK** |  |
| Cliente - bonificaciones de usados | `PUT /api/clientes/bonificacion` | `PUT /api/clientes/{id}/bonificaciones` | `clientes.bonificaciones.editar` | **OK** | lista de PARES en vez de dos cadenas '~' apareadas por posicion |
| Cliente - centros permitidos | `PUT /api/clientes/centroservicio` | `PUT /api/clientes/{id}/centros-servicio` | `clientes.centros.editar` | **OK** | lista de pares, igual que bonificaciones |
| Cliente - facturacion de mostrador | `PUT /api/clientes/facturacion/mostrador` | `PUT /api/clientes/{id}/facturacion-mostrador` | `clientes.facturacion.editar` | **OK** | el SP recibe un solo parametro y no guarda quien lo hizo |
| Cliente - modificacion de domicilio fiscal | `PUT /api/clientes/facturacion` | `PUT /api/clientes/facturacion/{id}` | `clientes.facturacion.editar` | **OK** | `idStatus` arranca en 1: en Legacy un PUT sin el APAGABA la facturacion. El SP concatena el UPDATE, asi que aqui si se escapa el apostrofo (duplicandolo) |
| Clientes - alta | `POST /api/clientes` | `POST /api/clientes` | `clientes.crear` | **OK** | el tipo 0 ya no pasa: en Legacy no validaba nada y reventaba con NullReference. El limite de credito lo pisa el SP en Tauro |
| Clientes - baja | `DELETE /api/clientes` | `POST /api/clientes/{id}/inhabilitar` | `clientes.estado.editar` | **OK** | conserva nombre, telefonos y limite de credito; comprobado antes/despues |
| Clientes - modificacion | `PUT /api/clientes` | `PUT /api/clientes/{id}` | `clientes.editar` | **OK** | ficha completa: el UPDATE pisa todas las columnas |
| Colonias - alta | `POST /api/catcolonias` | `POST /api/catalogos/colonias` | `catalogos.territorial.crear` | **OK** |  |
| Colonias - baja | `DELETE /api/catcolonias` | — no se expone | — | no migrado | sin IDStatus, igual que ciudades |
| Colonias - modificacion | `PUT /api/catcolonias` | `PUT /api/catalogos/colonias/{id}` | `catalogos.territorial.editar` | **OK** |  |
| Compra - marcar como factura | `PUT /api/consultacompras` | `PUT /api/compras/{id}/es-factura` | `compras.editar` | **OK** | es lo unico que el SP cambia; en Legacy un PUT sin el campo la marcaba como 'no factura' |
| Compras - cancelacion | `POST /api/comprascancelacion` | `POST /api/compras/{id}/cancelar` | `compras.cancelar` | **OK** | el motivo es obligatorio; una respuesta sin renglones ya no se reporta como exito |
| Compras - consulta | `POST /api/consultacompras/consultar` | `GET /api/compras` | `compras.ver` | **OK** | con `factura` se ignoran los demas filtros, y ahora solo si trae texto: en Legacy el campo sin valor por omision metia a esa rama a TODAS las consultas |
| Condiciones de credito - alta | `POST /api/CatCondicionesCredito` | `POST /api/catalogos/condiciones-credito` | `catalogos.crear` | **OK** |  |
| Condiciones de credito - baja | `DELETE /api/CatCondicionesCredito` | `POST /api/catalogos/condiciones-credito/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Condiciones de credito - modificacion | `PUT /api/CatCondicionesCredito` | `PUT /api/catalogos/condiciones-credito/{id}` | `catalogos.editar` | **OK** |  |
| Constantes de empresa | `PUT /api/constantes` | `PUT /api/constantes` | `constantes.editar` | **OK** | los cuatro valores son obligatorios: en Legacy un PUT vacio escribia CERO en los cuatro |
| Cortes - alta | `POST /api/cortes` | `POST /api/cortes` | `cortes.crear` | **OK** | el desglose son 20 campos CON NOMBRE y los gastos una lista de renglones; y por fin se lee el result (era el unico insert que lo tiraba) |
| Cortes - cancelacion | `POST /api/cortes/cancelar` | `POST /api/cortes/{id}/cancelar` | `cortes.cancelar` | **OK** | una sola ruta para los dos tipos: Legacy tenia dos que llamaban al MISMO sp_n_CancelarCorte |
| Cortes - consulta | `POST /api/cortes/consultar` | `GET /api/cortes` | `cortes.ver` | **OK** | con id se ignora el rango |
| Cortes - gastos por tipo | `POST /api/cortes/consultar/tipogastos` | `GET /api/cortes/tipo-gastos` | `cortes.ver` | **OK** | las dos fechas son obligatorias: en Legacy sin ellas reventaba con un error de posiciones |
| Cortes CS - alta | `POST /api/cortescs` | `POST /api/cortes/centros-servicio` | `cortes.cs.crear` | **OK** | solo viaja la fecha final: Legacy convertia una inicial que descartaba |
| Cortes CS - cancelacion | `POST /api/cortescs/cancelar` | `POST /api/cortes/{id}/cancelar` | `cortes.cancelar` | **OK** | la misma ruta: en la base es el mismo SP |
| Cortes CS - consulta | `POST /api/cortescs/consultar` | `GET /api/cortes/centros-servicio` | `cortes.cs.ver` | **OK** | con folio se ignoran id, tipo y fechas |
| Dias festivos - alta | `POST /api/catdiafestivo` | `POST /api/catalogos/dias-festivos` | `catalogos.crear` | **OK** | la fecha viaja como fecha |
| Dias festivos - consulta | `POST /api/catdiafestivo/consultar` | `GET /api/catalogos/dias-festivos` | `catalogos.ver` | **OK** | POST -> GET: el token ya no viaja en el cuerpo |
| Dias festivos - modificacion | `PUT /api/catdiafestivo` | `PUT /api/catalogos/dias-festivos/{id}` | `catalogos.editar` | **OK** | si la fecha nueva no existe, el SP da de BAJA el renglon e INSERTA otro: el id cambia |
| Documentos - alta | `POST /api/catdocumento` | `POST /api/catalogos/documentos` | `catalogos.crear` | **OK** |  |
| Documentos - baja | `DELETE /api/catdocumento` | `POST /api/catalogos/documentos/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Documentos - modificacion | `PUT /api/catdocumento` | `PUT /api/catalogos/documentos/{id}` | `catalogos.editar` | **OK** |  |
| Empleados - alta | `POST /api/empleados` | `POST /api/empleados` | `empleados.crear` | **OK** | ya estaba migrado (EmpleadosController + sp_w_InsertarEmpleado); se verifico, no se duplico |
| Empleados - modificacion | `PUT /api/empleados` | `PUT /api/empleados/{id}` | `empleados.editar` | **OK** | ya estaba migrado |
| Empresas de Mac31 - alta | `POST /api/empresas` | `POST /api/catalogos/empresas` | `catalogos.empresas.crear` | **OK** | es dbo.Empresas (la que factura), no EmpresaWeb; `[Calle y Numero]` lo arma el SP |
| Empresas de Mac31 - baja | `DELETE /api/empresas` | `POST /api/catalogos/empresas/{id}/inhabilitar` | `catalogos.empresas.baja` | **OK** | conserva RFC, regimen y cuenta bancaria |
| Empresas de Mac31 - modificacion | `PUT /api/empresas` | `PUT /api/catalogos/empresas/{id}` | `catalogos.empresas.editar` | **OK** | `esCentroServicio` solo existe en el alta |
| Fecha de operacion de pagos | `PUT /api/constantes/actualizar` | `PUT /api/constantes/fecha-operacion-pagos` | `constantes.fechapagos.editar` | **OK** | no es la fecha de operacion del negocio, que la rueda el cierre del dia |
| Galleros - alta | `POST /api/catgalleros` | `POST /api/catalogos/galleros` | `catalogos.galleros.crear` | **OK** | trae SALDO; el UPDATE del SP tiene `Saldo = @Saldo` COMENTADO, asi que la modificacion no lo cambia |
| Galleros - modificacion | `PUT /api/catgalleros` | `PUT /api/catalogos/galleros/{id}` | `catalogos.galleros.editar` | **OK** | trae SALDO; el UPDATE del SP tiene `Saldo = @Saldo` COMENTADO, asi que la modificacion no lo cambia |
| Grupos de categoria - alta | `POST /api/catgruposcategorias` | `POST /api/catalogos/grupos-categoria` | `catalogos.marcas.crear` | **OK** |  |
| Grupos de categoria - baja | `DELETE /api/catgruposcategorias` | `POST /api/catalogos/grupos-categoria/{id}/inhabilitar` | `catalogos.marcas.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Grupos de categoria - modificacion | `PUT /api/catgruposcategorias` | `PUT /api/catalogos/grupos-categoria/{id}` | `catalogos.marcas.editar` | **OK** |  |
| Huellas - registrar/actualizar | `POST /api/huellas` | `PUT /api/huellas` | `huellas.editar` | **OK** | varbinary(MAX) EXPLICITO (sin acotar, ADO puede inferir 8000 y recortar la plantilla), acepta una sola plantilla, y devuelve el result real: el repo de Legacy nunca lo asignaba |
| Inventario en transito - procesar | `POST /api/almacenproductos/transito` | `POST /api/inventario/transito` | `inventario.transito.procesar` | **OK** | el cliente es obligatorio: el movimiento se le carga a alguien |
| Latido de actividad del usuario | `PUT /api/constantes/actividad` | `PUT /api/constantes/actividad` | (solo sesion valida) | **OK** | el id es el del token: en Legacy se podia mandar actividad a nombre de otro |
| Marcas - alta | `POST /api/marcas` | `POST /api/catalogos/marcas` | `catalogos.marcas.crear` | **OK** |  |
| Marcas - baja | `DELETE /api/marcas` | `POST /api/catalogos/marcas/{id}/inhabilitar` | `catalogos.marcas.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Marcas - modificacion | `PUT /api/marcas` | `PUT /api/catalogos/marcas/{id}` | `catalogos.marcas.editar` | **OK** |  |
| Municipios - alta | `POST /api/catmunicipios` | `POST /api/catalogos/municipios` | `catalogos.territorial.crear` | **OK** |  |
| Municipios - baja | `DELETE /api/catmunicipios` | — no se expone | — | no migrado | sin IDStatus, igual que ciudades |
| Municipios - modificacion | `PUT /api/catmunicipios` | `PUT /api/catalogos/municipios/{idMunicipio}` | `catalogos.territorial.editar` | **OK** |  |
| Nomina - calcular recibos del periodo | `POST /api/empleadostiposueldorecibos/consultar` | `POST /api/nomina/recibos/calcular` | `nomina.recibos.calcular` | parcial | **no consultaba: CALCULA**. El SP borra los recibos del periodo y los reinserta con un cursor. Migrado el calculo y los datos; el ARMADO DEL PAPEL (plantillas 1020, 4 recibos por hoja en Tauro y 1 en Zaragoza) es un reporte y le toca a la ola de reportes. Se ejecuta UNA vez, no dos, y se deja de calcular el periodo de comision como 'el de nomina mas uno' |
| Nomina - descontar prestamos | `POST /api/empleadosprestamo/validar` | `POST /api/nomina/prestamos/descontar` | `nomina.prestamos.descontar` | **OK** | **no validaba: es el batch que descuenta de la nomina**. El id del usuario sale del token, no del cuerpo |
| Nomina - generar periodos | `POST /api/periodostiposueldodias/validar` | `POST /api/nomina/periodos/generar` | `nomina.periodos.generar` | **OK** | **no validaba: CREA los periodos**. En Mac31 lo disparaba el login de cualquiera |
| Nomina - recibos de un periodo | `POST /api/empleadostiposueldorecibos/nomina/consultar` | `POST /api/nomina/recibos/calcular con idPeriodoTipoSueldo` | `nomina.recibos.calcular` | **OK** | es el MISMO SP que el anterior con otro periodo: una sola ruta en vez de dos |
| Parametros de reporte - consulta | `POST /api/parametros/consultar` | `GET /api/parametros/{id}` | `reportes.parametros.ver` | **OK** | el SP solo filtra por id; Legacy exigia usuario y equipo y no los mandaba |
| Parametros de reporte - guardar | `PUT /api/parametros` | `PUT /api/parametros` | `reportes.parametros.editar` | **OK** | los nulos pasan (en Legacy tiraban la llamada); el equipo sale del token |
| Prestamos - alta | `POST /api/empleadosprestamo` | `POST /api/prestamos` | `prestamos.crear` | **OK** | ya estaba migrado |
| Prestamos - cambiar descuento | `PUT /api/empleadosprestamo` | `PUT /api/prestamos/{id}/monto-pagos` | `prestamos.editar` | **OK** | ya estaba migrado |
| Prestamos - cancelar | `PUT /api/empleadosprestamo/cancelar` | `POST /api/prestamos/{id}/cancelar` | `prestamos.cancelar` | **OK** | ya estaba migrado |
| Producto - adjuntar archivo | `PUT /api/productos/archivo` | `PUT /api/productos/{id}/archivo` | `productos.editar` | **OK** | el precio se LEE del renglon: el SP lo exige y en Legacy subir una foto lo ponia en cero |
| Productos - alta | `POST /api/productos` | `POST /api/productos` | `productos.crear` | **OK** | sin codigo de barras se usa el codigo; se deja de llamar a sp_n_ConsultaConstantes para leer un IVA que no se usaba |
| Productos - baja | `DELETE /api/productos` | `POST /api/productos/{id}/inhabilitar` | `productos.estado.editar` | **OK** | conserva precio, stock y categoria; comprobado |
| Productos - modificacion | `PUT /api/productos` | `PUT /api/productos/{id}` | `productos.editar` | **OK** | ficha completa, stock y tres precios incluidos |
| Proveedor - alta de apoderado | `POST /api/proveedores/apoderados` | `POST /api/proveedores/apoderados` | `proveedores.editar` | **OK** | EsRepresentanteLegal/Tecnico no se exponen: ningun SP de escritura los recibe |
| Proveedor - alta de documento | `POST /api/proveedores/documentos` | `POST /api/proveedores/documentos` | `proveedores.editar` | **OK** | se mandan los SIETE parametros que el SP tiene: Legacy le manda @IDStatus y @Escaneado, que NO existen, asi que su alta revienta en esta base |
| Proveedor - alta de domicilio | `POST /api/proveedores/domicilios` | `POST /api/proveedores/domicilios` | `proveedores.editar` | **OK** |  |
| Proveedor - modificacion de apoderado | `PUT /api/proveedores/apoderados` | `PUT /api/proveedores/apoderados/{id}` | `proveedores.editar` | **OK** | el id ya es obligatorio: en Legacy un PUT sin el actualizaba el renglon 0 |
| Proveedor - modificacion de documento | `PUT /api/proveedores/documentos` | `PUT /api/proveedores/documentos/{id}` | `proveedores.editar` | **OK** | Legacy manda un @Escaneado que el SP no recibe |
| Proveedor - modificacion de domicilio | `PUT /api/proveedores/domicilios` | `PUT /api/proveedores/domicilios/{id}` | `proveedores.editar` | **OK** |  |
| Proveedores - alta | `POST /api/proveedores` | `POST /api/proveedores` | `proveedores.crear` | **OK** |  |
| Proveedores - baja | `DELETE /api/proveedores` | `POST /api/proveedores/{id}/inhabilitar` | `proveedores.estado.editar` | **OK** | conserva nombre, RFC y telefono; comprobado |
| Proveedores - modificacion | `PUT /api/proveedores` | `PUT /api/proveedores/{id}` | `proveedores.editar` | **OK** | el SP no recibe IDDescuento, IDFacturama, IDUsuario ni Equipo: no se piden |
| Proyectos - alta | `POST /api/proyectos` | `POST /api/proyectos` | `proyectos.crear` | no verificable | el SP no esta aqui. La clave es obligatoria: en Legacy TODAS sus validaciones estaban comentadas menos usuario y equipo |
| Proyectos - consulta | `POST /api/proyectos/consultar` | `GET /api/proyectos` | `proyectos.ver` | no verificable | el SP no esta en Produccion_svr |
| Proyectos - importes de factura | `PUT /api/proyectos/facturas` | `PUT /api/proyectos/{id}/facturas` | `proyectos.editar` | no verificable | el SP no esta aqui. Solo se escriben desde esta ruta: antes tambien viajaban en el PUT del proyecto |
| Proyectos - modificacion | `PUT /api/proyectos` | `PUT /api/proyectos/{id}` | `proyectos.editar` | no verificable | el SP no esta aqui. Los campos *Docto no se piden: ningun SP de escritura los recibe |
| Recordatorios - alta | `POST /api/recordatorios` | `POST /api/recordatorios` | `recordatorios.crear` | **OK** | la fecha limite es obligatoria: en Legacy sin ella reventaba |
| Recordatorios - consulta | `POST /api/recordatorios/consultar` | `GET /api/recordatorios` | `recordatorios.ver` | **OK** |  |
| Recordatorios - modificacion | `PUT /api/recordatorios` | `PUT /api/recordatorios/{id}` | `recordatorios.editar` | **OK** |  |
| Relacion categoria - marca | `POST /api/catgruposcategorias/marcas/relacionar` | `PUT /api/catalogos/categorias-mac/marcas` | `catalogos.marcas.editar` | **OK** | interruptor idempotente; es el unico borrado fisico (la tabla solo tiene las dos llaves) |
| Repartidores - alta | `POST /api/catrepartidores` | `POST /api/catalogos/repartidores` | `catalogos.crear` | **OK** |  |
| Repartidores - baja | `DELETE /api/catrepartidores` | `POST /api/catalogos/repartidores/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Repartidores - modificacion | `PUT /api/catrepartidores` | `PUT /api/catalogos/repartidores/{id}` | `catalogos.editar` | **OK** |  |
| Series de folios - alta | `POST /api/empresas/series` | `POST /api/catalogos/empresas-series` | `catalogos.empresas.crear` | **OK** |  |
| Series de folios - baja | `DELETE /api/empresas/series` | `POST /api/catalogos/empresas-series/{id}/inhabilitar` | `catalogos.empresas.baja` | **OK** | conserva el folio |
| Series de folios - modificacion | `PUT /api/empresas/series` | `PUT /api/catalogos/empresas-series/{id}` | `catalogos.empresas.editar` | **OK** |  |
| Servicios - alta | `POST /api/servicios` | `POST /api/servicios` | `servicios.crear` | no verificable | el SP no esta aqui. La fecha ya se formatea: Legacy era el unico endpoint que mandaba la cadena cruda |
| Servicios - cancelacion | `PUT /api/servicios` | `POST /api/servicios/{id}/cancelar` | `servicios.cancelar` | no verificable | el SP no esta aqui y **en Legacy esta roto**: lo ejecuta SIN UN SOLO PARAMETRO (sus tres AddWithValue estan comentados, con nombres de otro modulo) y sin validar nada. Se reconstruye la llamada con id, usuario y equipo |
| Servicios - consulta | `POST /api/servicios/consultar` | `GET /api/servicios` | `servicios.ver` | no verificable | el SP no esta en Produccion_svr. Dos de sus parametros estan mal escritos en Legacy (@echaRecepcionFinal, @echaRegistroInicial) y se reproducen tal cual |
| Sincronizacion - cola de cajas | `POST /api/sincronizacion/consultar` | `GET /api/sincronizacion` | `sincronizacion.ver` | no verificable | el SP no esta aqui. Se quita la bandera con la que el CUERPO elegia la base de datos contra la que se ejecutaba |
| Tipos de domicilio - alta | `POST /api/tiposdomicilio` | `POST /api/catalogos/tipos-domicilio` | `catalogos.crear` | **OK** | el SP devuelve el id en la columna `result`, y revienta leyendo `MC_TiposDomicilio`, tabla que no existe aqui: se recupera el id de la tabla buena |
| Tipos de domicilio - modificacion | `PUT /api/tiposdomicilio` | `PUT /api/catalogos/tipos-domicilio/{id}` | `catalogos.editar` | **OK** | el SP devuelve el id en la columna `result`, y revienta leyendo `MC_TiposDomicilio`, tabla que no existe aqui: se recupera el id de la tabla buena |
| Tipos de gasto - alta | `POST /api/cattipogasto` | `POST /api/catalogos/tipos-gasto` | `catalogos.crear` | **OK** |  |
| Tipos de gasto - baja | `DELETE /api/cattipogasto` | `POST /api/catalogos/tipos-gasto/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Tipos de gasto - modificacion | `PUT /api/cattipogasto` | `PUT /api/catalogos/tipos-gasto/{id}` | `catalogos.editar` | **OK** |  |
| Tipos de recordatorio - alta | `POST /api/cattiporecordatorio` | `POST /api/catalogos/tipos-recordatorio` | `catalogos.crear` | **OK** |  |
| Tipos de recordatorio - baja | `DELETE /api/cattiporecordatorio` | `POST /api/catalogos/tipos-recordatorio/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Tipos de recordatorio - modificacion | `PUT /api/cattiporecordatorio` | `PUT /api/catalogos/tipos-recordatorio/{id}` | `catalogos.editar` | **OK** |  |
| Transferencias - aplicar pagos | `POST /api/bancostransferencias/aplicar` | `POST /api/bancos-transferencias/movimientos/aplicar` | `transferencias.aplicar` | **OK** | `aplicarA` obligatorio (dinero/cascos) en vez de dos banderas sueltas, y los pagos como lista de pares venta/monto |
| Transferencias - cancelar movimiento | `POST /api/bancostransferencias/cancelar` | `POST /api/bancos-transferencias/movimientos/{id}/cancelar` | `transferencias.cancelar` | **OK** | se manda @IDBancoTransferenciaMovimiento, el parametro que el SP tiene de verdad: **en Legacy esta operacion esta ROTA** porque manda @IDBancoTransferencia, que no existe |
| Transferencias - carga del estado de cuenta | `POST /api/bancostransferencias` | `POST /api/bancos-transferencias` | `transferencias.cargar` | **OK** | **el lote entero en UNA transaccion**. El SP inserta un movimiento por llamada y Legacy dejaba el bucle en el cliente: sin transaccion, un archivo de 400 que fallara en el 250 dejaba 249 cargados |
| Transferencias - consulta | `POST /api/bancostransferencias/consultar` | `GET /api/bancos-transferencias` | `transferencias.ver` | **OK** | precedencia explicita: id, luego archivo, luego el resto |
| Transferencias - identificar cliente | `POST /api/bancostransferencias/identificar` | `PUT /api/bancos-transferencias/movimientos/identificar` | `transferencias.identificar` | **OK** | el cliente es obligatorio |
| Transferencias - movimientos | `POST /api/bancostransferencias/consultar/detalle` | `POST /api/bancos-transferencias/movimientos/consultar` | `transferencias.ver` | **OK** | todos los filtros conviven |
| Unidades de medida - alta | `POST /api/catunidadesmedida` | `POST /api/catalogos/unidades-medida` | `catalogos.crear` | **OK** |  |
| Unidades de medida - baja | `DELETE /api/catunidadesmedida` | `POST /api/catalogos/unidades-medida/{id}/inhabilitar` | `catalogos.baja` | **OK** | la baja LEE los valores actuales y solo cambia IDStatus; hay `habilitar` |
| Unidades de medida - modificacion | `PUT /api/catunidadesmedida` | `PUT /api/catalogos/unidades-medida/{id}` | `catalogos.editar` | **OK** |  |
| Unidades territoriales - alta | `POST /api/catunidadterritorial` | `POST /api/catalogos/unidades-territoriales` | `catalogos.territorial.crear` | no verificable | el SP no esta en Produccion_svr; Legacy falla igual |
| Unidades territoriales - modificacion | `PUT /api/catunidadterritorial` | `PUT /api/catalogos/unidades-territoriales/{id}` | `catalogos.territorial.editar` | no verificable | el SP no esta aqui. Legacy manda SOLO el id aunque su cadena de depuracion arma cinco valores: se manda lo mismo para no inventar la firma |
| Usuario - almacenes | `POST /api/usuarios/almacenes` | `PUT /api/usuarios/mac31/{id}/almacenes` | `usuarios.editar` | **OK** | lista de pares EN UNA TRANSACCION; sin renglon aqui, 'Mis pedidos' del repartidor sale vacia |
| Usuario - fechas | `POST /api/usuarios/fechas` | `PUT /api/usuarios/mac31/{id}/fechas` | `usuarios.editar` | **OK** | lista de pares en una transaccion; la fecha viaja como fecha |
| Usuario - procesos | `PUT /api/usuarios/formas/procesos` | `PUT /api/usuarios/mac31/{id}/procesos` | `permisosweb.bootstrap` | **OK** | lista de pares en una transaccion; es la FUENTE de cuatro familias de permisos del web |
| Usuario de Mac31 - alta | `POST /api/usuarios` | `POST /api/usuarios/mac31` | `usuarios.crear` | **OK** | es dbo.Usuarios (llave entera), no UsuarioWeb (Guid) |
| Usuario de Mac31 - baja | `DELETE /api/usuarios` | `POST /api/usuarios/mac31/{id}/inhabilitar` | `usuarios.estado.editar` | **OK** | **conserva la contrasena**: en Legacy la dejaba vacia y al reactivarlo entraba sin contrasena |
| Usuario de Mac31 - modificacion | `PUT /api/usuarios` | `PUT /api/usuarios/mac31/{id}` | `usuarios.editar` | **OK** | ficha completa, contrasena incluida |

**127 en verde, 13 no verificables, 5 no migradas, 1 parcial.**

## Lógica escondida que se encontró

1. **Tres rutas de nómina que decían "consultar" y "validar" y escribían.**
   `empleadostiposueldorecibos/consultar` **calcula** la nómina: borra los recibos del
   periodo y los reinserta con un cursor — y legacy lo corría **dos veces por petición**
   (nómina y comisión), sin transacción, calculando el periodo de comisión como "el de
   nómina más uno". `periodostiposueldodias/validar` **crea** los periodos, y lo disparaba
   el **login** de Mac31: lo corría cualquiera que entrara. `empleadosprestamo/validar` es
   el batch que **descuenta los préstamos de la nómina**. Las tres ahora dicen lo que hacen,
   piden permiso propio y firman con el id del token, no con el del cuerpo.

2. **`catformasprocesos` POST y PUT no hacen lo que dicen: insertan días festivos.**
   El POST llama a `sp_n_InsertarDiaFestivo` mandándole solo usuario y equipo (el `IDForma`
   nunca se envía), así que "dar de alta una forma-proceso" mete **un día festivo en
   blanco**. El PUT llama a `sp_n_ActualizarDiaFestivo` y le pasa el **mismo `IDForma`** en
   `@IDDiaFestivo`, `@IDStatus` y `@DiaFestivo` a la vez. No se migran: no existe la
   capacidad, y en la base no hay ningún `sp_n_*FormaProceso` al que apuntarlas. Lo que sí
   existe —asignar un proceso a un usuario— está migrado.

3. **Tres pantallas que ejecutaban el procedimiento N veces sin transacción.** Los almacenes,
   los procesos y las fechas del usuario: legacy partía dos cadenas unidas con `~`,
   recorría el arreglo y llamaba al procedimiento una vez por elemento. Si fallaba en la
   quinta de ocho, las cuatro anteriores quedaban escritas, y la respuesta se armaba con el
   resultado de la **última vuelta**, así que un fallo intermedio podía terminar
   contestando éxito. Un usuario con la mitad de sus permisos cambiados y una pantalla que
   dice que todo bien es lo peor que puede pasar en una pantalla de seguridad.

4. **La carga del estado de cuenta del banco era un bucle en el cliente.**
   `sp_n_InsertarBancoTransferencia` inserta **un movimiento por llamada** y crea la
   cabecera en la primera. Legacy dejaba el bucle en el cliente: N peticiones HTTP sueltas,
   sin transacción. Un archivo de 400 renglones que fallara en el 250 dejaba 249 cargados y
   el estado de cuenta a medias, sin forma de saber desde dónde seguir. Ahora el lote entero
   va en una transacción y además se comprueba que `numMovimientos` cuadre con los renglones
   que llegan.

5. **Listas paralelas apareadas por posición, sin que nada comprobara su longitud.** Cuatro
   en el alta de corte (`IDsTipoGasto`, `IDsEmpleadoPrestamo`, `Conceptos`, `Montos`), tres
   en el cambio de vigencia (más un `NumProductos` que podía contradecirlas), dos en las
   bonificaciones del cliente, dos en sus centros de servicio, y dos al aplicar una
   transferencia (`IDsVentas` / `MontoPagos`). Una lista corrida le pone a un gasto el monto
   de otro, o aplica el pago de una venta a otra — en silencio y en dinero. Todas viajan
   ahora como listas de objetos y el backend arma las cadenas.

6. **`sp_n_ConsultaConstantes` llamado una docena de veces solo para preguntar el formato de
   fecha.** Ese procedimiento es la **rutina de cierre del día**: si la fecha de operación
   está atrasada, la mueve y rehace el corte de inventario. Legacy lo llamaba antes de casi
   cada operación de este lote para leer una columna de texto, y si fallaba, la operación
   fallaba. En cancelar y regresar un cambio de vigencia lo llamaba y **tiraba el resultado
   sin leerlo**. Ya no se llama: las fechas se formatean en el backend.

7. **Dos operaciones que están rotas en esta base y nadie lo sabía.**
   `sp_n_CancelarBancoTransferencia` declara `@IDBancoTransferenciaMovimiento` y el
   repositorio de legacy le manda `@IDBancoTransferencia`: SQL Server rechaza la llamada
   antes de ejecutar nada, así que **cancelar una transferencia desde MacServicios2 no
   funciona**. Y el alta de documento de proveedor le manda `@IDStatus` y `@Escaneado`, dos
   parámetros que el procedimiento no tiene.

8. **`sp_n_CancelarServicio` se ejecuta sin un solo parámetro.** Las tres `AddWithValue` del
   repositorio están comentadas —y los nombres que usaban eran de otro módulo
   (`@IDAjusteInventario`)— y su bloque de validaciones también. Cancela "el servicio" sin
   decir cuál.

9. **El `Replace("'","")` que convertía O'BRIEN en OBRIEN** estaba en casi todos los
   servicios. En una escritura el valor va como `SqlParameter` a un INSERT/UPDATE: no se
   concatena en ningún `EXEC(@Sql)`, así que no había nada de lo que protegerse y sí un
   apellido que mutilar. Se quita en todo el lote **con una excepción**:
   `sp_n_ActualizarClienteFacturacion` sí arma su UPDATE concatenando, y ahí la comilla se
   **duplica**, que es como se escapa en T-SQL. Legacy hacía eso solo en ese endpoint y
   tenía razón; está documentado para que nadie lo "limpie".

10. **Campos que el procedimiento pide y pisa.** `sp_n_ActualizarProductoFile` recibe
    `@PrecioLista` y lo escribe: en legacy, subir una foto sin mandar el precio dejaba el
    **precio en cero**. Ahora el backend lee el precio actual. `sp_n_ActualizarGallero`
    tiene el `Saldo = @Saldo` **comentado**, así que la modificación nunca cambia el saldo:
    el request ya no lo pide. Y en Tauro, `sp_n_InsertarCliente` **ignora** el límite de
    crédito que se le manda y usa `Constantes.LimiteCreditoNuevos` (se mandó 900 y quedó
    10 000).

11. **Un defecto del backend nuevo, no de legacy: `IDAlgo` salía al JSON como `205Algo`.**
    En C#, `'i' + 'd'` no es `"id"`: es 205, porque sumar dos `char` suma sus códigos.
    `Funciones.ACamelCase` lo hacía así, de modo que toda columna que empieza con dos
    mayúsculas llegaba al front con una llave inventada y el campo quedaba vacío sin ningún
    error. No se había visto porque los procedimientos de los primeros módulos que usan ese
    ayudante (préstamos, asistencias, checador) ya devuelven camelCase y esa rama no se
    tocaba. Lo destapó `GET api/catalogos/dias-festivos`, que devolvía `205DiaFestivo` y
    `205Status`. Arreglado, con prueba de regresión.

12. **El alta de tipo de domicilio guardaba la fila y contestaba error.**
    `sp_n_Insertar_TipoDomicilio` inserta bien en `[Catalogo Tipos Domicilio]` y después lee
    `MAX(IDTipoDomicilio)` de `MC_TiposDomicilio`, tabla que no existe aquí: la fila queda y
    la llamada revienta, así que el usuario reintenta y crea un duplicado. Comprobado: quedó
    el renglón 3 y la respuesta fue 500. Ante **ese error exacto** se comprueba si la fila
    quedó y se contesta con su id.

## Diferencias intencionales de contrato

1. **POST → GET en las consultas.** En legacy eran POST porque el token viajaba dentro del
   JSON; aquí va en la cabecera.
2. **La identidad sale del token, siempre.** `IDUsuario`, `IDUsuarioMovimiento`,
   `IDUsuarioCancelacion` y `Equipo` viajaban en el cuerpo: cualquiera podía firmar un
   movimiento, o el descuento de la nómina, a nombre de otro.
3. **La precedencia de los filtros se dice en vez de hacerse a escondidas.** Legacy
   reemplazaba el objeto de filtros completo ("si viene el folio, olvídate del resto") y
   tiraba sin avisar lo que el usuario había puesto. Peor: varios de esos campos no tenían
   valor por omisión, así que `null != ""` metía a **todas** las consultas en la rama del
   folio. Ahora la rama solo se toma cuando de verdad llega texto.
4. **Las modificaciones piden la ficha completa, y se dice.** El UPDATE de legacy escribe
   todas las columnas: ofrecer un PATCH sería mentir, porque el backend no puede saber qué
   valor dejar si el procedimiento ya los pisó todos. La excepción es el `IDFacturama` del
   cliente, que tiene su propia ruta porque el procedimiento tiene una rama (`@Accion = 1`)
   que cambia solo esa columna.
5. **Los rangos de fecha van completos o vacíos.** Legacy convertía una fecha sin comprobar
   que la otra viniera, con `Substring` de posición fija: la consulta devolvía 400 con el
   stack completo en vez de decir que faltaba una fecha. En el rango de pago de compras el
   `if` miraba la fecha **final** y convertía la **inicial**.
6. **`@Formato` no se expone.** Era el interruptor "0 todos los campos / 1 Excel" de
   WinForms; el Excel lo arma el front con los mismos datos.
7. **Cancelar un corte tiene UNA ruta.** Legacy tenía dos (`/cortes/cancelar` y
   `/cortescs/cancelar`) que llamaban al **mismo** `sp_n_CancelarCorte` con los mismos
   parámetros: dos puertas indistinguibles en la base.
8. **No se piden campos que no se guardan.** El motivo de cancelación del cambio de vigencia
   (legacy lo recibía, lo saneaba y lo tiraba), los `*Docto` del proyecto,
   `EsRepresentanteLegal`/`Tecnico` del apoderado, y el `IDDescuento`/`IDFacturama` en la
   modificación de proveedor.
9. **La consulta de sincronización ya no deja que el cuerpo elija la base de datos.** Legacy
   tenía una bandera booleana que reemplazaba la conexión por la cadena guardada en
   `Constantes.AviacionDBServer`.
10. **`aplicarA` es un valor, no dos banderas.** Con las dos en `true` legacy aplicaba
    cascos; con las dos en `false` ejecutaba un procedimiento con **nombre vacío** y el
    usuario leía `"[No se pudo procesar ] - ..."`.
11. **No hay baja en ciudades, municipios ni colonias.** Sus tablas no tienen `IDStatus`:
    el DELETE de legacy sobre ellas solo vaciaba el nombre.
12. **Los números se escriben con cultura invariante.** Legacy usaba `.ToString()` con la
    cultura del servidor: donde el separador decimal es la coma, un 12.5 se vuelve `"12,5"`
    y el procedimiento, que parte cadenas, lee dos valores donde había uno.

## Cómo se demuestra

**Primera tanda — 92 comprobaciones contra la base, desde la instantánea limpia.** Cada
escritura se ejecuta y se mira el **efecto en la base**, no solo la respuesta HTTP: que la
baja conserve nombre/precio/stock/contraseña, que el apóstrofo se guarde, que la fecha
quede en el día correcto, que la plantilla de huella se guarde completa (1200 bytes), que
un lote que falla no deje ningún renglón, que adjuntar un archivo no ponga el precio en
cero. **92 de 92.**

**Segunda tanda — los caminos de escritura restantes** (los catálogos que faltaban,
territorial, autos, empresas y series, las pestañas del proveedor, el ciclo del cambio de
vigencia, el corte, la carga del estado de cuenta y los tres batches de nómina).

**Comparación contra Legacy, con el ciclo del protocolo** (`volver` → escribir en Legacy →
foto → `volver` → escribir en el nuevo → foto → comparar), ocho operaciones:

| operación | resultado |
|---|---|
| alta de agente | **igual** (`1\|COMPARA 977\|977\|1`) |
| modificación de agente | **igual**, y el id es el mismo |
| baja de agente | **distinto A PROPÓSITO**: legacy deja `IDStatus = 1` (no da de baja nada), el nuevo deja 2 |
| alta de producto | **igual** (`1\|123.45\|5\|CMP977`) |
| alta de cliente | **igual** (`1\|…\|5500000980\|10000.00\|2`) |
| almacenes del usuario (lote) | **igual** |
| alta de recordatorio | **igual**, con la fecha límite en el día correcto |
| alta de marca | **igual** |

En el alta de cliente hubo que darle a Legacy un cuerpo con **todos** los campos de texto:
con `IDTipoCliente = 2` y los campos omitidos contesta
`"Referencia a objeto no establecida como instancia de un objeto"` — su DTO no inicializa
`ApellidoPaterno`, `Calle`, `Rfc`… y el `.Replace` revienta. Con el cuerpo completo las dos
fotos coinciden.

**Pruebas automatizadas:** `dotnet test` → **108 pruebas, 0 fallos** (18 nuevas): el formato
exacto del `CorteInfo` (20 campos, el `||||` del campo 13, sin delimitador al final, con
cultura de coma decimal), la elección dinero/cascos, los veinte parámetros con nulos, y la
regresión del `205`.

La instantánea se tomó con `/tmp/instantanea-en-uso-5085` y se devolvió con `volver` al
terminar. La fecha de operación era `2026-10-06` al empezar y al terminar.

## Permisos nuevos

61, todos en `PermissionCatalogSeeds`. Ninguno pisa las cuatro familias que manda Mac31
(`reportes.*`, `ventas.*`, `empleados.*`, `prestamos.*`): lo que se diera ahí desde la
pantalla de permisos se borraría en el siguiente recálculo, sin error y sin rastro.

| familia | claves | por qué va aparte |
|---|---|---|
| `catalogos.{crear,editar,baja}` | 3 | los desplegables de captura |
| `catalogos.marcas.{crear,editar,baja}` | 3 | mover una marca de columna cambia los reportes de concentrado de toda la empresa |
| `catalogos.galleros.{crear,editar,baja}` | 3 | el renglón trae **saldo**: es dinero |
| `catalogos.empresas.{crear,editar,baja}` | 3 | RFC, régimen fiscal, CLABE y el folio con el que se factura |
| `catalogos.territorial.{crear,editar}` | 2 | le mueve el domicilio a clientes y proveedores ya capturados. Sin `.baja`: esas tablas no tienen `IDStatus` |
| `catalogos.autos.{crear,editar,baja}` | 3 | póliza y monto asegurado |
| `clientes.{crear,editar,estado.editar}` | 3 | — |
| `clientes.bonificaciones.editar` | 1 | es lo que se le abona al cliente por cada casco: dinero |
| `clientes.facturacion.editar` | 1 | quien captura un teléfono no tiene por qué cambiar el RFC |
| `clientes.centros.editar` | 1 | decide dónde se le surte |
| `productos.{crear,editar,estado.editar}` | 3 | — |
| `compras.{ver_modulo,ver,editar,cancelar}` | 4 | cancelar devuelve mercancía y mueve inventario |
| `inventario.transito.procesar` | 1 | mueve existencias, y se usa desde otra pantalla |
| `cortes.{ver_modulo,ver,crear,cancelar,cs.ver,cs.crear}` | 6 | — |
| `transferencias.{ver_modulo,ver,cargar,identificar,aplicar,cancelar}` | 6 | cuatro pasos con consecuencias distintas: quien sube el archivo del banco no tiene por qué aplicar pagos |
| `cambiosvigencia.{ver_modulo,ver,crear,editar,cancelar}` | 5 | — |
| `constantes.{editar,fechapagos.editar}` | 2 | la fecha de pagos decide con qué fecha entran los cobros |
| `huellas.editar` | 1 | — |
| `seguridad.bitacora.registrar` | 1 | — |
| `reportes.parametros.{ver,editar}` | 2 | — |
| `nomina.{ver_modulo,recibos.calcular,periodos.generar,prestamos.descontar}` | 4 | el último mueve dinero de la gente |
| `recordatorios.*`, `servicios.*`, `proyectos.*`, `sincronizacion.ver` | 13 | pantallas que solo existen en algunas instalaciones |

El usuario de Mac31 **reúsa** `usuarios.{crear,editar,estado.editar}` y el proveedor reúsa
`proveedores.{crear,editar,estado.editar}`: son los mismos conceptos, no hacía falta
inventar claves nuevas. Los procesos del usuario exigen `permisosweb.bootstrap`, porque
quien los toca administra seguridad.

## Qué quedó sin verificar, y por qué

**Trece capacidades no verificables en esta base**, todas porque su procedimiento no está
instalado en `Produccion_svr` (comprobado en `sys.objects`) — **Legacy falla igual aquí**:

* proyectos (consulta, alta, modificación, facturas): `sp_n_Consultar_Proyectos`,
  `sp_n_Insertar_Proyecto`, `sp_n_Actualizar_Proyecto`, `sp_n_Actualizar_Proyecto_Facturas`;
* servicios (consulta, alta, cancelación): `sp_n_ConsultaServicio`, `sp_n_InsertarServicio`,
  `sp_n_CancelarServicio`;
* sincronización: `sp_n_ConsultaSincronizacion`;
* unidades territoriales y centros de servicio (alta, modificación, baja):
  `sp_n_InsertarCatUnidadTerritorial`, `sp_n_ActualizarCatUnidadTerritorial`,
  `sp_n_InsertarCentroServicio`, `sp_n_ActualizarCentroServicio`.

Dos cosas más que se comprobaron y **no son del backend nuevo**:

* **documentos**: la tabla `[Catalogo Documentos]` no existe en esta base (la ola 1 ya lo
  había anotado para su lectura), así que su alta y su baja no se pueden probar aquí;
* **la modificación de un cambio de vigencia**: `sp_n_ActualizarCambioVigencia` referencia
  las columnas `IDRepartidor`, `Ticket`, `Vigencia` y `Garantia`, que no existen en la tabla
  `CambiosVigencia` de esta base. El alta, la cancelación y el regresar sí funcionan.

**Una parcial, por alcance declarado:** `POST /api/nomina/recibos/calcular` migra el
**cálculo** —lo que escribe, que es lo peligroso— y devuelve los datos del recibo. El
**armado del papel** (las plantillas 1020 de `Template_Html`, cuatro recibos por hoja en
Tauro y uno en Zaragoza, con las reglas de `FormateaNumero` que convierten los ceros en
cadena vacía) es un **reporte**: tiene su propio motor y su propio plan en
`reportes-plan.md`, y duplicarlo aquí sería mantener dos veces el mismo papel.

**Cinco no migradas, a propósito:** las tres bajas territoriales (ciudades, municipios,
colonias — sus tablas no tienen `IDStatus`) y `POST`/`PUT /api/catformasprocesos`, que no
son una capacidad sino un copy-paste que inserta días festivos en blanco (ver "Lógica
escondida", punto 2).

## Pendientes anotados

* **`registro.tsv` no se tocó.** Las 146 filas siguen como estaban. Varios agentes escriben
  ese archivo al mismo tiempo y reescribirlo entero podría pisar sus filas. Hay que pasarlas
  a `hecho` con `ubicacion_nueva` = la ruta nueva de la tabla, `metodo_validacion` =
  `comparador` / `pruebas de efecto en base`, y `resultado` = el veredicto de la tabla.
* Las 13 no verificables hay que correrlas contra la instalación que sí tiene esos
  procedimientos (proyectos y servicios son de otra empresa; sincronización es de Aviación).
* `sp_n_Insertar_TipoDomicilio` referencia `MC_TiposDomicilio`. Se rodeó desde el backend,
  pero el arreglo de verdad es un `sp_w_` o corregir el `sp_n_` el día que se pueda.
* La modificación de cambio de vigencia necesita que esta base tenga las cuatro columnas que
  su procedimiento espera.
* El front todavía no llama a nada de esto: las rutas están, los permisos están sembrados en
  código y entran a `WPermiso` cuando alguien sincronice el catálogo desde la pantalla de
  permisos. **No se corrió ningún script contra la base.**
