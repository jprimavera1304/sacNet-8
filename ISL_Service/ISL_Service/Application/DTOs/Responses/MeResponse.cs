using System;

namespace ISL_Service.Application.DTOs.Responses;

public class MeResponse
{
    public Guid UserId { get; set; }
    public string RolLegacy { get; set; } = string.Empty;
    public bool PermissionsEnabled { get; set; }
    public List<string> Permissions { get; set; } = new();
    public string PermissionsVersion { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int EmpresaId { get; set; }
    public string CompanyKey { get; set; } = string.Empty;
    public bool DebeCambiarContrasena { get; set; }
}
