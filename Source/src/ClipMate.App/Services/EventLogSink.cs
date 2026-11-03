using System.Collections.ObjectModel;
using System.Windows.Threading;
using ClipMate.Core.ValueObjects;
using WpfApplication = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// In-memory sink that captures log events for display in the Event Log dialog.
/// Thread-safe and marshals updates to the UI thread.
/// </summary>
public class EventLogSink
{
    private const int _defaultMaxEvents = 500;
    private readonly Dispatcher? _dispatcher;
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogSink" /> class.
    /// </summary>
    public EventLogSink()
    {
        Events = [];
        MaxEvents = _defaultMaxEvents;

        // Capture the dispatcher if running in a WPF context
        _dispatcher = WpfApplication.Current?.Dispatcher;
    }

    /// <summary>
    /// Gets the collection of diagnostic events.
    /// </summary>
    public ObservableCollection<DiagnosticEvent> Events { get; }

    /// <summary>
    /// Gets the maximum number of events to retain.
    /// </summary>
    public int MaxEvents { get; }

    /// <summary>
    /// Adds a diagnostic event to the sink.
    /// </summary>
    /// <param name="diagnosticEvent">The event to add.</param>
    public void Add(DiagnosticEvent diagnosticEvent)
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
        {
            // Marshal to UI thread
            _dispatcher.BeginInvoke(() => AddInternal(diagnosticEvent));
        }
        else
            AddInternal(diagnosticEvent);
    }

    /// <summary>
    /// Clears all events from the sink.
    /// </summary>
    public void Clear()
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
            _dispatcher.BeginInvoke(ClearInternal);
        else
            ClearInternal();
    }

    private void AddInternal(DiagnosticEvent diagnosticEvent)
    {
        lock (_lock)
        {
            // Remove oldest events if at capacity
            while (Events.Count >= MaxEvents)
                Events.RemoveAt(0);

            Events.Add(diagnosticEvent);
        }
    }

    private void ClearInternal()
    {
        lock (_lock)
        {
            Events.Clear();
        }
    }
}
