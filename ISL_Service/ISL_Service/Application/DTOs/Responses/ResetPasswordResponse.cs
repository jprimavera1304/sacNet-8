namespace ISL_Service.Application.DTOs.Responses;

public class ResetPasswordResponse
{
    public Guid UserId { get; set; }
    public string PasswordTemporal { get; set; } = default!;
    public bool DebeCambiarContrasena { get; set; }
}
