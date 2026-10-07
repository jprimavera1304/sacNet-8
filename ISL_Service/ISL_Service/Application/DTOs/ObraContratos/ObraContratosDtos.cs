namespace ISL_Service.Application.DTOs.ObraContratos;

/*
  CONTRATOS DE OBRA (MC): EL CONTRATO, SUS CONVENIOS Y SUS CUATRO ARBOLES.

  Un contrato de obra tiene cuatro listas colgando, y cada una tiene su propio
  sp_n_:

    grupos       -> capitulos del catalogo de conceptos
    conceptos    -> el renglon que se estima y se paga, dentro de un grupo
    partidas     -> de que partida presupuestal sale el dinero
    periodos     -> el programa de avance fisico
    estimaciones -> lo que se cobra de un periodo

  TRES COSAS QUE CAMBIAN RESPECTO DE LEGACY, Y SON DE FONDO:

  1. LAS FECHAS SON FECHAS.
     En Legacy todas estas fechas eran `string` y pasaban por
     Funciones.ConvertFtmDate, que les voltea el dia y el mes a mano leyendo
     Constantes.formatoFechaDB; y para leer esa constante llamaba a
     sp_n_ConsultaConstantes, que CIERRA EL DIA del negocio. Aqui son DateOnly y
     no hace falta ninguna de las dos cosas. Con la fecha vacia, el
     ConvertFtmDate de Legacy ademas lanzaba (Substring fuera de rango) y la
     respuesta era un 400 con el stack trace completo.

  2. EL "ORDEN" NO VIAJA COMO NUMERO: ES SUBIR O BAJAR.
     sp_n_Actualizar_MCContratoGrupo trata @Orden como un DELTA y es la bandera
     que decide el modo entero del procedimiento: con @Orden <> 0 SOLO reordena
     (no toca ni el nombre ni el status), y con @Orden = 0 actualiza los campos.
     Legacy exigia al cliente mandar un Orden cualquiera distinto de cero y
     luego lo pisaba con -1 o +1 segun la ruta. Aqui son rutas propias
     (/orden con direccion subir|bajar) y el numero no viaja: un delta de 3
     mandado por error dejaba dos renglones con el mismo Orden.

  3. IDCONTRATO ES OBLIGATORIO PARA REORDENAR.
     En la rama de reordenar, el SP intercambia el renglon con el que ocupaba la
     posicion destino usando `WHERE IDContrato = @IDContrato`. Legacy no exigia
     IDContrato en /subir ni en /bajar, asi que al omitirlo el intercambio no
     ocurria y quedaban DOS renglones con el mismo Orden, sin error y sin
     rastro. Aqui es requerido.
*/

// ======================================================================
// El contrato
// ======================================================================

/// <summary>Filtros de la consulta de contratos de obra.</summary>
public class ObraContratosFiltro
{
    public int IdContrato { get; set; }
    public int IdProveedor { get; set; }
    public int IdContratoSupervisionExterna { get; set; }
    public int IdTipoObra { get; set; }
    public int IdTipoContratacion { get; set; }
    public int IdTipoRecurso { get; set; }
    public int IdResidenteSupervision { get; set; }
    public int IdResidenteObra { get; set; }
    public int IdStatus { get; set; }

    /// <summary>Numero de contrato. Filtra por coincidencia, no exacto.</summary>
    public string? Contrato { get; set; }

    /// <summary>Fecha de captura, desde.</summary>
    public DateOnly? FechaInicial { get; set; }
    /// <summary>Fecha de captura, hasta.</summary>
    public DateOnly? FechaFinal { get; set; }

    /// <summary>Fecha de contratacion, desde.</summary>
    public DateOnly? FechaContratacionInicial { get; set; }
    /// <summary>Fecha de contratacion, hasta.</summary>
    public DateOnly? FechaContratacionFinal { get; set; }
}

/// <summary>Alta de un contrato de obra.</summary>
public class CrearObraContratoRequest
{
    /// <summary>El contratista. Requerido.</summary>
    public int IdProveedor { get; set; }

    /// <summary>El apoderado del contratista que firma.</summary>
    public int IdProveedorApoderado { get; set; }

    /// <summary>El contrato de supervision externa que vigila este, si lo hay.</summary>
    public int IdContratoSupervisionExterna { get; set; }

    /// <summary>Requerido.</summary>
    public int IdTipoObra { get; set; }
    /// <summary>Requerido.</summary>
    public int IdTipoContratacion { get; set; }
    /// <summary>Requerido.</summary>
    public int IdTipoRecurso { get; set; }

    public int IdResidenteSupervision { get; set; }

    /// <summary>Requerido.</summary>
    public int IdResidenteObra { get; set; }

    /// <summary>El numero de contrato. Requerido.</summary>
    public string Contrato { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public string Descripcion { get; set; } = string.Empty;

    public string? Ubicacion { get; set; }

    /// <summary>Requerido.</summary>
    public DateOnly? FechaContratacion { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaInicio { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaTermino { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    public DateOnly? FechaAsignacionRecurso { get; set; }
    public string? NumeroOficio { get; set; }
    public string? Folio { get; set; }
    public string? Caso { get; set; }
    public string? NumeroProcedimiento { get; set; }

    /// <summary>
    /// El ejercicio presupuestal (el ano). Entero: en Legacy era `string` y el
    /// parametro del SP es int, asi que un ejercicio vacio reventaba el alta
    /// completa con "Error converting data type varchar to int".
    /// </summary>
    public int? Ejercicio { get; set; }

    public decimal Sicv { get; set; }
    public decimal Srcop { get; set; }
    public decimal Lfd { get; set; }

    /// <summary>
    /// El contrato se desglosa por partidas individuales en vez de por un solo
    /// monto.
    /// </summary>
    public bool PartidasIndividuales { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Modificacion de un contrato de obra.</summary>
public class ActualizarObraContratoRequest : CrearObraContratoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Cambia SOLO la bandera de partidas individuales de un contrato.
///
/// Legacy lo hacia por la misma ruta PUT /api/mccontratos, colando un
/// IDStatus = 3 que no es un status sino un interruptor de modo
/// ("SOLO Se va a actualizar PartidasIndividuales", MCContratosController.cs:63).
/// En esa rama, Legacy ademas se salta la conversion de fechas y le manda al SP
/// las cadenas de fecha tal como llegaron. Aqui es una ruta propia, y el status
/// vuelve a significar solamente el status.
/// </summary>
public class ActualizarObraContratoPartidasIndividualesRequest
{
    public bool PartidasIndividuales { get; set; }
}

/// <summary>Cierra o reabre un contrato de obra.</summary>
public class ActualizarObraContratoStatusRequest
{
    /// <summary>true = cerrado, false = abierto.</summary>
    public bool ContratoCerrado { get; set; }
}

// ======================================================================
// Convenios
// ======================================================================

/// <summary>Filtros de la consulta de convenios de obra.</summary>
public class ObraConveniosFiltro
{
    public int IdConvenio { get; set; }
    public int IdContrato { get; set; }
    public int IdConvenioMotivo { get; set; }
    public int IdProveedor { get; set; }
    public int IdStatus { get; set; }
    public string? Convenio { get; set; }
    public string? Contrato { get; set; }
    public DateOnly? FechaInicial { get; set; }
    public DateOnly? FechaFinal { get; set; }
}

/// <summary>Alta de un convenio sobre un contrato de obra.</summary>
public class CrearObraConvenioRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>El motivo del convenio. Requerido.</summary>
    public int IdConvenioMotivo { get; set; }

    /// <summary>El numero de convenio. Requerido.</summary>
    public string Convenio { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>Requerido.</summary>
    public DateOnly? FechaInicio { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaTermino { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Modificacion de un convenio de obra.</summary>
public class ActualizarObraConvenioRequest : CrearObraConvenioRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Cierra o reabre un convenio de obra.</summary>
public class ActualizarObraConvenioStatusRequest
{
    /// <summary>true = cerrado, false = abierto.</summary>
    public bool ConvenioCerrado { get; set; }
}

// ======================================================================
// Grupos del catalogo de conceptos
// ======================================================================

/// <summary>Alta de un grupo (capitulo) del catalogo de conceptos.</summary>
public class CrearObraContratoGrupoRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>La clave del capitulo ("1", "2.1").</summary>
    public string? Clave { get; set; }

    /// <summary>Requerido.</summary>
    public string Grupo { get; set; } = string.Empty;
}

/// <summary>Modificacion de un grupo del catalogo de conceptos.</summary>
public class ActualizarObraContratoGrupoRequest : CrearObraContratoGrupoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Mueve un renglon una posicion arriba o abajo.
///
/// Es su propia operacion porque el SP la trata como un modo aparte: con
/// @Orden distinto de cero NO toca ningun otro campo del renglon.
/// </summary>
public class ReordenarRequest
{
    /// <summary>El contrato del renglon. Requerido: sin el, el SP no intercambia con el vecino.</summary>
    public int IdContrato { get; set; }

    /// <summary>"subir" o "bajar".</summary>
    public string Direccion { get; set; } = string.Empty;
}

// ======================================================================
// Conceptos del contrato
// ======================================================================

/// <summary>Filtros de la consulta de conceptos de un contrato.</summary>
public class ObraContratoConceptosFiltro
{
    public int IdContratoConcepto { get; set; }
    public int IdContratoGrupo { get; set; }
    public int IdContrato { get; set; }
    public int IdStatus { get; set; }
    public string? Concepto { get; set; }
    public bool Exacto { get; set; }
}

/// <summary>Alta de un concepto del catalogo de un contrato.</summary>
public class CrearObraContratoConceptoRequest
{
    /// <summary>El grupo (capitulo) al que pertenece. Requerido.</summary>
    public int IdContratoGrupo { get; set; }

    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>La unidad de medida del concepto.</summary>
    public int IdUnidadMedida { get; set; }

    /// <summary>La partida presupuestal del concepto.</summary>
    public int IdContratoPartida { get; set; }

    /// <summary>La clave del concepto. Requerida.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public string Concepto { get; set; } = string.Empty;

    public decimal PrecioUnitario { get; set; }
    public decimal Cantidad { get; set; }

    /// <summary>El ajuste de costos del concepto.</summary>
    public decimal Ajuste { get; set; }

    /// <summary>El concepto se estima global, no por cantidad.</summary>
    public bool ConceptoGlobal { get; set; }
}

/// <summary>Modificacion de un concepto del catalogo de un contrato.</summary>
public class ActualizarObraContratoConceptoRequest : CrearObraContratoConceptoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Carga del catalogo de conceptos desde un archivo.
///
/// El SP recibe el contenido en @Archivo y lo parte el mismo; aqui no se
/// interpreta nada, solo se le pasa.
/// </summary>
public class CargarObraContratoConceptosRequest
{
    /// <summary>El contenido del archivo. Requerido.</summary>
    public string Archivo { get; set; } = string.Empty;

    /// <summary>El numero de contrato al que se cargan. Requerido.</summary>
    public string Contrato { get; set; } = string.Empty;

    /// <summary>true = borrar el catalogo que ya tenia el contrato antes de cargar.</summary>
    public bool Limpiar { get; set; }

    /// <summary>true = crear los grupos que no existan.</summary>
    public bool CrearGrupo { get; set; }

    public string? ClaveGrupo { get; set; }

    /// <summary>Requerido.</summary>
    public string Grupo { get; set; } = string.Empty;

    public string? ClaveConcepto { get; set; }
    public string? Concepto { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Ajuste { get; set; }
    public bool ConceptoGlobal { get; set; }
    public string? PartidaPresupuestal { get; set; }
}

/// <summary>Filtros del acumulado de estimaciones por concepto.</summary>
public class ObraConceptosEstimacionesFiltro
{
    public int IdContratoEstimacionDetalle { get; set; }

    /// <summary>Requerido.</summary>
    public int IdContratoEstimacion { get; set; }

    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    public int IdContratoPartida { get; set; }
}

/// <summary>
/// Las cantidades estimadas de los conceptos de una estimacion.
///
/// Cinco listas paralelas que el SP recibe pegadas con "~" y recorre a la vez.
/// Que vayan como listas y no como cadenas preconcatenadas por el cliente es a
/// proposito: aqui se comprueba que tengan el mismo largo, y la cadena se arma
/// con punto decimal invariante. Legacy pasaba lo que mandara el cliente, y un
/// delimitador de mas en el camino se volvia una cantidad estimada equivocada
/// sin ningun error visible.
/// </summary>
public class ActualizarObraConceptoEstimacionRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>Requerido.</summary>
    public int IdContratoEstimacion { get; set; }

    public int IdContratoPartida { get; set; }

    /// <summary>Los conceptos que se estiman. Requerido, al menos uno.</summary>
    public List<int> IdsContratoConcepto { get; set; } = new();

    /// <summary>La cantidad estimada de cada concepto. Mismo largo que la lista de conceptos.</summary>
    public List<decimal> Cantidades { get; set; } = new();

    /// <summary>La cantidad excedente de cada concepto. Vacia o del mismo largo.</summary>
    public List<decimal> CantidadesExcedentes { get; set; } = new();

    /// <summary>El porcentaje de excedente de cada concepto. Vacia o del mismo largo.</summary>
    public List<decimal> PorcentajesExcedentes { get; set; } = new();

    /// <summary>El ajuste de costos de cada concepto. Vacia o del mismo largo.</summary>
    public List<decimal> Ajustes { get; set; } = new();
}

// ======================================================================
// Partidas del contrato
// ======================================================================

/// <summary>Filtros de la consulta de partidas de un contrato.</summary>
public class ObraContratoPartidasFiltro
{
    public int IdContratoPartida { get; set; }
    public int IdContrato { get; set; }
    public int IdPartida { get; set; }
    public int IdStatus { get; set; }
    public string? Partida { get; set; }
    public string? Comentario { get; set; }
    public bool Exacto { get; set; }
}

/// <summary>Alta de una partida presupuestal de un contrato.</summary>
public class CrearObraContratoPartidaRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>El convenio del que sale el monto, si viene de un convenio.</summary>
    public int IdConvenio { get; set; }

    /// <summary>La partida del clasificador presupuestal. Requerido.</summary>
    public int IdPartida { get; set; }

    public decimal Monto { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>Modificacion de una partida presupuestal de un contrato.</summary>
public class ActualizarObraContratoPartidaRequest : CrearObraContratoPartidaRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

// ======================================================================
// Periodos del contrato
// ======================================================================

/// <summary>Filtros de la consulta de periodos de un contrato.</summary>
public class ObraContratoPeriodosFiltro
{
    public int IdContratoPeriodo { get; set; }
    public int IdContrato { get; set; }
    public int IdStatus { get; set; }
    public string? Periodo { get; set; }
    public bool Exacto { get; set; }
}

/// <summary>Alta de un periodo del programa de obra.</summary>
public class CrearObraContratoPeriodoRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>El nombre del periodo. Requerido.</summary>
    public string Periodo { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public DateOnly? FechaInicio { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaTermino { get; set; }

    /// <summary>El avance fisico programado del periodo, en por ciento (0 a 100).</summary>
    public decimal PorcentajeAvanceFisicoProgramado { get; set; }
}

/// <summary>Modificacion de un periodo del programa de obra.</summary>
public class ActualizarObraContratoPeriodoRequest : CrearObraContratoPeriodoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

// ======================================================================
// Estimaciones
// ======================================================================

/// <summary>Alta de una estimacion de un contrato.</summary>
public class CrearObraContratoEstimacionRequest
{
    /// <summary>Requerido.</summary>
    public int IdContrato { get; set; }

    /// <summary>El representante tecnico del contratista que firma.</summary>
    public int IdProveedorRepresentanteTecnico { get; set; }

    /// <summary>El representante tecnico de la supervision externa.</summary>
    public int IdProveedorRepresentanteTecnicoSupExt { get; set; }

    /// <summary>Requerido.</summary>
    public DateOnly? FechaElaboracion { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaInicioEstimacion { get; set; }
    /// <summary>Requerido.</summary>
    public DateOnly? FechaTerminoEstimacion { get; set; }

    /// <summary>El nombre de la estimacion. Requerido.</summary>
    public string Nombre { get; set; } = string.Empty;

    public decimal TotalEstimacionProgramada { get; set; }

    /// <summary>Con esta estimacion el contrato queda terminado.</summary>
    public bool ContratoTerminado { get; set; }

    /// <summary>La fecha real de termino. Solo tiene sentido si el contrato queda terminado.</summary>
    public DateOnly? FechaRealTermino { get; set; }

    /// <summary>El avance fisico real a la fecha, en por ciento (0 a 100).</summary>
    public decimal PorcentajeAvanceFisicoReal { get; set; }
}

/// <summary>Modificacion de una estimacion de un contrato.</summary>
public class ActualizarObraContratoEstimacionRequest : CrearObraContratoEstimacionRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}
