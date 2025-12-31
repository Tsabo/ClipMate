using System.Collections.ObjectModel;
using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for tracing paste operations using delayed rendering.
/// </summary>
public interface IPasteTraceService
{
    /// <summary>
    /// Gets the collection of trace entries from the current trace session.
    /// </summary>
    ObservableCollection<PasteTraceEntry> TraceEntries { get; }

    /// <summary>
    /// Gets whether a trace is currently active.
    /// </summary>
    bool IsTracing { get; }

    /// <summary>
    /// Gets the target application name for the current trace, if any.
    /// </summary>
    string? TargetApplication { get; }

    /// <summary>
    /// Starts a new paste trace session with the specified clip.
    /// Registers delayed rendering for all formats in the clip.
    /// </summary>
    /// <param name="clipId">The ID of the clip to trace.</param>
    /// <returns>True if trace started successfully.</returns>
    bool StartTrace(Guid clipId);

    /// <summary>
    /// Stops the current trace session and restores normal clipboard state.
    /// </summary>
    void StopTrace();

    /// <summary>
    /// Clears all trace entries.
    /// </summary>
    void ClearEntries();

    /// <summary>
    /// Event raised when tracing state changes.
    /// </summary>
    event EventHandler<bool>? TracingStateChanged;

    /// <summary>
    /// Event raised when a format is requested.
    /// </summary>
    event EventHandler<PasteTraceEntry>? FormatRequested;
}
