namespace ISL_Service.Application.DTOs.Gasolinas;

/*
  LOS RECIBOS DE GASOLINA DE LOS REPARTIDORES.

  Un recibo apunta: que auto, que repartidor, que dia, el kilometraje inicial y
  final, el precio por litro y el total del recibo. Los cuatro sp_n_ de este
  modulo SI estan en Produccion_svr y con la misma firma que espera Legacy, asi
  que esta es una de las partes comparables del lote.
*/

/// <summary>Filtros de la consulta de recibos de gasolina.</summary>
public class GasolinasFiltro
{
    public int IdGasolina { get; set; }
    public int IdAuto { get; set; }
    public int IdRepartidor { get; set; }

    /// <summary>
    /// 1 = solo los recibos que ya existen (el SP lo usa para la validacion de
    /// "este numero de recibo ya se capturo").
    /// </summary>
    public int Existe { get; set; }

    public string? NumeroRecibo { get; set; }

    /// <summary>Fecha del recibo, desde.</summary>
    public DateOnly? FechaInicial { get; set; }
    /// <summary>Fecha del recibo, hasta.</summary>
    public DateOnly? FechaFinal { get; set; }

    /// <summary>Fecha de captura, desde.</summary>
    public DateOnly? FechaInicialCaptura { get; set; }
    /// <summary>Fecha de captura, hasta.</summary>
    public DateOnly? FechaFinalCaptura { get; set; }
}

/// <summary>Alta de un recibo de gasolina.</summary>
public class CrearGasolinaRequest
{
    /// <summary>El auto que cargo. Requerido.</summary>
    public int IdAuto { get; set; }

    /// <summary>El repartidor que cargo. Requerido.</summary>
    public int IdRepartidor { get; set; }

    /// <summary>La fecha del recibo. Requerida.</summary>
    public DateOnly? FechaRecibo { get; set; }

    /// <summary>El numero del recibo. Requerido.</summary>
    public string NumeroRecibo { get; set; } = string.Empty;

    /// <summary>Kilometraje al cargar.</summary>
    public int KmInicial { get; set; }

    /// <summary>Kilometraje de la carga anterior o de la siguiente, segun la pantalla.</summary>
    public int KmFinal { get; set; }

    /// <summary>El precio por litro del dia.</summary>
    public decimal PrecioLitro { get; set; }

    /// <summary>El total del recibo.</summary>
    public decimal TotalRecibo { get; set; }
}

/// <summary>Modificacion de un recibo de gasolina.</summary>
public class ActualizarGasolinaRequest : CrearGasolinaRequest
{
}

/// <summary>
/// Cancelacion de uno o varios recibos.
///
/// El SP recibe DOS listas paralelas: los ids y los numeros de recibo. Las
/// recorre a la vez para dejar el numero de recibo en la bitacora de
/// cancelacion.
///
/// Ojo con los numeros: la columna es varchar(100) y el parametro del SP
/// tambien, pero sp_n_CancelarGasolina los mete en una tabla temporal declarada
/// `#TableNumeroRecibo ( NumeroRecibo int )`, asi que solo admite digitos.
/// </summary>
public class CancelarGasolinasRequest
{
    /// <summary>Los recibos a cancelar. Requerido, al menos uno.</summary>
    public List<int> IdsGasolina { get; set; } = new();

    /// <summary>El numero de cada recibo. Mismo largo que la lista de ids, y solo digitos.</summary>
    public List<string> NumerosRecibo { get; set; } = new();
}

/*
  LO QUE CONTESTA LA CANCELACION: UN RENGLON POR RECIBO.

  sp_n_CancelarGasolina NO contesta con el (result, mensaje) del resto de la
  familia: arma una tabla temporal #GasolinaResultado(IDGasolina, NumeroRecibo,
  resultado) y la devuelve, con un renglon por recibo y su texto
  ("EL RECIBO 99990003 HA SIDO CANCELADO."). Comprobado contra Produccion_svr.

  Si se leyera como los demas —buscando una columna `result`— se perderia
  exactamente lo que importa en una cancelacion de varios: CUALES se cancelaron
  y cuales no. Legacy devolvia esa tabla, pero como `List<DataRow>` crudos, asi
  que por el cable salian las propiedades del objeto (RowError, RowState, Table,
  ItemArray, HasErrors) y el cliente tenia que leer los valores por posicion
  dentro de ItemArray.

  Aqui sale la tabla, un renglon por recibo, con sus nombres.
*/

/// <summary>Lo que contesta la cancelacion de recibos de gasolina.</summary>
public class ResultadoCancelacionGasolinasDto
{
    /// <summary>
    /// Un renglon por recibo pedido, tal como lo reporta el procedimiento:
    /// `idGasolina`, `numeroRecibo` y `resultado` con el texto para el usuario.
    /// </summary>
    public List<Dictionary<string, object?>> Recibos { get; set; } = new();

    /// <summary>Cuantos renglones reporto el procedimiento.</summary>
    public int Total => Recibos.Count;
}
