using System.Reflection;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Tests;

/// <summary>
/// QUE TODA CLAVE DE PERMISO QUE EL CODIGO EXIGE LA SIEMBRE EL ARRANQUE.
///
/// POR QUE ESTA PRUEBA EXISTE
/// --------------------------
/// Una clave que no esta en `WPermiso` no puede tener fila de concesion, asi que
/// su endpoint contesta 403 a todo el mundo menos al SuperAdmin. El SuperAdmin
/// pasa antes de mirar nada, asi que el agujero es invisible para quien lo
/// prueba desde una cuenta de administrador: el modulo se ve perfecto en local y
/// esta muerto para los usuarios de verdad.
///
/// Ya paso tres veces, y cada vez se encontro a mano: primero `catalogos.*`,
/// `obra.*`, `cobranza.*` y `productos.*` (de ahi nacio
/// CatalogoDePermisosHostedService); luego `usuarios.password.ver` y
/// `reportes.centros.*`; y en la auditoria final, Cambio de Cascos, el Checador,
/// Asistencias y TODO el canal movil — que existian en WPermiso de Tauro solo
/// porque alguien corrio un script a mano, y en una base de empresa NUEVA
/// (comprobado en MacZ) nunca entraban.
///
/// El patron es siempre el mismo: la clave se escribe en el controlador y nadie
/// se acuerda de la lista de semillas. Esta prueba cierra el ciclo: si se agrega
/// una clave en un controlador y no se siembra, falla aqui y no en produccion.
///
/// NO PRUEBA que la clave llegue a la base —eso depende de la base— sino que el
/// codigo DECLARE quien la siembra. Es lo unico que una prueba puede garantizar.
/// </summary>
public class ClavesDePermisoSembradasTests
{
    /// <summary>
    /// Familias cuyas claves NO estan escritas a mano porque salen de la base, y
    /// que tienen su propia rutina de siembra llamada desde
    /// <c>SembrarCatalogoCompletoAsync</c>. Cada una con el motivo.
    /// </summary>
    private static readonly (string Prefijo, string Quien)[] FamiliasDerivadas =
    {
        // Una clave por nodo del catalogo dbo.Reportes y una por centro de
        // servicio activo: no se pueden listar en codigo porque cambian con la
        // base. Las siembran EnsureLegacyReportPermissionsSyncedAsync y
        // EnsureCentrosPermissionsSyncedAsync.
        ("reportes.", "EnsureLegacyReportPermissionsSyncedAsync / EnsureCentrosPermissionsSyncedAsync"),

        // Las cuatro familias que gobierna Mac31: sus claves se deducen de los
        // botones de n_Procesos, forma por forma. Las siembran
        // EnsureVentasPermissionsSyncedAsync y
        // EnsureLegacyModulePermissionsSyncedAsync (empleados y prestamos).
        ("ventas.", "EnsureVentasPermissionsSyncedAsync"),
        ("empleados.", "EnsureLegacyModulePermissionsSyncedAsync(EmpleadosBinding)"),
        ("prestamos.", "EnsureLegacyModulePermissionsSyncedAsync(PrestamosBinding)")
    };

    [Fact]
    public void Cada_Clave_Que_Exige_Un_Controlador_La_Siembra_Alguien()
    {
        var sembradas = ClavesSembradasEnCodigo();
        var huerfanas = new List<string>();

        foreach (var clave in ClavesQueExigenLosControladores())
        {
            if (sembradas.Contains(clave)) continue;
            if (FamiliasDerivadas.Any(f => clave.StartsWith(f.Prefijo, StringComparison.OrdinalIgnoreCase))) continue;
            huerfanas.Add(clave);
        }

        Assert.True(huerfanas.Count == 0,
            "Estas claves se exigen en un controlador y NADIE las siembra, asi que su endpoint "
            + "contesta 403 a todos menos al SuperAdmin en cuanto la base no las traiga de antes. "
            + "Agregalas a PermissionService.PermissionCatalogSeeds (o explica aqui quien las siembra): "
            + string.Join(", ", huerfanas.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Las claves de Cambio de Cascos, Checador, Asistencias y canal movil, una
    /// por una. La prueba de arriba las cubre, pero si alguien borra una semilla
    /// esta dice EXACTAMENTE cual, y deja escrito el hallazgo de la auditoria.
    /// </summary>
    public static TheoryData<string> ClavesDeLaAuditoriaFinal() => new()
    {
        "cascos_cambio.ver",
        "cascos_cambio.crear",
        "cascos_cambio.cancelar",
        "app_movil.cascos_cambio.ver",
        "app_movil.cascos_cambio.crear",
        "app_movil.cascos_cambio.cancelar",
        "app_movil.ventas.usados",
        "app_movil.pedidos",
        "asistencias.ver",
        "asistencias.editar",
        "checador.ver",
        "checador.huellas",
        "checador.reportes"
    };

    [Theory]
    [MemberData(nameof(ClavesDeLaAuditoriaFinal))]
    public void Las_Claves_De_La_Auditoria_Estan_En_La_Lista_De_Semillas(string clave)
    {
        Assert.Contains(clave, SemillasEstaticas());
    }

    /// <summary>
    /// `ventas.*` NO se siembra desde PermissionCatalogSeeds a proposito: la
    /// gobierna Mac31 y una segunda declaracion crearia dos duenos de la misma
    /// llave. Si alguien "arregla" el hueco metiendolas aqui, esto lo frena.
    /// </summary>
    [Theory]
    [InlineData("ventas.ver_modulo")]
    [InlineData("ventas.usados")]
    [InlineData("ventas.usados.guardar")]
    public void Las_Claves_De_Ventas_No_Se_Declaran_Dos_Veces(string clave)
    {
        Assert.DoesNotContain(clave, SemillasEstaticas());
    }

    // ------------------------------------------------------------------ ayudas

    private static HashSet<string> SemillasEstaticas()
    {
        var campo = typeof(PermissionService).GetField("PermissionCatalogSeeds",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return LeerClaves(campo.GetValue(null));
    }

    /// <summary>
    /// Todo lo que el codigo declara como semilla: la lista estatica mas las
    /// listas blancas del puente de legacy y los mapas boton -> clave.
    /// </summary>
    private static HashSet<string> ClavesSembradasEnCodigo()
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var campo in typeof(PermissionService)
                     .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
        {
            var valor = campo.GetValue(null);
            if (valor is null) continue;

            if (valor is string texto && texto.Contains('.'))
            {
                claves.Add(texto);
                continue;
            }

            foreach (var c in LeerClaves(valor)) claves.Add(c);
        }

        return claves;
    }

    /// <summary>
    /// Saca claves de una lista de semillas (<c>PermissionSeed.Key</c>) o de un
    /// mapa de botones de legacy (<c>boton -&gt; (Key, Name)</c>).
    /// </summary>
    private static HashSet<string> LeerClaves(object? valor)
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (valor is not System.Collections.IEnumerable lista) return claves;

        foreach (var item in lista)
        {
            if (item is null) continue;

            // HashSet<string> / List<string>: la clave es el elemento.
            if (item is string suelta)
            {
                if (suelta.Contains('.')) claves.Add(suelta);
                continue;
            }

            // Diccionario boton -> (Key, Name): la clave del permiso esta dentro
            // del Value, que es una tupla.
            var objetivo = item.GetType().GetProperty("Value")?.GetValue(item) ?? item;
            if (objetivo is null) continue;

            // PermissionSeed es un record con propiedad Key; la tupla tiene campo Item1.
            if (objetivo.GetType().GetProperty("Key")?.GetValue(objetivo) is string porPropiedad
                && porPropiedad.Contains('.'))
            {
                claves.Add(porPropiedad);
                continue;
            }

            if (objetivo.GetType().GetField("Item1")?.GetValue(objetivo) is string porCampo
                && porCampo.Contains('.'))
            {
                claves.Add(porCampo);
            }
        }

        return claves;
    }

    /// <summary>
    /// Las claves que de verdad deciden en un controlador, por los dos caminos
    /// vivos: la politica <c>perm:x|y</c> y los arreglos de claves que consume
    /// <c>PermisoControllerBase</c>.
    /// </summary>
    private static HashSet<string> ClavesQueExigenLosControladores()
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var controladores = typeof(ISL_Service.Controllers.SesionController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var tipo in controladores)
        {
            foreach (var attr in tipo.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                AgregarDePolitica(claves, attr.Policy);

            foreach (var metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var attr in metodo.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                    AgregarDePolitica(claves, attr.Policy);

            // Los controladores de PermisoControllerBase guardan sus claves en
            // campos estaticos: `private static readonly string[] PermisosVer`
            // o `private const string PermisoChecar`.
            foreach (var campo in tipo.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            {
                if (!campo.Name.StartsWith("Permiso", StringComparison.Ordinal)) continue;

                var valor = campo.GetValue(null);
                if (valor is string uno && EsClave(uno)) claves.Add(uno);
                if (valor is IEnumerable<string> varias)
                    foreach (var c in varias)
                        if (EsClave(c)) claves.Add(c);
            }
        }

        return claves;
    }

    private static void AgregarDePolitica(HashSet<string> claves, string? policy)
    {
        const string prefijo = "perm:";
        if (string.IsNullOrWhiteSpace(policy) || !policy.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var c in policy[prefijo.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (EsClave(c)) claves.Add(c);
    }

    private static bool EsClave(string? valor)
        => !string.IsNullOrWhiteSpace(valor) && valor.Contains('.') && !valor.Contains(' ');
}
