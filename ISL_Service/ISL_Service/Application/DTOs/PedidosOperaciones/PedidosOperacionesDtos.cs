namespace ISL_Service.Application.DTOs.PedidosOperaciones;

/*
  PROCESAR, CANCELAR Y AJUSTAR PEDIDOS

  Estas son las operaciones que convierten un pedido en una remision (o en una
  compra) y las que lo cancelan. Son las que mueven inventario y folios, y las
  dos de lote son las unicas de toda la ola donde legacy ejecuta el procedimiento
  N VECES sin transaccion global.
*/

/// <summary>
/// Procesa pedidos de venta. Cada pedido se procesa por separado.
/// </summary>
public class PedidosProcesarVentaRequest
{
    /// <summary>Los pedidos a procesar.</summary>
    public List<int> IdsPedido { get; set; } = new();

    /// <summary>
    /// true si, al procesar, quien lo hace tambien AUTORIZA el pedido. El
    /// usuario y el equipo que se guardan como autorizadores salen del token, no
    /// del cuerpo: en legacy viajaban en el cuerpo (IDUsuarioAutorizar,
    /// EquipoAutorizar) y eso permitia firmar una autorizacion a nombre de otro.
    /// </summary>
    public bool Autorizar { get; set; }

    /// <summary>true si, al procesar, quien lo hace RECHAZA el pedido.</summary>
    public bool Rechazar { get; set; }

    /* El cobro de contado del centro de servicio, cuando el pedido se cobra en
       el mismo acto de procesarlo. Todo en cero significa "no se cobra aqui". */

    public int IdTipoPagoCs { get; set; }
    public int IdBancoTransferCs { get; set; }
    public string? TransferenciaCs { get; set; }
    public int IdBancoTarjetaCs { get; set; }
    public string? TarjetaCs { get; set; }
    public int IdBancoDepositoEfeCs { get; set; }
    public string? DepositoEfectivoNumeroCs { get; set; }
    public decimal MontoTotalCs { get; set; }

    /// <summary>Credito o debito, como lo tipifica legacy.</summary>
    public int TipoTarjeta { get; set; }
}

/// <summary>Procesa un pedido de compra y lo convierte en una compra.</summary>
public class PedidoProcesarCompraRequest
{
    public int IdPedido { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>La factura del proveedor.</summary>
    public string? Factura { get; set; }

    public DateTime? FechaEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }
}

/// <summary>Cancela uno o varios pedidos.</summary>
public class PedidosCancelarRequest
{
    public List<int> IdsPedido { get; set; } = new();
}

/// <summary>
/// Ajusta cuantos cascos de un tipo lleva un pedido a credito.
/// </summary>
public class PedidoUsadoCreditoRequest
{
    public int IdPedido { get; set; }
    public int IdTipoUsado { get; set; }

    /// <summary>La cantidad nueva. 0 quita el renglon.</summary>
    public int Cantidad { get; set; }
}

/// <summary>
/// El resultado de UN pedido dentro de una operacion de lote.
///
/// Existe porque legacy devuelve SOLO los que fallaron: si todo salio bien, la
/// lista viene vacia y la pantalla no tiene forma de saber que folio le toco a
/// cada pedido. Aqui vienen todos, con su resultado.
/// </summary>
public class PedidoResultadoLote
{
    public int IdPedido { get; set; }

    /// <summary>1 se hizo, -1 no se pudo.</summary>
    public int Resultado { get; set; }

    /// <summary>El mensaje literal del procedimiento.</summary>
    public string? Mensaje { get; set; }

    /// <summary>El folio del pedido, cuando el procedimiento lo devuelve.</summary>
    public int Pedido { get; set; }

    /// <summary>La remision que se creo, cuando se procesa una venta.</summary>
    public int IdVenta { get; set; }
}

/// <summary>El resultado completo de una operacion de lote sobre pedidos.</summary>
public class PedidosLoteResponse
{
    /// <summary>Un renglon por pedido, en el orden en que se procesaron.</summary>
    public List<PedidoResultadoLote> Resultados { get; set; } = new();

    /// <summary>Cuantos se hicieron.</summary>
    public int Hechos => Resultados.Count(r => r.Resultado > 0);

    /// <summary>Cuantos fallaron.</summary>
    public int Fallidos => Resultados.Count(r => r.Resultado <= 0);

    /// <summary>
    /// Las remisiones que se crearon. Legacy devolvia esto como una cadena
    /// pegada con virgulilla; aqui es una lista.
    /// </summary>
    public List<int> IdsVenta { get; set; } = new();
}
