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
      sincronizarConLegacy: si es true, al terminar se ENCIENDE en Mac31
      ([Usuario Forma Procesos]) lo que el web conceda y que alla este apagado.

      SOLO OTORGA. NUNCA APAGA, por ningun camino: eso es lo que paso el
      2026-08-26 (44 permisos de JANET) y otra vez el 2026-11-04 (46 permisos
      de JESY, ALVARELYS y MARINA BERNAL), las dos veces porque el write-back
      calcula el efectivo SOLO del web —rol + overrides, sin lo heredado de
      Mac31— y trataba la ausencia como una orden de apagar. El detalle entero
      esta en PermissionService.EstadoDeseadoEnLegacy.

      QUITAR una capacidad se hace donde vive: en Mac31 (su propia pantalla, o
      PUT /api/usuarios-mac/{id}/procesos, que es su replica y donde el
      administrador manda IDStatus renglon por renglon) o, si es solo para esta
      plataforma, en las tablas del web — ver la copia de permisos
      (ICopiaDeMac31Service), que existe exactamente para eso.

      Sigue siendo EXPLICITO y no se deduce de las listas:

        - La pantalla de permisos pasa true: lo que el administrador acaba de
          conceder tiene que llegar al escritorio.
        - El alta de usuarios pasa false: ahi las listas vienen vacias solo
          para limpiar overrides residuales de un usuario web recien nacido, y
          no hay nada que conceder.
    */
    Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, bool sincronizarConLegacy, CancellationToken ct);
    /// <summary>Siembra la lista estatica Y las familias que salen de la base (centros, reportes).</summary>
    Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct);

    /*
      TIRAR LA CACHE DE PERMISOS A MANO, SIN REINICIAR NADA.

      Existen porque la invalidacion vivia SOLO dentro de
      UpsertUserOverridesAsync —el boton Guardar de la pantalla— y quien
      concede por base se la salta. El 2026-11-04 eso obligo a reiniciar el
      pool de IIS de produccion cuatro veces en una tarde, para arreglarle un
      permiso a una persona.

      Son SINCRONAS a proposito: no hablan con la base, solo cancelan los
      tokens de expiracion de la IMemoryCache. No hay nada que esperar.

      LO QUE ALCANZAN: la memoria de ESTE proceso. Con un pool de un solo
      proceso —como esta hoy produccion— eso es todo el backend. El dia que
      haya varios, el latido de PermissionService.CacheAlDia.cs es el que cubre
      a los demas, porque cada proceso lo mide por su cuenta.
    */
    void InvalidarCacheDeUsuario(int empresaId, Guid userId);
    void InvalidarCacheDeEmpresa(int empresaId);

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
