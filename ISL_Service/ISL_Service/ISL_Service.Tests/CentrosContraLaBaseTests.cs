using ISL_Service.Application.Centros;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// LA PERTENENCIA, CONTRA LAS BASES DE VERDAD.
/// </summary>
/// <remarks>
/// NO TOCA PRODUCCION. Todo corre contra las copias LOCALES de LAP-JUAND
/// (MacZ, Produccion_svr y MacZCS1..MacZCS12), y solo LEE: un SELECT TOP 1 de
/// dbo.Constantes. Si el servidor local no esta, cada prueba se sale sin
/// fallar — es lo mismo que hace AuditoriaHerenciaLegacyVsWeb.
///
/// POR QUE HACE FALTA ESTA PRUEBA SI YA ESTAN LAS PURAS
/// ---------------------------------------------------
/// Porque las puras comprueban el JUICIO con identidades inventadas por mi. La
/// pregunta que solo la base contesta es si las columnas que eleji dicen lo que
/// creo que dicen: si FacturasCorreoNombre estuviera vacio en una de las doce
/// bases de Zaragoza, el criterio seria correcto y el resultado seria que
/// Zaragoza se queda sin ese centro. Eso no se puede deducir leyendo codigo.
///
/// Si esta prueba empieza a fallar, lo mas probable es que alguien capturo
/// distinto una de esas columnas, no que el codigo este mal.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CentrosContraLaBaseTests
{
    private readonly ITestOutputHelper _out;
    public CentrosContraLaBaseTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static IdentidadDeEmpresaRepository Repo(string baseDatos) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Main"] = CadenaDe(baseDatos)
        }).Build(),
        new MemoryCache(new MemoryCacheOptions()));

    /// <summary>true si el servidor local esta. Si no, la prueba se sale.</summary>
    private static async Task<bool> HayBaseAsync(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); return true; }
        catch (SqlException) { return false; }
    }

    [Fact]
    public async Task Zaragoza_Reconoce_Sus_Doce_Centros()
    {
        if (!await HayBaseAsync("MacZ")) return;

        var repo = Repo("MacZ");
        var matriz = await repo.DeLaMatrizAsync();

        Assert.True(matriz.EstaCompleta);
        _out.WriteLine($"MacZ dice ser: '{matriz.NombreComercial}' ({matriz.Funcionalidad})");

        for (var n = 1; n <= 12; n++)
        {
            var candidata = await repo.DeLaBaseAsync($"MacZCS{n}");
            if (!candidata.EstaCompleta)
            {
                /* La copia local puede no tener las doce. No es un fallo del
                   criterio; se deja escrito y se sigue. */
                _out.WriteLine($"MacZCS{n}: no esta en esta copia local.");
                continue;
            }

            var veredicto = PertenenciaDeCentro.Juzgar(matriz, candidata);
            Assert.True(veredicto.Pertenece,
                $"MacZCS{n} deberia pertenecer a MacZ y el veredicto fue: {veredicto.Motivo}");
        }
    }

    [Fact]
    public async Task Tauro_RECHAZA_Las_Bases_De_Zaragoza_Que_Tiene_Mal_Capturadas()
    {
        if (!await HayBaseAsync("Produccion_svr")) return;
        if (!await HayBaseAsync("MacZCS7")) return;

        var repo = Repo("Produccion_svr");
        var matriz = await repo.DeLaMatrizAsync();

        Assert.True(matriz.EstaCompleta);
        _out.WriteLine($"Produccion_svr dice ser: '{matriz.NombreComercial}' ({matriz.Funcionalidad})");

        /* Las cuatro filas contaminadas de dbo.CentrosServicio de Tauro. */
        foreach (var n in new[] { 7, 8, 9, 10 })
        {
            var candidata = await repo.DeLaBaseAsync($"MacZCS{n}");
            if (!candidata.EstaCompleta) continue;

            var veredicto = PertenenciaDeCentro.Juzgar(matriz, candidata);

            Assert.False(veredicto.Pertenece,
                $"MacZCS{n} es de Zaragoza y Tauro no deberia poder abrirla.");
            _out.WriteLine($"MacZCS{n} rechazada: {veredicto.Motivo}");
        }
    }

    [Fact]
    public async Task Las_Dos_Matrices_Son_Empresas_DISTINTAS_Y_La_Base_Lo_Dice()
    {
        if (!await HayBaseAsync("MacZ")) return;
        if (!await HayBaseAsync("Produccion_svr")) return;

        var zaragoza = await Repo("MacZ").DeLaMatrizAsync();
        var tauro = await Repo("Produccion_svr").DeLaMatrizAsync();

        Assert.False(zaragoza.EsLaMismaEmpresaQue(tauro));
    }

    [Fact]
    public async Task Una_Matriz_No_Pasa_Como_Centro_De_Si_Misma()
    {
        if (!await HayBaseAsync("MacZ")) return;

        var repo = Repo("MacZ");
        var matriz = await repo.DeLaMatrizAsync();

        /* Si una fila de CentrosServicio apuntara a la base principal, el
           veredicto tiene que ser NO: EsCentroServicio viene en 0 ahi. */
        var veredicto = PertenenciaDeCentro.Juzgar(matriz, await repo.DeLaBaseAsync("MacZ"));
        Assert.False(veredicto.Pertenece);
    }

    [Fact]
    public async Task El_Nombre_Comercial_Esta_Capturado_Y_No_Es_La_Palabra_Principal()
    {
        /* Es la fuente del `matriz.nombre` de GET /api/centros. Si estuviera
           vacio habria que cambiar de fuente, y eso es exactamente lo que esta
           prueba esta aqui para avisar. */
        foreach (var baseDatos in new[] { "MacZ", "Produccion_svr" })
        {
            if (!await HayBaseAsync(baseDatos)) continue;

            var matriz = await Repo(baseDatos).DeLaMatrizAsync();

            Assert.NotEqual("", matriz.NombreComercial);
            Assert.NotEqual("Principal", matriz.NombreComercial);
            _out.WriteLine($"{baseDatos} -> '{matriz.NombreComercial}'");
        }
    }

    [Fact]
    public async Task Una_Base_Que_No_Existe_Falla_CERRADO_Y_No_Revienta()
    {
        if (!await HayBaseAsync("MacZ")) return;

        var identidad = await Repo("MacZ").DeLaBaseAsync("MacZCS_que_no_existe");

        Assert.Equal(IdentidadDeEmpresa.Desconocida, identidad);
        Assert.False(PertenenciaDeCentro.Juzgar(
            await Repo("MacZ").DeLaMatrizAsync(), identidad).Pertenece);
    }

    [Fact]
    public async Task El_Catalogo_De_Tauro_Sigue_Contaminado_Y_No_Lo_Arreglamos_Nosotros()
    {
        /* Esta prueba NO corrige nada: documenta el estado de los datos y falla
           el dia que cambie, para que nadie se entere por sorpresa. Las seis
           primeras filas con BaseCentro vacio y las cuatro ultimas apuntando a
           Zaragoza, medido el 2026-10-15 en la copia local. */
        if (!await HayBaseAsync("Produccion_svr")) return;

        await using var conn = new SqlConnection(CadenaDe("Produccion_svr"));
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT IDCentro, ISNULL(BaseCentro,'') FROM dbo.CentrosServicio WHERE IDStatus = 1 ORDER BY Numero;",
            conn);

        var ajenas = 0;
        var vacias = 0;
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var nombre = CentrosServicioRepository.NombreDeLaBase(rd.GetString(1));
            if (nombre.Length == 0) { vacias++; continue; }
            if (nombre.StartsWith("MacZCS", StringComparison.OrdinalIgnoreCase)) ajenas++;
        }

        _out.WriteLine($"Produccion_svr: {vacias} filas sin base y {ajenas} apuntando a bases de Zaragoza.");
        Assert.True(vacias > 0 || ajenas > 0,
            "El catalogo de Tauro ya esta limpio. Si alguien lo corrigio en la base, esta prueba sobra; la proteccion en codigo se queda igual.");
    }
}
