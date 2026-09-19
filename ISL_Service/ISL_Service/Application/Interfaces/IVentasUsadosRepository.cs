using ISL_Service.Application.DTOs.VentasUsados;

namespace ISL_Service.Application.Interfaces;

/*
  Cuatro lecturas y una escritura, todas contra procedimientos sp_n_ de legacy.
  Ninguno se edita: se llaman igual que los llama MacServicios2 por debajo de
  Mac31 (Legacy/MacServicios2/.../VentasUsadosCargoRepository.cs y
  VentasUsadosCreditoRepository.cs).
*/
public interface IVentasUsadosRepository
{
    /// Constantes de la empresa: tolerancia, iva y cual esquema de pago corre.
    Task<ConstantesUsados> ConsultarConstantesAsync(CancellationToken ct);

    /// sp_n_ConsultaVentas @IDVenta — la cabecera y los totales de la venta.
    Task<VentaDeUsados?> ConsultarVentaAsync(int idVenta, CancellationToken ct);

    /// sp_n_VentasUsadosCargo — los cascos que se le cobran (no editable).
    Task<VentasUsadosBloqueDto> ConsultarCargosAsync(int idVenta, CancellationToken ct);

    /// sp_n_VentasUsadosCredito @Accion=2 — los entregados MAS el catalogo.
    Task<VentasUsadosBloqueDto> ConsultarCreditosAsync(int idVenta, CancellationToken ct);

    /// sp_n_VentasUsadosCredito @Accion=1 — los que se dieron de baja antes.
    Task<List<VentasUsadosAnteriorDto>> ConsultarAnterioresAsync(int idVenta, CancellationToken ct);

    /// sp_n_VentasUsadosCreditoAjuste  o  ...AjusteZara segun Constantes.EsquemaPago.
    Task<VentasUsadosGuardarResultadoDto> AjustarAsync(AjusteDeUsados ajuste, CancellationToken ct);
}

public class ConstantesUsados
{
    public decimal MaximaDiferenciaUsados { get; set; }
    /// En tanto por uno: Constantes.IVA / 100.
    public decimal PorcentajeIva { get; set; }
    /// "TAU" o "ZARA". Decide cual de los dos procedimientos de ajuste corre.
    public string EsquemaPago { get; set; } = string.Empty;
}

public class VentaDeUsados
{
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Agente { get; set; } = string.Empty;
    public string FechaPagoFtm { get; set; } = string.Empty;
    /// "SI" / "NO", tal cual lo devuelve sp_n_ConsultaVentas.
    public string Cancelada { get; set; } = string.Empty;
    /// El folio como se PINTA. Si trae "CV-" no es una remision de venta.
    public string FolioFtm { get; set; } = string.Empty;

    public decimal Importe { get; set; }
    public decimal Cargos { get; set; }
    public decimal Creditos { get; set; }
    public decimal IvaCreditos { get; set; }
    public decimal TotalPagar { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Abonos { get; set; }
    public decimal Saldo { get; set; }
}

/*
  Lo que se le entrega al procedimiento de ajuste. Las cuatro listas van
  ALINEADAS por posicion —el procedimiento las vuelve a juntar por el IDENTITY
  de cada tabla temporal— asi que se arman juntas y nunca por separado.
*/
public class AjusteDeUsados
{
    public int IdVenta { get; set; }
    public int IdUsuario { get; set; }
    public string Equipo { get; set; } = string.Empty;
    public string EsquemaPago { get; set; } = string.Empty;

    public List<int> IdsTipoUsado { get; set; } = new();
    public List<decimal> Costos { get; set; } = new();
    /// La cantidad FINAL de cada tipo.
    public List<int> Cantidades { get; set; } = new();
    /// La cantidad FINAL menos la que habia: lo que se movio, puede ser negativo.
    public List<int> CantidadesNuevo { get; set; } = new();
    public List<decimal> Importes { get; set; } = new();

    public decimal Creditos { get; set; }
    public decimal IvaCreditos { get; set; }
    public decimal TotalPagar { get; set; }
}
