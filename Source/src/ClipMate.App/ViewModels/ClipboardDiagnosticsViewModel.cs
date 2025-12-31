using System.Collections.ObjectModel;
using System.Text;
using ClipMate.App.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Clipboard Diagnostics dialog.
/// </summary>
public sealed partial class ClipboardDiagnosticsViewModel : ObservableObject
{
    private readonly IClipboardDiagnosticsService _diagnosticsService;
    private readonly ILogger<ClipboardDiagnosticsViewModel> _logger;

    /// <summary>
    /// Gets or sets the clipboard owner process name.
    /// </summary>
    [ObservableProperty]
    private string _clipboardOwner = "Unknown";

    /// <summary>
    /// Gets or sets the last refresh time.
    /// </summary>
    [ObservableProperty]
    private DateTime _lastRefreshTime;

    /// <summary>
    /// Gets or sets the clipboard sequence number.
    /// </summary>
    [ObservableProperty]
    private uint _sequenceNumber;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardDiagnosticsViewModel" /> class.
    /// </summary>
    /// <param name="diagnosticsService">The clipboard diagnostics service.</param>
    /// <param name="logger">The logger instance.</param>
    public ClipboardDiagnosticsViewModel(IClipboardDiagnosticsService diagnosticsService,
        ILogger<ClipboardDiagnosticsViewModel> logger)
    {
        _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the clipboard formats currently on the clipboard.
    /// </summary>
    public ObservableCollection<ClipboardFormatEntry> Formats { get; } = [];

    /// <summary>
    /// Refreshes the clipboard state.
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        try
        {
            Formats.Clear();
            LastRefreshTime = DateTime.Now;

            var diagnostics = _diagnosticsService.GetDiagnostics();

            ClipboardOwner = diagnostics.OwnerProcessName;
            SequenceNumber = diagnostics.SequenceNumber;

            foreach (var item in diagnostics.Formats)
            {
                Formats.Add(new ClipboardFormatEntry
                {
                    FormatId = item.FormatId,
                    FormatName = item.FormatName,
                    DataSize = item.DataSize,
                });
            }

            StatusMessage = $"Found {Formats.Count} format(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh clipboard diagnostics");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Copies diagnostic information to clipboard as text.
    /// </summary>
    [RelayCommand]
    private void CopyDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ClipMate Clipboard Diagnostics");
        sb.AppendLine("==============================");
        sb.AppendLine($"Time: {LastRefreshTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Owner: {ClipboardOwner}");
        sb.AppendLine($"Sequence: {SequenceNumber}");
        sb.AppendLine($"Formats: {Formats.Count}");
        sb.AppendLine();
        sb.AppendLine("Format ID | Format Name                              | Size");
        sb.AppendLine("----------+------------------------------------------+-----------");

        foreach (var item in Formats)
            sb.AppendLine($"{item.FormatId,9} | {item.FormatName,-40} | {item.SizeDisplay}");

        Clipboard.SetText(sb.ToString());
        StatusMessage = "Diagnostics copied to clipboard";
    }
}
