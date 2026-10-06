# Inventario del Legacy `Mac3Servicios` (ASP.NET Web API 2 / .NET 4.7.1)

Fuente: `D:\JuanD\GitHub\MAC\Legacy\MacServicios2\Mac3Servicios`. 
Detalle fila por fila en `inventario-legacy.tsv` (una fila por metodo publico de controlador).

## Totales

| Concepto | Total |
|---|---|
| Archivos de controlador (`Controllers/**/*.cs`) | 132 |
| Controladores con al menos un metodo publico | 130 |
| Metodos publicos (endpoints) | 488 |
| Stored procedures distintos (con nombre conocido) | 447 |
| Mas: SPs de reporte resueltos en tiempo de ejecucion | ?dinamico, nombre en la tabla de `sp_ConsultaReportes` |
| Metodos que escriben | 278 |
| Metodos de solo lectura | 210 |

Los 2 archivos de controlador sin metodos publicos son:

- `Controllers/LogImpresionesWebController.cs` — el unico metodo esta comentado (`////public async ...`); la clase queda vacia.
- `Controllers/Pedidos/PedidoUsadosCargoController.cs` — clase declarada sin ningun metodo.

Verbos: DELETE=28, POST=349, PUT=111.
No hay rutas duplicadas (ningun par verbo+ruta se repite).

## Tipo de token

- `Todos`: 481 metodos
- `ninguno`: 7 metodos

**Hallazgo:** las 463 llamadas a `validateJwtTokenAsync` del proyecto usan **siempre** `Enums.enumToken.Todos`. 
Los valores `App`, `Cliente`, `Usuario`, `ClienteUsuario` y `AppUsuario` del enum existen y estan implementados en 
`Security/JwtTokenGenerator.cs`, pero **ningun endpoint los pide**. En la practica no hay autorizacion por tipo de 
token ni por permiso: cualquier token valido abre cualquier endpoint.

Los 7 metodos con `tipo_token = ninguno` son los 6 catalogos de Facturama (`FacturamaController`) y 
`VentasController.PostFacturaEnviarCorreo`.

## Stored procedures distintos (447 con nombre conocido + 1 dinamico)

- `?dinamico(columna SP de sp_ConsultaReportes)` (4 endpoints)
- `sp_ConsultaReportes` (4 endpoints)
- `sp_n_AbrirCobro` (1 endpoint)
- `sp_n_AceptarCuenta` (1 endpoint)
- `sp_n_AceptarEconomico` (1 endpoint)
- `sp_n_AceptarOperador` (1 endpoint)
- `sp_n_AceptarPermisionario` (1 endpoint)
- `sp_n_ActualizaEmpleadoPrestamos` (1 endpoint)
- `sp_n_Actualizar_MCContrato` (1 endpoint)
- `sp_n_Actualizar_MCContratoConcepto` (4 endpoints)
- `sp_n_Actualizar_MCContratoConceptoEstimacion` (1 endpoint)
- `sp_n_Actualizar_MCContratoEstimacion` (1 endpoint)
- `sp_n_Actualizar_MCContratoGrupo` (3 endpoints)
- `sp_n_Actualizar_MCContratoPartida` (3 endpoints)
- `sp_n_Actualizar_MCContratoPeriodo` (1 endpoint)
- `sp_n_Actualizar_MCContratoStatus` (1 endpoint)
- `sp_n_Actualizar_MCConvenio` (1 endpoint)
- `sp_n_Actualizar_MCConvenioStatus` (1 endpoint)
- `sp_n_Actualizar_MCPartida` (1 endpoint)
- `sp_n_Actualizar_MCProveedor` (1 endpoint)
- `sp_n_Actualizar_MCPuesto` (1 endpoint)
- `sp_n_Actualizar_MCResidente` (1 endpoint)
- `sp_n_Actualizar_MCTipoContratacion` (1 endpoint)
- `sp_n_Actualizar_MCTipoDocumento` (1 endpoint)
- `sp_n_Actualizar_MCTipoObra` (1 endpoint)
- `sp_n_Actualizar_MCTipoRecurso` (1 endpoint)
- `sp_n_Actualizar_MCUnidadMedida` (1 endpoint)
- `sp_n_Actualizar_Proyecto` (1 endpoint)
- `sp_n_Actualizar_Proyecto_Facturas` (1 endpoint)
- `sp_n_Actualizar_TipoDomicilio` (1 endpoint)
- `sp_n_ActualizarAgente` (2 endpoints)
- `sp_n_ActualizarAlmacen` (2 endpoints)
- `sp_n_ActualizarAuto` (1 endpoint)
- `sp_n_ActualizarAviacionTarjetaPorcentaje` (1 endpoint)
- `sp_n_ActualizarBanco` (2 endpoints)
- `sp_n_ActualizarCaja` (1 endpoint)
- `sp_n_ActualizarCambioVigencia` (1 endpoint)
- `sp_n_ActualizarCategoria` (2 endpoints)
- `sp_n_ActualizarCatUnidadTerritorial` (1 endpoint)
- `sp_n_ActualizarCentroServicio` (2 endpoints)
- `sp_n_ActualizarCiudad` (2 endpoints)
- `sp_n_ActualizarCliente` (2 endpoints)
- `sp_n_ActualizarClienteBonificacion` (1 endpoint)
- `sp_n_ActualizarClienteCentroServicio` (1 endpoint)
- `sp_n_ActualizarClienteFacturacion` (1 endpoint)
- `sp_n_ActualizarClienteFacturacionMostrador` (1 endpoint)
- `sp_n_ActualizarCobranzaDetalle` (1 endpoint)
- `sp_n_ActualizarCobro` (1 endpoint)
- `sp_n_ActualizarCobroDineroUsado` (1 endpoint)
- `sp_n_ActualizarCobroDineroUsadoCheques` (1 endpoint)
- `sp_n_ActualizarColonia` (2 endpoints)
- `sp_n_ActualizarCompra` (1 endpoint)
- `sp_n_ActualizarConcentradoDetalle` (1 endpoint)
- `sp_n_ActualizarConcepto` (1 endpoint)
- `sp_n_ActualizarCondicionCredito` (2 endpoints)
- `sp_n_ActualizarConstante` (1 endpoint)
- `sp_n_ActualizarCuenta` (1 endpoint)
- `sp_n_ActualizarDescuentos` (1 endpoint)
- `sp_n_ActualizarDescuentosDetalle` (1 endpoint)
- `sp_n_ActualizarDiaFestivo` (2 endpoints)
- `sp_n_ActualizarDocumento` (2 endpoints)
- `sp_n_ActualizarEconomico` (1 endpoint)
- `sp_n_ActualizarEconomicoAmortizacion` (1 endpoint)
- `sp_n_ActualizarEmpleado` (1 endpoint)
- `sp_n_ActualizarEmpresa` (2 endpoints)
- `sp_n_ActualizarEmpresaSerie` (2 endpoints)
- `sp_n_ActualizarFechaOperacion` (1 endpoint)
- `sp_n_ActualizarGallero` (1 endpoint)
- `sp_n_ActualizarGarantia` (1 endpoint)
- `sp_n_ActualizarGasolina` (1 endpoint)
- `sp_n_ActualizarGrupoCategoria` (2 endpoints)
- `sp_n_ActualizarHuella` (1 endpoint)
- `sp_n_ActualizarImpresora` (1 endpoint)
- `sp_n_ActualizarMarca` (2 endpoints)
- `sp_n_ActualizarMCGerente` (1 endpoint)
- `sp_n_ActualizarMotivoFianza` (1 endpoint)
- `sp_n_ActualizarMunicipio` (2 endpoints)
- `sp_n_ActualizarOperador` (1 endpoint)
- `sp_n_ActualizarOperadorCuenta` (1 endpoint)
- `sp_n_ActualizarOperadorFianza` (1 endpoint)
- `sp_n_ActualizarParametro` (3 endpoints)
- `sp_n_ActualizarPedido` (1 endpoint)
- `sp_n_ActualizarPermisionario` (1 endpoint)
- `sp_n_ActualizarPolizaCheque` (1 endpoint)
- `sp_n_ActualizarPolizaEncabezado` (1 endpoint)
- `sp_n_ActualizarProducto` (2 endpoints)
- `sp_n_ActualizarProductoFile` (1 endpoint)
- `sp_n_ActualizarProveedor` (2 endpoints)
- `sp_n_ActualizarProveedorApoderado` (1 endpoint)
- `sp_n_ActualizarProveedorDocumento` (1 endpoint)
- `sp_n_ActualizarProveedorDomicilio` (1 endpoint)
- `sp_n_ActualizarRecordatorio` (1 endpoint)
- `sp_n_ActualizarRepartidor` (2 endpoints)
- `sp_n_ActualizarTipoGasto` (2 endpoints)
- `sp_n_ActualizarTipoRecordatorio` (2 endpoints)
- `sp_n_ActualizarTipoUsado` (2 endpoints)
- `sp_n_ActualizarUnidadMedida` (2 endpoints)
- `sp_n_ActualizarUsadosCredito` (1 endpoint)
- `sp_n_ActualizarUsuario` (2 endpoints)
- `sp_n_ActualizarUsuarioActividad` (1 endpoint)
- `sp_n_ActualizarUsuarioAlmacen` (1 endpoint)
- `sp_n_ActualizarUsuarioFecha` (1 endpoint)
- `sp_n_ActualizarUsuarioFormaProceso` (1 endpoint)
- `sp_n_ActualizarVenta` (1 endpoint)
- `sp_n_ActualizarVentasPagoPorKilo` (1 endpoint)
- `sp_n_ActualizaStatusVentasDescuentosAjustes` (1 endpoint)
- `sp_n_AplicarBancoTransferencia` (1 endpoint)
- `sp_n_AplicarBancoTransferenciaUsados` (1 endpoint)
- `sp_n_AplicarCobroDineroUsado` (1 endpoint)
- `sp_n_AplicarVentasNuevosPagos` (1 endpoint)
- `sp_n_CancelaEmpleadoPrestamos` (1 endpoint)
- `sp_n_CancelarAjusteInventario` (1 endpoint)
- `sp_n_CancelarAplicacionCobroDineroCasco` (1 endpoint)
- `sp_n_CancelarBancoTransferencia` (2 endpoints)
- `sp_n_CancelarBancoTransferenciaPago` (1 endpoint)
- `sp_n_CancelarCambioVigencia` (1 endpoint)
- `sp_n_CancelarCobranza` (1 endpoint)
- `sp_n_CancelarCobro` (1 endpoint)
- `sp_n_CancelarCompra` (1 endpoint)
- `sp_n_CancelarConcentrado` (1 endpoint)
- `sp_n_CancelarCorte` (2 endpoints)
- `sp_n_CancelarGarantia` (1 endpoint)
- `sp_n_CancelarGarantiaRegistro` (1 endpoint)
- `sp_n_CancelarGasolina` (1 endpoint)
- `sp_n_CancelarInventarioPedidos` (1 endpoint)
- `sp_n_CancelarPedido` (1 endpoint)
- `sp_n_CancelarPoliza` (1 endpoint)
- `sp_n_CancelarRecaudacion` (1 endpoint)
- `sp_n_CancelarServicio` (1 endpoint)
- `sp_n_CancelarUsadoMovimiento` (1 endpoint)
- `sp_n_CancelarVenta` (1 endpoint)
- `sp_n_CancelarVentaFactura` (2 endpoints)
- `sp_n_CancelarVentasCobroUsados` (1 endpoint)
- `sp_n_CancelarVentasNuevosPagos` (1 endpoint)
- `sp_n_CancelarVentasPagos` (1 endpoint)
- `sp_n_CancelarVentasPagosKilos` (1 endpoint)
- `sp_n_CancelarVentasUsadosExcendentes` (1 endpoint)
- `sp_n_CerrarCobro` (1 endpoint)
- `sp_n_ConcentradoFoliosUsados` (1 endpoint)
- `sp_n_ConsultaAgentes` (1 endpoint)
- `sp_n_ConsultaAgentesDisponibles` (1 endpoint)
- `sp_n_ConsultaAjusteInventario` (1 endpoint)
- `sp_n_ConsultaAlmacenes` (1 endpoint)
- `sp_n_ConsultaAlmacenProducto` (1 endpoint)
- `sp_n_ConsultaAplicacionActualizacion` (1 endpoint)
- `sp_n_ConsultaAutos` (1 endpoint)
- `sp_n_ConsultaAviacionTarjetaPorcentajes` (1 endpoint)
- `sp_n_ConsultaBancos` (1 endpoint)
- `sp_n_ConsultaBancoTranferenciasMovimientos` (1 endpoint)
- `sp_n_ConsultaBancoTransferencias` (1 endpoint)
- `sp_n_ConsultaCajas` (1 endpoint)
- `sp_n_ConsultaCategorias` (1 endpoint)
- `sp_n_ConsultaCategoriasMarcas` (1 endpoint)
- `sp_n_ConsultaCatFEFormasPago` (1 endpoint)
- `sp_n_ConsultaCatFEMetodosPago` (1 endpoint)
- `sp_n_ConsultaCatFEMotivosCancelacion` (1 endpoint)
- `sp_n_ConsultaCatFERegimenesFiscales` (1 endpoint)
- `sp_n_ConsultaCatFERelacionFacturas` (1 endpoint)
- `sp_n_ConsultaCatFEUsosCFDI` (1 endpoint)
- `sp_n_ConsultaCatGruposCliente` (1 endpoint)
- `sp_n_ConsultaCatTiposDocumentos` (1 endpoint)
- `sp_n_ConsultaCatUnidadesTerritoriales` (1 endpoint)
- `sp_n_ConsultaCentrosServicio` (1 endpoint)
- `sp_n_ConsultaCiudades` (1 endpoint)
- `sp_n_ConsultaClientes` (1 endpoint)
- `sp_n_ConsultaColonias` (4 endpoints)
- `sp_n_ConsultaCompras` (1 endpoint)
- `sp_n_ConsultaConceptoNextNumCuenta` (1 endpoint)
- `sp_n_ConsultaConceptos` (1 endpoint)
- `sp_n_ConsultaCondicionesCredito` (1 endpoint)
- `sp_n_ConsultaConstantes` (155 endpoints)
- `sp_n_ConsultaCreditoDisponible` (1 endpoint)
- `sp_n_ConsultaCuentaNextNumCuenta` (1 endpoint)
- `sp_n_ConsultaCuentas` (2 endpoints)
- `sp_n_ConsultaDescuentos` (1 endpoint)
- `sp_n_ConsultaDescuentosDetalle` (1 endpoint)
- `sp_n_ConsultaDiasFestivos` (1 endpoint)
- `sp_n_ConsultaDocumentos` (1 endpoint)
- `sp_n_ConsultaDomicilios` (1 endpoint)
- `sp_n_ConsultaEconomicoNextID` (1 endpoint)
- `sp_n_ConsultaEconomicoNextNumCuenta` (1 endpoint)
- `sp_n_ConsultaEconomicos` (2 endpoints)
- `sp_n_ConsultaEconomicosAuxiliares` (1 endpoint)
- `sp_n_ConsultaEmpleadoPrestamos` (1 endpoint)
- `sp_n_ConsultaEmpleados` (1 endpoint)
- `sp_n_ConsultaEmpleadosAsistenciaDiaria` (1 endpoint)
- `sp_n_ConsultaEmpleadosPeriodoAsistencia` (2 endpoints)
- `sp_n_ConsultaEmpleadosTipoSueldo` (1 endpoint)
- `sp_n_ConsultaEmpleadosTipoSueldoRecibo` (3 endpoints)
- `sp_n_ConsultaEmpleadosTipoSueldoReciboZara` (2 endpoints)
- `sp_n_ConsultaEmpresas` (1 endpoint)
- `sp_n_ConsultaEmpresasSeries` (1 endpoint)
- `sp_n_ConsultaEstados` (1 endpoint)
- `sp_n_ConsultaFormas` (2 endpoints)
- `sp_n_ConsultaGalleros` (1 endpoint)
- `sp_n_ConsultaGrupoCategorias` (1 endpoint)
- `sp_n_ConsultaGrupoCategoriasMarcas` (1 endpoint)
- `sp_n_ConsultaGrupoCategoriasMarcasRelacion` (1 endpoint)
- `sp_n_ConsultaHuellas` (1 endpoint)
- `sp_n_ConsultaImpresoras` (1 endpoint)
- `sp_n_ConsultaMarcas` (1 endpoint)
- `sp_n_ConsultaMotivoFianzaNextID` (1 endpoint)
- `sp_n_ConsultaMotivosFianza` (2 endpoints)
- `sp_n_ConsultaMunicipios` (1 endpoint)
- `sp_n_ConsultaOperadores` (2 endpoints)
- `sp_n_ConsultaOperadorNextNumCuenta` (1 endpoint)
- `sp_n_ConsultaParametros` (5 endpoints)
- `sp_n_ConsultaPedido` (3 endpoints)
- `sp_n_ConsultaPedidoFaltante` (1 endpoint)
- `sp_n_ConsultaPeriodosTipoSueldo` (3 endpoints)
- `sp_n_ConsultaPeriodosTipoSueldoDias` (2 endpoints)
- `sp_n_ConsultaPermisionarioNextNumCuenta` (1 endpoint)
- `sp_n_ConsultaPermisionarios` (2 endpoints)
- `sp_n_ConsultaPolizaCheques` (1 endpoint)
- `sp_n_ConsultaPolizaDetalle` (1 endpoint)
- `sp_n_ConsultaPolizaEncabezados` (1 endpoint)
- `sp_n_ConsultaPolizas` (1 endpoint)
- `sp_n_ConsultaProductos` (1 endpoint)
- `sp_n_ConsultaProveedorApoderados` (1 endpoint)
- `sp_n_ConsultaProveedorDocumentos` (1 endpoint)
- `sp_n_ConsultaProveedorDomicilios` (1 endpoint)
- `sp_n_ConsultaProveedores` (1 endpoint)
- `sp_n_ConsultaPuestos` (1 endpoint)
- `sp_n_Consultar_MCContratos` (1 endpoint)
- `sp_n_Consultar_MCContratosConceptos` (1 endpoint)
- `sp_n_Consultar_MCContratosConceptosEstimacionesAcumulado` (1 endpoint)
- `sp_n_Consultar_MCContratosEstimaciones` (1 endpoint)
- `sp_n_Consultar_MCContratosGrupos` (1 endpoint)
- `sp_n_Consultar_MCContratosPartidas` (1 endpoint)
- `sp_n_Consultar_MCContratosPeriodos` (1 endpoint)
- `sp_n_Consultar_MCConvenios` (1 endpoint)
- `sp_n_Consultar_MCGerentes` (1 endpoint)
- `sp_n_Consultar_MCPartidas` (1 endpoint)
- `sp_n_Consultar_MCProveedores` (1 endpoint)
- `sp_n_Consultar_MCPuestos` (1 endpoint)
- `sp_n_Consultar_MCResidentes` (1 endpoint)
- `sp_n_Consultar_MCTiposContratacion` (1 endpoint)
- `sp_n_Consultar_MCTiposDocumento` (1 endpoint)
- `sp_n_Consultar_MCTiposObra` (1 endpoint)
- `sp_n_Consultar_MCTiposRecurso` (1 endpoint)
- `sp_n_Consultar_MCUnidadesMedida` (1 endpoint)
- `sp_n_Consultar_Proyectos` (1 endpoint)
- `sp_n_Consultar_TiposDomicilio` (1 endpoint)
- `sp_n_ConsultarCambiosVigencia` (1 endpoint)
- `sp_n_ConsultarClienteBonificacion` (1 endpoint)
- `sp_n_ConsultarClienteCentroServicio` (1 endpoint)
- `sp_n_ConsultarClientesFacturacion` (3 endpoints)
- `sp_n_ConsultarClientesLog` (1 endpoint)
- `sp_n_ConsultarCobranzaDetalle` (1 endpoint)
- `sp_n_ConsultarCobranzaFolio` (1 endpoint)
- `sp_n_ConsultarCobranzas` (1 endpoint)
- `sp_n_ConsultarCobroClientes` (1 endpoint)
- `sp_n_ConsultarCobroDineroUsadosDetalle` (1 endpoint)
- `sp_n_ConsultarCobros` (1 endpoint)
- `sp_n_ConsultarCobrosCentroServicio` (1 endpoint)
- `sp_n_ConsultarCobrosDineroUsados` (1 endpoint)
- `sp_n_ConsultarCobrosDineroUsadosCheques` (1 endpoint)
- `sp_n_ConsultarCobrosDineroUsadosCortesCS` (1 endpoint)
- `sp_n_ConsultarConcentradoDetalle` (1 endpoint)
- `sp_n_ConsultarConcentradoFolio` (1 endpoint)
- `sp_n_ConsultarConcentrados` (1 endpoint)
- `sp_n_ConsultarCorteFechaUltimo` (1 endpoint)
- `sp_n_ConsultarCortes` (1 endpoint)
- `sp_n_ConsultarCortesCS` (1 endpoint)
- `sp_n_ConsultarCortesCSCobros` (1 endpoint)
- `sp_n_ConsultarCortesDetalle` (1 endpoint)
- `sp_n_ConsultarCortesTipoGastos` (1 endpoint)
- `sp_n_ConsultarCortesVentasCanceladasCS` (1 endpoint)
- `sp_n_ConsultarDescuentosDetalleLog` (1 endpoint)
- `sp_n_ConsultaRecaudaciones` (1 endpoint)
- `sp_n_ConsultaRecaudacionesCortes` (1 endpoint)
- `sp_n_ConsultaRecaudacionesRecurrentes` (1 endpoint)
- `sp_n_ConsultaRecaudacionNextID` (1 endpoint)
- `sp_n_ConsultaRecordatorios` (1 endpoint)
- `sp_n_ConsultaRepartidores` (1 endpoint)
- `sp_n_ConsultarGarantias` (1 endpoint)
- `sp_n_ConsultarGarantiasAceptar` (1 endpoint)
- `sp_n_ConsultarGarantiasRegistro` (1 endpoint)
- `sp_n_ConsultarGasolinas` (1 endpoint)
- `sp_n_ConsultarInventarioPedidos` (1 endpoint)
- `sp_n_ConsultarUsadosMovimientos` (1 endpoint)
- `sp_n_ConsultarUsadosMovimientosDetalle` (1 endpoint)
- `sp_n_ConsultarUsadosMovimientosPagados` (1 endpoint)
- `sp_n_ConsultarVentasCobrosUsados` (1 endpoint)
- `sp_n_ConsultarVentasCobrosUsadosDetalle` (1 endpoint)
- `sp_n_ConsultarVentasPagosCS` (1 endpoint)
- `sp_n_ConsultarVentasPagosKilos` (1 endpoint)
- `sp_n_ConsultarVentasUsadosExcendentes` (1 endpoint)
- `sp_n_ConsultarVentasUsadosExcendentesDetalle` (1 endpoint)
- `sp_n_ConsultaServicio` (1 endpoint)
- `sp_n_ConsultaSincronizacion` (1 endpoint)
- `sp_n_ConsultaTemplateHtml` (7 endpoints)
- `sp_n_ConsultaTipoNotasCredito` (1 endpoint)
- `sp_n_ConsultaTiposGastos` (1 endpoint)
- `sp_n_ConsultaTiposPagos` (1 endpoint)
- `sp_n_ConsultaTiposRecordatorios` (1 endpoint)
- `sp_n_ConsultaTipoSueldos` (1 endpoint)
- `sp_n_ConsultaTiposUsados` (1 endpoint)
- `sp_n_ConsultaUnidadesServicio` (1 endpoint)
- `sp_n_ConsultaUsuarioFormaProcesos` (1 endpoint)
- `sp_n_ConsultaUsuarioNextID` (1 endpoint)
- `sp_n_ConsultaUsuarios` (2 endpoints)
- `sp_n_ConsultaUsuariosAlmacenes` (1 endpoint)
- `sp_n_ConsultaUsuariosFechas` (1 endpoint)
- `sp_n_ConsultaVentas` (1 endpoint)
- `sp_n_ConsultaVentasCobroPagos` (1 endpoint)
- `sp_n_ConsultaVentasDescuentosAjustes` (1 endpoint)
- `sp_n_ConsultaVentasFacturas` (3 endpoints)
- `sp_n_ConsultaVentasNuevosPagos` (1 endpoint)
- `sp_n_ConsultaVentasPagos` (1 endpoint)
- `sp_n_ConsultaVentasPagosLiquidacion` (1 endpoint)
- `sp_n_ConsultaVentasPedidoDetalle` (1 endpoint)
- `sp_n_ConsultaVentasPedidos` (1 endpoint)
- `sp_n_ConsultaVentasPendiente` (1 endpoint)
- `sp_n_ConsultaVentasPendientesImprimir` (1 endpoint)
- `sp_n_ConsultaVentasTransito` (1 endpoint)
- `sp_n_DevolucionVenta` (1 endpoint)
- `sp_n_DevolucionVentaLuro` (1 endpoint)
- `sp_n_EconomicoReasignacion` (1 endpoint)
- `sp_n_EliminarCobroDineroUsadoCortesCS` (1 endpoint)
- `sp_n_EliminarDescuentos` (1 endpoint)
- `sp_n_EliminarDescuentosDetalle` (1 endpoint)
- `sp_n_EliminarEmpleadosAsistencia` (1 endpoint)
- `sp_n_EliminarPedido` (1 endpoint)
- `sp_n_EliminarPedidoDetalle` (1 endpoint)
- `sp_n_GarantiasAceptar` (1 endpoint)
- `sp_n_IdentificarBancoTransferencia` (1 endpoint)
- `sp_n_InsertaEmpleadoPrestamos` (1 endpoint)
- `sp_n_Insertar_MCContrato` (1 endpoint)
- `sp_n_Insertar_MCContratoConcepto` (1 endpoint)
- `sp_n_Insertar_MCContratoConceptoFile` (1 endpoint)
- `sp_n_Insertar_MCContratoEstimacion` (1 endpoint)
- `sp_n_Insertar_MCContratoGrupo` (1 endpoint)
- `sp_n_Insertar_MCContratoPartida` (1 endpoint)
- `sp_n_Insertar_MCContratoPeriodo` (1 endpoint)
- `sp_n_Insertar_MCConvenio` (1 endpoint)
- `sp_n_Insertar_MCPartida` (1 endpoint)
- `sp_n_Insertar_MCProveedor` (1 endpoint)
- `sp_n_Insertar_MCPuesto` (1 endpoint)
- `sp_n_Insertar_MCResidente` (1 endpoint)
- `sp_n_Insertar_MCTipoContratacion` (1 endpoint)
- `sp_n_Insertar_MCTipoDocumento` (1 endpoint)
- `sp_n_Insertar_MCTipoObra` (1 endpoint)
- `sp_n_Insertar_MCTipoRecurso` (1 endpoint)
- `sp_n_Insertar_MCUnidadMedida` (1 endpoint)
- `sp_n_Insertar_Proyecto` (1 endpoint)
- `sp_n_Insertar_TipoDomicilio` (1 endpoint)
- `sp_n_InsertarAgente` (1 endpoint)
- `sp_n_InsertarAjusteInventario` (1 endpoint)
- `sp_n_InsertarAlmacen` (1 endpoint)
- `sp_n_InsertarAuto` (1 endpoint)
- `sp_n_InsertarBanco` (1 endpoint)
- `sp_n_InsertarBancoTransferencia` (1 endpoint)
- `sp_n_InsertarCambioVigencia` (1 endpoint)
- `sp_n_InsertarCategoria` (1 endpoint)
- `sp_n_InsertarCatUnidadTerritorial` (1 endpoint)
- `sp_n_InsertarCentroServicio` (1 endpoint)
- `sp_n_InsertarCiudad` (1 endpoint)
- `sp_n_InsertarCliente` (1 endpoint)
- `sp_n_InsertarClienteFacturacion` (1 endpoint)
- `sp_n_InsertarCobranzaDetalle` (1 endpoint)
- `sp_n_InsertarCobroDineroUsado` (1 endpoint)
- `sp_n_InsertarCobroDineroUsadoCheques` (1 endpoint)
- `sp_n_InsertarCobroDineroUsadoCortesCS` (1 endpoint)
- `sp_n_InsertarColonia` (1 endpoint)
- `sp_n_InsertarConcentradoDetalle` (1 endpoint)
- `sp_n_InsertarConcepto` (1 endpoint)
- `sp_n_InsertarCondicionCredito` (1 endpoint)
- `sp_n_InsertarContrasenaCodigo` (1 endpoint)
- `sp_n_InsertarCorte` (1 endpoint)
- `sp_n_InsertarCorteCS` (1 endpoint)
- `sp_n_InsertarCuenta` (1 endpoint)
- `sp_n_InsertarDescuentos` (1 endpoint)
- `sp_n_InsertarDescuentosDetalle` (1 endpoint)
- `sp_n_InsertarDiaFestivo` (2 endpoints)
- `sp_n_InsertarDocumento` (1 endpoint)
- `sp_n_InsertarEconomico` (1 endpoint)
- `sp_n_InsertarEmpleado` (1 endpoint)
- `sp_n_InsertarEmpleadosAsistencia` (1 endpoint)
- `sp_n_InsertarEmpresa` (1 endpoint)
- `sp_n_InsertarEmpresaSerie` (1 endpoint)
- `sp_n_InsertarGallero` (1 endpoint)
- `sp_n_InsertarGarantia` (1 endpoint)
- `sp_n_InsertarGarantiaRegistro` (1 endpoint)
- `sp_n_InsertarGasolina` (1 endpoint)
- `sp_n_InsertarGrupoCategoria` (1 endpoint)
- `sp_n_InsertarGrupoCategoriasMarcasRelacion` (1 endpoint)
- `sp_n_InsertarInventarioPedidos` (1 endpoint)
- `sp_n_InsertarMarca` (1 endpoint)
- `sp_n_InsertarMCGerente` (1 endpoint)
- `sp_n_InsertarMotivoFianza` (1 endpoint)
- `sp_n_InsertarMunicipio` (1 endpoint)
- `sp_n_InsertarOperador` (1 endpoint)
- `sp_n_InsertarPedidoDetalle` (1 endpoint)
- `sp_n_InsertarPermisionario` (1 endpoint)
- `sp_n_InsertarPoliza` (1 endpoint)
- `sp_n_InsertarProducto` (1 endpoint)
- `sp_n_InsertarProveedor` (1 endpoint)
- `sp_n_InsertarProveedorApoderado` (1 endpoint)
- `sp_n_InsertarProveedorDocumento` (1 endpoint)
- `sp_n_InsertarProveedorDomicilio` (1 endpoint)
- `sp_n_InsertarRecaudacion` (1 endpoint)
- `sp_n_InsertarRecaudacionCorte` (1 endpoint)
- `sp_n_InsertarRecordatorio` (1 endpoint)
- `sp_n_InsertarRepartidor` (1 endpoint)
- `sp_n_InsertarServicio` (1 endpoint)
- `sp_n_InsertarTipoGasto` (1 endpoint)
- `sp_n_InsertarTipoRecordatorio` (1 endpoint)
- `sp_n_InsertarTipoUsado` (1 endpoint)
- `sp_n_InsertarUnidadMedida` (1 endpoint)
- `sp_n_InsertarUsadoMovimiento` (1 endpoint)
- `sp_n_InsertarUsuario` (1 endpoint)
- `sp_n_InsertarVentaFactura` (2 endpoints)
- `sp_n_InsertarVentasCobroUsados` (1 endpoint)
- `sp_n_InsertarVentasDescuentosAjustes` (1 endpoint)
- `sp_n_InsertarVentasNuevosPagos` (1 endpoint)
- `sp_n_InsertarVentasPagos` (1 endpoint)
- `sp_n_InsertarVentasPagosIncobrable` (1 endpoint)
- `sp_n_InsertarVentasPagosKilos` (1 endpoint)
- `sp_n_InsertarVentasUsadosExcendentes` (1 endpoint)
- `sp_n_LimpiarCSVentasPagos` (1 endpoint)
- `sp_n_ProcesarCompra` (1 endpoint)
- `sp_n_ProcesarInventarioTransito` (1 endpoint)
- `sp_n_ProcesarPedido` (1 endpoint)
- `sp_n_RegresarCambioVigencia` (1 endpoint)
- `sp_n_rptFacturaElectronica` (1 endpoint)
- `sp_n_rptPolizaDocumentos` (1 endpoint)
- `sp_n_rptPolizaFianzas` (1 endpoint)
- `sp_n_rptPolizaIngresos` (1 endpoint)
- `sp_n_rptVentasFolios` (1 endpoint)
- `sp_n_rptVentasFoliosSinAsignar` (1 endpoint)
- `sp_n_rptVentasSaldos` (1 endpoint)
- `sp_n_rptVentasSaldosPrincipal` (1 endpoint)
- `sp_n_SimularDescuento` (1 endpoint)
- `sp_n_SimulaVentasDescuentosAjustes` (1 endpoint)
- `sp_n_SincronizarDownload` (19 endpoints)
- `sp_n_ValidaEmpleadosPrestamos` (1 endpoint)
- `sp_n_ValidaPeriodosTipoSueldo` (1 endpoint)
- `sp_n_VentasDetalle` (3 endpoints)
- `sp_n_VentasDetalleMultiple` (3 endpoints)
- `sp_n_VentasDevolucion` (2 endpoints)
- `sp_n_VentasInformacion` (2 endpoints)
- `sp_n_VentasReasignacion` (1 endpoint)
- `sp_n_VentasUsadosCargo` (3 endpoints)
- `sp_n_VentasUsadosCredito` (4 endpoints)
- `sp_n_VentasUsadosCreditoAjuste` (1 endpoint)
- `sp_n_VentasUsadosCreditoAjusteZara` (1 endpoint)
- `sp_n_VentasUsadosCreditoLog` (1 endpoint)

SPs mas usados: `sp_n_ConsultaConstantes` (155), `sp_n_SincronizarDownload` (19), `sp_n_ConsultaTemplateHtml` (7), `sp_n_ConsultaParametros` (5), `sp_n_ConsultaColonias` (4), `sp_n_Actualizar_MCContratoConcepto` (4), `sp_ConsultaReportes` (4), `?dinamico(columna SP de sp_ConsultaReportes)` (4).

## Logica de negocio que vive en C# y NO en el stored procedure

Esto es lo que se pierde si se migra "solo llamando a los mismos SPs".

### 1. La validacion del JWT no verifica firma ni expiracion

`Security/JwtTokenGenerator.readJwtTokenAsync` construye `new JwtSecurityToken(jwtEncodedString: token)`, que **decodifica sin validar la firma**. `validateJwtTokenAsync` revisa el correo, que `nombres`/`apellidos` no esten vacios, que el claim `empresa` coincida con `JWT_EMPRESA` del Web.config y que haya `idApp`/`idCliente`/`idUsuario` > 0, pero **nunca compara `ValidFrom`/`ValidTo`**: un token vencido sigue siendo valido. `LifetimeValidator` existe en `TokenValidationHandler` pero nadie lo invoca. El handler global solo comprueba que **exista** el header `Authorization`, no que el token sea bueno, y deja pasar sin header cualquier URL que contenga `authtoken`.

### 2. El login compara la contrasena en C#, en claro

`UsuarioService.findAsync` (unico metodo de `LoginController`, ruta `/api/login/authtoken/app/mac`) quita `@tauro.mx` del correo para armar el usuario, fuerza `IDStatus=Activo`, `Formato=Todo`, `Identico=1`, ejecuta `sp_n_ConsultaUsuarios` y luego compara en C#: `dtUsuarios[0]["contrasenaDesc"].ToUpper() != Contrasena.ToUpper()`. La contrasena viaja y se compara descifrada y sin distinguir mayusculas. El SP no valida credenciales.

### 3. Motor de reportes data-driven: el nombre del SP sale de la base

`ReporteService.findAsync` (v1) y `ReporteV2Service.findAsync` (v2, ~680 lineas) ejecutan `sp_ConsultaReportes` con `idReporte` y toman de la **columna `SP`** del resultado los nombres de los stored procedures de datos. Por eso en el TSV esos endpoints llevan `?dinamico(columna SP de sp_ConsultaReportes)`: **el SP real no se puede saber leyendo el codigo**, hay que consultar la tabla de configuracion de reportes en la base. Ademas: si `IDReporteMaster<>0` se sustituye el `idReporte`; `ConsultaSpRepository.ConsultaSp` parte nombres y parametros por `|`, usa `SqlCommandBuilder.DeriveParameters` y asigna los parametros **por posicion** (reemplazando `~` por `|`); v1 tiene un `switch` de 5 reportes y **devuelve `null` (500 vacio) si el idReporte no esta en el switch**; v2 tiene un `switch` de ~100 casos de `enumReportes` que enruta a ~20 servicios generadores.

### 4. Todo el HTML/PDF de reportes y remisiones se arma en C#

Los ~24 servicios `*GeneraHtmlAsync` de `Models/Service/ReportesServ`, `ReportesV2Serv` y `ReportesMCServ` hacen en C#: paginacion y corte de hoja por `maxLineas` de la plantilla, subtotales y acumulados por grupo, formateo de numeros y monedas (`Funciones.FormateaNumero`, `Funciones.TruncateDecimal`), sustitucion de placeholders (`Funciones.ReemplazaValores`), encabezados y pies, conversion de importe a letra (`Utils/ConvertirNumeroALetra.cs`) y generacion del QR del CFDI con `QRCoder` usando `URLVerificaSat` + `RFCEmisor` de Constantes. Los SPs solo devuelven filas planas.

### 5. Facturacion electronica: serie, folio y candado de la key, en C#

`VentasFacturaService.insertAsync` (~240 lineas, detras de `POST /api/ventasfacturas` y del complemento de pago): (a) valida que `FacturapiSecretKey` contenga el nombre de `AppMac` sin `.exe` ("KEY PARA FACTURAR NO COINCIDE") y luego **le quita ese nombre** para obtener la key real; (b) decide la **serie del CFDI** con una cadena de if/else comparando `AppMac` contra `MacT.exe`, `MacZCS1..CS6.exe`, `MacL.exe` -> series `T`, `CS1..CS6`, `L`, o `Z` por defecto, y `NCCS1..NCCS6`, `NCL`, `NCZ` para notas de credito; (c) parte `FolioFtm` por `-` para separar serie y folio; (d) recorta `IDFEMetodoPago` e `IDFEUsoCFDI` en el `-`; (e) calcula importes con `PorcentajeIva` de Constantes; (f) **timbra contra Facturapi por HTTP antes de grabar** con `sp_n_InsertarVentaFactura` y mueve el consecutivo con `sp_n_ActualizarParametro`. No hay transaccion que cubra timbrado + grabado.

### 6. Ramificacion por empresa decidida en C#

El SP a ejecutar cambia segun columnas de `sp_n_ConsultaConstantes`:

  - `VentasDevolucionRepository.spInsertar`: `sp_n_DevolucionVentaLuro` si `Constantes.EsLuro = 1`, si no `sp_n_DevolucionVenta`.
  - `VentasUsadosCreditoRepository.spAjustar`: `sp_n_VentasUsadosCreditoAjusteZara` si `Constantes.EsquemaPago.ToUpper() == "ZARA"`, si no `sp_n_VentasUsadosCreditoAjuste`.
  - `EmpleadoTipoSueldoReciboRepository.spConsultarRecibos`: `sp_n_ConsultaEmpleadosTipoSueldoRecibo` si `Constantes.AppMac` contiene `"MACT"` (Tauro), si no `sp_n_ConsultaEmpleadosTipoSueldoReciboZara`.
  - `ReporteV2Service`: `remisionZaragoza` / `remisionCentroServicio` / remision normal son tres generadores distintos elegidos en C#.

  Ademas, en varios repositorios (`AlmacenProductoRepository`, `PedidoDetalleRepository`, `VentasDetalleRepository`) el `if (EsLuro == 1) this.sp = ...Luro` **esta comentado** y en su lugar `EsLuro` se pasa como parametro `@EsLuro` al SP: la bifurcacion se movio a la base a medias, conviven los dos esquemas.

### 7. Seleccion de SP por bandera del request (no por empresa)

  - `BancoTransferenciaRepository.spAplicar`: `sp_n_AplicarBancoTransferencia` si `aplicarDinero`, `sp_n_AplicarBancoTransferenciaUsados` si `aplicarCascos`. Son dos `if` independientes sin `else`: **si llegan las dos banderas en true, el segundo sobreescribe `this.sp` y solo se ejecuta el de usados**.
  - `VentasCobrosDineroUsadoRepository.spActualizar`: `sp_n_ActualizarCobroDineroUsado` si `esAplicacion = 0`, `sp_n_AplicarCobroDineroUsado` si `esAplicacion = 1`.
  - `VentasDetalleService.findAsync`: `sp_n_VentasDetalleMultiple` cuando vienen varios `IDsVenta`, `sp_n_VentasDetalle` cuando viene uno.
  - `VentasUsadosCreditoService`: `findActualCatalogoAsync` y `findAnteriorAsync` llaman al **mismo** `sp_n_VentasUsadosCredito` cambiando una constante de `enumUsadosCreditoStatus` en el parametro `@accion`.

### 8. Ciclos en C# que ejecutan el SP N veces, sin transaccion

Patron repetido: el servicio recorre una lista, llama al SP una vez por elemento, arma a mano un `DataTable` de resultados y solo reporta los renglones que fallaron. Si falla el elemento 3, los dos primeros **ya quedaron grabados** y no se revierten.

  - `PedidoService.processVentaAsync` (`PUT /api/pedidos/procesar`): ordena `IDPedidoLst`, instancia un repositorio nuevo por iteracion, llama a `sp_n_ProcesarPedidoVenta` por pedido y concatena los `IDVenta` generados en una cadena separada por `~` que devuelve aparte.
  - `VentasPagosService.insertAsync`: multipago, un `sp_n_InsertarVentaPago` por venta cuando `NuevoEsquemaPagos = 0` y vienen varios `IDsVenta`.
  - `UsuarioFormaProcesoService.updateAsync`: parte `IDsProceso` e `IDsStatus` por `~` y llama al SP por pareja; si las dos listas no vienen del mismo largo, `IndexOutOfRangeException`.
  - `UsuarioFechaService.updateAsync`: una llamada al SP por fecha.

### 9. Campos compuestos armados como cadena posicional en C#

`CorteService.insertAsync` concatena ~19 importes del corte (`SaeEsquina`, `SaeEsquinaDomingo`, `SaeLocalTauro`, `TotalSae`, `MacCamioneta`, `MacAgentes`, `TotalMac`, `Transferencias`, `TransferenciasTiendaEsquina`, `TransferenciasLocalTauro`, `DepositoCheque`, `DepositoEfectivo`, `Tarjetas`+`TarjetasTiendas` con **otro** delimitador, `Cheque`, `Efectivo`, `Morraya`, `TotalGastos`, `TotalDesglose`, `GranTotal`, `NoAplicadoDesglose`) en un solo campo `CorteInfo`. El SP recibe un string posicional, no columnas: cambiar el orden en C# rompe el corte sin que la base se queje.

### 10. Formato de fechas resuelto en C# contra una constante de la base

En ~75 metodos de servicio el patron es: leer `sp_n_ConsultaConstantes`, tomar la columna `formatoFechaDB`, y convertir cada fecha con `Funciones.ConvertFtmDate(formatoFechaDB, fecha)` pegandole `" 00:00:00"` o `" 23:59:59"` antes de mandarla al SP. Los SPs esperan la fecha **ya formateada como string**. Tambien se usa `Constantes.FechaOperacion` como "hoy" del negocio (p.ej. `ConsultaVentasService.findAsync`, cuando `TodasRemisiones = 1`, fuerza el rango `01-01-2000` .. `FechaOperacion`).

### 11. Saneo manual de SQL injection y listas delimitadas por "~"

Antes de cada insert/update los servicios hacen `campo.Replace("'", "").Trim()` sobre los campos de texto libre (nombre, observaciones, motivo, numero de cheque...). Es el unico saneo: **se borran los apostrofos del dato del usuario**, con lo que "O'Brien" se guarda como "OBrien". Las listas se mandan al SP como un solo string unido con `Variables.DELIMITADOR_PARAM_TILDE_SP` (`~`) y el SP las vuelve a partir; `ConsultaSpRepository` ademas intercambia `~` por `|` y de vuelta, asi que un dato que contenga `~` o `|` corrompe la llamada.

### 12. Reglas sueltas de negocio en el servicio

  - `ProductoService.insertAsync`/`updateAsync`: si `CodigoBarras` llega vacio **se copia la `Clave`**; si `IDStatus = Activo` lee `PorcentajeIVA` de Constantes (el bloque que daba de alta el producto en Facturapi esta comentado, por lo que hoy ese `PorcentajeIVA` se lee y no se usa).
  - `CatAgentesController.DeleteActualizarCatAgentes` y los `Delete*` de casi todos los catalogos **no borran**: llaman a `updateAsync`, o sea a `sp_n_Actualizar<Entidad>` (baja logica por `IDStatus`). Varios repositorios tienen un `spEliminar` que ademas apunta al SP de *actualizar* y que nadie invoca.
  - `InventarioPedidoRepository.spConsultarDetalle` (`POST /api/inventariopedidos/consultar/detalle`) tiene **todo el SQL comentado**: valida el token y devuelve 200 con lista vacia. Endpoint muerto.
  - `VentasFacturaService.sendEmailAsync` (`POST /api/ventasfacturas/sendemail`) **no valida token**; deriva la ruta del PDF reemplazando `.zip` por `.pdf`, crea la carpeta si falta, descarga zip/pdf/xml de Facturapi si no existen y manda el correo. Convive con un `sendEmailAsync_Bak` (codigo duplicado, inalcanzable desde los controladores).

### 13. Credenciales y endpoints externos en duro en el codigo

`Utils/EndPoints.cs` lleva `URL_FACTURAMA = "https://apisandbox.facturama.mx/"` (es el **sandbox**) junto con `URL_FACTURAMA_USR = "CULA7909161M9"` y `URL_FACTURAMA_PWD = "Ab123456"`. Los 6 endpoints de `FacturamaController` no tocan la base y no piden token: hacen un GET contra esa API con esas credenciales. La key de Facturapi si viene de la base (`Constantes.FacturapiSecretKey`) pero guardada con el nombre del ejecutable pegado dentro como "proteccion", y el C# se lo quita.

### 14. Controladores con [AllowAnonymous]

Lo llevan `LoginController`, `ReportesController`, `ReportesV2Controller`, `VentasController`, `ComprasController` y `LogImpresionesWebController`. No hay filtros de autorizacion por accion, asi que el atributo solo neutraliza lo poco que hace `TokenValidationHandler`; la unica puerta real es el `validateJwtTokenAsync` que cada servicio decide llamar (y que no verifica firma ni expiracion, ver punto 1).

### 15. Todas las conexiones y el manejo de errores son ad hoc

Cada metodo de repositorio abre su propia `SqlConnection` via `Mac3SqlServerConnector`, hace `Conn.Open()` dos veces, llena un `DataSet` con `SqlDataAdapter`, mapea con `Funciones.DataTableToList<T>()` por reflexion, y en el `catch` solo hace `Debug.WriteLine` + guarda `msgError`, devolviendo `false`. El servicio traduce ese `false` a un HTTP 500 cuyo cuerpo incluye el nombre del SP y el mensaje crudo de SQL Server (`"[No se pudo procesar " + repo.sp + "] - " + repo.msgError`), y el `catch` del servicio devuelve `ex.ToString()` completo con un **400**: se filtra el stack trace al cliente. No hay `using`, no hay transacciones, no hay reintentos, y todo el trabajo sincrono se envuelve en `Task.Run` para fingir async.

## Como leer el TSV

- `ruta_http`: ya resuelta a literal (`RoutePrefix` + `Route` con las constantes de `Utils/EndPoints.cs` sustituidas). Las 488 quedaron resueltas, ninguna quedo como nombre de constante.
- `stored_procedures`: todos los SPs que acaba ejecutando el endpoint, siguiendo controlador -> servicio -> servicios anidados -> repositorios, separados por `;`. `?dinamico(...)` marca el caso de reportes donde el nombre del SP sale de la base.
- `escribe`: `si` cuando el nombre del SP indica insert/update/delete/cancelar/aplicar/aceptar/procesar/ajustar o cuando el verbo es PUT/DELETE; `no` cuando todos los SPs son de consulta.
- `tipo_token`: valor de `Enums.enumToken` exigido. `ninguno` = el servicio no llama a `validateJwtTokenAsync`.
- `dto_salida`: del atributo `[ResponseType(typeof(...))]`. Un `-` significa que el metodo no lo declara (muchos lo tienen comentado); el cuerpo real suele ser un `*Response` o `GenericResponse`.

## Metodo de extraccion (para poder reproducirlo)

Parseo estatico de los 132 archivos de `Controllers/`, los 175 servicios de `Models/Service/` y los 230 repositorios de `Models/Repository/`: se quitan comentarios, se extraen metodos por emparejamiento de llaves, se resuelven las constantes de `EndPoints.cs` por concatenacion, se mapean los campos `private readonly XService/XRepository` a su clase, se siguen las llamadas servicio -> servicio y servicio -> repositorio (incluidos metodos privados y delegaciones internas), y en cada metodo de repositorio se resuelve `this.sp = this.<campo>` contra los campos string de la clase. Lo que no se pudo determinar leyendo el codigo quedo marcado con `?` y explicado en `notas`.
