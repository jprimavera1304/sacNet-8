using System.Collections;
using System.Reflection;
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
/// LA MATRIZ DE DIFERENCIAS ENTRE MAC31 Y EL WEB, MEDIDA USUARIO POR USUARIO.
///
/// POR QUE HACE FALTA Y POR QUE NO LA CUBRE NINGUNA DE LAS QUE YA ESTAN
/// -------------------------------------------------------------------
/// <see cref="DiagnosticoFamiliasContraLaBase"/> mira el puente DESDE EL WEB:
/// coge las familias que el codigo declara y comprueba que cada clave que
/// promete la produzca algun boton de Mac31. Eso caza la clave que se pierde,
/// pero NO caza lo contrario, que es lo que impide migrar: un boton que Mac31
/// si concede y que el web no sabe leer. Esa pregunta solo se contesta
/// empezando por los datos —que tiene autorizado cada quien en Mac31— y
/// terminando en el snapshot de verdad.
///
/// Esto no afirma nada: MIDE y escribe. El resultado queda en
/// artifacts\auditoria-herencia-&lt;base&gt;.txt para poder compararlo despues de
/// cada correccion y ver si la diferencia bajo.
///
/// NO TOCA NI UNA FILA. Solo lee, y lee el snapshot por el camino de
/// produccion (PermissionService.GetPermissionsAsync), que es la unica forma de
/// incluir los filtros de modulo apagado y el orden en que las familias se
/// pisan entre ellas.
/// </summary>
[Collection(PruebasContraLaBase.Nombre)]
public class AuditoriaHerenciaLegacyVsWeb
{
    private readonly ITestOutputHelper _out;
    public AuditoriaHerenciaLegacyVsWeb(ITestOutputHelper salida) => _out = salida;

    private static readonly Type Tipo = typeof(PermissionService);
    private const int EmpresaId = 1;

    private static object LeerCampo(string nombre) =>
        Tipo.GetField(nombre, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static object? Prop(object o, string n) => o.GetType().GetProperty(n)?.GetValue(o);

    private static string CadenaDe(string baseDatos) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    private static PermissionService Servicio(string baseDatos) => new(
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaDe(baseDatos)).Options),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<PermissionService>.Instance,
        new EntornoDeAuditoria());

    private sealed class EntornoDeAuditoria : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ISL_Service.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// Un boton de Mac31 y la clave del web que produce, si produce alguna.
    private sealed record Puente(string Forma, string Proceso, int IDProceso, string? Clave, string Origen);

    [Theory]
    [InlineData("MacZ")]
    [InlineData("Produccion_svr")]
    public async Task Matriz_De_Diferencias(string baseDatos)
    {
        await using var conn = new SqlConnection(CadenaDe(baseDatos));
        try { await conn.OpenAsync(); }
        catch (SqlException)
        {
            _out.WriteLine($"No hay conexion a {baseDatos}: la auditoria NO se hizo.");
            return;
        }

        var verbos = (IDictionary)LeerCampo("VerbosLegacy");
        var renglones = new List<string>();
        void Di(string s) { renglones.Add(s); _out.WriteLine(s); }

        Di($"==== AUDITORIA DE HERENCIA - {baseDatos} - {DateTime.Now:yyyy-MM-dd HH:mm} ====");

        // 1. EL PUENTE COMPLETO: que boton de Mac31 produce que clave.
        var puentes = new List<Puente>();
        var formasConPuente = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task LeerBinding(object binding, string origen)
        {
            var formaExacta = (string?)Prop(binding, "FormaExacta");
            var legacyForm = (string)Prop(binding, "LegacyForm")!;
            var moduleKey = (string)Prop(binding, "ModuleKey")!;
            var prefijoForma = (string?)Prop(binding, "Prefijo") ?? moduleKey;
            var mapa = (IDictionary)Prop(binding, "Map")!;

            var filtro = formaExacta is null
                ? "UPPER(LTRIM(RTRIM(f.Descripcion))) = @Forma"
                : "f.Forma = @Forma";

            await using var cmd = new SqlCommand(
                "SELECT LTRIM(RTRIM(f.Forma)) AS Forma, p.Proceso, p.IDProceso "
                + "FROM dbo.n_Formas f INNER JOIN dbo.n_Procesos p ON p.IDForma = f.IDForma "
                + "WHERE " + filtro + " AND ISNULL(p.IDStatus, 1) = 1;", conn);
            cmd.Parameters.AddWithValue("@Forma", formaExacta ?? legacyForm);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var forma = rd.GetString(0).Trim();
                var proceso = (rd.IsDBNull(1) ? string.Empty : rd.GetString(1)).Trim();
                var idProceso = rd.GetInt32(2);
                formasConPuente.Add(forma);

                string? clave = null;
                if (mapa.Contains(proceso))
                {
                    var v = mapa[proceso]!;
                    clave = (string)v.GetType().GetField("Item1")!.GetValue(v)!;
                }
                else if (verbos.Contains(proceso))
                {
                    clave = $"{prefijoForma}.{(string)verbos[proceso]!}";
                }
                puentes.Add(new Puente(forma, proceso, idProceso, clave, origen));
            }
        }

        // Las familias viejas, escritas a mano en PermissionService.cs.
        foreach (var nombre in new[] { "VentasBinding", "VentasPagosBinding", "VentasUsadosBinding", "EmpleadosBinding", "PrestamosBinding" })
            await LeerBinding(LeerCampo(nombre), nombre);

        // Y las ampliadas, que son datos.
        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Prop(familia!, "Prefijo")!;
            foreach (var forma in (IEnumerable)Prop(familia!, "Formas")!)
                await LeerBinding(forma!, $"familia {prefijo}");
        }

        // Reportes: la clave NO se mapea, se deriva del texto del proceso. Aqui
        // solo hace falta saber que esta cubierta, no la clave exacta.
        formasConPuente.Add("ReportesMenu");

        var claveDeProceso = puentes
            .Where(p => p.Clave is not null)
            .GroupBy(p => p.IDProceso)
            .ToDictionary(g => g.Key, g => g.First().Clave!);

        Di($"Botones alcanzados por el puente: {puentes.Count}; con clave web: {claveDeProceso.Count}");
        Di($"Formas de Mac31 cubiertas por algun puente: {formasConPuente.Count}");

        // 2. QUE TIENE CONCEDIDO LA GENTE EN MAC31, Y SI EL WEB LO LEE.
        var concedidos = new List<(int IDUsuario, string Usuario, string Forma, string Proceso, int IDProceso)>();
        await using (var cmd = new SqlCommand(@"
SELECT ufp.IDUsuario, LTRIM(RTRIM(u.Usuario)) AS Usuario,
       LTRIM(RTRIM(f.Forma)) AS Forma, LTRIM(RTRIM(p.Proceso)) AS Proceso, p.IDProceso
FROM dbo.[Usuario Forma Procesos] ufp
INNER JOIN dbo.Usuarios u ON u.IDUsuario = ufp.IDUsuario
INNER JOIN dbo.n_Procesos p ON p.IDProceso = ufp.IDProceso
INNER JOIN dbo.n_Formas f ON f.IDForma = p.IDForma
WHERE ufp.IDStatus = 1 AND ISNULL(p.IDStatus, 1) = 1 AND ISNULL(u.IDStatus, 1) = 1;", conn))
        await using (var rd = await cmd.ExecuteReaderAsync())
            while (await rd.ReadAsync())
                concedidos.Add((rd.GetInt32(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetInt32(4)));

        Di($"Concesiones vivas en Mac31: {concedidos.Count} sobre {concedidos.Select(c => c.IDUsuario).Distinct().Count()} usuarios");

        // Formas que la gente tiene concedidas y que NINGUN puente mira.
        var sinPuente = concedidos
            .Where(c => !formasConPuente.Contains(c.Forma))
            .GroupBy(c => c.Forma)
            .Select(g => (Forma: g.Key, Usuarios: g.Select(x => x.IDUsuario).Distinct().Count(), Botones: g.Select(x => x.Proceso).Distinct().Count()))
            .OrderByDescending(x => x.Usuarios)
            .ToList();

        Di("");
        Di($"--- FORMAS DE MAC31 CON PERMISOS CONCEDIDOS Y SIN NINGUN PUENTE AL WEB: {sinPuente.Count} ---");
        foreach (var f in sinPuente)
            Di($"  {f.Forma,-45} usuarios={f.Usuarios,-4} botones={f.Botones}");

        // Botones dentro de una forma SI puenteada, pero que no producen clave.
        var sinClave = concedidos
            .Where(c => formasConPuente.Contains(c.Forma) && !claveDeProceso.ContainsKey(c.IDProceso) && c.Forma != "ReportesMenu")
            .GroupBy(c => (c.Forma, c.Proceso))
            .Select(g => (g.Key.Forma, g.Key.Proceso, Usuarios: g.Select(x => x.IDUsuario).Distinct().Count()))
            .OrderByDescending(x => x.Usuarios)
            .ToList();

        Di("");
        Di($"--- BOTONES EN FORMAS YA PUENTEADAS QUE NO PRODUCEN NINGUNA CLAVE: {sinClave.Count} ---");
        foreach (var b in sinClave)
            Di($"  {b.Forma,-35} {b.Proceso,-35} usuarios={b.Usuarios}");

        // 3. USUARIO POR USUARIO: lo que Mac31 concede contra el snapshot.
        var webUsers = new List<(Guid Id, string Usuario, string Rol)>();
        await using (var cmd = new SqlCommand(
            "SELECT Id, LTRIM(RTRIM(Usuario)), ISNULL(Rol,'User') FROM dbo.UsuarioWeb WHERE EmpresaId = @e AND Estado = 1;", conn))
        {
            cmd.Parameters.AddWithValue("@e", EmpresaId);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                webUsers.Add((rd.GetGuid(0), rd.GetString(1), rd.GetString(2)));
        }

        Di("");
        Di($"--- USUARIOS CON CUENTA WEB: {webUsers.Count} ---");

        var faltantesGlobales = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var puertasGlobales = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Un snapshot por usuario, leido por el camino de produccion y
        // guardado: las partes 3, 4 y 5 preguntan lo mismo y leerlo tres veces
        // multiplicaba por tres el tiempo de la auditoria sin cambiar nada.
        var snapshots = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in webUsers)
        {
            var s = await Servicio(baseDatos).GetPermissionsAsync(u.Id, EmpresaId, u.Rol, default);
            snapshots[u.Usuario] = s.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var u in webUsers.OrderBy(x => x.Usuario, StringComparer.OrdinalIgnoreCase))
        {
            var legacy = concedidos.Where(c => string.Equals(c.Usuario, u.Usuario, StringComparison.OrdinalIgnoreCase)).ToList();
            var esperadas = legacy
                .Where(c => claveDeProceso.ContainsKey(c.IDProceso))
                .Select(c => claveDeProceso[c.IDProceso])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tiene = snapshots[u.Usuario];

            var faltan = esperadas.Where(k => !tiene.Contains(k)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var k in faltan)
                faltantesGlobales[k] = faltantesGlobales.GetValueOrDefault(k) + 1;

            /*
              LAS PUERTAS, MEDIDAS CON LA TABLA DEL BACKEND Y NO A OJO.

              La primera version de esto preguntaba por "<prefijo>.ver_modulo" y
              acusaba en falso a media plantilla: la entrada del web es POR
              PANTALLA, no por prefijo, y varias familias abren una puerta que se
              llama de otra forma -la familia `cobranza.` entra por
              "cobros.ver_modulo" y `facturacion.` por "facturas.ver_modulo"-.
              Asi que se pregunta a EntradasDeModulo, que es la tabla que de
              verdad decide el 403, y no a una regla inventada aqui.

              Lo que esto mide es el bloqueo que importa para migrar: el usuario
              HEREDA el permiso fino de Mac31 y el backend se lo rechaza porque
              le falta la llave de la puerta.
            */
            var sinPuerta = esperadas
                .Where(k => tiene.Contains(k))
                .Where(k => !EntradasDeModulo.PuedeEntrar(k, p => tiene.Contains(p)))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var puertasQueFaltan = sinPuerta
                .SelectMany(k => EntradasDeModulo.Requeridas(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var m in puertasQueFaltan)
                puertasGlobales[m] = puertasGlobales.GetValueOrDefault(m) + 1;

            Di($"  {u.Usuario,-20} rol={u.Rol,-12} legacy={legacy.Count,-4} esperadas={esperadas.Count,-4} snapshot={tiene.Count,-4} FALTAN={faltan.Count} SINPUERTA={sinPuerta.Count}");
            if (faltan.Count > 0)
                Di($"      Mac31 concede y el snapshot no trae: {string.Join(", ", faltan)}");
            if (sinPuerta.Count > 0)
                Di($"      HEREDADOS QUE EL BACKEND RECHAZA por falta de puerta ({string.Join("/", puertasQueFaltan)}): {string.Join(", ", sinPuerta)}");
        }

        Di("");
        Di($"--- CLAVES QUE MAS SE PIERDEN: {faltantesGlobales.Count} distintas ---");
        foreach (var kv in faltantesGlobales.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
            Di($"  {kv.Key,-45} usuarios afectados={kv.Value}");

        Di("");
        Di($"--- PUERTAS QUE MAS FALTAN: {puertasGlobales.Count} modulos ---");
        foreach (var kv in puertasGlobales.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
            Di($"  {kv.Key,-45} usuarios afectados={kv.Value}");

        // 4. LOS BOTONES SIN MAPEAR: a quien le quitan algo DE VERDAD.
        //
        // Un boton de Mac31 sin clave web no siempre es un permiso perdido. Casi
        // todas las acciones de la lista YA existen en el web, pero protegidas
        // por una clave que OTRO boton de la misma pantalla ya produce: quien
        // tenga los dos no pierde nada y mapear el segundo no cambiaria nada.
        //
        // Lo que si es un permiso perdido es quien tiene SOLO el boton sin
        // mapear. Esa es la cuenta que decide si vale la pena mapearlo, y es la
        // unica forma de no "arreglar" 40 renglones que no le hacen falta a
        // nadie.
        var candidatos = new (string Forma, string Boton, string Clave)[]
        {
            ("ConsultarVentasFacturas", "btnImprimirFactura",            "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnDescargarFactura",           "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnImprimirNotaCredito",        "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnDescargarNotaCredito",       "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnImprimirComplementoPago",    "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnDescargarComplementoPago",   "facturacion.ver"),
            ("ConsultarVentasFacturas", "btnVerNota",                    "ventas.pantalla"),
            ("ConsultarVentasFacturas", "btnNuevaNotaCredito",           "facturacion.timbrar"),
            ("ConsultarVentasFacturas", "btnNuevoComplementoPago",       "facturacion.timbrar"),
            ("ConsultarVentasFacturas", "btnEnviarCorreoNotaCredito",    "facturacion.enviar"),
            ("ConsultarVentasFacturas", "btnEnviarCorreoComplementoPago","facturacion.enviar"),
            ("ConsultarVentasFacturas", "btnCancelarNotaCredito",        "facturacion.cancelar"),
            ("ConsultarVentasFacturas", "btnCancelarComplementoPago",    "facturacion.cancelar"),
            ("VentasFacturacion",       "btnImprimirFactura",            "facturacion.ver"),
            ("VentasFacturacion",       "btnDescargarFactura",           "facturacion.ver"),
            ("VentasFacturacion",       "btnEnviarCorreo",               "facturacion.enviar"),
            ("VentasFacturacion",       "btnCancelarFactura",            "facturacion.cancelar"),
            ("VentasFacturacion",       "btnVerNota",                    "ventas.pantalla"),
            ("ConsultarCobros",         "btnPagosCascos",                "ventas.pagos.ver"),
            ("ConsultarCobros",         "btnMultiPago",                  "ventas.pagos.multi"),
            ("ConsultarCobros",         "btnDevolucion",                 "ventas.devolucion"),
            ("CobrosDetalle",           "btnPagosUsados",                "ventas.pagos.ver"),
            ("CobrosDetalle",           "btnMultiPago",                  "ventas.pagos.multi"),
            ("CobrosDetalle",           "btnDevolucion",                 "ventas.devolucion"),
            ("CobrosDetalle",           "btnCancelarVenta",              "ventas.cancelar"),
            ("CobrosDetalle",           "btnNuevoUsadosExcedentes",      "usados.excedentes.editar"),
            ("CobrosDetalle",           "btnCancelarUsadosExcedentes",   "usados.excedentes.cancelar"),
            ("CobrosDetalle",           "btnNuevoPagoKilos",             "cobros.pagos.kilos"),
            ("CobrosDetalle",           "btnCancelKilos",                "cobros.pagos.kilos"),
            ("ConsultarVentasPagosUsados", "btnVerPago",                 "ventas.pagos.ver"),
            ("ConsultarVentasPagosUsados", "btnAgregarUsadoCredito",     "ventas.usados.guardar"),
            ("ConsultarVentasPagosUsados", "btnCancelarPagoDiffUsados",  "ventas.pagos.cancelar"),
            ("VentasReasignacion",      "btnReasignarNotas",             "cobros.reasignar"),
            ("VentasReasignacion",      "btnReasignarCliente",           "cobros.reasignar"),
            ("ConsultarGarantias",      "btnGarantia",                   "garantias.editar"),
            ("ConsultarGarantias",      "btnAjuste",                     "garantias.editar"),
            ("ConsultarGarantias",      "btnDiagnosticar",               "garantias.editar"),
            ("ConsultarGarantias",      "btnRegresarCliente",            "garantias.editar"),
            ("ConsultarGarantias",      "btnEnviarProveedor",            "garantias.editar"),
            ("ConsultarGarantias",      "btnRemisionar",                 "garantias.editar"),
            ("ConsultarCambiosVigencia","btnEntregar",                   "cambiosvigencia.editar")
        };

        var conCuentaWeb = webUsers
            .Select(u => u.Usuario)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Di("");
        Di("--- BOTONES SIN MAPEAR: USUARIOS QUE SE QUEDAN SIN LA ACCION ---");
        Di("    (quien tiene el boton y NINGUN otro que produzca su clave)");
        var hayPerdida = false;
        foreach (var c in candidatos)
        {
            var conElBoton = concedidos
                .Where(x => string.Equals(x.Forma, c.Forma, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(x.Proceso, c.Boton, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Usuario)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (conElBoton.Count == 0)
                continue;

            var yaConLaClave = concedidos
                .Where(x => claveDeProceso.TryGetValue(x.IDProceso, out var k)
                         && string.Equals(k, c.Clave, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Usuario)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var pierden = conElBoton.Where(x => !yaConLaClave.Contains(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var pierdenConWeb = pierden.Where(conCuentaWeb.Contains).ToList();
            if (pierden.Count == 0)
                continue;

            /*
              Y LO QUE DE VERDAD IMPORTA: SI EL PUENTE LLEGA AL SNAPSHOT.

              Las cuentas de arriba miden el HUECO en los datos de Mac31, y ese
              hueco sigue existiendo haga lo que haga el codigo: son los
              usuarios que tienen un boton y no el canonico. Lo que cambia con
              las ConcesionesExtra es si la clave acaba o no en su snapshot, y
              es lo unico que se puede declarar arreglado.
            */
            var rescatados = pierdenConWeb
                .Where(x => snapshots.TryGetValue(x, out var snapU) && snapU.Contains(c.Clave))
                .ToList();
            var siguenSinElla = pierdenConWeb.Where(x => !rescatados.Contains(x)).ToList();

            hayPerdida = true;
            Di($"  {c.Forma}.{c.Boton} -> {c.Clave}");
            Di($"      lo tienen={conElBoton.Count}  sin el boton canonico={pierden.Count}  de esos con cuenta web={pierdenConWeb.Count}");
            if (pierdenConWeb.Count > 0)
            {
                Di($"      YA LA HEREDAN por la concesion extra: {(rescatados.Count == 0 ? "ninguno" : string.Join(", ", rescatados))}");
                if (siguenSinElla.Count > 0)
                    Di($"      SIGUEN SIN ELLA (esto si es un hueco abierto): {string.Join(", ", siguenSinElla)}");
            }
        }
        if (!hayPerdida)
            Di("  Ninguno: en esta base, todos los que tienen el boton tienen ya su clave por otro boton.");

        // 5. LA PUERTA DEL FRONT: tarjetas escondidas de modulos que SI se pueden usar.
        //
        // Las partes 3 y 4 miden el backend. Pero hay una segunda puerta, la del
        // Inicio: el entradaDeModulo.ts del front decide si se DIBUJA la
        // tarjeta, y pide una clave que no siempre es la que abre el modulo. El
        // caso de ventas lo demostro: el backend ya abria los endpoints y la
        // tarjeta seguia escondida porque pedia "ventas.ver", una semilla del
        // web que Mac31 no produce.
        //
        // Se LEE el archivo del front en vez de copiar la tabla aqui, para que
        // esto no empiece a medir una copia que se quedo vieja. Si no se puede
        // leer, se dice y no se mide.
        var rutaFront = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..",
            "Web", "QR_Web", "docs2", "src", "nucleo", "entradaDeModulo.ts"));

        Di("");
        if (!File.Exists(rutaFront))
        {
            Di($"--- PUERTA DEL FRONT: NO MEDIDA, no se encontro {rutaFront} ---");
        }
        else
        {
            var ts = await File.ReadAllTextAsync(rutaFront);

            // Se buscan las DECLARACIONES (`const X`) y no los nombres a secas:
            // los dos aparecen antes, nombrados en el comentario de cabecera, y
            // buscar el nombre suelto recortaba un trozo de comentario en vez de
            // la tabla. Se midio: con el nombre suelto salian CERO modulos y la
            // parte 5 "pasaba" sin medir nada, que es peor que no tenerla.
            var corte = ts.IndexOf("const MODULOS_SIN_PUERTA_PROPIA", StringComparison.Ordinal);
            var inicioTabla = ts.IndexOf("const PERMISO_DE_ENTRADA", StringComparison.Ordinal);
            Assert.True(inicioTabla >= 0 && corte > inicioTabla,
                $"No se reconocieron las dos tablas en {rutaFront}: la parte 5 no midio nada.");
            var bloqueTabla = ts.Substring(inicioTabla, corte - inicioTabla);

            var puertaFront = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(bloqueTabla, @"^\s*([a-z0-9_]+)\s*:\s*\[([^\]]*)\]",
                         System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                var claves = System.Text.RegularExpressions.Regex.Matches(m.Groups[2].Value, "\"([^\"]+)\"")
                    .Select(x => x.Groups[1].Value).ToArray();
                if (claves.Length > 0) puertaFront[m.Groups[1].Value] = claves;
            }

            // La familia fina explicita del front, para los modulos que
            // comparten raiz y no comparten pantalla (los tres de la cobranza).
            // Se lee del mismo archivo por la misma razon que la tabla: una
            // copia aqui se queda vieja y la medicion empieza a mentir.
            var familiaFina = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            {
                var iFam = ts.IndexOf("const FAMILIA_FINA", StringComparison.Ordinal);
                Assert.True(iFam > 0, $"No se encontro FAMILIA_FINA en {rutaFront}.");
                var finFam = ts.IndexOf("};", iFam, StringComparison.Ordinal);
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             ts.Substring(iFam, finFam - iFam),
                             @"^\s*([a-z0-9_]+)\s*:\s*\[([^\]]*)\]",
                             System.Text.RegularExpressions.RegexOptions.Multiline))
                {
                    var ps = System.Text.RegularExpressions.Regex.Matches(m.Groups[2].Value, "\"([^\"]+)\"")
                        .Select(x => x.Groups[1].Value).ToArray();
                    if (ps.Length > 0) familiaFina[m.Groups[1].Value] = ps;
                }
                Assert.NotEmpty(familiaFina);
            }

            // Los modulos cuya PANTALLA abre con la puerta mas algun permiso
            // de trabajo, como la forma de Mac31. Para esos, no tener la clave
            // de lectura no es un rebote. Se lee del front para no mantener
            // aqui una copia que se quede vieja.
            var abrenConLaPuerta = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            {
                var iAb = ts.IndexOf("const ABREN_CON_LA_PUERTA", StringComparison.Ordinal);
                Assert.True(iAb > 0, $"No se encontro ABREN_CON_LA_PUERTA en {rutaFront}.");
                var finAb = ts.IndexOf("]);", iAb, StringComparison.Ordinal);
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             ts.Substring(iAb, finAb - iAb), "\"([^\"]+)\""))
                    abrenConLaPuerta.Add(m.Groups[1].Value);
                Assert.NotEmpty(abrenConLaPuerta);
            }

            var sinPuertaPropia = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            {
                var resto = ts.Substring(corte);
                var fin = resto.IndexOf("]", StringComparison.Ordinal);
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(fin > 0 ? resto.Substring(0, fin) : resto, "\"([^\"]+)\""))
                    sinPuertaPropia.Add(m.Groups[1].Value);
            }

            Di($"--- PUERTA DEL FRONT: {puertaFront.Count} modulos con tabla, {sinPuertaPropia.Count} sin puerta propia ---");

            var escondidosGlobal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in webUsers.OrderBy(x => x.Usuario, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(u.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                    continue;
                var tiene = snapshots[u.Usuario];

                // Los modulos cuya puerta de backend SI tiene: esos los puede usar.
                var conPuerta = tiene
                    .Where(k => k.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase))
                    .Select(k => k.Substring(0, k.Length - ".ver_modulo".Length))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                /*
                  TENER LA PUERTA DE LA PLATAFORMA NO ES PODER USAR EL MODULO.

                  La primera version de esto solo pedia el ".ver_modulo", y
                  acusaba 29 modulos en Tauro, 28 de ellos de DEMO. Eran falsos:
                  a DEMO le apagaron TODOS los permisos de Mac31 y solo le
                  quedan las 34 puertas de plataforma, asi que esconderle la
                  tarjeta es exactamente lo que se pidio -no llenar el Inicio de
                  cosas en las que no puedes entrar-.

                  El defecto de verdad es otro: tener la puerta Y ALGUN PERMISO
                  FINO del modulo, o sea poder usarlo, y aun asi no verlo. Por
                  eso se exige lo segundo.
                */
                var escondidos = new List<string>();
                foreach (var modulo in conPuerta)
                {
                    if (sinPuertaPropia.Contains(modulo)) continue;
                    if (abrenConLaPuerta.Contains(modulo)) continue;
                    if (!puertaFront.TryGetValue(modulo, out var requeridas)) continue;  // sin regla -> se ve
                    if (requeridas.Any(tiene.Contains)) continue;

                    // Y "del modulo" no es solo "empieza con su clave": la
                    // tarjeta `cobros` se abre con `cobranza.ver` y sus permisos
                    // finos son `cobranza.*`. Asi que tambien cuentan los
                    // prefijos de las claves que el propio front exige, que es
                    // la unica fuente que sabe esa correspondencia.
                    // Solo de la clave de LECTURA ("<familia>.ver" o
                    // ".consultar"), no de cualquiera de las requeridas: una
                    // como "ventas.facturar" es una alternativa concreta y
                    // tomar su familia encendia pantallas ajenas. Es la misma
                    // regla que el front, y alla esta la medicion que la
                    // obligo.
                    List<string> prefijos;
                    if (familiaFina.TryGetValue(modulo, out var propia))
                    {
                        prefijos = propia.ToList();
                    }
                    else
                    {
                        prefijos = new List<string> { modulo + "." };
                        foreach (var r in requeridas)
                        {
                            var punto = r.LastIndexOf('.');
                            if (punto <= 0) continue;
                            var verbo = r.Substring(punto + 1);
                            if (verbo != "ver" && verbo != "consultar") continue;
                            prefijos.Add(r.Substring(0, punto + 1));
                        }
                    }

                    bool Encaja(string k) => prefijos.Any(pre =>
                        pre.EndsWith(".", StringComparison.Ordinal)
                            ? k.StartsWith(pre, StringComparison.OrdinalIgnoreCase)
                            : string.Equals(k, pre, StringComparison.OrdinalIgnoreCase));

                    var tieneAlgoFino = tiene.Any(k =>
                        !k.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase) && Encaja(k));
                    if (!tieneAlgoFino) continue;

                    // Que claves finas SI tiene. Sin esto la linea no sirve
                    // para arreglar nada: dice que modulo rebota pero no con
                    // que permiso deberia abrirse.
                    var finas = tiene
                        .Where(k => !k.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase) && Encaja(k))
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    escondidos.Add($"{modulo} (pide {string.Join("|", requeridas)}; tiene {string.Join(",", finas)})");
                    escondidosGlobal[modulo] = escondidosGlobal.GetValueOrDefault(modulo) + 1;
                }

                if (escondidos.Count > 0)
                    Di($"  {u.Usuario,-20} LA TARJETA SE VE Y LA PANTALLA REBOTA: {string.Join(", ", escondidos)}");
            }

            Di("");
            /*
              LO QUE ESTA LISTA ES DE VERDAD, Y COMO ME EQUIVOQUE AL LEERLA.

              Se llamaba "modulos que la segunda via rescata" y yo la lei como
              un marcador de exito: la tarjeta se dibuja. Pero la segunda via
              solo arregla LA TARJETA. La pantalla de dentro vuelve a pedir la
              clave de la tabla, y el backend tambien. Cada renglon de aqui es
              un modulo que SE VE EN EL INICIO Y REBOTA AL ABRIRLO, que es peor
              que esconderlo.

              Lo encontro el usuario, no yo: XOCHITL (Tauro) abriendo COBRANZA.
              Por eso ahora la lista se llama por lo que mide y tiene que
              quedar VACIA; lo que no se pueda vaciar se explica uno por uno.
            */
            Di($"--- MODULOS QUE SE VEN EN EL INICIO Y REBOTAN AL ABRIRLOS: {escondidosGlobal.Count} ---");
            Di("    (puerta heredada + permisos finos, sin la clave de lectura que pide la pantalla)");
            foreach (var kv in escondidosGlobal.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
                Di($"  {kv.Key,-45} usuarios afectados={kv.Value}");

            /*
              Y AQUI SE PARA LA PRUEBA, no solo se imprime.

              Mientras esto solo se escribia en el informe, yo lo lei como un
              marcador de exito y entregue el trabajo con quince renglones de
              rebote dentro. Un numero que nadie comprueba no mide nada.

              LA UNICA EXCEPCION ES `obra`, y no es un fallo de permisos: ese
              modulo NO TIENE PANTALLA en el web todavia (no hay ruta suya en
              App.tsx; de Mac31 quedan 16 formas MC* sin puente). DEMO, en
              Zaragoza, tiene permisos de obra.proveedores.* y por eso le sale
              la tarjeta. El dia que la pantalla exista, se quita de aqui.
            */
            var sinExplicacion = escondidosGlobal.Keys
                .Where(m => !string.Equals(m, "obra", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.True(sinExplicacion.Count == 0,
                $"{baseDatos}: hay modulos que se ven en el Inicio y rebotan al abrirlos: "
                + string.Join(", ", sinExplicacion)
                + $". El detalle, con quien los sufre, en artifacts\auditoria-herencia-{baseDatos}.txt.");
        }

        /*
          EL RESIDUO DE VERDAD: permisos finos SIN puerta.

          Despues de las dos vias, lo unico que deja a alguien fuera es tener
          permisos finos de un modulo y NO tener su puerta: el front lo esconde
          (condicion 1) y el backend lo rechaza con 403. Tiene que salir vacio;
          si sale algo, es una pantalla cuya entrada no se deriva de Mac31.
        */
        Di("");
        var residuo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in webUsers)
        {
            if (string.Equals(u.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase)) continue;
            var tiene = snapshots[u.Usuario];
            foreach (var k in tiene)
            {
                if (k.EndsWith(".ver_modulo", StringComparison.OrdinalIgnoreCase)) continue;
                if (EntradasDeModulo.PuedeEntrar(k, p => tiene.Contains(p))) continue;
                residuo[$"{u.Usuario}: {k}"] = 1;
            }
        }
        Di($"--- RESIDUO (permiso fino sin puerta: 403 y tarjeta escondida): {residuo.Count} ---");
        foreach (var kv in residuo.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            Di($"  {kv.Key}");

        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts");
        Directory.CreateDirectory(dir);
        await File.WriteAllLinesAsync(Path.Combine(dir, $"auditoria-herencia-{baseDatos}.txt"), renglones);
        _out.WriteLine($"[escrito en artifacts\\auditoria-herencia-{baseDatos}.txt]");
    }
}
