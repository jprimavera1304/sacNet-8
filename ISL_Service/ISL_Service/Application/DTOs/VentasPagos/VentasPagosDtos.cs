namespace ISL_Service.Application.DTOs.VentasPagos;

/*
  CONSULTAR PAGOS DE UNA REMISION

  Es la pantalla ConsultarVentasPagos de Mac31
  (Legacy/Mac31/Mac31/Forms/Ventas/ConsultarVentasPagos.cs). Se abre desde
  Consulta de ventas con una remision elegida (ConsultarVentas.cs,
  btnPagos_Click, linea 3652) y ensena tres cosas: quien es la remision, el
  movimiento de su cuenta y los totales. Ademas deja hacer un pago nuevo y
  cancelar uno.

  Todos los numeros salen de UN solo procedimiento, sp_n_ConsultaVentasPagos,
  el mismo que llama Mac31. Aqui no se suma nada a mano: la cabecera y los
  totales son columnas del PRIMER renglon que devuelve ese procedimiento
  (ConsultarVentasPagos.cs, lineas 78-96), y asi es como se garantiza que el
  web y Mac31 digan el mismo saldo.
*/

/// <summary>
/// La cabecera y los totales de la pantalla. Todo viene del renglon 0 del
/// procedimiento, igual que en ConsultarVentasPagos.cs lineas 78-96.
/// </summary>
public class VentasPagosCabecera
{
    public int IdVenta { get; set; }

    public string FolioFtm { get; set; } = string.Empty;

    /// <summary>"207 - RAMIREZ ORTIZ ANGEL". Mac31 lo arma pegando numero y
    /// nombre (ConsultarVentasPagos.cs, linea 82).</summary>
    public string Cliente { get; set; } = string.Empty;

    public string Empresa { get; set; } = string.Empty;

    public string VencimientoFtm { get; set; } = string.Empty;

    public string Agente { get; set; } = string.Empty;

    /*
      LA REMISION CANCELADA.

      Mac31 no pinta una etiqueta: esconde los DOS botones cuando la venta trae
      fecha de cancelacion (ConsultarVentasPagos.cs, lineas 137-142). Se manda
      la fecha formateada tal cual para poder ensenar cuando se cancelo, que es
      lo que la pantalla de Mac31 no dice y siempre acaban preguntando.
    */
    public bool Cancelada { get; set; }
    public string CancelacionFtm { get; set; } = string.Empty;

    /* Los cinco totales de abajo, en el orden en que los pinta Mac31. */

    /// <summary>txtImporte. Es [Total a Pagar], NO ImporteConIvaRnd
    /// (ConsultarVentasPagos.cs, linea 87: columna TotalPagar).</summary>
    public decimal Importe { get; set; }

    public decimal Cargos { get; set; }

    public decimal Descuentos { get; set; }

    public decimal Abonos { get; set; }

    /*
      EL SALDO SE CALCULA DISTINTO EN CADA EMPRESA.

      Lo decide el procedimiento, no nosotros (sp_n_ConsultaVentasPagos,
      lineas 335-339), segun Constantes.EsquemaPago:

        TAU  (Tauro)     Saldo = [Total a Pagar]  + Cargos - Descuentos - Abonos
        ZARA (Zaragoza)  Saldo = ImporteConIvaRnd + Cargos - Descuentos - Abonos

      Se ven iguales y no lo son: [Total a Pagar] puede traer redondeos y
      cargos ya incorporados que ImporteConIvaRnd no tiene. Por eso este campo
      se toma TAL CUAL de la columna Saldo y no se vuelve a calcular aqui: si
      lo calculara el backend, Zaragoza empezaria a ver un saldo distinto al de
      su Mac31 en cuanto los dos numeros se separaran.
    */
    public decimal Saldo { get; set; }

    /// <summary>"TAU" o "ZARA". Va al front solo para poder explicarlo; ningun
    /// numero se recalcula con esto.</summary>
    public string EsquemaPago { get; set; } = string.Empty;

    /*
      LOS DOS BOTONES.

      Mac31 los ESCONDE; aqui se manda el porque y el front los deja pulsables
      con un aviso. Son las reglas de ConsultarVentasPagos.cs:

        saldo == 0                 -> se esconde Nuevo pago (lineas 134-135)
        la venta esta cancelada    -> se esconden los dos    (lineas 137-142)
    */
    public bool PuedeNuevoPago { get; set; }
    public string MotivoNuevoPago { get; set; } = string.Empty;

    public bool PuedeCancelarPago { get; set; }
    public string MotivoCancelarPago { get; set; } = string.Empty;

    /*
      LAS FORMAS DE PAGO DEL CLIENTE (efectivo / cheque / transferencia /
      deposito en efectivo).

      En Mac31 son cuatro casillas arriba de la tabla
      (chkPagaConEfectivo_CheckedChanged, linea 549). Se dejan aqui, pero hay
      que saber una cosa: HOY SIEMPRE VIENEN EN CERO en las dos empresas.
      sp_n_ConsultaVentasPagos no devuelve las columnas PagaConEfectivo,
      PagaConCheque, PagaConTransferencia ni PagaConDepositoEfectivo —se
      comprobo que no existen en NINGUNA tabla de Produccion_svr ni de MacZ—
      asi que el DTO de legacy las recibe en cero y Mac31 pinta las cuatro
      casillas grises y vacias, siempre. Es una funcion muerta, no un dato.

      Se conservan para que el dia que alguien las llene la pantalla ya sepa
      leerlas, pero el front NO dibuja cuatro casillas apagadas que nunca se
      encienden: eso es ruido que se lee como "este cliente no paga con nada".
    */
    public bool PagaConEfectivo { get; set; }
    public bool PagaConCheque { get; set; }
    public bool PagaConTransferencia { get; set; }
    public bool PagaConDepositoEfectivo { get; set; }
}

/// <summary>
/// Un renglon del movimiento. Las columnas y su orden son los de
/// ConsultarVentasPagos.cs, metodo GridVentasPagos (lineas 159-302).
/// </summary>
public class VentasPagoRenglon
{
    public int IdPagoVenta { get; set; }

    /// <summary>FECHA</summary>
    public string FechaFtm { get; set; } = string.Empty;

    /// <summary>USR</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>EQUIPO</summary>
    public string Equipo { get; set; } = string.Empty;

    /// <summary>NUM. Viene vacio cuando [Numero de Pago] es 0 — el renglon de
    /// la VENTA, que no es un pago.</summary>
    public string NumeroPago { get; set; } = string.Empty;

    /// <summary>El movimiento ("VENTA", "VIGENTE", "PAGO EXCEDENTE"...). Mac31
    /// tiene esta columna escondida (lineas 224-229), pero se manda porque es
    /// lo que se lee al confirmar la cancelacion de un cargo (linea 464).</summary>
    public string Movimiento { get; set; } = string.Empty;

    /// <summary>La columna sin titulo entre NUM. y CARGO: TipoPagoCorto
    /// (linea 232, el HeaderText se pone en blanco a proposito).</summary>
    public string FormaCorta { get; set; } = string.Empty;

    public decimal Cargo { get; set; }
    public decimal Abono { get; set; }

    /// <summary>SALDO: el acumulado renglon a renglon, no el saldo final.</summary>
    public decimal SaldoAcumulado { get; set; }

    /// <summary>DIFF USADO: "SI" o "NO".</summary>
    public bool DiffUsados { get; set; }

    /// <summary>LIQ. AGENTE</summary>
    public string AgenteLiquidacion { get; set; } = string.Empty;

    /// <summary>LIQ. REPARTIDOR</summary>
    public string RepartidorLiquidacion { get; set; } = string.Empty;

    /// <summary>DETALLE PAGO: el texto largo que arma el procedimiento con el
    /// banco, el numero de cheque o de transferencia y el importe.</summary>
    public string DetallePago { get; set; } = string.Empty;

    public bool Cancelado { get; set; }

    /// <summary>"23-02-2026 10:32 - JUAN - DESKTOP-0RTIOR5", o "----" si no
    /// esta cancelado.</summary>
    public string CancelacionFtm { get; set; } = string.Empty;

    /*
      POR QUE EL MOTIVO VIAJA POR RENGLON

      Cancelar un pago no depende solo del permiso: depende de ESE renglon
      (ConsultarVentasPagos.cs, btnCancelar_Click, lineas 422-474). Mandarlo
      calculado evita que el front tenga que repetir las reglas y evita el peor
      final posible: pulsar Cancelar y que el servidor conteste que no.
    */
    public bool PuedeCancelar { get; set; }
    public string MotivoNoCancelar { get; set; } = string.Empty;

    /// <summary>El texto de la confirmacion, ya armado con el importe y en el
    /// idioma de Mac31 (lineas 449-473). Vacio si no se puede cancelar.</summary>
    public string TextoConfirmacion { get; set; } = string.Empty;
}

public class VentasPagosRespuesta
{
    public VentasPagosCabecera Cabecera { get; set; } = new();
    public List<VentasPagoRenglon> Renglones { get; set; } = new();
}

public class VentasPagoCancelarRequest
{
    public int IdVenta { get; set; }
    public int IdPagoVenta { get; set; }
}

public class VentasPagoCancelarRespuesta
{
    public bool Cancelado { get; set; }

    /// <summary>El mensaje del procedimiento cuando se niega, tal cual.</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>La pantalla recargada, para no pedirla dos veces.</summary>
    public VentasPagosRespuesta? Pantalla { get; set; }
}
