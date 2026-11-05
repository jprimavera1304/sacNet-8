using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  GUARDAR PERMISOS EN EL WEB NO PUEDE APAGAR NADA EN MAC31
  ══════════════════════════════════════════════════════════════════════════

  LO QUE PASO, DOS VECES
  ----------------------
  2026-08-26, Tauro: a JANET le quedaron 91 de sus 135 permisos de Mac31
  despues de que alguien guardara permisos suyos desde la pantalla web. 44
  filas de [Usuario Forma Procesos] pasaron de IDStatus 1 a 2. Se arreglo
  dejando el write-back en solo-otorgar (9cbb34c).

  2026-11-04, Tauro: lo mismo, 46 permisos y tres personas —JESY 19,
  ALVARELYS 16, MARINA BERNAL 11—. Hubo que reponerlos del respaldo de las
  03:00.

  Entre una y otra, la revocacion se habia vuelto a permitir a proposito
  (primero para `reportes.*` el 28-sep-2026 y luego para `ventas.`,
  `empleados.` y `prestamos.`) con un razonamiento que era cierto el dia que
  se escribio: "la pantalla ya ensena el estado real de Mac31, asi que quien
  desmarca lo esta quitando a proposito". El 2026-10-08 la pantalla dejo de
  MANDAR esas familias en el PUT (`sinLosDeMac31` en
  Web/QR_Web/docs2/src/nucleo/permisosWeb.ts, para no escribir filas que el
  siguiente calculo borraba) y nadie se dio cuenta de que eso convertia
  "no me lo mandaron" otra vez en "apagalo".

  QUE FIJA ESTA PRUEBA
  --------------------
  Que no haga falta volver a tener razon sobre lo que manda la pantalla: el
  guardado del web NO BAJA UN IDStatus, pase lo que pase con el payload.

  Son dos niveles, a proposito:

    1. SIN BASE, sobre las dos funciones que deciden
       (EstadoDeseadoEnLegacy y CambiosQueHacenFalta). Estas corren siempre
       y no pueden pasar en vacio: no leen archivos ni base, y si alguien
       devuelve el `loTiene ? 1 : 2` fallan en seco. Comprobado a mano
       reintroduciendo el bug.

    2. CONTRA LA BASE, reproduciendo el 4-nov completo: una cuenta nueva con
       botones de CONSULTA DE VENTAS encendidos en Mac31, un Guardar con el
       payload que manda hoy la pantalla, y la foto de
       [Usuario Forma Procesos] antes y despues. Antes del arreglo esa foto
       salia con todos los botones en 2; ahora tiene que salir identica.
       Si no hay conexion lo DICE y no finge: las aserciones de verdad las
       lleva el nivel 1, que no depende de nada.
*/
[Collection(PruebasContraLaBase.Nombre)]
public class GuardarEnElWebNuncaRevocaEnMac31Tests
{
    private readonly ITestOutputHelper _out;
    public GuardarEnElWebNuncaRevocaEnMac31Tests(ITestOutputHelper salida) => _out = salida;

    // ═════════════════════════════════════════════════════════════════════
    // NIVEL 1: LA DECISION, SIN BASE
    // ═════════════════════════════════════════════════════════════════════

    private static HashSet<string> Efectivo(params string[] claves) =>
        new(claves, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// EL 4 DE NOVIEMBRE, RENGLON POR RENGLON.
    ///
    /// JESY tenia 19 botones de Mac31 encendidos. La pantalla guardo sin
    /// mandar ni una clave de la familia `ventas.` —porque desde el
    /// 2026-10-08 no las manda—, asi que el conjunto efectivo del web no
    /// tenia ninguna. Lo que se le escribio a Mac31 fueron 19 bajas.
    ///
    /// Con el arreglo, de ese guardado no sale NI UNA escritura.
    /// </summary>
    [Fact]
    public void El_Guardado_Que_Apago_19_Permisos_Ahora_No_Escribe_Nada()
    {
        // Los 19 botones que Mac31 le daba, cada uno su clave y su IDProceso.
        var catalogo = new List<(string Clave, int IdProceso)>();
        var enMac31 = new Dictionary<int, int>();
        for (var i = 1; i <= 19; i++)
        {
            catalogo.Add(($"ventas.boton{i}", 100 + i));
            enMac31[100 + i] = 1;   // encendido en [Usuario Forma Procesos]
        }

        // Lo que la pantalla manda hoy: ni una clave de `ventas.`.
        var efectivoDelWeb = Efectivo("usuarios.ver", "app_movil.pedidos");

        var deseado = PermissionService.EstadoDeseadoEnLegacy(catalogo, efectivoDelWeb);
        var cambios = PermissionService.CambiosQueHacenFalta(deseado, enMac31);

        Assert.Empty(cambios);
        Assert.DoesNotContain(deseado, par => par.Value != 1);
    }

    /// <summary>
    /// LA REGLA, DICHA COMO REGLA Y NO COMO CASO: de la decision no sale
    /// JAMAS un IDStatus distinto de 1. Da igual que el permiso este
    /// concedido, no concedido, o que la clave venga vacia.
    /// </summary>
    [Fact]
    public void De_La_Decision_Nunca_Sale_Una_Baja()
    {
        var catalogo = new List<(string Clave, int IdProceso)>
        {
            ("ventas.facturar", 1),
            ("ventas.cancelar", 2),
            ("empleados.editar", 3),
            ("prestamos.autorizar", 4),
            ("reportes.ventas.diario", 5),
            ("", 6)
        };

        var deseado = PermissionService.EstadoDeseadoEnLegacy(
            catalogo, Efectivo("ventas.facturar"));

        Assert.All(deseado, par => Assert.Equal(1, par.Value));
        // Lo que no esta concedido no se MENCIONA, que es lo que hace que no
        // se toque: CambiosQueHacenFalta solo recorre lo que esta aqui.
        Assert.Equal(new[] { 1 }, deseado.Keys.OrderBy(x => x).ToArray());
    }

    /// <summary>
    /// Y SE SIGUE OTORGANDO, que es la mitad que tiene que seguir
    /// funcionando: un permiso que el web concede y en Mac31 estaba de baja
    /// se enciende.
    /// </summary>
    [Fact]
    public void Conceder_Sigue_Encendiendo_En_Mac31()
    {
        var deseado = PermissionService.EstadoDeseadoEnLegacy(
            new[] { ("ventas.facturar", 7) }, Efectivo("ventas.facturar"));

        var cambios = PermissionService.CambiosQueHacenFalta(
            deseado, new Dictionary<int, int> { [7] = 2 });

        Assert.Equal(new[] { new KeyValuePair<int, int>(7, 1) }, cambios);
    }

    /// <summary>
    /// DOS CLAVES EN EL MISMO BOTON: CUALQUIER SI GANA.
    ///
    /// Medido el 2026-10-12 en Produccion_svr: las familias "cobranza." y
    /// "cobranzas." comparten botones, y recorriendo clave por clave la
    /// segunda apagaba lo que la primera acababa de encender. Con una
    /// concedida, el boton queda encendido.
    /// </summary>
    [Fact]
    public void Dos_Claves_Un_Solo_Proceso_Y_Basta_Una_Concedida()
    {
        var deseado = PermissionService.EstadoDeseadoEnLegacy(
            new[] { ("cobranza.cobranzas.consultar", 55), ("cobranzas.consultar", 55) },
            Efectivo("cobranza.cobranzas.consultar"));

        Assert.Equal(1, Assert.Single(deseado).Value);
        Assert.Equal(55, deseado.Keys.Single());
    }

    /// <summary>
    /// EL CANDADO FINAL, PROBADO APARTE. Si algun camino nuevo vuelve a
    /// pedir una baja —saltandose EstadoDeseadoEnLegacy—, el write-back no
    /// la escribe. Es la segunda vuelta de la misma llave, y esta puesta
    /// porque las dos veces que esto costo permisos el calculo estaba bien
    /// y los datos de entrada no.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Un_Camino_Que_Pida_Baja_No_La_Escribe(int status)
    {
        Assert.Empty(PermissionService.CambiosQueHacenFalta(
            deseado: new Dictionary<int, int> { [10] = status },
            estadoActual: new Dictionary<int, int> { [10] = 1 }));
    }

    // ═════════════════════════════════════════════════════════════════════
    // NIVEL 2: EL GUARDADO COMPLETO, CONTRA LA BASE
    // ═════════════════════════════════════════════════════════════════════

    private const string UsuarioPrueba = "ZZNOREVOCA";
    private const string FormaVentas = "CONSULTA DE VENTAS";
    private const int EmpresaId = 1;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoSinRevocar());

    private sealed class EntornoSinRevocar : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ISL_Service.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>
    /// EL 4-NOV COMPLETO: cuenta propia, botones de CONSULTA DE VENTAS
    /// encendidos en Mac31, y un Guardar desde el web como el que los apago.
    /// </summary>
    /// <remarks>
    /// El payload es el que manda la pantalla de verdad: Allow y Deny VACIOS.
    /// No es un caso raro inventado — es lo que llega cuando la persona no
    /// tiene excepciones sobre las claves que el web si reparte, porque la
    /// pantalla solo manda la diferencia contra el rol y de las familias de
    /// Mac31 no manda nada.
    ///
    /// SEGURIDAD: crea su propia cuenta legacy y web, aborta si ya existe, y
    /// la borra en un finally. No toca ninguna cuenta real ni cambia el
    /// estatus de ningun modulo.
    /// </remarks>
    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Guardar_Desde_El_Web_No_Baja_Ni_Un_IDStatus(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: este nivel NO se midio. " +
                           "La regla la fijan igualmente las pruebas sin base de este archivo.");
            return;
        }

        if (await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'") > 0
            || await EscalarAsync(conn, $"SELECT COUNT(*) FROM dbo.UsuarioWeb WHERE Usuario = '{UsuarioPrueba}'") > 0)
        {
            Assert.Fail($"{UsuarioPrueba} ya existe en {baseDatos}. No se escribe encima: borralo a mano y repite.");
        }

        Guid usuarioWeb = Guid.Empty;
        try
        {
            var (legacyId, procesos) = await AltaDelOperadorAsync(conn);
            usuarioWeb = await AltaDeLaCuentaWebAsync(conn);

            /* LA PRUEBA TIENE QUE PODER FALLAR, Y ESTO LO GARANTIZA. Si la
               forma no existiera en esta base, o el modulo 'ventas' estuviera
               apagado, el guardado no habria tocado nada ni antes del arreglo
               y la prueba pasaria sin medir nada. Se exige el escenario. */
            if (procesos.Count == 0)
                Assert.Fail($"{baseDatos}: la forma '{FormaVentas}' no tiene botones activos. " +
                            "Sin ellos este nivel no mide nada, asi que no se declara OK.");

            var moduloApagado = await EscalarAsync(conn, $@"
SELECT COUNT(*) FROM dbo.WModulo
WHERE EmpresaId = {EmpresaId} AND ModuloClave = 'ventas' AND IdStatus = 2");
            if (moduloApagado > 0)
                Assert.Fail($"{baseDatos}: el modulo 'ventas' esta apagado. Con el modulo apagado " +
                            "el write-back viejo tampoco revocaba, asi que esta prueba no probaria nada.");

            var antes = await FotoDeMac31Async(conn, legacyId);
            var encendidosAntes = antes.Count(f => f.Value == 1);
            _out.WriteLine($"[{baseDatos}] cuenta legacy {legacyId} con {encendidosAntes} botones encendidos de '{FormaVentas}'");
            Assert.True(encendidosAntes > 0, "El alta tenia que dejar botones encendidos en Mac31.");

            // EL GUARDADO. Esto es lo que el 4-nov dejo 46 filas en IDStatus 2.
            await Servicio(baseDatos).UpsertUserOverridesAsync(
                EmpresaId, usuarioWeb,
                Array.Empty<string>(), Array.Empty<string>(),
                sincronizarConLegacy: true, CancellationToken.None);

            var despues = await FotoDeMac31Async(conn, legacyId);

            var apagados = antes
                .Where(f => f.Value == 1
                            && (!despues.TryGetValue(f.Key, out var ahora) || ahora != 1))
                .Select(f => f.Key)
                .OrderBy(x => x)
                .ToList();

            Assert.True(apagados.Count == 0,
                $"{baseDatos}: guardar desde el web bajo {apagados.Count} permisos de Mac31 " +
                $"(IDProceso {string.Join(", ", apagados)}). Es el incidente del 2026-11-04 otra vez: " +
                "el guardado del web NO puede apagar una fila de [Usuario Forma Procesos].");

            // Y tampoco al reves: no inventa filas nuevas.
            Assert.Equal(
                antes.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"),
                despues.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"));

            _out.WriteLine($"  OK  los {encendidosAntes} botones siguen en IDStatus=1 despues de guardar");
        }
        finally
        {
            await LimpiarAsync(conn, usuarioWeb);
        }
    }

    // ───────────────────────── ayudantes ─────────────────────────

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

    /// <summary>
    /// La cuenta de Mac31 con los botones de CONSULTA DE VENTAS encendidos,
    /// que es lo que tenian JESY, ALVARELYS y MARINA BERNAL.
    ///
    /// Los IDProceso NO se escriben a mano: no coinciden entre MacZ y
    /// Produccion_svr, asi que se leen de la forma de ESTA base. Y se
    /// encienden con el SP de Mac31, nunca con un INSERT suelto.
    /// </summary>
    private static async Task<(int LegacyId, List<int> Procesos)> AltaDelOperadorAsync(SqlConnection conn)
    {
        await EjecutarAsync(conn, $@"
INSERT INTO dbo.Usuarios (IDStatus, Usuario, Contrasena, Nombre, IDPerfil)
VALUES (1, '{UsuarioPrueba}', 'ZZ2026', 'OPERADOR SIN REVOCAR', 2);");

        var id = await EscalarAsync(conn, $"SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}'");

        /* LA FORMA SE BUSCA POR DESCRIPCION, igual que el puente (ver
           FiltroDeForma en PermissionService): `n_Formas.Forma` de esta es
           'ConsultarVentas' y la descripcion es 'CONSULTA DE VENTAS'. Buscarla
           por nombre no devolvia nada y esta prueba se declaraba no medida. */
        var procesos = new List<int>();
        await using (var cmd = new SqlCommand($@"
SELECT p.IDProceso FROM dbo.n_Procesos p
JOIN dbo.n_Formas f ON f.IDForma = p.IDForma
WHERE UPPER(LTRIM(RTRIM(f.Descripcion))) = '{FormaVentas}' AND ISNULL(p.IDStatus,1) = 1;", conn))
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

    /// <summary>IDProceso -> IDStatus de ese usuario en Mac31.</summary>
    private static async Task<Dictionary<int, int>> FotoDeMac31Async(SqlConnection conn, int legacyId)
    {
        var foto = new Dictionary<int, int>();
        await using var cmd = new SqlCommand(
            $"SELECT IDProceso, IDStatus FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = {legacyId};", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            if (r.IsDBNull(0)) continue;
            foto[r.GetInt32(0)] = r.IsDBNull(1) ? 2 : r.GetInt32(1);
        }
        return foto;
    }

    /// <summary>
    /// Borra SOLO lo que creo esta prueba, en un finally, para que una
    /// asercion fallida no deje basura en la base.
    /// </summary>
    private static async Task LimpiarAsync(SqlConnection conn, Guid usuarioWeb)
    {
        if (usuarioWeb != Guid.Empty)
        {
            await EjecutarAsync(conn, $"DELETE FROM dbo.WUsuarioPermiso WHERE UsuarioWebId = '{usuarioWeb}';");
            await EjecutarAsync(conn, $"DELETE FROM dbo.UsuarioWeb WHERE Id = '{usuarioWeb}';");
        }

        await EjecutarAsync(conn, $@"
DECLARE @id INT = (SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = '{UsuarioPrueba}');
IF @id IS NOT NULL
BEGIN
    DELETE FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = @id;
    DELETE FROM dbo.Usuarios WHERE IDUsuario = @id;
END");
    }
}
