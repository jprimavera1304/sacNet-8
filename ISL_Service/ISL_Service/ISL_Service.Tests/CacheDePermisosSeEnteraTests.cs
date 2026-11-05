using System.Reflection;
using ISL_Service.Controllers;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// QUE CONCEDER UN PERMISO POR BASE SURTA EFECTO SIN REINICIAR EL POOL.
/// </summary>
/// <remarks>
/// EL INCIDENTE QUE MIDE, TAL CUAL PASO
/// ------------------------------------
/// El 2026-11-04 se concedieron permisos con `INSERT` directo en
/// `WUsuarioPermiso` —la pantalla de permisos no podia hacerlo—, el backend
/// siguio sirviendo su copia en memoria, y hubo que reiniciar `pool-tauro`
/// CUATRO VECES en una tarde.
///
/// Esta prueba reproduce exactamente eso contra la base local y comprueba las
/// dos salidas nuevas:
///
///   1. que el INSERT por si solo NO se ve (si esto dejara de ser verdad, es
///      que la cache dejo de cachear y hay que enterarse: el agujero de
///      rendimiento que arreglo seria peor que el desfase);
///   2. que `InvalidarCacheDeUsuario` —lo que llama el endpoint nuevo— lo hace
///      aparecer EN EL ACTO;
///   3. que el LATIDO lo hace aparecer SOLO, sin que nadie pulse nada, que es
///      la mitad que de verdad quita la dependencia del camino de escritura.
///
/// NO SE ESPERAN LOS 15 SEGUNDOS DE `LatidoTtl`. Lo que se prueba es la
/// COMPARACION de huellas, no el reloj: se olvida la foto anterior por reflexion
/// —lo mismo que hace el paso del tiempo— y se mira si la siguiente lectura se
/// entera. Una prueba que duerma quince segundos la acaba borrando alguien.
///
/// POR QUE EL ENTORNO ES "Production" Y NO "Development"
/// -----------------------------------------------------
/// Porque en Development `GetPermissionsAsync` NO cachea a proposito (devuelve
/// dato fresco siempre), asi que aqui no habria nada que medir. El fallo solo
/// existe donde hay cache, o sea en produccion.
///
/// SE SALTA SOLA si no hay SQL Server, y DEJA LA BASE COMO ESTABA: la fila que
/// inserta la borra en un `finally`.
/// </remarks>
[Collection(PruebasContraLaBase.Nombre)]
public class CacheDePermisosSeEnteraTests
{
    private readonly ITestOutputHelper _out;
    public CacheDePermisosSeEnteraTests(ITestOutputHelper salida) => _out = salida;

    private const int EmpresaId = 1;

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDeProduccion());

    /// <summary>Un entorno que NO es Development, para que la cache funcione.</summary>
    private sealed class EntornoDeProduccion : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ISL_Service.Tests";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Theory]
    [InlineData("MacZ")]            // Zaragoza
    [InlineData("Produccion_svr")]  // Tauro
    public async Task Un_Insert_Por_Base_Se_Ve_Sin_Reiniciar_Nada(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException) { _out.WriteLine($"Sin base {baseDatos}: NO se midio nada."); return; }

        /*
          SE BUSCA UN CASO REAL, no se inventa: una persona con cuenta web y una
          clave del catalogo que HOY NO TENGA. Clavar un usuario o una clave a
          mano deja la prueba muerta el dia que esa fila cambie, y ya hay
          precedente en esta casa de pruebas que se saltaban su propio Assert.
        */
        var caso = await BuscarCasoAsync(conn);
        if (caso is null)
        {
            _out.WriteLine($"{baseDatos}: no hay usuario web con rol, o no hay clave libre. NO se midio nada.");
            return;
        }

        var (userId, rol, permisoId, clave) = caso.Value;
        _out.WriteLine($"{baseDatos}: usuario {userId} (rol {rol}), clave de prueba '{clave}'.");

        var servicio = Servicio(baseDatos);
        OlvidarElLatido();

        try
        {
            var antes = await servicio.GetPermissionsAsync(userId, EmpresaId, rol, CancellationToken.None);
            if (!antes.PermissionsEnabled)
            {
                _out.WriteLine($"{baseDatos}: el modelo de capacidades esta apagado. NO se midio nada.");
                return;
            }
            Assert.DoesNotContain(clave, antes.Permissions, StringComparer.OrdinalIgnoreCase);

            await ConcederPorBaseAsync(conn, userId, permisoId);

            /* 1. El INSERT por si solo no se ve: la cache esta haciendo su trabajo. */
            var recien = await servicio.GetPermissionsAsync(userId, EmpresaId, rol, CancellationToken.None);
            Assert.DoesNotContain(clave, recien.Permissions, StringComparer.OrdinalIgnoreCase);

            /* 2. El boton explicito lo hace aparecer en el acto. */
            servicio.InvalidarCacheDeUsuario(EmpresaId, userId);
            var tirada = await servicio.GetPermissionsAsync(userId, EmpresaId, rol, CancellationToken.None);
            Assert.Contains(clave, tirada.Permissions, StringComparer.OrdinalIgnoreCase);

            /* 3. Y el latido lo hace aparecer SOLO. Se vuelve a dejar la cache
                  con el estado viejo —quitando la fila, leyendo, y poniendola—
                  para que el latido tenga algo que detectar. */
            await RevocarPorBaseAsync(conn, userId, permisoId);
            servicio.InvalidarCacheDeUsuario(EmpresaId, userId);
            var sinLaClave = await servicio.GetPermissionsAsync(userId, EmpresaId, rol, CancellationToken.None);
            Assert.DoesNotContain(clave, sinLaClave.Permissions, StringComparer.OrdinalIgnoreCase);

            /*
              La lectura de arriba dejo tomada la foto de "sin la clave". Ahora
              se concede y se CADUCA esa foto, que es lo unico que hace el paso
              del tiempo. No se BORRA: borrarla seria volver al caso de la
              primera medicion, que no invalida nada a proposito porque no tiene
              con que comparar — y entonces esta prueba pasaria sin medir el
              latido, que es justo el fallo del punto 6 de los pendientes.
            */
            await ConcederPorBaseAsync(conn, userId, permisoId);
            CaducarElLatido();

            var porElLatido = await servicio.GetPermissionsAsync(userId, EmpresaId, rol, CancellationToken.None);
            Assert.Contains(clave, porElLatido.Permissions, StringComparer.OrdinalIgnoreCase);

            _out.WriteLine($"{baseDatos}: OK. El INSERT se ve por el boton y tambien solo, sin reiniciar nada.");
        }
        finally
        {
            try { await RevocarPorBaseAsync(conn, userId, permisoId); }
            catch (SqlException ex) { _out.WriteLine("No se pudo limpiar la fila de prueba: " + ex.Message); }
            OlvidarElLatido();
        }
    }

    /// <summary>
    /// QUE EL ENDPOINT NUEVO ESTE PROTEGIDO. No hace falta base.
    /// </summary>
    /// <remarks>
    /// Un endpoint de mantenimiento sin candado es peor que no tenerlo: tirar la
    /// cache de la empresa entera a la hora de mas trabajo lo puede pedir
    /// cualquiera con un token. Se exige la MISMA llave que ya protege repartir
    /// ajustes por usuario.
    /// </remarks>
    [Fact]
    public void El_Endpoint_De_Tirar_Cache_Pide_La_Llave_De_Repartir_Permisos()
    {
        var tipo = typeof(PermisosCacheController);

        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());

        var metodos = new[] { nameof(PermisosCacheController.TirarDeUsuario), nameof(PermisosCacheController.TirarDeLaEmpresa) };
        foreach (var nombre in metodos)
        {
            var metodo = tipo.GetMethod(nombre)
                ?? throw new InvalidOperationException($"No existe {nombre}. Si se renombro, aqui hay que seguirlo.");

            var politicas = metodo.GetCustomAttributes<AuthorizeAttribute>()
                .Select(x => x.Policy ?? string.Empty)
                .ToList();

            Assert.Contains("perm:permisosweb.overrides.editar|app_movil.permisos_usuarios.editar", politicas);
        }
    }

    /// <summary>
    /// QUE LA CAPTURA DE PEDIDOS ACEPTE LA CLAVE PROPIA DEL MOVIL.
    /// </summary>
    /// <remarks>
    /// `app_movil.pedidos` abre el boton "+ Nuevo pedido" Y el portal de
    /// repartidor completo, y `ventas.pedidos.crear` no se puede conceder por
    /// base porque el puente legacy la borra del snapshot. Sin esta tercera
    /// clave no hay forma de dar el boton solo.
    ///
    /// Y SE COMPRUEBA QUE LAS OTRAS DOS SIGUEN. Es un OR que se SUMA: el dia que
    /// alguien las quite de aqui creyendo que sobran, a quien hoy captura se le
    /// cae el boton.
    /// </remarks>
    [Fact]
    public void La_Captura_De_Pedidos_Acepta_La_Clave_Propia_Del_Movil()
    {
        var campo = typeof(VentasPedidoCapturaController).GetField(
                        "PoliticaCaptura",
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("No se encontro PoliticaCaptura.");

        var politica = (string)(campo.GetRawConstantValue() ?? string.Empty);

        Assert.Contains("app_movil.ventas.pedidos.crear", politica);
        Assert.Contains("ventas.pedidos.crear", politica);
        Assert.Contains("app_movil.pedidos", politica);
    }

    /* ------------------------------------------------------------------ */

    private static object Latidos()
    {
        var campo = typeof(PermissionService).GetField("_latidos", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("No se encontro _latidos. Si se renombro, aqui hay que seguirlo.");
        return campo.GetValue(null)!;
    }

    /// <summary>Borra las huellas: deja al latido como recien arrancado.</summary>
    private static void OlvidarElLatido()
    {
        var diccionario = Latidos();
        diccionario.GetType().GetMethod("Clear")!.Invoke(diccionario, null);
    }

    /// <summary>
    /// CONSERVA la huella medida pero la deja vencida. Es exactamente lo que
    /// hace pasar `LatidoTtl`, sin dormir quince segundos en una prueba.
    /// </summary>
    private static void CaducarElLatido()
    {
        var diccionario = Latidos();
        var tipo = diccionario.GetType();
        var tipoLatido = tipo.GetGenericArguments()[1];
        var ctor = tipoLatido.GetConstructors().Single();
        var indexador = tipo.GetProperty("Item")!;

        var claves = ((System.Collections.IEnumerable)tipo.GetProperty("Keys")!.GetValue(diccionario)!)
            .Cast<object>().ToList();

        foreach (var clave in claves)
        {
            var actual = indexador.GetValue(diccionario, new[] { clave });
            if (actual is null) continue;

            var argumentos = ctor.GetParameters()
                .Select(p => p.Name == "Hasta"
                    ? (object)DateTime.MinValue
                    : tipoLatido.GetProperty(p.Name!)!.GetValue(actual)!)
                .ToArray();

            indexador.SetValue(diccionario, ctor.Invoke(argumentos), new[] { clave });
        }
    }

    /// <summary>
    /// Un usuario web con rol y una clave del catalogo que no tenga por ningun
    /// lado (ni por ajuste propio ni por su rol). Null si no hay caso.
    /// </summary>
    private static async Task<(Guid UserId, string Rol, long PermisoId, string Clave)?> BuscarCasoAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(@"
SELECT TOP 1 u.Id, ISNULL(u.Rol, N'User'), p.Id, p.Clave
FROM dbo.UsuarioWeb u
CROSS APPLY (
    SELECT TOP 1 p2.Id, p2.Clave
    FROM dbo.WPermiso p2
    WHERE p2.EmpresaId = @EmpresaId
      AND p2.Clave LIKE N'app_movil.%'
      AND NOT EXISTS (SELECT 1 FROM dbo.WUsuarioPermiso up
                       WHERE up.EmpresaId = p2.EmpresaId AND up.WPermisoId = p2.Id)
      AND NOT EXISTS (SELECT 1 FROM dbo.WRolPermiso rp
                       WHERE rp.EmpresaId = p2.EmpresaId AND rp.WPermisoId = p2.Id)
    ORDER BY p2.Clave
) p
WHERE u.EmpresaId = @EmpresaId
  /* El SuperAdmin se salta toda comprobacion: con el no se puede medir que una
     clave NO este. Es el mismo motivo por el que varios fallos de permisos de
     esta casa fueron invisibles para quien los probo con su cuenta. */
  AND ISNULL(u.Rol, N'User') <> N'SuperAdmin'
ORDER BY u.Id;", conn);
        cmd.Parameters.AddWithValue("@EmpresaId", EmpresaId);

        await using var lector = await cmd.ExecuteReaderAsync();
        if (!await lector.ReadAsync()) return null;

        return (lector.GetGuid(0), lector.GetString(1), Convert.ToInt64(lector.GetValue(2)), lector.GetString(3));
    }

    private const string MotivoDePrueba = "prueba.cachedepermisos";

    private static async Task ConcederPorBaseAsync(SqlConnection conn, Guid userId, long permisoId)
    {
        /* Exactamente lo que se hizo a mano en produccion: un INSERT, sin pasar
           por UpsertUserOverridesAsync ni por ningun endpoint. */
        await using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.WUsuarioPermiso
                WHERE EmpresaId = @EmpresaId AND UsuarioWebId = @UserId AND WPermisoId = @PermisoId)
    INSERT INTO dbo.WUsuarioPermiso (EmpresaId, UsuarioWebId, WPermisoId, Tipo, Motivo)
    VALUES (@EmpresaId, @UserId, @PermisoId, 'A', @Motivo);", conn);
        cmd.Parameters.AddWithValue("@EmpresaId", EmpresaId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@PermisoId", permisoId);
        cmd.Parameters.AddWithValue("@Motivo", MotivoDePrueba);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RevocarPorBaseAsync(SqlConnection conn, Guid userId, long permisoId)
    {
        /* Solo la fila de la prueba, reconocible por su Motivo: si alguien
           repartio esa clave de verdad mientras esto corria, no se la lleva. */
        await using var cmd = new SqlCommand(@"
DELETE FROM dbo.WUsuarioPermiso
WHERE EmpresaId = @EmpresaId AND UsuarioWebId = @UserId
  AND WPermisoId = @PermisoId AND Motivo = @Motivo;", conn);
        cmd.Parameters.AddWithValue("@EmpresaId", EmpresaId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@PermisoId", permisoId);
        cmd.Parameters.AddWithValue("@Motivo", MotivoDePrueba);
        await cmd.ExecuteNonQueryAsync();
    }
}
