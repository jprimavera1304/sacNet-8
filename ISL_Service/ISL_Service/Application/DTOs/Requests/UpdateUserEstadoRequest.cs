using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserEstadoRequest
{
    // 1 = Activo, 2 = Inactivo, 3 = Bloqueado
    [Required]
    [Range(1, 3)]
    public int Estado { get; set; }
}
