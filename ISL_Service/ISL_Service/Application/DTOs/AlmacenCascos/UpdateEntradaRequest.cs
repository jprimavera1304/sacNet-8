namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Request para actualizar una ENTRADA existente.
/// </summary>
public class UpdateEntradaRequest
{
    public int IdRepartidorRecibe { get; set; }
    public decimal Kilos { get; set; }
    public string? Observaciones { get; set; }
}
