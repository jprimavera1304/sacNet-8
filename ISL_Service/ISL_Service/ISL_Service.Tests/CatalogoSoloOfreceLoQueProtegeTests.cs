using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// QUE LA PANTALLA DE PERMISOS NO OFREZCA CASILLAS QUE NO DECIDEN NADA, Y QUE
/// AL ESCONDERLAS NO SE LLEVE NINGUNA QUE ALGUIEN TENGA.
/// </summary>
/// <remarks>
/// Las dos mitades importan por igual. Esconder lo inerte evita prometer una
/// decision que no existe; no esconder lo concedido evita dejar una clave
/// guardada en la base sin que nadie pueda verla ni quitarla. Un filtro que
/// acertara solo en la primera mitad seria un problema nuevo.
///
/// Es contra la base porque la tercera fuente de "esto si decide" —que la
/// clave corresponda a un boton de Mac31— vive en los datos de cada empresa,
/// no en el codigo. SE SALTA SOLA si no hay SQL Server.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CatalogoSoloOfreceLoQueProtegeTests
{
    private readonly ITestOutputHelper _out;
    public CatalogoSoloOfreceLoQueProtegeTests(ITestOutputHelper salida) => _out = salida;

    private const int EmpresaId = 1;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDeCatalogo());

    private sealed class EntornoDeCatalogo : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ISL_Service.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Ni_Ofrece_Lo_Inerte_Ni_Esconde_Lo_Concedido(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        var enLaBase = await ClavesAsync(conn, "SELECT Clave FROM dbo.WPermiso WHERE EmpresaId = " + EmpresaId);
        var concedidas = await ClavesAsync(conn, $@"
SELECT p.Clave FROM dbo.WPermiso p
WHERE p.EmpresaId = {EmpresaId}
  AND (EXISTS (SELECT 1 FROM dbo.WUsuarioPermiso up WHERE up.EmpresaId = p.EmpresaId AND up.WPermisoId = p.Id)
    OR EXISTS (SELECT 1 FROM dbo.WRolPermiso rp WHERE rp.EmpresaId = p.EmpresaId AND rp.WPermisoId = p.Id));");

        var catalogo = await Servicio(baseDatos).GetPermissionCatalogAsync(EmpresaId, CancellationToken.None);
        var ofrecidas = new HashSet<string>(catalogo.Select(x => x.Code), StringComparer.OrdinalIgnoreCase);

        _out.WriteLine($"=== {baseDatos} ===");
        _out.WriteLine($"en WPermiso: {enLaBase.Count}");
        _out.WriteLine($"ofrecidas por el catalogo: {ofrecidas.Count}");
        _out.WriteLine($"escondidas por inertes: {enLaBase.Count - ofrecidas.Count}");
        _out.WriteLine($"ofrecidas con aviso 'no protege nada': {catalogo.Count(x => !x.ProtegeAlgo)}");

        foreach (var x in catalogo.Where(x => !x.ProtegeAlgo).OrderBy(x => x.Code))
            _out.WriteLine($"  SIN COMPROBACION  {x.Code}");
        foreach (var c in enLaBase.Where(c => !ofrecidas.Contains(c)).OrderBy(x => x))
            _out.WriteLine($"  ESCONDIDA         {c}");

        // 1. Ninguna clave concedida se esconde.
        var concedidasEscondidas = concedidas.Where(c => !ofrecidas.Contains(c)).OrderBy(x => x).ToList();
        Assert.True(concedidasEscondidas.Count == 0,
            $"[{baseDatos}] estas claves las tiene alguien y el catalogo ya no las ensena, asi que no se podrian quitar:\n  - "
            + string.Join("\n  - ", concedidasEscondidas));

        // 2. Lo que se ofrece sin aviso, decide de verdad.
        var mentirosas = catalogo
            .Where(x => x.ProtegeAlgo
                        && !ClavesQueProtegenAlgo.Protege(x.Code)
                        && !x.IsLegacyReport
                        && x.LegacyProcessId is null)
            .Select(x => x.Code).OrderBy(x => x).ToList();
        Assert.True(mentirosas.Count == 0,
            $"[{baseDatos}] estas se ofrecen como si decidieran algo y no decide nada:\n  - "
            + string.Join("\n  - ", mentirosas));

        // 3. Y no se escondio nada que SI decide.
        var escondidasQueSiDeciden = enLaBase
            .Where(c => !ofrecidas.Contains(c) && ClavesQueProtegenAlgo.Protege(c))
            .OrderBy(x => x).ToList();
        Assert.True(escondidasQueSiDeciden.Count == 0,
            $"[{baseDatos}] estas si las comprueba el servidor y el catalogo las escondio:\n  - "
            + string.Join("\n  - ", escondidasQueSiDeciden));
    }

    private static async Task<HashSet<string>> ClavesAsync(SqlConnection conn, string sql)
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (!r.IsDBNull(0)) claves.Add(r.GetString(0).Trim());
        return claves;
    }
}
