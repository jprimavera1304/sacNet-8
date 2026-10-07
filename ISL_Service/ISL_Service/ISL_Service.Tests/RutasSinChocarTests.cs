using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ISL_Service.Tests;

/// <summary>
/// Que no haya DOS acciones con el mismo verbo y la misma ruta.
///
/// POR QUE ESTA PRUEBA EXISTE
/// --------------------------
/// Porque ya paso, y paso de la peor forma. Al juntar la ola 2 con la ola 4
/// quedaron dos acciones en `POST api/ventas/descuentos/detalle`:
/// `VentasDescuentosConsultaController.Detalle` (la consulta, migrada y comparada
/// en la ola 2) y `VentasDescuentosController.CrearDetalleAsync` (el alta, de la
/// ola 4).
///
/// Lo que hace ASP.NET con eso: NO avisa al arrancar. Avisa en la primera
/// peticion a esa ruta, con AmbiguousMatchException, que sale como 500 con
/// Content-Length 0 — sin mensaje, sin codigo, sin nada que leer en la respuesta.
/// Y no se cae la accion nueva: se caen LAS DOS. Los dos casos del banco que
/// cubrian la consulta pasaron de OK a 500 sin que nadie tocara su codigo, y la
/// colision no existe en ninguna de las dos ramas por separado: aparece solo al
/// juntarlas, que es justo donde nadie estaba mirando.
///
/// Una prueba de arranque no lo habria visto. Esta si, y cuesta milisegundos.
/// </summary>
public class RutasSinChocarTests
{
    [Fact]
    public void Ninguna_Ruta_Esta_Declarada_Dos_Veces_Con_El_Mismo_Verbo()
    {
        var controladores = typeof(ISL_Service.Controllers.SesionController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var vistas = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tipo in controladores)
        {
            var prefijo = tipo.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(a => a.Template)
                .FirstOrDefault() ?? string.Empty;

            var acciones = tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var metodo in acciones)
            {
                foreach (var verboAttr in metodo.GetCustomAttributes().OfType<HttpMethodAttribute>())
                {
                    foreach (var verbo in verboAttr.HttpMethods)
                    {
                        var plantilla = Combinar(prefijo, verboAttr.Template);
                        var clave = verbo.ToUpperInvariant() + " " + Normalizar(plantilla);
                        if (!vistas.TryGetValue(clave, out var quienes))
                            vistas[clave] = quienes = new List<string>();
                        quienes.Add(tipo.Name + "." + metodo.Name);
                    }
                }
            }
        }

        var choques = vistas
            .Where(p => p.Value.Count > 1)
            // `api/[controller]` lo sustituye el runtime con el nombre del
            // controlador, asi que dos plantillas con el token NO chocan.
            .Where(p => !p.Key.Contains("[controller]", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Key + "  ->  " + string.Join(", ", p.Value))
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            choques.Count == 0,
            "Hay rutas declaradas dos veces. ASP.NET contesta 500 con cuerpo vacio en la "
            + "primera peticion y tumba LAS DOS acciones, no solo la nueva:\n  "
            + string.Join("\n  ", choques));
    }

    private static string Combinar(string prefijo, string? sufijo)
    {
        if (string.IsNullOrEmpty(sufijo)) return prefijo;
        if (sufijo.StartsWith('/') || sufijo.StartsWith('~')) return sufijo.TrimStart('~', '/');
        if (string.IsNullOrEmpty(prefijo)) return sufijo;
        return prefijo.TrimEnd('/') + "/" + sufijo.TrimStart('/');
    }

    /// <summary>
    /// El NOMBRE del parametro no cuenta para el ruteo: `{id:int}` y
    /// `{idVenta:int}` son la misma plantilla y chocan igual. Se deja solo la
    /// restriccion.
    /// </summary>
    private static string Normalizar(string plantilla)
        => Regex.Replace(plantilla.Trim('/'), @"\{[^:}]*(:[^}]*)?\}", m => "{" + m.Groups[1].Value + "}");
}
