using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// EL ATAJO QUE DABA LA VUELTA A LA PANTALLA DE PERMISOS.
/// </summary>
/// <remarks>
/// GuardiaDePermisos ya impedia concederse una clave desde la pantalla de
/// permisos, pero un rol es un paquete de claves y la pantalla de USUARIOS no
/// pasaba por ningun techo: bastaba nombrarse —o nombrar a alguien— un rol que
/// ya traiga la clave. Medido en MacZ el 2026-10-12: el rol ADMIN guarda 17
/// claves y SUPER_ADMIN 29, asi que el atajo valia de verdad.
///
/// Las pruebas de aqui llaman al guardia directo, sin base: lo que se decide
/// es una comparacion de conjuntos, y meterle SQL solo haria que se saltaran
/// cuando no hay servidor.
/// </remarks>
public class TechoDeRolAlAsignarTests
{
    private static GuardiaDePermisos Guardia(string[] delLlamante, Dictionary<string, string[]> roles) =>
        new(new PermisosDeMentira(delLlamante, roles), new AccesoDeMentira(), NullLogger<GuardiaDePermisos>.Instance);

    private static ClaimsPrincipal Alguien(string rol) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, rol)
    }, "prueba"));

    [Fact]
    public async Task No_puede_asignar_un_rol_con_claves_que_no_tiene()
    {
        var guardia = Guardia(
            delLlamante: new[] { "usuarios.editar", "ventas.ver" },
            roles: new() { ["ADMIN"] = new[] { "usuarios.editar", "permisosweb.roles.editar" } });

        var ex = await Assert.ThrowsAsync<EscaladaDePermisosException>(() =>
            guardia.ExigirPuedeAsignarRolAsync(Alguien("Admin"), 1, "USER", "ADMIN", default));

        Assert.Contains("permisosweb.roles.editar", ex.Message);
    }

    [Fact]
    public async Task Si_tiene_todo_lo_que_el_rol_trae_el_cambio_pasa()
    {
        var guardia = Guardia(
            delLlamante: new[] { "usuarios.editar", "permisosweb.roles.editar", "ventas.ver" },
            roles: new() { ["ADMIN"] = new[] { "usuarios.editar", "permisosweb.roles.editar" } });

        await guardia.ExigirPuedeAsignarRolAsync(Alguien("Admin"), 1, "USER", "ADMIN", default);
    }

    [Fact]
    public async Task Tampoco_puede_degradar_un_rol_cuyas_claves_no_podria_devolver()
    {
        // Quitar lo que no puedes devolver es la forma de desarmar a quien
        // esta por encima: el mismo motivo por el que el techo de los ajustes
        // individuales mira la diferencia simetrica y no solo lo que entra.
        var guardia = Guardia(
            delLlamante: new[] { "usuarios.editar" },
            roles: new() { ["ADMIN"] = new[] { "permisosweb.roles.editar" } });

        var ex = await Assert.ThrowsAsync<EscaladaDePermisosException>(() =>
            guardia.ExigirPuedeAsignarRolAsync(Alguien("Admin"), 1, "ADMIN", "USER", default));

        Assert.Contains("no podrias devolverlos", ex.Message);
    }

    [Theory]
    [InlineData("SUPER_ADMIN", "SuperAdmin")]
    [InlineData("Admin", "ADMIN")]
    [InlineData("super admin", "SUPER_ADMIN")]
    public async Task Editar_a_alguien_sin_cambiarle_el_rol_no_pide_nada(string actual, string nuevo)
    {
        // Los PUT de usuarios mandan el renglon completo, asi que corregirle el
        // NOMBRE a un ADMIN vuelve a mandar su rol. Si eso pidiera el techo, un
        // administrador normal no podria ni arreglar una falta de ortografia.
        var guardia = Guardia(
            delLlamante: Array.Empty<string>(),
            roles: new() { ["SUPER_ADMIN"] = new[] { "todo.lo.que.existe" }, ["ADMIN"] = new[] { "otra" } });

        await guardia.ExigirPuedeAsignarRolAsync(Alguien("User"), 1, actual, nuevo, default);
    }

    [Fact]
    public async Task Un_SuperAdmin_no_tiene_techo()
    {
        // Su snapshot puede venir vacio porque la autorizacion no lo mira:
        // aplicarle un techo sacado de ahi le impediria todo.
        var guardia = Guardia(
            delLlamante: Array.Empty<string>(),
            roles: new() { ["ADMIN"] = new[] { "permisosweb.roles.editar" } });

        await guardia.ExigirPuedeAsignarRolAsync(Alguien("SuperAdmin"), 1, "USER", "ADMIN", default);
    }

    private sealed class AccesoDeMentira : ICurrentUserAccessor
    {
        public Guid? GetUserId(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        public int GetLegacyUserId(ClaimsPrincipal user) => 0;
        public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema") => fallback;
        public int? GetCompanyId(ClaimsPrincipal user) => 1;
        public string? GetCompanyKey(ClaimsPrincipal user) => "MACZ";
        public string GetRole(ClaimsPrincipal user) => user.FindFirst(ClaimTypes.Role)?.Value ?? "User";
    }

    /// <summary>
    /// Solo contesta de verdad las dos preguntas que el techo hace. El resto
    /// revienta a proposito: si una prueba futura las necesitara, es mejor que
    /// falle que no que decida con datos inventados.
    /// </summary>
    private sealed class PermisosDeMentira : IPermissionService
    {
        private readonly string[] _delLlamante;
        private readonly Dictionary<string, string[]> _roles;

        public PermisosDeMentira(string[] delLlamante, Dictionary<string, string[]> roles)
        {
            _delLlamante = delLlamante;
            _roles = new Dictionary<string, string[]>(roles, StringComparer.OrdinalIgnoreCase);
        }

        public Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
            => Task.FromResult(new PermissionSnapshot
            {
                UserId = userId,
                EmpresaId = empresaId,
                RolLegacy = rolLegacy,
                PermissionsEnabled = true,
                Permissions = _delLlamante.ToList()
            });

        public Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(int empresaId, string roleCode, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<string>>(
                _roles.TryGetValue(roleCode ?? string.Empty, out var claves) ? claves : Array.Empty<string>());

        public bool IsAllowedByLegacy(string rolLegacy, string permission) => false;
        public Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int e, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int e, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int e, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int e, string k, bool a, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int e, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebModuleItem> SetModuleStatusAsync(int e, string m, int s, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRoleItem> CreateRoleAsync(int e, string c, string n, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertRolePermissionsAsync(int e, string c, IReadOnlyCollection<string> p, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertUserOverridesAsync(int e, Guid u, IReadOnlyCollection<string> a, IReadOnlyCollection<string> d, bool s, CancellationToken ct) => throw new NotImplementedException();
        public Task SembrarCatalogoCompletoAsync(int e, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int e, IReadOnlyCollection<string>? m, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int e, string k, string n, string? d, CancellationToken ct) => throw new NotImplementedException();
        public Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(int e, Guid u, CancellationToken ct) => throw new NotImplementedException();
        public Task<AdministracionRestante> GetAdministracionRestanteAsync(int e, SimulacionDePermisos s, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> EsRolDeSistemaAsync(int e, string c, CancellationToken ct) => throw new NotImplementedException();
    }
}
