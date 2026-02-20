namespace ISL_Service.Application.Models;

public class WebLoginFallbackResult
{
    public int ResultCode { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public int LegacyUserId { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string ContrasenaHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "User";
    public int EmpresaId { get; set; }
    public bool DebeCambiarContrasena { get; set; }
    public int Estado { get; set; }
}
