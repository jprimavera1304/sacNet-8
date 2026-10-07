using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Middleware;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISL_Service.Tests;

/*
  OLA 12: EL AISLAMIENTO ENTRE EMPRESAS, LA FUGA DE SQL Y LA CACHE DE PERMISOS.

  Tres hallazgos de la auditoria de cierre, los tres de mecanismo y los tres
  comprobables sin base de datos:

    - 5. ExceptionHandlingMiddleware devolvia `detail = ex.Message` crudo SIEMPRE,
         tambien en Production. Comprobado en vivo: el nombre del procedimiento
         que falta se regalaba en la respuesta.
    - 7. El claim `companyKey` del token no se comparaba contra nada, asi que un
         token emitido en una instalacion valia en otra.
    - 2. La invalidacion de la cache de permisos colgaba de un diccionario de
         INSTANCIA en un servicio Scoped, con la IMemoryCache Singleton: no
         quitaba ni una clave de los demas usuarios.
*/
public class Ola12AislamientoYFugasTests
{
    // ==================================================================
    // 5. El detalle del motor no sale en Production
    // ==================================================================

    [Fact]
    public async Task En_Production_El_Detalle_Tecnico_No_Viaja_Y_Si_Va_El_TraceId()
    {
        var cuerpo = await EjecutarMiddlewareDeErrores(
            entorno: "Production",
            excepcion: new InvalidOperationException("Could not find stored procedure 'dbo.sp_w_ConsultarDashboardVentasKpis'."));

        Assert.Equal("Error interno.", cuerpo.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("detail").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("traceId").GetString()));

        // Lo importante: el nombre del procedimiento NO esta en la respuesta.
        Assert.DoesNotContain("sp_w_ConsultarDashboardVentasKpis", cuerpo.ToString());
    }

    [Fact]
    public async Task En_Development_El_Detalle_Si_Viaja_Porque_Ahi_Sirve()
    {
        var cuerpo = await EjecutarMiddlewareDeErrores(
            entorno: "Development",
            excepcion: new InvalidOperationException("Could not find stored procedure 'dbo.sp_w_X'."));

        Assert.Contains("sp_w_X", cuerpo.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Los mensajes que el propio backend redacta para el usuario (AppException)
    /// NO se tapan: no vienen del motor y el usuario los necesita.
    /// </summary>
    [Fact]
    public async Task El_Detalle_De_Una_AppException_Sigue_Saliendo_En_Production()
    {
        var cuerpo = await EjecutarMiddlewareDeErrores(
            entorno: "Production",
            excepcion: new ISL_Service.Application.Exceptions.ConflictException(
                "Falta desplegar sp_w_ConsultaAgenteUsuario en esta base.",
                "Lo pide la busqueda de clientes."));

        Assert.Equal("Falta desplegar sp_w_ConsultaAgenteUsuario en esta base.", cuerpo.GetProperty("message").GetString());
        Assert.Equal("Lo pide la busqueda de clientes.", cuerpo.GetProperty("detail").GetString());
    }

    // ==================================================================
    // 7. El token tiene que ser de esta empresa
    // ==================================================================

    [Fact]
    public async Task Token_De_La_Misma_Empresa_Pasa()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(claveDelToken: "tauro", claveDeLaInstalacion: "tauro");

        Assert.True(siguienteCorrio);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task Token_De_Otra_Empresa_Se_Rechaza_Con_401_Y_No_Llega_Al_Endpoint()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(claveDelToken: "zaragoza", claveDeLaInstalacion: "tauro");

        Assert.False(siguienteCorrio);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    /// <summary>
    /// La comparacion no distingue mayusculas: el generador normaliza a
    /// minusculas, pero un token viejo no tiene por que haberlo hecho.
    /// </summary>
    [Fact]
    public async Task La_Comparacion_No_Distingue_Mayusculas()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(claveDelToken: "TAURO", claveDeLaInstalacion: "tauro");

        Assert.True(siguienteCorrio);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task Token_Sin_El_Claim_Se_Rechaza()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(claveDelToken: null, claveDeLaInstalacion: "tauro");

        Assert.False(siguienteCorrio);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    /// <summary>
    /// Lo anonimo a proposito -el PDF de la remision, el login- no trae token y
    /// tiene que seguir pasando: ahi lo que autoriza es el pase firmado.
    /// </summary>
    [Fact]
    public async Task Una_Peticion_Sin_Token_Pasa_De_Largo()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(
            claveDelToken: null, claveDeLaInstalacion: "tauro", autenticado: false);

        Assert.True(siguienteCorrio);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    /// <summary>
    /// Si no se puede saber de que empresa es esta instalacion, se falla CERRADO.
    /// Un candado de aislamiento que se abre cuando la base no contesta no es un
    /// candado.
    /// </summary>
    [Fact]
    public async Task Si_No_Se_Puede_Resolver_La_Empresa_Se_Falla_Cerrado()
    {
        var (status, siguienteCorrio) = await EjecutarCompanyKey(
            claveDelToken: "tauro", claveDeLaInstalacion: null);

        Assert.False(siguienteCorrio);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    // ==================================================================
    // 2. La invalidacion de permisos alcanza a todos
    // ==================================================================

    /// <summary>
    /// El diccionario de instancia que no invalidaba nada ya no existe. Si
    /// alguien lo vuelve a poner, esto falla.
    /// </summary>
    [Fact]
    public void Ya_No_Hay_Registro_De_Claves_De_Instancia()
    {
        var campos = typeof(PermissionService)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(f => f.Name)
            .ToList();

        Assert.DoesNotContain("_observedUserCacheKeys", campos);
    }

    /// <summary>
    /// El registro de invalidadores tiene que ser ESTATICO: la IMemoryCache es
    /// Singleton y PermissionService es Scoped. Uno de instancia nace vacio en
    /// cada peticion, que es exactamente el defecto que se arreglo.
    /// </summary>
    [Fact]
    public void El_Registro_De_Invalidadores_Es_Estatico()
    {
        var campo = typeof(PermissionService)
            .GetField("_invalidadores", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(campo);
        Assert.True(campo!.IsStatic);
    }

    /// <summary>
    /// Y que funcione: un renglon guardado en UNA "peticion" se tiene que morir
    /// cuando OTRA invalida. Se ejercita el mecanismo real -las opciones de cache
    /// y el disparador- contra una IMemoryCache de verdad, que es justo el par
    /// que antes no se hablaba.
    /// </summary>
    [Theory]
    [InlineData(true)]   // invalidar al usuario
    [InlineData(false)]  // invalidar la empresa entera
    public void Invalidar_Mata_El_Renglon_Guardado_Por_Otra_Peticion(bool porUsuario)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();
        const int empresaId = 1;

        // "Peticion A": guarda el snapshot con las opciones reales.
        var opciones = Invocar<MemoryCacheEntryOptions>(
            "OpcionesDeCache", userId, empresaId, TimeSpan.FromMinutes(5));
        cache.Set("perm:1:x:ROL_PROPIO_DEL_TENANT", "permisos viejos", opciones);

        Assert.True(cache.TryGetValue("perm:1:x:ROL_PROPIO_DEL_TENANT", out _));

        // "Peticion B": otro usuario revoca el permiso. Ambito distinto, misma
        // cache.
        var ambito = porUsuario
            ? Invocar<string>("AmbitoUsuario", userId, empresaId)
            : Invocar<string>("AmbitoEmpresa", empresaId);
        Invocar<object?>("DispararInvalidacion", ambito);

        Assert.False(cache.TryGetValue("perm:1:x:ROL_PROPIO_DEL_TENANT", out _));
    }

    /// <summary>
    /// Invalidar a UN usuario no debe tirar la cache de los demas: si no, cada
    /// cambio de permiso volveria a reconstruir todo y se perderia el motivo por
    /// el que la cache existe.
    /// </summary>
    [Fact]
    public void Invalidar_A_Un_Usuario_No_Tira_La_De_Otro()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var unUsuario = Guid.NewGuid();
        var otroUsuario = Guid.NewGuid();

        cache.Set("a", "de uno", Invocar<MemoryCacheEntryOptions>("OpcionesDeCache", unUsuario, 1, TimeSpan.FromMinutes(5)));
        cache.Set("b", "del otro", Invocar<MemoryCacheEntryOptions>("OpcionesDeCache", otroUsuario, 1, TimeSpan.FromMinutes(5)));

        Invocar<object?>("DispararInvalidacion", Invocar<string>("AmbitoUsuario", unUsuario, 1));

        Assert.False(cache.TryGetValue("a", out _));
        Assert.True(cache.TryGetValue("b", out _));
    }

    // ==================================================================
    // Andamiaje
    // ==================================================================

    private static T Invocar<T>(string nombre, params object?[] argumentos)
    {
        var metodo = typeof(PermissionService)
            .GetMethod(nombre, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No existe {nombre} como estatico privado de PermissionService.");

        return (T)metodo.Invoke(null, argumentos)!;
    }

    private static async Task<JsonElement> EjecutarMiddlewareDeErrores(string entorno, Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw excepcion,
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new EntornoFijo(entorno));

        await middleware.Invoke(contexto);

        contexto.Response.Body.Position = 0;
        using var lector = new StreamReader(contexto.Response.Body);
        var json = await lector.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static async Task<(int Status, bool SiguienteCorrio)> EjecutarCompanyKey(
        string? claveDelToken, string? claveDeLaInstalacion, bool autenticado = true)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (claveDelToken is not null) claims.Add(new Claim("companyKey", claveDelToken));
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, autenticado ? "pruebas" : null));

        var corrio = false;
        var middleware = new CompanyKeyMiddleware(
            _ => { corrio = true; return Task.CompletedTask; },
            NullLogger<CompanyKeyMiddleware>.Instance);

        await middleware.Invoke(
            contexto,
            new CurrentUserAccessor(),
            new EmpresaFija(claveDeLaInstalacion),
            new MemoryCache(new MemoryCacheOptions()));

        return (contexto.Response.StatusCode, corrio);
    }

    private sealed class EntornoFijo : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public EntornoFijo(string nombre) => EnvironmentName = nombre;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "pruebas";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>
    /// `null` = no se puede resolver la empresa de la instalacion (la base no
    /// contesta), que es el caso de fallo cerrado.
    /// </summary>
    private sealed class EmpresaFija : IEmpresaRepository
    {
        private readonly string? _clave;

        public EmpresaFija(string? clave) => _clave = clave;

        public Task<List<int>> ListEmpresaIdsAsync(CancellationToken ct) => Task.FromResult(new List<int> { 1 });

        public Task<string> GetCompanyKeyAsync(CancellationToken ct)
            => _clave is null
                ? throw new InvalidOperationException("dbo.EmpresaWeb(Id=1).Clave no se pudo leer.")
                : Task.FromResult(_clave);
    }
}
