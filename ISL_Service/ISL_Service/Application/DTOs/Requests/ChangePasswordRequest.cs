using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class ChangePasswordRequest
{
    [Required, StringLength(100, MinimumLength = 8)]
    public string ContrasenaActual { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string NuevaContrasena { get; set; } = default!;
}
