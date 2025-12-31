namespace ClipMate.Core.Models;

/// <summary>
/// Represents diagnostic information about the current clipboard state.
/// </summary>
/// <param name="OwnerProcessName">The name of the process that owns the clipboard.</param>
/// <param name="SequenceNumber">The clipboard sequence number (changes on each clipboard update).</param>
/// <param name="Formats">The list of formats currently on the clipboard.</param>
public record ClipboardDiagnosticInfo(
    string OwnerProcessName,
    uint SequenceNumber,
    IReadOnlyList<ClipboardFormatDiagnostic> Formats);
