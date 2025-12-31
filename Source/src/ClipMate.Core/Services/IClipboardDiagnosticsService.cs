using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for retrieving diagnostic information about the system clipboard.
/// Provides a testable abstraction over Win32 clipboard diagnostic APIs.
/// </summary>
public interface IClipboardDiagnosticsService
{
    /// <summary>
    /// Gets comprehensive diagnostic information about the current clipboard state.
    /// </summary>
    /// <returns>Diagnostic information including owner, sequence number, and formats.</returns>
    ClipboardDiagnosticInfo GetDiagnostics();

    /// <summary>
    /// Gets the name of the process that currently owns the clipboard.
    /// </summary>
    /// <returns>The process name, or "(No owner)" if no process owns the clipboard.</returns>
    string GetOwnerProcessName();

    /// <summary>
    /// Gets the current clipboard sequence number.
    /// </summary>
    /// <returns>The sequence number, which changes each time the clipboard content changes.</returns>
    uint GetSequenceNumber();

    /// <summary>
    /// Gets the name of a clipboard format from its format code.
    /// </summary>
    /// <param name="formatCode">The clipboard format code.</param>
    /// <returns>The format name, or a fallback string if the format is unknown.</returns>
    string GetFormatName(uint formatCode);
}
