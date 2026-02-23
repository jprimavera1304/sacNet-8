namespace ISL_Service.Application.DTOs.Tarima;

/// <summary>
/// DTO de lectura para una tarima.
/// IdStatus: 1 = Activo, 2 = Cancelado.
/// </summary>
public class TarimaDto
{
    public int IdTarima { get; set; }
    public string NombreTarima { get; set; } = string.Empty;
    public int IdTipoCasco { get; set; }
    public int NumeroCascosBase { get; set; }
    public string? Observaciones { get; set; }
    /// <summary>1 = Activo, 2 = Cancelado</summary>
    public int IdStatus { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
