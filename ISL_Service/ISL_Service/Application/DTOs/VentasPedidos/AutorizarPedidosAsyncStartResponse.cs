namespace ISL_Service.Application.DTOs.VentasPedidos;

public class AutorizarPedidosAsyncStartResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int TotalPedidos { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
