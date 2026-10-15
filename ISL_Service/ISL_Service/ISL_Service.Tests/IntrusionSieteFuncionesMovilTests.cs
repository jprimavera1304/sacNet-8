using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.DTOs.VentasDevolucion;
using ISL_Service.Application.DTOs.VentasMultiPago;
using ISL_Service.Application.DTOs.VentasUsados;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using ISL_Service.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ISL_Service.Tests;

/*
  PRUEBAS DE INTRUSION DE LAS SIETE FUNCIONES DE VENTAS DEL TELEFONO.

  QUE PREGUNTA CONTESTAN
  ----------------------
  "Con un token VALIDO de la app —firmado, canal movil, con la llave maestra
  `app_movil.acceso`— pero SIN la clave de la accion, se puede ejecutar la
  accion llamando a la API a mano?" La respuesta tiene que ser 403 en las
  siete, y es un bloqueo de publicacion si alguna contesta otra cosa.

  Es el caso que la cuenta de administrador NO puede ejercer: el SuperAdmin
  pasa todo por diseno (PermisoControllerBase.LeerPermisosAsync, el primer if),
  asi que quien pruebe la app con su cuenta ve todo funcionar y no ve el
  agujero. Por eso esto se prueba aqui y no a mano.

  POR QUE SE LLAMA AL METODO DEL CONTROLADOR Y NO A UN DOBLE
  ---------------------------------------------------------
  Porque el agujero que se busca es exactamente "el endpoint existe y no
  comprueba". Una prueba sobre la tabla de claves diria que la clave esta
  declarada, que es otra cosa: `ClavesDePermisoSembradasTests` ya lo cubre.
  Aqui se invoca la accion real, con su `ExigirPermisoAsync` real y el filtro
  de canal real, y se mira QUE DEVUELVE.

  LOS SERVICIOS VAN EN null! A PROPOSITO
  --------------------------------------
  Si el guardia hace su trabajo, el servicio no se llega a tocar nunca: el
  metodo sale con el 403 antes. Pasar `null!` convierte "se me colo" en un
  reventon ruidoso en vez de en un 200 silencioso con datos inventados por un
  doble. En la prueba POSITIVA se usa eso mismo al reves: haber pasado del
  guardia se reconoce porque la llamada llega al servicio y truena.

  LAS DOS EMPRESAS
  ----------------
  Cada caso corre con empresaId 1 / "zaragoza" y con 2 / "tauro". El modelo de
  autorizacion es el mismo codigo para las dos —no hay rama por empresa en
  ExigirPermisoAsync— y esto lo fija: si alguien mete un atajo "en Tauro no se
  comprueba", esta prueba lo caza. Lo que cambia por empresa son las FILAS de
  WPermiso, y eso lo vigilan las pruebas contra la base.
*/
public class IntrusionSieteFuncionesMovilTests
{
    /// <summary>Las dos empresas, como las ve el token.</summary>
    public static TheoryData<int, string> Empresas() => new() { { 1, "zaragoza" }, { 2, "tauro" } };

    /// <summary>
    /// Lo que lleva un token de la app que NO tiene la clave de la accion:
    /// entra a la app y puede trabajar en pedidos, pero nada mas.
    /// </summary>
    private static readonly string[] SoloLoBasicoDeLaApp =
    {
        "app_movil.acceso",
        "app_movil.pedidos",
        "app_movil.ventas.pedidos.procesar"
    };

    // ==================================================================
    // 1. FACTURAR  ->  app_movil.ventas.facturar
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Facturar_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = Facturacion(SoloLoBasicoDeLaApp, empresaId, companyKey);

        // Preparar el CFDI en el servidor...
        Negado(await c.PrepararAsync(new ISL_Service.Application.DTOs.VentasFacturacion.FacturaPrepararRequest(), default));
        // ...y timbrarlo.
        Negado(await c.TimbrarAsync(new ISL_Service.Application.DTOs.VentasFacturacion.FacturaTimbrarRequest(), default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Facturar_Con_La_Clave_Movil_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = Facturacion(Con("app_movil.ventas.facturar"), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() => c.PrepararAsync(
            new ISL_Service.Application.DTOs.VentasFacturacion.FacturaPrepararRequest(), default)));
    }

    // ==================================================================
    // 2. MULTI PAGO  ->  app_movil.ventas.pagos.multi
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task MultiPago_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = new VentasMultiPagoController(null!, Accesor(empresaId, companyKey), Permisos(SoloLoBasicoDeLaApp))
        { ControllerContext = Contexto(empresaId) };

        Negado(await c.Pantalla(new VentasMultiPagoPantallaRequest(), default));
        Negado(await c.Guardar(new VentasMultiPagoGuardarRequest(), default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task MultiPago_Con_La_Clave_Movil_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = new VentasMultiPagoController(
            null!, Accesor(empresaId, companyKey), Permisos(Con("app_movil.ventas.pagos.multi")))
        { ControllerContext = Contexto(empresaId) };

        Assert.True(await PasaElGuardia(() => c.Pantalla(new VentasMultiPagoPantallaRequest(), default)));
    }

    // ==================================================================
    // 3. MODIFICAR PEDIDO  ->  app_movil.ventas.pedidos.modificar
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task ModificarPedido_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = Pedidos(SoloLoBasicoDeLaApp, empresaId, companyKey);

        Negado(await c.AjustarUsadosCreditoAsync(
            new ISL_Service.Application.DTOs.PedidosOperaciones.PedidoUsadoCreditoRequest(), default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task ModificarPedido_Con_La_Clave_Movil_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = Pedidos(Con("app_movil.ventas.pedidos.modificar"), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() => c.AjustarUsadosCreditoAsync(
            new ISL_Service.Application.DTOs.PedidosOperaciones.PedidoUsadoCreditoRequest(), default)));
    }

    // ==================================================================
    // 4. DEVOLUCION  ->  app_movil.ventas.devolucion
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Devolucion_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = new VentasDevolucionController(null!, Accesor(empresaId, companyKey), Permisos(SoloLoBasicoDeLaApp))
        { ControllerContext = Contexto(empresaId) };

        // Ni la pantalla (que ya ensena importes) ni la escritura.
        Negado(await c.Vista(1, default));
        Negado(await c.Devolver(new VentasDevolucionRequest(), default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Devolucion_Con_La_Clave_Movil_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = new VentasDevolucionController(
            null!, Accesor(empresaId, companyKey), Permisos(Con("app_movil.ventas.devolucion")))
        { ControllerContext = Contexto(empresaId) };

        Assert.True(await PasaElGuardia(() => c.Vista(1, default)));
    }

    // ==================================================================
    // 5. USADOS  ->  app_movil.ventas.usados / .usados.guardar
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Usados_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = Usados(SoloLoBasicoDeLaApp, empresaId, companyKey);

        Negado(await c.Consultar(new VentasUsadosConsultarRequest(), default));
        Negado(await c.Guardar(new VentasUsadosGuardarRequest(), default));
    }

    /// <summary>
    /// ABRIR Y GUARDAR SON DOS DECISIONES. En Mac31 son dos botones
    /// (btnUsados / btnAgregarUsadoCredito) y aqui tambien: con la clave de
    /// consultar se abre la pantalla, y guardar sigue siendo 403.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Usados_Consultar_No_Da_Guardar(int empresaId, string companyKey)
    {
        var c = Usados(Con("app_movil.ventas.usados"), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() => c.Consultar(new VentasUsadosConsultarRequest(), default)));
        Negado(await c.Guardar(new VentasUsadosGuardarRequest(), default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Usados_Guardar_Con_Su_Clave_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = Usados(Con("app_movil.ventas.usados.guardar"), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() => c.Guardar(new VentasUsadosGuardarRequest(), default)));
    }

    // ==================================================================
    // 6. SIMULAR  ->  app_movil.ventas.simular.con_remision / .sin_remision
    // ==================================================================

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Simular_Sin_La_Clave_Es_403(int empresaId, string companyKey)
    {
        var c = Consulta(SoloLoBasicoDeLaApp, empresaId, companyKey);

        // Con remision (IdVenta positivo)...
        Negado(await c.Simulacion(new VentasSimulacionRequest { IdVenta = 10 }, default));
        // ...sin remision (IdPedido, que se manda en negativo)...
        Negado(await c.Simulacion(new VentasSimulacionRequest { IdPedido = 10 }, default));
        // ...y los datos de la pantalla, que ya traen la cuenta completa.
        Negado(await c.SimulacionDatos(new VentasSimulacionRequest { IdVenta = 10 }, default));
    }

    /// <summary>
    /// LAS DOS SIMULACIONES SON CLAVES DISTINTAS, y tienen que seguir
    /// siendolo: en Mac31 simular un PEDIDO (sin remision) y simular una
    /// REMISION son dos botones con su propio permiso.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Simular_Con_Remision_No_Da_Simular_Sin_Remision(int empresaId, string companyKey)
    {
        var c = Consulta(Con("app_movil.ventas.simular.con_remision"), empresaId, companyKey);

        // La que SI tiene: devuelve la URL firmada del reporte 1175.
        Assert.IsType<OkObjectResult>(
            await c.Simulacion(new VentasSimulacionRequest { IdVenta = 10 }, default));

        // La que NO tiene.
        Negado(await c.Simulacion(new VentasSimulacionRequest { IdPedido = 10 }, default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Simular_Sin_Remision_No_Da_Simular_Con_Remision(int empresaId, string companyKey)
    {
        var c = Consulta(Con("app_movil.ventas.simular.sin_remision"), empresaId, companyKey);

        Assert.IsType<OkObjectResult>(
            await c.Simulacion(new VentasSimulacionRequest { IdPedido = 10 }, default));

        Negado(await c.Simulacion(new VentasSimulacionRequest { IdVenta = 10 }, default));
    }

    // ==================================================================
    // 7. RECHAZAR  ->  app_movil.ventas.pedidos.rechazar
    // ==================================================================

    /// <summary>
    /// RECHAZAR ES LA BANDERA `rechazar: true` DE `ventas/procesar`, y es el
    /// caso mas facil de colar: el token tiene la clave de PROCESAR, que es la
    /// del endpoint, y lo que quiere hacer es otra cosa. Si la bandera no se
    /// cobrara aparte, cualquiera que pueda procesar podria rechazar.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Rechazar_Sin_La_Clave_Es_403_Aunque_Pueda_Procesar(int empresaId, string companyKey)
    {
        var c = Pedidos(SoloLoBasicoDeLaApp, empresaId, companyKey);

        Negado(await c.ProcesarVentasAsync(
            new ISL_Service.Application.DTOs.PedidosOperaciones.PedidosProcesarVentaRequest
            {
                IdsPedido = new List<int> { 1 },
                Rechazar = true
            }, default));
    }

    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Rechazar_Con_La_Clave_Movil_Pasa_El_Guardia(int empresaId, string companyKey)
    {
        var c = Pedidos(Con("app_movil.ventas.pedidos.rechazar"), empresaId, companyKey);

        Assert.True(await PasaElGuardia(() => c.ProcesarVentasAsync(
            new ISL_Service.Application.DTOs.PedidosOperaciones.PedidosProcesarVentaRequest
            {
                IdsPedido = new List<int> { 1 },
                Rechazar = true
            }, default)));
    }

    // ==================================================================
    // Y EL OTRO LADO DE LA REGLA: la clave movil presentada desde un
    // token de canal WEB no cuenta.
    // ==================================================================

    /// <summary>
    /// El que se declara web para esquivar `app_movil.acceso` no se lleva las
    /// claves del telefono: en canal web se filtran (CanalDeAcceso.Filtrar), y
    /// el guardia de estos endpoints las ve ya filtradas. Sigue siendo 403.
    /// </summary>
    [Theory]
    [MemberData(nameof(Empresas))]
    public async Task Las_Claves_Moviles_No_Cuentan_En_Un_Token_De_Canal_WEB(int empresaId, string companyKey)
    {
        var todasLasMoviles = new[]
        {
            "app_movil.acceso",
            "app_movil.ventas.facturar",
            "app_movil.ventas.pagos.multi",
            "app_movil.ventas.pedidos.modificar",
            "app_movil.ventas.pedidos.rechazar",
            "app_movil.ventas.devolucion",
            "app_movil.ventas.usados",
            "app_movil.ventas.usados.guardar",
            "app_movil.ventas.simular.con_remision",
            "app_movil.ventas.simular.sin_remision"
        };

        var ctxWeb = Contexto(empresaId, canal: CanalDeAcceso.ValorWeb);

        Negado(await new VentasMultiPagoController(
                null!, Accesor(empresaId, companyKey), Permisos(todasLasMoviles))
            { ControllerContext = ctxWeb }.Pantalla(new VentasMultiPagoPantallaRequest(), default));

        Negado(await new VentasDevolucionController(
                null!, Accesor(empresaId, companyKey), Permisos(todasLasMoviles))
            { ControllerContext = ctxWeb }.Vista(1, default));

        Negado(await Usados(todasLasMoviles, empresaId, companyKey, canal: CanalDeAcceso.ValorWeb)
            .Guardar(new VentasUsadosGuardarRequest(), default));

        Negado(await Consulta(todasLasMoviles, empresaId, companyKey, canal: CanalDeAcceso.ValorWeb)
            .Simulacion(new VentasSimulacionRequest { IdVenta = 10 }, default));

        Negado(await Pedidos(todasLasMoviles, empresaId, companyKey, canal: CanalDeAcceso.ValorWeb)
            .AjustarUsadosCreditoAsync(
                new ISL_Service.Application.DTOs.PedidosOperaciones.PedidoUsadoCreditoRequest(), default));
    }

    // ==================================================================
    // Andamio
    // ==================================================================

    private static string[] Con(string clave) => SoloLoBasicoDeLaApp.Append(clave).ToArray();

    /// <summary>403 exacto, no "algo que no sea 200".</summary>
    private static void Negado(IActionResult resultado)
    {
        var obj = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    /// <summary>
    /// Si la llamada PASO del guardia. Se reconoce porque llega al servicio,
    /// que va en null! y truena: lo que se esta midiendo es que NO devolvio el
    /// 403, y un reventon ahi dentro es prueba de que el guardia la dejo pasar.
    /// </summary>
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

    private static VentasFacturacionController Facturacion(string[] permisos, int empresaId, string companyKey)
        => new(null!, null!, null!, null!, Accesor(empresaId, companyKey), Permisos(permisos))
        { ControllerContext = Contexto(empresaId) };

    private static PedidosOperacionesController Pedidos(
        string[] permisos, int empresaId, string companyKey, string canal = CanalDeAcceso.ValorMovil)
        => new(null!, Accesor(empresaId, companyKey), Permisos(permisos))
        { ControllerContext = Contexto(empresaId, canal) };

    private static VentasUsadosController Usados(
        string[] permisos, int empresaId, string companyKey, string canal = CanalDeAcceso.ValorMovil)
        => new(null!, Accesor(empresaId, companyKey), Permisos(permisos))
        { ControllerContext = Contexto(empresaId, canal) };

    private static VentasConsultaController Consulta(
        string[] permisos, int empresaId, string companyKey, string canal = CanalDeAcceso.ValorMovil)
        => new(null!, null!, null!, null!, Accesor(empresaId, companyKey), Permisos(permisos), Configuracion())
        { ControllerContext = Contexto(empresaId, canal) };

    private static IConfiguration Configuracion()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "A9F7Q3Z6Vw2R8eKJcUT4NBydH5sYpM3aXWmL"
        }).Build();

    private static ControllerContext Contexto(int empresaId, string canal = CanalDeAcceso.ValorMovil)
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

    private static ICurrentUserAccessor Accesor(int empresaId, string companyKey)
        => new AccesorDeEmpresa(empresaId, companyKey);

    private static IPermissionService Permisos(string[] claves) => new PermisosFijos(claves);

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

        public Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
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
        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(int empresaId, Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<AdministracionRestante> GetAdministracionRestanteAsync(int empresaId, SimulacionDePermisos simulacion, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> EsRolDeSistemaAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
    }
}
