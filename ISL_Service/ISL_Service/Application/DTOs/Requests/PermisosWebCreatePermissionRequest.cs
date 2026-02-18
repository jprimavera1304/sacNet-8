namespace ISL_Service.Application.DTOs.Requests;

public class PermisosWebCreatePermissionRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
