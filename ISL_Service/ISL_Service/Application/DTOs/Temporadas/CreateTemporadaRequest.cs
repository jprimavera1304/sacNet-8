namespace ISL_Service.Application.DTOs.Temporadas;

public class CreateTemporadaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
