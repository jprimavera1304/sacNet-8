using ClosedXML.Excel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ISL_Service.Infrastructure.Reports;

public sealed class ReportEngineWarmupHostedService : IHostedService
{
    private readonly ILogger<ReportEngineWarmupHostedService> _logger;

    public ReportEngineWarmupHostedService(ILogger<ReportEngineWarmupHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                WarmupPdfEngine();
                WarmupExcelEngine();
                _logger.LogInformation("Report engine warm-up completed.");
            }
            catch (OperationCanceledException)
            {
                // App is shutting down.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Report engine warm-up failed.");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void WarmupPdfEngine()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _ = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text("Warm-up PDF").FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static void WarmupExcelEngine()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("warmup");
        ws.Cell("A1").Value = "Warm-up XLSX";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
    }
}
