using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class CreateUserRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Usuario { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string PasswordTemporal { get; set; } = default!;

    [Required, StringLength(50)]
    public string Rol { get; set; } = default!; // "SuperAdmin" o "Admin"

    [Required]
    public int EmpresaId { get; set; }
}
