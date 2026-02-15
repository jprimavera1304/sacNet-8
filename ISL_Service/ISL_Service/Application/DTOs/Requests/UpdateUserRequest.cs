using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserRequest
{
    [Required, StringLength(60, MinimumLength = 4)]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Usuario invalido. Usa A-Z, 0-9 y _")]
    public string Usuario { get; set; } = default!;

    [Required, StringLength(30)]
    public string Rol { get; set; } = default!;
}
