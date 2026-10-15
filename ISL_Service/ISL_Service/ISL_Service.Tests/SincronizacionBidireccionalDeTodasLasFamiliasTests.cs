using System.Collections;
using System.Reflection;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ISL_Service.Infrastructure.Data;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// LAS CUATRO DIRECCIONES, FAMILIA POR FAMILIA, EN LAS DOS EMPRESAS.
/// </summary>
/// <remarks>
/// QUE SE PRUEBA Y POR QUE NO LO CUBRIA NADA
/// -----------------------------------------
/// <see cref="EscenariosPermisosContraLaBase"/> prueba la ida y la vuelta con
/// UNA familia (compras). Que compras funcione no dice nada de las otras
/// veintiuna: cada una tiene su propia forma de Mac31, su propio prefijo y su
/// propio mapa de botones, y un error de dedo en cualquiera de esos tres deja
/// esa familia leyendo bien y escribiendo a la nada —o al reves— sin que nada
/// se queje.
///
/// Por cada familia se miden las cuatro direcciones que pidio el encargo:
///
///   1. ENCENDER EN MAC31  -> la clave aparece en el snapshot del web.
///   2. APAGAR EN MAC31    -> la clave desaparece del snapshot.
///   3. CONCEDER EN EL WEB -> IDStatus = 1 en [Usuario Forma Procesos].
///   4. RETIRAR EN EL WEB  -> IDStatus = 2 (de baja, sin borrar la fila, que es
///                            como lo hace Mac31 en su propia pantalla).
///
/// COMO ELIGE EL BOTON
/// -------------------
/// No hay ni un IDProceso ni un nombre de boton escrito aqui: se lee la forma
/// de ESTA base, se cruza con el Map de la familia y con la tabla de verbos
/// —exactamente como lo hace el puente— y se toma el primer boton que produzca
/// una clave. Los IDProceso no coinciden entre MacZ y Produccion_svr, asi que
/// cualquier otra via mediria el boton equivocado.
///
/// Una familia se REPORTA COMO NO MEDIDA, y no como aprobada, cuando su modulo
/// esta apagado en esa empresa (con el modulo apagado la escritura hacia Mac31
/// esta bloqueada a proposito: es el candado del 2026-08-26) o cuando su forma
/// no tiene ni un boton que derive clave. Declararla OK sin medirla seria
/// exactamente lo que el encargo prohibe.
///
/// SEGURIDAD DE LA PRUEBA
/// ----------------------
/// Crea su propia cuenta —legacy y web—, aborta si el identificador ya existe,
/// y la borra en un finally junto con sus filas. No toca ninguna cuenta real ni
/// cambia el estatus de ningun modulo.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class SincronizacionBidireccionalDeTodasLasFamiliasTests
{
    private const string UsuarioPrueba = "ZZBIDI7";
    private const int EmpresaId = 1;

    private readonly ITestOutputHelper _out;
    public SincronizacionBidireccionalDeTodasLasFamiliasTests(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDePruebaBidi());

    private sealed class EntornoDePruebaBidi : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ISL_Service.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>Una familia, reducida a lo que esta prueba necesita.</summary>
    private sealed record FormaDeFamilia(
        string Prefijo, string Forma, bool PorNombreExacto, string ModuleKey,
        string PrefijoClave, IDictionary Map);

    private static object LeerCampo(string nombre) =>
        typeof(PermissionService).GetField(nombre, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static object? Prop(object o, string n) => o.GetType().GetProperty(n)?.GetValue(o);

    private static List<FormaDeFamilia> FormasDeLasFamilias()
    {
        var lista = new List<FormaDeFamilia>();
        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Prop(familia!, "Prefijo")!;
            foreach (var forma in (IEnumerable)Prop(familia!, "Formas")!)
            {
                var formaExacta = (string?)Prop(forma!, "FormaExacta");
                var legacyForm = (string)Prop(forma!, "LegacyForm")!;
                var moduleKey = (string)Prop(forma!, "ModuleKey")!;
                lista.Add(new FormaDeFamilia(
                    prefijo,
                    formaExacta ?? legacyForm,
                    formaExacta is not null,
                    moduleKey,
                    (string?)Prop(forma!, "Prefijo") ?? moduleKey,
                    (IDictionary)Prop(forma!, "Map")!));
            }
        }
        return lista;
    }

    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Las_Cuatro_Direcciones_Funcionan_En_Todas_Las_Familias(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: NO se midio nada.");
            return;
        }

        if (await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'") > 0
            || await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.UsuarioWeb WHERE Usuario = '{UsuarioPrueba}'") > 0)
        {
            Assert.Fail($"{UsuarioPrueba} ya existe en {baseDatos}. No se escribe encima.");
        }

        var verbos = (IDictionary)LeerCampo("VerbosLegacy");
        var modulosApagados = await ModulosApagadosAsync(conn);

        var fallos = new List<string>();
        var medidas = 0;
        var noMedidas = new List<string>();
        Guid usuarioWeb = Guid.Empty;

        try
        {
            var legacyId = await AltaLegacyAsync(conn);
            usuarioWeb = await AltaWebAsync(conn);
            _out.WriteLine($"=== {baseDatos} === cuenta aislada legacy {legacyId} / web {usuarioWeb}");

            foreach (var forma in FormasDeLasFamilias())
            {
                if (modulosApagados.Contains(forma.ModuleKey))
                {
                    noMedidas.Add($"{forma.Prefijo} ({forma.Forma}): su modulo '{forma.ModuleKey}' esta APAGADO en esta empresa");
                    continue;
                }

                var elegido = await BotonQueProduceClaveAsync(conn, forma, verbos);
                if (elegido is null)
                {
                    noMedidas.Add($"{forma.Prefijo} ({forma.Forma}): ningun boton de esta base deriva clave");
                    continue;
                }

                var (idProceso, boton, clave) = elegido.Value;
                medidas++;

                // ── 1. Encender en Mac31 -> aparece en el web ────────────────
                await SpAsync(conn, legacyId, idProceso, 1);
                var conEncendido = await PermisosAsync(baseDatos, usuarioWeb);
                if (!conEncendido.Contains(clave, StringComparer.OrdinalIgnoreCase))
                    fallos.Add($"[1 Legacy->Web ENCENDER] {forma.Prefijo}: {boton} encendido y '{clave}' NO llego al web");

                // ── 2. Apagar en Mac31 -> desaparece del web ─────────────────
                await SpAsync(conn, legacyId, idProceso, 2);
                var conApagado = await PermisosAsync(baseDatos, usuarioWeb);
                if (conApagado.Contains(clave, StringComparer.OrdinalIgnoreCase))
                    fallos.Add($"[2 Legacy->Web APAGAR] {forma.Prefijo}: {boton} apagado y '{clave}' SIGUE en el web");

                // ── 3. Conceder desde el web -> IDStatus 1 en Mac31 ──────────
                await Servicio(baseDatos).UpsertUserOverridesAsync(
                    EmpresaId, usuarioWeb, new[] { clave }, Array.Empty<string>(),
                    sincronizarConLegacy: true, CancellationToken.None);
                var trasConceder = await EstadoAsync(conn, legacyId, idProceso);
                if (trasConceder != 1)
                    fallos.Add($"[3 Web->Legacy CONCEDER] {forma.Prefijo}: se concedio '{clave}' y {boton} quedo en IDStatus={trasConceder}, no en 1");

                // ── 4. Retirar desde el web -> IDStatus 2, sin borrar fila ───
                await Servicio(baseDatos).UpsertUserOverridesAsync(
                    EmpresaId, usuarioWeb, Array.Empty<string>(), Array.Empty<string>(),
                    sincronizarConLegacy: true, CancellationToken.None);
                var trasRetirar = await EstadoAsync(conn, legacyId, idProceso);
                if (trasRetirar != 2)
                    fallos.Add($"[4 Web->Legacy RETIRAR] {forma.Prefijo}: se retiro '{clave}' y {boton} quedo en IDStatus={trasRetirar}, no en 2");

                _out.WriteLine($"  {forma.Prefijo,-26} {boton,-22} -> {clave}");
            }
        }
        finally
        {
            await LimpiarAsync(conn, usuarioWeb);
        }

        _out.WriteLine("");
        _out.WriteLine($"familias/formas medidas: {medidas}   fallos: {fallos.Count}");
        foreach (var n in noMedidas) _out.WriteLine($"  NO MEDIDA  {n}");

        Assert.True(fallos.Count == 0,
            $"[{baseDatos}] la sincronizacion falla en:\n  - " + string.Join("\n  - ", fallos));

        // Si no se midio ni una, la prueba no probo nada: eso es un fallo, no
        // un pase.
        Assert.True(medidas > 0, $"[{baseDatos}] no se pudo medir ninguna familia.");
    }

    // ───────────────────────── ayudantes ─────────────────────────

    /// <summary>
    /// El primer boton de esa forma que el puente convierte en clave, con la
    /// misma regla del puente: primero el Map, luego la tabla de verbos.
    /// </summary>
    private static async Task<(int IdProceso, string Boton, string Clave)?> BotonQueProduceClaveAsync(
        SqlConnection conn, FormaDeFamilia forma, IDictionary verbos)
    {
        var filtro = forma.PorNombreExacto
            ? "f.Forma = @Forma"
            : "UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma";

        await using var cmd = new SqlCommand(
            "SELECT p.IDProceso, p.Proceso FROM dbo.n_Formas f "
            + "INNER JOIN dbo.n_Procesos p ON p.IDForma = f.IDForma "
            + "WHERE " + filtro + " AND ISNULL(p.IDStatus, 1) = 1 ORDER BY p.IDProceso;", conn);
        cmd.Parameters.AddWithValue("@Forma", forma.Forma);

        var candidatos = new List<(int Id, string Boton)>();
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                candidatos.Add((r.GetInt32(0), r.IsDBNull(1) ? string.Empty : r.GetString(1).Trim()));

        foreach (var (id, boton) in candidatos)
        {
            if (forma.Map.Contains(boton))
            {
                var v = forma.Map[boton]!;
                var clave = (string)v.GetType().GetField("Item1")!.GetValue(v)!;
                return (id, boton, clave);
            }
            if (verbos.Contains(boton))
                return (id, boton, $"{forma.PrefijoClave}.{(string)verbos[boton]!}");
        }
        return null;
    }

    private static async Task<HashSet<string>> ModulosApagadosAsync(SqlConnection conn)
    {
        var apagados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(
            $"SELECT ModuloClave FROM dbo.WModulo WHERE EmpresaId = {EmpresaId} AND IdStatus <> 1;", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (!r.IsDBNull(0)) apagados.Add(r.GetString(0).Trim());
        return apagados;
    }

    private async Task<List<string>> PermisosAsync(string baseDatos, Guid usuarioWeb)
    {
        // Servicio NUEVO en cada lectura: la cache de permisos tiene TTL en
        // proceso, y reusar la instancia haria que la prueba leyera lo de hace
        // un rato en vez de lo que acaba de pasar en la base.
        var snap = await Servicio(baseDatos).GetPermissionsAsync(
            usuarioWeb, EmpresaId, "User", CancellationToken.None);
        return snap.Permissions;
    }

    private static async Task<int> EscalarAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        var r = await cmd.ExecuteScalarAsync();
        return r is null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
    }

    private static async Task EjecutarAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> AltaLegacyAsync(SqlConnection conn)
    {
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.Usuarios (IDStatus, Usuario, Contrasena, Nombre, IDPerfil)
VALUES (1, '{UsuarioPrueba}', 'ZZ2026', 'BIDIRECCIONAL DE PRUEBA', 2);");
        return await EscalarAsync(conn, $"SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'");
    }

    private static async Task<Guid> AltaWebAsync(SqlConnection conn)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.UsuarioWeb (Id, Usuario, ContrasenaHash, Rol, EmpresaId, DebeCambiarContrasena, Estado, FechaCreacion)
VALUES ('{id}', '{UsuarioPrueba}', 'no-se-usa-en-esta-prueba', 'User', {EmpresaId}, 0, 1, SYSUTCDATETIME());");
        return id;
    }

    /// <summary>
    /// Siempre por el procedimiento de Mac31, nunca con un INSERT suelto: es el
    /// unico que sabe insertar si no hay fila y actualizar si la hay.
    /// </summary>
    private static Task SpAsync(SqlConnection conn, int legacyId, int idProceso, int estado) =>
        EjecutarAsync(conn,
            $"EXEC dbo.sp_n_ActualizarUsuarioFormaProceso @IDUsuario={legacyId}, @IDProceso={idProceso}, @IDStatus={estado}, @IDUsuarioMovimiento={legacyId};");

    private static Task<int> EstadoAsync(SqlConnection conn, int legacyId, int idProceso) =>
        EscalarAsync(conn,
            $"SELECT ISNULL(MAX(IDStatus), 0) FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = {legacyId} AND IDProceso = {idProceso};");

    private static async Task LimpiarAsync(SqlConnection conn, Guid usuarioWeb)
    {
        try
        {
            if (usuarioWeb != Guid.Empty)
                await EjecutarAsync(conn, $"DELETE FROM dbo.WUsuarioPermiso WHERE UsuarioWebId = '{usuarioWeb}';");
            await EjecutarAsync(conn, $@"
DELETE FROM dbo.UsuarioWeb WHERE Usuario = '{UsuarioPrueba}';
DECLARE @u INT = (SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}');
IF @u IS NOT NULL
BEGIN
    DELETE FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = @u;
    DELETE FROM dbo.Usuarios WHERE IDUsuario = @u;
END");
        }
        catch (SqlException)
        {
            // Que la limpieza falle no debe tapar el fallo original.
        }
    }
}
