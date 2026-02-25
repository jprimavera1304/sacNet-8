namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Request para actualizar una SALIDA existente.
/// </summary>
public class UpdateSalidaRequest
{
    public int IdRepartidorEntrega { get; set; }
    public int IdTarima { get; set; }
    public int NumeroTarima { get; set; }
    public int Piezas { get; set; }
    public string? Observaciones { get; set; }
}
