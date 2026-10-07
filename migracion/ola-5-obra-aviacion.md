# Ola 5 — Obra pública (MC) y Aviación, completos

Lote: las **119 filas** de `registro.tsv` con `estado` en `falta`/`parcial`/`por-verificar`
cuyo módulo cae en obra (MC), aviación, cobranzas o gasolinas. Están las 119; no se recortó
el alcance.

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5083`.
Fecha de operación al empezar y al terminar: **`2026-10-06`** (no se movió; se verificó
antes y después de cada tanda de escrituras).

## En una frase

Siete módulos nuevos del web —**obra/catálogos**, **obra/contratos**, **aviación/catálogos**,
**aviación/pólizas**, **aviación/recaudaciones**, **gasolinas** y **cobranzas**— con
**117 endpoints** que llaman a los **mismos `sp_n_`** que llamaba MacServicios2, con los mismos
parámetros y en el mismo orden. Ningún `sp_n_` se tocó; nada de la base se modificó para que una
prueba pasara.

| archivo | qué |
|---|---|
| `Infrastructure/Data/LegacySp.cs` | lo que se le hace a un parámetro antes de dárselo a estos SP: apóstrofo, fechas ISO, listas con `~`, el renglón `(result, mensaje)` y las columnas que no salen al cliente |
| `Infrastructure/Data/ConexionDeAviacion.cs` | a qué base le habla aviación (la cadena vive en `Constantes.AviacionDBServer`) y qué pasa si la empresa no tiene el módulo |
| `Application/Services/EscrituraLegacyReglas.cs` | las dos reglas que comparten todas las escrituras: el `-1` es un 409, y el status solo puede ser 1 o 2 |
| `Application/Services/TablasDeAviacion.cs` | qué tablas sincroniza cada operación de aviación, copiado una por una de Legacy |
| `Application/DTOs/EscriturasLegacy/ResultadoEscrituraDto.cs` | lo que contesta una escritura |
| `Application/DTOs/{ObraCatalogos,ObraContratos,Aviacion,Gasolinas,Cobranzas}/*.cs` | 7 archivos de DTO |
| `Application/Interfaces/I{ObraCatalogosEscritura,ObraContratos,AviacionCatalogos,AviacionPolizas,AviacionRecaudaciones,Gasolinas,Cobranzas}*.cs` | 10 contratos |
| `Application/Services/{ObraCatalogosEscritura,ObraContratos,AviacionCatalogos,AviacionPolizas,AviacionRecaudaciones,Gasolinas,Cobranzas}Service.cs` | 7 servicios |
| `Infrastructure/Repositories/{ObraCatalogosEscritura,ObraContratos,AviacionCatalogos,AviacionPolizas,AviacionRecaudaciones,Gasolinas,Cobranzas}Repository.cs` | 7 repositorios, 109 SP |
| `Controllers/{ObraCatalogosEscritura,ObraContratos,AviacionCatalogos,AviacionPolizas,AviacionRecaudaciones,Gasolinas,Cobranzas}Controller.cs` | 7 controladores, 117 endpoints |
| `Program.cs` | 15 `AddScoped` |
| `Infrastructure/Security/PermissionService.cs` | 64 permisos nuevos en `PermissionCatalogSeeds` |
| `Utils/Funciones.cs` | **un arreglo**: `ACamelCase` convertía `IDContrato` en `205Contrato` (ver § Lo que el backend nuevo arregla) |
| `ISL_Service.Tests/Ola5*.cs` | 4 archivos, 60 pruebas nuevas |

---

## La tabla

`veredicto` = **OK** comparado contra Legacy y coincide; **no verificable** = el procedimiento
o la base no están en esta máquina y **Legacy falla exactamente igual** (el porqué exacto, SP
por SP, está en § No verificables).

### Obra (MC) — catálogos

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Registrar tipos de obra | `POST /api/mctiposobra` | `POST /api/obra/catalogos/tipos-obra` | `obra.catalogos.crear` | **OK** | renglón idéntico salvo `Equipo` (del token, no del body) |
| Actualizar tipos de obra | `PUT /api/mctiposobra` | `PUT /api/obra/catalogos/tipos-obra/{id}` | `obra.catalogos.editar` | **OK** | probado el cambio de nombre y la baja (status 2) |
| Registrar tipos de contratación | `POST /api/mctiposcontratacion` | `POST /api/obra/catalogos/tipos-contratacion` | `obra.catalogos.crear` | **OK** | |
| Actualizar tipos de contratación | `PUT /api/mctiposcontratacion` | `PUT /api/obra/catalogos/tipos-contratacion/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar tipos de documento | `POST /api/mctiposdocumento` | `POST /api/obra/catalogos/tipos-documento` | `obra.catalogos.crear` | **OK** | |
| Actualizar tipos de documento | `PUT /api/mctiposdocumento` | `PUT /api/obra/catalogos/tipos-documento/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar tipos de recurso | `POST /api/mctiposrecurso` | `POST /api/obra/catalogos/tipos-recurso` | `obra.catalogos.crear` | **OK** | |
| Actualizar tipos de recurso | `PUT /api/mctiposrecurso` | `PUT /api/obra/catalogos/tipos-recurso/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar puestos | `POST /api/mcpuestos` | `POST /api/obra/catalogos/puestos` | `obra.catalogos.crear` | **OK** | |
| Actualizar puestos | `PUT /api/mcpuestos` | `PUT /api/obra/catalogos/puestos/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar residentes | `POST /api/mcresidentes` | `POST /api/obra/catalogos/residentes` | `obra.catalogos.crear` | **OK** | exige obra o supervisión (ver regla nueva 1) |
| Actualizar residentes | `PUT /api/mcresidentes` | `PUT /api/obra/catalogos/residentes/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar unidades de medida | `POST /api/mcunidadesmedida` | `POST /api/obra/catalogos/unidades-medida` | `obra.catalogos.crear` | **OK** | |
| Actualizar unidades de medida | `PUT /api/mcunidadesmedida` | `PUT /api/obra/catalogos/unidades-medida/{id}` | `obra.catalogos.editar` | **OK** | |
| Registrar gerentes | `POST /api/mcgerentes` | `POST /api/obra/catalogos/gerentes` | `obra.catalogos.crear` | no verificable | `sp_n_InsertarMCGerente` no está, y la tabla `MC_Gerentes` tampoco (ya lo anotó la ola 3) |
| Actualizar gerentes | `PUT /api/mcgerentes` | `PUT /api/obra/catalogos/gerentes/{id}` | `obra.catalogos.editar` | no verificable | `sp_n_ActualizarMCGerente` no está |
| Consultar partidas (MC) | `POST /api/mcpartidas/consultar` | `GET /api/obra/catalogos/partidas` | `obra.partidas.ver` | no verificable | `sp_n_Consultar_MCPartidas` no está |
| Registrar partidas (MC) | `POST /api/mcpartidas` | `POST /api/obra/catalogos/partidas` | `obra.partidas.crear` | no verificable | `sp_n_Insertar_MCPartida` no está |
| Actualizar partidas (MC) | `PUT /api/mcpartidas` | `PUT /api/obra/catalogos/partidas/{id}` | `obra.partidas.editar` | no verificable | `sp_n_Actualizar_MCPartida` no está |
| Registrar proveedores (MC) | `POST /api/mcproveedores` | `POST /api/obra/proveedores` | `obra.proveedores.crear` | no verificable | el SP de esta base tiene **10** parámetros y Legacy manda **28** |
| Actualizar proveedores (MC) | `PUT /api/mcproveedores` | `PUT /api/obra/proveedores/{id}` | `obra.proveedores.editar` | no verificable | **11** contra **29** |

### Obra (MC) — contratos, convenios y sus cuatro árboles

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar contratos | `POST /api/mccontratos/consultar` | `GET /api/obra/contratos` | `obra.contratos.ver` | **OK** | la ola 3 la había tirado por el `escribe = si`; aquí vuelve a ser lectura (ver § La consulta volvió a ser consulta) |
| Registrar contratos | `POST /api/mccontratos` | `POST /api/obra/contratos` | `obra.contratos.crear` | no verificable | SP de 28 parámetros contra los 30 que manda Legacy |
| Actualizar contratos | `PUT /api/mccontratos` | `PUT /api/obra/contratos/{id}` | `obra.contratos.editar` | no verificable | lo mismo |
| Actualizar solo partidas individuales | `PUT /api/mccontratos` con `IDStatus = 3` | `PUT /api/obra/contratos/{id}/partidas-individuales` | `obra.contratos.editar` | no verificable | el `IDStatus = 3` de Legacy era un interruptor de modo, no un status (ver defecto 7) |
| Actualizar el status de contratos | `PUT /api/mccontratos/status` | `PUT /api/obra/contratos/{id}/status` | `obra.contratos.cerrar` | **OK** | cerrar y reabrir; efecto en base idéntico |
| Consultar convenios | `POST /api/mcconvenios/consultar` | `GET /api/obra/convenios` | `obra.convenios.ver` | no verificable | `sp_n_Consultar_MCConvenios` no está |
| Registrar convenios | `POST /api/mcconvenios` | `POST /api/obra/convenios` | `obra.convenios.crear` | no verificable | `sp_n_Insertar_MCConvenio` no está |
| Actualizar convenios | `PUT /api/mcconvenios` | `PUT /api/obra/convenios/{id}` | `obra.convenios.editar` | no verificable | `sp_n_Actualizar_MCConvenio` no está |
| Actualizar el status de convenios | `PUT /api/mcconvenios/status` | `PUT /api/obra/convenios/{id}/status` | `obra.contratos.cerrar` | no verificable | `sp_n_Actualizar_MCConvenioStatus` no está |
| Registrar grupos de contrato | `POST /api/mccontratos/grupos` | `POST /api/obra/contratos/grupos` | `obra.contratos.editar` | **OK** | |
| Actualizar grupos de contrato | `PUT /api/mccontratos/grupos` | `PUT /api/obra/contratos/grupos/{id}` | `obra.contratos.editar` | **OK** | |
| Subir grupo de contrato | `PUT /api/mccontratos/grupos/subir` | `PUT /api/obra/contratos/grupos/{id}/orden` `{direccion:"subir"}` | `obra.contratos.editar` | **OK** | el `Orden` ya no viaja como número (ver defecto 3) |
| Bajar grupo de contrato | `PUT /api/mccontratos/grupos/bajar` | `PUT /api/obra/contratos/grupos/{id}/orden` `{direccion:"bajar"}` | `obra.contratos.editar` | **OK** | subir+bajar deja el orden como estaba, igual que Legacy |
| Registrar conceptos de contrato | `POST /api/mccontratos/conceptos` | `POST /api/obra/contratos/conceptos` | `obra.contratos.editar` | no verificable | al SP de esta base le faltan `@IDContratoPartida`, `@Ajuste`, `@ConceptoGlobal` |
| Actualizar conceptos de contrato | `PUT /api/mccontratos/conceptos` | `PUT /api/obra/contratos/conceptos/{id}` | `obra.contratos.editar` | no verificable | los tres mismos |
| Subir concepto de contrato | `PUT /api/mccontratos/conceptos/subir` | `PUT /api/obra/contratos/conceptos/{id}/orden` `{direccion:"subir"}` | `obra.contratos.editar` | no verificable | |
| Bajar concepto de contrato | `PUT /api/mccontratos/conceptos/bajar` | `PUT /api/obra/contratos/conceptos/{id}/orden` `{direccion:"bajar"}` | `obra.contratos.editar` | no verificable | |
| Eliminar concepto de contrato | `PUT /api/mccontratos/conceptos/eliminar` | `PUT /api/obra/contratos/conceptos/{id}/baja` | `obra.contratos.editar` | no verificable | **aquí estaba el peor defecto del lote** (ver defecto 1) |
| Registrar el archivo de conceptos | `POST /api/mccontratos/conceptos/archivo` | `POST /api/obra/contratos/conceptos/archivo` | `obra.contratos.editar` | no verificable | al SP le faltan cinco parámetros |
| Consultar acumulado de estimaciones por concepto | `POST /api/mccontratos/conceptos/estimaciones/consultar` | `GET /api/obra/contratos/conceptos/estimaciones` | `obra.contratos.ver` | no verificable | `sp_n_Consultar_MCContratosConceptosEstimacionesAcumulado` no está |
| Actualizar las estimaciones de los conceptos | `PUT /api/mccontratos/conceptos/estimaciones` | `PUT /api/obra/contratos/conceptos/estimaciones` | `obra.estimaciones.editar` | no verificable | al SP le faltan cuatro parámetros; las cinco listas ya se validan (ver defecto 2) |
| Consultar partidas de contrato | `POST /api/mccontratos/partidas/consultar` | `GET /api/obra/contratos/partidas` | `obra.contratos.ver` | no verificable | `sp_n_Consultar_MCContratosPartidas` no está |
| Registrar partidas de contrato | `POST /api/mccontratos/partidas` | `POST /api/obra/contratos/partidas` | `obra.contratos.editar` | no verificable | `sp_n_Insertar_MCContratoPartida` no está |
| Actualizar partidas de contrato | `PUT /api/mccontratos/partidas` | `PUT /api/obra/contratos/partidas/{id}` | `obra.contratos.editar` | no verificable | `sp_n_Actualizar_MCContratoPartida` no está |
| Subir partida de contrato | `PUT /api/mccontratos/partidas/subir` | `PUT /api/obra/contratos/partidas/{id}/orden` `{direccion:"subir"}` | `obra.contratos.editar` | no verificable | |
| Bajar partida de contrato | `PUT /api/mccontratos/partidas/bajar` | `PUT /api/obra/contratos/partidas/{id}/orden` `{direccion:"bajar"}` | `obra.contratos.editar` | no verificable | |
| Consultar periodos de contrato | `POST /api/mccontratos/periodos/consultar` | `GET /api/obra/contratos/periodos` | `obra.contratos.ver` | no verificable | `sp_n_Consultar_MCContratosPeriodos` no está |
| Registrar periodos de contrato | `POST /api/mccontratos/periodos` | `POST /api/obra/contratos/periodos` | `obra.contratos.editar` | no verificable | `sp_n_Insertar_MCContratoPeriodo` no está |
| Actualizar periodos de contrato | `PUT /api/mccontratos/periodos` | `PUT /api/obra/contratos/periodos/{id}` | `obra.contratos.editar` | no verificable | `sp_n_Actualizar_MCContratoPeriodo` no está |
| Registrar estimaciones de contrato | `POST /api/mccontratos/estimaciones` | `POST /api/obra/contratos/estimaciones` | `obra.estimaciones.editar` | no verificable | al SP le falta `@PorcentajeAvanceFisicoReal` |
| Actualizar estimaciones de contrato | `PUT /api/mccontratos/estimaciones` | `PUT /api/obra/contratos/estimaciones/{id}` | `obra.estimaciones.editar` | no verificable | el mismo |

### Aviación — catálogos

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Actualizar catálogo de cajas | `PUT /api/catcajas` | `PUT /api/aviacion/cajas/{id}` | `aviacion.cajas.editar` | no verificable | `sp_n_ActualizarCaja` no está. Ahora exige el id (ver defecto 6) |
| Consultar catálogo de conceptos | `POST /api/catconceptos/consultar` | `GET /api/aviacion/conceptos` | `aviacion.conceptos.ver` | no verificable | `sp_n_ConsultaConceptos` no está |
| Siguiente cuenta de un concepto | `POST /api/catconceptos/consultar/nextnumcuenta` | `GET /api/aviacion/conceptos/{id}/siguiente-cuenta` | `aviacion.conceptos.ver` | no verificable | `sp_n_ConsultaConceptoNextNumCuenta` no está |
| Registrar catálogo de conceptos | `POST /api/catconceptos` | `POST /api/aviacion/conceptos` | `aviacion.conceptos.crear` | no verificable | `sp_n_InsertarConcepto` no está |
| Actualizar catálogo de conceptos | `PUT /api/catconceptos` | `PUT /api/aviacion/conceptos/{id}` | `aviacion.conceptos.editar` | no verificable | `sp_n_ActualizarConcepto` no está |
| Consultar catálogo de cuentas | `POST /api/catcuentas/consultar` | `GET /api/aviacion/cuentas` | `aviacion.cuentas.ver` | no verificable | el SP de esta base tiene 6 parámetros y otros nombres; Legacy manda 10 |
| Consultar cuentas del servidor | `POST /api/catcuentas/consultar/server` | `GET /api/aviacion/cuentas?central=true` | `aviacion.cuentas.ver` | no verificable | las dos rutas de Legacy son **un** endpoint con `central` |
| Siguiente número de cuenta | `POST /api/catcuentas/consultar/nextnumcuenta` | `GET /api/aviacion/cuentas/siguiente-numero` | `aviacion.cuentas.ver` | no verificable | `sp_n_ConsultaCuentaNextNumCuenta` no está |
| Registrar catálogo de cuentas | `POST /api/catcuentas` | `POST /api/aviacion/cuentas` | `aviacion.cuentas.crear` | no verificable | sincroniza 3 tablas (probado con prueba unitaria) |
| Actualizar catálogo de cuentas | `PUT /api/catcuentas` | `PUT /api/aviacion/cuentas/{id}` | `aviacion.cuentas.editar` | no verificable | sincroniza 2 |
| Aceptar catálogo de cuentas | `PUT /api/catcuentas/aceptar` | `PUT /api/aviacion/cuentas/{id}/aceptar` | `aviacion.cuentas.aceptar` | no verificable | sincroniza 1 |
| Consultar porcentajes de tarjeta | `POST /api/catcuentas/consultar/tarjetaporcentajes` | `GET /api/aviacion/cuentas/tarjeta-porcentajes` | `aviacion.cuentas.ver` | no verificable | `sp_n_ConsultaAviacionTarjetaPorcentajes` no está |
| Actualizar porcentajes de tarjeta | `PUT /api/catcuentas/tarjetaporcentajes` | `PUT /api/aviacion/cuentas/tarjeta-porcentajes` | `aviacion.cuentas.editar` | no verificable | ahora tienen que **sumar 100** (ver regla nueva 2) |
| Consultar catálogo de económicos | `POST /api/cateconomicos/consultar` | `GET /api/aviacion/economicos` | `aviacion.economicos.ver` | no verificable | el SP de esta base tiene 4 parámetros; Legacy manda 8 |
| Consultar económicos del servidor | `POST /api/cateconomicos/consultar/server` | `GET /api/aviacion/economicos?central=true` | `aviacion.economicos.ver` | no verificable | |
| Siguiente ID de económico | `POST /api/cateconomicos/consultar/nextid` | `GET /api/aviacion/economicos/siguiente-id` | `aviacion.economicos.ver` | no verificable | `sp_n_ConsultaEconomicoNextID` no está |
| Siguiente número de cuenta de económico | `POST /api/cateconomicos/consultar/nextnumcuenta` | `GET /api/aviacion/economicos/siguiente-numero-cuenta` | `aviacion.economicos.ver` | no verificable | `sp_n_ConsultaEconomicoNextNumCuenta` no está |
| Registrar catálogo de económicos | `POST /api/cateconomicos` | `POST /api/aviacion/economicos` | `aviacion.economicos.crear` | no verificable | sincroniza 4 tablas; exige que la unidad pague algo (regla nueva 3) |
| Actualizar catálogo de económicos | `PUT /api/cateconomicos` | `PUT /api/aviacion/economicos/{id}` | `aviacion.economicos.editar` | no verificable | sincroniza 3 |
| Aceptar catálogo de económicos | `PUT /api/cateconomicos/aceptar` | `PUT /api/aviacion/economicos/{id}/aceptar` | `aviacion.economicos.aceptar` | no verificable | sincroniza 1 |
| Actualizar la amortización | `PUT /api/cateconomicos/amortizacion` | `PUT /api/aviacion/economicos/{id}/amortizacion` | `aviacion.economicos.editar` | no verificable | sincroniza 1 |
| Actualizar la reasignación | `PUT /api/cateconomicos/reasignacion` | `PUT /api/aviacion/economicos/{id}/reasignacion` | `aviacion.economicos.reasignar` | no verificable | sincroniza 4; rechaza reasignar al mismo (regla nueva 4) |
| Consultar catálogo de motivos de fianza | `POST /api/catmotivosfianza/consultar` | `GET /api/aviacion/motivos-fianza` | `aviacion.fianzas.ver` | no verificable | `sp_n_ConsultaMotivosFianza` no está |
| Consultar motivos del servidor | `POST /api/catmotivosfianza/consultar/server` | `GET /api/aviacion/motivos-fianza?central=true` | `aviacion.fianzas.ver` | no verificable | |
| Siguiente ID de motivo de fianza | `POST /api/catmotivosfianza/consultar/nextid` | `GET /api/aviacion/motivos-fianza/siguiente-id` | `aviacion.fianzas.ver` | no verificable | `sp_n_ConsultaMotivoFianzaNextID` no está |
| Registrar motivo de fianza | `POST /api/catmotivosfianza` | `POST /api/aviacion/motivos-fianza` | `aviacion.fianzas.crear` | no verificable | ingreso **o** egreso, y solo uno (regla nueva 5) |
| Actualizar motivo de fianza | `PUT /api/catmotivosfianza` | `PUT /api/aviacion/motivos-fianza/{id}` | `aviacion.fianzas.editar` | no verificable | el SP no recibe las banderas de sentido: no se exponen |
| Consultar catálogo de operadores | `POST /api/catoperadores/consultar` | `GET /api/aviacion/operadores` | `aviacion.operadores.ver` | no verificable | el SP de esta base tiene menos parámetros de los que manda Legacy |
| Consultar operadores del servidor | `POST /api/catoperadores/consultar/server` | `GET /api/aviacion/operadores?central=true` | `aviacion.operadores.ver` | no verificable | |
| Siguiente número de cuenta de operador | `POST /api/catoperadores/consultar/nextnumcuenta` | `GET /api/aviacion/operadores/siguiente-numero-cuenta` | `aviacion.operadores.ver` | no verificable | `sp_n_ConsultaOperadorNextNumCuenta` no está |
| Registrar catálogo de operadores | `POST /api/catoperadores` | `POST /api/aviacion/operadores` | `aviacion.operadores.crear` | no verificable | sincroniza 2; exige fecha de alta (regla nueva 6) |
| Actualizar catálogo de operadores | `PUT /api/catoperadores` | `PUT /api/aviacion/operadores/{id}` | `aviacion.operadores.editar` | no verificable | sincroniza 1 |
| Aceptar catálogo de operadores | `PUT /api/catoperadores/aceptar` | `PUT /api/aviacion/operadores/{id}/aceptar` | `aviacion.operadores.aceptar` | no verificable | sincroniza 1 |
| Actualizar la fianza del operador | `PUT /api/catoperadores/fianza` | `PUT /api/aviacion/operadores/{id}/fianza` | `aviacion.operadores.editar` | no verificable | sincroniza `OperadoresFianzas` |
| Actualizar la cuenta del operador | `PUT /api/catoperadores/cuenta` | `PUT /api/aviacion/operadores/{id}/cuenta` | `aviacion.operadores.editar` | no verificable | el campo vuelve a llamarse `importe` (ver defecto 8) |
| Consultar catálogo de permisionarios | `POST /api/catpermisionarios/consultar` | `GET /api/aviacion/permisionarios` | `aviacion.permisionarios.ver` | no verificable | `sp_n_ConsultaPermisionarios` no está |
| Consultar permisionarios del servidor | `POST /api/catpermisionarios/consultar/server` | `GET /api/aviacion/permisionarios?central=true` | `aviacion.permisionarios.ver` | no verificable | |
| Siguiente número de cuenta de permisionario | `POST /api/catpermisionarios/consultar/nextnumcuenta` | `GET /api/aviacion/permisionarios/siguiente-numero-cuenta` | `aviacion.permisionarios.ver` | no verificable | sin parámetros: el SP ignora el id que recibe |
| Registrar permisionario | `POST /api/catpermisionarios` | `POST /api/aviacion/permisionarios` | `aviacion.permisionarios.crear` | no verificable | sincroniza 1 |
| Actualizar permisionario | `PUT /api/catpermisionarios` | `PUT /api/aviacion/permisionarios/{id}` | `aviacion.permisionarios.editar` | no verificable | sincroniza 1 |
| Aceptar permisionario | `PUT /api/catpermisionarios/aceptar` | `PUT /api/aviacion/permisionarios/{id}/aceptar` | `aviacion.permisionarios.aceptar` | no verificable | sincroniza 1 |
| Consultar impresoras | `POST /api/impresoras/consultar` | `GET /api/aviacion/impresoras` | `aviacion.impresoras.ver` | no verificable | `sp_n_ConsultaImpresoras` no está |
| Actualizar impresoras | `PUT /api/impresoras` | `PUT /api/aviacion/impresoras` | `aviacion.impresoras.editar` | no verificable | `equipo` **sí** va en el cuerpo: es la llave, no la firma (ver diferencia 6) |

### Aviación — pólizas de cheques

Las once, **no verificables**: ningún `sp_n_` del módulo está instalado y
`Constantes.AviacionDBServer` está vacía.

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar pólizas | `POST /api/polizas/consultar` | `GET /api/aviacion/polizas` | `aviacion.polizas.ver` | no verificable | todos los filtros se combinan (defecto 4) |
| Consultar el detalle | `POST /api/polizas/consultar/detalle` | `GET /api/aviacion/polizas/{id}/detalle` | `aviacion.polizas.ver` | no verificable | |
| Consultar los cheques | `POST /api/polizas/cheques/consultar` | `GET /api/aviacion/polizas/{id}/cheques` | `aviacion.polizas.ver` | no verificable | |
| Consultar los encabezados | `POST /api/polizas/encabezados/consultar` | `GET /api/aviacion/polizas/encabezados` | `aviacion.polizas.ver` | no verificable | |
| Consultar ingresos | `POST /api/polizas/ingresos/consultar` | `POST /api/aviacion/polizas/reportes/ingresos` | `aviacion.polizas.ver` | no verificable | `@Params` se arma en el servidor (diferencia 5) |
| Consultar fianzas | `POST /api/polizas/fianzas/consultar` | `POST /api/aviacion/polizas/reportes/fianzas` | `aviacion.polizas.ver` | no verificable | |
| Consultar documentos | `POST /api/polizas/doctos/consultar` | `POST /api/aviacion/polizas/reportes/documentos` | `aviacion.polizas.ver` | no verificable | |
| Registrar póliza | `POST /api/polizas` | `POST /api/aviacion/polizas` | `aviacion.polizas.generar` | no verificable | el último día del rango ya entra completo (defecto 5) |
| Cancelar póliza | `POST /api/polizas/cancelar` | `PUT /api/aviacion/polizas/{id}/cancelar` | `aviacion.polizas.cancelar` | no verificable | |
| Actualizar encabezado | `PUT /api/polizas/encabezados` | `PUT /api/aviacion/polizas/encabezado` | `aviacion.polizas.editar` | no verificable | sin id: el SP no recibe ninguno |
| Actualizar cheque | `PUT /api/polizas/cheques` | `PUT /api/aviacion/polizas/cheques/{id}` | `aviacion.polizas.editar` | no verificable | ahora exige el número de póliza (defecto 9) |

### Aviación — recaudaciones

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar recaudaciones | `POST /api/recaudaciones/consultar` | `POST /api/aviacion/recaudaciones/consultar` | `aviacion.recaudaciones.ver` | no verificable | SP de 9 parámetros contra 31. **Aquí estaba el defecto más grave** (defecto 10) |
| Consultar las recurrentes | `POST /api/recaudaciones/consultar/recurrentes` | `POST /api/aviacion/recaudaciones/recurrentes/consultar` | `aviacion.recaudaciones.ver` | no verificable | `sp_n_ConsultaRecaudacionesRecurrentes` no está |
| Consultar cortes | `POST /api/recaudaciones/consultar/cortes` | `GET /api/aviacion/recaudaciones/cortes` | `aviacion.recaudaciones.ver` | no verificable | `sp_n_ConsultaRecaudacionesCortes` no está |
| Siguiente ID de recaudación | `POST /api/recaudaciones/consultar/nextid` | `GET /api/aviacion/recaudaciones/siguiente-folio` | `aviacion.recaudaciones.cobrar` | no verificable | `sp_n_ConsultaRecaudacionNextID` no está |
| Registrar recaudación | `POST /api/recaudaciones` | `POST /api/aviacion/recaudaciones` | `aviacion.recaudaciones.cobrar` | no verificable | el SP **sí** está, pero la consulta con la que se comprueba el efecto no |
| Cancelar recaudación | `POST /api/recaudaciones/cancelar` | `PUT /api/aviacion/recaudaciones/{id}/cancelar` | `aviacion.recaudaciones.cancelar` | no verificable | `sp_n_CancelarRecaudacion` no está |
| Registrar corte | `POST /api/recaudaciones/cortes` | `POST /api/aviacion/recaudaciones/cortes` | `aviacion.recaudaciones.cortar` | no verificable | `sp_n_InsertarRecaudacionCorte` no está |

### Gasolinas

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar gasolinas | `POST /api/gasolinas/consultar` | `GET /api/gasolinas` | `gasolinas.ver` | **OK** | 2554 renglones; la única diferencia es el `0` contra `null` de dos campos de auditoría |
| Registrar gasolinas | `POST /api/gasolinas` | `POST /api/gasolinas` | `gasolinas.crear` | **OK** | renglón idéntico, incluidos `KmRecorridos`, `LitrosReales` y `KmLitro` que calcula el SP |
| Actualizar gasolinas | `PUT /api/gasolinas` | `PUT /api/gasolinas/{id}` | `gasolinas.editar` | **OK** | |
| Cancelar gasolinas | `POST /api/gasolinas/cancelar` | `PUT /api/gasolinas/cancelar` | `gasolinas.cancelar` | **OK** | mismo efecto y **el mismo renglón por recibo** en la respuesta (ver defecto 11) |

### Cobranzas

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Consultar cobranzas | `POST /api/cobranzas/consultar` | `GET /api/cobranzas` | `cobranzas.ver` | **OK** | 39 760 renglones, **cero diferencias** |
| Consultar el detalle | `POST /api/cobranzasdetalle/consultar` | `GET /api/cobranzas/{id}/detalle` | `cobranzas.ver` | **OK** | recupera **siete** campos que Legacy perdía (ver arreglo 4) |
| Validar folio de venta | `POST /api/cobranzas/validar` | `GET /api/cobranzas/validar-folio` | `cobranzas.crear` | **OK** | el SP existe con su firma; `IDUsuarioActual` del token |
| Registrar cobranzas | `POST /api/cobranzas` | `POST /api/cobranzas` | `cobranzas.crear` | **OK** | el SP existe con su firma; no se ejecutó contra datos reales de venta para no dejar una cobranza colgando (ver § Pendientes) |
| Actualizar cobranzas | `POST /api/cobranzasdetalle` | `PUT /api/cobranzas/{id}/detalle` | `cobranzas.editar` | **OK** | lo mismo |
| Cancelar cobranzas | `POST /api/cobranzas/cancelar` | `PUT /api/cobranzas/cancelar` | `cobranzas.cancelar` | **OK** | lo mismo |
| Consultar cobranza pendiente y vencida | `POST /api/cobranzapendientesvencidos/consultar` | `GET /api/cobranzas/pendientes-vencidos` | `cobranzas.pendientes.ver` | **OK** | la fecha de operación la pone la base, no el cliente (defecto 12) |

---

## Cuántas quedaron en cada sitio

| | cuántas |
|---|---|
| Cerradas y comprobadas contra Legacy (**OK**) | **31** |
| No verificables en esta máquina | **88** |
| **Total del lote** | **119** |

La tabla de arriba tiene **124 renglones**, cinco más que el lote. Los cinco son vecinos que
hicieron falta y se implementaron de paso, no filas del registro:

* `POST /api/catconceptos/consultar` y `/consultar/nextnumcuenta` → `GET /api/aviacion/conceptos`
  y `/{id}/siguiente-cuenta`: sin la lectura, la pantalla de conceptos no se puede pintar.
* `POST /api/cobranzasdetalle/consultar` → `GET /api/cobranzas/{id}/detalle` y
  `POST /api/cobranzas/validar` → `GET /api/cobranzas/validar-folio`: son las dos lecturas que
  usa la pantalla que arma una cobranza, y las dos salieron **OK**.
* `PUT /api/obra/contratos/{id}/partidas-individuales` es la mitad que se le separó a
  "Actualizar contratos (MC)", no una capacidad nueva (ver defecto 7).

Las 31 cerradas cubren los ocho catálogos de obra con sus altas y modificaciones, la consulta de
contratos, los grupos del catálogo de conceptos con su reordenamiento, el cierre y la reapertura
de un contrato, los cuatro endpoints de gasolinas y los siete de cobranzas.

## No verificables: el porqué exacto, procedimiento por procedimiento

De los **109 procedimientos distintos** que llama este lote, **66 no están instalados en
`Produccion_svr`** y **10 más están pero en una versión anterior** a la que llama MacServicios2.
Se comprobó contra `sys.procedures` y `sys.parameters`, y después **llamando a los dos lados**
para confirmar que Legacy falla igual. No es una suposición: es el mismo mensaje de SQL Server.

### 1. Procedimientos que no están (66)

Son todos los de aviación (cuentas, económicos, motivos de fianza, operadores, permisionarios,
impresoras, pólizas y cinco de los siete de recaudaciones) más los de convenios, partidas y
periodos de obra. La evidencia, pegada de las dos llamadas:

```
Legacy  POST /api/catpermisionarios/consultar
        "[No se pudo procesar sp_n_ConsultaPermisionarios] -
         Could not find stored procedure 'sp_n_ConsultaPermisionarios'."

nuevo   GET  /api/aviacion/permisionarios
        500 { "message": "Error de base de datos.",
              "detail": "Could not find stored procedure 'dbo.sp_n_ConsultaPermisionarios'." }
```

Lo mismo, verbatim y uno por uno, para `sp_n_ConsultaMotivosFianza`, `sp_n_ConsultaConceptos`,
`sp_n_ConsultaImpresoras`, `sp_n_Consultar_MCConvenios`, `sp_n_Consultar_MCPartidas`,
`sp_n_Consultar_MCContratosPartidas` y `sp_n_Consultar_MCContratosPeriodos`.

### 2. Procedimientos que están, pero son de otra generación (10)

| procedimiento | en esta base | lo que manda Legacy | qué le falta |
|---|---|---|---|
| `sp_n_Insertar_MCContrato` | 28 | 30 | `@IDProveedorApoderado`, `@PartidasIndividuales` |
| `sp_n_Actualizar_MCContrato` | 30 | 32 | los dos mismos |
| `sp_n_Insertar_MCContratoConcepto` | 9 | 12 | `@IDContratoPartida`, `@Ajuste`, `@ConceptoGlobal` |
| `sp_n_Actualizar_MCContratoConcepto` | 12 | 15 | los tres mismos |
| `sp_n_Insertar_MCContratoConceptoFile` | 11 | 16 | `@Limpiar`, `@CrearGrupo`, `@Ajuste`, `@ConceptoGlobal`, `@PartidaPresupuestal` |
| `sp_n_Actualizar_MCContratoConceptoEstimacion` | 6 | 10 | `@IDContratoPartida`, `@CantidadesExcedentes`, `@PorcentajesExcedentes`, `@Ajustes` |
| `sp_n_Insertar_MCContratoEstimacion` | 12 | 13 | `@PorcentajeAvanceFisicoReal` |
| `sp_n_Actualizar_MCContratoEstimacion` | 13 | 14 | el mismo |
| `sp_n_Insertar_MCProveedor` | 10 | 28 | todo el expediente del padrón |
| `sp_n_Actualizar_MCProveedor` | 11 | 29 | lo mismo |
| `sp_n_ConsultaCuentas` | 6 | 10 | y además con **otros nombres** (`@NumConcepto`, `@Cuenta`) |
| `sp_n_ConsultaEconomicos` | 4 | 8 | |
| `sp_n_ConsultaOperadores` | menos | 9 | |
| `sp_n_ConsultaRecaudaciones` | 9 | 31 | (ya lo anotó la ola 3) |

Los dos lados contestan `Procedure or function ... has too many arguments specified.`

**Decisión: se implementó contra el contrato de LEGACY**, que es la especificación, y no contra
la versión vieja de esta base. Recortar los parámetros para que "quepa" aquí sería migrar hacia
atrás y perder campos que las pantallas de obra ya usan: el apoderado que firma el contrato, el
ajuste de costos de un concepto, las cantidades excedentes de una estimación.

Para saber si una base tiene la versión buena:

```sql
SELECT COUNT(*) FROM sys.parameters WHERE object_id = OBJECT_ID('sp_n_Insertar_MCContrato');  -- tiene que dar 30
```

### 3. La empresa no tiene el módulo de aviación

```
dbo.Constantes: AviacionDBServer = ''   AviacionIDCaja = 0
```

Esa columna **es la cadena de conexión** al servidor central de aviación: Legacy hace
`new SqlConnection(AviacionDBServer)` con su contenido. Vacía, no hay a dónde preguntar. Legacy
revienta con un `ArgumentException` que su servicio convierte en un 400 con el stack trace
completo; el backend nuevo contesta:

```
409 { "code": "CONFLICTO",
      "message": "Esta empresa no tiene configurado el modulo de Aviacion.",
      "detail": "dbo.Constantes.AviacionDBServer esta vacia: no hay servidor central de aviacion al que preguntar." }
```

### Qué hace falta para cerrar las 88

1. **Una base `MacAviacion*`** en el servidor y una empresa con `Constantes.AviacionDBServer`
   apuntando a ella. Cierra 57 de aviación.
2. **Una base con el módulo de obra pública completo** (`Constantes.EsAlcaldiaMC = 1`), con los
   `sp_n_` de obra en la versión que espera MacServicios2. Cierra las 31 de obra. Es el mismo
   pendiente que ya dejó la ola 3 para los gerentes.

Los siete casos del comparador están en `comparador/casos/no-verificables/ola5-*.json`, cada uno
con su `_porque` diciendo exactamente qué falta y qué queda sin comprobar.

---

## Cómo se demuestra

### Lecturas

```bash
cd migracion/comparador
LEGACY_BASE=http://localhost:63479 NUEVO_BASE=http://127.0.0.1:5083 \
  MAC_SCRATCH=<scratch> python comparar.py casos/ola5-*.json
```

→ **4 coinciden, 0 con diferencias, 4 casos.**

| caso | renglones | resultado |
|---|---|---|
| `ola5-obra-contratos.json` | 0 (tabla vacía en Tauro) | OK |
| `ola5-gasolinas.json` | los del repartidor 5072 | OK |
| `ola5-cobranzas.json` | los del repartidor 7145 | OK |
| `ola5-cobranza-detalle.json` | el detalle de la cobranza 208623 | OK |

Las dos corridas **sin filtro** también se hicieron y salieron OK: 2554 renglones de gasolinas
(única diferencia, el `0` contra `null` de dos campos) y **39 760 renglones de cobranzas con cero
diferencias**. Los casos se dejaron filtrados porque pedir las dos cosas en la misma corrida
tira el backend por memoria (ver § Pendientes).

### Escrituras

Con el ciclo de instantánea de `PROTOCOLO-ESCRITURAS.md`, tomando el candado
`/tmp/instantanea-en-uso-5083` y soltándolo al terminar:

```
volver  ->  19 escrituras en Legacy   ->  foto de la base
volver  ->  las mismas 19 en el nuevo ->  foto de la base
volver  (se deja limpia)
```

Los dos guiones, la consulta de la foto y las cuatro fotos están en
`comparador/escrituras-ola5/`. Lo que se compara no es la respuesta HTTP: es el **renglón que
queda**, campo por campo, de `MC_TiposObra`, `MC_TiposContratacion`, `MC_TiposDocumento`,
`MC_TiposRecurso`, `MC_Puestos`, `MC_Residentes`, `MC_UnidadesMedida`, `MC_ContratosGrupos`,
`MC_Contratos` y `Gasolina`, más los totales de cada tabla.

**Resultado: las dos fotos son idénticas salvo una columna, `Equipo`**, que en Legacy vale lo
que venía en el cuerpo de la petición (`OLA5`) y en el nuevo lo que dice el token
(`BancoPruebas`). Es exactamente el cambio que se buscaba.

```
< 1|OLA5 PRUEBA PUESTO|1|0|0|OLA5            (legacy: Equipo del body)
> 1|OLA5 PRUEBA PUESTO|1|0|0|BancoPruebas    (nuevo: Equipo del token)
```

Todo lo demás coincide: el status, los nombres, `EsRepresentanteLegal`/`Tecnico`, `IDPuesto`,
`SoloUnidades`, `Clave`, `Grupo`, el `Orden` después de bajar y volver a subir un grupo (vuelve a
1 y 2, como en Legacy), el `IDUsuario`, y los `KmRecorridos`, `LitrosReales` y `KmLitro` que
calcula el SP de gasolinas.

La segunda tanda (gasolinas y cierre de contrato) salió **idéntica incluida la auditoría**, y va
aparte por un detalle de esta base: `dbo.UsuarioWeb` **no tiene columna que ligue con
`dbo.Usuarios` de Mac31**, así que cualquier token emitido aquí lleva `idUsuario = 0`, y el
controlador de Legacy **rechaza `IDUsuario = 0`** en esas rutas
(`GasolinasController.cs:39`). Por eso en esa foto no van `IDUsuario` ni `Equipo`: no se pueden
hacer coincidir en esta máquina. Lo que se compara es el dato.

### Pruebas automatizadas

```bash
dotnet test ISL_Service/ISL_Service/ISL_Service.Tests/ISL_Service.Tests.csproj
```

→ **175 pruebas, 0 fallos** (60 nuevas, en cuatro archivos):

| archivo | qué cubre |
|---|---|
| `Ola5LegacySpTests.cs` | el saneo del apóstrofo (fuera de los filtros, dentro de los datos), las fechas ISO sin tocar `Constantes`, las listas con `~` y punto decimal invariante, el renglón `(result, mensaje)`, y las columnas que no salen al cliente |
| `Ola5ObraReglasTests.cs` | las reglas de obra, **incluida la que prueba que dar de baja un concepto ya no le borra la clave, el precio ni la cantidad** |
| `Ola5AviacionReglasTests.cs` | la **tabla de sincronización por operación** (cuatro pruebas, una por forma), los porcentajes de tarjeta que tienen que sumar 100, y que pedir una recaudación devuelva una |
| `Ola5CobranzasYGasolinasTests.cs` | las reglas de cobranzas y gasolinas |

---

## Defectos de Legacy que NO se heredaron

### 1. "Eliminar" un concepto de contrato le borraba los datos — el peor del lote

`PUT /api/mccontratos/conceptos/eliminar` valida **únicamente** que venga el
`IDContratoConcepto` (`MCContratoConceptosController.cs:95-100`) y después llama al **mismo**
`updateAsync` que la modificación normal. El SP recibe entonces:

```
@IDStatus = 0        (lo que traiga el cuerpo, y el cuerpo no lo trae)
@Clave = ''
@Concepto = ''
@PrecioUnitario = 0
@Cantidad = 0
```

y hace `UPDATE ... SET Clave = @Clave, Concepto = @Concepto, PrecioUnitario = @PrecioUnitario,
Cantidad = @Cantidad, Total = @Total`. O sea que **borrar un concepto le borraba la clave, la
descripción, el precio unitario y la cantidad**, y lo dejaba en `IDStatus = 0`, que no es ni
activo ni cancelado: no sale en ninguna pantalla y **no hay forma de reactivarlo**, porque la
rama de reactivación del SP comprueba `IDStatus = 2`. El renglón se queda en la tabla, mudo, y el
catálogo del contrato ya no suma lo que sumaba.

El nuevo (`PUT /api/obra/contratos/conceptos/{id}/baja`) lee el concepto, lo reenvía tal como
está y solo le cambia el status a 2. Dos viajes a la base, ningún dato perdido, y la reactivación
sigue funcionando. Hay prueba unitaria que lo fija campo por campo.

### 2. Las cinco listas paralelas de una estimación no se comparaban

`sp_n_Actualizar_MCContratoConceptoEstimacion` recibe cinco cadenas pegadas con `~` y las recorre
**a la vez**, emparejando por posición. Legacy solo comprobaba que las dos primeras no estuvieran
vacías (`MCContratoConceptosController.cs:35`) y mandaba las cinco tal como el cliente las
hubiera concatenado: con las cantidades trayendo un elemento menos que los conceptos, **el último
concepto se estimaba con la cantidad de otro, o con cero, sin ningún error**. En una estimación
de obra eso es dinero que se paga mal. El nuevo comprueba que midan lo mismo, que no haya ceros
ni conceptos repetidos, y arma las cadenas con punto decimal invariante.

### 3. Reordenar sin `IDContrato` dejaba dos renglones con el mismo orden

En la rama de reordenar, el SP intercambia el renglón con el que ocupaba la posición destino
usando `WHERE IDContrato = @IDContrato`. Legacy **no exigía `IDContrato`** en `/subir` ni en
`/bajar`, así que al omitirlo el intercambio no ocurría y quedaban **dos renglones con el mismo
`Orden`**, sin error y sin rastro. Ahora es requerido.

De paso: en Legacy el cliente tenía que mandar un `Orden` cualquiera distinto de cero y el
controlador lo pisaba con `-1` o `+1` según la ruta. El número ya no viaja: la ruta es
`/orden` con `direccion: "subir" | "bajar"`. Un delta de 3 mandado por error hacía lo mismo que
omitir el contrato.

### 4. "Si viene el ID se ignoran los demás filtros", cinco veces más

El patrón que la ola 3 encontró en estimaciones de obra aparece **cinco veces más** en este lote,
y siempre igual: no se ignora un filtro, se **reconstruye el DTO** y se tira todo lo demás.

| dónde | qué se perdía |
|---|---|
| `MCContratoService.cs:44-51` | mandar el número de contrato tiraba los otros ocho filtros y las cuatro fechas |
| `PolizaService.cs:50-56` | mandar `IDPoliza` tiraba el resto |
| `GasolinaService.cs:46-53` | mandar el número de recibo tiraba el auto, el repartidor y las cuatro fechas |
| `ConsultaCobranzasService.cs:50-60` | mandar el folio de cobranza tiraba el resto **y `IDUsuarioActual`**, que es con lo que el SP decide qué cobranzas puede ver quien pregunta |
| `RecaudacionService.cs:51-57` | ver el defecto 10 |

En el nuevo **todos los filtros se combinan**, en los cinco.

### 5. La rama muerta del filtro por folio de venta en cobranzas

```csharp
if      (consultaCobranzasInputDTO.FolioInicialCobranza != "0") { /* folio de COBRANZA */ }
else if (consultaCobranzasInputDTO.FolioInicialCobranza != "0") { /* folio de VENTA   */ }
```

La segunda condición es la primera **repetida** — un copiar y pegar sin cambiar el campo — así
que esa rama **no se ejecuta nunca**. Buscar por folio de venta nunca hizo lo que dice su propio
comentario. En el nuevo no hay ramas, y buscar por folio de venta exige la empresa (el folio se
repite entre empresas; el SP compara `IDEmpresa + Folio`).

### 6. El último día del rango se quedaba fuera de la póliza

Al **generar** una póliza, Legacy le pegaba `" 00:00:00"` también a `FechaFinal`
(`PolizaService.cs:300`), de modo que el último día del rango entraba a medianoche y los cobros
de ese día **se quedaban fuera de la póliza**. El nuevo manda 23:59:59, como en la consulta.

### 7. El `IDStatus = 3` que no era un status

`PUT /api/mccontratos` con `IDStatus = 3` no cambiaba el status: era un **interruptor de modo**
("SOLO Se va a actualizar PartidasIndividuales", `MCContratosController.cs:63`). En esa rama
Legacy además se saltaba la conversión de fechas y le mandaba al SP las cadenas tal como
llegaron; y como el `UPDATE` del SP escribe todos los campos, si el cliente solo quería cambiar
la bandera y no reenviaba el contrato entero, **le borraba la descripción, la ubicación, los
importes y las fechas**. Ahora es su propia ruta
(`PUT /api/obra/contratos/{id}/partidas-individuales`), lee el contrato y reenvía lo que ya está,
y el status vuelve a significar solamente el status.

### 8. Dos cosas distintas compartiendo campo

`PUT /api/catoperadores/cuenta` mandaba `ImporteFianza` al parámetro `@Importe` del SP
(`CatOperadorRepository`, `spActualizarCuenta`). No es la fianza del operador: es el importe de
**esa cuenta** para ese operador. Compartían campo porque el DTO era uno para las siete
operaciones del catálogo. Ahora el campo se llama `importe`.

### 9. Validaciones que miraban el mismo campo dos veces, o ninguna

* `PUT /api/polizas/cheques` valida `NumCheque == 0` **dos veces** (el mismo campo repetido,
  `PolizasController.cs:131`) y **nunca** `NumPoliza`: con `NumPoliza` en 0 el cheque se quedaba
  sin número de póliza y no se podía amarrar con la contabilidad.
* `PUT /api/catcajas` valida `NumCaja`, `NombreCaja` y `EquipoCaja` pero **no `IDCaja`**: con 0,
  el `UPDATE` no tocaba nada y el usuario creía que había guardado.

### 10. Pedir una recaudación devolvía todas

```csharp
if ((recaudacionInputDTO.IDsRecaudacionLst.Count > 0) || (recaudacionInputDTO.IDRecaudacion > 0)) {
    string IDsRecaudacion = string.Join("~", recaudacionInputDTO.IDsRecaudacionLst);
    recaudacionInputDTO = new RecaudacionInputDTO();   // todo al suelo
    recaudacionInputDTO.IDsRecaudacion = IDsRecaudacion;
}
```

Si venía `IDRecaudacion` (un folio) y la **lista** venía vacía —que es el caso normal de "dame
esta recaudación"— la condición se cumple, el DTO se reconstruye, y `IDsRecaudacion` queda en
**cadena vacía**: se pierde el id que se pidió **y** todos los demás filtros. El SP recibe todo
en cero y devuelve **TODAS las recaudaciones de la base**. Pedir una, recibir todas. Hay prueba
unitaria que lo fija.

### 11. La cancelación de gasolinas perdía el resultado de cada recibo

`sp_n_CancelarGasolina` **no** contesta con el `(result, mensaje)` del resto de la familia: arma
una tabla `#GasolinaResultado(IDGasolina, NumeroRecibo, resultado)` y la devuelve, con un renglón
por recibo y su texto ("EL RECIBO 99990003 HA SIDO CANCELADO."). En una cancelación de varios eso
es justo lo que hay que leer: cuáles se cancelaron y cuáles no. Legacy lo devolvía, pero como
`List<DataRow>` crudos, así que por el cable salían las propiedades del objeto:

```json
"tablaLst":[{"RowError":"","RowState":2,"Table":[{...}],"ItemArray":[38,99990003,"EL RECIBO ..."],"HasErrors":false}]
```

y el cliente tenía que leer los valores **por posición** dentro de `ItemArray`. La primera
versión de este endpoint lo aplanaba a un `{id, mensaje}` y **perdía el reporte**; se corrigió al
verlo en la comparación. Ahora:

```json
"data":{"recibos":[{"idGasolina":38,"numeroRecibo":99990003,"resultado":"EL RECIBO 99990003 HA SIDO CANCELADO."}],"total":1}
```

### 12. La fecha contra la que se mide el vencimiento venía en el cuerpo

El reporte de cobranza pendiente necesita saber respecto a qué día está algo vencido, y eso es
`Constantes.FechaOperacion`. Legacy lo pedía **en el cuerpo de la petición**
(`CobranzaPendientesVencidosInputDTO.FechaOperacionFtm`): quien pidiera el reporte podía mandar
la fecha que quisiera y hacer que una venta vencida saliera como pendiente, o al contrario. Ahora
se lee de la base con un `SELECT TOP 1`.

### 13. El `Ejercicio` que reventaba el alta de un contrato

`MCContratoInputDTO.Ejercicio` es un `string` y `@Ejercicio` del SP es un `int`. Con el ejercicio
vacío, `AddWithValue` manda `""` y la conversión falla: **el alta completa del contrato reventaba**
con "Error converting data type varchar to int". Ahora es un `int?` y se valida que sea un año de
cuatro cifras.

### 14. El apóstrofo, al revés de como lo hacía Legacy

Legacy borraba el apóstrofo de **los datos que guarda** (`MCPuestoService.cs:63`) y no lo tocaba
en **los filtros**. Es exactamente al contrario de lo que hace falta:

* los filtros entran a un `EXEC(@Sql)` que arma el `WHERE` concatenando, así que ahí el apóstrofo
  es una inyección y pasarlo como `SqlParameter` **no protege**;
* los datos se guardan con un `INSERT`/`UPDATE` parametrizado de verdad, así que ahí es
  inofensivo — y un puesto llamado "ASESOR DE O'HIGGINS" se guardaba sin el apóstrofo.

El nuevo lo quita en los filtros y lo conserva en los datos, con prueba.

### 15. El 500 por un número de recibo con letras

La columna `Gasolina.NumeroRecibo` es `varchar(100)` y el parámetro del SP también, así que todo
apunta a que admite texto. **No lo admite**: `sp_n_CancelarGasolina` mete los números en una
tabla temporal declarada `#TableNumeroRecibo ( NumeroRecibo int )`. Con una letra, SQL Server
lanza "Conversion failed when converting the varchar value 'X' to data type int" y eso sale como
un 500 con el mensaje de la base: el usuario lee un error de SQL y no sabe que el problema es su
número de recibo. **Comprobado contra `Produccion_svr`**: el 500 sale de verdad. Ahora se
comprueba antes y se contesta un 400 que lo dice.

---

## Reglas nuevas: lo que el negocio exige y Legacy no miraba

Ninguna cambia lo que el SP hace: evitan que entre a la base un renglón que no sirve para nada.

1. **Un residente tiene que ser de obra o de supervisión.** `sp_n_Consultar_MCResidentes` filtra
   por esas dos banderas, y así se llenan los dos desplegables de contrato. Con las dos en cero,
   el residente existe, ocupa sitio en el catálogo y **no se puede elegir nunca**.
2. **Los porcentajes de tarjeta tienen que sumar 100.** Es la tabla con la que se reparte un
   cobro con tarjeta entre varias cuentas. Si suman 90, el 10 % del dinero de cada cobro no se
   asigna a ninguna cuenta; si suman 110, se asigna dos veces. En los dos casos la contabilidad
   cuadra mal y nadie se entera hasta el cierre.
3. **Un económico tiene que pagar al menos una cosa** (recaudación, amortización o inscripción).
   Con las tres banderas en cero, la unidad sale en el catálogo y la caja no puede cobrarle.
4. **Reasignar un económico al mismo permisionario no es reasignar.** El SP escribe el movimiento
   en el histórico y rehace las amortizaciones: queda un renglón que dice que la unidad cambió de
   dueño sin cambiar de dueño, y las amortizaciones se reinician por nada.
5. **Un motivo de fianza es de ingreso o de egreso, y solo uno.** Con las dos banderas en cero no
   sale en ninguno de los dos desplegables; con las dos en uno sale en los dos y el mismo
   movimiento puede sumar o restar a la fianza según por dónde se capture. Y es **irreversible**:
   `sp_n_ActualizarMotivoFianza` no recibe esas banderas.
6. **Un operador necesita fecha de alta** y un recibo de gasolina no puede ser del futuro.
7. **El total tiene que cuadrar.** En un contrato, `subtotal + IVA = total` (con un centavo de
   tolerancia); en un cobro de recaudación, `importeCuentas` = la suma de los importes de las
   cuentas y `numCuentas` = cuántas son. El SP guarda los tres números tal como llegan y no
   recalcula ninguno: un total que no es el de sus renglones descuadra el corte del día, y la
   diferencia se le carga al cajero.
8. **El status solo puede ser 1 o 2.** El 0 por omisión de Legacy dejaba el renglón en un status
   que no existe: invisible en todas las pantallas y sin forma de reactivarlo.
9. **Una cuenta no puede ser globalizada e individual a la vez**, y `debeHaber` solo admite `D` o
   `H`.
10. **Una póliza manda fecha de cheques o día del mes, no las dos.** El SP usa uno o el otro y
    cuál gana depende del orden de sus `IF`: un cheque con fecha equivocada no lo paga el banco.
11. **Cambiar una cobranza sin pedir nada no se manda**, y un renglón no se quita y se devuelve a
    la vez. Con las tres listas vacías el SP corría su transacción entera, reescribía el
    repartidor y dejaba bitácora, para nada — un camino fácil de pisar el repartidor de una
    cobranza sin querer.
12. **El cero no viaja en las listas de ids.** Estos SP pegan la lista y la parten dentro; un 0 se
    cuela en el `IN` sin cambiar nada visible. Es el mismo defecto que la ola 3 encontró en la
    lista de marcas de productos.

---

## Lo que el backend nuevo arregla

1. **`ACamelCase` convertía `IDContrato` en `205Contrato`.** `Utils/Funciones.cs` tenía escrito

   ```csharp
   return char.ToLowerInvariant(nombre[0]) + char.ToLowerInvariant(nombre[1]) + nombre.Substring(2);
   ```

   y en C# `char + char` **no concatena: suma los códigos**. `'i' + 'd'` da 205, y el
   `int + string` que viene después producía `"205Contrato"`. **Cualquier columna que empiece por
   dos mayúsculas** —`IDContrato`, `IDCliente`, `IDPoliza`, `RFC`— salía al cliente con un número
   pegado delante. No se había notado porque los SP que usaban `DataTableToRows` hasta hoy
   devuelven sus columnas ya en camelCase y la función sale antes por el
   `if (!char.IsUpper(nombre[0]))`; los de obra y aviación las devuelven en Pascal, y ahí se
   destapó. Arreglado con interpolación y con prueba. **Ojo al mezclar: esto afecta al contrato
   JSON de cualquier endpoint que use `DataTableToRows`**, incluidos los 37 de la ola 3 — pero
   solo para bien: antes esas claves no eran utilizables.
2. **La columna que el SP manda dos veces ya no sale duplicada.** `sp_n_ConsultarGasolinas`
   selecciona `G.Equipo` **dos veces** (y lo mismo `FechaModificacion` y `FechaCancelacion`): un
   copiar y pegar dentro del procedimiento. El `DataTable` renombra la segunda a `Equipo1` y
   habría salido al cliente un `equipo1` que no significa nada.
3. **Los campos que Legacy declaraba y el SP nunca manda.** El mapeo por nombre los dejaba con su
   valor por omisión y salían al cliente como dato: `IDStatus`, `UsuarioModificacion` y
   `UsuarioCancelacion` en gasolinas; `EsFacturaParaPago` y `FacturaParaPago` en cobranzas;
   `Cargos` en el detalle de cobranza. Seis campos que nunca traían nada. Fuera.
4. **Los campos que el SP manda y Legacy perdía.** En el detalle de una cobranza son **siete**:
   `inicialCascos`, `inicialCreditosZ`, `inicialImporteZ`, `inicialSaldoCascos`,
   `inicialSaldoFavor`, `inicialSaldoFavorCascos` e `inicialSaldoImporte`. Son los saldos de
   arranque del renglón: lo que el cliente ya debía o tenía a favor, en dinero y en cascos, antes
   de esta cobranza. El DTO de Legacy no los declaraba y el mapeo por nombre los tiraba en
   silencio.
5. **El `-1` de los SP ya no llega como un 200.** Estos `sp_n_` no lanzan `RAISERROR`: cuando algo
   sale mal contestan `SELECT -1 AS result, 'ERROR' AS mensaje` y devuelven 200. Legacy pasaba ese
   renglón tal cual al cliente, así que un alta que no se guardó llegaba al front como una
   respuesta exitosa con un `-1` adentro, y la pantalla tenía que acordarse de mirarlo. Ahora es
   un 409 con el texto del procedimiento.
6. **La identidad ya no viene del cuerpo.** `IDUsuario` y `Equipo` (y `IDUsuarioCancelo` /
   `EquipoCancelo` en cobranzas, y `IDUsuarioActual` en la consulta de cobranzas) salen del token.
   Con Legacy, cualquiera podía cancelar una cobranza a nombre de otro y dejar su nombre en la
   bitácora, o ver las cobranzas de otro mandando su id.
7. **Las consultas de este lote ya no pueden cerrar el día del negocio.** Ver la sección
   siguiente.

---

## La consulta volvió a ser consulta

La ola 3 dejó anotado que **21 de sus endpoints se cayeron por arrastrar
`sp_n_ConsultaConstantes`**, que no consulta: es la rutina de cierre del día del negocio (inserta
en `FechaOperacionLog`, borra y rehace `AlmacenProductoHist` y adelanta
`Constantes.FechaOperacion`). Lo arrastraban solo para leer `formatoFechaDB` y poder voltearle el
día y el mes a los filtros de fecha con `Funciones.ConvertFtmDate`.

**Este lote recupera ese trabajo para sus propias consultas.** Las fechas llegan tipadas
(`DateOnly`) y se escriben en ISO con la `T`, que SQL Server interpreta igual con cualquier
`DATEFORMAT`:

```
"2026-03-07T00:00:00"   el principio del día
"2026-03-07T23:59:59"   el final
```

Así que **ninguna** consulta de obra, aviación, gasolinas o cobranzas lee `Constantes` ni llama a
`sp_n_ConsultaConstantes`, y por tanto ninguna puede rodar la fecha de operación de la empresa.
Son **once** consultas que pasan de `escribe = si` a lectura de verdad:

`/api/obra/contratos`, `/api/obra/convenios`, `/api/gasolinas`, `/api/cobranzas`,
`/api/cobranzas/pendientes-vencidos`, `/api/aviacion/polizas`,
`/api/aviacion/recaudaciones/consultar`, `/api/aviacion/recaudaciones/recurrentes/consultar`,
`/api/aviacion/recaudaciones/cortes`, `/api/aviacion/cuentas`, `/api/aviacion/economicos`.

De paso desaparece un 400 con stack trace: `ConvertFtmDate` hace `Substring(3,2)` sobre la cadena
y con la fecha vacía lanzaba.

Lo único que sigue leyendo `Constantes` es **lo que de verdad la necesita**, y lo hace con un
`SELECT TOP 1` que no escribe nada:

* `AviacionDBServer` y `AviacionIDCaja`, para saber a qué base hablarle y si hay replica que
  sincronizar (`ConexionDeAviacion`);
* `FechaOperacion`, para el reporte de cobranza pendiente y vencida (`CobranzasRepository`).

---

## Diferencias intencionales

1. **POST → GET** en las consultas: en Legacy eran POST porque el token iba dentro del JSON.
   **Dos excepciones a propósito**: la consulta de recaudaciones y la de recurrentes siguen siendo
   POST, porque su filtro son 31 campos con seis listas de ids dentro y eso no cabe en una query
   string sin volverse ilegible. Es la misma decisión que toma el resto del backend nuevo para las
   consultas de ventas.
2. **Fuera las columnas `*Ftm`.** Cada fecha y cada importe venían dos veces: el valor y su texto
   ya formateado por la base (`FORMAT(C.Fecha, 'dd-MM-yyyy')`). Eso lo armaba la base para las
   rejillas de WinForms; un front que recibe el valor lo formatea con la configuración de quien
   mira la pantalla, y uno que recibe el texto ya no puede.
3. **Fuera `@Formato`.** Es el interruptor "0 pantalla / 1 Excel" del **mismo** SP: con 1 devuelve
   otras columnas, con los encabezados en español y con espacios. El Excel lo arma el front con
   los mismos datos; dos formas de la misma respuesta son dos contratos que se desincronizan.
4. **Las dos rutas de aviación son un endpoint con `central=true|false`.** Legacy tenía
   `/consultar` y `/consultar/server` para cada catálogo: la primera pregunta a la réplica local
   de la caja, la segunda al servidor central. Es la misma pregunta contra dos orígenes. Las
   **escrituras van siempre al central**, igual que en Legacy (allá la lectura degrada a local a
   propósito con un `if (AviacionDBServer != "")` que la escritura no tiene).
5. **`@Params` de los tres reportes de póliza se arma en el servidor.** Esos SP reciben un solo
   parámetro: una cadena con los valores pegados, que Legacy recibía ya concatenada del cliente y
   reenviaba sin mirarla. Como los tres SP no están en esta base, su orden de parámetros —que es
   el contrato— no se puede leer; el endpoint recibe la **lista** de valores, comprueba que
   ninguno traiga el delimitador y los pega con `~`. Es exactamente lo que Legacy reenviaba, menos
   la posibilidad de que el cliente rompa el delimitador o escriba `12,5` en vez de `12.5`.
   Cuando la base esté disponible hay que tipar campo por campo.
6. **`equipo` viaja en el cuerpo en un solo endpoint: las impresoras.** Ahí no es la firma de
   quien hace el cambio, es la **llave** del renglón: de qué computadora son las impresoras. Se
   deduce de que `sp_n_ConsultaImpresoras` lo recibe como filtro y de que
   `sp_n_ActualizarImpresora` no recibe ningún otro identificador de renglón. La identidad del
   usuario sigue saliendo del token. Está anotado en el controlador, en el DTO y en su caso del
   comparador para que se revise cuando la base esté disponible.
7. **Reordenar y cerrar son rutas propias, no campos.** `/orden` con `direccion`, `/status` con
   `contratoCerrado`, `/baja`, `/aceptar`, `/partidas-individuales`. En Legacy todo eso pasaba por
   el `PUT` de modificación con un valor mágico (un `Orden` distinto de cero, un `IDStatus = 3`),
   y el modo que el SP tomaba dependía de un campo que parecía un dato.
8. **Los campos que el SP recibe y no usa no se exponen.** `@Identico` donde el SP lo ignora,
   `@IDPermisionario` en el siguiente número de cuenta de permisionario (Legacy le mandaba la
   cadena `"0"` literal), las banderas de sentido en la modificación de un motivo de fianza (el SP
   no las recibe). Ofrecerlos sería mentir sobre lo que hace la base.
9. **Las escrituras devuelven `{ id, mensaje }`**, donde `id` es el nuevo en un alta y el renglón
   tocado en una modificación. La excepción es la cancelación de gasolinas, que devuelve un
   renglón por recibo porque el SP contesta así.
10. **El `result`/`mensaje` de cada renglón sale de las consultas.** Era el hueco de error de los
    SP de escritura; en una consulta vale 0/null siempre.
11. **`api/aviacion/recaudaciones` y no `api/recaudaciones`**, porque esa ya existe en este
    backend y es otra cosa: el ticket anónimo por QR. Colgar aquí siete rutas más habría juntado
    dos módulos que no se parecen —uno público y de solo lectura, el otro que cobra dinero— bajo
    el mismo prefijo.
12. **`cobranzas.*` y no `ventas.cobranzas.*`.** Ver la sección de permisos: es una trampa del
    puente con Mac31.

---

## Permisos nuevos

**64**, todos en `PermissionService.PermissionCatalogSeeds`. Cuatro familias nuevas (`obra`,
`aviacion`, `gasolinas`, `cobranzas`).

**Ninguna cae en las cuatro familias que gobierna Mac31** (`ventas.*`, `reportes.*`,
`empleados.*`, `prestamos.*`), y eso es a propósito. Para esas cuatro, el puente de permisos
**borra del snapshot lo que diga el web** y lo recalcula desde los botones de la forma de Mac31
(§ 2.8 del resumen de arquitectura). Una clave `ventas.cobranzas.crear` se habría guardado en
`WUsuarioPermiso` y **habría desaparecido en el siguiente cálculo, sin error y sin rastro**. Por
eso cobranzas se llama `cobranzas.*`.

| familia | claves | por qué están separadas |
|---|---|---|
| `obra.catalogos.*` | `crear`, `editar` | dar de alta un tipo de obra y cambiarle el nombre a uno ya usado por contratos firmados no son el mismo riesgo |
| `obra.partidas.*` | `ver`, `crear`, `editar` | el renglón trae el **monto autorizado** del presupuesto, no solo un nombre |
| `obra.proveedores.*` | `crear`, `editar` | el renglón trae RFC, CURP, teléfono y correo: es dato personal |
| `obra.contratos.*` | `crear`, `editar`, `cerrar` | cerrar es lo que deja de admitir estimaciones |
| `obra.convenios.*` | `ver`, `crear`, `editar` | un convenio modifica el monto contratado |
| `obra.estimaciones.editar` | | capturar cantidades estimadas es **autorizar un pago**; no lo hace quien mantiene el catálogo de conceptos |
| `aviacion.ver_modulo` | | la tarjeta del módulo en el inicio |
| `aviacion.{cajas,conceptos,cuentas,economicos,fianzas,operadores,permisionarios,impresoras}.*` | `ver`/`crear`/`editar` | no es la misma gente: quien mantiene el catálogo de cuentas (que define cuánto se cobra) no es quien da de alta operadores |
| `aviacion.{cuentas,economicos,operadores,permisionarios}.aceptar` | | "aceptar" convierte un alta hecha en una caja en un renglón bueno del central: lo hace quien supervisa, no quien captura |
| `aviacion.economicos.reasignar` | | cambia de dueño una unidad y deja rastro en el histórico |
| `aviacion.polizas.*` | `ver`, `generar`, `cancelar`, `editar` | generar una póliza emite los cheques con los que se paga; corregir un número de cheque es otra cosa y la hace quien imprime |
| `aviacion.recaudaciones.*` | `ver`, `cobrar`, `cancelar`, `cortar` | en una caja son tres personas distintas: el cajero cobra, el supervisor cancela, y el corte lo hace quien entrega el dinero |
| `gasolinas.*` | `ver_modulo`, `ver`, `crear`, `editar`, `cancelar` | cancelar borra un gasto ya capturado del control del repartidor |
| `cobranzas.*` | `ver_modulo`, `ver`, `crear`, `editar`, `cancelar`, `pendientes.ver` | |

**Cuatro claves se declaran aquí y también las siembra la ola 3**: `obra.ver_modulo`,
`obra.catalogos.ver`, `obra.contratos.ver` y `obra.proveedores.ver`. Están porque los endpoints de
este lote las usan y sin la fila en `WPermiso` nadie puede tenerlas; sembrar dos veces la misma
clave es inofensivo (`SincronizarCatalogo` salta la que ya existe, `PermissionExistsAsync`), pero
**al mezclar las dos ramas hay que dejar UNA sola línea por clave**. Están marcadas en el código
con `// (tambien de la ola 3)`.

No se corrió ningún script contra la base: los permisos quedan sembrados en código y entran a
`WPermiso` cuando alguien llame a la sincronización del catálogo desde la pantalla de permisos.

---

## Pendientes que quedan anotados

1. **`registro.tsv` no se tocó.** Las 119 filas siguen en `falta`/`parcial`/`por-verificar`. Hay
   que pasarlas a `hecho`, con `ubicacion_nueva` = la ruta nueva de la tabla de arriba,
   `metodo_validacion` = `comparador` + `instantanea`, y `resultado` = `OK` o `no verificable`.
   Varios agentes escriben ese archivo a la vez y reescribirlo entero podría pisar sus filas.
2. **Este `migracion/` es el del worktree.** La carpeta `migracion/` no está en git y el agente
   trabaja en un worktree aislado, así que estos archivos —este documento, los once casos del
   comparador y `comparador/escrituras-ola5/`— están en
   `.claude/worktrees/agent-.../migracion/` y hay que **copiarlos a la carpeta `migracion/` del
   checkout compartido**.
3. **Las 88 no verificables** necesitan las dos bases de § No verificables. Los siete casos de
   `casos/no-verificables/` ya traen la pregunta armada para los dos lados.
4. **Estas consultas no tienen paginado.** Sin filtros, cobranzas devuelve 39 760 renglones
   (23 MB) y gasolinas 2554 (2.0 MB). Pedir las dos en la misma corrida **tiró el backend por
   memoria** durante las pruebas. Legacy tiene el mismo problema (y peor: 2.2 MB y 28 MB), así que
   no es una regresión, pero el front va a necesitar un tope. Es una decisión de contrato y no se
   tomó aquí.
5. **Las escrituras de cobranzas no se ejecutaron contra datos reales de venta.** Armar una
   cobranza necesita folios de venta vivos y habría dejado una cobranza colgando o habría fallado
   por la misma razón en los dos lados. Los tres SP están instalados con la firma que espera
   Legacy, el armado de parámetros está cubierto con pruebas, y el ciclo de instantánea está listo:
   es una tanda corta cuando se decida qué venta se usa de conejillo.
6. **El `@Params` de los tres reportes de póliza** (diferencia 5) y el **`@Equipo` de impresoras**
   (diferencia 6) son las dos decisiones de este lote que hay que confirmar leyendo el SP cuando
   la base de aviación esté disponible. Las dos están marcadas en su caso del comparador.
7. **La instantánea es compartida y hoy no se respeta.** Durante estas pruebas, otro agente
   restauró `Produccion_svr` **tres veces** en medio de una tanda, lo que invalidó dos corridas y
   dejó al backend con el pool de conexiones envenenado (`Login failed for user 'sa'` hasta
   reiniciarlo). Se tomó y se soltó `/tmp/instantanea-en-uso-5083` como dice el protocolo, pero el
   candado solo sirve si lo miran todos. Vale la pena que `instantanea.sh volver` **falle** si hay
   un candado de otro puerto, en vez de confiar en que cada agente lo consulte.
