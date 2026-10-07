namespace ISL_Service.Application.DTOs.OperacionMac;

/*
  OPERACION DIARIA DE MAC31: COMPRAS, INVENTARIO EN TRANSITO, CORTES DE CAJA,
  TRANSFERENCIAS BANCARIAS Y CAMBIOS DE VIGENCIA.

  Son cinco pantallas distintas que comparten tres problemas, y por eso van
  juntas:

  1. LISTAS PARALELAS UNIDAS CON '~'. Legacy mandaba dos o cuatro cadenas
     separadas que el procedimiento apareaba POR POSICION, y nadie comprobaba
     que tuvieran el mismo numero de elementos. En el corte eran cuatro
     (IDsTipoGasto, IDsEmpleadoPrestamo, Conceptos, Montos); al aplicar una
     transferencia eran dos (IDsVentas y MontoPagos) y una desalineacion aplica
     el pago de una venta a otra, en silencio y en dinero. Aqui todo viaja como
     LISTA DE OBJETOS y el backend arma las cadenas: ya no hay forma de
     desalinearlas.

  2. NUMEROS CON LA CULTURA DEL SERVIDOR. Legacy usaba string.Join y .ToString()
     sin cultura: en un servidor configurado en un idioma donde el separador
     decimal es la coma, un 12.5 se convierte en "12,5" y el procedimiento, que
     parte cadenas, lee dos valores donde habia uno. Aqui todo se escribe con
     cultura invariante.

  3. FECHAS COMO TEXTO dd-MM-yyyy convertido a mano con Substring. Aqui las
     fechas son fechas y el backend las pasa a 'MM-dd-yyyy', que es lo que
     espera legacy.

  Y una cuarta que es de contrato: en legacy el id del usuario y el nombre del
  equipo viajaban en el cuerpo. Aqui salen del token.
*/

/// <summary>El par result/mensaje de legacy, traducido.</summary>
public class OperacionEscrituraResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Id { get; set; }
}

// ===========================================================================
// COMPRAS
// ===========================================================================

/// <summary>
/// Filtro de la consulta de compras.
///
/// LA PRECEDENCIA ES EXPLICITA: si viene `factura`, se busca SOLO por factura y
/// los demas filtros se ignoran. Eso es lo que hacia legacy, pero a escondidas:
/// reemplazaba el DTO entero por uno nuevo con solo la factura. Y lo hacia mal,
/// porque el campo no tenia valor por omision: una peticion que no mandara
/// factura entraba igual a esa rama (null != "") y consultaba con @Factura nulo,
/// tirando todos los filtros que el usuario si habia puesto. Aqui la rama solo
/// se toma cuando de verdad hay texto, y la respuesta lo dice.
/// </summary>
public class ComprasFiltro
{
    public int IdCompra { get; set; }
    public int IdEmpresa { get; set; }
    public int IdAlmacen { get; set; }
    public int IdProveedor { get; set; }
    public int IdProducto { get; set; }

    /// <summary>El usuario que capturo la compra. Es un FILTRO, no la firma.</summary>
    public int IdUsuarioCaptura { get; set; }

    public string? Factura { get; set; }

    public DateTime? CapturaDesde { get; set; }
    public DateTime? CapturaHasta { get; set; }
    public DateTime? EmisionDesde { get; set; }
    public DateTime? EmisionHasta { get; set; }
    public DateTime? CancelacionDesde { get; set; }
    public DateTime? CancelacionHasta { get; set; }
    public DateTime? PagoDesde { get; set; }
    public DateTime? PagoHasta { get; set; }
}

/// <summary>
/// Marca o desmarca la compra como factura.
///
/// Es lo UNICO que sp_n_ActualizarCompra cambia. En legacy el campo no tenia
/// valor por omision, asi que un PUT que no lo mandara dejaba la compra marcada
/// como "no es factura" sin que nadie lo pidiera. Aqui es obligatorio por ser el
/// cuerpo entero del endpoint.
/// </summary>
public class MarcarCompraFacturaRequest
{
    public bool EsFactura { get; set; }
}

/// <summary>Cancelacion de compra. El motivo queda guardado en la compra.</summary>
public class CancelarCompraRequest
{
    public string MotivoCancelacion { get; set; } = string.Empty;
}

/// <summary>
/// Procesa el inventario en transito de un renglon de venta.
///
/// En legacy este POST solo exigia el IDVentaDetalle: ni el cliente ni el
/// usuario, y el equipo no tenia valor por omision, asi que una peticion sin
/// equipo reventaba con una referencia nula. Aqui el cliente es obligatorio
/// (el movimiento se le carga a alguien) y el equipo sale del token.
/// </summary>
public class ProcesarTransitoRequest
{
    public int IdVentaDetalle { get; set; }
    public int IdCliente { get; set; }
}

// ===========================================================================
// CORTES DE CAJA
// ===========================================================================

/// <summary>
/// Los veinte importes del desglose del corte.
///
/// Legacy los mandaba en UNA cadena armada en C#, pegada con '@@@@' y, en medio,
/// un '||||' para separar Tarjetas de TarjetasTiendas dentro del mismo campo. El
/// procedimiento la vuelve a partir con CHARINDEX, campo por campo, veinte
/// veces. Ese formato se conserva porque el procedimiento es intocable, pero se
/// arma AQUI, desde campos con nombre y con cultura invariante: en legacy un
/// servidor con coma decimal partia un importe en dos y corria todo el desglose
/// un lugar.
///
/// El orden importa y es el del procedimiento, no uno elegido: SaeEsquina,
/// SaeEsquinaDomingo, SaeLocalTauro, TotalSae, MacCamioneta, MacAgentes,
/// TotalMac, Transferencias, TransferenciasTiendaEsquina,
/// TransferenciasLocalTauro, DepositoCheque, DepositoEfectivo,
/// Tarjetas||||TarjetasTiendas, Cheque, Efectivo, Morraya, TotalGastos,
/// TotalDesglose, GranTotal, NoAplicadoDesglose.
/// </summary>
public class DesgloseCorte
{
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
    public decimal NoAplicadoDesglose { get; set; }
}

/// <summary>
/// Un gasto del corte: de que tipo es, a que prestamo se aplica, el concepto y
/// el monto.
///
/// En legacy eran CUATRO listas paralelas (IDsTipoGasto, IDsEmpleadoPrestamo,
/// Conceptos, Montos) unidas con '~' y apareadas por posicion por el
/// procedimiento, sin que nadie comprobara que tenian la misma longitud: una
/// lista corrida le pone a un gasto el monto de otro. Aqui es un renglon.
/// </summary>
public class GastoCorte
{
    public int IdTipoGasto { get; set; }

    /// <summary>0 cuando el gasto no es el descuento de un prestamo.</summary>
    public int IdEmpleadoPrestamo { get; set; }

    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>Alta del corte de caja del dia.</summary>
public class CrearCorteRequest
{
    public DateTime FechaCorte { get; set; }
    public DesgloseCorte Desglose { get; set; } = new();
    public List<GastoCorte> Gastos { get; set; } = new();

    public string? Observaciones { get; set; }
    public string? ObservacionesCheque { get; set; }
    public string? ObservacionesDepositosCheque { get; set; }
    public string? ObservacionesDepositoEfectivo { get; set; }
    public string? ObservacionesSobranteLiquidaciones { get; set; }

    public decimal LiquidacionTiendas { get; set; }
    public decimal NoAplicado { get; set; }
    public decimal SobranteLiquidaciones { get; set; }
}

/// <summary>Corte de retiro de un centro de servicio.</summary>
public class CrearCorteCentroServicioRequest
{
    public int IdTipoRetiro { get; set; }
    public decimal MontoRetiro { get; set; }
    public decimal MontoRetiroTarjetaCredito { get; set; }
    public decimal MontoRetiroTarjetaDebito { get; set; }

    /// <summary>
    /// Hasta que fecha se corta. Es la unica fecha que viaja: legacy convertia
    /// tambien una FechaInicial y la descartaba sin usarla.
    /// </summary>
    public DateTime FechaFinal { get; set; }

    public string? Observaciones { get; set; }
}

// ===========================================================================
// TRANSFERENCIAS BANCARIAS
// ===========================================================================

/// <summary>
/// Un movimiento del estado de cuenta que se sube al sistema.
///
/// SE SUBEN EN LOTE, Y ESO ES EL CAMBIO DE FONDO. En legacy cada movimiento era
/// una peticion suelta: el cliente llamaba N veces, sin transaccion, y si el
/// archivo tenia 400 renglones y fallaba en el 250, quedaban 249 cargados y el
/// estado de cuenta a medias — sin forma de saber desde donde seguir. Aqui el
/// lote entero va en UNA transaccion: o entra completo o no entra nada.
/// </summary>
public class MovimientoTransferencia
{
    public int Num { get; set; }
    public DateTime Fecha { get; set; }
    public string? Descripcion { get; set; }
    public int Suc { get; set; }
    public string? RefNumerica { get; set; }
    public string? RefAlfanumerica { get; set; }
    public string? Autorizacion { get; set; }
    public string? Ordenante { get; set; }
    public string? BancoEmisor { get; set; }
    public decimal Deposito { get; set; }
    public decimal Retiro { get; set; }
    public decimal Saldo { get; set; }
}

/// <summary>Carga de un estado de cuenta: la cabecera y sus movimientos.</summary>
public class CargarTransferenciasRequest
{
    /// <summary>
    /// 0 para crear el estado de cuenta; el id existente para agregarle
    /// movimientos. El procedimiento crea la cabecera en la primera llamada y
    /// devuelve su id.
    /// </summary>
    public int IdBancoTransferencia { get; set; }

    public string? RutaArchivo { get; set; }
    public string Archivo { get; set; } = string.Empty;

    public decimal SaldoInicial { get; set; }
    public decimal SaldoFinal { get; set; }
    public decimal TotalDepositos { get; set; }
    public decimal TotalRetiros { get; set; }

    /// <summary>
    /// Cuantos movimientos dice el archivo que trae. Se compara contra los que
    /// vienen en la lista: si no coinciden, no se carga nada. Legacy no lo
    /// comparaba.
    /// </summary>
    public int NumMovimientos { get; set; }

    public List<MovimientoTransferencia> Movimientos { get; set; } = new();
}

/// <summary>Filtro de los estados de cuenta.</summary>
public class TransferenciasFiltro
{
    public int IdBancoTransferencia { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int IdCliente { get; set; }
    public string? Referencia { get; set; }
    public string? Archivo { get; set; }
    public int Aplicado { get; set; }
    public int Rechazado { get; set; }
}

/// <summary>Filtro de los movimientos de un estado de cuenta.</summary>
public class MovimientosTransferenciaFiltro
{
    public int IdBancoTransferencia { get; set; }
    public int IdStatusTransferencia { get; set; }
    public List<int> IdsClientes { get; set; } = new();
    public int IdCliente { get; set; }
    public string? Referencia { get; set; }
    public int Aplicado { get; set; }
    public int Rechazado { get; set; }
    public int Identificado { get; set; }
    public int NoIdentificado { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

/// <summary>
/// Dice a que cliente pertenecen uno o varios movimientos.
///
/// En legacy el cliente no era obligatorio: identificar al cliente 0 dependia de
/// lo que el procedimiento hiciera con un cero. Aqui es obligatorio, porque sin
/// cliente la operacion no significa nada.
/// </summary>
public class IdentificarTransferenciasRequest
{
    public List<int> IdsMovimiento { get; set; } = new();
    public int IdBancoTransferencia { get; set; }
    public int IdEmpresa { get; set; }
    public int IdCliente { get; set; }
}

/// <summary>Un pago que se aplica a una venta desde un movimiento bancario.</summary>
public class PagoDeTransferencia
{
    public int IdVenta { get; set; }
    public decimal Monto { get; set; }
}

/// <summary>
/// Aplica un movimiento bancario a una o varias ventas. AQUI SE MUEVE DINERO.
///
/// DOS COSAS QUE SE ARREGLAN:
///  - Los pagos viajan como lista de pares venta/monto. En legacy eran dos
///    cadenas '~' apareadas por posicion y nadie comprobaba que tuvieran la
///    misma longitud: un pago aplicado a la venta equivocada, en silencio.
///  - `aplicarA` es un valor, no dos banderas. Legacy tenia dos booleanos
///    independientes (aplicarDinero y aplicarCascos) con dos `if` sueltos: si
///    los dos venian en true ganaba cascos, y si los dos venian en false
///    ejecutaba un procedimiento con nombre VACIO y contestaba
///    "[No se pudo procesar ] - ...". Con un valor obligatorio no hay estado
///    imposible.
/// </summary>
public class AplicarTransferenciaRequest
{
    public int IdMovimiento { get; set; }
    public int IdBancoTransferencia { get; set; }
    public int IdEmpresa { get; set; }

    /// <summary>"dinero" o "cascos".</summary>
    public string AplicarA { get; set; } = AplicacionTransferencia.Dinero;

    public List<PagoDeTransferencia> Pagos { get; set; } = new();
}

/// <summary>A que se aplica un movimiento bancario.</summary>
public static class AplicacionTransferencia
{
    public const string Dinero = "dinero";
    public const string Cascos = "cascos";

    /// <summary>El procedimiento que corresponde, o null si el valor no se reconoce.</summary>
    public static string? SpDe(string? aplicarA)
        => (aplicarA ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Dinero => "dbo.sp_n_AplicarBancoTransferencia",
            Cascos => "dbo.sp_n_AplicarBancoTransferenciaUsados",
            _ => null
        };
}

// ===========================================================================
// CAMBIOS DE VIGENCIA
// ===========================================================================

/// <summary>Un producto que entra a cambio de vigencia, con su vigencia y garantia.</summary>
public class ProductoCambioVigencia
{
    public int IdProducto { get; set; }
    public string Vigencia { get; set; } = string.Empty;
    public string Garantia { get; set; } = string.Empty;
}

/// <summary>
/// Alta de un cambio de vigencia.
///
/// Los productos viajan como lista de objetos. En legacy eran TRES cadenas '~'
/// (IDsProducto, Vigencias, Garantias) apareadas por posicion, mas un
/// NumProductos que podia contradecirlas: nada comprobaba que los cuatro datos
/// hablaran del mismo renglon. Aqui NumProductos lo cuenta el backend desde la
/// lista.
/// </summary>
public class CrearCambioVigenciaRequest
{
    public int IdCliente { get; set; }
    public int IdUsuarioRecibe { get; set; }
    public List<ProductoCambioVigencia> Productos { get; set; } = new();
    public string? Observaciones { get; set; }
}

/// <summary>
/// Modificacion de un renglon del cambio de vigencia. Es UN producto por
/// llamada: asi lo recibe el procedimiento (@IDProducto en singular).
/// </summary>
public class ActualizarCambioVigenciaRequest
{
    public int IdStatusCambioVigencia { get; set; }
    public int IdCliente { get; set; }
    public int IdUsuarioRecibe { get; set; }
    public int IdProducto { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Ticket { get; set; }
    public string? Vigencia { get; set; }
    public string? Garantia { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Filtro de los cambios de vigencia.</summary>
public class CambiosVigenciaFiltro
{
    public int IdCambioVigencia { get; set; }
    public int IdStatusCambioVigencia { get; set; }
    public int IdCliente { get; set; }
    public int IdUsuarioRecibe { get; set; }
    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}
