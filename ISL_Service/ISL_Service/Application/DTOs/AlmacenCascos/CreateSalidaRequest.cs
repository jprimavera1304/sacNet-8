namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Request para crear una SALIDA (cabecera + detalle).
/// </summary>
public class CreateSalidaRequest
{
    public int IdRepartidorEntrega { get; set; }
    public string? Observaciones { get; set; }
    public List<SalidaDetalleItemDto> Detalle { get; set; } = new();
}
