using System.Collections;
using System.Reflection;
using ISL_Service.Infrastructure.Security;

namespace ISL_Service.Tests;

/// <summary>
/// QUE EL PUENTE DESDE MAC31 NO SE LLEVE LOS PERMISOS QUE SON DEL WEB.
///
/// POR QUE ESTA PRUEBA EXISTE
/// --------------------------
/// Cuando una familia la manda Mac31, el snapshot se limpia POR PREFIJO: todo
/// lo que empiece con "compras." se borra y se vuelve a calcular desde los
/// botones de n_Procesos. Las claves del web que viven bajo ese prefijo tienen
/// que estar declaradas como semilla del web o el borrado se las lleva.
///
/// Y cuando se las lleva, no hay error, ni excepcion, ni renglon en el log: se
/// guardan en WUsuarioPermiso, se dice que si, y desaparecen en la siguiente
/// lectura. Ya paso dos veces documentadas —"pendientes de autorizar" en
/// ventas y "reportes.ver_modulo"—, las dos encontradas a mano y las dos
/// despues de que alguien de produccion dijera "le doy Guardar y al volver a
/// entrar no esta".
///
/// Esta prueba cierra el ciclo: si se agrega una clave al catalogo bajo un
/// prefijo que manda legacy y no se dice de quien es, falla aqui.
/// </summary>
public class FamiliasLegacyNoSePierdenTests
{
    private static readonly Type Tipo = typeof(PermissionService);

    private static object LeerCampo(string nombre)
    {
        var campo = Tipo.GetField(nombre, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"No existe el campo estatico {nombre}.");
        return campo.GetValue(null) ?? throw new InvalidOperationException($"{nombre} es null.");
    }

    private static object? Propiedad(object obj, string nombre) =>
        obj.GetType().GetProperty(nombre)?.GetValue(obj);

    private static IEnumerable<string> ClavesDe(object coleccionDeSeeds)
    {
        foreach (var seed in (IEnumerable)coleccionDeSeeds)
            yield return (string)Propiedad(seed!, "Key")!;
    }

    /// <summary>Las familias nuevas, como (prefijo, claves que gobierna, claves del web).</summary>
    private static List<(string Prefijo, HashSet<string> DeLegacy, HashSet<string> DelWeb)> Familias()
    {
        var resultado = new List<(string, HashSet<string>, HashSet<string>)>();

        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Propiedad(familia!, "Prefijo")!;

            var deLegacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var forma in (IEnumerable)Propiedad(familia!, "Formas")!)
            {
                var mapa = (IEnumerable)Propiedad(forma!, "Map")!;
                foreach (var par in mapa)
                {
                    // KeyValuePair<string, (string Key, string Name)>
                    var valor = Propiedad(par!, "Value")!;
                    deLegacy.Add((string)valor.GetType().GetField("Item1")!.GetValue(valor)!);
                }
            }

            var delWeb = new HashSet<string>(
                ClavesDe(Propiedad(familia!, "SemillasDelWeb")!),
                StringComparer.OrdinalIgnoreCase);

            resultado.Add((prefijo, deLegacy, delWeb));
        }

        return resultado;
    }

    /// <summary>
    /// QUE LOS ACENTOS DE LOS NOMBRES DE FORMA SOBREVIVAN AL COMPILADOR.
    ///
    /// Las formas se buscan en Mac31 por su DESCRIPCION, y tres de las nuevas
    /// la llevan acentuada: "CONSULTA DE GARANTÍAS", "CONSULTA DE REGISTRO DE
    /// GARANTÍAS" y "FECHA DE OPERACIÓN". Si el archivo fuente se guardara con
    /// otra codificacion, el literal llegaria con la letra cambiada, la
    /// consulta no empataria ninguna fila, el catalogo vendria vacio y —por la
    /// red de seguridad que no borra nada cuando el catalogo esta vacio— el
    /// puente simplemente NO PASARIA NADA. Sin error, sin log: el permiso se
    /// quedaria con lo que diga el web y nadie entenderia por que.
    ///
    /// Se comprueba por punto de codigo, no comparando con otro literal del
    /// mismo archivo —eso se cumpliria solo— sino contra los numeros.
    /// </summary>
    [Fact]
    public void Los_Nombres_De_Forma_Con_Acento_Llegan_Enteros()
    {
        var formas = new List<string>();
        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
            foreach (var forma in (IEnumerable)Propiedad(familia!, "Formas")!)
                formas.Add((string)Propiedad(forma!, "LegacyForm")!);

        // I LATINA MAYUSCULA CON ACENTO = U+00CD, O = U+00D3.
        Assert.Contains("CONSULTA DE GARANTÍAS", formas);
        Assert.Contains("CONSULTA DE REGISTRO DE GARANTÍAS", formas);
        Assert.Contains("FECHA DE OPERACIÓN", formas);

        // Y que no se haya colado un '?' ni una interrogacion de reemplazo.
        Assert.DoesNotContain(formas, f => f.Contains('?') || f.Contains('�'));
    }

    [Fact]
    public void Hay_Familias_Declaradas()
    {
        // Si la reflexion deja de encontrar el campo, el resto de la prueba
        // pasaria en vacio y no probaria nada. Se comprueba primero.
        Assert.NotEmpty(Familias());
    }

    /// <summary>
    /// Toda clave del catalogo que caiga bajo un prefijo que manda legacy tiene
    /// que tener dueno: o la da Mac31, o esta declarada como semilla del web.
    /// </summary>
    [Fact]
    public void Ninguna_Clave_Del_Catalogo_Se_Queda_Sin_Dueno()
    {
        var catalogo = ClavesDe(LeerCampo("PermissionCatalogSeeds")).ToList();
        var huerfanas = new List<string>();

        foreach (var (prefijo, deLegacy, delWeb) in Familias())
        {
            foreach (var clave in catalogo)
            {
                if (!clave.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) continue;
                if (deLegacy.Contains(clave) || delWeb.Contains(clave)) continue;
                huerfanas.Add($"{clave} (prefijo '{prefijo}')");
            }
        }

        Assert.True(
            huerfanas.Count == 0,
            "Estas claves se borrarian en silencio en la siguiente lectura. Hay que mapearlas a un boton "
            + "de Mac31 o declararlas como semilla del web en PermissionService.FamiliasLegacy.cs:\n  - "
            + string.Join("\n  - ", huerfanas.Distinct()));
    }

    /// <summary>
    /// Lo mismo, pero mirando desde los CONTROLADORES: una clave que un
    /// endpoint exige y que la familia borra sin reponer deja ese endpoint en
    /// 403 permanente para todos menos el SuperAdmin.
    /// </summary>
    [Fact]
    public void Ninguna_Clave_Que_Exige_Un_Controlador_Se_Queda_Sin_Dueno()
    {
        var familias = Familias();
        var huerfanas = new List<string>();

        foreach (var clave in ClavesQueExigenLosControladores())
        {
            foreach (var (prefijo, deLegacy, delWeb) in familias)
            {
                if (!clave.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) continue;
                if (deLegacy.Contains(clave) || delWeb.Contains(clave)) continue;
                huerfanas.Add($"{clave} (prefijo '{prefijo}')");
            }
        }

        Assert.True(
            huerfanas.Count == 0,
            "Estos endpoints quedarian en 403 permanente:\n  - " + string.Join("\n  - ", huerfanas.Distinct()));
    }

    /// <summary>
    /// Una clave no puede venir de Mac31 Y ser del web a la vez: si lo fuera,
    /// el borrado la respetaria y el puente la volveria a poner, y cual gana
    /// dependeria del orden en que corrio el codigo.
    /// </summary>
    [Fact]
    public void Ninguna_Clave_Tiene_Dos_Duenos()
    {
        foreach (var (prefijo, deLegacy, delWeb) in Familias())
        {
            var ambas = deLegacy.Intersect(delWeb, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.True(ambas.Count == 0, $"'{prefijo}' declara con dos duenos: {string.Join(", ", ambas)}");
        }
    }

    /// <summary>
    /// Dos familias no pueden pelearse la misma clave: el borrado de una se
    /// llevaria lo que acaba de poner la otra.
    /// </summary>
    [Fact]
    public void Dos_Familias_No_Se_Pisan()
    {
        var familias = Familias();
        var vistas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (prefijo, deLegacy, _) in familias)
        {
            foreach (var clave in deLegacy)
            {
                Assert.False(
                    vistas.TryGetValue(clave, out var otro),
                    $"'{clave}' la reclaman '{otro}' y '{prefijo}'.");
                vistas[clave] = prefijo;
            }
        }
    }

    /// <summary>
    /// Toda clave que una familia saca de Mac31 tiene que existir tambien en
    /// el catalogo escrito a mano. No es redundante: el catalogo es lo que
    /// siembra el arranque, y sin esa semilla el permiso solo aparece en
    /// WPermiso la primera vez que alguien abre la pantalla de permisos.
    /// </summary>
    [Fact]
    public void Lo_Que_Sale_De_Mac31_Tambien_Esta_En_El_Catalogo()
    {
        var catalogo = new HashSet<string>(
            ClavesDe(LeerCampo("PermissionCatalogSeeds")),
            StringComparer.OrdinalIgnoreCase);

        var faltantes = new List<string>();
        foreach (var (prefijo, deLegacy, _) in Familias())
        {
            foreach (var clave in deLegacy)
            {
                if (!catalogo.Contains(clave))
                    faltantes.Add($"{clave} (familia '{prefijo}')");
            }
        }

        Assert.True(faltantes.Count == 0,
            "Faltan en PermissionCatalogSeeds:\n  - " + string.Join("\n  - ", faltantes));
    }

    /// <summary>
    /// Las claves que de verdad deciden en un controlador, por los dos caminos
    /// vivos: la politica <c>perm:x|y</c> y los arreglos de claves que consume
    /// <c>PermisoControllerBase</c>.
    /// </summary>
    /// <remarks>
    /// Por REFLEXION y no leyendo los .cs del disco: una prueba que busca
    /// archivos fuente se rompe en cuanto se compila a una carpeta de salida
    /// fuera del arbol, y entonces no falla — pasa en vacio, que es peor.
    /// Es el mismo recorrido que hace ClavesDePermisoSembradasTests.
    /// </remarks>
    private static HashSet<string> ClavesQueExigenLosControladores()
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var controladores = typeof(ISL_Service.Controllers.SesionController).Assembly
            .GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var tipo in controladores)
        {
            foreach (var attr in tipo.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true))
                AgregarDePolitica(claves, attr.Policy);

            foreach (var metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var attr in metodo.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true))
                    AgregarDePolitica(claves, attr.Policy);

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
