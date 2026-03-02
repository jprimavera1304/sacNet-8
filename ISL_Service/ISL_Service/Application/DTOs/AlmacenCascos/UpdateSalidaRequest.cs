namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Request para actualizar una SALIDA existente.
/// </summary>
public class UpdateSalidaRequest
{
    public int IdRepartidorEntrega { get; set; }
    public string? Observaciones { get; set; }
    public List<SalidaTarimaDto> Tarimas { get; set; } = new();
}
