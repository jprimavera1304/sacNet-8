namespace ISL_Service.Application.DTOs.Requests;

public class RegisterRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string Rol { get; set; } = "User";
}
