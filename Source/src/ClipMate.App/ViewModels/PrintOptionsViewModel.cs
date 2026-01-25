using System.Collections.ObjectModel;
using System.Drawing.Printing;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Print Options.
/// </summary>
public sealed partial class PrintOptionsViewModel : ObservableObject
{
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private double _bottomMargin;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private double _leftMargin;

    [ObservableProperty]
    private bool _marginsInInches;

    [ObservableProperty]
    private bool _marginsInMillimeters;

    [ObservableProperty]
    private bool _printDetails;

    [ObservableProperty]
    private bool _printFooter;

    [ObservableProperty]
    private bool _printHeader;

    [ObservableProperty]
    private bool _quickPrintEnabled;

    [ObservableProperty]
    private double _rightMargin;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private bool _singleItemPerPage;

    [ObservableProperty]
    private double _topMargin;

    public PrintOptionsViewModel(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        LoadAvailablePrinters();
        MarginsInInches = true;
    }

    public ObservableCollection<string> AvailablePrinters { get; } = [];

    public void LoadAsync() => LoadConfiguration(_configurationService.Configuration.Print);

    public void SaveAsync() => _configurationService.Configuration.Print = SaveConfiguration();

    private void LoadConfiguration(PrintConfiguration config)
    {
        SelectedPrinter = config.SelectedPrinter;
        PrintHeader = config.PrintHeader;
        HeaderText = config.HeaderText;
        PrintFooter = config.PrintFooter;
        PrintDetails = config.PrintDetails;
        QuickPrintEnabled = config.QuickPrintEnabled;
        SingleItemPerPage = config.SingleItemPerPage;
        MarginsInInches = config.MarginUnit == MarginUnit.Inches;
        MarginsInMillimeters = config.MarginUnit == MarginUnit.Millimeters;
        LeftMargin = config.LeftMargin;
        RightMargin = config.RightMargin;
        TopMargin = config.TopMargin;
        BottomMargin = config.BottomMargin;
    }

    private PrintConfiguration SaveConfiguration()
    {
        return new PrintConfiguration
        {
            SelectedPrinter = SelectedPrinter,
            PrintHeader = PrintHeader,
            HeaderText = HeaderText,
            PrintFooter = PrintFooter,
            PrintDetails = PrintDetails,
            QuickPrintEnabled = QuickPrintEnabled,
            SingleItemPerPage = SingleItemPerPage,
            MarginUnit = MarginsInInches
                ? MarginUnit.Inches
                : MarginUnit.Millimeters,
            LeftMargin = LeftMargin,
            RightMargin = RightMargin,
            TopMargin = TopMargin,
            BottomMargin = BottomMargin,
        };
    }

    private void LoadAvailablePrinters()
    {
        try
        {
            AvailablePrinters.Add("(Default Printer)");
            var printers = PrinterSettings.InstalledPrinters;
            foreach (string printer in printers)
                AvailablePrinters.Add(printer);

            if (string.IsNullOrEmpty(SelectedPrinter))
                SelectedPrinter = AvailablePrinters.FirstOrDefault();
        }
        catch
        {
            AvailablePrinters.Add("(Default Printer)");
            SelectedPrinter = "(Default Printer)";
        }
    }

    partial void OnMarginsInInchesChanged(bool value)
    {
        if (value)
            MarginsInMillimeters = false;
    }

    partial void OnMarginsInMillimetersChanged(bool value)
    {
        if (value)
            MarginsInInches = false;
    }
}
