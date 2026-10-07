namespace ISL_Service.Application.DTOs.Aviacion;

/*
  LOS CATALOGOS DE AVIACION: CAJAS, CONCEPTOS, CUENTAS, ECONOMICOS, MOTIVOS DE
  FIANZA, OPERADORES, PERMISIONARIOS E IMPRESORAS.

  TRES COSAS QUE HAY QUE ENTENDER ANTES DE LEER LOS DTO:

  1. HAY DOS BASES Y SE ELIGE CON UN PARAMETRO, NO CON DOS RUTAS.
     Legacy tenia /consultar y /consultar/server para cada catalogo: la primera
     pregunta a la replica local de la caja y la segunda al servidor central de
     aviacion, cuya cadena de conexion vive en dbo.Constantes.AviacionDBServer.
     Aqui es el mismo endpoint con `central=true|false`. Es la misma pregunta
     contra dos origenes, y dos rutas que devuelven el mismo renglon son dos
     contratos que se desincronizan.

  2. "ACEPTAR" ES PARTE DEL FLUJO, NO UN DETALLE.
     Cuentas, economicos, operadores y permisionarios se dan de alta en una caja
     y quedan PENDIENTES DE ACEPTACION hasta que alguien del central los acepta.
     Por eso cada uno tiene su /aceptar y su filtro `pendienteAceptacion`.

  3. DESPUES DE ESCRIBIR HAY QUE SINCRONIZAR, Y SOLO SI ESTA INSTALACION ES UNA
     CAJA. Legacy llama a sp_n_SincronizarDownload una vez por cada tabla que el
     cambio toca, y solo cuando Constantes.AviacionIDCaja no es 0. Se respeta
     tabla por tabla; la lista esta en AviacionCatalogosService.

  Lo que NO esta en ningun DTO: IDUsuario y Equipo. Legacy los pedia en el
  cuerpo. Salen del token.
*/

// ======================================================================
// Cajas
// ======================================================================

/// <summary>
/// Modificacion de una caja de recaudacion.
///
/// No hay alta: en Legacy el insertAsync de cajas esta comentado
/// (CatCajasController.cs:28-39) porque una caja se crea instalando el equipo,
/// no desde una pantalla. Aqui tampoco se expone.
///
/// Tampoco hay cambio de status: sp_n_ActualizarCaja no recibe @IDStatus, asi
/// que ofrecerlo seria mentir.
/// </summary>
public class ActualizarAviacionCajaRequest
{
    /// <summary>El numero de caja. Requerido.</summary>
    public int NumCaja { get; set; }

    /// <summary>Requerido.</summary>
    public string NombreCaja { get; set; } = string.Empty;

    /// <summary>El nombre del equipo donde corre la caja. Requerido.</summary>
    public string EquipoCaja { get; set; } = string.Empty;

    /// <summary>Esta caja es la maestra de la instalacion.</summary>
    public bool MaestraCaja { get; set; }
}

// ======================================================================
// Conceptos
// ======================================================================

/// <summary>Filtros de la consulta de conceptos de aviacion.</summary>
public class AviacionConceptosFiltro
{
    public int IdConcepto { get; set; }
    public int IdStatus { get; set; }
    public string? NumConcepto { get; set; }
    public string? NombreConcepto { get; set; }
}

/// <summary>Alta de un concepto de aviacion.</summary>
public class CrearAviacionConceptoRequest
{
    /// <summary>
    /// El numero de concepto. Es texto, no numero: lleva ceros a la izquierda
    /// y asi lo declara el DTO de Legacy.
    /// </summary>
    public string NumConcepto { get; set; } = string.Empty;

    public string NombreConcepto { get; set; } = string.Empty;
}

/// <summary>Modificacion de un concepto de aviacion.</summary>
public class ActualizarAviacionConceptoRequest : CrearAviacionConceptoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

// ======================================================================
// Cuentas
// ======================================================================

/// <summary>Filtros de la consulta de cuentas de aviacion.</summary>
public class AviacionCuentasFiltro
{
    public int IdCuenta { get; set; }
    public int IdConcepto { get; set; }
    public int IdStatus { get; set; }
    public string? NumCuenta { get; set; }
    public string? CuentaContable { get; set; }
    public string? NombreCuenta { get; set; }

    /// <summary>"D" o "H": si la cuenta es de cargo o de abono.</summary>
    public string? DebeHaber { get; set; }

    public int Auxiliar { get; set; }
    public int TarjetaDistribucion { get; set; }

    /// <summary>1 = solo las que esperan aceptacion del central.</summary>
    public int PendienteAceptacion { get; set; }
}

/// <summary>Alta de una cuenta de aviacion.</summary>
public class CrearAviacionCuentaRequest
{
    /// <summary>El concepto al que cuelga. Requerido.</summary>
    public int IdConcepto { get; set; }

    /// <summary>Requerido.</summary>
    public string NumCuenta { get; set; } = string.Empty;

    public string? CuentaContable { get; set; }

    /// <summary>Requerido.</summary>
    public string NombreCuenta { get; set; } = string.Empty;

    public decimal Importe { get; set; }

    /// <summary>El importe es fijo y no se puede cambiar al cobrar.</summary>
    public bool ImporteFijo { get; set; }

    /// <summary>La cuenta se cobra cada periodo sin que nadie la capture.</summary>
    public bool EsRecurrente { get; set; }

    /// <summary>"D" (debe) o "H" (haber).</summary>
    public string? DebeHaber { get; set; }

    public string? Enlace { get; set; }
    public string? SRojo { get; set; }

    public bool Auxiliar { get; set; }
    public bool CuentaGlobalizada { get; set; }
    public bool CuentaIndividual { get; set; }
    public bool PagoVirtual { get; set; }
}

/// <summary>Modificacion de una cuenta de aviacion.</summary>
public class ActualizarAviacionCuentaRequest : CrearAviacionCuentaRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Los porcentajes de distribucion por tarjeta de varias cuentas, de golpe.
///
/// Dos listas paralelas que el SP recibe pegadas con "~" y recorre a la vez.
/// Legacy las recibia ya concatenadas desde el cliente; aqui se comprueba que
/// midan lo mismo, que los porcentajes esten entre 0 y 100 y que sumen 100, y
/// la cadena se arma con punto decimal invariante.
/// </summary>
public class ActualizarAviacionTarjetaPorcentajesRequest
{
    /// <summary>Las cuentas. Requerido, al menos una.</summary>
    public List<int> IdsCuenta { get; set; } = new();

    /// <summary>El porcentaje de cada cuenta. Mismo largo que la lista de cuentas.</summary>
    public List<decimal> Porcentajes { get; set; } = new();
}

// ======================================================================
// Economicos
// ======================================================================

/// <summary>Filtros de la consulta de economicos (unidades).</summary>
public class AviacionEconomicosFiltro
{
    public int IdEconomico { get; set; }
    public int IdPermisionario { get; set; }
    public int IdStatus { get; set; }
    public int NumEconomico { get; set; }
    public string? NumCuentaEconomico { get; set; }
    public string? NombreEconomico { get; set; }
    public int EsMicro { get; set; }
    public int PendienteAceptacion { get; set; }
}

/// <summary>Alta de un economico (una unidad).</summary>
public class CrearAviacionEconomicoRequest
{
    /// <summary>El permisionario dueno de la unidad. Requerido.</summary>
    public int IdPermisionario { get; set; }

    /// <summary>El numero economico de la unidad. Requerido.</summary>
    public int NumEconomico { get; set; }

    public string? NumCuentaEconomico { get; set; }

    /// <summary>Requerido.</summary>
    public string NombreEconomico { get; set; } = string.Empty;

    /// <summary>La unidad es un microbus.</summary>
    public bool EsMicro { get; set; }

    /// <summary>La unidad paga recaudacion.</summary>
    public bool EsRecaudacion { get; set; }

    /// <summary>La unidad paga amortizacion.</summary>
    public bool EsAmortizacion { get; set; }

    /// <summary>La unidad paga inscripcion.</summary>
    public bool EsInscripcion { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Modificacion de un economico.</summary>
public class ActualizarAviacionEconomicoRequest : CrearAviacionEconomicoRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Cambia solo que conceptos paga un economico.</summary>
public class ActualizarAviacionEconomicoAmortizacionRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;

    public bool EsRecaudacion { get; set; }
    public bool EsAmortizacion { get; set; }
    public bool EsInscripcion { get; set; }
}

/// <summary>
/// Pasa un economico de un permisionario a otro.
///
/// El SP recibe el permisionario ANTERIOR y el NUEVO, y guarda el movimiento en
/// el historico ([Catalogo Economicos His]), por eso la reasignacion sincroniza
/// cuatro tablas y no una.
/// </summary>
public class ReasignarAviacionEconomicoRequest
{
    /// <summary>El permisionario que lo tiene hoy. Requerido.</summary>
    public int IdPermisionarioAnterior { get; set; }

    /// <summary>El permisionario que lo recibe. Requerido.</summary>
    public int IdPermisionarioNuevo { get; set; }

    public bool EsRecaudacion { get; set; }
    public bool EsAmortizacion { get; set; }
    public bool EsInscripcion { get; set; }
}

/// <summary>Filtros de la consulta de auxiliares de un economico.</summary>
public class AviacionEconomicosAuxiliaresFiltro
{
    public int IdRecaudacion { get; set; }
    public int IdEconomico { get; set; }
    public int NumEconomico { get; set; }
    public int IdPermisionario { get; set; }
    public int IdCuenta { get; set; }
    public int NumCuenta { get; set; }
    public int IdCaja { get; set; }
    public int StatusPago { get; set; }
}

// ======================================================================
// Motivos de fianza
// ======================================================================

/// <summary>Filtros de la consulta de motivos de fianza.</summary>
public class AviacionMotivosFianzaFiltro
{
    public int IdMotivoFianza { get; set; }
    public int IdStatus { get; set; }
    public int NumMotivoFianza { get; set; }
    public string? MotivoFianza { get; set; }
    public int EsIngreso { get; set; }
    public int EsEgreso { get; set; }
}

/// <summary>Alta de un motivo de fianza.</summary>
public class CrearAviacionMotivoFianzaRequest
{
    /// <summary>Requerido.</summary>
    public int NumMotivoFianza { get; set; }

    public string? CuentaContable { get; set; }

    /// <summary>Requerido.</summary>
    public string MotivoFianza { get; set; } = string.Empty;

    /// <summary>El motivo suma a la fianza.</summary>
    public bool EsIngreso { get; set; }

    /// <summary>El motivo resta de la fianza.</summary>
    public bool EsEgreso { get; set; }

    /// <summary>El motivo exige documento.</summary>
    public bool EsDocumento { get; set; }

    /// <summary>El motivo se lleva a la poliza contable.</summary>
    public bool EsContabilizable { get; set; }
}

/// <summary>
/// Modificacion de un motivo de fianza.
///
/// Ojo: sp_n_ActualizarMotivoFianza NO recibe @EsIngreso, @EsEgreso,
/// @EsDocumento ni @EsContabilizable — solo el alta los recibe. O sea que una
/// vez dado de alta, un motivo no cambia de sentido. No se exponen aqui para no
/// prometer un cambio que la base no hace.
/// </summary>
public class ActualizarAviacionMotivoFianzaRequest
{
    /// <summary>Requerido.</summary>
    public int NumMotivoFianza { get; set; }

    public string? CuentaContable { get; set; }

    /// <summary>Requerido.</summary>
    public string MotivoFianza { get; set; } = string.Empty;

    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

// ======================================================================
// Operadores
// ======================================================================

/// <summary>Filtros de la consulta de operadores.</summary>
public class AviacionOperadoresFiltro
{
    public int IdOperador { get; set; }
    public int IdCuenta { get; set; }
    public int IdStatus { get; set; }
    public int NumNomina { get; set; }
    public string? NumCuentaOperador { get; set; }
    public string? OperadorNombre { get; set; }
    public string? OperadorApellidoPaterno { get; set; }
    public string? OperadorApellidoMaterno { get; set; }
    public int PendienteAceptacion { get; set; }
}

/// <summary>Alta de un operador.</summary>
public class CrearAviacionOperadorRequest
{
    /// <summary>
    /// El operador al que sustituye, si esta alta es un reemplazo. 0 = ninguno.
    /// </summary>
    public int IdOperadorAnterior { get; set; }

    /// <summary>Requerido.</summary>
    public int NumNomina { get; set; }

    public string? NumCuentaOperador { get; set; }

    /// <summary>Requerido.</summary>
    public string OperadorNombre { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public string OperadorApellidoPaterno { get; set; } = string.Empty;

    public string? OperadorApellidoMaterno { get; set; }

    /// <summary>Requerida.</summary>
    public DateOnly? FechaAlta { get; set; }
}

/// <summary>Modificacion de un operador.</summary>
public class ActualizarAviacionOperadorRequest : CrearAviacionOperadorRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

/// <summary>Cambia el importe de la fianza de un operador.</summary>
public class ActualizarAviacionOperadorFianzaRequest
{
    public decimal ImporteFianza { get; set; }
}

/// <summary>
/// Asigna una cuenta a un operador con su importe.
///
/// En Legacy este endpoint mandaba `ImporteFianza` al parametro `@Importe` del
/// SP (CatOperadorRepository, spActualizarCuenta: @Importe recibia ImporteFianza).
/// Son dos cosas distintas que compartian campo porque el DTO era uno para
/// todas las operaciones; aqui el campo se llama como lo que es.
/// </summary>
public class ActualizarAviacionOperadorCuentaRequest
{
    /// <summary>La cuenta que se le asigna. Requerido.</summary>
    public int IdCuenta { get; set; }

    /// <summary>El importe de esa cuenta para este operador.</summary>
    public decimal Importe { get; set; }
}

// ======================================================================
// Permisionarios
// ======================================================================

/// <summary>Filtros de la consulta de permisionarios.</summary>
public class AviacionPermisionariosFiltro
{
    public int IdPermisionario { get; set; }
    public int IdStatus { get; set; }
    public int NumPermisionario { get; set; }
    public string? CuentaContablePermisionario { get; set; }
    public string? NombrePermisionario { get; set; }
    public int PendienteAceptacion { get; set; }
}

/// <summary>Alta de un permisionario.</summary>
public class CrearAviacionPermisionarioRequest
{
    /// <summary>Requerido.</summary>
    public int NumPermisionario { get; set; }

    /// <summary>Requerido.</summary>
    public string NombrePermisionario { get; set; } = string.Empty;

    public string? CuentaContablePermisionario { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Modificacion de un permisionario.</summary>
public class ActualizarAviacionPermisionarioRequest : CrearAviacionPermisionarioRequest
{
    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; } = 1;
}

// ======================================================================
// Impresoras
// ======================================================================

/// <summary>
/// Las impresoras configuradas para un equipo.
///
/// Aqui `equipo` NO es la firma de auditoria: es la LLAVE, el nombre de la
/// computadora de cuyas impresoras se habla (sp_n_ConsultaImpresoras recibe
/// @IDImpresora y @Equipo como filtros). Es el unico sitio de los dos modulos
/// donde el equipo es un dato y no una identidad, y por eso aqui si viaja en la
/// peticion. La identidad —quien hizo el cambio— sigue saliendo del token.
/// </summary>
public class AviacionImpresorasFiltro
{
    public int IdImpresora { get; set; }

    /// <summary>El equipo cuyas impresoras se consultan.</summary>
    public string? Equipo { get; set; }
}

/// <summary>Modificacion de las impresoras de un equipo.</summary>
public class ActualizarAviacionImpresoraRequest
{
    /// <summary>
    /// El equipo cuya configuracion se cambia. Requerido: es la llave del
    /// renglon, no la firma.
    /// </summary>
    public string Equipo { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public string ImpresoraTickets { get; set; } = string.Empty;

    /// <summary>Requerido.</summary>
    public string ImpresoraReportes { get; set; } = string.Empty;
}
