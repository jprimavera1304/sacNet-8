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
        // El prefijo del canal, que se usa suelto en comparaciones y filtros.
        "app_movil"
    };

    [Fact]
    public void Cada_Clave_Que_Pide_La_App_Esta_Sembrada()
    {
        var raices = CarpetasDeLaApp();
        if (raices.Count == 0)
            return; // La app no esta al lado de este repo. Ver el resumen.

        var sembradas = ClavesSembradas();
        var faltantes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var halladas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raiz in raices)
        {
            foreach (var archivo in Directory.EnumerateFiles(raiz, "*.dart", SearchOption.AllDirectories))
            {
                var texto = File.ReadAllText(archivo);
                foreach (Match m in Regex.Matches(texto, @"'(app_movil[A-Za-z0-9_.]*)'"))
                {
                    var clave = m.Groups[1].Value;
                    halladas.Add(clave);
                    if (ClavesDeMosaico.Contains(clave)) continue;
                    if (sembradas.Contains(clave)) continue;
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

    /// <summary>Las carpetas de codigo de la app, si este repo las tiene al lado.</summary>
    private static List<string> CarpetasDeLaApp()
    {
        // bin/Debug/net8.0 -> proyecto de pruebas -> ISL_Service -> sacNet-8 -> MAC
        var mac = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", ".."));
        var app = Path.Combine(mac, "Movil", "mac-mac-mobile");

        var carpetas = new List<string>();
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
}
