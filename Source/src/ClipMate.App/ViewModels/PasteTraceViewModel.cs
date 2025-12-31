using System.Collections.ObjectModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Paste Trace diagnostic dialog.
/// </summary>
public sealed partial class PasteTraceViewModel : ObservableObject, IDisposable
{
    private readonly IPasteTraceService _pasteTraceService;
    private bool _disposed;

    /// <summary>
    /// Gets whether tracing is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isTracing;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Ready to trace";

    /// <summary>
    /// Gets or sets the target application name.
    /// </summary>
    [ObservableProperty]
    private string? _targetApplication;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasteTraceViewModel" /> class.
    /// </summary>
    public PasteTraceViewModel(IPasteTraceService pasteTraceService)
    {
        _pasteTraceService = pasteTraceService ?? throw new ArgumentNullException(nameof(pasteTraceService));

        // Subscribe to events
        _pasteTraceService.TracingStateChanged += OnTracingStateChanged;
        _pasteTraceService.FormatRequested += OnFormatRequested;
    }

    /// <summary>
    /// Gets the collection of trace entries.
    /// </summary>
    public ObservableCollection<PasteTraceEntry> TraceEntries => _pasteTraceService.TraceEntries;

    public void Dispose()
    {
        if (_disposed)
            return;

        _pasteTraceService.TracingStateChanged -= OnTracingStateChanged;
        _pasteTraceService.FormatRequested -= OnFormatRequested;

        // Stop any active trace
        if (_pasteTraceService.IsTracing)
            _pasteTraceService.StopTrace();

        _disposed = true;
    }

    /// <summary>
    /// Starts the paste trace.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartTrace))]
    private void StartTrace()
    {
        // Use a new GUID for each trace session (the service traces current clipboard contents)
        var traceId = Guid.NewGuid();

        var success = _pasteTraceService.StartTrace(traceId);
        if (success)
            StatusMessage = "Tracing active - paste in target application";
        else
            StatusMessage = "Failed to start trace - copy something to clipboard first";
    }

    private bool CanStartTrace() => !IsTracing;

    /// <summary>
    /// Stops the paste trace.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopTrace))]
    private void StopTrace()
    {
        _pasteTraceService.StopTrace();
        StatusMessage = "Trace stopped - copy something to clipboard to trace again";
    }

    private bool CanStopTrace() => IsTracing;

    /// <summary>
    /// Clears all trace entries.
    /// </summary>
    [RelayCommand]
    private void ClearEntries() => _pasteTraceService.ClearEntries();

    /// <summary>
    /// Copies trace results to clipboard as text.
    /// </summary>
    [RelayCommand]
    private void CopyResults()
    {
        if (TraceEntries.Count == 0)
            return;

        var lines = new List<string>
        {
            "Paste Trace Results",
            $"Target Application: {TargetApplication ?? "Unknown"}",
            $"Formats Requested: {TraceEntries.Count}",
            string.Empty,
            "Time                 | Format ID | Format Name                    | Size",
            "---------------------+-----------+--------------------------------+----------",
        };

        foreach (var item in TraceEntries)
        {
            var size = item.DataSize?.ToString("N0") ?? "N/A";
            lines.Add($"{item.Timestamp:HH:mm:ss.fff}       | {item.FormatId,9} | {item.FormatName,-30} | {size}");
        }

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        StatusMessage = "Results copied to clipboard";
    }

    private void OnTracingStateChanged(object? sender, bool isTracing)
    {
        IsTracing = isTracing;
        StartTraceCommand.NotifyCanExecuteChanged();
        StopTraceCommand.NotifyCanExecuteChanged();
    }

    private void OnFormatRequested(object? sender, PasteTraceEntry entry)
    {
        TargetApplication = _pasteTraceService.TargetApplication;
        StatusMessage = $"Format requested: {entry.FormatName}";
    }
}
