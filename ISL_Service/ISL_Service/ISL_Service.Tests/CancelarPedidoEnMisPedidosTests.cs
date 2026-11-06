using System.Reflection;
using System.Security.Claims;
using ISL_Service.Application.DTOs.PedidosOperaciones;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using ISL_Service.Controllers;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ISL_Service.Tests;

/*
  CANCELAR UN PEDIDO DESDE EL MODULO PEDIDOS DEL TELEFONO.

  QUE PREGUNTA CONTESTA
  ---------------------
  "Con la clave nueva del modulo Pedidos del telefono
  —`app_movil.pedidos.cancelar`— se puede cancelar, y quien hoy cancela por las
  dos claves de siempre sigue pudiendo?" Las tres respuestas tienen que ser si,
  y sin la clave tiene que ser 403.

  POR QUE IMPORTA QUE SEAN TRES Y NO UNA
  --------------------------------------
  `ExigirPermisoAsync` pide ALGUNA de la lista. Agregar la tercera no quita
  nada, pero si alguien "ordena" el arreglo dejando solo la nueva, la gente que
  cancela desde la computadora (`ventas.pedidos.cancelar`) o desde Consulta de
  ventas en el telefono (`app_movil.ventas.pedidos.cancelar`) se queda fuera sin
  que nada lo diga. Por eso las tres se prueban una por una.

  Y OJO CON EL NOMBRE: la nueva es `app_movil.pedidos.cancelar`, la hermana de
  `app_movil.pedidos.editar`. NO es `app_movil.ventas.pedidos.cancelar`, que es
  la de Ventas y ya existia. Son dos modulos distintos del telefono, y una
  clave `app_movil.*` que el catalogo no conoce no deja el permiso apagado: le
  esconde la casilla a quien reparte. Por eso aqui tambien se comprueba que la
  clave este SEMBRADA.

  LOS SERVICIOS VAN EN null! A PROPOSITO, igual que en
  IntrusionSieteFuncionesMovilTests: si el guardia hace su trabajo el servicio
  no se toca, y haber pasado el guardia se reconoce porque la llamada llega al
  servicio y truena.

  LAS DOS EMPRESAS: el modelo de autorizacion es el mismo codigo para las dos,
  y esto lo fija.
*/
public class CancelarPedidoEnMisPedidosTests
{
    /// <summary>La clave nueva: el boton Cancelar de "Mis pedidos".</summary>
    private const string ClaveNueva = "app_movil.pedidos.cancelar";

    /// <summary>La de Ventas en el telefono, que ya existia.</summary>
    private const string ClaveDeVentasEnElMovil = "app_movil.ventas.pedidos.cancelar";

    /// <summary>La de la computadora (btnCancelarPedido de Mac31).</summary>
    private const string ClaveDelWeb = "ventas.pedidos.cancelar";

    public static TheoryData<int, string> Empresas() => new() { { 1, "zaragoza" }, { 2, "tauro" } };

    /// <summary>
    /// Lo que lleva un token de la app que NO puede cancelar: entra a la app,
    /// abre el portal de pedidos y puede editar el suyo. Nada mas.
    /// </summary>
    private static readonly string[] SoloLoBasicoDeLaApp =
    {
        "app_movil.acceso",
        "app_movil.pedidos",
        "app_movil.pedidos.editar"
    };

    // ==================================================================
    // El guardia del endpoint
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Sin_Ninguna_De_Las_Tres_Es_403(int empresaId, string companyKey)
    {
        var c = Pedidos(SoloLoBasicoDeLaApp, empresaId, companyKey);

        var r = await c.CancelarAsync(new PedidosCancelarRequest { IdsPedido = new List<int> { 1 } }, default);

        var obj = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    /// <summary>
    /// LA CLAVE NUEVA BASTA, Y SOLA: el repartidor cancela su pedido sin que
    /// haya que darle Ventas ni el permiso de la computadora, que es todo el
    /// motivo de que esta clave exista.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Con_La_Clave_Nueva_Del_Modulo_Pedidos_Pasa(int empresaId, string companyKey)
    {
        var c = Pedidos(SoloLoBasicoDeLaApp.Append(ClaveNueva).ToArray(), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() =>
            c.CancelarAsync(new PedidosCancelarRequest { IdsPedido = new List<int> { 1 } }, default)));
    }

    /// <summary>La de Ventas en el telefono sigue valiendo: nadie pierde nada.</summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Con_La_Clave_De_Ventas_En_El_Movil_Sigue_Pasando(int empresaId, string companyKey)
    {
        var c = Pedidos(SoloLoBasicoDeLaApp.Append(ClaveDeVentasEnElMovil).ToArray(), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() =>
            c.CancelarAsync(new PedidosCancelarRequest { IdsPedido = new List<int> { 1 } }, default)));
    }

    /// <summary>
    /// Y la de la computadora tambien, con su token de canal web. Va con
    /// `ventas.ver_modulo` porque esa familia si tiene puerta de modulo
    /// (EntradasDeModulo): es como entra de verdad quien cancela desde el web.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Con_La_Clave_Del_Web_Sigue_Pasando(int empresaId, string companyKey)
    {
        var c = Pedidos(
            new[] { ClaveDelWeb, "ventas.ver_modulo" }, empresaId, companyKey, CanalDeAcceso.ValorWeb);

        Assert.True(await PasaElGuardia(() =>
            c.CancelarAsync(new PedidosCancelarRequest { IdsPedido = new List<int> { 1 } }, default)));
    }

    /// <summary>
    /// DECLARAR CANAL WEB NO SIRVE PARA ESQUIVAR NADA: con la clave nueva pero
    /// un token de navegador, las `app_movil.*` no cuentan (CanalDeAcceso.Filtrar).
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task La_Clave_Nueva_No_Vale_Con_Token_De_Canal_Web(int empresaId, string companyKey)
    {
        var c = Pedidos(new[] { ClaveNueva }, empresaId, companyKey, CanalDeAcceso.ValorWeb);

        var r = await c.CancelarAsync(new PedidosCancelarRequest { IdsPedido = new List<int> { 1 } }, default);

        var obj = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    // ==================================================================
    // Lo que declara el codigo
    // ==================================================================

    /// <summary>
    /// Las TRES, exactamente, en el arreglo del controlador. Si alguien quita
    /// una, esto dice cual.
    /// </summary>
    [Fact]
    public void El_Arreglo_Del_Controlador_Trae_Las_Tres()
    {
        var campo = typeof(PedidosOperacionesController)
            .GetField("PermisoCancelar", BindingFlags.NonPublic | BindingFlags.Static)!;
        var claves = (string[])campo.GetValue(null)!;

        Assert.Contains(ClaveDelWeb, claves);
        Assert.Contains(ClaveDeVentasEnElMovil, claves);
        Assert.Contains(ClaveNueva, claves);
        Assert.Equal(3, claves.Length);
    }

    /// <summary>
    /// Y la nueva SEMBRADA. Sin esto la clave no existe en WPermiso, no se
    /// puede conceder a nadie y su casilla no sale en el panel: el fallo
    /// silencioso de siempre.
    /// </summary>
    [Fact]
    public void La_Clave_Nueva_Esta_Sembrada_En_El_Catalogo()
    {
        var campo = typeof(PermissionService).GetField("PermissionCatalogSeeds",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var claves = new List<string>();
        foreach (var item in (System.Collections.IEnumerable)campo.GetValue(null)!)
        {
            if (item?.GetType().GetProperty("Key")?.GetValue(item) is string k) claves.Add(k);
        }

        Assert.Contains(ClaveNueva, claves);
        // La hermana sigue ahi: son las dos casillas de la misma tarjeta.
        Assert.Contains("app_movil.pedidos.editar", claves);
    }

    /// <summary>
    /// El modulo tiene que ser `app_movil`, como el resto del canal: es lo que
    /// mete la clave en la pestaña Movil del panel de permisos.
    /// </summary>
    [Fact]
    public void La_Clave_Nueva_Se_Siembra_En_El_Modulo_Del_Movil()
    {
        var campo = typeof(PermissionService).GetField("PermissionCatalogSeeds",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        string? modulo = null;
        foreach (var item in (System.Collections.IEnumerable)campo.GetValue(null)!)
        {
            if (item?.GetType().GetProperty("Key")?.GetValue(item) is not string k) continue;
            if (!string.Equals(k, ClaveNueva, StringComparison.Ordinal)) continue;
            modulo = item.GetType().GetProperty("Module")?.GetValue(item) as string;
        }

        Assert.Equal("app_movil", modulo);
    }

    /// <summary>
    /// Una clave `app_movil.*` no lleva puerta de modulo, asi que la nueva
    /// decide sola. Si alguien le pusiera una, el repartidor volveria a
    /// necesitar un permiso del web para cancelar — justo lo que se arregla.
    /// </summary>
    [Fact]
    public void La_Clave_Nueva_No_Pide_Entrada_De_Modulo()
    {
        Assert.Empty(EntradasDeModulo.Requeridas(ClaveNueva));
    }

    // ==================================================================
    // Andamio
    // ==================================================================

    private static async Task<bool> PasaElGuardia(Func<Task<IActionResult>> accion)
    {
        try
        {
            var r = await accion();
            return !(r is ObjectResult o && o.StatusCode == StatusCodes.Status403Forbidden);
        }
        catch (NullReferenceException)
        {
            return true;
        }
    }

    private static PedidosOperacionesController Pedidos(
        string[] permisos, int empresaId, string companyKey, string canal = CanalDeAcceso.ValorMovil)
        => new(null!, new AccesorDeEmpresa(empresaId, companyKey), new PermisosFijos(permisos))
        { ControllerContext = Contexto(empresaId, canal) };

    private static ControllerContext Contexto(int empresaId, string canal)
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("empresaId", empresaId.ToString()),
            new("rolLegacy", "User"),      // NO SuperAdmin: el SuperAdmin pasa todo.
            new("idUsuario", "1125"),
            new(CanalDeAcceso.Claim, canal)
        };

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "pruebas"))
            }
        };
    }

    private sealed class AccesorDeEmpresa : ICurrentUserAccessor
    {
        private readonly int _empresaId;
        private readonly string _companyKey;

        public AccesorDeEmpresa(int empresaId, string companyKey)
        {
            _empresaId = empresaId;
            _companyKey = companyKey;
        }

        public Guid? GetUserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirst("sub")!.Value);
        public int GetLegacyUserId(ClaimsPrincipal user) => 1125;
        public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema") => "QA";
        public int? GetCompanyId(ClaimsPrincipal user) => _empresaId;
        public string? GetCompanyKey(ClaimsPrincipal user) => _companyKey;
        public string GetRole(ClaimsPrincipal user) => "User";
    }

    private sealed class PermisosFijos : IPermissionService
    {
        private readonly IReadOnlyCollection<string> _permisos;

        public PermisosFijos(IReadOnlyCollection<string> permisos) => _permisos = permisos;

        public Task<PermissionSnapshot> GetPermissionsAsync(
            Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
            => Task.FromResult(new PermissionSnapshot
            {
                UserId = userId,
                EmpresaId = empresaId,
                RolLegacy = rolLegacy,
                PermissionsEnabled = true,
                Permissions = _permisos.ToList()
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
        public Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, bool sincronizarConLegacy, CancellationToken ct) => throw new NotImplementedException();
        public Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct) => Task.CompletedTask;
        public void InvalidarCacheDeUsuario(int empresaId, Guid userId) { }
        public void InvalidarCacheDeEmpresa(int empresaId) { }
        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(int empresaId, Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<AdministracionRestante> GetAdministracionRestanteAsync(int empresaId, SimulacionDePermisos simulacion, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> EsRolDeSistemaAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
    }
}
