namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Un renglon TAL COMO SALE de sp_n_ConsultaVentasPagos, sin interpretar.
///
/// Se separa del DTO que ve el navegador a proposito: el procedimiento devuelve
/// mas de cien columnas y con nombres de legacy ("Abono2", "DiffUsadosTexto",
/// "Cancelado" como texto "SI"/"NO"). Traducir aqui y decidir en el servicio
/// deja las REGLAS en un solo archivo, en vez de repartidas entre el SQL y el
/// mapeo.
/// </summary>
public class VentasPagoCrudo
{
    public int IdPagoVenta { get; set; }
    public int IdVenta { get; set; }
    public int IdTipoPago { get; set; }

    public string FolioFtm { get; set; } = string.Empty;
    public int NumeroCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string NumeroNombreAgente { get; set; } = string.Empty;
    public string FechaVencimientoFtm { get; set; } = string.Empty;

    /// <summary>Fecha de cancelacion de la VENTA (no la del pago). Vacia cuando
    /// la remision sigue viva.</summary>
    public string FechaCancelacionFtm { get; set; } = string.Empty;

    public string FechaPagoFtm { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;
    public string NumeroPago { get; set; } = string.Empty;
    public string Movimiento { get; set; } = string.Empty;
    public string TipoPagoCorto { get; set; } = string.Empty;

    public decimal Cargo { get; set; }
    public decimal Abono { get; set; }
    public decimal SaldoAcumulado { get; set; }

    public string DiffUsadosTexto { get; set; } = string.Empty;
    public string AgenteLiquidacion { get; set; } = string.Empty;
    public string RepartidorLiquidacion { get; set; } = string.Empty;
    public string DetallePago { get; set; } = string.Empty;

    /// <summary>"SI" / "NO", tal como lo devuelve el procedimiento.</summary>
    public string Cancelado { get; set; } = "NO";
    public string FechaPagoCancelacionFtm { get; set; } = string.Empty;

    /* Los totales. Vienen repetidos en TODOS los renglones; Mac31 usa los del
       renglon 0 (ConsultarVentasPagos.cs, lineas 84-90). */
    public decimal TotalPagar { get; set; }
    public decimal Cargos { get; set; }
    public decimal Abonos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Saldo { get; set; }

    public int PagaConEfectivo { get; set; }
    public int PagaConCheque { get; set; }
    public int PagaConTransferencia { get; set; }
    public int PagaConDepositoEfectivo { get; set; }
}

/// <summary>Lo que dice sp_n_CancelarVentasPagos: 1 y sin mensaje, o -1 con el
/// motivo escrito por legacy.</summary>
public class VentasPagoCancelacionCrudo
{
    public int Result { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public interface IVentasPagosRepository
{
    /// <summary>Corre sp_n_ConsultaVentasPagos con los mismos parametros que
    /// Mac31 (solo el IDVenta; el resto en cero).</summary>
    Task<List<VentasPagoCrudo>> ConsultarAsync(int idVenta, CancellationToken ct);

    /// <summary>Corre sp_n_CancelarVentasPagos. Escribe: es el mismo
    /// procedimiento que usa Mac31 y hace su propia transaccion.</summary>
    Task<VentasPagoCancelacionCrudo> CancelarAsync(int idPagoVenta, int idVenta, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>"TAU" o "ZARA", de la tabla Constantes. Es lo unico que cambia
    /// entre las dos empresas en esta pantalla.</summary>
    Task<string> EsquemaPagoAsync(CancellationToken ct);
}
