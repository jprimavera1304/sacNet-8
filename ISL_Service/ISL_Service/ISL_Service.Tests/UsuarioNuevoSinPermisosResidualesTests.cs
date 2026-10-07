using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Services;
using ISL_Service.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// AL CREAR UN USUARIO SE LE LIMPIAN LOS OVERRIDES, Y SI ESO FALLA SE TIENE QUE
/// NOTAR.
///
/// El catch que rodeaba esa limpieza no tenia filtro ni registro. Existe por un
/// motivo bueno —en una base sin el esquema de capacidades no hay overrides que
/// limpiar y eso no debe impedir dar de alta a nadie—, pero se comia TODO: si
/// fallaba por un error de la base, el usuario quedaba creado CON los overrides
/// que hubiera y la API contestaba 200. Un usuario nuevo con permisos que nadie
/// le dio, y sin una linea en el registro.
/// </summary>
public class UsuarioNuevoSinPermisosResidualesTests
{
    /// <summary>
    /// Base sin el esquema de capacidades: se crea igual. Es el caso que el
    /// catch venia a cubrir y sigue cubierto.
    /// </summary>
    [Fact]
    public async Task SinEsquemaDeCapacidadesElUsuarioSeCreaIgual()
    {
        var permisos = new PermisosQueFallan(new KeyNotFoundException("Capacidades no disponibles para este tenant/base."));
        var service = NuevoServicio(permisos);

        var resultado = await service.CreateUserAsync(
            new CreateUserRequest { Usuario = "NUEVO", Password = "Secreta2026", Rol = "User" },
            Actor(),
            CancellationToken.None);

        Assert.NotNull(resultado.User);
        Assert.True(permisos.SeIntento);
    }

    /// <summary>
    /// Cualquier otro fallo YA NO se traga: revienta, y la API deja de contestar
    /// 200 a un alta que quedo a medias.
    /// </summary>
    [Fact]
    public async Task SiLaLimpiezaFallaDeVerdadNoSeContestaComoSiNada()
    {
        var permisos = new PermisosQueFallan(new InvalidOperationException("la base dijo no"));
        var service = NuevoServicio(permisos);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(
            new CreateUserRequest { Usuario = "NUEVO", Password = "Secreta2026", Rol = "User" },
            Actor(),
            CancellationToken.None));

        Assert.Equal("la base dijo no", ex.Message);
    }

    private static UserAdminService NuevoServicio(IPermissionService permisos)
        => new(new RepositorioMinimo(), permisos, NullLogger<UserAdminService>.Instance);

    private static ClaimsPrincipal Actor()
        => new(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("empresaId", "1"),
            new Claim("rolLegacy", "SuperAdmin"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "test"));

    private sealed class PermisosQueFallan : IPermissionService
    {
        private readonly Exception _falla;

        public PermisosQueFallan(Exception falla) => _falla = falla;

        public bool SeIntento { get; private set; }

        public Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct)
        {
            SeIntento = true;
            throw _falla;
        }

        public Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct) => throw new NotImplementedException();
        public bool IsAllowedByLegacy(string rolLegacy, string permission) => true;
        public Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int empresaId, string companyKey, bool includeAllTenants, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebModuleItem> SetModuleStatusAsync(int empresaId, string moduleKey, int idStatus, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRoleItem> CreateRoleAsync(int empresaId, string roleCode, string name, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct) => throw new NotImplementedException();
        // La siembra del arranque: en las pruebas no hace nada.
        public Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct) => Task.CompletedTask;

        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RepositorioMinimo : IUserRepository
    {
        public Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult(false);

        public Task<Usuario> UpsertWebAndLegacyAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, string nombre, string rol, bool debeCambiarContrasena, int estado, CancellationToken ct)
            => Task.FromResult(new Usuario
            {
                Id = Guid.NewGuid(),
                EmpresaId = 1,
                UsuarioNombre = usuario,
                Rol = rol
            });

        public Task<List<RoleCatalogItem>> ListRolesCatalogAsync(int empresaId, CancellationToken ct)
            => Task.FromResult(new List<RoleCatalogItem> { new() { Code = "User", Name = "Usuario" } });
        public Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult<Usuario?>(null);
        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Usuario?>(null);
        public Task<WebLoginFallbackResult?> LoginWithFallbackAsync(string usuario, string contrasenaPlano, string? contrasenaHashWeb, CancellationToken ct) => Task.FromResult<WebLoginFallbackResult?>(null);
        public Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct) => Task.FromResult(new List<Usuario>());
        public Task<Dictionary<string, int>> ListLegacyLinksAsync(CancellationToken ct) => Task.FromResult(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        public Task AddAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task<Usuario> UpdateUsuarioAndRolAsync(Guid userId, string usuarioNuevo, string rolNuevo, CancellationToken ct) => throw new NotImplementedException();
        public Task<Usuario> UpdateEstadoWithLegacyAsync(Guid userId, int estado, CancellationToken ct) => throw new NotImplementedException();
        public Task<string?> GetLegacyPasswordAsync(string usuario, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
