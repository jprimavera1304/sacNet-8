using ISL_Service.Application.Models;
using ISL_Service.Application.DTOs.Responses;

namespace ISL_Service.Application.Interfaces;

public interface IPermissionService
{
    Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct);
    bool IsAllowedByLegacy(string rolLegacy, string permission);
    Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct);
    Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct);
    Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct);
    Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int empresaId, string companyKey, bool includeAllTenants, CancellationToken ct);
    Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int empresaId, CancellationToken ct);
    Task<PermisosWebModuleItem> SetModuleStatusAsync(int empresaId, string moduleKey, int idStatus, CancellationToken ct);
    Task<PermisosWebRoleItem> CreateRoleAsync(int empresaId, string roleCode, string name, CancellationToken ct);
    Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct);
    Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct);
    Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct);
    Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct);
}
