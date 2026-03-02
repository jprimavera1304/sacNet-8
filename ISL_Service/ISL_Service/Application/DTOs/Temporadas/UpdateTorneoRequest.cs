namespace ISL_Service.Application.DTOs.Temporadas;

public class UpdateTorneoRequest
{
    public Guid TemporadaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Clave { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
