namespace ClipMate.Core.Events;

/// <summary>
/// Event sent when the auto capture (clipboard monitoring) state changes.
/// </summary>
/// <param name="IsMonitoring">True if clipboard monitoring is now active, false if stopped.</param>
public record AutoCaptureStateChangedEvent(bool IsMonitoring);
