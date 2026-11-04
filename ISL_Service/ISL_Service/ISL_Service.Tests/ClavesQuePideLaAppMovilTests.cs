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
        var app = string.Empty;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null;
             dir = dir.Parent)
        {
            var candidata = Path.Combine(dir.FullName, "Movil", "mac-mac-mobile");
            if (Directory.Exists(candidata))
            {
                app = candidata;
                break;
            }
        }

        var carpetas = new List<string>();
        if (app.Length == 0) return carpetas; // La app no esta al lado de este repo.

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
