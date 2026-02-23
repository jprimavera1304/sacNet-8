using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Tarima;

/// <summary>
/// Request para cambiar estatus de una tarima. IdStatus: 1 = Activo, 2 = Cancelado.
/// </summary>
public class UpdateTarimaStatusRequest
{
    [Required]
    [Range(1, 2, ErrorMessage = "idStatus debe ser 1 (Activo) o 2 (Cancelado)")]
    public int IdStatus { get; set; }
}
