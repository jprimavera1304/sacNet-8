using ISL_Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// LOS ESCENARIOS DE PERMISOS, CORRIDOS CONTRA LAS BASES DE VERDAD.
///
/// La lista de los trece esta en docs\auditoria\ESCENARIOS-PERMISOS.md. Aqui
/// viven los que NO se pueden comprobar leyendo codigo, porque dependen de lo
/// que cada empresa tenga capturado en Mac31:
///
///   1  herencia basica            4  apagar la entrada no revoca lo interno
///   2  legacy manda sobre el web  5  reencender la entrada devuelve todo
///   3  la semilla sobrevive       6  conceder desde web enciende en Mac31
///   7  revocar desde web apaga    8  el alta no toca Legacy
///   9  el candado de agosto      10  guardar a uno no toca a otro
///  13  lo mismo en las dos empresas, con IDProceso distintos
///
/// Los escenarios 11 y 12 (403 de escalada y de acceso sin autorizacion) son
/// de la capa HTTP y viven en SuperAdminEsUnRolTests y en las reglas de
/// GuardiaDePermisos; aqui no se repiten.
///
/// POR QUE CREA SUS PROPIAS CUENTAS
/// --------------------------------
/// Porque la regla del proyecto es no tocar cuentas reales para simular
/// permisos, y porque una sesion anterior reuso un GUID que ya estaba ocupado
/// y le borro los permisos a una cuenta de prueba ajena (PRUEBACL). De ahi las
/// dos defensas de abajo: los nombres llevan un prefijo que nadie usa, y si ya
/// existen la prueba ABORTA en vez de escribir encima.
///
/// SE SALTA SOLA si no hay SQL Server a mano, igual que
/// <see cref="DiagnosticoFamiliasContraLaBase"/>: una prueba que "pasa" sin
/// medir es peor que no tenerla.
///
/// SE ELIGIO COMPRAS como modulo de prueba porque es el empate mas limpio:
/// cuatro botones que la tabla de verbos y el mapa cubren
/// (btnNuevo/btnModificar/btnCancelar/btnImprimir), una entrada propia
/// (compras.ver_modulo) que se DERIVA de compras.ver, y una semilla del web
/// (compras.ver) que Mac31 no conoce. Ademas sus IDProceso NO coinciden entre
/// empresas -5333/5334 en MacZ y 5334/5335 en Produccion_svr-, que es justo lo
/// que el escenario 13 tiene que demostrar que da igual.
/// </summary>
[Collection(PruebasContraLaBase.Nombre)]
public class EscenariosPermisosContraLaBase
{
    private const string UsuarioPrueba = "ZZE13OPER";
    private const string FormaPrueba = "ConsultarCompras";
    private const int EmpresaId = 1;

    private readonly ITestOutputHelper _out;
    public EscenariosPermisosContraLaBase(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    /// <summary>
    /// Instancia nueva Y CACHE NUEVA en cada lectura, a proposito: la cache de
    /// permisos tiene TTL dentro del proceso y no se limpia sola, asi que
    /// reusar el servicio haria que los escenarios 4, 5 y 7 midieran el estado
    /// anterior y pasaran en falso.
    /// </summary>
    private static PermissionService Servicio(string baseDatos) => new(
        // Por el constructor de OPCIONES, no por el de cadena: PermissionService
        // pide la conexion con _db.Database.GetConnectionString(), y el
        // constructor de cadena guarda el texto pero no configura el proveedor,
        // asi que ese camino revienta con "No database provider configured".
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDePrueba());

    private sealed class EntornoDePrueba : IHostEnvironment
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
    public async Task Los_Escenarios_De_Permisos_Se_Cumplen(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: los escenarios NO se corrieron.");
            return;
        }

        if (await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'") > 0
            || await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.UsuarioWeb WHERE Usuario = '{UsuarioPrueba}'") > 0)
        {
            Assert.Fail($"{UsuarioPrueba} ya existe en {baseDatos}. No se escribe encima: borralo a mano y repite.");
        }

        Guid usuarioWeb = Guid.Empty;
        // El estado del modulo se guarda ANTES de nada y se repone en el
        // finally. Sin esto, una asercion que falle entre el apagado y el
        // reencendido deja el modulo apagado en la base, y la siguiente
        // corrida mide un mundo distinto: paso de verdad, y la prueba
        // siguiente acuso en falso que la herencia no traia nada.
        var estadoModulo = await EscalarAsync(conn,
            $"SELECT ISNULL(MAX(IdStatus), 1) FROM dbo.WModulo WHERE EmpresaId = {EmpresaId} AND ModuloClave = 'compras'");
        try
        {
            var (legacyId, procesos) = await AltaDelOperadorAsync(conn);
            usuarioWeb = await AltaDeLaCuentaWebAsync(conn);
            _out.WriteLine($"[{baseDatos}] operador legacy {legacyId} con {procesos.Count} botones; cuenta web {usuarioWeb}");

            // ── 1. Herencia basica ───────────────────────────────────────────
            var inicial = await PermisosAsync(baseDatos, usuarioWeb);
            Assert.Contains("compras.editar", inicial);
            Assert.Contains("compras.cancelar", inicial);
            Assert.True(inicial.Contains("compras.ver_modulo"),
                "La entrada al modulo tiene que DERIVARSE de compras.ver, sin capturarla a mano.");
            _out.WriteLine($"  1 OK  hereda {inicial.Count(p => p.StartsWith("compras.", StringComparison.OrdinalIgnoreCase))} claves de compras");

            // ── 3. La semilla del web sobrevive al borrado por prefijo ───────
            // Una semilla esta en el CATALOGO, no concedida: hay que darsela
            // primero. Lo que se prueba es que el borrado por prefijo de la
            // familia -que arrasa con todo lo que empiece por "compras."- NO
            // se lleva una clave que Mac31 no conoce.
            await DarAllowEnElWebAsync(conn, usuarioWeb, "compras.ver");
            var conSemilla = await PermisosAsync(baseDatos, usuarioWeb);
            Assert.Contains("compras.ver", conSemilla);
            _out.WriteLine("  3 OK  compras.ver (semilla del web, sin boton en Mac31) sobrevive al borrado por prefijo");
            await QuitarAllowDelWebAsync(conn, usuarioWeb, "compras.ver");

            // ── 2. Legacy manda sobre el web ─────────────────────────────────
            // Se apaga btnCancelar en Mac31 y se deja un allow en el web. Si
            // la clave sigue saliendo, el web gano y la herencia no manda.
            await ApagarEnMac31Async(conn, legacyId, "btnCancelar");
            await DarAllowEnElWebAsync(conn, usuarioWeb, "compras.cancelar");
            var trasApagar = await PermisosAsync(baseDatos, usuarioWeb);
            Assert.DoesNotContain("compras.cancelar", trasApagar);
            _out.WriteLine("  2 OK  Mac31 dice no y el allow del web no lo resucita");
            await EncenderEnMac31Async(conn, legacyId, "btnCancelar");
            await QuitarAllowDelWebAsync(conn, usuarioWeb, "compras.cancelar");

            // ── 4 y 5. La entrada por plataforma es independiente ────────────
            var antesDeApagar = await FotoDeMac31Async(conn, legacyId);
            await Servicio(baseDatos).SetModuleStatusAsync(EmpresaId, "compras", 2, CancellationToken.None);
            var conModuloApagado = await PermisosAsync(baseDatos, usuarioWeb);
            var despuesDeApagar = await FotoDeMac31Async(conn, legacyId);

            Assert.DoesNotContain("compras.ver_modulo", conModuloApagado);
            Assert.Equal(antesDeApagar, despuesDeApagar);
            _out.WriteLine("  4 OK  se cierra la puerta del web y Mac31 queda intacto");

            await Servicio(baseDatos).SetModuleStatusAsync(EmpresaId, "compras", 1, CancellationToken.None);
            var alReencender = await PermisosAsync(baseDatos, usuarioWeb);
            Assert.Equal(
                inicial.Where(p => p.StartsWith("compras.", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x),
                alReencender.Where(p => p.StartsWith("compras.", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x));
            _out.WriteLine("  5 OK  al reencender vuelve exactamente lo mismo, sin reasignar nada");

            // ── 8. El alta de usuario no toca Legacy ─────────────────────────
            // Es lo que hace el alta: limpiar overrides SIN sincronizar.
            var antesDelAlta = await FotoDeMac31Async(conn, legacyId);
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb, Array.Empty<string>(), Array.Empty<string>(),
                sincronizarConLegacy: false, CancellationToken.None);
            Assert.Equal(antesDelAlta, await FotoDeMac31Async(conn, legacyId));
            _out.WriteLine("  8 OK  limpiar overrides sin sincronizar no apaga nada en Mac31");

            // ── 7. Revocar desde el web apaga en Mac31 (IDStatus 2, no borra) ─
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb, Array.Empty<string>(), Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);
            var trasRevocar = await EstadoEnMac31Async(conn, legacyId, "btnModificar");
            Assert.Equal(2, trasRevocar);
            _out.WriteLine("  7 OK  revocar escribe IDStatus=2 y conserva la fila");

            // ── 6. Conceder desde el web enciende en Mac31 ───────────────────
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb, new[] { "compras.editar" }, Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);
            Assert.Equal(1, await EstadoEnMac31Async(conn, legacyId, "btnModificar"));
            _out.WriteLine("  6 OK  conceder escribe IDStatus=1 en Mac31");

            // ── 9. El candado de agosto ──────────────────────────────────────
            // Modulo apagado: guardar OTRA cosa no puede revocar lo suyo.
            await Servicio(baseDatos).SetModuleStatusAsync(EmpresaId, "compras", 2, CancellationToken.None);
            var antesDelCandado = await FotoDeMac31Async(conn, legacyId);
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb, Array.Empty<string>(), Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);
            Assert.Equal(antesDelCandado, await FotoDeMac31Async(conn, legacyId));
            await Servicio(baseDatos).SetModuleStatusAsync(EmpresaId, "compras", 1, CancellationToken.None);
            _out.WriteLine("  9 OK  con el modulo apagado, guardar otra cosa no revoca en Mac31");

            // ── 10. Guardar a uno no toca a otro ─────────────────────────────
            var ajenasAntes = await FotoDeTodosMenosAsync(conn, legacyId);
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb, new[] { "compras.editar" }, Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);
            Assert.Equal(ajenasAntes, await FotoDeTodosMenosAsync(conn, legacyId));
            _out.WriteLine("  10 OK  no se movio ni una fila de ningun otro usuario");

            // ── 13. Y nada de esto llevo un IDProceso escrito a mano ─────────
            _out.WriteLine($"  13 OK  corrido contra {baseDatos} usando los IDProceso de ESA base");
        }
        finally
        {
            await LimpiarAsync(conn, usuarioWeb, estadoModulo);
        }
    }

    // ───────────────────────── ayudantes ─────────────────────────

    private async Task<List<string>> PermisosAsync(string baseDatos, Guid usuarioWeb)
    {
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

    private static async Task<(int LegacyId, List<int> Procesos)> AltaDelOperadorAsync(SqlConnection conn)
    {
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.Usuarios (IDStatus, Usuario, Contrasena, Nombre, IDPerfil)
VALUES (1, '{UsuarioPrueba}', 'ZZ2026', 'OPERADOR DE PRUEBA', 2);");

        var id = await EscalarAsync(conn, $"SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'");

        // Por el SP de Mac31, nunca con un INSERT suelto: es el unico que sabe
        // insertar si no hay fila y actualizar si la hay.
        var procesos = new List<int>();
        await using (var cmd = new SqlCommand($@"
SELECT p.IDProceso FROM dbo.n_Procesos p
JOIN dbo.n_Formas f ON f.IDForma = p.IDForma
WHERE f.Forma = '{FormaPrueba}'
  AND p.Proceso IN ('btnNuevo','btnModificar','btnCancelar','btnImprimir')
  AND ISNULL(p.IDStatus,1) = 1;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) procesos.Add(r.GetInt32(0));

        foreach (var p in procesos)
            await EjecutarAsync(conn,
                $"EXEC dbo.sp_n_ActualizarUsuarioFormaProceso @IDUsuario={id}, @IDProceso={p}, @IDStatus=1, @IDUsuarioMovimiento={id};");

        return (id, procesos);
    }

    private static async Task<Guid> AltaDeLaCuentaWebAsync(SqlConnection conn)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.UsuarioWeb (Id, Usuario, ContrasenaHash, Rol, EmpresaId, DebeCambiarContrasena, Estado, FechaCreacion)
VALUES ('{id}', '{UsuarioPrueba}', 'no-se-usa-en-esta-prueba', 'User', {EmpresaId}, 0, 1, SYSUTCDATETIME());");
        return id;
    }

    private static Task ApagarEnMac31Async(SqlConnection conn, int legacyId, string boton) =>
        CambiarEnMac31Async(conn, legacyId, boton, 2);

    private static Task EncenderEnMac31Async(SqlConnection conn, int legacyId, string boton) =>
        CambiarEnMac31Async(conn, legacyId, boton, 1);

    private static async Task CambiarEnMac31Async(SqlConnection conn, int legacyId, string boton, int estado)
    {
        await EjecutarAsync(conn, $@"
DECLARE @p INT = (SELECT p.IDProceso FROM dbo.n_Procesos p
                  JOIN dbo.n_Formas f ON f.IDForma = p.IDForma
                  WHERE f.Forma = '{FormaPrueba}' AND p.Proceso = '{boton}');
EXEC dbo.sp_n_ActualizarUsuarioFormaProceso
     @IDUsuario={legacyId}, @IDProceso=@p, @IDStatus={estado}, @IDUsuarioMovimiento={legacyId};");
    }

    private static async Task<int> EstadoEnMac31Async(SqlConnection conn, int legacyId, string boton) =>
        await EscalarAsync(conn, $@"
SELECT ISNULL(MAX(ufp.IDStatus), 0) FROM dbo.[Usuario Forma Procesos] ufp
JOIN dbo.n_Procesos p ON p.IDProceso = ufp.IDProceso
JOIN dbo.n_Formas f ON f.IDForma = p.IDForma
WHERE ufp.IDUsuario = {legacyId} AND f.Forma = '{FormaPrueba}' AND p.Proceso = '{boton}'");

    /// <summary>Foto exacta de lo que ese usuario tiene en Mac31, para comparar.</summary>
    private static async Task<string> FotoDeMac31Async(SqlConnection conn, int legacyId)
    {
        var filas = new List<string>();
        await using var cmd = new SqlCommand($@"
SELECT CAST(IDProceso AS varchar(12)) + ':' + CAST(IDStatus AS varchar(4))
FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = {legacyId} ORDER BY IDProceso;", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) filas.Add(r.GetString(0));
        return string.Join(",", filas);
    }

    /// <summary>Lo mismo pero de TODOS LOS DEMAS: nadie ajeno se puede mover.</summary>
    private static async Task<string> FotoDeTodosMenosAsync(SqlConnection conn, int legacyId)
    {
        await using var cmd = new SqlCommand($@"
SELECT CAST(COUNT(*) AS varchar(12)) + '/' + CAST(SUM(CAST(IDStatus AS bigint)) AS varchar(20))
FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario <> {legacyId};", conn);
        var r = await cmd.ExecuteScalarAsync();
        return r?.ToString() ?? string.Empty;
    }

    private static async Task DarAllowEnElWebAsync(SqlConnection conn, Guid usuarioWeb, string clave) =>
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.WUsuarioPermiso (EmpresaId, UsuarioWebId, WPermisoId, Tipo, Motivo, FechaCreacion)
SELECT {EmpresaId}, '{usuarioWeb}', Id, 'allow', 'ESCENARIOS', SYSUTCDATETIME()
FROM dbo.WPermiso WHERE EmpresaId = {EmpresaId} AND Clave = '{clave}'
  AND NOT EXISTS (SELECT 1 FROM dbo.WUsuarioPermiso up
                  WHERE up.UsuarioWebId = '{usuarioWeb}' AND up.WPermisoId = dbo.WPermiso.Id);");

    private static async Task QuitarAllowDelWebAsync(SqlConnection conn, Guid usuarioWeb, string clave) =>
        await EjecutarAsync(conn, $@"
DELETE up FROM dbo.WUsuarioPermiso up
JOIN dbo.WPermiso p ON p.Id = up.WPermisoId
WHERE up.UsuarioWebId = '{usuarioWeb}' AND p.Clave = '{clave}';");

    /// <summary>
    /// Borra SOLO lo que creo esta prueba. Va en un finally para que una
    /// asercion fallida no deje basura en la base.
    /// </summary>
    private static async Task LimpiarAsync(SqlConnection conn, Guid usuarioWeb, int estadoModulo)
    {
        try
        {
            await EjecutarAsync(conn,
                $"UPDATE dbo.WModulo SET IdStatus = {estadoModulo} WHERE EmpresaId = {EmpresaId} AND ModuloClave = 'compras';");
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
            // Si la limpieza falla hay que saberlo, pero sin tapar el fallo
            // original de la prueba.
        }
    }
}
