using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for appending multiple clips together into a single clip.
/// </summary>
public interface IClipAppendService
{
    /// <summary>
    /// Appends multiple clips together into a single clip with a separator.
    /// </summary>
    /// <param name="clips">The clips to append together.</param>
    /// <param name="separator">The separator string to use between clips. Supports escape sequences (\n, \t, \r).</param>
    /// <param name="stripTrailingLineBreaks">Whether to strip trailing line breaks from each clip before appending.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A new clip containing the appended content.</returns>
    /// <remarks>
    /// The method will:
    /// 1. Process escape sequences in the separator (\n → newline, \t → tab, \r → carriage return)
    /// 2. Optionally strip trailing line breaks (\r\n, \n, \r) from each clip
    /// 3. Join all clips with the separator
    /// 4. Play an append sound notification
    /// 5. Create a new clip with the combined content
    /// </remarks>
    Task<Clip> AppendClipsAsync(IEnumerable<Clip> clips,
        string separator,
        bool stripTrailingLineBreaks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends newly captured text onto an existing clip (used by Auto-Append mode to grow a clip
    /// one clipboard capture at a time).
    /// </summary>
    /// <param name="targetClipId">The clip to append the captured text onto.</param>
    /// <param name="capturedText">The newly captured text to append.</param>
    /// <param name="separator">
    /// The separator string to use between the existing content and the new text. Supports escape
    /// sequences (\n, \t, \r).
    /// </param>
    /// <param name="stripTrailingLineBreaks">Whether to strip trailing line breaks from the existing content before appending.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The updated clip containing the combined content.</returns>
    Task<Clip> AppendCapturedTextAsync(Guid targetClipId,
        string capturedText,
        string separator,
        bool stripTrailingLineBreaks,
        CancellationToken cancellationToken = default);
}
