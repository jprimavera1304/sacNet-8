namespace ISL_Service.Application.DTOs.Aviacion;

/*
  POLIZAS DE CHEQUES DE AVIACION.

  Una poliza agrupa los cheques con los que se paga a los permisionarios en un
  rango de fechas. El encabezado (concepto, cuentas contables, banco, quien
  revisa y quien autoriza) se guarda aparte y se reusa, y la generacion de la
  poliza puede, de paso, actualizar ese encabezado: eso es el
  `cambiarEncabezado` del alta.

  Todo este modulo vive en el servidor central de aviacion
  (Constantes.AviacionDBServer), no en la base de la empresa.
*/

/// <summary>Filtros de la consulta de polizas.</summary>
public class AviacionPolizasFiltro
{
    public int IdPoliza { get; set; }
    public int IdPolizaEncabezado { get; set; }
    public int IdStatus { get; set; }

    public DateOnly? FechaInicial { get; set; }
    public DateOnly? FechaFinal { get; set; }

    public int NumChequeInicial { get; set; }
    public int NumChequeFinal { get; set; }
    public int NumPolizaInicial { get; set; }
    public int NumPolizaFinal { get; set; }
}

/// <summary>
/// Genera una poliza de cheques.
///
/// El SP la arma recorriendo los cobros del rango y emitiendo un cheque por
/// permisionario, a partir del numero de cheque y del numero de poliza que se le
/// dan.
/// </summary>
public class CrearAviacionPolizaRequest
{
    /// <summary>El encabezado que se usa. Requerido.</summary>
    public int IdPolizaEncabezado { get; set; }

    /// <summary>Requerida.</summary>
    public DateOnly? FechaInicial { get; set; }
    /// <summary>Requerida.</summary>
    public DateOnly? FechaFinal { get; set; }

    /// <summary>El primer numero de cheque a usar. Requerido.</summary>
    public int NumChequeInicial { get; set; }

    /// <summary>El primer numero de poliza a usar. Requerido.</summary>
    public int NumPolizaInicial { get; set; }

    /// <summary>
    /// true = de paso, guarda en el encabezado los datos que vienen abajo.
    /// Legacy lo llamaba `cambioPolizaEncabezado`.
    /// </summary>
    public bool CambiarEncabezado { get; set; }

    // --- Los datos del encabezado ---
    public string? ConceptoPoliza { get; set; }
    public string? CuentaDebe { get; set; }
    public string? CuentaHaber { get; set; }
    public string? NombreCuentaBancaria { get; set; }
    public string? BancoCuenta { get; set; }
    public string? BancoSubCuenta { get; set; }

    /// <summary>El porcentaje que se retiene del cheque, de 0 a 100.</summary>
    public decimal ChequePorcentaje { get; set; }

    public string? ChequeIdentificador { get; set; }
    public string? RevisadoPor { get; set; }
    public string? AutorizadoPor { get; set; }

    /// <summary>La fecha que llevan los cheques.</summary>
    public DateOnly? FechaCheques { get; set; }

    /// <summary>El dia del mes que llevan los cheques, cuando no es una fecha fija.</summary>
    public int DiaCheques { get; set; }
}

/// <summary>Modificacion de un encabezado de poliza.</summary>
public class ActualizarAviacionPolizaEncabezadoRequest
{
    public string? ConceptoPoliza { get; set; }
    public string? CuentaDebe { get; set; }
    public string? CuentaHaber { get; set; }
    public string? NombreCuentaBancaria { get; set; }
    public string? BancoCuenta { get; set; }
    public string? BancoSubCuenta { get; set; }

    /// <summary>De 0 a 100.</summary>
    public decimal ChequePorcentaje { get; set; }

    public string? ChequeIdentificador { get; set; }
    public string? RevisadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
}

/// <summary>
/// Corrige el numero de cheque y el numero de poliza de un renglon.
///
/// Es la operacion de "se atasco la impresora y se brinco dos cheques".
/// </summary>
public class ActualizarAviacionPolizaChequeRequest
{
    /// <summary>La poliza del renglon. Requerido.</summary>
    public int IdPoliza { get; set; }

    /// <summary>El numero de cheque nuevo. Requerido.</summary>
    public int NumCheque { get; set; }

    /// <summary>El numero de poliza nuevo. Requerido.</summary>
    public int NumPoliza { get; set; }
}

/// <summary>
/// Los parametros de los tres reportes de poliza (ingresos, fianzas y
/// documentos).
///
/// Estos tres SP (sp_n_rptPolizaIngresos, sp_n_rptPolizaFianzas,
/// sp_n_rptPolizaDocumentos) reciben UN SOLO parametro, @Params, que es una
/// cadena con los valores pegados. Legacy la recibia ya concatenada desde el
/// cliente y la reenviaba tal cual, sin mirarla.
///
/// Los tres procedimientos NO estan instalados en Produccion_svr, asi que su
/// orden de parametros —que es el contrato— no se puede leer de esta base. Por
/// eso aqui se recibe la LISTA de valores y el servidor arma la cadena con el
/// delimitador "~" y con formato invariante. Es exactamente lo que Legacy
/// reenviaba, menos la posibilidad de que el cliente rompa el delimitador o
/// escriba "12,5" en vez de "12.5". Cuando la base de aviacion este disponible
/// se puede tipar campo por campo.
/// </summary>
public class AviacionPolizaReporteFiltro
{
    /// <summary>Los valores del reporte, en el orden que espera el SP.</summary>
    public List<string> Parametros { get; set; } = new();
}
