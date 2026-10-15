using System.Data;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ISL_Service.Infrastructure.Security;

/// <summary>
/// LO QUE EL GUARDIA NECESITA PREGUNTARLE A LA BASE.
/// </summary>
/// <remarks>
/// POR QUE VIVE EN PermissionService Y NO EN UNA CLASE NUEVA
/// ---------------------------------------------------------
/// Porque las tres consultas de aqui tienen que partir las tablas EXACTAMENTE
/// igual que las de PermissionService.cs, y eso no se consigue copiando el
/// SQL: los nombres de columna NO son fijos. GetSchemaAsync los descubre en
/// tiempo de ejecucion (en MacZ son WRolId/WPermisoId/UsuarioWebId, pero el
/// codigo acepta otras variantes porque las dos bases de produccion no
/// nacieron iguales). Una clase aparte tendria que repetir ese descubrimiento,
/// y el dia que las dos copias no coincidieran el guardia estaria mirando una
/// tabla distinta de la que de verdad decide los permisos: diria "si queda un
/// administrador" sobre datos que no son los que se consultan al autorizar.
///
/// Mismo motivo para reusar GetOverrideTypeTokensAsync (los tokens de
/// allow/deny tambien varian) y EsDeModuloApagado (la regla de "lo que va
/// antes del primer punto").
/// </remarks>
public sealed partial class PermissionService
{
    /// <summary>
    /// Las claves que ese rol tiene GUARDADAS hoy en WRolPermiso.
    /// </summary>
    /// <remarks>
    /// Es lo que hay que comparar contra lo que llega en el PUT para saber que
    /// se CONCEDE y que se RETIRA — el endpoint manda la lista completa, no un
    /// delta, asi que sin leer el estado anterior no se puede distinguir
    /// "agrego una clave" de "quito quince".
    ///
    /// OJO, Y ES IMPORTANTE: esto es lo guardado, que NO es lo mismo que lo
    /// efectivo. En MacZ el rol USER tiene UNA fila en WRolPermiso y el usuario
    /// DEMO acaba con 423 claves, porque las demas salen del puente con Mac31
    /// (ver PermissionService.FamiliasLegacy.cs). Para lo que puede el
    /// LLAMANTE hay que usar GetPermissionsAsync, no esto; esto es solo para
    /// saber que filas de esta tabla se van a tocar.
    /// </remarks>
    public async Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(
        int empresaId,
        string roleCode,
        CancellationToken ct)
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codigo = (roleCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo)) return claves;

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        if (!await AreCapabilityTablesAvailableAsync(conn, ct)) return claves;

        var schema = await GetSchemaAsync(conn, ct);
        var sql = $@"
SELECT p.Clave
FROM dbo.WRol r
INNER JOIN dbo.WRolPermiso rp
    ON rp.EmpresaId = r.EmpresaId
   AND rp.{Q(schema.RolePermRoleIdColumn)} = r.{Q(schema.RoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE r.EmpresaId = @EmpresaId
  AND UPPER(r.Codigo) = UPPER(@RolCodigo);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@RolCodigo", SqlDbType.NVarChar, 80) { Value = codigo });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clave = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(clave)) claves.Add(clave.Trim());
        }
        return claves;
    }

    /// <summary>
    /// Los ajustes individuales (allow y deny) que ese usuario tiene guardados.
    /// Mismo motivo que arriba: el PUT manda la lista completa.
    /// </summary>
    public async Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(
        int empresaId,
        Guid userId,
        CancellationToken ct)
    {
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        if (!await AreCapabilityTablesAvailableAsync(conn, ct)) return (allow, deny);

        var schema = await GetSchemaAsync(conn, ct);
        var sql = $@"
SELECT p.Clave, LOWER(up.Tipo) AS Tipo
FROM dbo.WUsuarioPermiso up
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = up.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
WHERE up.EmpresaId = @EmpresaId
  AND up.{Q(schema.UserPermUserIdColumn)} = @UsuarioWebId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@UsuarioWebId", SqlDbType.UniqueIdentifier) { Value = userId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clave = reader.GetString(0);
            var tipo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(clave)) continue;

            // Los mismos tokens que reconoce LoadEffectivePermissionsAsync. Si
            // aqui se aceptaran otros, un 'block' guardado por una version
            // vieja se leeria como allow y el guardia contaria administradores
            // que en realidad estan bloqueados.
            if (tipo is "permit" or "allow" or "a") allow.Add(clave.Trim());
            else if (tipo is "deny" or "block" or "d") deny.Add(clave.Trim());
        }
        return (allow, deny);
    }

    /// <summary>
    /// Quien seguiria pudiendo administrar permisos si el cambio descrito en
    /// <paramref name="simulacion"/> se guardara.
    /// </summary>
    /// <remarks>
    /// COMO SE CALCULA, Y POR QUE ASI
    /// ------------------------------
    /// Se repite a mano la misma cuenta que hace LoadEffectivePermissionsAsync
    /// —rol UNION allow, menos deny, menos lo de los modulos apagados— pero
    /// para TODOS los usuarios de la empresa de una sola vez y limitada a las
    /// nueve claves de gobierno. Limitarla es lo que la hace viable: en MacZ
    /// son 31 usuarios web y 491 permisos; sin el filtro serian ~15.000 filas
    /// en cada comprobacion, y esto corre en el camino de un PUT.
    ///
    /// NO se llama a GetPermissionsAsync usuario por usuario, que seria lo
    /// "limpio": ese metodo siembra catalogos, sincroniza cuatro familias de
    /// legacy y escribe en la base. Llamarlo 31 veces dentro de un PUT
    /// multiplicaria por 31 el trabajo que hoy ya tarda segundos la primera
    /// vez, y encima escribiria durante una comprobacion que tiene que ser de
    /// solo lectura.
    ///
    /// LO QUE ESTE CALCULO NO VE, Y POR QUE NO IMPORTA
    /// -----------------------------------------------
    /// El puente con Mac31 puede ANADIR claves al snapshot (de ahi las 423 de
    /// DEMO con un rol que tiene una sola fila). Pero ese puente solo trata
    /// familias de negocio —reportes, ventas, empleados, prestamos, compras,
    /// garantias...— y NINGUNA de las nueve de gobierno existe en Mac31: son
    /// llaves que nacieron en el web. Asi que para estas nueve, lo guardado en
    /// WRolPermiso/WUsuarioPermiso ES lo efectivo. Se comprobo contra MacZ:
    /// las claves "permisosweb.*" y "permisos_*" del /api/me de DEMO salen
    /// todas de WRolPermiso.
    ///
    /// Al que tiene el rol legacy SuperAdmin se le cuenta SIEMPRE, porque
    /// PermissionAuthorizationHandler lo deja pasar antes de mirar el snapshot.
    /// No es una puerta nueva: es la que ya existe, y no contarla haria que el
    /// guardia negara cambios inofensivos en una base con un SuperAdmin vivo.
    /// </remarks>
    public async Task<AdministracionRestante> GetAdministracionRestanteAsync(
        int empresaId,
        SimulacionDePermisos simulacion,
        CancellationToken ct)
    {
        var vacio = Array.Empty<string>();
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);

        // Una base sin el modelo de capacidades no tiene este problema: no hay
        // permisos que repartir. No se bloquea nada.
        if (!await AreCapabilityTablesAvailableAsync(conn, ct))
            return new AdministracionRestante(vacio, vacio, NoSePudoComprobar: true);
        if (!await TableExistsAsync(conn, "UsuarioWeb", ct))
            return new AdministracionRestante(vacio, vacio, NoSePudoComprobar: true);

        var schema = await GetSchemaAsync(conn, ct);
        var claveDeGobierno = string.Join(",", LlavesDeGobierno.Todas.Select((_, i) => $"@g{i}"));

        /*
          UNA SOLA IDA Y VUELTA, TRES RESULTADOS.

          Son tres consultas independientes (usuarios, claves por rol, ajustes
          por usuario) y se mandan juntas porque esto corre dentro de un PUT:
          contra la base de la oficina cada viaje cuesta decenas de
          milisegundos, y la auditoria de rendimiento de este mismo archivo ya
          enseno que el coste esta en el viaje, no en la consulta.
        */
        var sql = $@"
SELECT u.Id, u.Usuario, u.Rol
FROM dbo.UsuarioWeb u
WHERE u.EmpresaId = @EmpresaId
  AND u.Estado = 1;

SELECT UPPER(r.Codigo) AS Codigo, p.Clave
FROM dbo.WRol r
INNER JOIN dbo.WRolPermiso rp
    ON rp.EmpresaId = r.EmpresaId
   AND rp.{Q(schema.RolePermRoleIdColumn)} = r.{Q(schema.RoleIdColumn)}
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = rp.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = rp.{Q(schema.RolePermPermissionIdColumn)}
WHERE r.EmpresaId = @EmpresaId
  AND p.Clave IN ({claveDeGobierno});

SELECT up.{Q(schema.UserPermUserIdColumn)} AS UsuarioWebId, p.Clave, LOWER(up.Tipo) AS Tipo
FROM dbo.WUsuarioPermiso up
INNER JOIN dbo.WPermiso p
    ON p.EmpresaId = up.EmpresaId
   AND p.{Q(schema.PermissionIdColumn)} = up.{Q(schema.UserPermPermissionIdColumn)}
WHERE up.EmpresaId = @EmpresaId
  AND p.Clave IN ({claveDeGobierno});";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        var indice = 0;
        foreach (var clave in LlavesDeGobierno.Todas)
            cmd.Parameters.Add(new SqlParameter($"@g{indice++}", SqlDbType.NVarChar, 200) { Value = clave });

        var usuarios = new List<(Guid Id, string Usuario, string Rol)>();
        var porRol = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var allowPorUsuario = new Dictionary<Guid, HashSet<string>>();
        var denyPorUsuario = new Dictionary<Guid, HashSet<string>>();

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                usuarios.Add((
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim(),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim()));
            }

            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var codigo = reader.GetString(0).Trim();
                var clave = reader.GetString(1).Trim();
                if (!porRol.TryGetValue(codigo, out var set))
                    porRol[codigo] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(clave);
            }

            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var usuarioId = reader.GetGuid(0);
                var clave = reader.GetString(1).Trim();
                var tipo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var destino = tipo is "permit" or "allow" or "a" ? allowPorUsuario
                    : tipo is "deny" or "block" or "d" ? denyPorUsuario
                    : null;
                if (destino is null) continue;
                if (!destino.TryGetValue(usuarioId, out var set))
                    destino[usuarioId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(clave);
            }
        }

        // Los modulos apagados HOY, mas el que apagaria este cambio.
        var apagados = await LoadInactiveModuleKeysPublicAsync(conn, empresaId, ct);
        if (!string.IsNullOrWhiteSpace(simulacion.ModuloApagado))
            apagados.Add(simulacion.ModuloApagado.Trim());

        // El cambio simulado, aplicado en memoria sobre lo que se acaba de leer.
        if (!string.IsNullOrWhiteSpace(simulacion.RolCodigoReemplazado))
        {
            var codigo = simulacion.RolCodigoReemplazado.Trim().ToUpperInvariant();
            porRol[codigo] = (simulacion.ClavesDelRol ?? Array.Empty<string>())
                .Where(LlavesDeGobierno.EsDeGobierno)
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        if (simulacion.UsuarioReemplazado is Guid usuarioSimulado)
        {
            allowPorUsuario[usuarioSimulado] = (simulacion.AllowDelUsuario ?? Array.Empty<string>())
                .Where(LlavesDeGobierno.EsDeGobierno).Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            denyPorUsuario[usuarioSimulado] = (simulacion.DenyDelUsuario ?? Array.Empty<string>())
                .Where(LlavesDeGobierno.EsDeGobierno).Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var conceden = new List<string>();
        var reactivan = new List<string>();

        foreach (var (id, usuario, rol) in usuarios)
        {
            // El rol que salta toda la comprobacion: cuenta para las dos cosas.
            if (string.Equals(rol, LlavesDeGobierno.RolQueSaltaTodo, StringComparison.OrdinalIgnoreCase))
            {
                conceden.Add(usuario);
                reactivan.Add(usuario);
                continue;
            }

            var codigoRol = ToRoleCode(rol).ToUpperInvariant();
            var efectivas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (porRol.TryGetValue(codigoRol, out var delRol)) efectivas.UnionWith(delRol);
            if (allowPorUsuario.TryGetValue(id, out var allow)) efectivas.UnionWith(allow);
            if (denyPorUsuario.TryGetValue(id, out var deny)) efectivas.ExceptWith(deny);
            efectivas.RemoveWhere(clave => EsDeModuloApagado(clave, apagados));

            if (efectivas.Overlaps(LlavesDeGobierno.Conceder)) conceden.Add(usuario);
            if (efectivas.Overlaps(LlavesDeGobierno.Reactivar)) reactivan.Add(usuario);
        }

        return new AdministracionRestante(conceden, reactivan, NoSePudoComprobar: false);
    }

    /// <summary>
    /// Igual que LoadInactiveModuleKeysAsync, pero devolviendo un conjunto que
    /// el guardia puede modificar para meterle el modulo de la simulacion.
    /// </summary>
    private static async Task<HashSet<string>> LoadInactiveModuleKeysPublicAsync(
        SqlConnection conn,
        int empresaId,
        CancellationToken ct)
    {
        var apagados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!await TableExistsAsync(conn, "WModulo", ct))
            return apagados;

        await using var cmd = new SqlCommand(@"
SELECT ModuloClave
FROM dbo.WModulo
WHERE EmpresaId = @EmpresaId
  AND IdStatus = 2;", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clave = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(clave)) apagados.Add(clave.Trim());
        }
        return apagados;
    }

    /// <summary>
    /// Si ese rol es de los que vienen con la instalacion (WRol.EsSistema = 1).
    /// Devuelve false cuando la columna no existe: en una base sin ella no hay
    /// forma de saberlo y no se inventa.
    /// </summary>
    public async Task<bool> EsRolDeSistemaAsync(int empresaId, string roleCode, CancellationToken ct)
    {
        var codigo = (roleCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo)) return false;

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        if (!await AreCapabilityTablesAvailableAsync(conn, ct)) return false;

        var columnas = await GetTableColumnsAsync(conn, "WRol", ct);
        if (!columnas.Contains("EsSistema")) return false;

        await using var cmd = new SqlCommand(@"
SELECT TOP 1 EsSistema
FROM dbo.WRol
WHERE EmpresaId = @EmpresaId
  AND UPPER(Codigo) = UPPER(@Codigo);", conn);
        cmd.Parameters.Add(new SqlParameter("@EmpresaId", SqlDbType.Int) { Value = empresaId });
        cmd.Parameters.Add(new SqlParameter("@Codigo", SqlDbType.NVarChar, 80) { Value = codigo });
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is not null && valor is not DBNull && Convert.ToInt32(valor) == 1;
    }
}
