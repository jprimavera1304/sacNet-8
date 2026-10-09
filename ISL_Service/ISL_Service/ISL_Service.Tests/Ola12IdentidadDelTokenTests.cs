using System.Security.Claims;
using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Application.DTOs.Persona;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.DTOs.VentasConsulta;
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
  LA IDENTIDAD NUNCA VIENE DEL CUERPO. (Ola 12, hallazgo 1 de la auditoria de cierre.)

  En cuatro sitios se habia colado al contrario:

    - VentasConsulta: @IDUsuarioActual -el candado de almacen de los
      procedimientos- se rellenaba del token SOLO si el cuerpo lo traia en 0, o
      sea que el cuerpo ganaba.
    - Pedidos (autorizar): quien procesa y quien autoriza salian de seis campos
      del cuerpo y el token era el ULTIMO recurso.
    - Pagos a proveedores: los seis campos de auditoria se EXIGIAN del cuerpo.
    - Personas: IDUsuario y Equipo se exigian del cuerpo.

  Estas pruebas fijan las dos mitades de la regla: que el actor salga del token,
  y que @IDUsuario -que SI es un filtro legitimo, "de quien quiero ver"- no deje
  ver los renglones de otro a quien no tiene permiso.
*/
public class Ola12IdentidadDelTokenTests
{
    private const int IdDelToken = 77;
    private const int IdDeOtro = 1234;

    // ------------------------------------------------------------------
    // VentasConsulta: quien pregunta
    // ------------------------------------------------------------------

    [Fact]
    public async Task IDUsuarioActual_Sale_Del_Token_Aunque_El_Cuerpo_Mande_Otro()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "ventas.ver_modulo", "ventas.consultar" });

        await controller.Remisiones(new VentasConsultaRequest { IDUsuarioActual = IdDeOtro }, default);

        Assert.Equal(IdDelToken, servicio.UltimoRequest!.IDUsuarioActual);
    }

    /// <summary>
    /// El caso que mas se les escapa: mandar 0 era "sin candado de almacen".
    /// </summary>
    [Fact]
    public async Task IDUsuarioActual_No_Se_Puede_Apagar_Mandando_Cero()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "ventas.ver_modulo", "ventas.consultar" });

        await controller.Pedidos(new VentasConsultaRequest { IDUsuarioActual = 0 }, default);

        Assert.Equal(IdDelToken, servicio.UltimoRequest!.IDUsuarioActual);
    }

    // ------------------------------------------------------------------
    // VentasConsulta: de quien quiero ver (filtro, con permiso)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Con_Permiso_El_Filtro_Por_Usuario_Se_Respeta_Tal_Cual()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "ventas.ver_modulo", "ventas.consultar" });

        var respuesta = await controller.Remisiones(new VentasConsultaRequest { IDUsuario = IdDeOtro }, default);

        Assert.IsType<OkObjectResult>(respuesta);
        Assert.True(servicio.UltimoPuedeVerDeOtros);
        Assert.Equal(IdDeOtro, servicio.UltimoRequest!.IDUsuario);
    }

    [Fact]
    public async Task Sin_Permiso_Pedir_Las_De_Otro_Da_403()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "app_movil.pedidos" });

        var respuesta = await controller.Pedidos(new VentasConsultaRequest { IDUsuario = IdDeOtro }, default);

        var status = Assert.IsType<ObjectResult>(respuesta);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Null(servicio.UltimoRequest);
    }

    /// <summary>
    /// El agujero de omitir `SoloMisPedidos`: sin permiso y sin pedir a nadie en
    /// particular, se acota a los propios en vez de devolver los de todos.
    /// </summary>
    [Fact]
    public async Task Sin_Permiso_Y_Sin_Filtro_Se_Acota_A_Los_Propios()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "app_movil.pedidos" });

        await controller.Pedidos(new VentasConsultaRequest { SoloMisPedidos = false }, default);

        Assert.False(servicio.UltimoPuedeVerDeOtros);
    }

    [Fact]
    public async Task Sin_Permiso_Pedir_Las_Propias_Por_Su_Id_Se_Permite()
    {
        var servicio = new VentasConsultaEspia();
        var controller = Consulta(servicio, permisos: new[] { "app_movil.pedidos" });

        var respuesta = await controller.Pagos(new VentasConsultaRequest { IDUsuario = IdDelToken }, default);

        Assert.IsType<OkObjectResult>(respuesta);
    }

    // ------------------------------------------------------------------
    // Pedidos: la autoria ya no cabe en el cuerpo
    // ------------------------------------------------------------------

    /// <summary>
    /// Los seis campos de identidad se BORRARON del DTO. Si alguien los vuelve a
    /// poner, esta prueba falla: es la unica forma de que no se cuelen otra vez.
    /// </summary>
    [Theory]
    [InlineData("IdUsuario")]
    [InlineData("IdUsuarioProcesar")]
    [InlineData("IdUsuarioAutorizar")]
    [InlineData("Equipo")]
    [InlineData("EquipoProcesar")]
    [InlineData("EquipoAutorizar")]
    public void AutorizarPedidosRequest_No_Tiene_Campos_De_Identidad(string propiedad)
    {
        var tipo = typeof(ISL_Service.Application.DTOs.VentasPedidos.AutorizarPedidosRequest);
        Assert.Null(tipo.GetProperty(propiedad));
    }

    // ------------------------------------------------------------------
    // Pagos a proveedores y Personas: la firma la pone el token
    // ------------------------------------------------------------------

    [Fact]
    public void PagosProveedores_Sella_Los_Seis_Campos_De_Auditoria_Con_El_Token()
    {
        var repo = new ProveedoresPagosRepositorioEspia();
        var controller = new ProveedoresPagosController(
            new ISL_Service.Application.Services.ProveedoresPagosService(repo),
            new AccesorFijo())
        {
            ControllerContext = Contexto()
        };

        controller.Insertar(new ProveedorPagoInputDTO
        {
            IDPersona = 9,
            FechaPago = "01-01-2026",
            // Lo que mande el cliente da igual:
            IDUsuario = IdDeOtro,
            Equipo = "LA-MAQUINA-DE-OTRO",
            IDUsuarioModificacion = IdDeOtro,
            IDUsuarioCancelacion = IdDeOtro
        });

        var visto = repo.UltimoInsertado!;
        Assert.Equal(IdDelToken, visto.IDUsuario);
        Assert.Equal(IdDelToken, visto.IDUsuarioModificacion);
        Assert.Equal(IdDelToken, visto.IDUsuarioCancelacion);
        Assert.Equal("JUAN", visto.Equipo);
        Assert.Equal("JUAN", visto.EquipoModificacion);
        Assert.Equal("JUAN", visto.EquipoCancelacion);
    }

    [Fact]
    public async Task Personas_Sella_La_Autoria_Con_El_Token()
    {
        var servicio = new PersonasServicioEspia();
        var controller = new PersonasController(servicio, new AccesorFijo())
        {
            ControllerContext = Contexto()
        };

        await controller.Insertar(new PersonaInputDTO
        {
            Nombre = "PROVEEDOR NUEVO",
            IDUsuario = IdDeOtro,
            Equipo = "LA-MAQUINA-DE-OTRO"
        });

        Assert.Equal(IdDelToken, servicio.Ultimo!.IDUsuario);
        Assert.Equal("JUAN", servicio.Ultimo!.Equipo);
    }

    // ------------------------------------------------------------------
    // Andamiaje
    // ------------------------------------------------------------------

    private static ControllerContext Contexto() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("idUsuario", IdDelToken.ToString()),
                new Claim("username", "JUAN"),
                new Claim("empresaId", "1"),
                new Claim("rolLegacy", "User")
            }, "pruebas"))
        }
    };

    private static VentasConsultaController Consulta(VentasConsultaEspia servicio, string[] permisos)
    {
        var controller = new VentasConsultaController(
            servicio,
            new RemisionesNoUsado(),
            new AccesorFijo(),
            new PermisosFijosDeOla12(permisos),
            new ConfigurationBuilder().Build())
        {
            ControllerContext = Contexto()
        };

        return controller;
    }

    private sealed class AccesorFijo : ICurrentUserAccessor
    {
        public Guid? GetUserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
        public int GetLegacyUserId(ClaimsPrincipal user) => IdDelToken;
        public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema") => "JUAN";
        public int? GetCompanyId(ClaimsPrincipal user) => 1;
        public string? GetCompanyKey(ClaimsPrincipal user) => "tauro";
        public string GetRole(ClaimsPrincipal user) => "User";
    }

    private sealed class VentasConsultaEspia : IVentasConsultaService
    {
        public VentasConsultaRequest? UltimoRequest { get; private set; }
        public bool UltimoPuedeVerDeOtros { get; private set; }

        private Task<VentasConsultaRowsResponse> Anotar(VentasConsultaRequest request, bool puedeVerDeOtros)
        {
            UltimoRequest = request;
            UltimoPuedeVerDeOtros = puedeVerDeOtros;
            return Task.FromResult(new VentasConsultaRowsResponse());
        }

        public Task<VentasConsultaCatalogosResponse> ConsultarCatalogosAsync(CancellationToken ct)
            => Task.FromResult(new VentasConsultaCatalogosResponse());

        public Task<VentasConsultaRowsResponse> ConsultarRemisionesAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        {
            request.IDUsuarioActual = idUsuarioToken;
            return Anotar(request, puedeVerDeOtros);
        }

        public Task<VentasConsultaRowsResponse> ConsultarPedidosAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        {
            request.IDUsuarioActual = idUsuarioToken;
            return Anotar(request, puedeVerDeOtros);
        }

        public Task<VentasConsultaRowsResponse> ConsultarPagosAsync(VentasConsultaRequest request, int idUsuarioToken, bool puedeVerDeOtros, CancellationToken ct)
        {
            request.IDUsuarioActual = idUsuarioToken;
            return Anotar(request, puedeVerDeOtros);
        }

        public Task<VentasConsultaRowsResponse> ConsultarPendientesImprimirAsync(VentasConsultaRequest request, int idUsuarioToken, CancellationToken ct)
        {
            request.IDUsuarioActual = idUsuarioToken;
            return Anotar(request, false);
        }

        public Task<List<VentasPedidoFaltanteItem>> ConsultarPedidosFaltantesAsync(CancellationToken ct)
            => Task.FromResult(new List<VentasPedidoFaltanteItem>());

        public Task<VentasReportePreparacionResponse> PrepararReporteAsync(VentasReporteRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<string> CrearTicketReporteAsync(VentasReporteRequest request, int idUsuarioToken, string equipo, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class RemisionesNoUsado : IRemisionImpresionService
    {
        public Task<RemisionPdf> GenerarAsync(IReadOnlyList<int> idsVenta, int idUsuarioImpresion, int primerImpresion, int reimpresion, string equipoImpresion, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class ProveedoresPagosRepositorioEspia : ISL_Service.Infrastructure.Repositories.ProveedoresPagosRepository
    {
        public ProveedoresPagosRepositorioEspia()
            : base(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = "Server=.;Database=x;Integrated Security=true;"
            }).Build())
        {
        }

        public ProveedorPagoInputDTO? UltimoInsertado { get; private set; }

        public override int Insertar(ProveedorPagoInputDTO input)
        {
            UltimoInsertado = input;
            return 1;
        }
    }

    private sealed class PersonasServicioEspia : IPersonasService
    {
        public PersonaInputDTO? Ultimo { get; private set; }

        public Task<PersonaResponse> ConsultarAsync(int? idPersona, int? idStatus)
            => Task.FromResult(new PersonaResponse { Success = true, List = new List<PersonaDTO>() });

        public Task<PersonaResponse> InsertarAsync(PersonaInputDTO input)
        {
            Ultimo = input;
            return Task.FromResult(new PersonaResponse { Success = true });
        }

        public Task<PersonaResponse> ActualizarAsync(PersonaInputDTO input)
        {
            Ultimo = input;
            return Task.FromResult(new PersonaResponse { Success = true });
        }

        public Task<PersonaResponse> CancelarAsync(int idPersona, PersonaInputDTO input)
        {
            Ultimo = input;
            return Task.FromResult(new PersonaResponse { Success = true });
        }

        public Task<PersonaResponse> ReactivarAsync(int idPersona, PersonaInputDTO input)
        {
            Ultimo = input;
            return Task.FromResult(new PersonaResponse { Success = true });
        }
    }

    private sealed class PermisosFijosDeOla12 : IPermissionService
    {
        private readonly string[] _permisos;

        public PermisosFijosDeOla12(string[] permisos) => _permisos = permisos;

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
        public Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, CancellationToken ct) => throw new NotImplementedException();
        public Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct) => Task.CompletedTask;
        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
    }
}
