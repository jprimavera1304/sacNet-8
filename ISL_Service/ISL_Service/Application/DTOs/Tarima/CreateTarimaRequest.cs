using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Tarima;

/// <summary>
/// Request para crear una tarima. Usuario se obtiene del token (no enviar en body).
/// </summary>
public class CreateTarimaRequest
{
    [Required(ErrorMessage = "nombreTarima es requerido")]
    [StringLength(150)]
    public string NombreTarima { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "idTipoCasco debe ser mayor a 0")]
    public int IdTipoCasco { get; set; }

    [Required]
    [Range(1, 99999, ErrorMessage = "numeroCascosBase debe estar entre 1 y 99999")]
    public int NumeroCascosBase { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }
}
