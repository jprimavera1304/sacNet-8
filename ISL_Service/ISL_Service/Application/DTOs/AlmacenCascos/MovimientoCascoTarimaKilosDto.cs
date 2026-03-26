namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Kilos capturados por tarima para un movimiento de entrada.
/// </summary>
public class MovimientoCascoTarimaKilosDto
{
    public int IdTarimaKilo { get; set; }
    public int IdMovimiento { get; set; }
    public int NumeroTarima { get; set; }
    public decimal Kilos { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
