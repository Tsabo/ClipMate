using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for appending multiple clips together into a single clip.
/// </summary>
public partial class ClipAppendService : IClipAppendService
{
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<ClipAppendService> _logger;
    private readonly ISoundService _soundService;

    public ClipAppendService(IDatabaseContextFactory contextFactory,
        ICollectionService collectionService,
        ISoundService soundService,
        ILogger<ClipAppendService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Clip> AppendClipsAsync(IEnumerable<Clip> clips,
        string separator,
        bool stripTrailingLineBreaks,
        CancellationToken cancellationToken = default)
    {
        var clipList = clips.ToList();

        if (clipList.Count == 0)
            throw new ArgumentException("At least one clip is required for appending.", nameof(clips));

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var clipRepository = _contextFactory.GetClipRepository(databaseKey);

        _logger.LogInformation("Appending {Count} clips together", clipList.Count);

        try
        {
            // Process escape sequences in separator
            var processedSeparator = ProcessEscapeSequences(separator);

            // Build the combined text content
            var combinedText = new StringBuilder();
            var isFirst = true;

            foreach (var item in clipList)
            {
                // Get the text content (ensure it's loaded)
                var textContent = item.TextContent;
                if (string.IsNullOrEmpty(textContent))
                {
                    // Try to load text content from repository if not loaded
                    var loadedClip = await clipRepository.GetByIdAsync(item.Id, cancellationToken);
                    textContent = loadedClip?.TextContent;
                }

                if (string.IsNullOrEmpty(textContent))
                {
                    _logger.LogWarning("Clip {ClipId} has no text content, skipping", item.Id);

                    continue;
                }

                // Strip trailing line breaks if requested
                if (stripTrailingLineBreaks)
                    textContent = StripTrailingLineBreaks(textContent);

                // Add separator between clips (not before the first one)
                if (!isFirst && !string.IsNullOrEmpty(processedSeparator))
                    combinedText.Append(processedSeparator);

                combinedText.Append(textContent);
                isFirst = false;
            }

            var finalText = combinedText.ToString();

            // Create a new clip with the combined content
            var newClip = new Clip
            {
                Id = Guid.NewGuid(),
                Title = $"Appended ({clipList.Count} clips)",
                CapturedAt = DateTimeOffset.Now,
                Type = ClipType.Text,
                TextContent = finalText,
                Size = Encoding.UTF8.GetByteCount(finalText),
                ContentHash = ComputeContentHash(finalText),
                Checksum = ComputeChecksum(finalText),
                CollectionId = clipList.First().CollectionId, // Use first clip's collection
                Creator = Environment.UserName,
            };

            // Save the new clip to the repository
            var savedClip = await clipRepository.CreateAsync(newClip, cancellationToken);

            // Play append sound notification
            await _soundService.PlaySoundAsync(SoundEvent.Append, cancellationToken);

            _logger.LogInformation("Successfully created appended clip {ClipId} with {Length} characters",
                savedClip.Id, finalText.Length);

            return savedClip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append clips");

            throw;
        }
    }

    /// <summary>
    /// Processes escape sequences in the separator string.
    /// Converts \n to newline, \t to tab, \r to carriage return.
    /// </summary>
    private static string ProcessEscapeSequences(string separator)
    {
        if (string.IsNullOrEmpty(separator))
            return string.Empty;

        return separator
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r");
    }

    /// <summary>
    /// Strips trailing line breaks (\r\n, \n, or \r) from the text.
    /// </summary>
    private static string StripTrailingLineBreaks(string text) => string.IsNullOrEmpty(text)
        ? text
        : TrailingLineBreakRegex().Replace(text, string.Empty);

    /// <summary>
    /// Computes a SHA-256 hash of the text content for duplicate detection.
    /// </summary>
    private static string ComputeContentHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Computes a simple checksum for ClipMate 7.5 compatibility.
    /// </summary>
    private static int ComputeChecksum(string text) => text.GetHashCode();

    [GeneratedRegex(@"(\r\n|\n|\r)+$", RegexOptions.Compiled)]
    private static partial Regex TrailingLineBreakRegex();
}
