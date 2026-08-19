namespace ISL_Service.Application.DTOs.Responses;

public class CreateUserResponse
{
    public UserResponse User { get; set; } = default!;

    /// <summary>Contrasena con la que quedo el usuario (la que mando el administrador).</summary>
    public string Password { get; set; } = default!;

    /// <summary>Alias del nombre viejo, para no romper a quien todavia lo lee.</summary>
    public string PasswordTemporal => Password;
}
