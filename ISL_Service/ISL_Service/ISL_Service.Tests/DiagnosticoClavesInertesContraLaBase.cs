using ISL_Service.Application.Security;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// CUANTAS CLAVES DEL CATALOGO NO DECIDEN NADA, Y QUIEN LAS TIENE.
/// </summary>
/// <remarks>
/// Es la medicion que respalda la decision de no ofrecerlas como
/// administrables. Imprime, por empresa: las claves del catalogo que
/// <see cref="ClavesQueProtegenAlgo"/> no reconoce, cuantas de ellas estan
/// concedidas a alguien, y cuantas vienen del puente con Mac31.
///
/// SE SALTA SOLA sin base, como el resto de los diagnosticos.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class DiagnosticoClavesInertesContraLaBase
{
    private readonly ITestOutputHelper _out;
    public DiagnosticoClavesInertesContraLaBase(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Cuenta_Las_Claves_Sin_Comprobacion(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"SIN BASE {baseDatos}: no se midio nada."); return; }

        var catalogo = new List<(string Clave, int Tenedores)>();
        const string sql = @"
SELECT p.Clave, COUNT(DISTINCT up.UsuarioWebId) AS Tenedores
FROM dbo.WPermiso p
LEFT JOIN dbo.WUsuarioPermiso up
    ON up.EmpresaId = p.EmpresaId AND up.WPermisoId = p.Id AND up.Tipo = 'allow'
WHERE p.EmpresaId = 1
GROUP BY p.Clave
ORDER BY p.Clave;";
        await using (var cmd = new SqlCommand(sql, conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) catalogo.Add((r.GetString(0).Trim(), r.GetInt32(1)));
        }

        var inertes = catalogo.Where(x => !ClavesQueProtegenAlgo.Protege(x.Clave)).ToList();
        var conTenedor = inertes.Where(x => x.Tenedores > 0).ToList();

        _out.WriteLine($"=== {baseDatos} ===");
        _out.WriteLine($"catalogo: {catalogo.Count}");
        _out.WriteLine($"protegen algo: {catalogo.Count - inertes.Count}");
        _out.WriteLine($"NO protegen nada: {inertes.Count}  (concedidas a alguien: {conTenedor.Count})");
        _out.WriteLine("");
        foreach (var grupo in inertes.GroupBy(x => x.Clave.Split('.')[0]).OrderByDescending(g => g.Count()))
            _out.WriteLine($"  {grupo.Key,-28} {grupo.Count(),3}   {string.Join(", ", grupo.Select(x => x.Clave).Take(6))}");

        // Deja la lista en disco para poder cruzarla con el front sin volver a
        // levantar nada. Carpeta de temporales: no entra al repo.
        var destino = Path.Combine(Path.GetTempPath(), $"inertes-{baseDatos}.txt");
        await File.WriteAllLinesAsync(destino, inertes.Select(x => $"{x.Clave}	{x.Tenedores}"));
        _out.WriteLine($"lista en: {destino}");

        _out.WriteLine("");
        _out.WriteLine("-- inertes QUE ALGUIEN TIENE CONCEDIDAS --");
        foreach (var x in conTenedor.OrderByDescending(x => x.Tenedores))
            _out.WriteLine($"  {x.Clave,-50} {x.Tenedores}");
    }
}
