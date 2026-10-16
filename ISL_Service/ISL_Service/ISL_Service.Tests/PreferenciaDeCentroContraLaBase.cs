using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// QUE LA PREFERENCIA DE CENTRO SE GUARDE EN EL SERVIDOR Y SE PUEDA LEER.
/// </summary>
/// <remarks>
/// POR QUE CONTRA LA BASE
/// ----------------------
/// Porque lo unico que hay que comprobar aqui es lo que depende de la base: que
/// la tabla tenga la forma que el servicio espera, que una sola persona no pueda
/// terminar con dos preferencias, y -la importante- QUE SIN LA TABLA NO SE CAIGA
/// NADA. Esa ultima es la que decide si el script de la tabla se puede correr
/// antes o despues del despliegue, y no se puede comprobar sin una base.
///
/// SE SALTA SOLA si no hay SQL Server, y tambien si la tabla todavia no esta en
/// esa base: correr el script es una decision del despliegue, no de una prueba.
///
/// LIMPIA LO QUE ESCRIBE. Usa un usuario web de verdad -la tabla tiene llave
/// foranea- pero borra su preferencia al terminar, y si ese usuario ya tenia una
/// guardada, la deja como estaba.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class PreferenciaDeCentroContraLaBase
{
    private readonly ITestOutputHelper _out;
    public PreferenciaDeCentroContraLaBase(ITestOutputHelper salida) => _out = salida;

    private const int EmpresaId = 1;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PreferenciaDeCentroService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        NullLogger<PreferenciaDeCentroService>.Instance);

    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Se_Guarda_Se_Lee_Y_Se_Olvida(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        if (!await ExisteLaTablaAsync(conn))
        {
            _out.WriteLine($"{baseDatos}: dbo.WCentroPreferido todavia no esta "
                + "(falta correr Querrys/53/03). NO se midio nada.");
            return;
        }

        var usuario = await UnUsuarioWebAsync(conn);
        if (usuario is null)
        {
            _out.WriteLine($"{baseDatos}: no hay usuarios web. NO se midio nada.");
            return;
        }

        var servicio = Servicio(baseDatos);
        var ct = CancellationToken.None;
        var previa = await servicio.LeerAsync(EmpresaId, usuario.Value, ct);

        try
        {
            /* Guardar y leer. */
            Assert.True(await servicio.GuardarAsync(EmpresaId, usuario.Value, 4, ct));
            Assert.Equal(4, await servicio.LeerAsync(EmpresaId, usuario.Value, ct));

            /* Guardar OTRA cosa no deja dos filas: la clave primaria es por
               usuario y el guardado hace UPDATE antes de intentar INSERT. */
            Assert.True(await servicio.GuardarAsync(EmpresaId, usuario.Value, 9, ct));
            Assert.Equal(9, await servicio.LeerAsync(EmpresaId, usuario.Value, ct));
            Assert.Equal(1, await CuantasFilasAsync(conn, usuario.Value));

            /* LA MATRIZ ES UN VALOR GUARDADO VALIDO, no "sin preferencia". Quien
               eligio la matriz a proposito no se encuentra un centro al entrar. */
            Assert.True(await servicio.GuardarAsync(EmpresaId, usuario.Value, CentroInicial.Matriz, ct));
            Assert.Equal(CentroInicial.Matriz, await servicio.LeerAsync(EmpresaId, usuario.Value, ct));

            /* Olvidar deja "nunca eligio", que es distinto de "eligio la matriz"
               solo en que la regla del primero del catalogo vuelve a decidir. */
            Assert.True(await servicio.OlvidarAsync(EmpresaId, usuario.Value, ct));
            Assert.Null(await servicio.LeerAsync(EmpresaId, usuario.Value, ct));

            _out.WriteLine($"{baseDatos}: guardar / releer / olvidar OK, una sola fila por usuario.");
        }
        finally
        {
            await servicio.OlvidarAsync(EmpresaId, usuario.Value, ct);
            if (previa is int antes)
                await servicio.GuardarAsync(EmpresaId, usuario.Value, antes, ct);
        }
    }

    /// <summary>
    /// QUE UN USUARIO DESCONOCIDO NO TENGA PREFERENCIA Y NO REVIENTE.
    /// </summary>
    /// <remarks>
    /// Es el camino de todos los dias: la inmensa mayoria nunca eligio nada.
    /// Contestar null es lo que hace que CentroInicial caiga al primero del
    /// catalogo, o sea a la matriz, o sea a lo que pasa hoy.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Quien_Nunca_Eligio_No_Tiene_Preferencia(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        var nadie = Guid.NewGuid();
        Assert.Null(await Servicio(baseDatos).LeerAsync(EmpresaId, nadie, CancellationToken.None));
    }

    /// <summary>
    /// QUE SIN LA TABLA NO SE CAIGA NADA, Y ES LA PRUEBA QUE DECIDE EL ORDEN DEL
    /// DESPLIEGUE.
    /// </summary>
    /// <remarks>
    /// Si el servicio tirara una excepcion con la tabla ausente, el script de
    /// Querrys/53/03 tendria que correr OBLIGATORIAMENTE antes del backend, y
    /// equivocarse en ese orden dejaria el sistema sin contestar. Asi no: sin
    /// tabla se contesta "sin preferencia", todo el mundo abre en la matriz -que
    /// es lo que pasa hoy- y el script se puede correr cuando se quiera.
    ///
    /// Se comprueba apuntando a una base que seguro no tiene la tabla, en vez de
    /// borrarsela a una de verdad.
    /// </remarks>
    [Fact]
    public async Task Sin_La_Tabla_Contesta_Sin_Preferencia_Y_No_Revienta()
    {
        await using var conn = new SqlConnection(CadenaDe("master"));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine("Sin SQL Server: NO se midio nada."); return; }

        var servicio = Servicio("master");
        var alguien = Guid.NewGuid();
        var ct = CancellationToken.None;

        Assert.Null(await servicio.LeerAsync(EmpresaId, alguien, ct));
        Assert.False(await servicio.GuardarAsync(EmpresaId, alguien, 4, ct));
        Assert.False(await servicio.OlvidarAsync(EmpresaId, alguien, ct));
    }

    private static async Task<bool> ExisteLaTablaAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID('dbo.WCentroPreferido','U') IS NOT NULL THEN 1 ELSE 0 END;", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    private static async Task<Guid?> UnUsuarioWebAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 Id FROM dbo.UsuarioWeb WHERE EmpresaId = @E ORDER BY Usuario;", conn);
        cmd.Parameters.AddWithValue("@E", EmpresaId);
        var valor = await cmd.ExecuteScalarAsync();
        return valor is Guid g ? g : null;
    }

    private static async Task<int> CuantasFilasAsync(SqlConnection conn, Guid usuario)
    {
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.WCentroPreferido WHERE EmpresaId = @E AND UsuarioWebId = @U;", conn);
        cmd.Parameters.AddWithValue("@E", EmpresaId);
        cmd.Parameters.AddWithValue("@U", usuario);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
