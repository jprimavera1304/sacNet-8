namespace ISL_Service.Application.DTOs.VentasDevolucion;

/*
  DEVOLUCION DE UNA REMISION

  Todo lo que hay aqui sale de Legacy/Mac31/Mac31/Forms/Ventas/VentasDevolucion.cs
  y de los procedimientos que ese formulario llama por debajo (a traves de
  Legacy/MacServicios2, repositorios VentasDetalle, VentasUsadosCargo,
  VentasUsadosCredito y VentasDevolucion):

    sp_n_ConsultaVentas        el encabezado y los totales de dinero
    sp_n_VentasDetalle         las partidas que se pueden devolver
    sp_n_VentasUsadosCargo     CASCOS REMISION
    sp_n_VentasUsadosCredito   CASCOS ENTREGADOS  (@Accion = 2)
    sp_n_DevolucionVenta       la devolucion, que es quien decide de verdad

  NINGUN NUMERO SE CALCULA AQUI

  La tentacion es obvia —restar cantidad por precio y enseñar el nuevo total—
  y es justo lo que no se debe hacer: el importe de una partida depende de
  redondeos, bonificaciones, IVA por renglon y del esquema de pagos, y eso solo
  lo sabe el SP. Un total calculado en C# que no cuadre con el papel de Mac31 es
  peor que no enseñar total. Aqui se transportan las cifras que devuelven los
  procedimientos, con el mismo nombre y sin tocarlas.

  LAS DOS EMPRESAS

  Tauro y Zaragoza corren el MISMO sp_n_DevolucionVenta: el texto del
  procedimiento es identico en las dos bases (se comparo Produccion_svr contra
  MacZ y solo cambian SET ANSI_WARNINGS/ANSI_NULLS). Lo que cambia es que el
  propio SP se bifurca por Constantes.Funcionalidad ('TAU' / 'ZARA'):

    ZARA  valida Abonos > TotalImporteConIvaRnd  (DEVOLUCION 002) y ademas que
          los cascos entregados no superen el nuevo cargo de cascos (003).
    TAU   valida Abonos > TotalPagar             (DEVOLUCION 004).

  Y sp_n_ConsultaVentas SI difiere entre bases, pero NO donde parecia. El bloque
  que llena msgErr ("EL SALDO DE DINERO/CASCOS ... ES INCORRECTO") vive dentro de
  un IF @Funcionalidad = 'ZARA' en LAS DOS bases, y ademas solo se recorre cuando
  se consulta por la cadena @IDsVenta, no por @IDVenta. Comprobado ejecutando los
  dos caminos en las dos bases:

    Tauro (Funcionalidad 'TAU')  -> el bloque nunca entra: msgErr siempre vacio.
    Zaragoza ('ZARA') + @IDsVenta -> msgErr = 'EL SALDO DE DINERO -0.20 ...'
    Cualquiera de las dos + @IDVenta -> msgErr vacio.

  Y la linea del SALDO DE CASCOS esta comentada a proposito en MacZ, con su nota:
  el saldo de cascos SI puede quedar negativo cuando ya se pagaron todos los
  cascos y ademas hubo un pago por diferencia.

  O sea que el lblErrMsg de Mac31 esta MUERTO en esta pantalla, en las dos
  empresas: legacy consulta por @IDVenta (VentasDevolucion.cs:110). Se sigue
  transportando el campo porque es lo que hace legacy y porque si algun dia esa
  consulta cambia de camino el aviso tiene que aparecer — pero hoy no es la razon
  por la que una devolucion se bloquea.
*/

/// <summary>Una partida de la remision, como la pinta gridDetalles.</summary>
public class VentasDevolucionPartida
{
    public int IdVentaDetalle { get; set; }
    public int IdProducto { get; set; }
    public int IdAlmacen { get; set; }
    public int IdTipoUsado { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;

    public decimal PrecioLista { get; set; }
    public decimal MontoDescuento { get; set; }
    public decimal ImportePartida { get; set; }

    /*
      OJO: CANTIDAD ES EN PIEZAS, CANTIDADMOSTRAR ES EN LA UNIDAD DE VENTA.

      Mac31 guarda las dos y las usa para cosas distintas (VentasDevolucion.cs:
      566 y 575): `numPiezas` es cuantas piezas trae una caja/paquete, y lo que
      se teclea son CAJAS — la cantidad que viaja al SP es tecleado * numPiezas.
      Confundirlas devuelve doce veces menos producto del que se pidio.
    */
    public int Cantidad { get; set; }
    public string CantidadMostrarFtm { get; set; } = string.Empty;
    public int NumPiezas { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
}

/// <summary>Un renglon de cascos, de cargo o de credito.</summary>
public class VentasDevolucionCasco
{
    public string Tipo { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public int Cantidad { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>
/// El pie de un bloque de cascos: importe, IVA, total y piezas. Son los cuatro
/// numeros que Mac31 pone bajo cada rejilla (lblUsadosCargo* / lblUsadosCredito*).
/// </summary>
public class VentasDevolucionCascosResumen
{
    public decimal Importe { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public int Piezas { get; set; }

    public List<VentasDevolucionCasco> Renglones { get; set; } = new();
}

/*
  LOS TOTALES DE DINERO, EN EL MISMO ORDEN Y CON LA MISMA CUENTA QUE MAC31

  De VentasDevolucion.cs:139-153. Dos de ellos NO son columnas: son sumas que
  hace el formulario y que hay que repetir exactamente igual o el web enseña un
  numero distinto al de la pantalla de al lado.

    Creditos    = Creditos + IvaCreditos          (linea 143)
    TotalPagar  = TotalPagar + Cargos             (linea 145)

  El resto viaja tal cual sale de sp_n_ConsultaVentas.
*/
public class VentasDevolucionTotales
{
    public decimal Importe { get; set; }
    public decimal Cargos { get; set; }
    public decimal Creditos { get; set; }
    public decimal TotalPagar { get; set; }
    public decimal Abonos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Saldo { get; set; }
}

/// <summary>Todo lo que la pantalla necesita para abrirse, en una sola ida.</summary>
public class VentasDevolucionVistaResponse
{
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Agente { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;

    /*
      'TAU' o 'ZARA', de Constantes. No se manda para que el front decida
      reglas —las decide el SP— sino para poder DECIRLE a quien mira por que un
      mensaje habla de cascos entregados y otro de pagos.
    */
    public string Funcionalidad { get; set; } = string.Empty;

    /*
      Las dos puertas de ConsultarVentas.cs:3610 y 3619: Mac31 ni siquiera abre
      esta pantalla si la remision esta cancelada o ya esta pagada. Viajan para
      que la pantalla pueda decirlo con el folio en la mano, y se vuelven a
      mirar al guardar.
    */
    public bool Cancelada { get; set; }
    public bool Pagada { get; set; }

    public VentasDevolucionTotales Totales { get; set; } = new();
    public List<VentasDevolucionPartida> Partidas { get; set; } = new();
    public VentasDevolucionCascosResumen CascosRemision { get; set; } = new();
    public VentasDevolucionCascosResumen CascosEntregados { get; set; } = new();

    /*
      El msgErr de sp_n_ConsultaVentas. Cuando trae texto, Mac31 lo pinta en
      rojo y ESCONDE el boton de guardar (VentasDevolucion.cs:157-162). Aqui el
      boton no se esconde —los botones no se apagan— pero el motivo es el mismo
      y el texto es el suyo.
    */
    public string MsgErr { get; set; } = string.Empty;

    /*
      La otra razon por la que Mac31 no deja devolver: los cascos ya se
      entregaron (VentasDevolucion.cs:83-96). La condicion es
      CreditoTotal > 0 && CreditoTotal >= CargoTotal, y el texto es el suyo.
      Se calcula en el servidor para que las dos mitades no puedan opinar
      distinto.
    */
    public bool CascosEntregadosBloquean { get; set; }
    public string MotivoBloqueo { get; set; } = string.Empty;

    /// Vacio si todo bien. Si no, por que no se pudo ni abrir la pantalla.
    public string Error { get; set; } = string.Empty;
}

/// <summary>Una linea de lo que se quiere devolver.</summary>
public class VentasDevolucionLinea
{
    public int IdVentaDetalle { get; set; }
    /// EN PIEZAS, ya multiplicada por numPiezas. Ver el comentario de Partida.
    public int Cantidad { get; set; }
}

public class VentasDevolucionRequest
{
    public int IdVenta { get; set; }
    public List<VentasDevolucionLinea> Lineas { get; set; } = new();
}

/*
  LO QUE CONTESTA sp_n_DevolucionVenta: result, mensaje, IDVenta, Folio.

  result = 1 y mensaje vacio es el unico caso bueno. Cualquier otra cosa es un
  "no" de negocio —no una excepcion— y el mensaje ES el dato: son los textos
  "NO SE PUEDE REALIZAR LA DEVOLUCION 002/003/004" con las cifras que no
  cuadran. Mac31 los enseña tal cual cambiando '@@@@' por saltos de linea
  (VentasDevolucion.cs:733); aqui se hace lo mismo.
*/
public class VentasDevolucionResponse
{
    public bool Ok { get; set; }
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
