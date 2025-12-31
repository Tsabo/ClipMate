using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Event Log diagnostic dialog.
/// </summary>
public sealed partial class EventLogViewModel : ObservableObject
{
    private readonly IEventLogSink _eventLogSink;

    /// <summary>
    /// Gets or sets whether to auto-scroll to newest events.
    /// </summary>
    [ObservableProperty]
    private bool _autoScroll = true;

    /// <summary>
    /// Gets or sets the filter text for searching events.
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// Gets or sets the selected log level filter.
    /// </summary>
    [ObservableProperty]
    private LogLevel _selectedLevel = LogLevel.Debug;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogViewModel" /> class.
    /// </summary>
    /// <param name="eventLogSink">The event log sink containing events.</param>
    public EventLogViewModel(IEventLogSink eventLogSink)
    {
        _eventLogSink = eventLogSink ?? throw new ArgumentNullException(nameof(eventLogSink));

        // Create a filtered view of the events
        FilteredEvents = CollectionViewSource.GetDefaultView(eventLogSink.Events);
        FilteredEvents.Filter = FilterEvent;

        // Refresh filter when collection changes
        eventLogSink.Events.CollectionChanged += OnEventsCollectionChanged;
    }

    /// <summary>
    /// Gets the filtered view of diagnostic events to display.
    /// </summary>
    public ICollectionView FilteredEvents { get; }

    /// <summary>
    /// Gets the raw events collection for count display.
    /// </summary>
    public ObservableCollection<DiagnosticEvent> Events => _eventLogSink.Events;

    /// <summary>
    /// Gets the available log levels for filtering.
    /// </summary>
    public IReadOnlyList<LogLevel> AvailableLogLevels { get; } =
    [
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    ];

    /// <summary>
    /// Clears all events from the log.
    /// </summary>
    [RelayCommand]
    private void ClearLog() => _eventLogSink.Clear();

    /// <summary>
    /// Copies selected events to clipboard as formatted text.
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard(IEnumerable<DiagnosticEvent>? selectedEvents)
    {
        if (selectedEvents == null)
            return;

        var lines = selectedEvents.Select(p => p.DisplayText);
        var text = string.Join(Environment.NewLine, lines);

        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    /// <summary>
    /// Filter predicate for events based on level and search text.
    /// </summary>
    private bool FilterEvent(object obj)
    {
        if (obj is not DiagnosticEvent evt)
            return false;

        // Level filter
        if (evt.Level < SelectedLevel)
            return false;

        // Text filter
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        var searchText = FilterText.Trim();
        return evt.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               evt.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // No need to refresh on add - the view handles it automatically
    }

    partial void OnSelectedLevelChanged(LogLevel value) => FilteredEvents.Refresh();

    partial void OnFilterTextChanged(string value) => FilteredEvents.Refresh();
}
