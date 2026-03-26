namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Resultado de guardar kilos por tarima.
/// </summary>
public class MovimientoCascoTarimaKilosResultDto
{
    public int IdMovimiento { get; set; }
    public int NumeroTarima { get; set; }
    public decimal Kilos { get; set; }
    public decimal TotalKilos { get; set; }
    public int TotalTarimasConKilos { get; set; }
}
