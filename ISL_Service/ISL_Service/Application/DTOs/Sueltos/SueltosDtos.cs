namespace ISL_Service.Application.DTOs.Sueltos;

/*
  LOS CUATRO SUELTOS: PROYECTOS, RECORDATORIOS, SERVICIOS Y SINCRONIZACION.

  No son un modulo: son cuatro pantallas que no caen en ningun otro lote y que
  comparten que TRES DE LAS CUATRO no se pueden comprobar en esta base. Sus
  procedimientos no estan instalados en Produccion_svr:

    sp_n_Consultar_Proyectos, sp_n_Insertar_Proyecto, sp_n_Actualizar_Proyecto,
    sp_n_Actualizar_Proyecto_Facturas, sp_n_ConsultaServicio,
    sp_n_InsertarServicio, sp_n_CancelarServicio, sp_n_ConsultaSincronizacion

  Leido de sys.objects: no existen. Legacy falla igual contra esta base. Se
  implementan de todas formas, con los parametros exactos que manda
  MacServicios2, y queda anotado que la comprobacion tiene que hacerse en la
  instalacion que si los tiene. Recordatorios si se puede comprobar aqui.
*/

/// <summary>El par result/mensaje de legacy, traducido.</summary>
public class SueltoEscrituraResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Id { get; set; }
}

// ===========================================================================
// RECORDATORIOS
// ===========================================================================

public class RecordatoriosFiltro
{
    public int IdRecordatorio { get; set; }
    public int IdTipoRecordatorio { get; set; }
    public int IdStatusRecordatorio { get; set; }

    /// <summary>Texto del recordatorio. El procedimiento lo usa como filtro.</summary>
    public string? Recordatorio { get; set; }

    public int EsUrgente { get; set; }
    public int EsImportante { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

/// <summary>
/// Alta de recordatorio.
///
/// `FechaLimite` es obligatoria. En legacy no se validaba y la conversion de la
/// fecha se hacia sin comprobar que viniera, asi que un alta sin fecha limite
/// reventaba con un error de posicion de caracteres y devolvia 400 con el stack
/// completo. Un recordatorio sin fecha limite tampoco recuerda nada.
/// </summary>
public class CrearRecordatorioRequest
{
    public int IdTipoRecordatorio { get; set; }
    public string Recordatorio { get; set; } = string.Empty;
    public DateTime FechaLimite { get; set; }

    /// <summary>La fecha exacta en la que hay que avisar, si la hay.</summary>
    public DateTime? FechaEspecifica { get; set; }

    public bool EsUrgente { get; set; }
    public bool EsImportante { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Modificacion de recordatorio. Pide la ficha completa: el UPDATE del
/// procedimiento escribe todas las columnas.
/// </summary>
public class ActualizarRecordatorioRequest
{
    public int IdTipoRecordatorio { get; set; }
    public int IdStatusRecordatorio { get; set; }
    public string Recordatorio { get; set; } = string.Empty;
    public DateTime FechaLimite { get; set; }
    public DateTime? FechaEspecifica { get; set; }
    public bool EsUrgente { get; set; }
    public bool EsImportante { get; set; }
    public DateTime? FechaTerminado { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? Observaciones { get; set; }
}

// ===========================================================================
// SERVICIOS
// ===========================================================================

public class ServiciosFiltro
{
    public int IdServicio { get; set; }
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public int IdUsuario { get; set; }

    /// <summary>
    /// Rango de folios. Si viene el inicial, se busca por folio y los demas
    /// filtros se ignoran (salvo la empresa): es la precedencia de legacy.
    /// </summary>
    public int FolioInicial { get; set; }

    public int FolioFinal { get; set; }

    public DateTime? RecepcionDesde { get; set; }
    public DateTime? RecepcionHasta { get; set; }
    public DateTime? RegistroDesde { get; set; }
    public DateTime? RegistroHasta { get; set; }
    public DateTime? CancelacionDesde { get; set; }
    public DateTime? CancelacionHasta { get; set; }
}

/// <summary>
/// Alta de servicio.
///
/// `FechaRecepcion` es una fecha de verdad y el backend la formatea. En legacy
/// este era el UNICO endpoint que mandaba la fecha cruda, sin pasarla por la
/// conversion que usaban todos los demas: el procedimiento recibia lo que
/// escribiera el cliente.
/// </summary>
public class CrearServicioRequest
{
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
    public int IdProducto { get; set; }
    public int IdRepartidor { get; set; }
    public int Cantidad { get; set; }
    public DateTime FechaRecepcion { get; set; }
    public decimal Importe { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

// ===========================================================================
// SINCRONIZACION
// ===========================================================================

/// <summary>
/// Filtro de la cola de sincronizacion de cajas.
///
/// LO QUE NO SE HEREDA: legacy tenia una bandera `Server` en el cuerpo que, en
/// true, hacia que el procedimiento se ejecutara contra OTRA BASE DE DATOS — la
/// de Aviacion, con la cadena de conexion sacada de Constantes.AviacionDBServer.
/// O sea que el cliente elegia la base destino con un booleano del JSON. Eso no se
/// expone: la base a la que apunta el backend no la decide quien llama. Si hace
/// falta consultar la cola de otra instalacion, se hace desde el backend de esa
/// instalacion.
/// </summary>
public class SincronizacionFiltro
{
    public int IdCaja { get; set; }
    public int IdStatusSincronizacion { get; set; }
    public int IdTabla { get; set; }
    public int NumRegistros { get; set; }
    public int UltimaSincronizacion { get; set; }
}

// ===========================================================================
// PROYECTOS (obra por consulta ciudadana)
// ===========================================================================

/// <summary>Filtro de la consulta de proyectos.</summary>
public class ProyectosFiltro
{
    public int IdProyecto { get; set; }
    public int IdUnidadTerritorial { get; set; }
    public int IdContratista { get; set; }
    public int IdStatus { get; set; }

    /// <summary>
    /// Si viene la clave, se busca SOLO por clave y los demas filtros se ignoran:
    /// es la precedencia de legacy, dicha en vez de escondida en un reemplazo del
    /// objeto de filtros.
    /// </summary>
    public string? Clave { get; set; }

    public string? FolioGanador { get; set; }
    public string? NombreGanador { get; set; }
    public string? Contrato { get; set; }
    public DateTime? ContratacionDesde { get; set; }
    public DateTime? ContratacionHasta { get; set; }
}

/// <summary>
/// La ficha del proyecto.
///
/// Son cincuenta y tantos campos y van todos en el mismo request porque el
/// procedimiento los escribe todos: el alta y la modificacion reciben la misma
/// lista (la modificacion solo agrega el id y el estatus).
///
/// DOS COSAS QUE LEGACY TIENE Y AQUI NO SE PIDEN, porque no se guardan:
///  - los campos *Docto (FolioGanadorDocto, ContratoDocto, los cinco
///    EnvioEvidenciasFotoDocto...): estan en su DTO y NINGUN procedimiento de
///    escritura los recibe. Pedirlos seria prometer que se guarda el documento.
///  - IDUnidadTerritorial: solo se usa al consultar; el alta y la modificacion no
///    lo mandan.
///
/// Los importes de factura 1 a 5 tienen su propio endpoint
/// (PUT /{id}/facturas) porque su procedimiento es otro; mandarlos tambien aqui
/// seria dejar que dos pantallas se pisen el mismo dato.
/// </summary>
public class ProyectoRequest
{
    public int IdAlcaldia { get; set; }
    public int IdContratista { get; set; }

    public string Clave { get; set; } = string.Empty;
    public string? Denominacion { get; set; }

    public bool CuentaConFolioGanador { get; set; }
    public string? FolioGanador { get; set; }
    public string? UbicacionGanador { get; set; }
    public string? GeolocalizacionGanador { get; set; }
    public string? NombreGanador { get; set; }
    public string? DescripcionGanador { get; set; }

    public bool SeEjecutoElProyectoConsulta { get; set; }

    public DateTime? FechaAsambleaAutorizo { get; set; }
    public DateTime? FechaDictaminacion { get; set; }

    public bool CuentaConFolioSustituto { get; set; }
    public string? FolioSustituto { get; set; }
    public string? UbicacionSustituto { get; set; }
    public string? GeolocalizacionSustituto { get; set; }
    public string? NombreSustituto { get; set; }
    public string? DescripcionSustituto { get; set; }

    public bool CuentaConContrato { get; set; }
    public string? Contrato { get; set; }
    public DateTime? FechaInicioContrato { get; set; }
    public DateTime? FechaTerminoContrato { get; set; }

    public string? CapituloGasto { get; set; }
    public decimal PresupuestoAsignadoProyecto { get; set; }
    public decimal Contratado { get; set; }
    public string? NumFactura { get; set; }
    public decimal MontoFactura { get; set; }
    public decimal Ejercido { get; set; }
    public decimal SaldoDelCompromiso { get; set; }
    public decimal AvancePresupuestal { get; set; }
    public decimal Economia { get; set; }

    public bool CuentaConFechaActaEntregaProyecto { get; set; }
    public DateTime? FechaActaEntregaProyecto { get; set; }
    public bool CuentaConFechaEntregaTrabajos { get; set; }
    public DateTime? FechaEntregaTrabajos { get; set; }

    public string? AreaResponsable { get; set; }
    public decimal AvanceEjecucion { get; set; }

    public bool EnvioEvidenciasFoto { get; set; }
    public int NumEvidenciasFoto { get; set; }
    public bool CuentaConEvidenciasFoto1 { get; set; }
    public bool CuentaConEvidenciasFoto2 { get; set; }
    public bool CuentaConEvidenciasFoto3 { get; set; }
    public bool CuentaConEvidenciasFoto4 { get; set; }
    public bool CuentaConEvidenciasFoto5 { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>La ficha del proyecto al modificarla: la misma, mas el estatus.</summary>
public class ActualizarProyectoRequest : ProyectoRequest
{
    public int IdStatus { get; set; } = 1;
}

/// <summary>Una de las cinco facturas del proyecto.</summary>
public class FacturaProyecto
{
    public bool CuentaConFactura { get; set; }
    public decimal Monto { get; set; }
}

/// <summary>
/// Las cinco facturas del proyecto.
///
/// Son exactamente cinco porque el procedimiento tiene diez parametros fijos
/// (CuentaConFactura1..5 y MontoFactura1..5). Van en una lista para que el front
/// no tenga que nombrar diez campos, y el backend la reparte en orden; si llegan
/// mas de cinco, se rechaza en vez de tirar las sobrantes en silencio.
/// </summary>
public class FacturasProyectoRequest
{
    public List<FacturaProyecto> Facturas { get; set; } = new();
}
