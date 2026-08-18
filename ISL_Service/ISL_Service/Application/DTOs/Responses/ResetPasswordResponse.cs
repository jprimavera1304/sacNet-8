namespace ISL_Service.Application.DTOs.Responses;

public class ResetPasswordResponse
{
    public Guid UserId { get; set; }

    /// <summary>Contrasena con la que quedo el usuario despues del cambio.</summary>
    public string Password { get; set; } = default!;

    /// <summary>Alias del nombre viejo, para no romper a quien todavia lo lee.</summary>
    public string PasswordTemporal => Password;

    /// <summary>Siempre false: ya no existe el cambio obligatorio al primer ingreso.</summary>
    public bool DebeCambiarContrasena { get; set; }
}
