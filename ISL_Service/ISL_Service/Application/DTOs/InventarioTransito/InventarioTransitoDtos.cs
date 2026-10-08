namespace ISL_Service.Application.DTOs.InventarioTransito;

/*
  LA PANTALLA InventarioTransito DE MAC31.

  Lo que lista NO son compras a proveedor: son REMISIONES DE VENTA de la matriz
  hacia los centros de servicio, con el detalle de piezas que todavia no se ha
  aceptado en el centro. Desde el centro es "lo que viene en camino"; desde la
  matriz, "lo que ya salio y nadie ha acusado".

  Un renglon por PIEZA (detalle) o uno por remision (agrupado), segun como se
  pida. En Mac31 son dos llamadas al mismo procedimiento: la rejilla de arriba
  agrupada y la de abajo con el detalle del renglon seleccionado. Aqui es el
  mismo endpoint con `agrupado`.

  FUERA LOS `*Ftm` DE FECHA: FechaEmisionFtm, FechaEmisionCortoFtm y
  FechaAceptoFtm son la misma fecha como texto para la rejilla de WinForms.
  SI se conserva `FolioFtm`, que NO es una fecha: es el folio con su prefijo
  (lo arma fn_DevuelvePrefijoFolio y distingue servicios, aceites, logistica y
  devoluciones). Rearmarlo en el front seria imprimir otro folio.
*/

/// <summary>
/// Un renglon de inventario en transito: la remision, el producto y si ya se
/// acepto en el centro.
/// </summary>
public class InventarioTransitoDto
{
    public int IDVenta { get; set; }

    /// <summary>
    /// El renglon de la venta. Es lo que recibe POST api/inventario/transito para
    /// procesarlo. En el modo agrupado viene en 0: ahi no hay un solo renglon.
    /// </summary>
    public int IDVentaDetalle { get; set; }

    public int IDCliente { get; set; }
    public int Folio { get; set; }
    /// <summary>El folio con su prefijo, ya armado por la base. No es una fecha.</summary>
    public string? FolioFtm { get; set; }
    public DateTime? FechaEmision { get; set; }

    public int NumeroCliente { get; set; }
    public string? NombreCliente { get; set; }

    /// <summary>
    /// Piezas de la remision COMPLETA (Ventas.Productos), no de este renglon. Es
    /// la columna que la pantalla suma para el total de abajo.
    /// </summary>
    public int Productos { get; set; }

    public int IDProducto { get; set; }
    public string? Codigo { get; set; }
    /// <summary>Piezas de ESTE renglon.</summary>
    public int Cantidad { get; set; }

    public int IDUsuarioAcepto { get; set; }
    public string? UsuarioAcepto { get; set; }
    public DateTime? FechaAcepto { get; set; }
    public string? EquipoAcepto { get; set; }

    /// <summary>
    /// "EN TRASITO" o "ACEPTADO". Con la falta de ortografia incluida: es el
    /// literal que escribe el procedimiento y el que Mac31 muestra. Corregirlo
    /// aqui haria que las dos pantallas dijeran cosas distintas.
    /// </summary>
    public string? Estatus { get; set; }

    public int IDGarantia { get; set; }
}
