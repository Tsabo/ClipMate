using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for appending multiple clips together into a single clip.
/// </summary>
public class ClipAppendService : IClipAppendService
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<ClipAppendService> _logger;
    private readonly ISoundService _soundService;

    public ClipAppendService(IDatabaseContextFactory contextFactory,
        ICollectionService collectionService,
        IClipService clipService,
        ISoundService soundService,
        ILogger<ClipAppendService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
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
                // Get the text content (ensure it's loaded). Grid-bound Clip objects are
                // lightweight and don't carry blob content until explicitly loaded.
                var textContent = item.TextContent;
                if (string.IsNullOrEmpty(textContent))
                {
                    await _clipService.LoadBlobDataAsync(databaseKey, item, cancellationToken);
                    textContent = item.TextContent;
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

    /// <inheritdoc />
    public async Task<Clip> AppendCapturedTextAsync(Guid targetClipId,
        string capturedText,
        string separator,
        bool stripTrailingLineBreaks,
        CancellationToken cancellationToken = default)
    {
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var clipRepository = _contextFactory.GetClipRepository(databaseKey);
        var clipDataRepository = _contextFactory.GetClipDataRepository(databaseKey);
        var blobRepository = _contextFactory.GetBlobRepository(databaseKey);

        var targetClip = await clipRepository.GetByIdAsync(targetClipId, cancellationToken) ??
                         throw new InvalidOperationException($"Clip {targetClipId} was not found.");

        try
        {
            // Text content lives in the ClipData/BlobTxt tables, not on the Clip row itself -
            // find the existing text blob (if any) so we can update it in place.
            var clipDataList = await clipDataRepository.GetByClipIdAsync(targetClipId, cancellationToken);
            var textClipData = clipDataList.FirstOrDefault(p => p.Format == Formats.Text.Code || p.Format == Formats.UnicodeText.Code);

            var textBlobs = await blobRepository.GetTextByClipIdAsync(targetClipId, cancellationToken);
            var existingBlob = textClipData != null
                ? textBlobs.FirstOrDefault(p => p.ClipDataId == textClipData.Id)
                : null;

            var existingText = existingBlob?.Data ?? string.Empty;
            if (stripTrailingLineBreaks)
                existingText = StripTrailingLineBreaks(existingText);

            var processedSeparator = ProcessEscapeSequences(separator);

            var combinedText = new StringBuilder(existingText);
            if (!string.IsNullOrEmpty(processedSeparator))
                combinedText.Append(processedSeparator);

            combinedText.Append(capturedText);

            var finalText = combinedText.ToString();

            if (existingBlob != null)
            {
                existingBlob.Data = finalText;
                await blobRepository.UpdateTextAsync(existingBlob, cancellationToken);
            }
            else
            {
                // No existing text blob on this clip (e.g. the seed capture had no text format yet) - create one.
                var newClipData = await clipDataRepository.CreateAsync(new ClipData
                {
                    Id = Guid.NewGuid(),
                    ClipId = targetClipId,
                    FormatName = Formats.UnicodeText.Name,
                    Format = Formats.UnicodeText.Code,
                    Size = finalText.Length * 2,
                    StorageType = 1,
                }, cancellationToken);

                await blobRepository.CreateTextAsync(new BlobTxt
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = newClipData.Id,
                    ClipId = targetClipId,
                    Data = finalText,
                }, cancellationToken);
            }

            targetClip.TextContent = finalText;
            targetClip.Size = Encoding.UTF8.GetByteCount(finalText);
            targetClip.ContentHash = ComputeContentHash(finalText);
            targetClip.Checksum = ComputeChecksum(finalText);

            await clipRepository.UpdateAsync(targetClip, cancellationToken);

            await _soundService.PlaySoundAsync(SoundEvent.Append, cancellationToken);

            _logger.LogInformation("Appended captured text onto clip {ClipId}, now {Length} characters",
                targetClip.Id, finalText.Length);

            return targetClip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append captured text onto clip {ClipId}", targetClipId);

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
        : RegexPatterns.TrailingLineBreak().Replace(text, string.Empty);

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
}
