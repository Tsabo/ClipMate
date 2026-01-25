using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for loading clip data prepared for printing.
/// </summary>
internal sealed class PrintService : IPrintService
{
    private readonly IDecryptedBlobCacheService _cacheService;
    private readonly IClipService _clipService;
    private readonly ILogger<PrintService> _logger;

    public PrintService(IClipService clipService,
        IDecryptedBlobCacheService cacheService,
        ILogger<PrintService> logger)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<PrintClipData>> LoadClipDataForPrintingAsync(List<Guid> clipIds,
        string databaseKey)
    {
        ArgumentNullException.ThrowIfNull(clipIds);
        ArgumentNullException.ThrowIfNull(databaseKey);

        var result = new List<PrintClipData>(clipIds.Count);

        foreach (var item in clipIds)
        {
            // Step 1: Load the clip
            var clip = await _clipService.GetByIdAsync(databaseKey, item);
            if (clip == null)
            {
                _logger.LogWarning("Clip {ClipId} not found in database {DatabaseKey}", item, databaseKey);
                continue;
            }

            // Step 2: Determine if this is an image clip and load appropriate data
            var isImage = false;
            byte[]? imageData = null;
            var content = string.Empty;

            if (clip.Encrypted)
            {
                // For encrypted clips, check the decrypted cache first
                var cachedBlobs = _cacheService.GetDecryptedBlobs(item);

                // Check for image data first (JPG or PNG)
                if (cachedBlobs?.JpgBlobs.Count > 0)
                {
                    isImage = true;
                    imageData = cachedBlobs.JpgBlobs[0].Data;
                }
                else if (cachedBlobs?.PngBlobs.Count > 0)
                {
                    isImage = true;
                    imageData = cachedBlobs.PngBlobs[0].Data;
                }
                else if (cachedBlobs is { TextBlobs.Count: > 0 })
                    content = cachedBlobs.TextBlobs[0].Data;
                else
                {
                    _logger.LogWarning("Encrypted clip {ClipId} not found in decryption cache", item);
                    content = "[Encrypted - Not Available]";
                }
            }
            else
            {
                // For non-encrypted clips, load blob data
                await _clipService.LoadBlobDataAsync(databaseKey, clip);

                // Check if this is an image clip
                if (clip.ImageData is { Length: > 0 })
                {
                    isImage = true;
                    imageData = clip.ImageData;
                }
                else
                    content = clip.TextContent ?? "[No Text Content]";
            }

            result.Add(new PrintClipData
            {
                ClipId = clip.Id,
                Title = clip.Title,
                Creator = clip.Creator,
                Created = clip.CapturedAt.DateTime,
                Url = clip.SourceUrl,
                Content = content,
                IsImage = isImage,
                ImageData = imageData,
            });
        }

        return result;
    }
}
