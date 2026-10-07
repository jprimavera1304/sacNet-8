namespace ISL_Service.Application.DTOs.VentasFacturacion;

/*
  FACTURACION ELECTRONICA

  Son cinco capacidades: timbrar, timbrar un complemento de pago, cancelar,
  consultar y enviar por correo. Las tres primeras tocan un servicio EXTERNO
  (Facturapi) y una de ellas manda un correo de verdad, asi que no se pueden
  comparar contra legacy dentro del ciclo de instantanea: timbrar dos veces la
  misma remision —una por cada lado— emitiria DOS CFDI reales ante el SAT, y eso
  no se deshace volviendo la base.

  Lo que si se puede probar y esta probado sin base: el candado del key, la serie,
  el folio, los catalogos del SAT, el armado del `@Params` posicional y la cadena
  del QR (ver ReglasFacturacion y sus pruebas).

  UNA DIFERENCIA DE CONTRATO QUE CONVIENE LEER
  --------------------------------------------
  En legacy, el servicio arma el CFDI leyendo la remision de la base: dos mil
  lineas que componen los conceptos, el IVA, los usados y las dos variantes de
  empresa. Aqui los conceptos VIENEN EN EL REQUEST. No es pereza: es que esa
  composicion no se puede verificar sin emitir comprobantes fiscales de verdad, y
  entregar dos mil lineas sin probar seria peor que entregar un contrato explicito.
  Lo que SI hace este backend completo es todo lo demas: el candado, la serie, el
  folio, el timbrado por HTTP, el renglon de parametros y el insert en la base.
*/

/// <summary>El receptor del comprobante.</summary>
public class FacturaReceptor
{
    /// <summary>La razon social, como esta dada de alta en el SAT.</summary>
    public string? RazonSocial { get; set; }

    public string? Rfc { get; set; }

    /// <summary>La clave del regimen fiscal del receptor.</summary>
    public string? RegimenFiscal { get; set; }

    public string? Correo { get; set; }

    public string? CodigoPostal { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Ciudad { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Pais { get; set; }
}

/// <summary>Un concepto del comprobante.</summary>
public class FacturaConcepto
{
    /// <summary>La clave del producto o servicio del catalogo del SAT.</summary>
    public string? ClaveProdServ { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>La clave de unidad del catalogo del SAT.</summary>
    public string? ClaveUnidad { get; set; }

    public decimal Cantidad { get; set; }

    /// <summary>El precio unitario CON iva incluido, como lo maneja Mac31.</summary>
    public decimal PrecioUnitarioConIva { get; set; }

    public decimal Descuento { get; set; }

    /// <summary>El producto de Mac31 al que corresponde, para el reporte.</summary>
    public int IdProducto { get; set; }

    public string? Codigo { get; set; }
}

/// <summary>Timbra una factura o una nota de credito.</summary>
public class FacturaTimbrarRequest
{
    public int IdEmpresa { get; set; }

    /// <summary>El cliente de facturacion (los datos fiscales dados de alta).</summary>
    public int IdClienteFacturacion { get; set; }

    public int IdCliente { get; set; }

    /// <summary>El pedido, cuando se factura sin remision.</summary>
    public int IdPedido { get; set; }

    /// <summary>Las remisiones que se facturan. Vacio = factura sin remision.</summary>
    public List<int> IdsVenta { get; set; } = new();

    /// <summary>
    /// El folio de cada remision, en el mismo orden que IdsVenta. De aqui sale el
    /// folio fiscal y, si trae prefijo, parte de la serie.
    /// </summary>
    public List<string> FoliosVenta { get; set; } = new();

    /// <summary>Lo que se factura de cada remision, en el mismo orden.</summary>
    public List<decimal> TotalesFacturar { get; set; } = new();

    /// <summary>0 factura, 1 nota de credito, 2 nota de credito de cascos, 3 de garantia.</summary>
    public int Tipo { get; set; }

    public int IdTipoNotaCredito { get; set; }

    /// <summary>La factura a la que se relaciona una nota de credito.</summary>
    public int IdVentaFacturaRelacionada { get; set; }

    /// <summary>El id de Facturapi de la factura relacionada (para el UUID).</summary>
    public string? IdFacturapiRelacionada { get; set; }

    public string? RelacionFactura { get; set; }
    public string? MetodoPago { get; set; }
    public string? FormaPago { get; set; }
    public string? RegimenFiscal { get; set; }
    public string? UsoCfdi { get; set; }

    public FacturaReceptor Receptor { get; set; } = new();

    public List<FacturaConcepto> Conceptos { get; set; } = new();

    public string? Observaciones { get; set; }

    public decimal TotalFacturarDinero { get; set; }
    public decimal TotalFacturarCascos { get; set; }
    public decimal TotalFacturarDineroCascos { get; set; }
    public int TotalProductos { get; set; }
    public int TotalUsados { get; set; }
    public decimal Descuento { get; set; }

    /// <summary>1 para que el documento imprima los importes.</summary>
    public int IncluirImportes { get; set; }

    public int IdEmpresaCs { get; set; }
}

/// <summary>Un documento al que se le aplica el complemento de pago.</summary>
public class ComplementoPagoDocumento
{
    /// <summary>El id de Facturapi de la factura que se paga.</summary>
    public string? IdFacturapi { get; set; }

    /// <summary>La factura de Mac31 que se paga.</summary>
    public int IdVentaFactura { get; set; }

    /// <summary>Lo que se paga de ese documento.</summary>
    public decimal Importe { get; set; }
}

/// <summary>Timbra un complemento de pago.</summary>
public class ComplementoPagoTimbrarRequest
{
    public int IdEmpresa { get; set; }
    public int IdClienteFacturacion { get; set; }
    public int IdCliente { get; set; }

    public string? FormaPago { get; set; }

    /// <summary>La fecha y hora del pago. Obligatoria: va dentro del CFDI.</summary>
    public DateTime? FechaPago { get; set; }

    public FacturaReceptor Receptor { get; set; } = new();

    /// <summary>Los documentos que se pagan. Al menos uno.</summary>
    public List<ComplementoPagoDocumento> Documentos { get; set; } = new();

    public string? Observaciones { get; set; }

    public int IdEmpresaCs { get; set; }
}

/// <summary>Cancela un CFDI.</summary>
public class FacturaCancelarRequest
{
    /// <summary>El id del comprobante en Facturapi.</summary>
    public string? IdFacturapi { get; set; }

    /// <summary>La clave del motivo de cancelacion del SAT (01 a 04).</summary>
    public string? IdFeMotivoCancelacion { get; set; }

    /// <summary>El motivo en palabras, para la bitacora.</summary>
    public string? MotivoCancelacion { get; set; }

    /// <summary>0 factura, 1 nota de credito, 4 complemento de pago.</summary>
    public int Tipo { get; set; }

    /// <summary>
    /// El folio fiscal sustituto, cuando el motivo del SAT es 01 ("comprobante
    /// emitido con errores con relacion").
    /// </summary>
    public string? UuidSustituto { get; set; }
}

/// <summary>Filtros de la consulta de facturas.</summary>
public class FacturaConsultaRequest
{
    public int IdVentaFactura { get; set; }
    public List<int> IdsVentaFactura { get; set; } = new();
    public int IdVentaFacturaRelacionada { get; set; }
    public string? IdFacturapi { get; set; }
    public List<int> IdsVenta { get; set; } = new();
    public int IdCliente { get; set; }
    public int IdAgente { get; set; }
    public List<int> IdsUsuario { get; set; } = new();

    /// <summary>1 vigente, 2 cancelada.</summary>
    public int IdStatus { get; set; }

    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>0 factura, 1 nota de credito, 4 complemento de pago.</summary>
    public int Tipo { get; set; }

    public int Formato { get; set; }
    public int Identico { get; set; }
    public int IdEmpresaCs { get; set; }
    public List<int> IdsCliente { get; set; } = new();
}

/// <summary>Envia una factura ya timbrada por correo.</summary>
public class FacturaEnviarCorreoRequest
{
    public int IdVentaFactura { get; set; }

    /// <summary>Los destinatarios, separados por coma o punto y coma.</summary>
    public string? Correos { get; set; }
}

/// <summary>Lo que contesto el timbrado.</summary>
public class FacturaTimbradaDto
{
    /// <summary>El id del comprobante en Facturapi.</summary>
    public string? IdFacturapi { get; set; }

    public string? Uuid { get; set; }
    public string? Serie { get; set; }
    public int Folio { get; set; }
    public decimal Total { get; set; }
    public string? Status { get; set; }
    public string? CancellationStatus { get; set; }
    public string? UrlVerificacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaComprobante { get; set; }

    /// <summary>La cadena del QR de verificacion del SAT.</summary>
    public string? CadenaQr { get; set; }

    /// <summary>Las facturas de Mac31 que se crearon (una por remision).</summary>
    public List<int> IdsVentaFactura { get; set; } = new();

    /// <summary>El renglon de parametros donde quedaron los datos del CFDI.</summary>
    public int IdParametros { get; set; }

    /// <summary>Lo que dijo el ultimo insert: 1 bien, -1 no se pudo.</summary>
    public int? Resultado { get; set; }

    public string? Mensaje { get; set; }
}
