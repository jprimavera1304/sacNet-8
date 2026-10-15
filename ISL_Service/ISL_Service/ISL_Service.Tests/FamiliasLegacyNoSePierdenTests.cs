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

    /// <summary>
    /// Las familias nuevas, como (prefijo, claves que gobierna, claves del web).
    /// </summary>
    /// <remarks>
    /// EL `Map` NO ES TODO LO QUE LEGACY PRODUCE, Y POR NO VERLO ESTA PRUEBA
    /// ACUSABA EN FALSO.
    ///
    /// Desde que existe la tabla de verbos (PermissionService.VerbosLegacy.cs),
    /// una forma que NO mapea nada igual produce claves: LoadLegacyModuleCatalogAsync
    /// llama a DerivarClaveLegacy y arma "{Prefijo de la forma}.{verbo}" para
    /// cada boton cuyo nombre este en la tabla. La familia de proveedores es el
    /// ejemplo: su unica forma tiene el `Map` VACIO a proposito, y aun asi
    /// produce "proveedores.ver", ".crear" y ".editar" -los tres botones que el
    /// web ya usaba-.
    ///
    /// Leyendo solo el `Map`, esas tres claves parecian huerfanas y la prueba
    /// decia que ProveedoresController quedaria en 403 permanente. Es falso, y
    /// se comprobo contra las dos bases el 2026-10-10: cero claves que se
    /// borren sin reponerse y cero endpoints en 403, tanto en MacZ como en
    /// Produccion_svr (ver DiagnosticoFamiliasContraLaBase).
    ///
    /// Asi que aqui se modela el mecanismo entero: lo del `Map` MAS lo que la
    /// tabla de verbos puede derivar para el prefijo de cada forma.
    ///
    /// LO QUE ESTA APROXIMACION NO PUEDE SABER: si ese boton existe de verdad
    /// en n_Procesos. "{prefijo}.editar" se da por cubierto aunque la forma no
    /// tenga btnModificar -es justo el caso de `gasolinas.editar`, donde el
    /// boton MODIFICAR se llama btnVer-. Esa mitad solo se puede comprobar
    /// contra una base, y para eso esta la prueba de la base.
    /// </remarks>
    /// <param name="Mapeadas">
    /// Lo que la familia declara A MANO en su <c>Map</c>. Es intencion escrita,
    /// exacta, y por eso es lo que miran las pruebas de "dos duenos" y "dos
    /// familias se pisan": ahi lo que importa es lo que alguien declaro.
    /// </param>
    /// <param name="Derivables">
    /// <paramref name="Mapeadas"/> mas todo lo que la tabla de verbos PODRIA
    /// derivar. Es lo que miran las pruebas de "sin dueno", porque la pregunta
    /// ahi es "despues del borrado por prefijo, puede legacy reponer esto".
    /// </param>
    private readonly record struct Familia(
        string Prefijo,
        HashSet<string> Mapeadas,
        HashSet<string> Derivables,
        HashSet<string> DelWeb);

    private static List<Familia> Familias()
    {
        var verbos = (IDictionary)LeerCampo("VerbosLegacy");
        var resultado = new List<Familia>();

        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Propiedad(familia!, "Prefijo")!;

            var mapeadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var derivables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var forma in (IEnumerable)Propiedad(familia!, "Formas")!)
            {
                var mapa = (IEnumerable)Propiedad(forma!, "Map")!;
                foreach (var par in mapa)
                {
                    // KeyValuePair<string, (string Key, string Name)>
                    var valor = Propiedad(par!, "Value")!;
                    mapeadas.Add((string)valor.GetType().GetField("Item1")!.GetValue(valor)!);
                }

                // Y lo que la tabla de verbos deriva sola. Mismo fallback que
                // LoadLegacyModuleCatalogAsync: `Prefijo` de la forma, y si no
                // lo declara, su ModuleKey.
                var prefijoForma = (string?)Propiedad(forma!, "Prefijo")
                    ?? (string)Propiedad(forma!, "ModuleKey")!;
                foreach (string verbo in verbos.Values)
                    derivables.Add($"{prefijoForma}.{verbo}");
            }

            derivables.UnionWith(mapeadas);

            var delWeb = new HashSet<string>(
                ClavesDe(Propiedad(familia!, "SemillasDelWeb")!),
                StringComparer.OrdinalIgnoreCase);

            resultado.Add(new Familia(prefijo, mapeadas, derivables, delWeb));
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

        foreach (var f in Familias())
        {
            foreach (var clave in catalogo)
            {
                if (!clave.StartsWith(f.Prefijo, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Derivables.Contains(clave) || f.DelWeb.Contains(clave)) continue;
                huerfanas.Add($"{clave} (prefijo '{f.Prefijo}')");
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
            foreach (var f in familias)
            {
                if (!clave.StartsWith(f.Prefijo, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Derivables.Contains(clave) || f.DelWeb.Contains(clave)) continue;
                huerfanas.Add($"{clave} (prefijo '{f.Prefijo}')");
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
        // Sobre lo MAPEADO, no sobre lo derivable: una clave como "compras.ver"
        // es semilla del web a proposito -la forma de Mac31 no tiene boton de
        // buscar dado de alta- y la tabla de verbos la daria por derivable sin
        // que exista el boton. Lo que no puede pasar es que alguien la declare
        // en el `Map` Y como semilla.
        foreach (var f in Familias())
        {
            var ambas = f.Mapeadas.Intersect(f.DelWeb, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.True(ambas.Count == 0, $"'{f.Prefijo}' declara con dos duenos: {string.Join(", ", ambas)}");
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

        // Tambien sobre lo MAPEADO: lo derivable se solapa por construccion
        // entre familias cuyo prefijo cuelga de otro ("cobros." y
        // "cobros.reasignar"), y eso ya lo resuelve EsLaFamiliaMasEspecifica.
        foreach (var f in familias)
        {
            foreach (var clave in f.Mapeadas)
            {
                Assert.False(
                    vistas.TryGetValue(clave, out var otro),
                    $"'{clave}' la reclaman '{otro}' y '{f.Prefijo}'.");
                vistas[clave] = f.Prefijo;
            }
        }
    }

    /// <summary>
    /// TODA CLAVE MAPEADA A MANO CAE BAJO EL PREFIJO DE SU PROPIA FAMILIA.
    /// </summary>
    /// <remarks>
    /// ESTA PRUEBA PEDIA OTRA COSA Y LA PEDIA MAL.
    ///
    /// Antes exigia que cada clave que sale de Mac31 estuviera ademas escrita a
    /// mano en <c>PermissionCatalogSeeds</c>, con el argumento de que el
    /// catalogo es lo unico que siembra WPermiso. Eso no es cierto desde que
    /// existen las familias: <c>EnsureLegacyModulePermissionsSyncedAsync</c>
    /// llama a <c>SincronizarCatalogoDePermisosAsync</c> con el catalogo que
    /// acaba de resolver, o sea que la familia SIEMBRA SUS PROPIAS CLAVES cada
    /// vez que se resuelve, incluido el snapshot de /api/me. La semilla a mano
    /// es redundante para ellas.
    ///
    /// Y pedirla era ademas contrario a lo decidido: las claves nuevas que
    /// todavia no protegen ningun endpoint NO deben aparecer como permisos
    /// administrables. Sembrar las 105 a mano es justo lo contrario.
    ///
    /// Lo que de verdad hacia falta comprobar -que nada se borre sin reponerse-
    /// no se puede saber leyendo el codigo, porque depende de que el boton
    /// exista en n_Procesos. Se midio contra las dos bases el 2026-10-10 y dio
    /// cero en las dos; esa comprobacion vive en
    /// <see cref="DiagnosticoFamiliasContraLaBase"/>.
    ///
    /// Lo que SI se puede comprobar aqui, y sigue siendo un fallo real si se
    /// rompe: una clave mapeada que cayera FUERA del prefijo de su familia se
    /// sembraria pero no la gobernaria nadie —el borrado por prefijo no la
    /// alcanza y el puente no la repone—, asi que se quedaria congelada con lo
    /// que dijera el web y Mac31 dejaria de mandar sobre ella en silencio.
    /// </remarks>
    [Fact]
    public void Ninguna_Clave_Mapeada_Se_Sale_De_Su_Familia()
    {
        var fuera = new List<string>();

        foreach (var f in Familias())
        {
            foreach (var clave in f.Mapeadas)
            {
                if (!clave.StartsWith(f.Prefijo, StringComparison.OrdinalIgnoreCase))
                    fuera.Add($"{clave} (familia '{f.Prefijo}')");
            }
        }

        Assert.True(fuera.Count == 0,
            "Estas claves las produce la familia pero su prefijo no las gobierna, asi que nadie "
            + "las repone ni las borra:\n  - " + string.Join("\n  - ", fuera));
    }

    /// <summary>
    /// EL PRIMER TRAMO DE LA CLAVE TIENE QUE SER EL MODULO DE LA FAMILIA.
    ///
    /// De esto depende el unico candado que hace seguro escribir de vuelta en
    /// Mac31: <c>SyncLegacyModulePermissionsForUserAsync</c> solo apaga un
    /// permiso si <c>IsPermissionInActiveModule</c> dice que su modulo esta
    /// encendido, y ese metodo saca el modulo cortando la clave en el PRIMER
    /// punto (<c>ExtractModuleFromPermissionKey</c>).
    ///
    /// Si el <c>ModuleKey</c> de una familia no fuera ese primer tramo, el
    /// modulo que se da de alta en WModulo no seria el que luego se busca: el
    /// candado daria siempre falso, las concesiones si viajarian a Mac31 y las
    /// revocaciones no. Esa es exactamente la "puerta de un solo sentido" que
    /// ya se documento para reportes —encendias un boton y no habia forma de
    /// apagarlo desde ninguna de las dos pantallas—.
    ///
    /// NO se exige que cada atadura declare como <c>ModuleKey</c> su propio
    /// primer tramo: <c>FacturasFamilia</c> pone a proposito <c>"facturas"</c>
    /// en la atadura que deriva claves <c>facturacion.*</c>, para que el upsert
    /// de WModulo cree el renglon <c>facturas</c> que a Tauro le falta. Eso vale
    /// porque OTRA atadura de la misma familia si da de alta <c>facturacion</c>.
    /// Lo que se exige es que el renglon exista, lo cree quien lo cree.
    /// </summary>
    [Fact]
    public void El_Modulo_De_Cada_Clave_Lo_Da_De_Alta_Alguien()
    {
        // Todos los modulos que el puente llega a crear en WModulo.
        var modulosQueSeCrean = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            modulosQueSeCrean.Add((string)Propiedad(familia!, "ModuleKey")!);
            foreach (var forma in (IEnumerable)Propiedad(familia!, "Formas")!)
                modulosQueSeCrean.Add((string)Propiedad(forma!, "ModuleKey")!);
        }

        var malas = new List<string>();

        foreach (var familia in (IEnumerable)LeerCampo("FamiliasLegacyAmpliadas"))
        {
            var prefijo = (string)Propiedad(familia!, "Prefijo")!;
            var moduleKey = (string)Propiedad(familia!, "ModuleKey")!;

            // El prefijo que GOBIERNA el borrado.
            if (!modulosQueSeCrean.Contains(prefijo.Split('.')[0]))
                malas.Add($"familia '{prefijo}': nadie da de alta el modulo '{prefijo.Split('.')[0]}'");

            if (!string.Equals(prefijo.Split('.')[0], moduleKey, StringComparison.OrdinalIgnoreCase))
                malas.Add($"familia '{prefijo}' dice ModuleKey '{moduleKey}' pero su primer tramo es '{prefijo.Split('.')[0]}'");

            // Y el prefijo con el que cada forma DERIVA sus claves.
            foreach (var forma in (IEnumerable)Propiedad(familia!, "Formas")!)
            {
                var moduloForma = (string)Propiedad(forma!, "ModuleKey")!;
                var prefijoForma = (string?)Propiedad(forma!, "Prefijo") ?? moduloForma;
                var tramoForma = prefijoForma.Split('.')[0];

                if (!modulosQueSeCrean.Contains(tramoForma))
                    malas.Add($"forma de '{prefijo}' deriva claves '{tramoForma}.*' y nadie da de alta ese modulo");
            }
        }

        Assert.True(malas.Count == 0,
            "El candado del modulo no cerraria para estas, asi que se podria conceder en Mac31 y no revocar:\n  - "
            + string.Join("\n  - ", malas.Distinct()));
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
