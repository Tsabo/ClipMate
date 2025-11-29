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
    Task<Clip> AppendClipsAsync(
        IEnumerable<Clip> clips,
        string separator,
        bool stripTrailingLineBreaks,
        CancellationToken cancellationToken = default);
}
