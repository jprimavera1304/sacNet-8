using System.Diagnostics;
using System.Text;

namespace ISL_Service.Infrastructure.Reports;

public static class WkhtmltopdfHtmlPdfRenderer
{
    private const string WkhtmltopdfPath = "Assets/Wkhtmltopdf/wkhtmltopdf.exe";

    public static async Task<byte[]> RenderAsync(string html, string? orientation, CancellationToken ct = default)
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
            startInfo.ArgumentList.Add("--enable-local-file-access");
            startInfo.ArgumentList.Add("--orientation");
            startInfo.ArgumentList.Add(NormalizeOrientation(orientation));
            startInfo.ArgumentList.Add("--margin-top");
            startInfo.ArgumentList.Add("4");
            startInfo.ArgumentList.Add("--margin-bottom");
            startInfo.ArgumentList.Add("6");
            startInfo.ArgumentList.Add("--margin-left");
            startInfo.ArgumentList.Add("2");
            startInfo.ArgumentList.Add("--margin-right");
            startInfo.ArgumentList.Add("8");
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
