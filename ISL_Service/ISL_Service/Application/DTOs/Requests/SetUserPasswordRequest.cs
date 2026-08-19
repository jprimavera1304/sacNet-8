using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

/// <summary>
/// Cambio de contrasena hecho por un administrador: el la escribe, no se genera sola.
/// </summary>
public class SetUserPasswordRequest
{
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    /// <summary>Nombre viejo del campo; significa lo mismo que Password.</summary>
    [StringLength(100, MinimumLength = 8)]
    public string? PasswordTemporal { get; set; }

    public string ResolverPassword()
        => !string.IsNullOrWhiteSpace(Password) ? Password! : (PasswordTemporal ?? string.Empty);
}
