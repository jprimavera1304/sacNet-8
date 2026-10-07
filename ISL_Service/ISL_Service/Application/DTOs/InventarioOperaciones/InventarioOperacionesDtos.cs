namespace ISL_Service.Application.DTOs.InventarioOperaciones;

/*
  AJUSTES DE INVENTARIO, INVENTARIO DE PEDIDOS Y EL CATALOGO DE TIPOS DE CASCO

  Tres cosas pequenas que viajaban juntas en legacy y que comparten una
  caracteristica: su DELETE o su cancelacion es lo mas peligroso que tienen, y es
  justo lo que menos validaban.

  El caso extremo es el catalogo de tipos de casco: en legacy el DELETE y el PUT
  llaman al MISMO metodo, con los MISMOS doce parametros, y la unica diferencia
  es que el DELETE no valida el nombre. O sea que "borrar" un tipo de casco
  consiste en que el cliente se acuerde de mandar el IDStatus de baja — y si
  manda el cuerpo incompleto, sobrescribe el renglon con ceros: precios en cero,
  peso en cero, bonificacion en cero. Eso no es un borrado, es perder el dato.
  Aqui el cambio de estatus es su propio endpoint y recibe el estatus, nada mas.
*/

/// <summary>Un ajuste manual de existencias.</summary>
public class AjusteInventarioCrearRequest
{
    public int IdAlmacen { get; set; }
    public int IdProducto { get; set; }

    /// <summary>Piezas que entran. Entrada y salida son excluyentes.</summary>
    public int Entrada { get; set; }

    /// <summary>Piezas que salen.</summary>
    public int Salida { get; set; }

    /// <summary>Por que se ajusta. Obligatorio: es lo unico que explica el ajuste.</summary>
    public string? Motivo { get; set; }
}

/// <summary>Cancela un ajuste de inventario.</summary>
public class AjusteInventarioCancelarRequest
{
    public int IdAjusteInventario { get; set; }
}

/// <summary>Filtros de la consulta de ajustes de inventario.</summary>
public class AjusteInventarioConsultaRequest
{
    public List<int> IdsGrupoCategoria { get; set; } = new();
    public List<int> IdsCategoria { get; set; } = new();
    public List<int> IdsMarca { get; set; } = new();

    /// <summary>Vacio = todos los almacenes.</summary>
    public List<int> IdsAlmacen { get; set; } = new();

    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>
/// Un renglon del inventario de pedidos: lo que hay, lo que se vendio, lo que
/// falta y lo que se va a pedir, de un producto.
/// </summary>
public class InventarioPedidoPartida
{
    public int IdProducto { get; set; }

    /// <summary>El codigo tal como se escaneo o se tecleo.</summary>
    public string? Codigo { get; set; }

    public int CantidadInventario { get; set; }
    public int CantidadVentas { get; set; }
    public int CantidadFaltante { get; set; }
    public int CantidadPedido { get; set; }
}

/// <summary>Captura un inventario de pedidos completo.</summary>
public class InventarioPedidoCrearRequest
{
    /// <summary>1 activo, 2 cancelado. El procedimiento lo recibe tal cual.</summary>
    public int IdStatus { get; set; } = 1;

    public string? Observaciones { get; set; }

    /// <summary>
    /// Las partidas. En legacy eran SEIS listas paralelas que el servicio pegaba
    /// con virgulilla, cruzadas por posicion dentro del procedimiento: si una
    /// venia con un elemento menos, el codigo de un producto acababa con la
    /// cantidad de otro. Aqui es una lista de renglones y el desajuste no se
    /// puede expresar.
    /// </summary>
    public List<InventarioPedidoPartida> Partidas { get; set; } = new();
}

/// <summary>Cancela un inventario de pedidos.</summary>
public class InventarioPedidoCancelarRequest
{
    public int IdInventarioPedido { get; set; }
}

/// <summary>Filtros de la consulta del inventario de pedidos.</summary>
public class InventarioPedidoConsultaRequest
{
    public int IdInventarioPedido { get; set; }
    public int IdStatus { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }
}

/// <summary>Un tipo de casco usado del catalogo.</summary>
public class TipoUsadoCrearRequest
{
    public string? TipoUsado { get; set; }

    /// <summary>El nombre corto, el que cabe en la rejilla y en el ticket.</summary>
    public string? TipoUsadoCorto { get; set; }

    public decimal VentaCostoCargoSinIva { get; set; }
    public decimal VentaCostoCargoConIva { get; set; }
    public decimal VentaCostoCreditoSinIva { get; set; }
    public decimal VentaCostoCreditoConIva { get; set; }

    /// <summary>Lo que se le bonifica al cliente por entregarlo.</summary>
    public decimal Bonificacion { get; set; }

    /// <summary>Lo que se cobra cuando entrega mas de los que le tocan.</summary>
    public decimal CostoExcedentes { get; set; }

    public decimal PesoKg { get; set; }

    /// <summary>En que posicion sale en la rejilla.</summary>
    public int Orden { get; set; }
}

/// <summary>Modifica un tipo de casco usado.</summary>
public class TipoUsadoActualizarRequest : TipoUsadoCrearRequest
{
    public int IdTipoUsado { get; set; }
}

/// <summary>
/// Cambia el estatus de un tipo de casco usado: 1 activo, 2 inactivo.
///
/// Es un endpoint propio y no un campo de "actualizar" porque en legacy el
/// DELETE reusaba el actualizar completo, y con un cuerpo incompleto dejaba el
/// renglon con todos los importes en cero. Aqui no hay manera de borrar un
/// precio por querer inhabilitar un tipo.
/// </summary>
public class TipoUsadoCambiarStatusRequest
{
    public int IdTipoUsado { get; set; }

    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; }
}

/// <summary>
/// Un tipo de casco tal como esta guardado: sus datos y su estatus. Existe
/// porque el procedimiento de actualizar sobrescribe el renglon completo, asi
/// que para cambiar un campo hay que conocer los otros once.
/// </summary>
public class TipoUsadoGuardado
{
    public TipoUsadoActualizarRequest Datos { get; set; } = new();

    /// <summary>1 = Activo, 2 = Inactivo.</summary>
    public int IdStatus { get; set; }
}
