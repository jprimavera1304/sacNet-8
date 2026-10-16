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
/// QUE LAS CLAVES DE CENTROS APAREZCAN SOLAS EN EL CATALOGO DE CADA EMPRESA, LAS
/// VIEJAS Y LAS NUEVAS, Y QUE SE PUEDAN REPARTIR.
/// </summary>
/// <remarks>
/// POR QUE CONTRA LA BASE Y NO EN SECO
/// -----------------------------------
/// Porque la lista correcta la dice la base: son dos claves nuevas por CENTRO
/// ACTIVO, y cuantos hay y como se llaman solo lo sabe dbo.CentrosServicio de
/// cada empresa. Zaragoza tiene doce (2001..2012) y Tauro diez. Escribir el
/// numero en el codigo seria escribir el de hoy.
///
/// Y porque el fallo que vigila es el que esta casa ya se encontro tres veces y
/// siempre de la misma forma: la clave se escribe en el codigo, nadie la siembra,
/// y el endpoint contesta 403 a todo el mundo menos al SuperAdmin —que pasa antes
/// de mirar nada—, asi que desde una cuenta de administrador se ve perfecto.
///
/// SE SALTA SOLA si no hay SQL Server delante. No escribe nada que no escriba el
/// arranque del backend: la siembra es idempotente y es exactamente la que correra
/// en produccion.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class ClavesDeCentrosSembradasContraLaBase
{
    private readonly ITestOutputHelper _out;
    public ClavesDeCentrosSembradasContraLaBase(ITestOutputHelper salida) => _out = salida;

    private const int EmpresaId = 1;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDeCentros());

    private sealed class EntornoDeCentros : IHostEnvironment
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
    public async Task Cada_Centro_Activo_Trae_Sus_Claves_Y_El_Catalogo_Las_Ofrece(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        /* Los centros ACTIVOS, que son los que la siembra mira. */
        var centros = new List<(int Numero, string Nombre)>();
        await using (var cmd = new SqlCommand(@"
SELECT Numero, ISNULL(NombreCentro, '')
FROM dbo.CentrosServicio
WHERE IDStatus = 1 AND Numero IS NOT NULL AND Numero > 0
ORDER BY Numero;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
                centros.Add((r.GetInt32(0), r.GetString(1).Trim()));
        }

        _out.WriteLine($"=== {baseDatos} === centros activos: {centros.Count}");
        if (centros.Count == 0)
        {
            _out.WriteLine("Sin centros activos: la siembra de centros no tiene nada que hacer aqui.");
            return;
        }

        /* La siembra, la misma que corre el arranque del backend. */
        await Servicio(baseDatos).SembrarCatalogoCompletoAsync(EmpresaId, CancellationToken.None);

        var enLaBase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new SqlCommand(
            "SELECT Clave FROM dbo.WPermiso WHERE EmpresaId = @E;", conn))
        {
            cmd.Parameters.AddWithValue("@E", EmpresaId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                if (!r.IsDBNull(0)) enLaBase.Add(r.GetString(0).Trim());
        }

        /* Lo que TIENE que estar: las dos del selector, las dos de la matriz, y
           por cada centro activo la vieja de ver mas las dos nuevas. */
        var esperadas = new List<string>
        {
            PermisosDeCentros.CambiarLegado,
            PermisosDeCentros.Cambiar,
            PermisosDeCentros.MatrizVer,
            PermisosDeCentros.MatrizOperar
        };
        foreach (var (numero, _) in centros)
        {
            esperadas.Add(PermisosDeCentros.VerLegado(numero));
            esperadas.Add(PermisosDeCentros.Ver(numero));
            esperadas.Add(PermisosDeCentros.Operar(numero));
        }

        var faltan = esperadas.Where(k => !enLaBase.Contains(k)).ToList();
        _out.WriteLine($"claves de centros esperadas: {esperadas.Count}   faltan: {faltan.Count}");
        foreach (var k in faltan) _out.WriteLine($"  FALTA  {k}");

        Assert.True(faltan.Count == 0,
            $"[{baseDatos}] estas claves de centros no las sembro nadie, asi que nadie salvo el "
            + "SuperAdmin podria tenerlas:\n  - " + string.Join("\n  - ", faltan));

        /* LA VIEJA NO DESAPARECIO. Es la que la gente tiene concedida hoy: si se
           cayera del catalogo quedaria concedida y sin forma de verla ni quitarla
           desde la pantalla. */
        foreach (var (numero, _) in centros)
        {
            Assert.Contains(PermisosDeCentros.VerLegado(numero), enLaBase);
        }

        /* Y EL CATALOGO DEL WEB LAS OFRECE, que es la diferencia entre "esta en la
           base" y "se puede repartir desde la pantalla". */
        var catalogo = await Servicio(baseDatos).GetPermissionCatalogAsync(EmpresaId, CancellationToken.None);
        var ofrecidas = catalogo.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var noOfrecidas = esperadas.Where(k => !ofrecidas.ContainsKey(k)).ToList();
        Assert.True(noOfrecidas.Count == 0,
            $"[{baseDatos}] la pantalla de Accesos y permisos no ofrece estas claves y no habria "
            + "forma de repartirlas:\n  - " + string.Join("\n  - ", noOfrecidas));

        /* Ninguna sale con el aviso "no protege nada": ese aviso le dice al
           administrador que la casilla no decide, y aqui si decide. */
        var mentirosas = esperadas.Where(k => !ofrecidas[k].ProtegeAlgo).ToList();
        Assert.True(mentirosas.Count == 0,
            $"[{baseDatos}] estas se ofrecen con el aviso de que no deciden nada:\n  - "
            + string.Join("\n  - ", mentirosas));

        /* LA FAMILIA NUEVA VIVE EN SU PROPIA TARJETA. El web agrupa por
           ModuleKey; si las nuevas salieran en "reportes", las casillas de
           ESCRIBIR en un centro quedarian enterradas dentro de Reportes, que es
           donde nadie las buscaria. */
        var enModuloEquivocado = esperadas
            .Where(k => k.StartsWith(PermisosDeCentros.Prefijo, StringComparison.OrdinalIgnoreCase))
            .Where(k => !string.Equals(ofrecidas[k].ModuleKey, "centros", StringComparison.OrdinalIgnoreCase))
            .Select(k => $"{k} -> {ofrecidas[k].ModuleKey}")
            .ToList();
        Assert.True(enModuloEquivocado.Count == 0,
            $"[{baseDatos}] estas claves nuevas no caen en la tarjeta 'Centros':\n  - "
            + string.Join("\n  - ", enModuloEquivocado));

        _out.WriteLine($"OK: {esperadas.Count} claves sembradas y ofrecidas, "
            + $"{centros.Count} centros x (ver viejo + ver nuevo + operar) + 4 generales.");
    }

    /// <summary>
    /// QUE LA SIEMBRA NO REPARTA NADA. Sembrar crea la clave para que se pueda
    /// conceder; conceder es otra cosa y la decide una persona.
    /// </summary>
    /// <remarks>
    /// Importa el doble en `centros.&lt;n&gt;.operar`: si la siembra concediera, el
    /// dia del despliegue todo el mundo podria ESCRIBIR en los doce centros de
    /// Zaragoza sin que nadie lo hubiera decidido. Es el error contrario al de
    /// perder acceso y es peor.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Sembrar_No_Concede_El_Permiso_De_Escribir_En_Un_Centro(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        var antes = await ConcesionesDeOperarAsync(conn);
        await Servicio(baseDatos).SembrarCatalogoCompletoAsync(EmpresaId, CancellationToken.None);
        var despues = await ConcesionesDeOperarAsync(conn);

        _out.WriteLine($"=== {baseDatos} === concesiones de centros.*.operar: antes {antes}, despues {despues}");

        Assert.Equal(antes, despues);
    }

    private static async Task<int> ConcesionesDeOperarAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(@"
SELECT (SELECT COUNT(*) FROM dbo.WRolPermiso rp
          JOIN dbo.WPermiso p ON p.Id = rp.WPermisoId
         WHERE p.EmpresaId = @E AND p.Clave LIKE 'centros.%.operar')
     + (SELECT COUNT(*) FROM dbo.WUsuarioPermiso up
          JOIN dbo.WPermiso p ON p.Id = up.WPermisoId
         WHERE p.EmpresaId = @E AND p.Clave LIKE 'centros.%.operar');", conn);
        cmd.Parameters.AddWithValue("@E", EmpresaId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
