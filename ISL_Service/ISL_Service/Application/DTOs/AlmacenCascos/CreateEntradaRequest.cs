namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Request para aceptar ENTRADA (desde una salida registrada).
/// </summary>
public class CreateEntradaRequest
{
    public int IdMovimientoSalida { get; set; }
    public int IdRepartidorRecibe { get; set; }
    public decimal Kilos { get; set; }
    public string? Observaciones { get; set; }
    public List<EntradaDetalleItemDto>? Detalle { get; set; }
}
