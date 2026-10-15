namespace ISL_Service.Application.DTOs.VentasFacturacion;

/*
  ══════════════════════════════════════════════════════════════════════════
  *PREPARAR* UN COMPROBANTE: EL CONTRATO
  ══════════════════════════════════════════════════════════════════════════

  QUE PROBLEMA RESUELVE
  ---------------------
  `POST /api/ventas/facturas` es tonto a proposito: recibe el CFDI YA ARMADO.
  El encabezado de `VentasFacturacionDtos.cs` lo explica y la decision era
  correcta en su momento —no se podia verificar la composicion sin emitir
  comprobantes de verdad—, pero tiene una consecuencia: HOY EL QUE DECIDE QUE
  DICE EL COMPROBANTE ES EL CLIENTE. El navegador, concretamente
  `nucleo/facturarRemision.ts` y `FacturaDeRemisionPage.tsx`.

  Con un solo cliente eso se aguanta. Con dos —web y movil— son dos copias de
  una regla fiscal, y dos copias son dos comprobantes distintos para la misma
  remision. Peor: la copia que se equivoca es la segunda, y nadie lo descubre
  hasta comparar dos PDF del SAT.

  Asi que `preparar` devuelve el comprobante ARMADO Y VALIDADO y los dos
  clientes solo lo ensenan, lo retocan donde se puede retocar, y lo confirman.

  ES ADITIVO. NO SE TOCA EL TIMBRADO
  ----------------------------------
  `POST /api/ventas/facturas` sigue EXACTAMENTE igual: el web lo llama tal cual
  y no se le cambia ni un campo. `preparar` no escribe nada, no habla con
  Facturapi y no es obligatorio. Un cliente viejo sigue funcionando sin
  enterarse de que existe.

  LA EQUIVALENCIA ES EL CONTRATO
  ------------------------------
  `Timbrado` de la respuesta es, campo por campo, el cuerpo que el web arma hoy
  para el mismo caso. Eso esta probado en
  `ISL_Service.Tests/PrepararFacturaEquivaleAlWebTests.cs` contra casos tomados
  del propio web. Si alguien cambia una regla de `ReglasPreparacionFactura` y la
  equivalencia se rompe, esa prueba lo dice antes de que un CFDI lo diga.
*/

// ==========================================================================
// FACTURA DE REMISION
// ==========================================================================

/// <summary>
/// Lo MINIMO que hace falta para preparar una factura. Todo lo demas se lee del
/// servidor.
/// </summary>
/// <remarks>
/// La primera llamada no manda nada mas que los ids: `preparar` contesta con las
/// claves del SAT por omision, los montos que propone Mac31 y todas las partidas
/// marcadas, que es como abre la pantalla de Mac31. Las llamadas siguientes
/// —cuando la persona retoca— vuelven a mandar lo retocado, y asi las
/// validaciones y los totales SIEMPRE los calcula el servidor con los datos
/// de ahora.
/// </remarks>
public class FacturaPrepararRequest
{
    /// <summary>Las remisiones a facturar. Vacio + IdPedido = factura sin remision.</summary>
    public List<int> IdsVenta { get; set; } = new();

    /// <summary>El pedido, cuando se factura SIN remision.</summary>
    public int IdPedido { get; set; }

    /// <summary>
    /// El domicilio fiscal elegido. 0 = el primero, que es como abre Mac31 (su
    /// reja arranca en la fila 0, VentasFacturacion.cs:201).
    /// </summary>
    public int IdClienteFacturacion { get; set; }

    /// <summary>
    /// Vacio = las del domicilio fiscal. Mac31 sobreescribe los TRES combos con
    /// los del renglon de RFC al elegirlo (VentasFacturacion.cs:3539-3549), asi
    /// que lo que el domicilio trae es el valor por omision y no una sugerencia.
    /// </summary>
    public string? FormaPago { get; set; }

    public string? MetodoPago { get; set; }
    public string? UsoCfdi { get; set; }

    /// <summary>
    /// Lo que se factura de cada remision. Vacio = lo que propone Mac31
    /// (`MontoQueProponeMac31`). La casilla "modificar monto" de Mac31 es
    /// invisible y siempre esta marcada, asi que esto SIEMPRE se puede mandar.
    /// </summary>
    public List<FacturaPrepararMonto> Montos { get; set; } = new();

    /// <summary>
    /// Las partidas y su cantidad. Vacio = todas marcadas con su cantidad
    /// completa, que es como abre Mac31 (`chkSeleccionar.Checked = true`, :218).
    /// </summary>
    public List<FacturaPrepararPartida> Partidas { get; set; } = new();

    /// <summary>
    /// Los cascos que se AGREGAN a la factura. NO son los de la remision: salen
    /// del catalogo con las cantidades en cero y la persona teclea cuantos quiere
    /// facturar (VentasFacturacion.cs:325 y :338-342).
    /// </summary>
    public List<FacturaPrepararCasco> Cascos { get; set; } = new();

    /// <summary>Los correos marcados. Vacio = todos, como Mac31 (:652-653).</summary>
    public List<string> Correos { get; set; } = new();

    public string? Observaciones { get; set; }

    /// <summary>1 para que el documento imprima los importes.</summary>
    public int IncluirImportes { get; set; } = 1;

    public int IdEmpresaCs { get; set; }
}

public class FacturaPrepararMonto
{
    public int IdVenta { get; set; }
    public decimal Monto { get; set; }
}

public class FacturaPrepararPartida
{
    public int IdVentaDetalle { get; set; }

    /// <summary>false = la partida NO va al CFDI (la casilla SELECCIONAR de Mac31).</summary>
    public bool Marcada { get; set; } = true;

    public decimal Cantidad { get; set; }
}

public class FacturaPrepararCasco
{
    public int IdTipoUsado { get; set; }
    public int Cantidad { get; set; }
}

/// <summary>El comprobante armado y revisado, listo para que la persona confirme.</summary>
public class FacturaPreparadaDto
{
    /// <summary>
    /// El cuerpo EXACTO que hay que mandarle a `POST /api/ventas/facturas`.
    ///
    /// Es el mismo objeto que hoy arma el navegador. El cliente no lo recompone:
    /// lo reenvia. Si tuviera que recomponerlo volveriamos a tener dos reglas.
    /// </summary>
    public FacturaTimbrarRequest Timbrado { get; set; } = new();

    /// <summary>
    /// Lo que BLOQUEA el timbrado, con el campo al que llevar el foco. Null =
    /// se puede timbrar. Mientras haya bloqueo, el cliente NO debe ofrecer el
    /// boton: es un documento irreversible.
    /// </summary>
    public FacturaPreparadaReclamo? Bloqueo { get; set; }

    /// <summary>
    /// Lo que conviene LEER pero no impide timbrar: divergencias medidas con
    /// Mac31, ramas no verificadas, datos que el procedimiento no mando.
    /// </summary>
    public List<string> Avisos { get; set; } = new();

    /// <summary>La confirmacion literal de Mac31, con el total, el nombre y el RFC.</summary>
    public string TextoDeConfirmacion { get; set; } = string.Empty;

    // ---- lo que la pantalla ensena para revisar ----

    public FacturaPreparadaCliente Cliente { get; set; } = new();
    public List<FacturaPreparadaRemision> Remisiones { get; set; } = new();
    public List<FacturaPreparadaRfc> Rfcs { get; set; } = new();
    public List<FacturaPreparadaConcepto> Conceptos { get; set; } = new();
    public List<FacturaPreparadaCasco> CatalogoDeCascos { get; set; } = new();
    public FacturaPreparadaTotales Totales { get; set; } = new();

    /// <summary>Los tres catalogos del SAT que pide una FACTURA.</summary>
    /// <remarks>
    /// SON TRES, NO CINCO. Una factura pide forma de pago, metodo de pago y uso
    /// del CFDI. Los TIPOS DE NOTA y las RELACIONES de factura son de la nota de
    /// credito: una factura no acredita nada. Y pedir los cinco no es solo traer
    /// dos listas de mas — `tipos-nota-credito` esta tras `catalogos.ver`, que es
    /// otra familia, y un 403 en una lista que esta pantalla no usa dejaba los
    /// tres desplegables vacios (medido con DEMO, que factura en Mac31 y no tiene
    /// `catalogos.ver`; web: facturarRemision.ts:268-278).
    /// </remarks>
    public FacturaPreparadaCatalogos Catalogos { get; set; } = new();

    /// <summary>Los correos a los que se mandaria, ya marcados.</summary>
    public List<string> Correos { get; set; } = new();

    /// <summary>Todos los que el cliente tiene dados de alta.</summary>
    public List<string> CorreosDisponibles { get; set; } = new();

    /// <summary>
    /// `facturacionMuestraPreciosConIva`, derivada de
    /// `funcionalidad.Contains("ZARA")`. Decide de que columna se lee el precio y
    /// si corre la validacion de descuento. Se publica para que el cliente pueda
    /// titular la columna "PRECIO C/IVA" en vez de "PRECIO" — que es lo que ya
    /// hace Mac31 en facturacion (:1040/:1077) y lo que la nota de credito NO
    /// hace, dejando la misma columna con el mismo titulo significando dos cosas.
    /// </summary>
    public bool PreciosConIva { get; set; }
}

public class FacturaPreparadaReclamo
{
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>rfc | formaPago | metodoPago | usoCfdi | monto | cantidades | partidas | correos</summary>
    public string Campo { get; set; } = string.Empty;
}

public class FacturaPreparadaCliente
{
    public int IdCliente { get; set; }
    public string NumeroCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>
    /// La empresa con la que se piden los datos fiscales (:1157) y, en centro de
    /// servicio, con la que se factura (:1634). Fuera de CS es la misma.
    /// </summary>
    public int IdEmpresa { get; set; }
}

public class FacturaPreparadaRemision
{
    public int IdVenta { get; set; }
    public int Folio { get; set; }
    public string FolioFtm { get; set; } = string.Empty;

    /// <summary>El DINERO de la remision (columna DINERO de la reja de Mac31).</summary>
    public decimal ImporteZ { get; set; }

    /// <summary>Lo que se le cobro de cascos (columna CASCOS).</summary>
    public decimal Cascos { get; set; }

    /// <summary>Lo que ya se facturo antes de esta vez.</summary>
    public decimal TotalFacturado { get; set; }

    /// <summary>`ImporteZ + Cascos - TotalFacturado`. El tope del monto.</summary>
    public decimal PorFacturar { get; set; }

    /// <summary>Lo que propone Mac31, que NO es `PorFacturar`. Ver las cuatro ramas.</summary>
    public decimal MontoPropuesto { get; set; }

    /// <summary>Lo que se va a facturar de esta remision, cascos ya sumados si es la primera.</summary>
    public decimal MontoAFacturar { get; set; }

    public string Cancelada { get; set; } = string.Empty;
}

public class FacturaPreparadaRfc
{
    public int IdClienteFacturacion { get; set; }
    public string NombreFacturacion { get; set; } = string.Empty;
    public string Rfc { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    /// <summary>
    /// El regimen fiscal NO es un desplegable: sale del renglon de RFC elegido
    /// (VentasFacturacion.cs:1562, VentasNotaCredito.cs:2811-2812).
    /// </summary>
    public string RegimenFiscal { get; set; } = string.Empty;

    public string RegimenFiscalNombre { get; set; } = string.Empty;
    public string FormaPago { get; set; } = string.Empty;
    public string MetodoPago { get; set; } = string.Empty;
    public string UsoCfdi { get; set; } = string.Empty;
    public List<string> Correos { get; set; } = new();
}

public class FacturaPreparadaConcepto
{
    public int IdVenta { get; set; }
    public int IdVentaDetalle { get; set; }
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ClaveProdServ { get; set; } = string.Empty;
    public string ClaveUnidad { get; set; } = string.Empty;

    /// <summary>Lo que se factura. Editable, con tope en `CantidadOriginal`.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Lo que trae la remision.</summary>
    public decimal CantidadOriginal { get; set; }

    /// <summary>
    /// El precio NETO por pieza, con IVA dentro, ya reescalado si el monto a
    /// facturar no es el de la remision. Es el precio que se va a timbrar: la
    /// pantalla lo ensena, no lo calcula.
    /// </summary>
    public decimal PrecioUnitarioConIva { get; set; }

    /// <summary>
    /// SIEMPRE CERO. No es un campo olvidado: Legacy no manda descuento de
    /// concepto en ningun CFDI (FacturapiService.cs:860-872) porque el descuento
    /// ya esta dentro de `PrecioUnitarioConIva`. Se deja publicado para que el
    /// cliente que hoy lo calcula pueda compararlo y dejar de hacerlo.
    /// </summary>
    public decimal Descuento { get; set; }

    public bool Marcada { get; set; }

    /// <summary>
    /// El precio de LISTA que lee Mac31 (`FacturaPrecioListaConIva`). No viaja
    /// al CFDI: sirve para la validacion de descuento y para ensenar de cuanto
    /// fue la rebaja. Ver los avisos de la respuesta.
    /// </summary>
    public decimal PrecioListaLegacy { get; set; }

    /// <summary>El importe de este concepto, para que la pantalla no lo calcule.</summary>
    public decimal Importe { get; set; }
}

public class FacturaPreparadaCasco
{
    public int IdTipoUsado { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal CostoConIva { get; set; }
    public string ClaveSat { get; set; } = string.Empty;
    public int Orden { get; set; }

    /// <summary>Cuantos se van a facturar. Arranca en CERO, como Mac31.</summary>
    public int Cantidad { get; set; }
}

public class FacturaPreparadaTotales
{
    public decimal TotalFacturarDinero { get; set; }
    public decimal TotalFacturarCascos { get; set; }
    public decimal TotalFacturarDineroCascos { get; set; }

    /// <summary>PIEZAS de producto, no importe.</summary>
    public int TotalProductos { get; set; }

    /// <summary>PIEZAS de casco, no importe. Es `TotalUsados` de Mac31.</summary>
    public int TotalUsados { get; set; }

    /// <summary>Lo que falta por facturar de todas las remisiones, sin comision.</summary>
    public decimal TotalPorFacturar { get; set; }

    /// <summary>El tope real: `TotalPorFacturar * FactorDeComision`.</summary>
    public decimal TopeDeFacturacion { get; set; }

    /// <summary>1.03 con tarjeta de credito, 1.02 con debito, 1 en lo demas.</summary>
    public decimal FactorDeComision { get; set; }
}

public class FacturaPreparadaCatalogos
{
    public List<OpcionSatDto> FormasDePago { get; set; } = new();
    public List<OpcionSatDto> MetodosDePago { get; set; } = new();
    public List<OpcionSatDto> UsosCfdi { get; set; } = new();
}

/// <summary>Una opcion de catalogo del SAT: la clave es lo que viaja al CFDI.</summary>
public class OpcionSatDto
{
    public string Clave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

// ==========================================================================
// NOTA DE CREDITO
// ==========================================================================

/// <summary>Lo minimo para preparar una nota de credito.</summary>
/// <remarks>
/// LAS PARTIDAS NO LAS INVENTA ESTE ENDPOINT, y es lo primero que hay que saber
/// de esta forma. Mac31 las captura en un PEDIDO DE VERDAD (`cur_idPedido`,
/// VentasNotaCredito.cs:2864) con el mismo servicio de detalle que la captura de
/// pedidos normal (:1782), y el timbrado recibe ese `IDPedido` — que, comprobado
/// en un PDF real de MacZ, **se IMPRIME en el comprobante** ("APLICA A PEDIDOS
/// 116379"). Asi que aqui se recibe el `IdPedido` ya capturado y lo que se
/// prepara es el ENCABEZADO fiscal: receptor, claves del SAT, relacion con la
/// factura acreditada y las validaciones contra el saldo.
///
/// La captura en si vive en su propio controlador
/// (`/api/ventas/facturas/nota-credito/*`), con el permiso de facturacion y sin
/// la guardia de domicilio — ver la nota larga de `nucleo/notaDeCredito.ts`
/// sobre por que NO sirve `/api/ventas/pedidos/captura`.
/// </remarks>
public class NotaCreditoPrepararRequest
{
    /// <summary>La factura que se acredita.</summary>
    public int IdVentaFacturaRelacionada { get; set; }

    /// <summary>El pedido donde se capturaron las partidas de la nota.</summary>
    public int IdPedido { get; set; }

    public int IdClienteFacturacion { get; set; }

    /// <summary>1 nota de credito, 2 nota de credito de CASCOS. Lo elige `cboTipoProducto`.</summary>
    public int Tipo { get; set; } = 1;

    public int IdTipoNotaCredito { get; set; }
    public string? RelacionFactura { get; set; }
    public string? FormaPago { get; set; }
    public string? MetodoPago { get; set; }
    public string? UsoCfdi { get; set; }
    public List<string> Correos { get; set; } = new();
    public string? Observaciones { get; set; }
    public int IdEmpresaCs { get; set; }

    /// <summary>
    /// El total de la nota, TAL COMO LO DEVOLVIO LA CAPTURA.
    /// </summary>
    /// <remarks>
    /// Lo manda el cliente y no se recalcula aqui, y es a proposito: el total lo
    /// calcula el procedimiento al armar el pedido (`totalPagar` de la cuarta
    /// tabla de `sp_n_ConsultaPedido`) y Mac31 lo lee de `lblTotalPagar.Text`
    /// (VentasNotaCredito.cs:2789). Volver a sumarlo aqui seria tener dos reglas
    /// sobre el mismo peso, y la que se equivoca es siempre la copia. Lo que SI
    /// se hace con el es validarlo contra el saldo de la factura (:3066) y meterlo
    /// en la confirmacion.
    /// </remarks>
    public decimal Total { get; set; }

    /// <summary>
    /// Los conceptos que devolvio la captura, ya traducidos por el cliente desde
    /// el `snapshot`. Si vienen, se reenvian tal cual en el cuerpo del timbrado.
    /// </summary>
    public List<FacturaConcepto> Conceptos { get; set; } = new();
}

public class NotaCreditoPreparadaDto
{
    public FacturaTimbrarRequest Timbrado { get; set; } = new();
    public FacturaPreparadaReclamo? Bloqueo { get; set; }
    public List<string> Avisos { get; set; } = new();
    public string TextoDeConfirmacion { get; set; } = string.Empty;

    public List<FacturaPreparadaRfc> Rfcs { get; set; } = new();
    public List<FacturaPreparadaConcepto> Conceptos { get; set; } = new();
    public List<string> Correos { get; set; } = new();
    public List<string> CorreosDisponibles { get; set; } = new();
    public bool PreciosConIva { get; set; }

    /// <summary>El total de la nota, leido del procedimiento cuando lo manda.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// El saldo de la factura acreditada, TAL COMO LO MANDA EL PROCEDIMIENTO.
    /// No se resta aqui: es el numero contra el que se compara el total de la
    /// nota (VentasNotaCredito.cs:3066), y una resta propia podria dejar pasar
    /// una nota mayor al saldo por un centavo de redondeo.
    /// </summary>
    public decimal SaldoDeLaFactura { get; set; }

    public string FolioDeLaFactura { get; set; } = string.Empty;
    public string UuidDeLaFactura { get; set; } = string.Empty;
}

// ==========================================================================
// COMPLEMENTO DE PAGO
// ==========================================================================

/// <summary>Lo minimo para preparar un complemento de pago.</summary>
public class ComplementoPrepararRequest
{
    /// <summary>Las facturas que se pagan. Llegan de la consulta de facturas.</summary>
    public List<int> IdsVentaFactura { get; set; } = new();

    public int IdClienteFacturacion { get; set; }
    public string? FormaPago { get; set; }

    /// <summary>La fecha y la HORA del pago: las dos van dentro del CFDI.</summary>
    public DateTime? FechaPago { get; set; }

    /// <summary>Lo que se paga de cada factura. Vacio = su saldo completo.</summary>
    public List<ComplementoPrepararPago> Pagos { get; set; } = new();

    public List<string> Correos { get; set; } = new();
    public string? Observaciones { get; set; }
    public int IdEmpresaCs { get; set; }
}

public class ComplementoPrepararPago
{
    public int IdVentaFactura { get; set; }
    public decimal Importe { get; set; }
}

public class ComplementoPreparadoDto
{
    /// <summary>El cuerpo exacto para `POST /api/ventas/facturas/complemento-pago`.</summary>
    public ComplementoPagoTimbrarRequest Timbrado { get; set; } = new();

    public FacturaPreparadaReclamo? Bloqueo { get; set; }
    public List<string> Avisos { get; set; } = new();

    public List<ComplementoPreparadoDocumento> Documentos { get; set; } = new();
    public List<OpcionSatDto> FormasDePago { get; set; } = new();
    public List<string> Correos { get; set; } = new();
    public List<string> CorreosDisponibles { get; set; } = new();

    public decimal TotalSaldo { get; set; }
    public decimal TotalAPagar { get; set; }

    public FacturaPreparadaCliente Cliente { get; set; } = new();
    public List<FacturaPreparadaRfc> Rfcs { get; set; } = new();
}

public class ComplementoPreparadoDocumento
{
    public int IdVentaFactura { get; set; }
    public string IdFacturapi { get; set; } = string.Empty;
    public string FolioFtm { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string FechaFtm { get; set; } = string.Empty;

    public decimal TotalPagarFactura { get; set; }
    public decimal Abonos { get; set; }
    public decimal NotasCredito { get; set; }

    /// <summary>`TotalPagarFactura - Abonos - NotasCredito`.</summary>
    public decimal Saldo { get; set; }

    /// <summary>Lo que se va a pagar. Arranca en el saldo completo.</summary>
    public decimal Importe { get; set; }

    /// <summary>Cuantos complementos lleva ya. Es la PARCIALIDAD del SAT menos uno.</summary>
    public int NumeroPagos { get; set; }

    public string Cancelada { get; set; } = string.Empty;
}
