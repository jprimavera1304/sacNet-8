using System.Reflection;
using System.Text.RegularExpressions;
using ISL_Service.Infrastructure.Security;

namespace ISL_Service.Tests;

/// <summary>
/// QUE TODA CLAVE `app_movil.*` QUE PIDE LA APP ESTE SEMBRADA EN EL CATALOGO.
///
/// POR QUE EXISTE, CON EL CASO QUE LA MOTIVA
/// -----------------------------------------
/// <see cref="ClavesDePermisoSembradasTests"/> ya vigila esto, pero solo para las
/// claves que exige un CONTROLADOR: las encuentra por reflexion sobre los
/// atributos del backend. Y resulta que las claves mas importantes del canal
/// movil no las exige ningun endpoint: las comprueba el telefono para decidir
/// que ENSENA, y viven unicamente en el codigo Dart.
///
/// Por eso se nos pasaron cuatro, y una era la llave maestra:
///
///   app_movil.acceso         sin ella NO ENTRA NADIE a la app
///   app_movil.oficina        el portal de oficina
///   app_movil.reportes       el modulo de Reportes en el telefono
///   app_movil.pedidos.editar el boton Editar de "Mis pedidos"
///
/// Existian en las dos bases de la casa porque alguien corrio a mano los
/// scripts de `Querrys/07` y `Querrys/25` en su dia. En una base de empresa
/// NUEVA no existirian, y entonces la app no se abre para nadie que no sea
/// SuperAdmin —que se salta toda comprobacion, y por eso el agujero es
/// invisible para quien lo prueba con su cuenta de administrador—.
///
/// COMO LO MIDE
/// ------------
/// Lee los .dart de la app y saca los literales `'app_movil...'`. No es analisis
/// del lenguaje y no hace falta que lo sea: una clave de permiso en Dart es
/// siempre una cadena literal.
///
/// SI LA APP NO ESTA AL LADO, LA PRUEBA NO FALLA: son dos repos distintos y
/// quien compile solo el backend no tiene por que tener el movil. Se salta con
/// su motivo, que es lo honesto —una prueba que no pudo medir no debe decir que
/// midio—.
/// </summary>
public class ClavesQuePideLaAppMovilTests
{
    /// <summary>
    /// Claves de MOSAICO, que se parecen a permisos y no lo son.
    ///
    /// `modules_config.dart` nombra cada pantalla con una clave `app_movil.*`
    /// —es el identificador del mosaico, no algo que se conceda—. Sembrarlas
    /// pondria casillas en la pantalla de permisos que no apagan nada.
    ///
    /// `app_movil.productos` NO esta en esta lista a proposito: alli el nombre
    /// del mosaico y el del permiso coinciden, y el permiso SI existe.
    ///
    /// COMO SE DECIDE SI UNA CLAVE ENTRA AQUI: se mira en el codigo Dart para
    /// que se usa. Si es el valor de `clave:` de un `ModuloDelTelefono`, es el
    /// nombre del mosaico y entra. Si alguien la pasa a `hasPermission` o a
    /// `hasAnyPermission`, es un PERMISO y tiene que estar sembrada — meterla
    /// aqui seria tapar justo el fallo que esta prueba busca.
    /// </summary>
    private static readonly HashSet<string> ClavesDeMosaico = new(StringComparer.OrdinalIgnoreCase)
    {
        "app_movil.autorizar",
        "app_movil.repartidor",
        "app_movil.facturas",
        "app_movil.cortes",
        "app_movil.ventas.consulta",
        "app_movil.almacen_cascos",
        "app_movil.inventario.ajustes",
        "app_movil.inventario.transito",
        /*
          Los mosaicos de las pantallas que la app estreno despues. Ninguno se
          concede: son el identificador del mosaico, y el permiso que SI se
          concede es su `.ver` (`app_movil.gasolinas.ver`,
          `app_movil.cobranzas.ver`...), que va sembrado por separado.
        */
        "app_movil.asistencias",
        "app_movil.bancos_transferencias",
        "app_movil.cambios_vigencia",
        "app_movil.cobranzas",
        "app_movil.concentrados",
        "app_movil.dinero_usados",
        "app_movil.garantias",
        "app_movil.garantias_aceptadas",
        "app_movil.gasolinas",
        "app_movil.reasignacion",
        "app_movil.recepcion_cascos",
        // El prefijo del canal, que se usa suelto en comparaciones y filtros.
        "app_movil"
    };

    [Fact]
    public void Cada_Clave_Que_Pide_La_App_Esta_Sembrada()
    {
        /*
          SALTARSE LA PRUEBA SOLO SE PERMITE SI DE VERDAD NO HAY APP AL LADO.

          El `return` de antes se tragaba los dos casos a la vez:

            1. nadie clono el movil (CI del backend solo) -> saltar es correcto;
            2. la carpeta del movil se renombro o se movio  -> saltar es el bug
               que esta clase vino a corregir, y saltar es MENTIR.

          Se distinguen porque el caso 2 deja huella: si se encuentra la raiz del
          monorepo —la carpeta que contiene `sacNet-8`— entonces el movil DEBERIA
          estar ahi, y no encontrarlo es un renombre, no una ausencia. Entonces
          falla, con la ruta exacta en la que lo busco.
        */
        var raices = CarpetasDeLaApp();
        if (raices.Count == 0)
        {
            var raizDelMonorepo = SubiendoHasta("sacNet-8");
            Assert.True(raizDelMonorepo is null,
                $"Se encontro la raiz del repo ({raizDelMonorepo}) pero no "
                + $"'Movil{Path.DirectorySeparatorChar}mac-mac-mobile' dentro. Si la carpeta de la app "
                + "se renombro o se movio, hay que seguirla aqui: saltarse la prueba en "
                + "silencio es como se colaron diecisiete claves sin semilla.");
            return; // La app no esta clonada al lado. Ver el resumen de la clase.
        }

        var sembradas = ClavesSembradas();

        /*
          LAS CLAVES DE JORNADA NO SE SIEMBRAN AQUI, Y NO ES UN OLVIDO.

          `lib/modules/jornada/` es otro producto: otro servidor, otra base,
          otro catalogo de permisos. Sus claves (`app_movil.jornada.*`) se
          siembran en `Jornada/db/empresa/03_Permisos_Semilla.sql`, no en
          `PermissionCatalogSeeds`, y sembrarlas aqui las pondria en WPermiso de
          MAC —una casilla en el panel que no abre absolutamente nada—.

          Asi que se comparan contra SU semilla. Lo que NO se hace es saltarselas:
          una clave de Jornada que nadie siembre en ningun sitio sigue siendo el
          mismo fallo silencioso de siempre, y si el archivo de semillas de
          Jornada no aparece, esta prueba FALLA con la ruta en la que lo busco.
        */
        var sembradasEnJornada = ClavesSembradasEnJornada();
        var faltantes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var halladas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raiz in raices)
        {
            foreach (var archivo in Directory.EnumerateFiles(raiz, "*.dart", SearchOption.AllDirectories))
            {
                var texto = SinComentarios(File.ReadAllText(archivo));
                foreach (Match m in Regex.Matches(texto, @"'(app_movil[A-Za-z0-9_.]*)'"))
                {
                    var clave = m.Groups[1].Value;
                    halladas.Add(clave);
                    if (ClavesDeMosaico.Contains(clave)) continue;
                    if (sembradas.Contains(clave)) continue;
                    if (clave.StartsWith("app_movil.jornada.", StringComparison.OrdinalIgnoreCase)
                        && sembradasEnJornada.Contains(clave)) continue;
                    if (!faltantes.ContainsKey(clave))
                        faltantes[clave] = Path.GetFileName(archivo);
                }
            }
        }

        /*
          QUE LA PRUEBA NO PASE EN VERDE SIN HABER MEDIDO.

          Si la ruta a la app se rompe —un renombre de carpeta, otra estructura
          de repo— `EnumerateFiles` no encuentra nada y todo "pasa". Una prueba
          que mide cero y dice que esta bien es peor que no tenerla, que es
          justamente el error que esta clase vino a corregir.

          Diez es un suelo deliberadamente bajo: hoy son mas de veinte, y no se
          quiere que esto falle cada vez que la app deje de usar una clave.
        */
        Assert.True(halladas.Count >= 10,
            $"Se encontraron las carpetas de la app ({string.Join(", ", raices)}) pero solo "
            + $"{halladas.Count} claves `app_movil`. La prueba no esta midiendo lo que cree.");

        Assert.True(faltantes.Count == 0,
            "La app movil comprueba estas claves y PermissionCatalogSeeds no las siembra. "
            + "Una clave que no esta en WPermiso no puede concederse, asi que lo que gobierne "
            + "solo le funcionara al SuperAdmin: "
            + string.Join(", ", faltantes.Select(f => $"{f.Key} ({f.Value})")));
    }

    /// <summary>
    /// El codigo Dart sin sus comentarios.
    ///
    /// NOMBRAR UNA CLAVE EN UN COMENTARIO NO ES PEDIRLA.
    ///
    /// El regex de abajo busca literales `'app_movil...'` en el texto crudo, y
    /// el texto crudo incluye los comentarios. Este repo documenta MUCHO: cada
    /// pantalla explica arriba que claves usa y —lo que lo hizo evidente—
    /// `proveedores.dart` explica el bug de `app_movil.proveedores` nombrando la
    /// clave MALA. Con los comentarios dentro, escribir "antes pedia 'x'" basta
    /// para que esta prueba exija sembrar 'x': la prueba empieza a cobrar por la
    /// documentacion y el unico arreglo que acepta es borrar la explicacion.
    ///
    /// Se quitan `/* ... */` y `// ...` antes de buscar. No es un analizador de
    /// Dart y no hace falta que lo sea: lo unico que se puede confundir es una
    /// clave dentro de una CADENA que lleve `//` adentro, y una clave de permiso
    /// nunca los lleva.
    /// </summary>
    private static string SinComentarios(string dart)
    {
        var sinBloque = Regex.Replace(dart, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(sinBloque, @"//[^\n]*", " ");
    }

    /// <summary>Las carpetas de codigo de la app, si este repo las tiene al lado.</summary>
    private static List<string> CarpetasDeLaApp()
    {
        /*
          SE BUSCA SUBIENDO, NO CON UNA CUENTA DE ".." ESCRITA A MANO.

          Antes eran seis `..` con un comentario que decia
          "bin/Debug/net8.0 -> proyecto de pruebas -> ISL_Service -> sacNet-8 ->
          MAC". La cuenta estaba MAL: el repo tiene ISL_Service DOS veces
          (ISL_Service\ISL_Service), asi que seis saltos llegan a `sacNet-8` y no
          a `MAC`, y `sacNet-8\Movil\mac-mac-mobile` no existe.

          Resultado: `CarpetasDeLaApp` devolvia vacio y la prueba se saltaba
          SIEMPRE, en verde, sin medir nada. Es exactamente el fallo que su
          propio `Assert` de "al menos diez claves" venia a impedir — y no lo
          impedia, porque ese Assert queda despues del `return` de arriba.
          Medido: con la cuenta corregida aparecieron diecisiete claves que la
          app pide y nadie habia sembrado.

          Subiendo hasta encontrar la carpeta no hay cuenta que equivocar, y
          sigue funcionando si alguien mueve el proyecto de sitio.
        */
        var app = SubiendoHasta(Path.Combine("Movil", "mac-mac-mobile"));

        var carpetas = new List<string>();
        if (app is null) return carpetas; // La app no esta clonada al lado.

        foreach (var sub in new[]
                 {
                     Path.Combine(app, "lib"),
                     Path.Combine(app, "shared_mobile", "lib")
                 })
        {
            if (Directory.Exists(sub)) carpetas.Add(sub);
        }

        return carpetas;
    }

    /// <summary>
    /// La primera carpeta, subiendo desde el binario de las pruebas, que
    /// contenga <paramref name="relativa"/>. Devuelve la ruta completa de
    /// <paramref name="relativa"/>, o <c>null</c> si no aparece en ningun nivel.
    /// </summary>
    private static string? SubiendoHasta(string relativa)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null;
             dir = dir.Parent)
        {
            var candidata = Path.Combine(dir.FullName, relativa);
            if (Directory.Exists(candidata)) return candidata;
        }

        return null;
    }

    /// <summary>Las claves de <c>PermissionCatalogSeeds</c>, leidas por reflexion.</summary>
    private static HashSet<string> ClavesSembradas()
    {
        var campo = typeof(PermissionService).GetField(
                        "PermissionCatalogSeeds",
                        BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                    ?? throw new InvalidOperationException(
                        "No se encontro PermissionCatalogSeeds. Si se renombro, aqui hay que seguirlo.");

        var valor = campo.GetValue(null)
                    ?? throw new InvalidOperationException("PermissionCatalogSeeds vino nulo.");

        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var semilla in (System.Collections.IEnumerable)valor)
        {
            if (semilla is null) continue;
            var key = semilla.GetType().GetProperty("Key")?.GetValue(semilla) as string;
            if (!string.IsNullOrWhiteSpace(key)) claves.Add(key!);
        }

        Assert.True(claves.Count > 0, "PermissionCatalogSeeds no entrego ni una clave.");
        return claves;
    }

    /// Las claves `app_movil.*` que siembra el catalogo de JORNADA, leidas de
    /// su propio script de semillas.
    ///
    /// Si el script no esta, devuelve vacio y el aserto de arriba denuncia las
    /// claves como no sembradas, con el nombre del archivo en el mensaje. Es
    /// deliberado: un `return` silencioso aqui seria otra vez una prueba que
    /// aprueba sin mirar.
    private static HashSet<string> ClavesSembradasEnJornada()
    {
        var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var raiz = SubiendoHasta("sacNet-8");
        if (raiz is null) return claves;

        var semilla = Path.Combine(
            Path.GetDirectoryName(raiz)!, "Jornada", "db", "empresa", "03_Permisos_Semilla.sql");
        if (!File.Exists(semilla)) return claves;

        foreach (Match m in Regex.Matches(
                     File.ReadAllText(semilla),
                     @"jp_Permiso_Guardar\s+'(app_movil[A-Za-z0-9_.]*)'"))
        {
            claves.Add(m.Groups[1].Value);
        }

        return claves;
    }
}
