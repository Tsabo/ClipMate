using System.Collections.ObjectModel;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services;

/// <summary>
/// Interface for the in-memory event log sink that captures log events for UI display.
/// </summary>
public interface IEventLogSink
{
    /// <summary>
    /// Gets the collection of diagnostic events.
    /// </summary>
    ObservableCollection<DiagnosticEvent> Events { get; }

    /// <summary>
    /// Gets the maximum number of events to retain.
    /// </summary>
    int MaxEvents { get; }

    /// <summary>
    /// Adds a diagnostic event to the sink.
    /// </summary>
    /// <param name="diagnosticEvent">The event to add.</param>
    void Add(DiagnosticEvent diagnosticEvent);

    /// <summary>
    /// Clears all events from the sink.
    /// </summary>
    void Clear();
}
