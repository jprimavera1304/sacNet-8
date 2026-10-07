namespace ISL_Service.Application.DTOs.VentasUsadosOperaciones;

/*
  LOS CASCOS USADOS: COBROS DE DINERO, COBROS DE CASCOS, EXCEDENTES Y
  MOVIMIENTOS DE ALMACEN.

  En MacServicios2 esto son cuatro controladores con VEINTE endpoints, y los
  cuatro comparten un vicio: UN SOLO DTO de entrada por controlador, con todos
  los campos de todas las pantallas juntos. El de cobros de dinero tiene mas de
  noventa propiedades, y cada endpoint usa diez. El resultado practico es que
  nadie puede saber, leyendo el DTO, que necesita una llamada: hay que leer el
  repositorio para enterarse de que @CantidadesG1..G7 solo los mira /actualizar.

  Aqui va un request POR OPERACION. Es mas codigo y es a proposito: el contrato
  de cada endpoint se lee en su propio tipo, y un campo que no esta no se puede
  mandar por accidente.

  LO QUE NO VIAJA EN NINGUNO DE ESTOS REQUEST
  -------------------------------------------
  `IDUsuario` y `Equipo`. En legacy son campos del cuerpo, y eso significa que
  cualquiera podia aplicar un cobro de cascos a nombre de otro: el cuerpo lo
  escribe el cliente. Aqui salen del token, siempre, y el controlador no ofrece
  manera de pisarlos.

  LAS LISTAS VAN COMO LISTAS
  --------------------------
  Legacy recibe listas de verdad y luego las pega con virgulilla antes de
  llamar al procedimiento (string.Join("~", ...)). Los sp_n_ siguen esperando
  esa cadena, asi que el pegado se queda — pero en el repositorio, que es donde
  vive el detalle de como habla la base, y no en el contrato publico.
*/

/// <summary>
/// Un cobro de dinero por cascos usados: lo que el repartidor entrega.
/// </summary>
public class CobroDineroUsadoCrearRequest
{
    /// <summary>
    /// 0 para uno nuevo. Distinto de 0 para seguir cargando sobre el que ya
    /// existe: el procedimiento lo trata como "agregar al mismo cobro".
    /// </summary>
    public int IdCobroDineroUsado { get; set; }

    public int IdCobro { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Los tipos de casco que se entregan. Mismo largo que las otras dos.</summary>
    public List<int> IdsTipoUsado { get; set; } = new();

    public List<decimal> PreciosUnitario { get; set; } = new();

    public List<int> Cantidades { get; set; } = new();

    public decimal Efectivo { get; set; }
    public decimal Cheque { get; set; }
    public decimal Transferencia { get; set; }
    public decimal Tarjeta { get; set; }
    public decimal DepositoEfectivo { get; set; }
}

/// <summary>
/// El reparto del cobro de dinero entre los clientes que lo pagaron.
///
/// Sirve para dos operaciones distintas que en legacy se distinguian con una
/// bandera `esAplicacion` en el cuerpo: 0 guardaba el reparto
/// (sp_n_ActualizarCobroDineroUsado) y 1 lo aplicaba de verdad a los saldos
/// (sp_n_AplicarCobroDineroUsado). Aqui son dos endpoints distintos: una
/// bandera en el cuerpo que decide entre "guardar" y "mover dinero" es una
/// bandera que se manda mal una vez y nadie sabe por que se aplico un cobro.
/// </summary>
public class CobroDineroUsadoRepartoRequest
{
    public int IdCobroDineroUsado { get; set; }

    /// <summary>Clientes ya existentes a los que se reparte.</summary>
    public List<int> IdsClientes { get; set; } = new();

    /// <summary>
    /// Clientes que se dan de alta en el mismo movimiento. Van por separado
    /// porque el procedimiento los distingue: los de aqui todavia no tienen
    /// IDCliente.
    /// </summary>
    public List<int> ClientesNuevos { get; set; } = new();

    public List<int> IdsTipoUsado { get; set; } = new();

    /// <summary>
    /// Las cantidades por grupo de casco, G1 a G7 mas moto. Son ocho listas
    /// paralelas porque la rejilla de Mac31 tiene ocho columnas fijas, una por
    /// grupo, y el procedimiento recibe ocho parametros.
    /// </summary>
    public List<int> CantidadesG1 { get; set; } = new();
    public List<int> CantidadesG2 { get; set; } = new();
    public List<int> CantidadesG3 { get; set; } = new();
    public List<int> CantidadesG4 { get; set; } = new();
    public List<int> CantidadesG5 { get; set; } = new();
    public List<int> CantidadesG6 { get; set; } = new();
    public List<int> CantidadesG7 { get; set; } = new();
    public List<int> CantidadesMoto { get; set; } = new();

    public List<decimal> PagosEfectivo { get; set; } = new();
    public List<decimal> PagosTarjeta { get; set; } = new();

    public string? ObservacionesAplicacion { get; set; }
}

/// <summary>Cancela la aplicacion de un cobro de dinero por cascos.</summary>
public class CobroDineroUsadoCancelarRequest
{
    public int IdCobroDineroUsado { get; set; }
}

/// <summary>Los filtros de la pantalla de cobros de dinero por cascos.</summary>
public class CobroDineroUsadoConsultaRequest
{
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }
    public int IdRepartidor { get; set; }
    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
    public DateTime? FechaModificacionInicial { get; set; }
    public DateTime? FechaModificacionFinal { get; set; }
}

/// <summary>Un cheque recibido dentro de un cobro de dinero por cascos.</summary>
public class CobroDineroUsadoChequeCrearRequest
{
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }
    public int IdBancoCheque { get; set; }
    public string? NumeroCheque { get; set; }

    /// <summary>
    /// La fecha del cheque. Obligatoria: en legacy, si venia vacia, la
    /// conversion de formato reventaba con un error de Substring y el endpoint
    /// contestaba 400 con el stack.
    /// </summary>
    public DateTime? FechaCheque { get; set; }

    public decimal ImporteCheque { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Modifica un cheque ya registrado, o le cambia el estatus.</summary>
public class CobroDineroUsadoChequeActualizarRequest
{
    public int IdCobroDineroUsadoCheque { get; set; }
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }

    /// <summary>El estatus del cheque (cobrado, devuelto…) tal como lo tipifica legacy.</summary>
    public int IdStatusCheque { get; set; }

    public int IdBancoCheque { get; set; }
    public string? NumeroCheque { get; set; }
    public DateTime? FechaCheque { get; set; }
    public decimal ImporteCheque { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Filtros de los cheques de un cobro de dinero por cascos.</summary>
public class CobroDineroUsadoChequeConsultaRequest
{
    public int IdCobroDineroUsadoCheque { get; set; }
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>
/// Liga (o desliga) un corte de un centro de servicio al cobro de dinero.
/// El mismo request sirve para las dos operaciones: el procedimiento de
/// insertar y el de eliminar reciben exactamente los mismos parametros.
/// </summary>
public class CobroDineroUsadoCorteCsRequest
{
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }
    public int IdCentro { get; set; }
    public int IdCorte { get; set; }
}

/// <summary>Filtros de los cortes de centro de servicio del cobro.</summary>
public class CobroDineroUsadoCorteCsConsultaRequest
{
    public int IdCobroDineroUsadoCorteCs { get; set; }
    public int IdCobroDineroUsado { get; set; }
    public int IdCobro { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>
    /// Varios cobros de una vez. El procedimiento lo recibe como
    /// nvarchar(max) y lo parte; es el unico parametro de esta familia que no
    /// esta acotado a 4000.
    /// </summary>
    public List<int> IdsCobro { get; set; } = new();
}

/// <summary>El ingreso de cascos usados contra un cobro.</summary>
public class VentasCobroUsadoCrearRequest
{
    public int IdCobro { get; set; }
    public int IdEmpresa { get; set; }
    public string? Observaciones { get; set; }
    public List<int> IdsTipoUsado { get; set; } = new();
    public List<int> CantidadesUsado { get; set; } = new();
    public List<decimal> CostosConIvaUsado { get; set; } = new();
}

/// <summary>Cancela un ingreso de cascos usados.</summary>
public class VentasCobroUsadoCancelarRequest
{
    public int IdVentaCobroUsado { get; set; }
}

/// <summary>Filtros de los ingresos de cascos usados.</summary>
public class VentasCobroUsadoConsultaRequest
{
    public int IdVentaCobroUsado { get; set; }
    public int IdCobro { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>
/// Un excedente de cascos: los cascos que el cliente entrego de mas y quedan
/// a su favor con un vale.
///
/// El nombre del procedimiento y de sus parametros dice "Excendente", con una
/// `n` de mas, y asi se llama en la base. En el contrato nuevo se escribe bien
/// (`Excedente`): el dia que esto sea un sp_w_ el error no le sobrevive.
/// </summary>
public class VentasUsadoExcedenteCrearRequest
{
    public int IdCobro { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public int IdVenta { get; set; }
    public int IdPagoVenta { get; set; }
    public int IdEmpresa { get; set; }

    /// <summary>El numero de vale con el que el cliente reclama el excedente.</summary>
    public string? Vale { get; set; }

    public string? Observaciones { get; set; }
    public List<int> IdsTipoUsado { get; set; } = new();
    public List<int> CantidadesUsado { get; set; } = new();
    public List<decimal> CostosConIvaUsado { get; set; } = new();
}

/// <summary>Cancela un excedente de cascos.</summary>
public class VentasUsadoExcedenteCancelarRequest
{
    public int IdVentaUsadoExcedente { get; set; }
    public int IdPagoVenta { get; set; }
}

/// <summary>Filtros de los excedentes de cascos.</summary>
public class VentasUsadoExcedenteConsultaRequest
{
    public int IdVentaUsadoExcedente { get; set; }
    public int IdCobro { get; set; }
    public int IdCliente { get; set; }
    public int IdRepartidor { get; set; }
    public string? Vale { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>
/// Un movimiento del almacen de cascos usados: entradas, salidas y cambios con
/// un gallero.
/// </summary>
public class UsadoMovimientoCrearRequest
{
    public int IdAlmacenUsado { get; set; }
    public int IdGallero { get; set; }
    public int IdRepartidor { get; set; }
    public string? Motivo { get; set; }

    public List<int> IdsTipoUsado { get; set; } = new();
    public List<decimal> PreciosCambio { get; set; } = new();
    public List<decimal> PreciosUnitario { get; set; } = new();
    public List<int> CantidadesSalida { get; set; } = new();
    public List<int> CantidadesEntrada { get; set; } = new();

    /// <summary>
    /// El total de la entrada. Es el UNICO total que recibe el procedimiento:
    /// legacy tiene tambien un `TotalPagar` en el DTO y nunca lo manda, asi
    /// que aqui no existe.
    /// </summary>
    public decimal TotalPagarEntrada { get; set; }

    public int IdCobro { get; set; }
    public int IdVenta { get; set; }
    public int IdTipoCompra { get; set; }
    public int IdTipoVenta { get; set; }
}

/// <summary>Cancela un movimiento del almacen de cascos usados.</summary>
public class UsadoMovimientoCancelarRequest
{
    public int IdUsadoMovimiento { get; set; }
}

/// <summary>Filtros de los movimientos del almacen de cascos usados.</summary>
public class UsadoMovimientoConsultaRequest
{
    public int IdUsadoMovimiento { get; set; }
    public int IdGallero { get; set; }
    public int IdRepartidor { get; set; }
    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>El tipo de consulta que tipifica legacy (0 todos, 1 cambios…).</summary>
    public int TipoConsulta { get; set; }
}

/// <summary>Filtros de los movimientos ya pagados (los del corte).</summary>
public class UsadoMovimientoPagadoConsultaRequest
{
    public int IdGallero { get; set; }
    public int IdRepartidor { get; set; }
    public int TipoConsulta { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
    public int IdCorte { get; set; }
}
