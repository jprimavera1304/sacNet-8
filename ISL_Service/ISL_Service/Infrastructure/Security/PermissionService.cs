using System.Data;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ISL_Service.Infrastructure.Security;

public sealed class PermissionService : IPermissionService
{
    private const string FeatureKey = "autorizacion.capacidades";
    private const string SchemaCacheKey = "permissions:schema:v2";
    private const string OverrideTypeCacheKey = "permissions:override-type:v1";
    private const string UserPermColumnsCacheKey = "permissions:userperm-columns:v1";
    private static readonly TimeSpan ActiveCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SchemaCacheTtl = TimeSpan.FromMinutes(15);

    private static readonly Dictionary<string, HashSet<string>> LegacyPolicyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["usuarios.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["usuarios.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.estado.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.password.reset"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["usuarios.empresa.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["empresas.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisosweb.bootstrap"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["permisosweb.roles.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisosweb.overrides.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["permisosweb.catalogo.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin" },
        ["proveedores.ver"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin", "User" },
        ["proveedores.crear"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["proveedores.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" },
        ["proveedores.estado.editar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "Admin" }
    };

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(AppDbContext db, IMemoryCache cache, ILogger<PermissionService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    private static readonly List<PermissionSeed> PermissionCatalogSeeds = new()
    {
        new("usuarios.ver", "Usuarios - Ver", "usuarios"),
        new("usuarios.crear", "Usuarios - Crear", "usuarios"),
        new("usuarios.editar", "Usuarios - Editar", "usuarios"),
        new("usuarios.estado.editar", "Usuarios - Activar/Inactivar", "usuarios"),
        new("usuarios.password.reset", "Usuarios - Reset Password", "usuarios"),
        new("usuarios.empresa.editar", "Usuarios - Cambiar Empresa", "usuarios"),

        new("empresas.ver", "Empresas - Ver", "empresas"),

        new("permisosweb.bootstrap", "Permisos Web - Ver Administracion", "permisosweb"),
        new("permisosweb.roles.editar", "Permisos Web - Editar Roles", "permisosweb"),
        new("permisosweb.overrides.editar", "Permisos Web - Editar Overrides", "permisosweb"),
        new("permisosweb.catalogo.editar", "Permisos Web - Editar Catalogo", "permisosweb"),

        new("proveedores.ver", "Proveedores - Ver", "proveedores"),
        new("proveedores.crear", "Proveedores - Crear", "proveedores"),
        new("proveedores.editar", "Proveedores - Editar", "proveedores"),
        new("proveedores.estado.editar", "Proveedores - Activar/Inactivar", "proveedores"),

        new("pagosproveedores.ver", "Pagos Proveedores - Ver", "pagosproveedores"),
        new("pagosproveedores.crear", "Pagos Proveedores - Crear", "pagosproveedores"),
        new("pagosproveedores.editar", "Pagos Proveedores - Modificar", "pagosproveedores"),
        new("pagosproveedores.cancelar", "Pagos Proveedores - Cancelar", "pagosproveedores"),

        new("cheques.ver", "Cheques - Entrar", "cheques")
    };

    public bool IsAllowedByLegacy(string rolLegacy, string permission)
    {
        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (LegacyPolicyMap.TryGetValue(permission, out var allowedRoles))
            return allowedRoles.Contains(rolLegacy ?? string.Empty);

        return true;
    }

    public async Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(userId, empresaId, rolLegacy);
        var staleCacheKey = $"{cacheKey}:stale";

        if (_cache.TryGetValue<PermissionSnapshot>(cacheKey, out var cached) && cached is not null)
            return cached;

        try
        {
            var snapshot = await BuildSnapshotAsync(userId, empresaId, rolLegacy, ct);
            _cache.Set(cacheKey, snapshot, ActiveCacheTtl);
            _cache.Set(staleCacheKey, snapshot, StaleCacheTtl);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al calcular permisos para userId={UserId} empresaId={EmpresaId}. Se usara fallback.", userId, empresaId);
            if (_cache.TryGetValue<PermissionSnapshot>(staleCacheKey, out var stale) && stale is not null)
                return stale;

            return BuildLegacyFallback(userId, empresaId, rolLegacy);
        }
    }

    public async Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);

        var response = new PermisosWebBootstrapResponse { PermissionsEnabled = true };

        await using (var rolesCmd = new SqlCommand(@"
SELECT Codigo, Nombre
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
ORDER BY Codigo;", conn))
        {
            rolesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Roles.Add(new PermisosWebRoleItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Codigo")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        await using (var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn))
        {
            permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await permsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Permissions.Add(new PermisosWebPermissionItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Clave")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        var rolePermsSql = $@"
SELECT r.Codigo AS RoleCode, p.Clave AS Permission
FROM dbo.WRolPermiso rp
INNER JOIN dbo.WRol r
    ON r.EmpresaId = rp.EmpresaId
   AND r.{Q(schema.RoleIdColumn)} = rp.{Q(schema.RolePermRoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE rp.EmpresaId = @EmpresaId
ORDER BY r.Codigo, p.Clave;";

        await using (var rolePermsCmd = new SqlCommand(rolePermsSql, conn))
        {
            rolePermsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolePermsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.RolePermissions.Add(new PermisosWebRolePermissionItem
                {
                    RoleCode = reader.GetString(reader.GetOrdinal("RoleCode")),
                    Permission = reader.GetString(reader.GetOrdinal("Permission"))
                });
            }
        }

        await using (var usersCmd = new SqlCommand(@"
SELECT Id, Usuario, Rol
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
ORDER BY Usuario;", conn))
        {
            usersCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await usersCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Users.Add(new PermisosWebUserItem
                {
                    UserId = reader.GetGuid(reader.GetOrdinal("Id")),
                    Username = reader.GetString(reader.GetOrdinal("Usuario")),
                    RoleLegacy = reader.GetString(reader.GetOrdinal("Rol"))
                });
            }
        }

        var overridesSql = $@"
SELECT up.{Q(schema.UserPermUserIdColumn)} AS UsuarioWebId, LOWER(up.Tipo) AS Tipo, p.Clave
FROM dbo.WUsuarioPermiso up
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = up.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
WHERE up.EmpresaId = @EmpresaId;";

        var overrides = new Dictionary<Guid, PermisosWebUserOverrideItem>();
        await using (var overridesCmd = new SqlCommand(overridesSql, conn))
        {
            overridesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await overridesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rawUserId = reader["UsuarioWebId"];
                if (!TryReadGuid(rawUserId, out var userId))
                    continue;

                var tipo = reader.GetString(reader.GetOrdinal("Tipo"));
                var clave = reader.GetString(reader.GetOrdinal("Clave"));

                if (!overrides.TryGetValue(userId, out var row))
                {
                    row = new PermisosWebUserOverrideItem { UserId = userId };
                    overrides[userId] = row;
                }

                if (string.Equals(tipo, "permit", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "allow", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "a", StringComparison.OrdinalIgnoreCase))
                    row.Allow.Add(clave);
                else if (string.Equals(tipo, "deny", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
                else if (string.Equals(tipo, "block", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
                else if (string.Equals(tipo, "d", StringComparison.OrdinalIgnoreCase))
                    row.Deny.Add(clave);
            }
        }

        response.UserOverrides = overrides.Values.OrderBy(x => x.UserId).ToList();
        return response;
    }

    public async Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return null;

        var schema = await GetSchemaAsync(conn, ct);
        var response = new PermisosWebRolesBootstrapResponse { PermissionsEnabled = true };

        await using (var rolesCmd = new SqlCommand(@"
SELECT Codigo, Nombre
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
ORDER BY Codigo;", conn))
        {
            rolesCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolesCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Roles.Add(new PermisosWebRoleItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Codigo")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        await using (var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn))
        {
            permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await permsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.Permissions.Add(new PermisosWebPermissionItem
                {
                    Code = reader.GetString(reader.GetOrdinal("Clave")),
                    Name = reader.GetString(reader.GetOrdinal("Nombre"))
                });
            }
        }

        var rolePermsSql = $@"
SELECT r.Codigo AS RoleCode, p.Clave AS Permission
FROM dbo.WRolPermiso rp
INNER JOIN dbo.WRol r
    ON r.EmpresaId = rp.EmpresaId
   AND r.{Q(schema.RoleIdColumn)} = rp.{Q(schema.RolePermRoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE rp.EmpresaId = @EmpresaId
ORDER BY r.Codigo, p.Clave;";

        await using (var rolePermsCmd = new SqlCommand(rolePermsSql, conn))
        {
            rolePermsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            await using var reader = await rolePermsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                response.RolePermissions.Add(new PermisosWebRolePermissionItem
                {
                    RoleCode = reader.GetString(reader.GetOrdinal("RoleCode")),
                    Permission = reader.GetString(reader.GetOrdinal("Permission"))
                });
            }
        }

        return response;
    }

    public async Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return Array.Empty<PermisosWebPermissionItem>();

        var response = new List<PermisosWebPermissionItem>();
        await using var permsCmd = new SqlCommand(@"
SELECT Clave, Nombre
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
ORDER BY Clave;", conn);
        permsCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var reader = await permsCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Add(new PermisosWebPermissionItem
            {
                Code = reader.GetString(reader.GetOrdinal("Clave")),
                Name = reader.GetString(reader.GetOrdinal("Nombre"))
            });
        }

        return response;
    }

    public async Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var normalizedPermissions = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
            await using var spCmd = new SqlCommand("dbo.sp_w_RolPermisos_ReemplazarPorClaves", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            spCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            spCmd.Parameters.Add(new SqlParameter("@RolCodigo", SqlDbType.NVarChar, 80) { Value = roleCode?.Trim() ?? string.Empty });
            spCmd.Parameters.Add(new SqlParameter("@ClavesCsv", SqlDbType.NVarChar, -1) { Value = string.Join(",", normalizedPermissions) });
            await spCmd.ExecuteNonQueryAsync(ct);
            return;
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
        }

        var schema = await GetSchemaAsync(conn, ct);
        var rolePermColumns = await GetTableColumnsAsync(conn, "WRolPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var roleId = await GetRoleIdByCodeAsync(conn, (SqlTransaction)tx, schema, empresaId, roleCode, ct);
        if (roleId is null)
            throw new KeyNotFoundException($"Rol no encontrado: {roleCode}");

        var permissionIds = await GetPermissionIdsByKeysAsync(conn, (SqlTransaction)tx, schema, empresaId, normalizedPermissions, ct);
        if (permissionIds.Count != normalizedPermissions.Count)
        {
            var missing = normalizedPermissions.Where(x => !permissionIds.Keys.Contains(x, StringComparer.OrdinalIgnoreCase));
            throw new ArgumentException($"Permisos invalidos: {string.Join(", ", missing)}");
        }

        var deleteSql = $@"
DELETE FROM dbo.WRolPermiso
WHERE EmpresaId = @EmpresaId
  AND {Q(schema.RolePermRoleIdColumn)} = @RoleId;";
        await using (var deleteCmd = new SqlCommand(deleteSql, conn, (SqlTransaction)tx))
        {
            deleteCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            deleteCmd.Parameters.Add(new SqlParameter("@RoleId", roleId));
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        var insertColumns = new List<string> { "EmpresaId", schema.RolePermRoleIdColumn, schema.RolePermPermissionIdColumn };
        var insertValues = new List<string> { "@EmpresaId", "@RoleId", "@PermissionId" };
        if (rolePermColumns.Contains("FechaCreacion"))
        {
            insertColumns.Add("FechaCreacion");
            insertValues.Add("SYSUTCDATETIME()");
        }
        var insertSql = $@"
INSERT INTO dbo.WRolPermiso ({string.Join(", ", insertColumns.Select(Q))})
VALUES ({string.Join(", ", insertValues)});";
        foreach (var permissionId in permissionIds.Values)
        {
            await using var insertCmd = new SqlCommand(insertSql, conn, (SqlTransaction)tx);
            insertCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            insertCmd.Parameters.Add(new SqlParameter("@RoleId", roleId));
            insertCmd.Parameters.Add(new SqlParameter("@PermissionId", permissionId));
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var schema = await GetSchemaAsync(conn, ct);
        var overrideTypes = await GetOverrideTypeTokensAsync(conn, ct);
        var userPermCols = await GetUserPermColumnsAsync(conn, null, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await EnsureUserExistsAsync(conn, (SqlTransaction)tx, empresaId, userId, ct);

        var allowSet = allow.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var denySet = deny.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicated = allowSet.Intersect(denySet, StringComparer.OrdinalIgnoreCase).ToList();
        if (duplicated.Count > 0)
            throw new ArgumentException($"Un permiso no puede estar en allow y deny: {string.Join(", ", duplicated)}");

        var combined = allowSet.Concat(denySet).ToList();
        var permissionIds = await GetPermissionIdsByKeysAsync(conn, (SqlTransaction)tx, schema, empresaId, combined, ct);
        if (permissionIds.Count != combined.Count)
        {
            var missing = combined.Where(x => !permissionIds.Keys.Contains(x, StringComparer.OrdinalIgnoreCase));
            throw new ArgumentException($"Permisos invalidos: {string.Join(", ", missing)}");
        }

        var deleteSql = $@"
DELETE FROM dbo.WUsuarioPermiso
WHERE EmpresaId = @EmpresaId
  AND {Q(schema.UserPermUserIdColumn)} = @UsuarioWebId;";
        await using (var deleteCmd = new SqlCommand(deleteSql, conn, (SqlTransaction)tx))
        {
            deleteCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            deleteCmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var key in allowSet)
            await InsertUserOverrideAsync(conn, (SqlTransaction)tx, schema, userPermCols, empresaId, userId, permissionIds[key], overrideTypes.AllowToken, ct);

        foreach (var key in denySet)
            await InsertUserOverrideAsync(conn, (SqlTransaction)tx, schema, userPermCols, empresaId, userId, permissionIds[key], overrideTypes.DenyToken, ct);

        await tx.CommitAsync(ct);
        InvalidateUserPermissionCache(userId, empresaId);
    }

    private void InvalidateUserPermissionCache(Guid userId, int empresaId)
    {
        var roleCandidates = new[] { "SuperAdmin", "Admin", "User", "SUPER_ADMIN", "ADMIN", "USER" };
        foreach (var role in roleCandidates)
        {
            var key = BuildCacheKey(userId, empresaId, role);
            _cache.Remove(key);
            _cache.Remove($"{key}:stale");
        }
    }

    private async Task<OverrideTypeTokens> GetOverrideTypeTokensAsync(SqlConnection conn, CancellationToken ct)
    {
        // Modelo nuevo: Tipo CHAR/NCHAR(1) con valores A/D.
        await using (var tipoLenCmd = new SqlCommand(@"
SELECT c.max_length
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = 'WUsuarioPermiso'
  AND c.name = 'Tipo';", conn))
        {
            var lenObj = await tipoLenCmd.ExecuteScalarAsync(ct);
            if (lenObj is not null && lenObj is not DBNull)
            {
                var maxLen = Convert.ToInt32(lenObj);
                if (maxLen == 1 || maxLen == 2) // nchar(1)=2 bytes
                {
                    return new OverrideTypeTokens("A", "D");
                }
            }
        }

        var sql = @"
SELECT cc.definition
FROM sys.check_constraints cc
INNER JOIN sys.tables t ON t.object_id = cc.parent_object_id
WHERE t.name = 'WUsuarioPermiso';";

        await using var cmd = new SqlCommand(sql, conn);
        var definitions = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    definitions.Add(reader.GetString(0));
            }
        }
        var definition = string.Join(" ", definitions).ToLowerInvariant();

        var hasA = definition.Contains("'a'");
        var hasD = definition.Contains("'d'");
        if (hasA && hasD)
        {
            return new OverrideTypeTokens("A", "D");
        }

        var allowToken = definition.Contains("'allow'") ? "allow" : "permit";
        var denyToken = definition.Contains("'block'") && !definition.Contains("'deny'") ? "block" : "deny";
        return new OverrideTypeTokens(allowToken, denyToken);
    }

    public async Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var moduleFilter = (modules ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seeds = PermissionCatalogSeeds
            .Where(x => moduleFilter.Count == 0 || moduleFilter.Contains(x.Module))
            .ToList();

        if (seeds.Count == 0)
            throw new ArgumentException("No hay permisos semilla para los modulos solicitados.");

        var permColumns = await GetTableColumnsAsync(conn, "WPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var response = new PermisosWebSyncCatalogResponse
        {
            TotalSeeds = seeds.Count
        };

        foreach (var seed in seeds)
        {
            if (await PermissionExistsAsync(conn, (SqlTransaction)tx, empresaId, seed.Key, ct))
            {
                response.SkippedPermissions.Add(seed.Key);
                continue;
            }

            await InsertPermissionSeedAsync(conn, (SqlTransaction)tx, permColumns, empresaId, seed, null, ct);
            response.InsertedPermissions.Add(seed.Key);
        }

        await tx.CommitAsync(ct);
        response.InsertedCount = response.InsertedPermissions.Count;
        return response;
    }

    public async Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct)
    {
        var normalizedKey = NormalizePermissionKey(key);
        var normalizedName = string.IsNullOrWhiteSpace(name) ? normalizedKey : name.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            throw new KeyNotFoundException("Capacidades no disponibles para este tenant/base.");

        var permColumns = await GetTableColumnsAsync(conn, "WPermiso", ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await PermissionExistsAsync(conn, (SqlTransaction)tx, empresaId, normalizedKey, ct))
        {
            await tx.CommitAsync(ct);
            return false;
        }

        try
        {
            await using var spCmd = new SqlCommand("dbo.sp_w_Permiso_Guardar", conn, (SqlTransaction)tx)
            {
                CommandType = CommandType.StoredProcedure
            };
            spCmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            spCmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 150) { Value = normalizedKey });
            spCmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 150) { Value = normalizedName });
            spCmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 300) { Value = (object?)normalizedDescription ?? DBNull.Value });
            await spCmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
        }

        await InsertPermissionSeedAsync(
            conn,
            (SqlTransaction)tx,
            permColumns,
            empresaId,
            new PermissionSeed(normalizedKey, normalizedName, ModuleFromKey(normalizedKey)),
            normalizedDescription,
            ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<PermissionSnapshot> BuildSnapshotAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return BuildLegacyFallback(userId, empresaId, rolLegacy);

        var feature = await GetFeatureStatusAsync(conn, empresaId, ct);
        if (!feature.Enabled)
            return BuildLegacyFallback(userId, empresaId, rolLegacy, feature.Version);

        var schema = await GetSchemaAsync(conn, ct);
        var permissions = await LoadEffectivePermissionsAsync(conn, schema, userId, empresaId, rolLegacy, ct);
        return new PermissionSnapshot
        {
            UserId = userId,
            EmpresaId = empresaId,
            RolLegacy = rolLegacy,
            PermissionsEnabled = true,
            Permissions = permissions,
            PermissionsVersion = feature.Version
        };
    }

    private static async Task<bool> AreCapabilityTablesAvailableAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT CASE
    WHEN OBJECT_ID('dbo.WRol', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WPermiso', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WRolPermiso', 'U') IS NOT NULL
     AND OBJECT_ID('dbo.WUsuarioPermiso', 'U') IS NOT NULL
    THEN 1 ELSE 0 END;", conn);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is not null && Convert.ToInt32(value) == 1;
    }

    private static async Task<(bool Enabled, string Version)> GetFeatureStatusAsync(SqlConnection conn, int empresaId, CancellationToken ct)
    {
        await using (var existsCmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.WConfiguracionEmpresa', 'U') IS NOT NULL THEN 1 ELSE 0 END;", conn))
        {
            var exists = await existsCmd.ExecuteScalarAsync(ct);
            if (exists is null || Convert.ToInt32(exists) != 1)
                return (true, DateTime.UtcNow.ToString("O"));
        }

        await using var cmd = new SqlCommand(@"
SELECT TOP 1
    Activo,
    FechaActualizacion
FROM dbo.WConfiguracionEmpresa
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave
ORDER BY FechaActualizacion DESC;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = FeatureKey });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (true, DateTime.UtcNow.ToString("O"));

        var enabled = reader.GetBoolean(reader.GetOrdinal("Activo"));
        var version = reader.IsDBNull(reader.GetOrdinal("FechaActualizacion"))
            ? DateTime.UtcNow.ToString("O")
            : reader.GetDateTime(reader.GetOrdinal("FechaActualizacion")).ToUniversalTime().ToString("O");
        return (enabled, version);
    }

    private static async Task<List<string>> LoadEffectivePermissionsAsync(
        SqlConnection conn,
        CapabilitySchema schema,
        Guid userId,
        int empresaId,
        string rolLegacy,
        CancellationToken ct)
    {
        var rolCodigo = ToRoleCode(rolLegacy);

        var sql = $@"
;WITH RolePermisos AS (
    SELECT p.Clave
    FROM dbo.WRol r
    INNER JOIN dbo.WRolPermiso rp
        ON rp.EmpresaId = r.EmpresaId
       AND rp.{Q(schema.RolePermRoleIdColumn)} = r.{Q(schema.RoleIdColumn)}
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = rp.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
    WHERE r.EmpresaId = @EmpresaId
      AND r.Codigo = @RolCodigo
),
Permits AS (
    SELECT p.Clave
    FROM dbo.WUsuarioPermiso up
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = up.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
    WHERE up.EmpresaId = @EmpresaId
      AND up.{Q(schema.UserPermUserIdColumn)} = @UsuarioWebId
      AND LOWER(up.Tipo) IN ('permit','allow','a')
),
Denies AS (
    SELECT p.Clave
    FROM dbo.WUsuarioPermiso up
    INNER JOIN dbo.WPermiso p
        ON p.EmpresaId = up.EmpresaId
       AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
    WHERE up.EmpresaId = @EmpresaId
      AND up.{Q(schema.UserPermUserIdColumn)} = @UsuarioWebId
      AND LOWER(up.Tipo) IN ('deny','block','d')
),
PermisosUnidos AS (
    SELECT Clave FROM RolePermisos
    UNION
    SELECT Clave FROM Permits
)
SELECT pu.Clave
FROM PermisosUnidos pu
LEFT JOIN Denies d ON d.Clave = pu.Clave
WHERE d.Clave IS NULL
ORDER BY pu.Clave;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        cmd.Parameters.Add(new SqlParameter("@RolCodigo", SqlDbType.NVarChar, 30) { Value = rolCodigo });

        var permissions = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clave = reader.GetString(reader.GetOrdinal("Clave"));
            if (!string.IsNullOrWhiteSpace(clave))
                permissions.Add(clave);
        }
        return permissions;
    }

    private async Task<CapabilitySchema> GetSchemaAsync(SqlConnection conn, CancellationToken ct)
    {
        if (_cache.TryGetValue<CapabilitySchema>(SchemaCacheKey, out var cached) && cached is not null)
            return cached;

        await using var cmd = new SqlCommand(@"
SELECT t.name AS TableName, c.name AS ColumnName
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name IN ('WRol','WPermiso','WRolPermiso','WUsuarioPermiso');", conn);

        var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var table = reader.GetString(reader.GetOrdinal("TableName"));
                var col = reader.GetString(reader.GetOrdinal("ColumnName"));
                if (!columnsByTable.TryGetValue(table, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    columnsByTable[table] = set;
                }
                set.Add(col);
            }
        }

        string Pick(string table, params string[] candidates)
        {
            if (!columnsByTable.TryGetValue(table, out var cols))
                throw new InvalidOperationException($"Tabla no disponible: {table}");
            var chosen = candidates.FirstOrDefault(cols.Contains);
            if (string.IsNullOrWhiteSpace(chosen))
                throw new InvalidOperationException($"No se encontro columna esperada en {table}. Candidatas: {string.Join(", ", candidates)}");
            return chosen;
        }

        var schema = new CapabilitySchema(
            RoleIdColumn: Pick("WRol", "Id", "WRolId", "RolId"),
            PermissionIdColumn: Pick("WPermiso", "Id", "WPermisoId", "PermisoId"),
            RolePermRoleIdColumn: Pick("WRolPermiso", "WRolId", "RolId"),
            RolePermPermissionIdColumn: Pick("WRolPermiso", "WPermisoId", "PermisoId"),
            UserPermUserIdColumn: Pick("WUsuarioPermiso", "UsuarioWebId", "UsuarioId", "UserId", "WUsuarioId"),
            UserPermPermissionIdColumn: Pick("WUsuarioPermiso", "WPermisoId", "PermisoId")
        );

        _cache.Set(SchemaCacheKey, schema, SchemaCacheTtl);
        return schema;
    }

    private static string ToRoleCode(string rolLegacy)
    {
        if (string.Equals(rolLegacy, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return "SUPER_ADMIN";
        if (string.Equals(rolLegacy, "Admin", StringComparison.OrdinalIgnoreCase))
            return "ADMIN";
        return "USER";
    }

    private static PermissionSnapshot BuildLegacyFallback(Guid userId, int empresaId, string rolLegacy, string? version = null)
    {
        return new PermissionSnapshot
        {
            UserId = userId,
            EmpresaId = empresaId,
            RolLegacy = rolLegacy,
            PermissionsEnabled = false,
            Permissions = new List<string>(),
            PermissionsVersion = version ?? DateTime.UtcNow.ToString("O")
        };
    }

    private static string BuildCacheKey(Guid userId, int empresaId, string rolLegacy)
        => $"perm:{empresaId}:{userId:N}:{rolLegacy}";

    private static async Task<object?> GetRoleIdByCodeAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        int empresaId,
        string roleCode,
        CancellationToken ct)
    {
        var sql = $@"
SELECT TOP 1 {Q(schema.RoleIdColumn)}
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
  AND Codigo = @Codigo;";
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Codigo", SqlDbType.NVarChar, 30) { Value = roleCode.Trim().ToUpperInvariant() });
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is DBNull ? null : value;
    }

    private static async Task<Dictionary<string, object>> GetPermissionIdsByKeysAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        int empresaId,
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var sql = $@"
SELECT TOP 1 {Q(schema.PermissionIdColumn)}
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave;";

        foreach (var key in keys)
        {
            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
            cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = key });
            var value = await cmd.ExecuteScalarAsync(ct);
            if (value is null || value is DBNull) continue;
            result[key] = value;
        }

        return result;
    }

    private static async Task EnsureUserExistsAsync(
        SqlConnection conn,
        SqlTransaction tx,
        int empresaId,
        Guid userId,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.UsuarioWeb
WHERE EmpresaId = @EmpresaId
  AND Id = @Id;", conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = userId });
        var exists = await cmd.ExecuteScalarAsync(ct);
        if (exists is null)
            throw new KeyNotFoundException($"Usuario no encontrado: {userId}");
    }

    private async Task InsertUserOverrideAsync(
        SqlConnection conn,
        SqlTransaction tx,
        CapabilitySchema schema,
        UserPermColumns userPermCols,
        int empresaId,
        Guid userId,
        object permissionId,
        string tipo,
        CancellationToken ct)
    {
        var safeMotivo = NormalizeForColumn("actualizacion.permisosweb", userPermCols.MotivoMaxChars);
        var rawTipo = (tipo ?? string.Empty).Trim();
        var tipoCandidates = BuildTipoCandidates(rawTipo);

        var sql = $@"
INSERT INTO dbo.WUsuarioPermiso (EmpresaId, {Q(schema.UserPermUserIdColumn)}, {Q(schema.UserPermPermissionIdColumn)}, Tipo, Motivo)
VALUES (@EmpresaId, @UsuarioWebId, @WPermisoId, @Tipo, @Motivo);";
        SqlException? lastCheckEx = null;

        foreach (var candidate in tipoCandidates)
        {
            var safeTipo = NormalizeForColumn(candidate, userPermCols.TipoMaxChars);
            try
            {
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
                cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
                cmd.Parameters.Add(new SqlParameter("@WPermisoId", permissionId));
                cmd.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.NVarChar, Math.Max(1, safeTipo.Length)) { Value = safeTipo });
                cmd.Parameters.Add(new SqlParameter("@Motivo", SqlDbType.NVarChar, Math.Max(1, safeMotivo.Length)) { Value = safeMotivo });
                await cmd.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (SqlException ex) when (IsTipoCheckConstraintViolation(ex))
            {
                lastCheckEx = ex;
            }
        }

        if (lastCheckEx is not null) throw lastCheckEx;
        throw new InvalidOperationException("No se pudo insertar override de usuario.");
    }

    private static bool IsTipoCheckConstraintViolation(SqlException ex)
        => ex.Number == 547 &&
           ex.Message.Contains("CK_WUsuarioPermiso_Tipo", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildTipoCandidates(string rawTipo)
    {
        var token = rawTipo.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return new[] { "deny", "block", "D", "d" };

        if (string.Equals(token, "A", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "allow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "permit", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "permit", "allow", "A", "a" };
        }

        return new[] { "deny", "block", "D", "d" };
    }

    private async Task<UserPermColumns> GetUserPermColumnsAsync(SqlConnection conn, SqlTransaction? tx, CancellationToken ct)
    {
        if (_cache.TryGetValue<UserPermColumns>(UserPermColumnsCacheKey, out var cached) && cached is not null)
            return cached;

        await using var cmd = new SqlCommand(@"
SELECT c.name, c.max_length
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = 'WUsuarioPermiso'
  AND c.name IN ('Tipo','Motivo');", conn, tx);

        var tipoMaxChars = 20;
        var motivoMaxChars = 300;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var maxLengthBytes = reader.GetInt16(1);
            var maxChars = maxLengthBytes < 0 ? 4000 : Math.Max(1, maxLengthBytes / 2); // nvarchar/nchar stored in bytes

            if (string.Equals(name, "Tipo", StringComparison.OrdinalIgnoreCase))
                tipoMaxChars = maxChars;
            else if (string.Equals(name, "Motivo", StringComparison.OrdinalIgnoreCase))
                motivoMaxChars = maxChars;
        }

        var cols = new UserPermColumns(tipoMaxChars, motivoMaxChars);
        _cache.Set(UserPermColumnsCacheKey, cols, SchemaCacheTtl);
        return cols;
    }

    private static string NormalizeForColumn(string? value, int maxChars)
    {
        var text = value ?? string.Empty;
        if (maxChars <= 0) return string.Empty;
        return text.Length <= maxChars ? text : text.Substring(0, maxChars);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        SqlConnection conn,
        string tableName,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT c.name
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = @TableName;", conn);
        cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            cols.Add(reader.GetString(0));
        }
        return cols;
    }

    private static async Task<bool> PermissionExistsAsync(
        SqlConnection conn,
        SqlTransaction tx,
        int empresaId,
        string key,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.WPermiso
WHERE EmpresaId = @EmpresaId
  AND Clave = @Clave;", conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = key });
        var exists = await cmd.ExecuteScalarAsync(ct);
        return exists is not null;
    }

    private static async Task InsertPermissionSeedAsync(
        SqlConnection conn,
        SqlTransaction tx,
        HashSet<string> permColumns,
        int empresaId,
        PermissionSeed seed,
        string? explicitDescription,
        CancellationToken ct)
    {
        var insertColumns = new List<string> { "EmpresaId", "Clave", "Nombre" };
        var insertValues = new List<string> { "@EmpresaId", "@Clave", "@Nombre" };

        if (permColumns.Contains("Descripcion"))
        {
            insertColumns.Add("Descripcion");
            insertValues.Add("@Descripcion");
        }
        if (permColumns.Contains("FechaCreacion"))
        {
            insertColumns.Add("FechaCreacion");
            insertValues.Add("SYSUTCDATETIME()");
        }

        var sql = $@"
INSERT INTO dbo.WPermiso ({string.Join(", ", insertColumns.Select(Q))})
VALUES ({string.Join(", ", insertValues)});";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Clave", SqlDbType.NVarChar, 200) { Value = seed.Key });
        cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = seed.Name });
        if (permColumns.Contains("Descripcion"))
            cmd.Parameters.Add(new SqlParameter("@Descripcion", SqlDbType.NVarChar, 500) { Value = explicitDescription ?? $"{seed.Module}.seed" });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static bool TryReadGuid(object? raw, out Guid value)
    {
        value = Guid.Empty;
        if (raw is Guid g)
        {
            value = g;
            return true;
        }
        if (raw is string s && Guid.TryParse(s, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    private static string NormalizePermissionKey(string key)
    {
        var value = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La clave del permiso es requerida.");
        if (value.Length > 200)
            throw new ArgumentException("La clave del permiso excede el maximo permitido.");

        foreach (var ch in value)
        {
            var valid = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_';
            if (!valid)
                throw new ArgumentException("La clave del permiso solo permite a-z, 0-9, punto y guion bajo.");
        }

        if (!value.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException("La clave debe tener formato modulo.accion");

        return value;
    }

    private static string ModuleFromKey(string key)
    {
        var idx = key.IndexOf('.');
        return idx > 0 ? key.Substring(0, idx) : "general";
    }

    private static string Q(string identifier) => $"[{identifier}]";

    private sealed record CapabilitySchema(
        string RoleIdColumn,
        string PermissionIdColumn,
        string RolePermRoleIdColumn,
        string RolePermPermissionIdColumn,
        string UserPermUserIdColumn,
        string UserPermPermissionIdColumn);

    private sealed record OverrideTypeTokens(string AllowToken, string DenyToken);
    private sealed record UserPermColumns(int TipoMaxChars, int MotivoMaxChars);

    private sealed record PermissionSeed(string Key, string Name, string Module);
}
