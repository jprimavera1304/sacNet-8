namespace ISL_Service.Application.DTOs.CascosCambio;

/*
  CASCOS A CAMBIO: LOS DATOS QUE VIAJAN

  Zaragoza le vende a Tauro y Tauro le regresa cascos de baterias para que se
  los descuenten. Cada empresa valua los MISMOS cascos con SU precio (verificado
  en las dos bases: 400 contra 380, 520 contra 495, ...), asi que cada
  movimiento tiene dos importes y una diferencia. Esa diferencia es todo el
  motivo por el que este modulo existe: es lo que falta o sobra al conciliar.

  Lo que NO trae ningun DTO de entrada, a proposito: precios ni importes. Se
  capturan la fecha, la remision, la persona y las PIEZAS. Lo demas lo calcula
  la base con el catalogo. Ahi es donde hoy se equivocan a mano.
*/

/* Un tipo de casco con sus dos precios. El front pinta las columnas con esto. */
public class TipoCascoCambioDto
{
    public int idTipoUsado { get; set; }
    /* 'G1'..'G7' en Zaragoza, 'MINI CHICO(1)'... en Tauro. */
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    /* Precio de la empresa que esta usando el sistema. */
    public decimal precio { get; set; }
    /* Precio de la otra empresa, copiado de su catalogo. */
    public decimal precioContraparte { get; set; }
    /* false = nunca se sincronizo ese tipo: su diferencia no se puede calcular. */
    public bool tienePrecioContraparte { get; set; }
    public string? empresaContraparte { get; set; }
    public DateTime? contraparteActualizada { get; set; }
}

public class MovimientoCascoCambioDto
{
    public int idMovimiento { get; set; }
    public DateTime fecha { get; set; }
    /* 1 ENTREGA, 2 PEDIDO, 3 PAGO, 4 SALDO INICIAL. */
    public int tipoMovimiento { get; set; }
    public string? tipoMovimientoNombre { get; set; }
    public string? remision { get; set; }
    public string? persona { get; set; }
    public string? observaciones { get; set; }
    public int totalPiezas { get; set; }
    public decimal importe { get; set; }
    public decimal importeContraparte { get; set; }
    /* Positiva = esta empresa cuenta mas de lo que le reconoce la otra. */
    public decimal diferencia { get; set; }
    public int signo { get; set; }
    public decimal importeConSigno { get; set; }
    public decimal importeContraparteConSigno { get; set; }
    public int estatus { get; set; }
    public string? motivoCancelacion { get; set; }
    public string? usuarioCreacion { get; set; }
    public DateTime? fechaCreacion { get; set; }

    /*
      QUIEN CANCELO Y CUANDO. Se guardaban desde siempre y no las leia nadie: el
      motivo solo dice POR QUE, y en una cuenta que dos empresas comparan
      renglon por renglon, quien lo hizo y que dia es la otra mitad.

      Nulas mientras el movimiento este vivo, que es lo normal.
    */
    public string? usuarioCancelacion { get; set; }
    public DateTime? fechaCancelacion { get; set; }

    /*
      LOS DOS SALDOS QUE CORREN

      No salen de la base: los acumula el servicio recorriendo los movimientos
      en orden. Guardarlos seria firmar un numero que se vuelve mentira en
      cuanto alguien cancele un movimiento viejo.
    */
    public decimal saldo { get; set; }
    public decimal saldoContraparte { get; set; }
}

public class DetalleCascoCambioDto
{
    public int idDetalle { get; set; }
    public int idMovimiento { get; set; }
    public int idTipoUsado { get; set; }
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    public int piezas { get; set; }
    public decimal precioUnitario { get; set; }
    public decimal precioContraparte { get; set; }
    public decimal importe { get; set; }
    public decimal importeContraparte { get; set; }
    public decimal diferencia { get; set; }
}

/*
  UN RENGLON DEL DESGLOSE, PERO DE TODO UN PERIODO

  Es lo que devuelve sp_w_ConsultarCascosCambioDetallePeriodo: la pieza minima
  que necesita el reporte "con detalle" —que movimiento, que tipo y cuantas
  piezas— y NADA MAS.

  No es DetalleCascoCambioDto recortado por gusto: aquel trae los dos precios y
  los tres importes de cada renglon porque la pantalla del "Ver" los enseña. El
  papel con detalle no: sus columnas por tipo son PIEZAS, y el dinero ya va en
  la columna de importe del movimiento. Traer seis decimales por renglon que
  nadie va a imprimir son 693 renglones de lastre en un mes cualquiera.
*/
public class DetallePeriodoCascoCambioDto
{
    public int idMovimiento { get; set; }
    public int idTipoUsado { get; set; }
    /* 'MINI CHICO(1)', 'CHICO(2)', ... Es el rotulo corto de la columna. */
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    public int piezas { get; set; }
}

public class ResumenTipoCascoCambioDto
{
    public int idTipoUsado { get; set; }
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    public decimal precioActual { get; set; }
    public int piezasEntrega { get; set; }
    public decimal importeEntrega { get; set; }
    public decimal importeEntregaContraparte { get; set; }
    public int piezasPedido { get; set; }
    public decimal importePedido { get; set; }
    public decimal importePedidoContraparte { get; set; }
}

/*
  EL CORTE DE LA CUENTA: es el pie de la hoja de Excel, calculado.

  'diferencia' es el numero que la hoja nunca tuvo: cuanto se lleva de mas o de
  menos la cuenta de esta empresa contra la de la otra.
*/
public class CorteCascosCambioDto
{
    /*
      Con que saldo llegaba la cuenta al primer dia del periodo consultado. Es
      lo que hace que los numeros del periodo se puedan leer sin mentir:
      saldoAnterior + lo de estos dias = saldo.
    */
    public decimal saldoAnterior { get; set; }
    public decimal saldoAnteriorContraparte { get; set; }

    public decimal entregas { get; set; }
    public decimal pedidos { get; set; }
    public decimal pagos { get; set; }
    public decimal saldoInicial { get; set; }
    public decimal saldo { get; set; }

    public decimal entregasContraparte { get; set; }
    public decimal pedidosContraparte { get; set; }
    public decimal saldoContraparte { get; set; }

    /* saldo - saldoContraparte. Positiva: a esta empresa le falta que le reconozcan. */
    public decimal diferencia { get; set; }
    public int piezasEntrega { get; set; }
    public int piezasPedido { get; set; }
    public int movimientos { get; set; }
}

public class MovimientosCascosCambioResponse
{
    public List<MovimientoCascoCambioDto> movimientos { get; set; } = new();
    public CorteCascosCambioDto corte { get; set; } = new();
}

/* --------------------------- Entradas --------------------------- */

public class PiezasPorTipoRequest
{
    public int IdTipoUsado { get; set; }
    public int Piezas { get; set; }
}

public class CrearMovimientoCascoCambioRequest
{
    public DateTime? Fecha { get; set; }
    /* 1 ENTREGA, 2 PEDIDO, 3 PAGO, 4 SALDO INICIAL. */
    public int TipoMovimiento { get; set; }
    public string? Remision { get; set; }
    public string? Persona { get; set; }
    public string? Observaciones { get; set; }
    /* Solo para PAGO y SALDO INICIAL. En los de cascos se ignora: ahi el
       importe SIEMPRE sale de piezas por precio. */
    public decimal Importe { get; set; }
    public List<PiezasPorTipoRequest>? Piezas { get; set; }
}

public class CancelarMovimientoCascoCambioRequest
{
    public string? Motivo { get; set; }
}

public class MovimientoCascoCambioCreadoDto
{
    public int IdMovimiento { get; set; }
    /* Texto vacio cuando no hay nada que advertir. */
    public string Advertencia { get; set; } = string.Empty;
}

public class SincronizacionPreciosDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Tipos { get; set; }
}
