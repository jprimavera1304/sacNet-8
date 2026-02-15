using System;

namespace ISL_Service.Application.DTOs.Responses;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int EmpresaId { get; set; }
    public string CompanyKey { get; set; } = string.Empty;
    public bool DebeCambiarContrasena { get; set; }
}
