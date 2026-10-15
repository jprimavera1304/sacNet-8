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
    /*
      sincronizarConLegacy: si es true, al terminar se escribe de vuelta en
      Mac31 ([Usuario Forma Procesos]) el estado efectivo del web.

      Tiene que ser EXPLICITO y no deducirse de las listas. El write-back
      calcula el efectivo SOLO del web (rol + overrides, sin lo heredado de
      legacy) y apaga lo que no aparezca ahi. Por eso:

        - La pantalla de permisos pasa true: el administrador acaba de decidir
          el estado completo y una clave desmarcada SI debe revocarse en Mac31.
        - El alta de usuarios pasa false: ahi las listas vienen vacias solo
          para limpiar overrides residuales de un usuario web recien nacido.
          Con true, a alguien que ya existia en Mac31 se le apagaban TODOS sus
          permisos del escritorio, en silencio y con HTTP 200.

      No vale el atajo "si allow y deny vienen vacias no escribas": un usuario
      cuyos permisos vengan solo de Mac31 y al que el administrador desmarque
      todo produce exactamente esas dos listas vacias, y ahi si hay que revocar.
    */
    Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, bool sincronizarConLegacy, CancellationToken ct);
    /// <summary>Siembra la lista estatica Y las familias que salen de la base (centros, reportes).</summary>
    Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct);

    Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct);
    Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct);

    /*
      LO QUE NECESITA EL GUARDIA (ver IGuardiaDePermisos).

      Son LECTURAS, y viven aqui y no en una clase nueva porque tienen que
      partir las tablas igual que GetPermissionsAsync: los nombres de columna
      se descubren en tiempo de ejecucion y las dos bases de produccion no
      nacieron iguales. Ver el porque largo en PermissionService.Gobierno.cs.
    */

    /// <summary>Las claves que ese rol tiene GUARDADAS hoy (no las efectivas).</summary>
    Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(int empresaId, string roleCode, CancellationToken ct);

    /// <summary>Los ajustes individuales guardados de ese usuario.</summary>
    Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(int empresaId, Guid userId, CancellationToken ct);

    /// <summary>Quien seguiria pudiendo administrar permisos si el cambio simulado se guardara.</summary>
    Task<AdministracionRestante> GetAdministracionRestanteAsync(int empresaId, SimulacionDePermisos simulacion, CancellationToken ct);

    /// <summary>Si ese rol viene con la instalacion (WRol.EsSistema = 1).</summary>
    Task<bool> EsRolDeSistemaAsync(int empresaId, string roleCode, CancellationToken ct);
}
