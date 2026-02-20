namespace ISL_Service.Application.DTOs.Responses;

public class CapabilitiesResponse
{
    public Guid UserId { get; set; }
    public int EmpresaId { get; set; }
    public string RolLegacy { get; set; } = string.Empty;
    public bool PermissionsEnabled { get; set; }
    public List<string> Permissions { get; set; } = new();
    public string PermissionsVersion { get; set; } = string.Empty;
}
