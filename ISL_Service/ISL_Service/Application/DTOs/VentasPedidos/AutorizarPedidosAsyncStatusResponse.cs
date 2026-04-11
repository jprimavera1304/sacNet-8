namespace ISL_Service.Application.DTOs.VentasPedidos;

public class AutorizarPedidosAsyncStatusResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string Message { get; set; } = string.Empty;
    public int TotalPedidos { get; set; }
    public int CompletedPedidos { get; set; }
    public int FailedPedidos { get; set; }
    public string? IdsVenta { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public List<PedidoResultadoDto> Pedidos { get; set; } = new();
}
