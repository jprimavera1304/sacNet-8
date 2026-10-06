namespace ISL_Service.Application.DTOs.Cortes;

/*
  CORTES DE CAJA: LAS CUATRO LECTURAS QUE SI SON LECTURAS.

  De los siete endpoints de corte de legacy, tres llaman a
  sp_n_ConsultaConstantes antes de consultar —y ese procedimiento, cuando la
  fecha de operacion esta atrasada, hace el CIERRE DEL DIA: inserta en
  FechaOperacionLog, borra y rehace AlmacenProductoHist y adelanta
  Constantes.FechaOperacion. Son escrituras disfrazadas y no entran aqui.

  Los cuatro que quedan no lo llaman: detalle de un corte, siguiente fecha de
  corte, cobros de corte por centro de servicio y ventas canceladas de corte.

  SOBRE LOS `*Ftm`: igual que en el resto de la migracion, se manda el valor y no
  el texto ya formateado. La excepcion son los campos que el SP arma como UNA
  frase con varias partes (`folioFtm` = "EFE-123", `motivo`, `tipoRetiro`): esos
  no son el formato de un numero, son el dato.
*/

/// <summary>
/// Un renglon del detalle de un corte de caja: la cabecera del corte repetida
/// mas UN gasto.
///
/// Es la forma que devuelve el SP y se conserva: Mac31 pinta una rejilla con los
/// totales arriba y los gastos abajo, y los dos salen de aqui. Un corte sin
/// gastos devuelve un solo renglon con `idCorteDetalle = 0`.
/// </summary>
public class CorteDetalleDto
{
    public int IDCorte { get; set; }
    public DateTime? FechaCorte { get; set; }

    public decimal NoAplicadoDesglose { get; set; }
    public decimal LiquidacionTiendas { get; set; }
    public decimal NoAplicado { get; set; }
    public decimal SobranteLiquidaciones { get; set; }
    public decimal SaeEsquina { get; set; }
    public decimal SaeEsquinaDomingo { get; set; }
    public decimal SaeLocalTauro { get; set; }
    public decimal TotalSae { get; set; }
    public decimal MacCamioneta { get; set; }
    public decimal MacAgentes { get; set; }
    public decimal TotalMac { get; set; }
    public decimal Transferencias { get; set; }
    public decimal TransferenciasTiendaEsquina { get; set; }
    public decimal TransferenciasLocalTauro { get; set; }
    public decimal DepositoCheque { get; set; }
    public decimal DepositoEfectivo { get; set; }
    public decimal Tarjetas { get; set; }
    public decimal TarjetasTiendas { get; set; }
    public decimal Cheque { get; set; }
    public decimal Efectivo { get; set; }
    public decimal Morraya { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal TotalDesglose { get; set; }
    public decimal GranTotal { get; set; }

    public string? Observaciones { get; set; }
    public string? ObservacionesCheque { get; set; }
    public string? ObservacionesDepositosCheque { get; set; }
    public string? ObservacionesDepositoEfectivo { get; set; }
    public string? ObservacionesSobranteLiquidaciones { get; set; }

    /// <summary>0 = el corte no tiene gastos capturados.</summary>
    public int IDCorteDetalle { get; set; }
    public int IDStatus { get; set; }
    public int IDTipoGasto { get; set; }
    /// <summary>El SP lo deja en "GASTO" cuando el tipo esta nulo.</summary>
    public string TipoGasto { get; set; } = string.Empty;
    public string? Concepto { get; set; }
    public decimal Monto { get; set; }
}

/// <summary>
/// La siguiente fecha de corte. Es un solo renglon.
///
/// No es "el corte actual" pese al nombre del endpoint de legacy: el SP toma el
/// ultimo corte no cancelado y le suma un dia, y si cae en domingo suma otro.
/// </summary>
public class CorteSiguienteFechaDto
{
    /// <summary>El IDCorte del ULTIMO corte, no de uno nuevo.</summary>
    public int IDCorte { get; set; }
    public DateTime? FechaCorte { get; set; }
}

/// <summary>
/// Un corte de caja de centro de servicio con sus saldos por forma de pago.
///
/// Los nombres vienen de la tabla temporal del SP y se conservan porque son los
/// que usa la pantalla de Mac31: SaldoAnterior*, Pagos*, Retiro*, SaldoActual*
/// para las seis formas de pago (efectivo, cheque, transferencia, tarjeta de
/// credito, tarjeta de debito, deposito en efectivo).
/// </summary>
public class CorteCsCobroDto
{
    public int IDCentro { get; set; }
    public string NombreCentro { get; set; } = string.Empty;
    public int IDCobroDineroUsado { get; set; }
    public int IDCorte { get; set; }
    public int Folio { get; set; }
    /// <summary>
    /// "EFE-123", "CHE-45"... El prefijo sale del tipo de retiro. Se conserva
    /// porque es el folio como lo dice la gente, no el formato de un numero.
    /// </summary>
    public string? FolioFtm { get; set; }
    public int NumPagos { get; set; }
    public string? Observaciones { get; set; }
    public int IDTipoRetiro { get; set; }
    /// <summary>EFECTIVO, CHEQUE, TRANSFERENCIA, TARJETA o DEPOSITO EFECTIVO.</summary>
    public string? TipoRetiro { get; set; }
    public int IDUsuario { get; set; }
    public string? Usuario { get; set; }
    public string? Equipo { get; set; }
    public DateTime? Fecha { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    public decimal PagosSalidaEfectivo { get; set; }

    public decimal SaldoAnteriorEfectivo { get; set; }
    public decimal SaldoAnteriorCheque { get; set; }
    public decimal SaldoAnteriorTransferencia { get; set; }
    public decimal SaldoAnteriorTarjetaCredito { get; set; }
    public decimal SaldoAnteriorTarjetaDebito { get; set; }
    public decimal SaldoAnteriorDepEfectivo { get; set; }

    public decimal PagosEfectivo { get; set; }
    public decimal PagosCheque { get; set; }
    public decimal PagosTransferencia { get; set; }
    public decimal PagosTarjetaCredito { get; set; }
    public decimal PagosTarjetaDebito { get; set; }
    public decimal PagosDepEfectivo { get; set; }

    public decimal RetiroEfectivo { get; set; }
    public decimal RetiroCheque { get; set; }
    public decimal RetiroTransferencia { get; set; }
    public decimal RetiroTarjetaCredito { get; set; }
    public decimal RetiroTarjetaDebito { get; set; }
    public decimal RetiroDepEfectivo { get; set; }

    public decimal SaldoActualEfectivo { get; set; }
    public decimal SaldoActualCheque { get; set; }
    public decimal SaldoActualTransferencia { get; set; }
    public decimal SaldoActualTarjetaCredito { get; set; }
    public decimal SaldoActualTarjetaDebito { get; set; }
    public decimal SaldoActualDepEfectivo { get; set; }
}

/// <summary>
/// Una venta cancelada que salio por un corte de centro de servicio.
/// </summary>
public class CorteVentaCanceladaCsDto
{
    public int IDCorteVentaCanceladaCS { get; set; }
    public int IDCorte { get; set; }
    public int IDCorteCancelado { get; set; }
    public int IDVenta { get; set; }
    public int IDPagoVenta { get; set; }
    public int IDTipoPago { get; set; }
    public int FolioVenta { get; set; }
    /// <summary>
    /// El folio con su prefijo, como lo calcula fn_DevuelvePrefijoFolio. Es el
    /// folio que la gente lee en el papel.
    /// </summary>
    public string? FolioVentaFtm { get; set; }
    public DateTime? FechaVenta { get; set; }
    public DateTime? FechaVentaCancelacion { get; set; }
    public decimal Abono { get; set; }
    /// <summary>
    /// "SALIDA POR FOLIO &lt;folio&gt;  CANCELADO EL &lt;fecha&gt;", armado por el SP.
    /// Lo que este guardado en la base se pisa: el SP lo reescribe en todas las
    /// filas antes de devolverlas.
    /// </summary>
    public string? Motivo { get; set; }
}

/// <summary>
/// Las ventas canceladas de un corte MAS su total.
///
/// El total va aparte y no repetido en cada renglon. El SP lo devuelve como una
/// columna `SumaAbonos` con el mismo valor en todas las filas, y legacy lo
/// PERDIA: su DTO no declaraba esa columna, asi que el total nunca llegaba a la
/// pantalla aunque la base lo calculara.
/// </summary>
public class CorteVentasCanceladasCsDto
{
    public List<CorteVentaCanceladaCsDto> Renglones { get; set; } = new();
    public decimal SumaAbonos { get; set; }
}
