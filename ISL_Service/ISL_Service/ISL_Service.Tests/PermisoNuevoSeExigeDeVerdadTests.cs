using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace ISL_Service.Tests;

/// <summary>
/// QUE LAS CLAVES NUEVAS SE EXIJAN DE VERDAD.
///
/// Poner [Authorize(Policy = "perm:almacen_cascos.cancelar")] no sirve de nada si
/// la clave no llega a decidir. Aqui se prueba el mecanismo completo
/// —PermissionPolicyProvider que arma la politica desde el nombre, y
/// PermissionAuthorizationHandler que la resuelve contra el snapshot del
/// usuario— con las once claves nuevas.
///
/// No se puede probar contra el backend levantado en esta base: el unico usuario
/// con el que se puede entrar en local es SuperAdmin, y el SuperAdmin pasa todo
/// por diseno (PermissionAuthorizationHandler, primer if). Esta prueba cubre
/// justo el caso que esa cuenta no puede ejercer.
/// </summary>
public class PermisoNuevoSeExigeDeVerdadTests
{
    public static TheoryData<string> ClavesNuevas() => new()
    {
        "almacen_cascos.crear",
        "almacen_cascos.editar",
        "almacen_cascos.cancelar",
        "temporadas.crear",
        "temporadas.editar",
        "temporadas.cancelar",
        "torneos.crear",
        "torneos.editar",
        "torneos.cancelar"
    };

    [Theory]
    [MemberData(nameof(ClavesNuevas))]
    public async Task SinElPermisoNoPasa(string clave)
    {
        var resultado = await Autorizar(clave, permisosDelUsuario: new[] { "otra.cosa" });
        Assert.False(resultado);
    }

    /// <summary>
    /// Con el permiso fino Y la entrada al modulo pasa.
    /// </summary>
    /// <remarks>
    /// La entrada se agrego cuando el backend empezo a exigirla de verdad (ver
    /// <see cref="EntradaAlModuloSeExigeTests"/>). Antes esta prueba pasaba con
    /// el permiso fino a secas, y eso era justo el agujero: con la clave suelta
    /// y sin el modulo concedido, el endpoint contestaba 200.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ClavesNuevas))]
    public async Task ConElPermisoYLaEntradaPasa(string clave)
    {
        var resultado = await Autorizar(clave, permisosDelUsuario: new[] { clave, EntradaDe(clave) });
        Assert.True(resultado);
    }

    /// <summary>
    /// Y con el permiso fino SOLO, no. Sin entrada no se puede, aunque tenga
    /// los especificos.
    /// </summary>
    [Theory]
    [MemberData(nameof(ClavesNuevas))]
    public async Task SinLaEntradaNoPasaAunqueTengaElFino(string clave)
    {
        var resultado = await Autorizar(clave, permisosDelUsuario: new[] { clave });
        Assert.False(resultado);
    }

    private static string EntradaDe(string clave)
    {
        var entradas = ISL_Service.Application.Security.EntradasDeModulo.Requeridas(clave);
        return entradas.Count > 0 ? entradas[0] : clave;
    }

    /// <summary>
    /// Tener el de CREAR no da el de CANCELAR. Es lo que se gana al separarlos:
    /// antes cualquier token hacia las tres cosas.
    /// </summary>
    [Fact]
    public async Task CrearNoDaCancelar()
    {
        Assert.False(await Autorizar("almacen_cascos.cancelar", new[] { "almacen_cascos.crear", "almacen_cascos.editar" }));
        Assert.False(await Autorizar("torneos.cancelar", new[] { "torneos.crear" }));
    }

    /// <summary>
    /// La politica tiene que existir con el nombre exacto que llevan los
    /// atributos. Un "perm:" mal escrito no es un 403: es una politica que no
    /// existe, y eso revienta en caliente.
    /// </summary>
    [Theory]
    [MemberData(nameof(ClavesNuevas))]
    public async Task LaPoliticaExisteConEsePrefijo(string clave)
    {
        var provider = new PermissionPolicyProvider(new Microsoft.Extensions.Options.OptionsWrapper<AuthorizationOptions>(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync($"perm:{clave}");

        Assert.NotNull(policy);

        // La politica pide, ademas del permiso, estar autenticado: por eso se
        // busca el requisito en vez de exigir que sea el unico.
        var requisito = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Contains(clave, requisito.Permissions);
    }

    private static async Task<bool> Autorizar(string claveExigida, IEnumerable<string> permisosDelUsuario)
    {
        var handler = new PermissionAuthorizationHandler(new PermisosFijos(permisosDelUsuario.ToList()));

        var usuario = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("empresaId", "1"),
            new Claim("rolLegacy", "User")
        }, "test"));

        var requisito = new PermissionRequirement(claveExigida);
        var contexto = new AuthorizationHandlerContext(new[] { requisito }, usuario, resource: null);

        await handler.HandleAsync(contexto);
        return contexto.HasSucceeded;
    }

    private sealed class PermisosFijos : IPermissionService
    {
        private readonly List<string> _permisos;

        public PermisosFijos(List<string> permisos) => _permisos = permisos;

        public Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
            => Task.FromResult(new PermissionSnapshot
            {
                UserId = userId,
                EmpresaId = empresaId,
                RolLegacy = rolLegacy,
                PermissionsEnabled = true,
                Permissions = _permisos
            });

        public bool IsAllowedByLegacy(string rolLegacy, string permission) => false;
        public Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int empresaId, string companyKey, bool includeAllTenants, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebModuleItem> SetModuleStatusAsync(int empresaId, string moduleKey, int idStatus, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRoleItem> CreateRoleAsync(int empresaId, string roleCode, string name, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct) => throw new NotImplementedException();
        // La siembra del arranque: en las pruebas no hace nada.
        public Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct) => Task.CompletedTask;

        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
    }
}
