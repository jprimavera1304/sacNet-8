using System.Diagnostics;
using System.Text;

namespace ISL_Service.Infrastructure.Reports;

public static class WkhtmltopdfHtmlPdfRenderer
{
    private const string WkhtmltopdfPath = "Assets/Wkhtmltopdf/wkhtmltopdf.exe";

    /*
      EL TITULO ES LO QUE SE LEE EN LA PESTAÑA DEL NAVEGADOR

      Sin el, el visor del navegador no tiene de donde sacar un nombre y usa el
      ultimo pedazo de la direccion: la pestaña decia "pdf" —literalmente— con
      el icono generico de documento. No es que faltara el <title> en el HTML;
      wkhtmltopdf ignora el del documento salvo que se le pase --title, y las
      plantillas de Template_Html no traen cabecera.

      Es lo UNICO que se puede escribir en la pestaña de un PDF: el icono lo
      decide el navegador y no se puede tocar desde el documento.
    */
    /*
      LAS OPCIONES SON OPCIONALES Y NO CAMBIAN LO DE ANTES

      Los margenes de aqui abajo (4/6/2/8) son los de la remision y NO se
      tocan: ese papel tiene que salir identico al de Mac31. Pero son suyos, no
      universales — el margen derecho de 8 mm contra 2 del izquierdo esta
      pensado para la remision, y en un reporte de siete columnas descentra la
      tabla.

      Con esto, un reporte nuevo puede pedir los suyos sin que nadie mas se
      entere. Quien no pase nada sigue obteniendo exactamente lo de siempre.
    */
    public sealed class Opciones
    {
        /// En milimetros. Null = el de siempre.
        public int? MargenSuperior { get; init; }
        public int? MargenInferior { get; init; }
        public int? MargenIzquierdo { get; init; }
        public int? MargenDerecho { get; init; }

        /*
          A CUANTOS PUNTOS POR PULGADA SE REMUESTREAN LAS IMAGENES.

          wkhtmltopdf reduce cada imagen al tamaño que ocupa en la hoja por este
          valor, y el suyo por omision (600) deja un logo de 3015 px de ancho en
          520. A ese tamaño los trazos finos y los contornos blancos del logo se
          promedian con el fondo y la marca se ve lavada, como si fuera
          transparente. Subiendolo, el visor tiene mas pixeles de donde tirar.

          No se sube para todos: son mas bytes por documento, y a la remision
          —que lleva su logo a un tamaño mayor— no le hace falta.
        */
        public int? ImagenDpi { get; init; }

        /*
          EL PIE DE PAGINA DE VERDAD, EL DE LA FRANJA DE LA HOJA

          No es lo mismo que un parrafo al final del cuerpo, y la diferencia se
          nota justo cuando el documento crece: un parrafo con su margen encima
          puede no caber en lo que queda de hoja y arrastrar una pagina entera
          en blanco solo para enseñar una linea de texto. Paso exactamente eso.

          Aqui el pie vive FUERA del flujo, en el margen inferior que el propio
          wkhtmltopdf reserva. No empuja nada, no puede desbordar, y sale en
          TODAS las hojas — que es lo que se quiere de un "impreso el" y de un
          numero de pagina.

          En el texto se pueden usar las marcas de wkhtmltopdf: [page] es el
          numero de hoja y [topage] el total.
        */
        public string? PieIzquierdo { get; init; }
        public string? PieDerecho { get; init; }

        /// Una raya encima del pie, como la del diseño.
        public bool PieConLinea { get; init; }
    }

    public static async Task<byte[]> RenderAsync(
        string html,
        string? orientation,
        string? titulo = null,
        CancellationToken ct = default,
        Opciones? opciones = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new InvalidOperationException("No hay HTML para generar el PDF.");

        var executablePath = Path.Combine(AppContext.BaseDirectory, WkhtmltopdfPath);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("No se encontro wkhtmltopdf para generar el PDF.", executablePath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "isl-reportes");
        Directory.CreateDirectory(tempRoot);

        var token = Guid.NewGuid().ToString("N");
        var htmlPath = Path.Combine(tempRoot, $"{token}.html");
        var pdfPath = Path.Combine(tempRoot, $"{token}.pdf");

        await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(false), ct);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
            };

            startInfo.ArgumentList.Add("--quiet");

            if (!string.IsNullOrWhiteSpace(titulo))
            {
                startInfo.ArgumentList.Add("--title");
                startInfo.ArgumentList.Add(titulo);
            }

            startInfo.ArgumentList.Add("--enable-local-file-access");
            startInfo.ArgumentList.Add("--orientation");
            startInfo.ArgumentList.Add(NormalizeOrientation(orientation));
            startInfo.ArgumentList.Add("--margin-top");
            startInfo.ArgumentList.Add((opciones?.MargenSuperior ?? 4).ToString());
            startInfo.ArgumentList.Add("--margin-bottom");
            startInfo.ArgumentList.Add((opciones?.MargenInferior ?? 6).ToString());
            startInfo.ArgumentList.Add("--margin-left");
            startInfo.ArgumentList.Add((opciones?.MargenIzquierdo ?? 2).ToString());
            startInfo.ArgumentList.Add("--margin-right");
            startInfo.ArgumentList.Add((opciones?.MargenDerecho ?? 8).ToString());

            if (!string.IsNullOrWhiteSpace(opciones?.PieIzquierdo))
            {
                startInfo.ArgumentList.Add("--footer-left");
                startInfo.ArgumentList.Add(opciones!.PieIzquierdo!);
            }

            if (!string.IsNullOrWhiteSpace(opciones?.PieDerecho))
            {
                startInfo.ArgumentList.Add("--footer-right");
                startInfo.ArgumentList.Add(opciones!.PieDerecho!);
            }

            if (opciones?.PieIzquierdo != null || opciones?.PieDerecho != null)
            {
                /* Chico y separado del contenido: el pie acompaña, no compite
                   con lo que se vino a leer. */
                startInfo.ArgumentList.Add("--footer-font-size");
                startInfo.ArgumentList.Add("7");
                startInfo.ArgumentList.Add("--footer-spacing");
                startInfo.ArgumentList.Add("5");

                if (opciones.PieConLinea)
                    startInfo.ArgumentList.Add("--footer-line");
            }

            if (opciones?.ImagenDpi is int dpi)
            {
                startInfo.ArgumentList.Add("--image-dpi");
                startInfo.ArgumentList.Add(dpi.ToString());
                /* Sin recomprimir: el logo es una marca, y los artefactos de
                   JPEG se le notan justo en los bordes. */
                startInfo.ArgumentList.Add("--image-quality");
                startInfo.ArgumentList.Add("100");
            }
            startInfo.ArgumentList.Add(htmlPath);
            startInfo.ArgumentList.Add(pdfPath);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar wkhtmltopdf.");

            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stderr = await stderrTask;

            if (process.ExitCode != 0 || !File.Exists(pdfPath))
                throw new InvalidOperationException($"No se pudo generar el PDF. {stderr}".Trim());

            return await File.ReadAllBytesAsync(pdfPath, ct);
        }
        finally
        {
            TryDelete(htmlPath);
            TryDelete(pdfPath);
        }
    }

    private static string NormalizeOrientation(string? orientation)
    {
        return string.Equals(orientation, "portrait", StringComparison.OrdinalIgnoreCase)
            ? "Portrait"
            : "Landscape";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup for temp report files.
        }
    }
}
