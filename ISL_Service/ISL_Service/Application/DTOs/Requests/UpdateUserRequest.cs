using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserRequest
{
    [Required, StringLength(60, MinimumLength = 4)]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Usuario inválido. Usa A-Z, 0-9 y _")]
    public string Usuario { get; set; } = default!;

    [Required, StringLength(30)]
    public string Rol { get; set; } = default!;

    /// <summary>
    /// Opcional. Si viene, se cambia la contrasena en los dos lados (web y legacy).
    /// Vacia o ausente significa "dejala como esta": editar el rol de alguien no
    /// tiene por que tumbarle la contrasena.
    /// </summary>
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }
}
