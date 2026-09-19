namespace ISL_Service.Application.DTOs.VentasSaldos;

/*
  SALDOS DEL CLIENTE

  Es la rejilla de abajo a la derecha de Mac31: lo que el cliente de la remision
  seleccionada DEBE, con sus dias vencidos. Se mira antes de venderle otra vez.

  SALE DEL MISMO PROCEDIMIENTO QUE MAC31 (sp_n_rptVentasSaldos) y con los mismos
  parametros: el cliente, la fecha de operacion y estatus 4 = pendientes y
  vencidas. No se filtra por el periodo que se este consultando arriba, y ESO ES
  EL PUNTO: lo que el cliente debe de hace seis meses es justo el dato por el que
  se abre este panel. Una version que solo mirara el periodo en pantalla estaria
  callando lo unico importante.
*/
public class VentasSaldosRequest
{
    /// El IDCliente interno, el que viene en el renglon de la consulta. No es el
    /// "Numero" que se le enseña a la persona.
    public int IdCliente { get; set; }
}

public class VentasSaldoRenglon
{
    public string Numero { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Cargos { get; set; }
    public decimal Abonos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Saldo { get; set; }
    /// "PENDIENTE" / "VENCIDA". Es la columna que Mac31 usa para pintar el
    /// renglon de rojo o de azul, y la que aqui decide el color tambien.
    public string EstatusMostrar { get; set; } = string.Empty;
    /// Dias vencidos. Es la columna VENC de Mac31.
    public int DiasVencimiento { get; set; }
}

public class VentasSaldosResponse
{
    public List<VentasSaldoRenglon> Renglones { get; set; } = new();
    public decimal TotalSaldo { get; set; }
    /// El vencimiento mas alto de todos. Es lo que de verdad decide si se le
    /// vende o no, y en la rejilla hay que buscarlo renglon por renglon.
    public int MaximoVencimiento { get; set; }
}
