namespace ISL_Service.Application.DTOs.VentasMultiPago;

/*
  MULTI PAGO: COBRAR VARIAS REMISIONES DE UN SOLO GOLPE

  Es el boton "Multi pago" de CONSULTA DE VENTAS. En Mac31 vive en
  Forms/Ventas/ConsultarVentas.cs, btnMultiPago_Click (linea 4017), que arma la
  lista de IDVenta seleccionados y abre Forms/Ventas/VentasPago.cs con el
  constructor de la linea 158 ("Multipago Cobro").

  TODO lo que hay en estos DTOs sale de ese formulario. Donde no era obvio se
  cita archivo y linea, porque la pantalla del web tiene que dar EXACTAMENTE
  los mismos numeros que Mac31 — no parecidos.

  QUE *NO* LLEGA AQUI DESDE VENTAS, Y POR QUE IMPORTA
  ---------------------------------------------------
  ConsultarVentas.cs llama al formulario con IDCobro = 0 (linea 4025, declarado
  y jamas asignado) y SaldoCascos = 0 (linea 4024, idem). Y el DTO de entrada
  solo lleva IDsVentaLst, asi que NuevoEsquemaPagos queda en 0. De ahi salen
  tres consecuencias que este modulo da por hechas:

    * chkEsDiffusados solo se muestra con UNA remision y SaldoCascos > 0
      (VentasPago.cs 613-620). Como SaldoCascos siempre es 0 por esta puerta,
      nunca se ve: DiffUsados va en 0 siempre.
    * rbAjusteAbono solo existe con NuevoEsquemaPagos = 1 (VentasPago.cs 1852).
      Por esta puerta no aplica.
    * En Zaragoza, "sin hoja de cobro" es la regla, no la excepcion. Ver
      VentasMultiPagoService.
*/

/* ------------------------------------------------------------------ */
/* Entrada                                                             */
/* ------------------------------------------------------------------ */

public class VentasMultiPagoPantallaRequest
{
    /// <summary>Los IDVenta marcados en la pantalla de Ventas.</summary>
    public List<int> IdsVenta { get; set; } = new();
}

/// <summary>Lo que se le carga a UNA remision de la lista.</summary>
public class VentasMultiPagoRenglonPago
{
    public int IdVenta { get; set; }
    public decimal Monto { get; set; }
}

public class VentasMultiPagoGuardarRequest
{
    public List<int> IdsVenta { get; set; } = new();

    /// <summary>El reparto por remision: la columna PAGO del grid de Mac31.</summary>
    public List<VentasMultiPagoRenglonPago> Pagos { get; set; } = new();

    /// <summary>enumTipoPagos de Mac31 (Utils/Enums.cs, linea 134).</summary>
    public int IdTipoPago { get; set; }

    /// <summary>txtAbono: el dinero que entra en esta operacion.</summary>
    public decimal Abono { get; set; }

    /// <summary>
    /// El importe del documento (cheque / transferencia / tarjeta / deposito).
    /// Puede ser MAYOR que el abono: un cheque de 5,000 puede abonar 3,000.
    /// </summary>
    public decimal MontoTotal { get; set; }

    public int IdBancoCheque { get; set; }
    public int IdBancoTransfer { get; set; }
    public int IdBancoTarjeta { get; set; }
    public int IdBancoDepositoEfe { get; set; }

    public string NumeroCheque { get; set; } = string.Empty;
    public string Transferencia { get; set; } = string.Empty;
    public string NumeroTarjeta { get; set; } = string.Empty;
    public string DepositoEfectivoNumero { get; set; } = string.Empty;

    /// <summary>1 credito, 2 debito. Solo lo pregunta Zaragoza; ver el DTO de pantalla.</summary>
    public int TipoTarjeta { get; set; }

    /// <summary>Motivo del descuento / nota de credito / cargo / casco por kilo.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Observaciones libres (txtMotivo de Mac31).</summary>
    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Monto del cargo adicional (txtCargo).</summary>
    public decimal Cargo { get; set; }

    /* Casco por kilo: solo Tauro. El precio NO viaja, lo pone el servidor
       desde Constantes.PrecioCascoKilo — si viajara, cualquiera podria
       cobrarse los cascos al precio que quisiera. */
    public decimal CascoKiloCantidad { get; set; }

    /* Liquidacion: solo Tauro (VentasPago.cs 693-697 y 2676-2707). */
    public string Liquidacion { get; set; } = "agente";   // "agente" | "camioneta"
    public int IdAgenteLiquidacion { get; set; }
    public int IdRepartidorLiquidacion { get; set; }

    /// <summary>
    /// La clave del dia que Zaragoza pide para ciertos pagos
    /// (VentasPago.cs 3604-3640). Vacia cuando no hace falta.
    /// </summary>
    public string ContrasenaAutorizacion { get; set; } = string.Empty;

    /// <summary>
    /// El usuario ya confirmo que quiere pagar de mas. Equivale al
    /// "¿DESEA REALIZAR UN PAGO MAYOR AL SALDO?" de VentasPago.cs 3351.
    /// </summary>
    public bool ConfirmaPagoMayorAlSaldo { get; set; }

    /// <summary>Confirmacion del cargo adicional (VentasPago.cs 3407).</summary>
    public bool ConfirmaCargo { get; set; }

    /// <summary>Confirmacion de aplicar el saldo a favor (VentasPago.cs 3441).</summary>
    public bool ConfirmaSaldoAFavor { get; set; }
}

/* ------------------------------------------------------------------ */
/* Salida                                                              */
/* ------------------------------------------------------------------ */

/// <summary>Una remision del grid, con los mismos campos que pinta Mac31.</summary>
public class VentasMultiPagoRemision
{
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }
    public string Empresa { get; set; } = string.Empty;
    public string FolioFtm { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>
    /// La columna del medio: en Tauro es TOTAL PAGAR y en Zaragoza es IMPORTE.
    /// No es el mismo numero — ver SetColumnasRemisiones (VentasPago.cs 1310).
    /// El nombre que le toca va en `etiquetaImporte` de la respuesta.
    /// </summary>
    public decimal Importe { get; set; }

    public decimal Cargos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Abonos { get; set; }

    /// <summary>
    /// El saldo que se puede cobrar. Tauro usa la columna `Saldo` y Zaragoza
    /// `SaldoImporte` (VentasPago.cs 1256-1259 y 1370-1388). Elegir mal la
    /// columna descuadra el total de la pantalla.
    /// </summary>
    public decimal Saldo { get; set; }

    public decimal SaldoFavor { get; set; }

    /// <summary>Saldo de cascos de la remision. Informativo en esta pantalla.</summary>
    public decimal SaldoCascos { get; set; }

    /// <summary>Lo que ya se cobro en la barra de cobro de Zaragoza, si hay.</summary>
    public int FolioCobro { get; set; }
}

/// <summary>Una remision que Mac31 se NIEGA a incluir, y por que.</summary>
public class VentasMultiPagoRechazo
{
    public int IdVenta { get; set; }
    public string FolioFtm { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Una forma de pago, con lo que hay que llenar si se elige.</summary>
public class VentasMultiPagoFormaDePago
{
    /// <summary>enumTipoPagos de Mac31.</summary>
    public int IdTipoPago { get; set; }

    /// <summary>Clave corta para el front ("efectivo", "cheque", ...).</summary>
    public string Clave { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Que campos se destapan al elegirla. Ver rbEfectivo_Click (VentasPago.cs 1783).</summary>
    public bool PideBanco { get; set; }
    public bool PideNumero { get; set; }
    public bool PideMontoTotal { get; set; }
    public bool PideMotivo { get; set; }
    public bool PideTipoTarjeta { get; set; }
    public bool PideKilos { get; set; }
    public bool PideCargo { get; set; }

    /// <summary>Etiqueta del campo de numero ("N° de cheque", "Clave del deposito"...).</summary>
    public string EtiquetaNumero { get; set; } = string.Empty;

    /// <summary>Banco preseleccionado (el de la empresa, para el deposito).</summary>
    public int BancoPorOmision { get; set; }

    /// <summary>
    /// Si NO se puede usar, aqui va el por que. La opcion se manda IGUAL y se
    /// pinta encendida: los botones no se apagan, contestan.
    /// </summary>
    public string Impedimento { get; set; } = string.Empty;

    /// <summary>Esta forma pide la clave del dia antes de guardar.</summary>
    public bool PideAutorizacion { get; set; }
}

public class VentasMultiPagoCatalogoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class VentasMultiPagoPantallaResponse
{
    /// <summary>"TAU" o "ZARA": Constantes.Funcionalidad, igual que Mac31.</summary>
    public string Funcionalidad { get; set; } = string.Empty;

    public int EsCentroServicio { get; set; }

    /// <summary>El encabezado "FECHA PAGOS:" (VentasPago.cs 195 y 226).</summary>
    public string FechaPagos { get; set; } = string.Empty;

    public List<VentasMultiPagoRemision> Remisiones { get; set; } = new();
    public List<VentasMultiPagoRechazo> Rechazadas { get; set; } = new();

    /// <summary>Suma de los saldos. Es el txtSaldo de Mac31 (VentasPago.cs 1263).</summary>
    public decimal SaldoTotal { get; set; }

    public decimal ImporteTotal { get; set; }
    public decimal CargosTotal { get; set; }
    public decimal DescuentosTotal { get; set; }
    public decimal AbonosTotal { get; set; }

    /// <summary>"TOTAL PAGAR" en Tauro, "IMPORTE" en Zaragoza.</summary>
    public string EtiquetaImporte { get; set; } = string.Empty;

    /// <summary>Del renglon 0 de sp_n_ConsultaVentas (VentasPago.cs 1184-1185).</summary>
    public decimal SaldoFavorDineroCliente { get; set; }
    public decimal SaldoFavorCascosCliente { get; set; }

    public List<VentasMultiPagoFormaDePago> FormasDePago { get; set; } = new();
    public List<VentasMultiPagoCatalogoItem> Bancos { get; set; } = new();

    /// <summary>Tauro: la lista de agente / repartidor de la liquidacion.</summary>
    public List<VentasMultiPagoCatalogoItem> Repartidores { get; set; } = new();

    /// <summary>Tauro pide liquidacion (agente o camioneta) antes de guardar.</summary>
    public bool PideLiquidacion { get; set; }

    /// <summary>Constantes.PrecioCascoKilo. El total de kilos se calcula con este.</summary>
    public decimal PrecioCascoKilo { get; set; }

    /// <summary>
    /// Constantes.DiferenciaPagoVsTransCheq: cuanto se tolera que el abono pase
    /// del monto del documento. Tauro 1.00, Zaragoza 0.00 — comprobado.
    /// </summary>
    public decimal ToleranciaDocumento { get; set; }

    /// <summary>Si viene lleno, la pantalla NO puede guardar nada y esto lo explica.</summary>
    public string Impedimento { get; set; } = string.Empty;
}

public class VentasMultiPagoGuardarResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Cuantas remisiones alcanzaron a grabarse (ver el servicio).</summary>
    public int Aplicadas { get; set; }

    public List<string> FoliosAplicados { get; set; } = new();

    /// <summary>Saldos a favor del cliente despues del movimiento.</summary>
    public decimal SaldoFavorDineroCliente { get; set; }
    public decimal SaldoFavorCascosCliente { get; set; }

    /// <summary>
    /// El front debe volver a preguntar confirmando esto antes de reintentar.
    /// "pago_mayor" | "cargo" | "saldo_favor" | "autorizacion".
    /// </summary>
    public string RequiereConfirmacion { get; set; } = string.Empty;
}
