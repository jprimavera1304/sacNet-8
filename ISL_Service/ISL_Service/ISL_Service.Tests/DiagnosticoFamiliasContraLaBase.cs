using System.Collections;
using System.Reflection;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// LA UNICA COMPROBACION QUE DE VERDAD SABE SI ALGUIEN PIERDE UN PERMISO.
///
/// POR QUE NO BASTA CON LEER EL CODIGO
/// -----------------------------------
/// El puente borra del snapshot todo lo que empiece con el prefijo de la
/// familia y lo vuelve a poner desde los botones de Mac31. Si un boton NO
/// existe en <c>n_Procesos</c>, la clave se borra y no se repone: el permiso
/// desaparece sin error, sin excepcion y sin una linea en el log.
///
/// Si esa clave la exige ademas un controlador, el endpoint queda en 403
/// permanente para todo el mundo menos el SuperAdmin.
///
/// Y si eso pasa o no DEPENDE DE LOS DATOS DE CADA EMPRESA, no del codigo:
/// <c>gasolinas.editar</c> es el ejemplo -en esa pantalla el boton MODIFICAR
/// se llama <c>btnVer</c>, asi que la tabla de verbos nunca produce
/// <c>gasolinas.editar</c> por mucho que el prefijo lo prometa-. Ninguna
/// prueba que solo lea los .cs puede responder esto, y
/// <see cref="FamiliasLegacyNoSePierdenTests"/> llego a acusar en falso por
/// intentarlo.
///
/// SE SALTA SOLA si no hay base a mano, para que el resto del suite siga
/// corriendo en una maquina sin SQL Server.
/// </summary>
[Collection(PruebasContraLaBase.Nombre)]
public class DiagnosticoFamiliasContraLaBase
{
    private readonly ITestOutputHelper _out;
    public DiagnosticoFamiliasContraLaBase(ITestOutputHelper salida) => _out = salida;

    private static readonly Type Tipo = typeof(PermissionService);

    private static object LeerCampo(string nombre) =>
        Tipo.GetField(nombre, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static object? Prop(object o, string n) => o.GetType().GetProperty(n)?.GetValue(o);

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Nadie_Pierde_Un_Permiso_Ni_Queda_Un_Endpoint_En_403(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try
        {
            await conn.OpenAsync();
        }
        catch (SqlException)
        {
            // Sin base no se puede medir, y una prueba que "pasa" sin medir
            // seria peor que no tenerla. Se dice y se sale.
            _out.WriteLine($"No hay conexion a {baseDatos}: la comprobacion NO se hizo.");
            return;
        }

        var verbos = (IDictionary)LeerCampo("VerbosLegacy");

        // Clave que Mac31 produce de verdad en ESTA base -> familia que la pone.
        var producidas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var familias = new List<(string Prefijo, HashSet<string> Semillas)>();
        var formasVacias = new List<string>();

        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Prop(familia!, "Prefijo")!;
            var semillas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in (IEnumerable)Prop(familia!, "SemillasDelWeb")!)
                semillas.Add((string)Prop(s!, "Key")!);
            familias.Add((prefijo, semillas));

            foreach (var forma in (IEnumerable)Prop(familia!, "Formas")!)
            {
                var formaExacta = (string?)Prop(forma!, "FormaExacta");
                var legacyForm = (string)Prop(forma!, "LegacyForm")!;
                var moduleKey = (string)Prop(forma!, "ModuleKey")!;
                var prefijoForma = (string?)Prop(forma!, "Prefijo") ?? moduleKey;
                var mapa = (IDictionary)Prop(forma!, "Map")!;

                // EL MISMO filtro que FiltroDeForma/ValorDeForma. Si aqui se
                // mirara otra forma que alla, la medicion no mediria nada.
                var filtro = formaExacta is null
                    ? "UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma"
                    : "f.Forma = @Forma";

                await using var cmd = new SqlCommand(
                    "SELECT p.Proceso FROM dbo.n_Formas f INNER JOIN dbo.n_Procesos p "
                    + "ON p.IDForma = f.IDForma WHERE " + filtro
                    + " AND ISNULL(p.IDStatus, 1) = 1;", conn);
                cmd.Parameters.AddWithValue("@Forma", formaExacta ?? legacyForm);

                var procesos = new List<string>();
                await using (var rd = await cmd.ExecuteReaderAsync())
                    while (await rd.ReadAsync())
                        procesos.Add(rd.IsDBNull(0) ? string.Empty : rd.GetString(0).Trim());

                if (procesos.Count == 0)
                    formasVacias.Add($"{formaExacta ?? legacyForm} (familia '{prefijo}')");

                foreach (var proc in procesos)
                {
                    string? clave = null;
                    if (mapa.Contains(proc))
                    {
                        var v = mapa[proc]!;
                        clave = (string)v.GetType().GetField("Item1")!.GetValue(v)!;
                    }
                    else if (verbos.Contains(proc))
                    {
                        clave = $"{prefijoForma}.{(string)verbos[proc]!}";
                    }

                    if (clave is not null)
                        producidas.TryAdd(clave, prefijo);
                }
            }
        }

        _out.WriteLine($"{baseDatos}: {producidas.Count} claves salen de Mac31.");
        foreach (var f in formasVacias)
            _out.WriteLine($"  [forma sin procesos activos] {f}");

        // La familia que gobierna una clave es la del prefijo MAS LARGO, igual
        // que EsLaFamiliaMasEspecifica.
        bool SeBorraYNoVuelve(string clave)
        {
            var duenas = familias
                .Where(f => clave.StartsWith(f.Prefijo, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.Prefijo.Length)
                .ToList();
            if (duenas.Count == 0) return false;            // no la gobierna nadie
            if (duenas[0].Semillas.Contains(clave)) return false; // es del web
            return !producidas.ContainsKey(clave);
        }

        var catalogo = new List<string>();
        foreach (var s in (IEnumerable)LeerCampo("PermissionCatalogSeeds"))
            catalogo.Add((string)Prop(s!, "Key")!);

        var seBorran = catalogo
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(SeBorraYNoVuelve)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var deControladores = (HashSet<string>)typeof(FamiliasLegacyNoSePierdenTests)
            .GetMethod("ClavesQueExigenLosControladores", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        var en403 = deControladores
            .Where(SeBorraYNoVuelve)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(seBorran.Count == 0,
            $"[{baseDatos}] Estas claves del catalogo se borrarian y nadie las repone:\n  - "
            + string.Join("\n  - ", seBorran));

        Assert.True(en403.Count == 0,
            $"[{baseDatos}] Estos endpoints quedarian en 403 permanente:\n  - "
            + string.Join("\n  - ", en403));
    }
}
