using System.Runtime.CompilerServices;

namespace ISL_Service.Tests;

/// <summary>
/// NINGUN REPOSITORIO LLAMA AL PROCEDIMIENTO QUE CIERRA EL DIA DEL NEGOCIO.
///
/// `sp_n_ConsultaConstantes` no es una consulta: cuando la fecha de operacion
/// esta atrasada apunta `FechaOperacionLog`, borra y rehace
/// `AlmacenProductoHist` y adelanta `Constantes.FechaOperacion`. Y el `IF` de
/// `@ValidaActividad` **solo** envuelve el bloque de actividad del usuario, asi
/// que pasar 0 no evita nada: basta con que la fecha este atrasada.
///
/// Habia SEIS sitios que lo llamaban nada mas para leer -el IVA, la ruta de un
/// logo, la fecha formateada-, los seis con `@ValidaActividad = 0` y un
/// comentario que daba por hecho que asi se portaba como consulta. La primera
/// peticion de la manana cerraba el dia.
///
/// Ahora se lee con `sp_w_ConsultaConstantesSoloLectura`, el mismo
/// procedimiento sin las escrituras (`Querrys/31 Constantes Solo Lectura/`).
/// Medido en MacZ dentro de una transaccion con rollback, con la fecha atrasada
/// un dia: el viejo la adelanto y dejo un renglon en `FechaOperacionLog`; el
/// nuevo dejo la fecha quieta y cero renglones. Las 250 columnas del resultado
/// coinciden en nombre, tipo y orden.
///
/// Esta prueba mira el fuente porque el defecto no esta en la logica sino en
/// QUE PROCEDIMIENTO se llama: un doble de base de datos la dejaria pasar con
/// la bomba dentro.
/// </summary>
public class NadieCierraElDiaAlLeerTests
{
    [Fact]
    public void Ningun_Repositorio_Llama_Al_Procedimiento_Que_Cierra_El_Dia()
    {
        var culpables = new List<string>();

        foreach (var archivo in Directory.GetFiles(RaizDelProyecto(), "*.cs", SearchOption.AllDirectories))
        {
            /* Las pruebas si pueden nombrarlo: lo nombran para prohibirlo. */
            if (archivo.Contains("ISL_Service.Tests", StringComparison.OrdinalIgnoreCase)) continue;

            /*
              CancelacionSegura tambien lo nombra para prohibir, no para llamar.

              Ese archivo decide a que procedimientos se les deja llegar la
              cancelacion: a las consultas si, a las escrituras no. La decision
              la toma por el nombre, y `sp_n_ConsultaConstantes` se llama
              "Consulta" y sin embargo escribe -siete sentencias, contadas en la
              base-, asi que esta en su lista de excepciones. Si se quitara de
              ahi, una cancelacion podria abortarlo a media transaccion.

              Se exceptua SOLO este archivo. Cualquier otro que escriba el
              nombre entre comillas sigue reventando la prueba, que es el punto.
            */
            if (Path.GetFileName(archivo).Equals("CancelacionSegura.cs", StringComparison.OrdinalIgnoreCase)) continue;

            var texto = File.ReadAllText(archivo);
            /*
              Se busca entre comillas, que es como se ejecuta. Los comentarios
              que EXPLICAN la trampa son justo lo que hay que conservar: hay
              una treintena y son la documentacion de este agujero.
            */
            if (texto.Contains("\"sp_n_ConsultaConstantes\"", StringComparison.Ordinal))
                culpables.Add(Path.GetFileName(archivo));
        }

        Assert.Empty(culpables);
    }

    [Fact]
    public void El_Gemelo_De_Solo_Lectura_Es_El_Que_Se_Usa()
    {
        var hay = Directory
            .GetFiles(Path.Combine(RaizDelProyecto(), "Infrastructure", "Repositories"), "*.cs")
            .Any(a => File.ReadAllText(a).Contains("\"sp_w_ConsultaConstantesSoloLectura\"", StringComparison.Ordinal));

        Assert.True(hay, "Nadie lee las constantes con el gemelo de solo lectura.");
    }

    private static string RaizDelProyecto([CallerFilePath] string estaPrueba = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(estaPrueba)!);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ISL_Service.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro la raiz del proyecto ISL_Service.");
    }
}
