using System.Security.Claims;
using ISL_Service.Application.Centros;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Centros;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// LA CAPA DE CONEXION POR CENTRO, DECIDIENDO.
/// </summary>
/// <remarks>
/// Lo que se mide aqui es QUE BASE sale y, sobre todo, CUANDO NO SALE NINGUNA.
/// Se usa `BaseValidadaParaLeerAsync`, que corre las cinco comprobaciones y
/// devuelve el nombre de la base, en vez de abrir la conexion: la decision es la
/// misma — es el mismo metodo privado — y no hace falta SQL Server para
/// comprobarla. Las bases de verdad se tocan en
/// <see cref="CentrosContraLaBaseTests"/>.
///
/// LO QUE ESTAS PRUEBAS NO DEJAN PASAR
/// -----------------------------------
/// Que un 409 se convierta en "devuelve la matriz". Es el fallo que no se ve:
/// la pantalla se llena, los numeros son plausibles, y son de otro sitio. Cada
/// caso de rechazo comprueba que lo que sale es una ConflictException y NO la
/// cadena vacia (que es la matriz).
/// </remarks>
public class CentrosLaCapaDeConexionTests
{
    /* POR QUE `CentroException` Y NO `ConflictException`
       ---------------------------------------------------------------
       Los dos son 409 -`CentroException` hereda de `ConflictException`- pero
       `Assert.ThrowsAsync` exige el tipo EXACTO, y eso aqui es lo que
       queremos: cada rechazo de centros tiene que traer su propio `Code`
       (`CENTRO_...`), porque el telefono lo usa para distinguir "esta
       pantalla todavia no sabe de centros" de cualquier otro 409 de los que
       ya habia. Antes el cliente lo adivinaba mirando si el mensaje hablaba
       de "centro"; una frase reescrita lo rompia en silencio.
       Por eso cada prueba comprueba ademas que el codigo empieza por CENTRO. */

    private const string BaseMatriz = "MacZ";
    private const string NombreZaragoza = "Distribuidora de Acumuladores Zaragoza";
    private const string NombreTauro = "Refacciones Automotrices TAURO";

    // ----------------------------------------------------------- la matriz

    [Fact]
    public async Task Sin_Cabecera_Es_La_Matriz_Y_La_Cadena_No_Se_Toca()
    {
        var capa = Capa(cabecera: null, permisos: Array.Empty<string>());

        Assert.Equal(0, capa.CentroPedido);
        Assert.True(capa.EsLaMatriz);
        /* "" significa "deja la cadena como esta": es literalmente el camino de
           hoy, byte por byte (ver BaseDelCentro.Apuntar con null). */
        Assert.Equal("", await capa.BaseValidadaParaLeerAsync("reportes", 0));
    }

    [Fact]
    public async Task La_Matriz_Funciona_Para_Un_Modulo_QUE_NO_Esta_Declarado()
    {
        /* Esto es lo que hace que el despliegue no rompa nada: las 275 llamadas
           que hoy no saben de centros siguen pidiendo centro 0, y centro 0 no
           pasa por la declaracion de modulos. */
        var capa = Capa(cabecera: null, permisos: Array.Empty<string>());
        Assert.Equal("", await capa.BaseValidadaParaLeerAsync("ventas", 0));
    }

    [Fact]
    public async Task Una_Cabecera_Basura_No_Cae_A_La_Matriz()
    {
        var capa = Capa(cabecera: "MacZCS7", permisos: Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("reportes", capa.CentroPedido));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    // --------------------------------------------------- modulo no declarado

    [Fact]
    public async Task Un_Modulo_No_Declarado_Con_Centro_Da_409_Y_NO_Datos_De_La_Matriz()
    {
        var capa = Capa(cabecera: "2", permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2002)
        });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("ventas", 2));

        Assert.StartsWith("CENTRO", ex.Code);

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("todavia no funciona por centro", ex.Message);
    }

    [Fact]
    public async Task El_Modulo_Se_Mira_ANTES_Del_Catalogo()
    {
        /* Con permisos completos y un centro que existe, el 409 sigue siendo del
           modulo. Importa el orden: si el catalogo hablara primero, el mensaje
           distinguiria un centro que existe de uno que no para alguien que no
           tiene por que saberlo — y costaria un viaje a SQL por nada. */
        var capa = Capa(cabecera: "99", permisos: new[] { ClavesDeCentros.Cambiar });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("cortes", 99));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Contains("todavia no funciona por centro", ex.Message);
    }

    [Fact]
    public async Task Un_Modulo_De_Solo_Lectura_No_Deja_Escribir_En_Un_Centro()
    {
        var capa = Capa(cabecera: "2", permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2002), ClavesDeCentros.Operar(2002)
        });

        /* Reportes esta declarado y el usuario tiene hasta `operar`: el freno es
           que el MODULO no declara ninguna operacion de escritura. */
        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.AbrirParaEscribirAsync("reportes", "generar"));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Contains("solo se puede hacer en la matriz", ex.Message);
    }

    // ------------------------------------------------------------ permisos

    [Fact]
    public async Task Sin_Permiso_De_Cambiar_No_Se_Abre_Ningun_Centro()
    {
        var capa = Capa(cabecera: "2", permisos: new[] { ClavesDeCentros.Ver(2002) });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("reportes", 2));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Contains("no puede cambiar de centro", ex.Message);
    }

    [Fact]
    public async Task Sin_Permiso_De_Ver_Ese_Centro_No_Se_Abre()
    {
        var capa = Capa(cabecera: "2", permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2003)   // otro centro
        });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("reportes", 2));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Contains("no tiene permiso para consultar el centro", ex.Message);
    }

    [Fact]
    public async Task Con_Permiso_De_Ver_Se_Abre_La_Base_De_Ese_Centro()
    {
        var capa = Capa(cabecera: "2", permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2002)
        });

        Assert.Equal("MacZCS2", await capa.BaseValidadaParaLeerAsync("reportes", 2));
    }

    [Fact]
    public async Task La_Clave_Vieja_De_Reportes_Sigue_Abriendo_El_Centro()
    {
        var capa = Capa(cabecera: "2", permisos: new[]
        {
            "reportes.ver_modulo", PermisosDeCentros.Cambiar, PermisosDeCentros.Ver(2002)
        });

        Assert.Equal("MacZCS2", await capa.BaseValidadaParaLeerAsync("reportes", 2));
    }

    [Fact]
    public async Task Un_Centro_Que_No_Esta_En_El_Catalogo_De_Esta_Empresa_Da_409()
    {
        var capa = Capa(cabecera: "77", permisos: new[] { ClavesDeCentros.Cambiar });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("reportes", 77));

        Assert.StartsWith("CENTRO", ex.Code);
        Assert.Contains("no existe o esta dado de baja", ex.Message);
    }

    // ------------------------------------------- la pertenencia protege a Tauro

    [Fact]
    public async Task Un_Centro_De_OTRA_Empresa_Se_Rechaza_Aunque_Este_En_El_Catalogo()
    {
        /* El caso real: Produccion_svr (Tauro) tiene en su propio
           dbo.CentrosServicio cuatro filas apuntando a MacZCS7..MacZCS10, que
           son bases de Zaragoza. El usuario tiene TODOS los permisos y el
           modulo esta declarado: lo unico que lo para es la pertenencia. */
        var capa = CapaDeTauro(cabecera: "7", permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2007)
        });

        var ex = await Assert.ThrowsAsync<CentroException>(
            () => capa.BaseValidadaParaLeerAsync("reportes", 7));

        Assert.StartsWith("CENTRO", ex.Code);

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("OTRA empresa", ex.Message);
        /* Y al usuario no se le dice que base era: eso se queda en el log. */
        Assert.DoesNotContain("MacZCS7", ex.Message);
    }

    [Fact]
    public async Task El_Catalogo_De_Tauro_No_Ofrece_Los_Centros_De_Zaragoza()
    {
        var capa = CapaDeTauro(cabecera: null, permisos: new[]
        {
            ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2007), ClavesDeCentros.Ver(2008)
        });

        var catalogo = await capa.CatalogoAsync();

        Assert.True(catalogo.PuedeCambiar);
        Assert.Empty(catalogo.Centros);          // los dos que podria ver no son suyos
        Assert.Equal(NombreTauro, catalogo.Matriz.Nombre);
    }

    // ------------------------------------------------------------ catalogo

    [Fact]
    public async Task El_Catalogo_Sale_Filtrado_Por_El_Servidor()
    {
        var capa = Capa(cabecera: null, permisos: new[]
        {
            ClavesDeCentros.Cambiar,
            ClavesDeCentros.Ver(2002),
            ClavesDeCentros.Ver(2003), ClavesDeCentros.Operar(2003)
        });

        var catalogo = await capa.CatalogoAsync();

        Assert.Equal(2, catalogo.Centros.Count);
        Assert.DoesNotContain(catalogo.Centros, c => c.Numero == 2001);

        var dos = catalogo.Centros.Single(c => c.Numero == 2002);
        Assert.True(dos.PuedeVer);
        Assert.False(dos.PuedeOperar);          // operar no se deduce de ver

        var tres = catalogo.Centros.Single(c => c.Numero == 2003);
        Assert.True(tres.PuedeOperar);
    }

    [Fact]
    public async Task El_Nombre_De_La_Matriz_Es_El_De_La_Empresa_Y_Nunca_Principal()
    {
        var catalogo = await Capa(null, Array.Empty<string>()).CatalogoAsync();

        Assert.Equal(NombreZaragoza, catalogo.Matriz.Nombre);
        Assert.NotEqual("Principal", catalogo.Matriz.Nombre);
    }

    [Fact]
    public async Task El_Nombre_De_La_Matriz_Se_Puede_Configurar()
    {
        /* FacturasCorreoNombre es el nombre fiscal y puede ser largo para un
           selector. Este es el sitio donde se pone el corto. */
        var catalogo = await Capa(null, Array.Empty<string>(), nombreConfigurado: "Zaragoza")
            .CatalogoAsync();

        Assert.Equal("Zaragoza", catalogo.Matriz.Nombre);
    }

    [Fact]
    public async Task Sin_Permiso_De_Cambiar_El_Catalogo_Sale_Vacio_Pero_Con_La_Matriz()
    {
        var catalogo = await Capa(null, new[] { ClavesDeCentros.Ver(2002) }).CatalogoAsync();

        Assert.False(catalogo.PuedeCambiar);
        Assert.Empty(catalogo.Centros);
        Assert.Equal(NombreZaragoza, catalogo.Matriz.Nombre);
        Assert.True(catalogo.Matriz.PuedeOperar);   // la matriz sigue como hoy
    }

    [Fact]
    public async Task El_Nombre_De_La_Base_No_Viaja_En_Ningun_Campo_De_La_Respuesta()
    {
        var capa = Capa(null, new[] { ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2002) });
        var catalogo = await capa.CatalogoAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(catalogo);
        Assert.DoesNotContain("MacZCS", json);
        Assert.DoesNotContain(BaseMatriz, json);
    }

    // -------------------------------------------------------------- armado

    private static ConexionDelCentro Capa(
        string? cabecera, string[] permisos, string? nombreConfigurado = null)
        => Armar(cabecera, permisos, NombreZaragoza, "ZARA", CatalogoDeZaragoza(), nombreConfigurado);

    /// <summary>
    /// Tauro, con su catalogo CONTAMINADO tal como esta en la base: las seis
    /// primeras filas con BaseCentro vacio y las cuatro ultimas apuntando a
    /// MacZCS7..MacZCS10.
    /// </summary>
    private static ConexionDelCentro CapaDeTauro(string? cabecera, string[] permisos)
        => Armar(cabecera, permisos, NombreTauro, "TAU", CatalogoDeTauro(), null);

    private static ConexionDelCentro Armar(
        string? cabecera,
        string[] permisos,
        string nombreDeLaMatriz,
        string funcionalidad,
        List<CentroServicioItem> catalogo,
        string? nombreConfigurado)
    {
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        if (cabecera is not null)
            http.HttpContext!.Request.Headers[CentroPedido.Cabecera] = cabecera;

        http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("empresaId", "1"),
            new Claim("rolLegacy", "User"),     // NO SuperAdmin: ese pasa todo
            new Claim("idUsuario", "1125")
        }, "pruebas"));

        var ajustes = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Main"] =
                $"Server=noexiste;Database={BaseMatriz};User Id=sa;Password=x;TrustServerCertificate=True;"
        };
        if (nombreConfigurado is not null) ajustes["Centros:NombreDeLaMatriz"] = nombreConfigurado;

        return new ConexionDelCentro(
            http,
            new ConfigurationBuilder().AddInMemoryCollection(ajustes).Build(),
            new CatalogoFijo(catalogo),
            new IdentidadFija(nombreDeLaMatriz, funcionalidad),
            new AccesorDePruebas(),
            new PermisosFijosDeCentros(permisos),
            NullLogger<ConexionDelCentro>.Instance);
    }

    private static List<CentroServicioItem> CatalogoDeZaragoza() => new()
    {
        new CentroServicioItem { IdCentro = 1, Numero = 2001, Nombre = "ZARA",     Base = "MacZCS1" },
        new CentroServicioItem { IdCentro = 2, Numero = 2002, Nombre = "EJE 6",    Base = "MacZCS2" },
        new CentroServicioItem { IdCentro = 3, Numero = 2003, Nombre = "AGRICOLA", Base = "MacZCS3" }
    };

    private static List<CentroServicioItem> CatalogoDeTauro() => new()
    {
        new CentroServicioItem { IdCentro = 7, Numero = 2007, Nombre = "CIRCUITO", Base = "MacZCS7" },
        new CentroServicioItem { IdCentro = 8, Numero = 2008, Nombre = "ERMITA",   Base = "MacZCS8" }
    };

    // --------------------------------------------------------------- dobles

    private sealed class CatalogoFijo : ICentrosServicioRepository
    {
        private readonly List<CentroServicioItem> _centros;
        public CatalogoFijo(List<CentroServicioItem> centros) => _centros = centros;

        public Task<IReadOnlyList<CentroServicioItem>> ListarAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CentroServicioItem>>(_centros);

        public Task<CentroServicioItem?> BuscarAsync(int idCentro, CancellationToken ct = default)
            => Task.FromResult(_centros.FirstOrDefault(c => c.IdCentro == idCentro));
    }

    /// <summary>
    /// Las bases contestan quien son. Las MacZCS* dicen ser centros de
    /// Zaragoza, que es lo que dicen de verdad (medido en las copias locales).
    /// </summary>
    private sealed class IdentidadFija : IIdentidadDeEmpresaRepository
    {
        private readonly string _nombre;
        private readonly string _funcionalidad;

        public IdentidadFija(string nombre, string funcionalidad)
        {
            _nombre = nombre;
            _funcionalidad = funcionalidad;
        }

        public Task<IdentidadDeEmpresa> DeLaMatrizAsync(CancellationToken ct = default)
            => Task.FromResult(new IdentidadDeEmpresa(_nombre, _funcionalidad, false));

        public Task<string> BaseDeLaMatrizAsync(CancellationToken ct = default)
            => Task.FromResult(BaseMatriz);

        public Task<IdentidadDeEmpresa> DeLaBaseAsync(string nombreDeLaBase, CancellationToken ct = default)
        {
            if (nombreDeLaBase.StartsWith("MacZCS", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new IdentidadDeEmpresa(NombreZaragoza, "ZARA", true));

            return Task.FromResult(IdentidadDeEmpresa.Desconocida);
        }
    }

    private sealed class AccesorDePruebas : ICurrentUserAccessor
    {
        public Guid? GetUserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirst("sub")!.Value);
        public int GetLegacyUserId(ClaimsPrincipal user) => 1125;
        public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema") => "QA";
        public int? GetCompanyId(ClaimsPrincipal user) => 1;
        public string? GetCompanyKey(ClaimsPrincipal user) => "zaragoza";
        public string GetRole(ClaimsPrincipal user) => "User";
    }

    private sealed class PermisosFijosDeCentros : IPermissionService
    {
        private readonly IReadOnlyCollection<string> _permisos;
        public PermisosFijosDeCentros(IReadOnlyCollection<string> permisos) => _permisos = permisos;

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
