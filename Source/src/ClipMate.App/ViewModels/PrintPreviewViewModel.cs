using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services.Printing;
using CommunityToolkit.Mvvm.ComponentModel;
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

public partial class PrintPreviewViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IPrintService _printService;

    [ObservableProperty]
    private XtraReport? _report;

    public PrintPreviewViewModel(IPrintService printService, ILogger<PrintPreviewViewModel> logger)
    {
        _printService = printService;
        _logger = logger;
    }

    public async Task SetClips(List<Guid> clipIds, string databaseKey, PrintConfiguration config)
    {
        try
        {
            // Load clip data asynchronously
            var clipData = await _printService.LoadClipDataForPrintingAsync(clipIds, databaseKey);

            Report = new ClipTextReportVisual();
            Report.DataSource = clipData;
            Report.Parameters["PrintHeader"].Value = config.PrintHeader;
            Report.Parameters["HeaderText"].Value = config.HeaderText;
            Report.Parameters["PrintFooter"].Value = config.PrintFooter;
            Report.Parameters["PrintDetails"].Value = config.PrintDetails;
            Report.Parameters["SingleItemPerPage"].Value = config.SingleItemPerPage;

            Report.ReportUnit = config.MarginUnit == MarginUnit.Inches
                ? ReportUnit.Inches
                : ReportUnit.Millimeters;

            Report.Margins.Left = (float)config.LeftMargin;
            Report.Margins.Bottom = (float)config.BottomMargin;
            Report.Margins.Right = (float)config.RightMargin;
            Report.Margins.Top = (float)config.TopMargin;

            Report.PrintingSystem.Document.AutoFitToPagesWidth = 1;

            _logger.LogInformation("Print preview loaded successfully with {Count} clips", clipData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load print preview");
        }
    }
}
