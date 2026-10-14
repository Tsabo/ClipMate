using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Markdown tab of the ClipViewer. Converts the clip's HTML or RTF
/// format to Markdown on demand (only while the tab is active).
/// </summary>
public partial class MarkdownViewerViewModel : ObservableObject
{
    private const string _previewStyle =
        """
        <style>
        body { font-family: 'Segoe UI', sans-serif; font-size: 10pt; margin: 8px; }
        code { font-family: Consolas, monospace; background-color: #f2f2f2; }
        pre { font-family: Consolas, monospace; background-color: #f2f2f2; padding: 6px; }
        blockquote { border-left: 3px solid #cccccc; margin-left: 0; padding-left: 8px; color: #555555; }
        table { border-collapse: collapse; }
        th, td { border: 1px solid #cccccc; padding: 3px 6px; }
        </style>
        """;

    private static readonly MarkdownPipeline _previewPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly IMarkdownConversionService _markdownConversionService;
    private readonly IRtfToHtmlConverter _rtfToHtmlConverter;
    private readonly IConfigurationService _configurationService;
    private readonly IClipService _clipService;
    private readonly IMessenger _messenger;
    private readonly ILogger<MarkdownViewerViewModel> _logger;

    private string? _cfHtml;
    private string? _rtf;
    private string? _databaseKey;
    private Guid _collectionId;
    private string? _sourceTitle;
    private bool _isDirty;
    private bool _isPreviewDirty;
    private bool _isSettingSource;

    [ObservableProperty]
    private MarkdownFlavor _selectedFlavor;

    [ObservableProperty]
    private MarkdownSourceFormat _selectedSource;

    [ObservableProperty]
    private bool _hasMultipleSources;

    [ObservableProperty]
    private bool _removeComments;

    [ObservableProperty]
    private bool _smartHref;

    [ObservableProperty]
    private bool _isPreviewMode;

    [ObservableProperty]
    private bool _isWordWrapEnabled = true;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand), nameof(SaveAsNewClipCommand))]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private string _previewHtml = string.Empty;

    public MarkdownViewerViewModel(IMarkdownConversionService markdownConversionService,
        IRtfToHtmlConverter rtfToHtmlConverter,
        IConfigurationService configurationService,
        IClipService clipService,
        IMessenger messenger,
        ILogger<MarkdownViewerViewModel> logger)
    {
        _markdownConversionService = markdownConversionService ?? throw new ArgumentNullException(nameof(markdownConversionService));
        _rtfToHtmlConverter = rtfToHtmlConverter ?? throw new ArgumentNullException(nameof(rtfToHtmlConverter));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize backing fields directly so loading preferences doesn't trigger a save
        var preferences = _configurationService.Configuration.Preferences;
        _selectedFlavor = preferences.MarkdownFlavor;
        _removeComments = preferences.MarkdownRemoveComments;
        _smartHref = preferences.MarkdownSmartHref;
        _isPreviewMode = preferences.MarkdownShowPreview;
    }

    /// <summary>
    /// Gets the Markdown flavors the user can choose from.
    /// </summary>
    public IReadOnlyList<MarkdownFlavor> AvailableFlavors { get; } = Enum.GetValues<MarkdownFlavor>();

    /// <summary>
    /// Gets the source formats the user can choose from when the clip has both HTML and RTF.
    /// </summary>
    public IReadOnlyList<MarkdownSourceFormat> AvailableSources { get; } = Enum.GetValues<MarkdownSourceFormat>();

    /// <summary>
    /// Gets whether the current clip has an HTML or RTF format to convert.
    /// </summary>
    public bool HasSource => _cfHtml != null || _rtf != null;

    /// <summary>
    /// Invoked when the user requests a copy. The control copies the editor selection, or all Markdown when nothing is selected.
    /// </summary>
    public Action? OnCopyRequested { get; set; }

    /// <summary>
    /// Sets the clip content to convert. Conversion is deferred until the tab is active.
    /// </summary>
    /// <param name="cfHtml">The raw CF_HTML clip format, or null when the clip has no HTML.</param>
    /// <param name="rtf">The RTF clip format, or null when the clip has no RTF.</param>
    /// <param name="databaseKey">The database the clip belongs to (used by Save as New Clip).</param>
    /// <param name="collectionId">The collection the clip belongs to (used by Save as New Clip).</param>
    /// <param name="sourceTitle">The title of the source clip.</param>
    public void SetSource(string? cfHtml, string? rtf, string? databaseKey, Guid collectionId, string? sourceTitle)
    {
        _cfHtml = string.IsNullOrWhiteSpace(cfHtml) ? null : cfHtml;
        _rtf = string.IsNullOrWhiteSpace(rtf) ? null : rtf;
        _databaseKey = databaseKey;
        _collectionId = collectionId;
        _sourceTitle = sourceTitle;

        _isSettingSource = true;
        try
        {
            HasMultipleSources = _cfHtml != null && _rtf != null;
            SelectedSource = _cfHtml != null ? MarkdownSourceFormat.Html : MarkdownSourceFormat.Rtf;
        }
        finally
        {
            _isSettingSource = false;
        }

        OnPropertyChanged(nameof(HasSource));
        Invalidate();
    }

    /// <summary>
    /// Clears the current source and output.
    /// </summary>
    public void Clear() => SetSource(null, null, null, Guid.Empty, null);

    /// <summary>
    /// Converts the current source to Markdown if it has changed since the last conversion.
    /// </summary>
    public void EnsureConverted()
    {
        if (_isDirty)
        {
            _isDirty = false;
            _isPreviewDirty = true;
            MarkdownText = Convert();
        }

        if (IsPreviewMode && _isPreviewDirty)
        {
            _isPreviewDirty = false;
            PreviewHtml = RenderPreview(MarkdownText);
        }
    }

    private string Convert()
    {
        if (!HasSource)
            return string.Empty;

        try
        {
            var html = SelectedSource == MarkdownSourceFormat.Html && _cfHtml != null
                ? CfHtmlParser.ExtractFragment(_cfHtml)
                : _rtfToHtmlConverter.ConvertToHtml(_rtf ?? string.Empty);

            var options = new MarkdownConversionOptions(SelectedFlavor, RemoveComments, SmartHref);
            var markdown = _markdownConversionService.ConvertHtmlToMarkdown(html, options);

            _logger.LogDebug("[MarkdownViewer] Converted {Source} to {Flavor} Markdown ({Length} chars)",
                SelectedSource, SelectedFlavor, markdown.Length);

            return markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MarkdownViewer] Failed to convert {Source} to Markdown", SelectedSource);
            return $"<!-- Unable to convert {SelectedSource} to Markdown: {ex.Message} -->";
        }
    }

    private static string RenderPreview(string markdown) =>
        string.IsNullOrEmpty(markdown)
            ? string.Empty
            : _previewStyle + Markdown.ToHtml(markdown, _previewPipeline);

    private void Invalidate()
    {
        _isDirty = true;
        if (IsActive)
            EnsureConverted();
    }

    private void SavePreferences()
    {
        var preferences = _configurationService.Configuration.Preferences;
        preferences.MarkdownFlavor = SelectedFlavor;
        preferences.MarkdownRemoveComments = RemoveComments;
        preferences.MarkdownSmartHref = SmartHref;
        preferences.MarkdownShowPreview = IsPreviewMode;

        _ = _configurationService.SaveAsync();
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
            EnsureConverted();
    }

    partial void OnSelectedFlavorChanged(MarkdownFlavor value)
    {
        SavePreferences();
        Invalidate();
    }

    partial void OnRemoveCommentsChanged(bool value)
    {
        SavePreferences();
        Invalidate();
    }

    partial void OnSmartHrefChanged(bool value)
    {
        SavePreferences();
        Invalidate();
    }

    partial void OnSelectedSourceChanged(MarkdownSourceFormat value)
    {
        if (!_isSettingSource)
            Invalidate();
    }

    partial void OnIsPreviewModeChanged(bool value)
    {
        SavePreferences();
        if (value && IsActive)
            EnsureConverted();
    }

    #region Commands

    [RelayCommand(CanExecute = nameof(HasMarkdown))]
    private void Copy() => OnCopyRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(HasMarkdown))]
    private async Task SaveAsNewClipAsync()
    {
        if (string.IsNullOrEmpty(_databaseKey) || _collectionId == Guid.Empty)
        {
            _messenger.Send(new StatusUpdateEvent("Cannot save Markdown: no collection for the current clip", true));
            return;
        }

        try
        {
            var newClip = new Clip
            {
                Title = string.IsNullOrWhiteSpace(_sourceTitle)
                    ? "Markdown"
                    : $"{_sourceTitle} (Markdown)",
                TextContent = MarkdownText,
                ContentHash = ContentHasher.HashText(MarkdownText),
                CollectionId = _collectionId,
                CapturedAt = DateTimeOffset.UtcNow,
                Type = ClipType.Text,
            };

            var createdClip = await _clipService.CreateAsync(_databaseKey, newClip);

            // ClipService returns the existing clip instead when the content hash already exists
            _messenger.Send(ReferenceEquals(createdClip, newClip)
                ? new StatusUpdateEvent($"Created new clip: {createdClip.Title}")
                : new StatusUpdateEvent($"Markdown already saved as clip: {createdClip.Title}"));

            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("[MarkdownViewer] Saved Markdown as clip {ClipId}", createdClip.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MarkdownViewer] Failed to save Markdown as new clip");
            _messenger.Send(new StatusUpdateEvent("Error saving Markdown as new clip", true));
        }
    }

    private bool HasMarkdown() => !string.IsNullOrEmpty(MarkdownText);

    #endregion
}
