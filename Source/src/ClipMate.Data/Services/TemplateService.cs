using System.Diagnostics;
using System.Globalization;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for managing file-based templates.
/// Templates are plain text files stored in the Templates directory.
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<TemplateService> _logger;
    private DateTimeOffset _lastCacheRefresh = DateTimeOffset.MinValue;
    private int _sequenceCounter = 1;
    private List<FileTemplate> _templateCache = [];

    public TemplateService(IConfigurationService configurationService, ILogger<TemplateService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Refresh cache if empty or stale (> 5 minutes)
            if (_templateCache.Count == 0 || DateTimeOffset.Now - _lastCacheRefresh > TimeSpan.FromMinutes(5))
                await RefreshTemplatesCacheAsync(cancellationToken);

            return _templateCache.AsReadOnly();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<FileTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var templates = await GetAllTemplatesAsync(cancellationToken);
        return templates.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task RefreshTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            await RefreshTemplatesCacheAsync(cancellationToken);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public string MergeClipWithTemplate(FileTemplate template, Clip clip, int sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(template);
        return ReplaceTagsInText(template.Content, clip, sequenceNumber);
    }

    /// <inheritdoc />
    public string ReplaceTagsInText(string text, Clip clip, int sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(clip);

        var result = text;

        // Replace all tags with clip data
        result = result.Replace("#CLIP#", clip.TextContent ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#TITLE#", clip.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#URL#", clip.SourceUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#CREATOR#", clip.SourceApplicationName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#DATE#", clip.CapturedAt.ToString("d", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#TIME#", clip.CapturedAt.ToString("t", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#CURRENTDATE#", DateTime.Now.ToString("d", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#CURRENTTIME#", DateTime.Now.ToString("t", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("#SEQUENCE#", sequenceNumber.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        // Convert ClipMate 7 macro syntax to current syntax
        result = result.Replace("#PAUSE#", "{PAUSE}", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <inheritdoc />
    public string GetTemplatesDirectory() => _configurationService.Configuration.Directories.GetTemplatesDirectory();

    /// <inheritdoc />
    public void OpenTemplatesDirectory()
    {
        var templatesDir = GetTemplatesDirectory();

        try
        {
            // Open in Windows Explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = templatesDir,
                UseShellExecute = true,
                Verb = "open",
            });

            _logger.LogInformation("Opened templates directory: {Directory}", templatesDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open templates directory: {Directory}", templatesDir);
            throw;
        }
    }

    /// <inheritdoc />
    public FileTemplate? ActiveTemplate { get; private set; }

    /// <inheritdoc />
    public async Task SetActiveTemplateAsync(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            ActiveTemplate = null;
            _logger.LogInformation("Template cleared");
            return;
        }

        ActiveTemplate = await GetTemplateByNameAsync(templateName);
        if (ActiveTemplate != null)
            _logger.LogInformation("Template selected: {TemplateName}", ActiveTemplate.Name);
        else
            _logger.LogWarning("Template not found: {TemplateName}", templateName);
    }

    /// <inheritdoc />
    public void ResetSequenceCounter()
    {
        _sequenceCounter = 1;
        _logger.LogInformation("Template sequence counter reset to 1");
    }

    /// <inheritdoc />
    public Clip? TryApplyTemplate(Clip clip)
    {
        // Only apply templates to text-based clips (Text, RichText, and Html)
        if (ActiveTemplate == null || clip.Type != ClipType.Text && clip.Type != ClipType.Html && clip.Type != ClipType.RichText)
            return null;

        var mergedText = MergeClipWithTemplate(ActiveTemplate, clip, _sequenceCounter);
        _sequenceCounter++; // Increment for next use

        // Create a new clip with the merged template content
        // Preserve the original clip type (Text, RichText, or Html)
        var transformedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = clip.Type,
            TextContent = mergedText,
            ContentHash = ContentHasher.HashText(mergedText),
            CapturedAt = DateTime.UtcNow,
        };

        _logger.LogInformation("Applied template {TemplateName} to clip (Type: {ClipType}), sequence: {Sequence}",
            ActiveTemplate.Name, clip.Type, _sequenceCounter - 1);

        return transformedClip;
    }

    /// <summary>
    /// Refreshes the template cache by scanning the Templates directory.
    /// </summary>
    private async Task RefreshTemplatesCacheAsync(CancellationToken cancellationToken)
    {
        var templatesDir = GetTemplatesDirectory();
        _logger.LogDebug("Refreshing templates from directory: {Directory}", templatesDir);

        var newCache = new List<FileTemplate>();

        if (!Directory.Exists(templatesDir))
        {
            _logger.LogWarning("Templates directory does not exist: {Directory}", templatesDir);
            _templateCache = newCache;
            _lastCacheRefresh = DateTimeOffset.Now;

            return;
        }

        try
        {
            var txtFiles = Directory.GetFiles(templatesDir, "*.txt", SearchOption.TopDirectoryOnly);

            foreach (var item in txtFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(item);
                    var content = await File.ReadAllTextAsync(item, cancellationToken);

                    newCache.Add(new FileTemplate
                    {
                        Name = Path.GetFileNameWithoutExtension(item),
                        FilePath = item,
                        Content = content,
                        LastModified = fileInfo.LastWriteTimeUtc,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load template file: {FilePath}", item);
                }
            }

            // Sort by name
            newCache.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            _templateCache = newCache;
            _lastCacheRefresh = DateTimeOffset.Now;

            _logger.LogInformation("Loaded {Count} template(s) from {Directory}", newCache.Count, templatesDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh templates from directory: {Directory}", templatesDir);
            throw;
        }
    }
}
