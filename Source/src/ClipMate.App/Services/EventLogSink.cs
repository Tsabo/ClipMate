using System.Collections.ObjectModel;
using System.Windows.Threading;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using WpfApplication = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// In-memory sink that captures log events for display in the Event Log dialog.
/// Thread-safe and marshals updates to the UI thread.
/// </summary>
public class EventLogSink : IEventLogSink
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

    /// <inheritdoc />
    public ObservableCollection<DiagnosticEvent> Events { get; }

    /// <inheritdoc />
    public int MaxEvents { get; }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
