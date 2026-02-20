namespace ISL_Service.Application.DTOs.Requests;

public class PermisosWebUpdateRolePermissionsRequest
{
    public string RoleCode { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}
