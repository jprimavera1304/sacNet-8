namespace ISL_Service.Application.DTOs.VentasPagosOperaciones;

/*
  LO QUE QUEDABA DE PAGOS DE VENTA, Y LAS OPERACIONES SUELTAS DE LA REMISION

  En legacy todo esto vive en UN controlador —VentasController— con la clase
  entera marcada [AllowAnonymous] y un solo DTO de cincuenta campos para los
  nueve endpoints de pagos. El [AllowAnonymous] no significa que fuera abierto
  (el handler global sigue pidiendo la cabecera Authorization y el servicio
  valida el token del cuerpo), pero si significa que la autorizacion no estaba
  declarada en ningun sitio: habia que leer el servicio para saber que pedia.

  Aqui cada operacion tiene su request con sus campos, y el permiso esta en el
  controlador, a la vista.

  LAS DOS BANDERAS QUE ELEGIAN PROCEDIMIENTO
  ------------------------------------------
  `NuevoEsquemaPagos` venia en el CUERPO y decidia entre
  sp_n_InsertarVentasPagos / sp_n_InsertarVentasNuevosPagos al capturar, y entre
  sp_n_CancelarVentasPagos / sp_n_CancelarVentasNuevosPagos al cancelar. Una
  bandera de cuerpo que decide contra que tabla se escribe es una bandera que se
  manda mal una vez y nadie entiende por que el pago aparecio en el otro lado.
  Aqui son rutas distintas.
*/

/// <summary>Declara una remision como incobrable.</summary>
public class VentaIncobrableRequest
{
    public int IdVenta { get; set; }
}

/// <summary>Un abono en kilos de casco contra una remision.</summary>
public class VentaPagoKilosRequest
{
    public int IdCobro { get; set; }
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }

    /// <summary>Los kilos que se abonan.</summary>
    public decimal AbonoKilos { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Cancela un abono en kilos.</summary>
public class VentaPagoKilosCancelarRequest
{
    public int IdPagoVenta { get; set; }
    public int IdPagoVentaUsado { get; set; }
    public int IdVenta { get; set; }
}

/// <summary>
/// Un pago del esquema nuevo: no cuelga de una remision, cuelga del cliente.
/// Despues se APLICA a las remisiones que corresponda.
/// </summary>
public class NuevoPagoCrearRequest
{
    public int IdCliente { get; set; }
    public int IdTipoPago { get; set; }

    /// <summary>Lo que se le carga al cliente.</summary>
    public decimal Cargo { get; set; }

    /// <summary>Lo que el cliente abona.</summary>
    public decimal Abono { get; set; }

    /// <summary>El total del movimiento, para cuadrar contra cargo y abono.</summary>
    public decimal MontoTotal { get; set; }

    public int IdBancoTarjeta { get; set; }
    public string? NumeroTarjeta { get; set; }

    /// <summary>Credito o debito, como lo tipifica legacy.</summary>
    public int TipoTarjeta { get; set; }

    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Aplica o cancela un pago del esquema nuevo.</summary>
public class NuevoPagoRequest
{
    public int IdNuevoPagoVenta { get; set; }
}

/// <summary>Cancela una transferencia bancaria completa.</summary>
public class TransferenciaCancelarRequest
{
    public int IdBancoTransferenciaMovimiento { get; set; }
}

/// <summary>
/// Cancela UN pago hecho con transferencia, dejando viva la transferencia.
/// </summary>
public class TransferenciaPagoCancelarRequest
{
    public int IdPagoVenta { get; set; }
    public int IdPagoVentaUsado { get; set; }
    public int IdEmpresa { get; set; }
}

/// <summary>Reasigna remisiones a otro cliente o a otro agente.</summary>
public class VentasReasignarRequest
{
    public int IdCliente { get; set; }
    public int IdAgente { get; set; }

    /// <summary>
    /// 1 para cambiar el cliente; 0 para cambiar solo el agente. Es lo que
    /// decide cual de los dos ids se usa, asi que no es cosmetico.
    /// </summary>
    public int CambiarCliente { get; set; }

    public List<int> IdsVenta { get; set; } = new();

    /// <summary>Por que se reasigna. Obligatorio.</summary>
    public string? Motivo { get; set; }
}

/// <summary>Cambia la nota de una remision. Es lo unico que deja cambiar.</summary>
public class VentaActualizarRequest
{
    public int IdVenta { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Marca o desmarca remisiones como "se pagan por kilo".</summary>
public class VentasPagoPorKiloRequest
{
    public List<int> IdsVenta { get; set; } = new();

    /// <summary>1 se paga por kilo, 0 no.</summary>
    public int PagoPorKilo { get; set; }
}

/// <summary>
/// Las partidas de una o varias remisiones.
///
/// Con una sola remision se usa sp_n_VentasDetalle (que acepta ademas un
/// descuento a simular); con varias, sp_n_VentasDetalleMultiple. No es una
/// bandera: es la forma del pedido lo que decide, igual que en legacy.
/// </summary>
public class VentasDetalleConsultaRequest
{
    public List<int> IdsVenta { get; set; } = new();
    public int IdVenta { get; set; }

    /// <summary>
    /// El descuento con el que se quiere ver el detalle. Solo aplica a una
    /// remision: el procedimiento multiple no lo recibe.
    /// </summary>
    public int IdDescuento { get; set; }
}
