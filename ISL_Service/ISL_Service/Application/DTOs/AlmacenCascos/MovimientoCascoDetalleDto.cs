namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Línea de detalle de un movimiento (tarima + piezas).
/// </summary>
public class MovimientoCascoDetalleDto
{
    public int IdDetalle { get; set; }
    public int IdMovimiento { get; set; }
    public int? IdTarima { get; set; }
    public int IdTipoCasco { get; set; }
    public int NumeroTarima { get; set; }
    public int Piezas { get; set; }
    /// <summary>Nombre de la tarima (desde WTarima).</summary>
    public string? NombreTarima { get; set; }
    /// <summary>Descripción del tipo de casco (desde [Catalogo TiposUsados]).</summary>
    public string? TipoCascoDescripcion { get; set; }
}
