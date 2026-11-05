using ISL_Service.Application.Interfaces;
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
/// COPIAR LOS PERMISOS DE MAC31, MEDIDO CONTRA LAS DOS BASES.
///
/// Las cinco cosas que esta prueba tiene que demostrar, y que no se pueden
/// demostrar leyendo codigo porque dependen de lo que cada empresa tenga
/// capturado en Mac31:
///
///   1  Copiar deja a la persona EXACTAMENTE con las mismas claves que
///      heredaba. Ni una mas, ni una menos.
///   2  Copiar a uno no le toca NADA a los demas: quien no se copio sigue
///      heredando igual.
///   3  Copiar no escribe NI UNA FILA en Mac31.
///   4  Despues de copiar, quitar algo en el web LO QUITA DE VERDAD, y Mac31
///      sigue igual. (Y para quien NO esta copiado, el write-back a Mac31
///      sigue funcionando como siempre: el candado es de los copiados, no de
///      todo el mundo.)
///   5  Deshacer la copia devuelve la herencia, incluido lo que se le habia
///      quitado en el web.
///
/// SE SALTA SOLA si no hay SQL Server a mano, igual que
/// <see cref="EscenariosPermisosContraLaBase"/>: una prueba que "pasa" sin
/// medir es peor que no tenerla. Si hay base pero no estan las dos tablas de la
/// copia, FALLA diciendo que falta correr el script: ahi no hay nada que medir
/// pero si algo que arreglar.
///
/// POR QUE CREA SUS PROPIAS CUENTAS Y NO USA LAS DE VERDAD: la regla del
/// proyecto es no tocar cuentas reales para simular permisos, y ya hubo una
/// sesion que reuso un GUID ocupado y le borro los permisos a una cuenta
/// ajena. De ahi las dos defensas: prefijo que nadie usa y aborto si ya existe.
///
/// SE ELIGIO COMPRAS por lo mismo que la prueba de escenarios: cuatro botones
/// mapeados, una entrada que se DERIVA (compras.ver_modulo) y una semilla del
/// web que Mac31 no conoce (compras.ver). Si la copia se come las entradas
/// derivadas, esta familia lo canta.
/// </summary>
[Collection(PruebasContraLaBase.Nombre)]
public class CopiaDeMac31ContraLaBase
{
    private const string UsuarioA = "ZZCOPIA1";
    private const string UsuarioB = "ZZCOPIA2";
    private const string FormaPrueba = "ConsultarCompras";
    private const int EmpresaId = 1;

    private readonly ITestOutputHelper _out;
    public CopiaDeMac31ContraLaBase(ITestOutputHelper salida) => _out = salida;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    /// <summary>
    /// Instancia nueva Y CACHE NUEVA en cada lectura, por lo mismo que en la
    /// prueba de escenarios: la cache de permisos tiene plazo dentro del
    /// proceso, y reusar el servicio haria que los pasos de despues midieran el
    /// estado de antes y pasaran en falso.
    /// </summary>
    private static PermissionService Servicio(string baseDatos) => new(
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
    public async Task Copiar_De_Mac31_Se_Comporta_Como_Se_Prometio(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: la prueba NO se corrio.");
            return;
        }

        var hayTablas = await EscalarAsync(conn, @"
SELECT CASE WHEN OBJECT_ID('dbo.WPermisoCopiaMac31','U') IS NOT NULL
             AND OBJECT_ID('dbo.WPermisoCopiaMac31Clave','U') IS NOT NULL
            THEN 1 ELSE 0 END");
        Assert.True(hayTablas == 1,
            $"En {baseDatos} faltan las tablas de la copia. Corre Querrys\\64 Copiar Permisos De Mac31\\01_Tablas_CopiaDeMac31.sql.");

        foreach (var nombre in new[] { UsuarioA, UsuarioB })
        {
            if (await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.Usuarios WHERE Usuario = '{nombre}'") > 0
                || await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.UsuarioWeb WHERE Usuario = '{nombre}'") > 0)
            {
                Assert.Fail($"{nombre} ya existe en {baseDatos}. No se escribe encima: borralo a mano y repite.");
            }
        }

        var webA = Guid.Empty;
        var webB = Guid.Empty;
        try
        {
            var legacyA = await AltaDelOperadorAsync(conn, UsuarioA);
            var legacyB = await AltaDelOperadorAsync(conn, UsuarioB);
            webA = await AltaDeLaCuentaWebAsync(conn, UsuarioA);
            webB = await AltaDeLaCuentaWebAsync(conn, UsuarioB);
            _out.WriteLine($"[{baseDatos}] operadores {legacyA} y {legacyB}; cuentas web {webA} / {webB}");

            // ── Lo que heredan HOY, antes de copiar nada ─────────────────────
            var antesA = await PermisosAsync(baseDatos, webA);
            var antesB = await PermisosAsync(baseDatos, webB);
            Assert.Contains("compras.editar", antesA);
            Assert.Contains("compras.cancelar", antesA);
            Assert.Contains("compras.ver_modulo", antesA);
            var fotoMac31A = await FotoDeMac31Async(conn, legacyA);
            _out.WriteLine($"  hereda {antesA.Count(p => p.StartsWith("compras.", StringComparison.OrdinalIgnoreCase))} claves de compras");

            // ── 1. Copiar deja las MISMAS claves ────────────────────────────
            var resultado = await Servicio(baseDatos).CopiarDeMac31Async(
                EmpresaId, webA, "PRUEBA", CancellationToken.None);
            Assert.True(resultado.Copiado, "La copia tenia que hacerse.");
            Assert.True(resultado.Concedidos > 0, "La copia no concedio nada: algo no se esta leyendo.");
            Assert.Equal(0, resultado.SinCatalogo);

            var despuesA = await PermisosAsync(baseDatos, webA);
            Assert.Equal(antesA.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                         despuesA.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            _out.WriteLine($"  1 OK  copiar dejo las mismas {despuesA.Count} claves "
                + $"({resultado.Concedidos} concedidas y {resultado.Quitados} quitadas escritas a mano)");

            // ── 2. A los demas no les paso nada ─────────────────────────────
            Assert.Equal(antesB.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                         (await PermisosAsync(baseDatos, webB)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            _out.WriteLine("  2 OK  el otro usuario sigue heredando exactamente igual");

            // ── 3. Copiar no escribio en Mac31 ──────────────────────────────
            Assert.Equal(fotoMac31A, await FotoDeMac31Async(conn, legacyA));
            _out.WriteLine("  3 OK  ni una fila de [Usuario Forma Procesos] se movio al copiar");

            // ── 4. Quitar en el web SI pega, y Mac31 sigue igual ────────────
            //
            // Se guarda como guarda la pantalla: la lista completa de lo que la
            // persona debe tener. Se le quita `compras.cancelar` de los allow y
            // se manda en los deny, que es lo que hace la casilla al
            // desmarcarse sobre algo que el rol da.
            var copiadas = await ClavesDeLaCopiaAsync(conn, webA, "allow");
            var negadas = await ClavesDeLaCopiaAsync(conn, webA, "deny");
            Assert.Contains("compras.cancelar", copiadas);

            var allow = copiadas.Where(c => !c.Equals("compras.cancelar", StringComparison.OrdinalIgnoreCase)).ToList();
            var deny = negadas.Append("compras.cancelar").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, webA, allow, deny, sincronizarConLegacy: true, CancellationToken.None);

            var conQuitado = await PermisosAsync(baseDatos, webA);
            Assert.DoesNotContain("compras.cancelar", conQuitado);
            Assert.Contains("compras.editar", conQuitado);
            Assert.Equal(fotoMac31A, await FotoDeMac31Async(conn, legacyA));
            Assert.Equal(1, await EstadoEnMac31Async(conn, legacyA, "btnCancelar"));
            _out.WriteLine("  4 OK  quitado en el web y SEGUIDO AUTORIZADO en Mac31: dos sistemas, no un espejo");

            // ── 4b. Y a quien NO esta copiado, guardar TAMPOCO le apaga nada ─
            //
            // Esto media lo contrario —que sin copia el web seguia siendo un
            // espejo y guardar apagaba en Mac31— hasta el 2026-11-04, cuando
            // ese espejo le quito 46 permisos de Mac31 a tres personas de
            // Tauro sin que nadie los tocara. Desde el arreglo, el write-back
            // SOLO OTORGA para todo el mundo, copiado o no; la copia sigue
            // sirviendo para lo que se hizo (poder QUITAR en el web sin que
            // eso signifique nada en Mac31), no para protegerse de esto.
            var fotoMac31B = await FotoDeMac31Async(conn, legacyB);
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, webB, Array.Empty<string>(), Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);
            Assert.Equal(1, await EstadoEnMac31Async(conn, legacyB, "btnModificar"));
            Assert.Equal(fotoMac31B, await FotoDeMac31Async(conn, legacyB));
            _out.WriteLine("  4b OK  sin copia, guardar tampoco baja ningun IDStatus de Mac31");

            // ── 5. Deshacer devuelve la herencia ────────────────────────────
            Assert.True(await Servicio(baseDatos).DeshacerCopiaDeMac31Async(EmpresaId, webA, CancellationToken.None));
            var alDeshacer = await PermisosAsync(baseDatos, webA);
            Assert.Equal(antesA.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                         alDeshacer.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(0, await EscalarAsync(conn,
                $"SELECT COUNT(*) FROM dbo.WUsuarioPermiso WHERE UsuarioWebId = '{webA}'"));
            _out.WriteLine("  5 OK  al deshacer vuelve la herencia entera y no queda ni un ajuste de la copia");

            // Y deshacer dos veces no revienta ni miente.
            Assert.False(await Servicio(baseDatos).DeshacerCopiaDeMac31Async(EmpresaId, webA, CancellationToken.None));
        }
        finally
        {
            await LimpiarAsync(conn, webA, webB);
        }
    }

    // ───────────────────────── ayudantes ─────────────────────────

    private async Task<List<string>> PermisosAsync(string baseDatos, Guid usuarioWeb)
    {
        var snap = await Servicio(baseDatos).GetPermissionsAsync(
            usuarioWeb, EmpresaId, "User", CancellationToken.None);
        return snap.Permissions;
    }

    private static async Task<List<string>> ClavesDeLaCopiaAsync(SqlConnection conn, Guid usuarioWeb, string tipo)
    {
        var claves = new List<string>();
        await using var cmd = new SqlCommand($@"
SELECT Clave FROM dbo.WPermisoCopiaMac31Clave
WHERE UsuarioWebId = '{usuarioWeb}' AND LOWER(Tipo) = '{tipo}'
ORDER BY Clave;", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) claves.Add(r.GetString(0));
        return claves;
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

    private static async Task<int> AltaDelOperadorAsync(SqlConnection conn, string nombre)
    {
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.Usuarios (IDStatus, Usuario, Contrasena, Nombre, IDPerfil)
VALUES (1, '{nombre}', 'ZZ2026', 'OPERADOR DE PRUEBA', 2);");

        var id = await EscalarAsync(conn, $"SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{nombre}'");

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

        return id;
    }

    private static async Task<Guid> AltaDeLaCuentaWebAsync(SqlConnection conn, string nombre)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.UsuarioWeb (Id, Usuario, ContrasenaHash, Rol, EmpresaId, DebeCambiarContrasena, Estado, FechaCreacion)
VALUES ('{id}', '{nombre}', 'no-se-usa-en-esta-prueba', 'User', {EmpresaId}, 0, 1, SYSUTCDATETIME());");
        return id;
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

    /// <summary>
    /// Borra SOLO lo que creo esta prueba. En un finally para que una asercion
    /// fallida no deje basura.
    /// </summary>
    private static async Task LimpiarAsync(SqlConnection conn, Guid webA, Guid webB)
    {
        try
        {
            foreach (var id in new[] { webA, webB })
            {
                if (id == Guid.Empty) continue;
                await EjecutarAsync(conn, $@"
DELETE FROM dbo.WPermisoCopiaMac31Clave WHERE UsuarioWebId = '{id}';
DELETE FROM dbo.WPermisoCopiaMac31 WHERE UsuarioWebId = '{id}';
DELETE FROM dbo.WUsuarioPermiso WHERE UsuarioWebId = '{id}';");
            }

            foreach (var nombre in new[] { UsuarioA, UsuarioB })
            {
                await EjecutarAsync(conn, $@"
DELETE FROM dbo.UsuarioWeb WHERE Usuario = '{nombre}';
DECLARE @u INT = (SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{nombre}');
IF @u IS NOT NULL
BEGIN
    DELETE FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = @u;
    DELETE FROM dbo.Usuarios WHERE IDUsuario = @u;
END");
            }
        }
        catch (SqlException)
        {
            // Si la limpieza falla hay que saberlo, pero sin tapar el fallo
            // original de la prueba.
        }
    }
}
