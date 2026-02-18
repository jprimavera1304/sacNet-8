namespace ISL_Service.Application.DTOs.Responses;

public class PermisosWebSyncCatalogResponse
{
    public int TotalSeeds { get; set; }
    public int InsertedCount { get; set; }
    public List<string> InsertedPermissions { get; set; } = new();
    public List<string> SkippedPermissions { get; set; } = new();
}
