namespace ISL_Service.Application.DTOs.VentasPedidos;

public class AutorizarPedidosRequest
{
    public List<int> IdsPedido { get; set; } = new();

    public int? IdUsuario { get; set; }
    public int? IdUsuarioProcesar { get; set; }
    public int? IdUsuarioAutorizar { get; set; }
    public string? Equipo { get; set; }
    public string? EquipoProcesar { get; set; }
    public string? EquipoAutorizar { get; set; }

    public int? IDTipoPagoCS { get; set; }
    public int? IDBancoTransferCS { get; set; }
    public string? TransferenciaCS { get; set; }
    public int? IDBancoTarjetaCS { get; set; }
    public string? TarjetaCS { get; set; }
    public int? IDBancoDepositoEfeCS { get; set; }
    public string? DepositoEfectivoNumeroCS { get; set; }
    public decimal? MontoTotalCS { get; set; }
    public int? TipoTarjeta { get; set; }
}
