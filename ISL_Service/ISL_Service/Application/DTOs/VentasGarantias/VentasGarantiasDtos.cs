namespace ISL_Service.Application.DTOs.VentasGarantias;

/*
  GARANTIAS: LAS TRES PANTALLAS Y LA CADENA QUE ARMABA EL CLIENTE

  Hay tres flujos: la GARANTIA de un producto vendido, su ACEPTACION en el centro
  de servicio, y el REGISTRO previo (cuando el vale se apunta antes de que haya
  una remision a la que colgarlo).

  LA PIEZA RARA: @GarantiasInfo
  -----------------------------
  Es un parametro varchar(4000) que los procedimientos de actualizar y cancelar
  escriben en la bitacora. No es un dato estructurado: es TEXTO PARA LEER, y lo
  armaba el cliente de WinForms recorriendo la rejilla
  (Mac31, ConsultarGarantias.cs, GarantiaActualizar), con este formato exacto:

      CLAVE: <codigo>  -  VALE: <ticket>~CLAVE: <codigo>  -  VALE: <ticket>

  dos espacios antes y despues del guion, y los elementos pegados con
  virgulilla. Que lo armara el cliente tiene dos consecuencias que se ven en la
  base: cada cliente podia escribirlo distinto (el movil nunca lo mando), y la
  bitacora de produccion tiene renglones con formatos que no coinciden.

  Aqui el servidor lo arma, a partir de las partidas que recibe. El formato es el
  mismo, byte por byte, para que la bitacora siga siendo legible junto con la
  vieja.

  `IdUsuario` y `Equipo` no viajan en ningun request: salen del token.
*/

/// <summary>
/// Una partida de garantia, para armar el texto de bitacora. `Codigo` es la
/// clave del producto y `Ticket` el vale con el que entro.
/// </summary>
public class GarantiaPartida
{
    public string? Codigo { get; set; }
    public string? Ticket { get; set; }
}

/// <summary>Registra una garantia sobre un producto.</summary>
public class GarantiaCrearRequest
{
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public int IdAlmacen { get; set; }
    public int IdProducto { get; set; }

    /// <summary>Cuando entro el producto. Obligatoria.</summary>
    public DateTime? FechaIngreso { get; set; }

    /// <summary>El vale o ticket con el que el cliente la reclama.</summary>
    public string? Ticket { get; set; }

    /// <summary>La vigencia que trae impresa el producto.</summary>
    public string? Vigencia { get; set; }

    /// <summary>El numero o folio de garantia del fabricante.</summary>
    public string? Garantia { get; set; }

    public string? Motivo { get; set; }

    /// <summary>1 cuando la garantia es de un mayorista.</summary>
    public int GarantiaMayorista { get; set; }

    public int IdEmpresaMayorista { get; set; }

    /// <summary>El producto que se entrega a cambio, si ya se decidio.</summary>
    public int IdProductoCambio { get; set; }
}

/// <summary>
/// Modifica una o varias garantias a la vez. Es tambien la via por la que Mac31
/// las cancela de verdad (mandando el estatus de cancelada), porque el endpoint
/// de cancelar de legacy no lo usa ninguna pantalla.
/// </summary>
public class GarantiaActualizarRequest
{
    /// <summary>Las garantias que se modifican.</summary>
    public List<int> IdsGarantia { get; set; } = new();

    /// <summary>Las remisiones a las que se ligan, si aplica.</summary>
    public List<int> IdsVenta { get; set; } = new();

    /// <summary>
    /// Las partidas con las que se arma el texto de bitacora. Puede tener MAS
    /// elementos que IdsGarantia: cuando una garantia ya remisionada no se puede
    /// cancelar, Mac31 la deja fuera de la lista de ids pero la apunta igual en
    /// el texto, para que quede constancia de que se intento.
    /// </summary>
    public List<GarantiaPartida> Partidas { get; set; } = new();

    /// <summary>El estatus al que pasan. Es lo que de verdad decide la operacion.</summary>
    public int IdStatusGarantia { get; set; }

    public int IdCliente { get; set; }
    public int IdProducto { get; set; }
    public int IdRepartidor { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string? Ticket { get; set; }
    public string? Vigencia { get; set; }
    public string? Garantia { get; set; }
    public string? Motivo { get; set; }

    /// <summary>El porcentaje de bonificacion que se reconoce.</summary>
    public decimal Porcentaje { get; set; }

    /// <summary>El monto de bonificacion, cuando no es porcentaje.</summary>
    public decimal Monto { get; set; }

    public string? Diagnostico { get; set; }

    /// <summary>El folio con el que se envio al proveedor.</summary>
    public string? FolioEnviadaProveedor { get; set; }

    public int IdProductoCambio { get; set; }

    /// <summary>El producto que se carga al cliente, cuando la garantia se niega.</summary>
    public int IdProductoCarga { get; set; }

    public int IdPedidoPagos { get; set; }
}

/// <summary>Cancela una o varias garantias.</summary>
public class GarantiaCancelarRequest
{
    public List<int> IdsGarantia { get; set; } = new();

    /// <summary>Las partidas con las que se arma el texto de bitacora.</summary>
    public List<GarantiaPartida> Partidas { get; set; } = new();
}

/// <summary>Acepta una garantia en un centro de servicio.</summary>
public class GarantiaAceptarRequest
{
    public int IdGarantia { get; set; }

    /// <summary>
    /// El centro que la acepta. El procedimiento lo usa para armar, con SQL
    /// dinamico, el UPDATE contra la tabla Garantias de la base de ESE centro:
    /// el nombre de la base lo resuelve el procedimiento, no este codigo.
    /// Obligatorio por eso mismo — con 0 el procedimiento no sabe a que base ir.
    /// </summary>
    public int IdCentroServicio { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Filtros de la pantalla de garantias.</summary>
public class GarantiaConsultaRequest
{
    public int IdGarantia { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public int IdProducto { get; set; }
    public int IdStatusGarantia { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
    public DateTime? FechaIngresoInicial { get; set; }
    public DateTime? FechaIngresoFinal { get; set; }
    public string? Ticket { get; set; }
    public string? Vigencia { get; set; }
    public string? Garantia { get; set; }
    public string? Diagnostico { get; set; }
    public string? Folio { get; set; }

    /// <summary>
    /// 1 = las pendientes de aceptar, 2 = las ya aceptadas. Solo lo usa la
    /// pantalla de aceptacion; en la de garantias el procedimiento NI SIQUIERA
    /// lo recibe (legacy tiene esa linea comentada), aunque si lo usa para
    /// decidir que filtros manda.
    /// </summary>
    public int IdStatusGarantiaAceptada { get; set; }
}

/// <summary>Da de alta un registro previo de garantia.</summary>
public class GarantiaRegistroCrearRequest
{
    public int IdEmpresa { get; set; }
    public int IdProducto { get; set; }
    public string? Ticket { get; set; }
    public string? Vigencia { get; set; }
    public string? Garantia { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Cancela uno o varios registros previos de garantia.</summary>
public class GarantiaRegistroCancelarRequest
{
    /// <summary>
    /// Los ids del REGISTRO (IDGarantiaRegistro), no de la garantia. El
    /// procedimiento llama al parametro @IDsGarantia, que es confuso y es su
    /// nombre.
    /// </summary>
    public List<int> IdsRegistro { get; set; } = new();

    public List<GarantiaPartida> Partidas { get; set; } = new();
}

/// <summary>Filtros de la pantalla de registros previos de garantia.</summary>
public class GarantiaRegistroConsultaRequest
{
    public int IdGarantiaRegistro { get; set; }
    public int IdProducto { get; set; }

    /// <summary>1 activo, 2 cancelado.</summary>
    public int IdStatus { get; set; }

    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
    public string? Ticket { get; set; }
    public string? Vigencia { get; set; }
    public string? Garantia { get; set; }
    public string? Folio { get; set; }
}
