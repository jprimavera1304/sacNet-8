namespace ISL_Service.Application.DTOs.Responses;

public class PermisosWebBootstrapResponse
{
    public bool PermissionsEnabled { get; set; }
    public List<PermisosWebRoleItem> Roles { get; set; } = new();
    public List<PermisosWebPermissionItem> Permissions { get; set; } = new();
    public List<PermisosWebRolePermissionItem> RolePermissions { get; set; } = new();
    public List<PermisosWebUserItem> Users { get; set; } = new();
    public List<PermisosWebUserOverrideItem> UserOverrides { get; set; } = new();
}

public class PermisosWebRolesBootstrapResponse
{
    public bool PermissionsEnabled { get; set; }
    public List<PermisosWebRoleItem> Roles { get; set; } = new();
    public List<PermisosWebPermissionItem> Permissions { get; set; } = new();
    public List<PermisosWebRolePermissionItem> RolePermissions { get; set; } = new();
}

public class PermisosWebRoleItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PermisosWebPermissionItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModuleKey { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? LegacyFormId { get; set; }
    public int? LegacyProcessId { get; set; }
    public bool IsLegacyReport { get; set; }
}

public class PermisosWebModuleItem
{
    public string ModuloClave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

public class PermisosWebRolePermissionItem
{
    public string RoleCode { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

public class PermisosWebUserItem
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RoleLegacy { get; set; } = string.Empty;
}

public class PermisosWebUserOverrideItem
{
    public Guid UserId { get; set; }
    public List<string> Allow { get; set; } = new();
    public List<string> Deny { get; set; } = new();
}
