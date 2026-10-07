using System.Reflection;
using System.Security.Claims;
using ISL_Service.Application.DTOs.Requests;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Services;
using ISL_Service.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Tests;

/// <summary>
/// RECIBIR UN DATO Y TIRARLO EN SILENCIO NO ES UNA OPCION.
///
/// La auditoria final encontro cuatro endpoints que aceptaban parametros y no
/// los usaban en ninguna linea. El que llama cree que filtro, y nada en la
/// respuesta le dice lo contrario: devuelven 200 con todo sin filtrar, o 200 sin
/// haber modificado nada.
///
/// Cada uno se resolvio de una de las dos maneras honestas —implementar el filtro
/// o quitarlo del contrato— y aqui se fija la decision, para que no vuelva a
/// aparecer un parametro decorativo al refactorizar.
/// </summary>
public class Ola10FiltrosQueNoSeTiranTests
{
    /// <summary>
    /// El tablero aceptaba 11 filtros en `filtros/consultar` y el repositorio no
    /// tocaba `request`: sp_w_ConsultarDashboardVentasFiltros no declara ni un
    /// parametro, asi que no hay nada que filtrar. Se quito del contrato.
    /// </summary>
    [Fact]
    public void Los_Catalogos_Del_Tablero_Ya_No_Piden_Filtros()
    {
        var metodo = typeof(IDashboardVentasRepository).GetMethod("ConsultarFiltrosAsync")!;
        var tipos = metodo.GetParameters().Select(p => p.ParameterType).ToArray();

        Assert.Equal(new[] { typeof(CancellationToken) }, tipos);
        Assert.DoesNotContain(typeof(ISL_Service.Application.DTOs.DashboardVentas.DashboardVentasFiltroRequest), tipos);
    }

    [Fact]
    public void La_Accion_De_Catalogos_Del_Tablero_Ya_No_Declara_Cuerpo()
    {
        var accion = typeof(ISL_Service.Controllers.DashboardVentasController)
            .GetMethod("ConsultarFiltros")!;

        Assert.DoesNotContain(accion.GetParameters(),
            p => p.GetCustomAttribute<FromBodyAttribute>() is not null);
    }

    /// <summary>
    /// El PUT de unidades territoriales exigia un cuerpo con clave, idStatus y
    /// nombre, y el repositorio mandaba solo el id al SP. Renombrar era un no-op
    /// con 200. Se quito el cuerpo del contrato.
    /// </summary>
    [Fact]
    public void El_Put_De_Unidades_Territoriales_Ya_No_Pide_Cuerpo()
    {
        var accion = typeof(ISL_Service.Controllers.CatalogosAdminController)
            .GetMethod("ActualizarUnidadTerritorial")!;

        Assert.DoesNotContain(accion.GetParameters(),
            p => p.GetCustomAttribute<FromBodyAttribute>() is not null);

        var repo = typeof(ICatalogosAdminRepository).GetMethod("ActualizarUnidadTerritorialAsync")!;
        Assert.Equal(new[] { typeof(int), typeof(CancellationToken) },
            repo.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    /// <summary>
    /// `PATCH /usuarios/{id}/empresa` contestaba 500 "Error interno." a
    /// cualquier cuerpo, porque devolvia un InvalidOperationException que el
    /// middleware no clasifica. Ahora: la misma empresa es 200 idempotente, otra
    /// empresa es 409.
    /// </summary>
    [Fact]
    public async Task Cambiar_A_La_Misma_Empresa_Devuelve_El_Usuario_Sin_Reventar()
    {
        var id = Guid.NewGuid();
        var service = Servicio(new Usuario { Id = id, UsuarioNombre = "ADMIN", Rol = "Admin", EmpresaId = 1, Estado = 1 });

        var resp = await service.UpdateEmpresaAsync(id, new UpdateUserEmpresaRequest { EmpresaId = 1 }, SuperAdmin(), CancellationToken.None);

        Assert.Equal(1, resp.EmpresaId);
        Assert.Equal("ADMIN", resp.Usuario);
    }

    [Fact]
    public async Task Cambiar_A_Otra_Empresa_Es_Conflicto_Y_No_Error_Interno()
    {
        var id = Guid.NewGuid();
        var service = Servicio(new Usuario { Id = id, UsuarioNombre = "ADMIN", Rol = "Admin", EmpresaId = 1, Estado = 1 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateEmpresaAsync(id, new UpdateUserEmpresaRequest { EmpresaId = 7 }, SuperAdmin(), CancellationToken.None));

        // 409, no 500: no es una averia del servidor, es una peticion que este
        // modelo no puede cumplir.
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("una sola empresa", ex.Message);
    }

    [Fact]
    public async Task Cambiar_Sin_EmpresaId_Es_Peticion_Mala()
    {
        var id = Guid.NewGuid();
        var service = Servicio(new Usuario { Id = id, UsuarioNombre = "ADMIN", Rol = "Admin", EmpresaId = 1, Estado = 1 });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateEmpresaAsync(id, new UpdateUserEmpresaRequest { EmpresaId = 0 }, SuperAdmin(), CancellationToken.None));
    }

    [Fact]
    public async Task Cambiar_La_Empresa_De_Un_Usuario_Que_No_Existe_Es_404()
    {
        var service = Servicio(null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateEmpresaAsync(Guid.NewGuid(), new UpdateUserEmpresaRequest { EmpresaId = 1 }, SuperAdmin(), CancellationToken.None));
    }

    // ------------------------------------------------------------------ ayudas

    private static UserAdminService Servicio(Usuario? usuario)
        => new(new RepoDeUnSoloUsuario(usuario), null!, new NullLogger<UserAdminService>());

    private static ClaimsPrincipal SuperAdmin()
        => new(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("rolLegacy", "SuperAdmin"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim("empresaId", "1")
        }, "test"));

    private sealed class NullLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { }
    }

    private sealed class RepoDeUnSoloUsuario : IUserRepository
    {
        private readonly Usuario? _usuario;

        public RepoDeUnSoloUsuario(Usuario? usuario) => _usuario = usuario;

        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_usuario);

        public Task<Usuario?> GetByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult<Usuario?>(null);
        public Task<WebLoginFallbackResult?> LoginWithFallbackAsync(string usuario, string contrasenaPlano, string? contrasenaHashWeb, CancellationToken ct) => Task.FromResult<WebLoginFallbackResult?>(null);
        public Task<bool> ExistsByUsuarioAsync(string usuario, CancellationToken ct) => Task.FromResult(false);
        public Task<List<RoleCatalogItem>> ListRolesCatalogAsync(int empresaId, CancellationToken ct) => Task.FromResult(new List<RoleCatalogItem>());
        public Task<List<Usuario>> ListAsync(int? empresaId, CancellationToken ct) => Task.FromResult(new List<Usuario>());
        public Task<Dictionary<string, int>> ListLegacyLinksAsync(CancellationToken ct) => Task.FromResult(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        public Task AddAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Usuario user, CancellationToken ct) => Task.CompletedTask;
        public Task<Usuario> UpsertWebAndLegacyAsync(string usuario, string contrasenaPlano, string contrasenaHashWeb, string nombre, string rol, bool debeCambiarContrasena, int estado, CancellationToken ct) => Task.FromResult(new Usuario());
        public Task<Usuario> UpdateUsuarioAndRolAsync(Guid userId, string usuarioNuevo, string rolNuevo, CancellationToken ct) => Task.FromResult(new Usuario());
        public Task<Usuario> UpdateEstadoWithLegacyAsync(Guid userId, int estado, CancellationToken ct) => Task.FromResult(new Usuario());
        public Task<string?> GetLegacyPasswordAsync(string usuario, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
