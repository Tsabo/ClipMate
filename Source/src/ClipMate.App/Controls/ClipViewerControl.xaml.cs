using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Bars;
using DevExpress.XtraRichEdit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThrottleDebounce;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Controls;

/// <summary>
/// Multi-tab viewer for clipboard data with support for Text, HTML, RTF, Bitmap, Picture, and Binary formats.
/// Listens to ClipSelectedEvent and PreferencesChangedEvent via MVVM Toolkit Messenger.
/// </summary>
public partial class ClipViewerControl : IRecipient<ClipSelectedEvent>, IRecipient<PreferencesChangedEvent>
{
    #region Constructor

    public ClipViewerControl()
    {
        InitializeComponent();

        // Get services from DI container
        var app = (App)Application.Current;
        var serviceProvider = app.ServiceProvider;

        _logger = serviceProvider.GetRequiredService<ILogger<ClipViewerControl>>();
        _messenger = serviceProvider.GetRequiredService<IMessenger>();
        _configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
        _textTransformService = serviceProvider.GetRequiredService<ITextTransformService>();
        _databaseContextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
        _clipService = serviceProvider.GetRequiredService<IClipService>();

        // Initialize toolbar ViewModel
        _toolbarViewModel = serviceProvider.GetRequiredService<ClipViewerToolbarViewModel>();
        TextEditorToolbar.DataContext = _toolbarViewModel;

        // Wire up toolbar command handlers
        WireToolbarCommands();

        // Load Monaco Editor configuration
        var monacoOptions = _configurationService.Configuration.MonacoEditor;
        _logger.LogInformation("Monaco configuration loaded - EnableDebug: {EnableDebug}, Theme: {Theme}, FontSize: {FontSize}",
            monacoOptions.EnableDebug, monacoOptions.Theme, monacoOptions.FontSize);

        TextEditor.EditorOptions = monacoOptions;

        // Register for ClipSelectedEvent and PreferencesChangedEvent messages
        Loaded += (_, _) =>
        {
            _messenger.Register<ClipSelectedEvent>(this);
            _messenger.Register<PreferencesChangedEvent>(this);

            // Apply initial editor settings
            ApplyEditorSettings();

            // Set up debounced auto-save (1 second after last change)
            _debouncedTextSave = Debouncer.Debounce(SaveTextEditorDebounced, TimeSpan.FromSeconds(1));

            // Monitor Text property changes on text editor
            var textDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            textDescriptor?.AddValueChanged(TextEditor, OnTextEditorTextChanged);

            // Monitor Language property changes on text editor
            var textLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            textLangDescriptor?.AddValueChanged(TextEditor, OnTextEditorLanguageChanged);
        };

        Unloaded += (_, _) =>
        {
            _messenger.Unregister<ClipSelectedEvent>(this);
            _messenger.Unregister<PreferencesChangedEvent>(this);

            // Cancel any ongoing load operation
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = null;

            // Remove value change handlers
            var textDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            textDescriptor?.RemoveValueChanged(TextEditor, OnTextEditorTextChanged);

            var textLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            textLangDescriptor?.RemoveValueChanged(TextEditor, OnTextEditorLanguageChanged);
        };
    }

    #endregion

    #region Property Changed Handlers

    private static async void OnClipIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ClipViewerControl control)
            return;

        if (e.NewValue is Guid clipId)
        {
            // When ClipId is set directly (not via event), we don't have database key
            // Fall back to current or default database
            var token = control._loadCancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                await control.LoadClipDataAsync(clipId, control._currentDatabaseKey, token);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected when user rapidly switches clips - don't log
            }
        }
        else
        {
            // No clip selected - hide all tabs
            control.HideAllTabs();
        }
    }

    #endregion

    #region Event Handlers

    private void OnBinaryFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BinaryFormatComboBox.SelectedItem is string formatName &&
            _binaryFormats.TryGetValue(formatName, out var data))
            HexViewer.Stream = new MemoryStream(data);
    }

    #endregion

    #region Messenger Event Handlers

    /// <summary>
    /// Receives ClipSelectedEvent messages from the messenger.
    /// </summary>
    public void Receive(ClipSelectedEvent message)
    {
        var clipId = message.SelectedClip?.Id;
        var databaseKey = message.DatabaseKey;
        _logger.LogInformation("[ClipViewer] Received ClipSelectedEvent - ClipId: {ClipId}, DatabaseKey: {DatabaseKey}", clipId, databaseKey);

        // Cancel previous load operation
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        // Store the database key before setting ClipId (which triggers load)
        if (!string.IsNullOrEmpty(databaseKey))
            _currentDatabaseKey = databaseKey;

        ClipId = clipId;
    }

    /// <summary>
    /// Receives PreferencesChangedEvent messages from the messenger.
    /// </summary>
    public void Receive(PreferencesChangedEvent message)
    {
        _logger.LogInformation("[ClipViewer] Received PreferencesChangedEvent - Applying editor settings");
        ApplyEditorSettings();
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ClipIdProperty =
        DependencyProperty.Register(
            nameof(ClipId),
            typeof(Guid?),
            typeof(ClipViewerControl),
            new PropertyMetadata(null, OnClipIdChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(ClipViewerControl),
            new PropertyMetadata(false));

    public Guid? ClipId
    {
        get => (Guid?)GetValue(ClipIdProperty);
        set => SetValue(ClipIdProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingProperty, value);
    }

    #endregion

    #region Fields

    private readonly ILogger<ClipViewerControl> _logger;
    private readonly IMessenger _messenger;
    private readonly IConfigurationService _configurationService;
    private readonly ClipViewerToolbarViewModel _toolbarViewModel;
    private readonly ITextTransformService _textTransformService;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IClipService _clipService;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    private CancellationTokenSource? _loadCancellationTokenSource;
    private List<ClipData> _currentClipData = [];
    private Dictionary<Guid, BlobTxt> _textBlobs = [];
    private Dictionary<Guid, BlobJpg> _jpgBlobs = [];
    private Dictionary<Guid, BlobPng> _pngBlobs = [];
    private Dictionary<Guid, BlobBlob> _binaryBlobs = [];
    private readonly Dictionary<string, byte[]> _binaryFormats = [];

    // Track which ClipData entries are being edited
    private Guid? _textFormatClipDataId;

    // Track when we're loading content (not editing)
    private bool _isLoadingContent;

    // Track what changed to optimize saves
    private bool _textEditorTextDirty;
    private bool _textEditorLanguageDirty;

    // Debounced actions for auto-save
    private RateLimitedAction? _debouncedTextSave;

    #endregion

    #region Data Loading

    private string? _currentDatabaseKey;

    private async Task LoadClipDataAsync(Guid clipId, string? databaseKey, CancellationToken cancellationToken = default)
    {
        // Wait for any previous load operation to complete to prevent concurrent DbContext access
        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            IsLoading = true;
            _isLoadingContent = true; // Suppress save operations during load

            // Store the database key for this clip
            _currentDatabaseKey = databaseKey;

            // Reset dirty flags for new clip
            _textEditorTextDirty = false;
            _textEditorLanguageDirty = false;

            _logger.LogInformation("[ClipViewer] LoadClipDataAsync START - ClipId: {ClipId}", clipId);

            await LoadClipDataInternalAsync(clipId, cancellationToken);
        }
        finally
        {
            _loadSemaphore.Release();
            IsLoading = false;
            _isLoadingContent = false; // Re-enable save operations
        }
    }

    private async Task LoadClipDataInternalAsync(Guid clipId, CancellationToken cancellationToken)
    {
        try
        {
            // Get database context for the clip's database
            if (string.IsNullOrEmpty(_currentDatabaseKey))
            {
                _logger.LogWarning("No database key provided for clip {ClipId}, using default database", clipId);
                _currentDatabaseKey = _configurationService.Configuration.DefaultDatabase;
            }

            if (string.IsNullOrEmpty(_currentDatabaseKey))
                throw new InvalidOperationException("No database configured");

            // Get repositories - each creates its own fresh context internally
            var clipDataRepository = _databaseContextFactory.GetClipDataRepository(_currentDatabaseKey);
            var blobRepository = _databaseContextFactory.GetBlobRepository(_currentDatabaseKey);
            var monacoStateRepository = _databaseContextFactory.GetMonacoEditorStateRepository(_currentDatabaseKey);

            // Load all ClipData entries for this clip
            _currentClipData = (await clipDataRepository.GetByClipIdAsync(clipId, cancellationToken))
                .OrderBy(p => p.FormatName)
                .ToList();

            _logger.LogInformation("[ClipViewer] Found {Count} ClipData entries", _currentClipData.Count);

            if (_currentClipData.Count == 0)
            {
                _logger.LogWarning("No ClipData entries found for ClipId: {ClipId}", clipId);
                HideAllTabs();
                return;
            }

            // Load all BLOBs upfront
            var textBlobs = await blobRepository.GetTextByClipIdAsync(clipId, cancellationToken);
            var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clipId, cancellationToken);
            var pngBlobs = await blobRepository.GetPngByClipIdAsync(clipId, cancellationToken);
            var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clipId, cancellationToken);

            // Check cancellation after blob loading
            cancellationToken.ThrowIfCancellationRequested();

            // Create lookup dictionaries by ClipDataId
            _textBlobs = textBlobs.ToDictionary(p => p.ClipDataId);
            _jpgBlobs = jpgBlobs.ToDictionary(p => p.ClipDataId);
            _pngBlobs = pngBlobs.ToDictionary(p => p.ClipDataId);
            _binaryBlobs = binaryBlobs.ToDictionary(p => p.ClipDataId);

            _logger.LogInformation("[ClipViewer] Loaded blobs - Text: {TextCount}, JPG: {JpgCount}, PNG: {PngCount}, Binary: {BinaryCount}",
                _textBlobs.Count, _jpgBlobs.Count, _pngBlobs.Count, _binaryBlobs.Count);

            // Reset all tabs
            ResetAllTabs();

            // Load data for each format type
            _logger.LogInformation("[ClipViewer] Loading formats for ClipId: {ClipId}", clipId);
            await LoadTextFormatsAsync(monacoStateRepository, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded text formats");
            await LoadHtmlFormatAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded HTML format");
            await LoadRtfFormatAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded RTF format");
            await LoadBitmapFormatsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded bitmap formats");
            await LoadPictureFormatsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded picture formats");
            await LoadBinaryFormatsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("[ClipViewer] Loaded binary formats");
            _logger.LogInformation("[ClipViewer] All formats loaded for ClipId: {ClipId}", clipId);

            // Select default tab based on preferences
            SetDefaultActiveTab();
            _logger.LogInformation("[ClipViewer] Default tab selected and load complete for ClipId: {ClipId}", clipId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Load clip data cancelled for ClipId: {ClipId}", clipId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clip data for ClipId: {ClipId}", clipId);
            MessageBox.Show($"Error loading clip data: {ex.Message}", "Load Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadTextFormatsAsync(IMonacoEditorStateRepository monacoStateRepository, CancellationToken cancellationToken = default)
    {
        // Debug log all ClipData entries
        foreach (var item in _currentClipData)
        {
            _logger.LogDebug("[ClipViewer] ClipData: Format={Format}/{FormatName}, StorageType={StorageType}, Id={Id}, HasBlob={HasBlob}",
                item.Format, item.FormatName, item.StorageType, item.Id, _textBlobs.ContainsKey(item.Id));
        }

        // Look for text formats (CF_TEXT or CF_UNICODETEXT)
        var textFormat = _currentClipData
            .FirstOrDefault(p =>
                p is { StorageType: StorageType.Text } &&
                (p.Format == Formats.Text.Code || p.Format == Formats.UnicodeText.Code));

        _logger.LogDebug("[ClipViewer] Text format search result: {Result}, Looking for formats {Text}/{Unicode}",
            textFormat != null
                ? $"Found {textFormat.FormatName}"
                : "Not found",
            Formats.Text.Code, Formats.UnicodeText.Code);

        if (textFormat != null && _textBlobs.TryGetValue(textFormat.Id, out var blobData))
        {
            // Wait for Monaco to initialize before setting text
            await WaitForMonacoInitializationAsync(TextEditor, 10000, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Load saved language and view state from MonacoEditorState
            var editorState = await monacoStateRepository.GetByClipDataIdAsync(textFormat.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var savedLanguage = editorState?.Language ?? "plaintext";

            // Load everything in one atomic operation
            await TextEditor.LoadContentAsync(blobData.Data, savedLanguage, editorState?.ViewState);

            TextEditor.IsReadOnly = false; // Allow editing
            TextEditor.Visibility = Visibility.Visible;
            NoTextMessage.Visibility = Visibility.Collapsed;
            TextTab.Visibility = Visibility.Visible;

            // Track the ClipData ID for saving changes
            _textFormatClipDataId = textFormat.Id;
            return;
        }

        // No text format available
        TextEditor.Visibility = Visibility.Collapsed;
        NoTextMessage.Visibility = Visibility.Visible;
        TextTab.Visibility = Visibility.Collapsed;
    }

    // ReSharper disable once UnusedParameter.Local
    private Task LoadHtmlFormatAsync(CancellationToken cancellationToken = default)
    {
        var htmlFormat = _currentClipData
            .FirstOrDefault(p =>
                (p.Format == Formats.Html.Code || p.Format == Formats.HtmlAlt.Code) && p.StorageType == StorageType.Text);

        _logger.LogDebug("[ClipViewer] HTML format search result: {Result}, Looking for formats {Html}/{HtmlAlt}",
            htmlFormat != null
                ? $"Found {htmlFormat.FormatName}"
                : "Not found",
            Formats.Html.Code, Formats.HtmlAlt.Code);

        if (htmlFormat != null)
        {
            var hasBlob = _textBlobs.ContainsKey(htmlFormat.Id);
            _logger.LogDebug("[ClipViewer] HTML format has blob: {HasBlob}, BlobData length: {Length}",
                hasBlob, hasBlob
                    ? _textBlobs[htmlFormat.Id].Data.Length
                    : 0);
        }

        if (htmlFormat != null && _textBlobs.TryGetValue(htmlFormat.Id, out var blobData))
        {
            try
            {
                _logger.LogDebug("[ClipViewer] Starting HTML format load");

                // Make tab visible so content can be displayed
                HtmlTab.Visibility = Visibility.Visible;
                NoHtmlMessage.Visibility = Visibility.Collapsed;
                HtmlPreview.Visibility = Visibility.Visible;

                // Load HTML viewer options from configuration
                var config = _configurationService.Configuration;
                HtmlUsePicturesFromCacheCheckItem.IsChecked = config.Preferences.HtmlUsePicturesFromCache;
                HtmlUseAllHtmlInClipCheckItem.IsChecked = config.Preferences.HtmlUseAllHtmlInClip;
                HtmlDeleteScriptsCheckItem.IsChecked = config.Preferences.HtmlDeleteScripts;

                // Parse CF_HTML format to extract actual HTML content
                var htmlContent = config.Preferences.HtmlUseAllHtmlInClip
                    ? blobData.Data
                    : ParseCfHtmlFormat(blobData.Data);

                // Extract source URL from HTML content (if present)
                var sourceUrl = ExtractSourceUrl(htmlContent);
                HtmlSourceUrlEdit.EditValue = sourceUrl ?? "(No source URL found)";

                // Strip scripts if DeleteScripts option is enabled
                if (config.Preferences.HtmlDeleteScripts)
                    htmlContent = StripScripts(htmlContent);

                // Display HTML content using HtmlRenderer.WPF (handles DPI automatically)
                _logger.LogDebug("[ClipViewer] Rendering HTML content with HtmlRenderer.WPF");
                HtmlPreview.Text = htmlContent;

                _logger.LogInformation("[ClipViewer] HTML tab displayed successfully");

                return Task.CompletedTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[ClipViewer] HTML format load cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ClipViewer] Error loading HTML format");
                // Fall through to hide HTML tab
            }
        }

        // No HTML format available
        HtmlPreview.Visibility = Visibility.Collapsed;
        NoHtmlMessage.Visibility = Visibility.Visible;
        HtmlTab.Visibility = Visibility.Collapsed;
        _logger.LogDebug("[ClipViewer] No HTML format available");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses CF_HTML clipboard format to extract actual HTML content.
    /// CF_HTML format includes metadata headers like "Version:0.9 StartHTML:..." that need to be stripped.
    /// Uses StartHTML/EndHTML offsets to preserve full document structure like ClipMate 7.5.
    /// </summary>
    private static string ParseCfHtmlFormat(string cfHtml)
    {
        if (string.IsNullOrWhiteSpace(cfHtml))
            return string.Empty;

        // Check if this is CF_HTML format (has Version: header)
        if (!cfHtml.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
            return cfHtml; // Already plain HTML

        try
        {
            // Parse the StartHTML and EndHTML offsets to get the full HTML document
            // (This preserves <html> and <body> tags with their styles, like ClipMate 7.5)
            var startHtmlMatch = Regex.Match(cfHtml, @"StartHTML:(\d+)", RegexOptions.IgnoreCase);
            var endHtmlMatch = Regex.Match(cfHtml, @"EndHTML:(\d+)", RegexOptions.IgnoreCase);

            if (startHtmlMatch.Success && endHtmlMatch.Success)
            {
                var startHtml = int.Parse(startHtmlMatch.Groups[1].Value);
                var endHtml = int.Parse(endHtmlMatch.Groups[1].Value);

                // Extract the full HTML document between StartHTML and EndHTML
                if (startHtml >= 0 && endHtml > startHtml && endHtml <= cfHtml.Length)
                {
                    var html = cfHtml.Substring(startHtml, endHtml - startHtml);
                    return html.Trim();
                }
            }

            // Fallback: try to extract everything after the last header line
            var lines = cfHtml.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var htmlStartIndex = -1;

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith('<'))
                    continue;

                htmlStartIndex = i;
                break;
            }

            if (htmlStartIndex >= 0)
                return string.Join(Environment.NewLine, lines.Skip(htmlStartIndex));

            return cfHtml; // Return as-is if parsing fails
        }
        catch (Exception)
        {
            // If parsing fails, return the original content
            return cfHtml;
        }
    }

    /// <summary>
    /// Strips script tags and inline event handlers from HTML content for security.
    /// </summary>
    private static string StripScripts(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return string.Empty;

        // Remove <script> tags and their content
        htmlContent = ScriptTagRegEx().Replace(htmlContent, "");

        // Remove inline event handlers (onclick, onload, onerror, etc.)
        htmlContent = InlineEventHandlerQuotedRegex().Replace(htmlContent, "");
        htmlContent = InlineEventHandlerUnquotedRegex().Replace(htmlContent, "");

        // Remove javascript: URLs
        htmlContent = JavascriptUrlRegEx().Replace(htmlContent, "#");

        return htmlContent;
    }

    /// <summary>
    /// Extracts the source URL from HTML content.
    /// Looks for <!--SourceURL: http://example.com--> comment pattern.
    /// </summary>
    private static string? ExtractSourceUrl(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return null;

        var match = SourceUrlRegEx().Match(htmlContent);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : null;
    }

    /// <summary>
    /// Handler for HTML Navigate button click
    /// </summary>
    private void OnHtmlNavigateClick(object sender, ItemClickEventArgs e)
    {
        if (HtmlSourceUrlEdit?.EditValue is not string url || string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            // Ensure URL has a scheme
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;

            // Open URL in default browser (HtmlRenderer doesn't support navigation)
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });

            _logger.LogInformation("[ClipViewer] Opened URL in default browser: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClipViewer] Error navigating to URL: {Url}", url);
            MessageBox.Show($"Invalid URL: {ex.Message}", "Navigation Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handler for HTML viewer option checkbox changes
    /// </summary>
    private void OnHtmlOptionChanged(object sender, ItemClickEventArgs e)
    {
        if (_isLoadingContent)
            return; // Ignore changes during initial load

        var config = _configurationService.Configuration;

        // Update configuration from checkboxes
        config.Preferences.HtmlUsePicturesFromCache = HtmlUsePicturesFromCacheCheckItem?.IsChecked ?? false;
        config.Preferences.HtmlUseAllHtmlInClip = HtmlUseAllHtmlInClipCheckItem?.IsChecked ?? false;
        config.Preferences.HtmlDeleteScripts = HtmlDeleteScriptsCheckItem?.IsChecked ?? true;

        // Save configuration asynchronously
        _ = _configurationService.SaveAsync();

        _logger.LogInformation("[ClipViewer] HTML options changed - UsePicturesFromCache: {UsePictures}, UseAllHtml: {UseAllHtml}, DeleteScripts: {DeleteScripts}",
            config.Preferences.HtmlUsePicturesFromCache,
            config.Preferences.HtmlUseAllHtmlInClip,
            config.Preferences.HtmlDeleteScripts);

        // Reload current HTML content with new settings if we have clip data
        if (_currentClipData.Count > 0)
            _ = LoadHtmlFormatAsync();
    }

    // ReSharper disable once UnusedParameter.Local
    private Task LoadRtfFormatAsync(CancellationToken cancellationToken = default)
    {
        var rtfFormat = _currentClipData
            .FirstOrDefault(p => p.FormatName.Contains("RTF", StringComparison.OrdinalIgnoreCase) &&
                                 p.StorageType == StorageType.Text);

        if (rtfFormat != null && _textBlobs.TryGetValue(rtfFormat.Id, out var blobData))
        {
            try
            {
                // Load RTF content into DevExpress RichEditControl
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(blobData.Data));
                RtfViewer.Document.LoadDocument(stream, DocumentFormat.Rtf);

                RtfViewer.Visibility = Visibility.Visible;
                NoRtfMessage.Visibility = Visibility.Collapsed;
                RtfTab.Visibility = Visibility.Visible;
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading RTF format");
            }
        }

        // No RTF format available
        RtfViewer.Visibility = Visibility.Collapsed;
        NoRtfMessage.Visibility = Visibility.Visible;
        RtfTab.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    // ReSharper disable once UnusedParameter.Local
    private Task LoadBitmapFormatsAsync(CancellationToken cancellationToken = default)
    {
        // Look for CF_BITMAP or CF_DIB formats (stored in BLOBBLOB)
        var bitmapFormat = _currentClipData
            .FirstOrDefault(p =>
                (p.Format == Formats.Bitmap.Code || p.Format == Formats.Dib.Code) && p.StorageType == StorageType.Binary);

        if (bitmapFormat != null && _binaryBlobs.TryGetValue(bitmapFormat.Id, out var blobData))
        {
            try
            {
                var bitmap = BytesToBitmapImage(blobData.Data);
                BitmapViewer.Source = bitmap;
                BitmapViewer.Visibility = Visibility.Visible;
                NoBitmapMessage.Visibility = Visibility.Collapsed;
                BitmapTab.Visibility = Visibility.Visible;
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading bitmap format");
            }
        }

        // No bitmap format available
        BitmapViewer.Visibility = Visibility.Collapsed;
        NoBitmapMessage.Visibility = Visibility.Visible;
        BitmapTab.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    // ReSharper disable once UnusedParameter.Local
    private Task LoadPictureFormatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[LoadPicture] Looking for PNG or JPG formats in {Count} ClipData entries", _currentClipData.Count);

        // Look for PNG or JPG formats
        var pngFormat = _currentClipData.FirstOrDefault(p => p.StorageType == StorageType.Png);
        var jpgFormat = _currentClipData.FirstOrDefault(p => p.StorageType == StorageType.Jpeg);

        _logger.LogDebug("[LoadPicture] PNG format found: {Found}, JPG format found: {Found2}",
            pngFormat != null, jpgFormat != null);

        byte[]? imageData = null;

        if (pngFormat != null && _pngBlobs.TryGetValue(pngFormat.Id, out var pngBlob))
        {
            imageData = pngBlob.Data;
            _logger.LogDebug("[LoadPicture] Retrieved PNG data: {Size} bytes", imageData.Length);
        }
        else if (jpgFormat != null && _jpgBlobs.TryGetValue(jpgFormat.Id, out var jpgBlob))
        {
            imageData = jpgBlob.Data;
            _logger.LogDebug("[LoadPicture] Retrieved JPG data: {Size} bytes", imageData.Length);
        }

        if (imageData != null)
        {
            try
            {
                _logger.LogDebug("[LoadPicture] Creating BitmapImage from {Size} bytes", imageData.Length);
                var bitmap = BytesToBitmapImage(imageData);
                _logger.LogDebug("[LoadPicture] BitmapImage created successfully: {Width}x{Height}",
                    bitmap.PixelWidth, bitmap.PixelHeight);

                PictureViewer.Source = bitmap;
                PictureViewer.ZoomRatio = 1;
                PictureViewer.Visibility = Visibility.Visible;
                NoPictureMessage.Visibility = Visibility.Collapsed;
                PictureTab.Visibility = Visibility.Visible;

                // Force layout update
                PictureViewer.UpdateLayout();

                _logger.LogDebug("[LoadPicture] PictureViewer updated - Source set: {HasSource}, ActualWidth: {Width}, ActualHeight: {Height}, IsVisible: {IsVisible}",
                    PictureViewer.Source != null, PictureViewer.ActualWidth, PictureViewer.ActualHeight, PictureViewer.IsVisible);

                // Broadcast image dimensions for status bar
                if (ClipId.HasValue)
                    _messenger.Send(new ImageDimensionsLoadedEvent(ClipId.Value, bitmap.PixelWidth, bitmap.PixelHeight));

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading picture format");
            }
        }

        // No picture format available
        PictureViewer.Visibility = Visibility.Collapsed;
        NoPictureMessage.Visibility = Visibility.Visible;
        PictureTab.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    // ReSharper disable once UnusedParameter.Local
    private Task LoadBinaryFormatsAsync(CancellationToken cancellationToken = default)
    {
        _binaryFormats.Clear();
        BinaryFormatComboBox.Items.Clear();

        // Load all available formats into binary viewer
        foreach (var item in _currentClipData)
        {
            var data = item.StorageType switch
            {
                StorageType.Text when _textBlobs.TryGetValue(item.Id, out var txt) =>
                    Encoding.UTF8.GetBytes(txt.Data),
                StorageType.Jpeg when _jpgBlobs.TryGetValue(item.Id, out var jpg) =>
                    jpg.Data,
                StorageType.Png when _pngBlobs.TryGetValue(item.Id, out var png) =>
                    png.Data,
                StorageType.Binary when _binaryBlobs.TryGetValue(item.Id, out var blob) =>
                    blob.Data,
                var _ => null,
            };

            if (data is not { Length: > 0 })
                continue;

            var displayName = $"{item.FormatName} ({item.Format}) - {FormatBytes(item.Size)}";
            _binaryFormats[displayName] = data;
            BinaryFormatComboBox.Items.Add(displayName);
        }

        if (_binaryFormats.Count > 0)
        {
            BinaryFormatComboBox.SelectedIndex = 0;
            BinaryFormatComboBox.Visibility = Visibility.Visible;
            HexViewer.Visibility = Visibility.Visible;
            NoBinaryMessage.Visibility = Visibility.Collapsed;
            BinaryTab.Visibility = Visibility.Visible;
        }
        else
        {
            BinaryFormatComboBox.Visibility = Visibility.Collapsed;
            HexViewer.Visibility = Visibility.Collapsed;
            NoBinaryMessage.Visibility = Visibility.Visible;
            BinaryTab.Visibility = Visibility.Collapsed;
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Editor Change Handlers

    private void OnTextEditorTextChanged(object? sender, EventArgs e)
    {
        // Don't save if we're loading content - only save actual user edits
        if (_isLoadingContent)
            return;

        if (_textFormatClipDataId == null || TextEditor.IsReadOnly)
            return;

        _textEditorTextDirty = true;
        // Trigger debounced save
        _debouncedTextSave?.Invoke();
        _logger.LogDebug("[ClipViewer] Text editor text changed by user, save scheduled");
    }

    private void OnTextEditorLanguageChanged(object? sender, EventArgs e)
    {
        // Keep toolbar ViewModel in sync with editor language
        if (_toolbarViewModel.SelectedLanguage != TextEditor.Language)
            _toolbarViewModel.SelectedLanguage = TextEditor.Language;

        // Don't save if we're loading content - only save actual user changes
        if (_isLoadingContent)
            return;

        if (_textFormatClipDataId == null || TextEditor.IsReadOnly)
            return;

        _textEditorLanguageDirty = true;
        // Save immediately when user changes language (no debounce)
        _ = SaveTextEditorAsync();
        _logger.LogInformation("[ClipViewer] Text editor language changed by user to: {TextEditorLanguage}", TextEditor.Language);
    }

    #endregion

    #region Save Methods

    public async Task SaveTextEditorAsync()
    {
        if (_textFormatClipDataId == null)
        {
            _logger.LogWarning("Cannot save text editor - no ClipData ID tracked");
            return;
        }

        try
        {
            var textChanged = _textEditorTextDirty;
            var languageChanged = _textEditorLanguageDirty;

            // Early exit if nothing changed
            if (!textChanged && !languageChanged)
            {
                _logger.LogDebug("[ClipViewer] No changes to save for text editor");
                return;
            }

            var newText = TextEditor.Text;
            var newLanguage = TextEditor.Language;

            // Get database context for the current clip
            if (string.IsNullOrEmpty(_currentDatabaseKey))
                throw new InvalidOperationException("No database key available for current clip");

            // Create repositories using factory
            var blobRepository = _databaseContextFactory.GetBlobRepository(_currentDatabaseKey);
            var monacoStateRepository = _databaseContextFactory.GetMonacoEditorStateRepository(_currentDatabaseKey);
            var clipRepository = _databaseContextFactory.GetClipRepository(_currentDatabaseKey);

            // Save text content to BlobTxt only if text changed
            if (textChanged && _textBlobs.TryGetValue(_textFormatClipDataId.Value, out var blob))
            {
                blob.Data = newText;
                await blobRepository.UpdateTextAsync(blob);
                _logger.LogDebug("[ClipViewer] Saved text content ({TextLength} chars)", newText.Length);

                // Update clip title if AutoChangeClipTitles is enabled AND CustomTitle is false
                // If CustomTitle=true, the user manually renamed the clip and title should never auto-update
                if (_configurationService.Configuration.Preferences.AutoChangeClipTitles)
                {
                    var clipData = _currentClipData.FirstOrDefault(p => p.Id == _textFormatClipDataId.Value);
                    if (clipData != null)
                    {
                        var clip = await clipRepository.GetByIdAsync(clipData.ClipId);
                        if (clip is { CustomTitle: false })
                        {
                            var newTitle = GenerateClipTitle(newText);
                            if (clip.Title != newTitle)
                            {
                                clip.Title = newTitle;
                                await clipRepository.UpdateAsync(clip);
                                _logger.LogInformation("[ClipViewer] Updated clip title to: {Title}", newTitle);
                                _messenger.Send(new ClipUpdatedMessage(clip.Id, newTitle));
                            }
                        }
                        else if (clip?.CustomTitle == true)
                            _logger.LogDebug("[ClipViewer] Skipped title update - CustomTitle flag is set");
                    }
                }
            }

            // Save language and view state to MonacoEditorState
            var editorState = await monacoStateRepository.GetByClipDataIdAsync(_textFormatClipDataId.Value)
                              ?? new MonacoEditorState
                              {
                                  Id = Guid.NewGuid(),
                                  ClipDataId = _textFormatClipDataId.Value,
                              };

            if (languageChanged)
            {
                editorState.Language = newLanguage;
                _logger.LogDebug("[ClipViewer] Saved language: {Language}", newLanguage);
            }

            // Only fetch view state if text changed (cursor/scroll might have moved)
            // If only language changed, skip the round trip to Monaco
            if (textChanged)
            {
                try
                {
                    var viewState = await TextEditor.SaveViewStateAsync();
                    if (!string.IsNullOrEmpty(viewState))
                    {
                        editorState.ViewState = viewState;
                        _logger.LogDebug("[ClipViewer] Saved view state ({ViewStateLength} chars)", viewState.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save view state for text editor");
                }
            }
            else
                _logger.LogDebug("[ClipViewer] Skipped view state fetch (language-only change)");

            editorState.LastModified = DateTime.UtcNow;
            await monacoStateRepository.UpsertAsync(editorState);

            // Clear dirty flags
            _textEditorTextDirty = false;
            _textEditorLanguageDirty = false;

            _logger.LogInformation("[ClipViewer] Saved text editor changes (text: {TextChanged}, language: {LanguageChanged})",
                textChanged, languageChanged);

            // Update clipboard with the edited content if text changed
            if (textChanged)
                await UpdateClipboardAfterEditAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving text editor changes");
            MessageBox.Show($"Error saving text: {ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Wrapper for SaveTextEditorAsync that marshals to UI thread.
    /// Called by debouncer which executes on background thread.
    /// </summary>
    private void SaveTextEditorDebounced() => Dispatcher.InvokeAsync(async () => await SaveTextEditorAsync());

    /// <summary>
    /// Updates the Windows clipboard with the currently edited clip's content.
    /// Called after saving changes to ensure clipboard reflects the latest edits.
    /// </summary>
    private async Task UpdateClipboardAfterEditAsync()
    {
        try
        {
            if (!ClipId.HasValue)
            {
                _logger.LogWarning("[ClipViewer] Cannot update clipboard - no clip ID");
                return;
            }

            if (string.IsNullOrEmpty(_currentDatabaseKey))
            {
                _logger.LogWarning("[ClipViewer] Cannot update clipboard - no database key");
                return;
            }

            // Use the centralized service method to load and set clipboard
            await _clipService.LoadAndSetClipboardAsync(_currentDatabaseKey, ClipId.Value);
            _logger.LogInformation("[ClipViewer] Updated clipboard after editing clip: {ClipId}", ClipId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClipViewer] Failed to update clipboard after edit");
            // Don't show error to user - clipboard update failure shouldn't interrupt editing workflow
        }
    }

    #endregion

    #region Helper Methods

    private async Task WaitForMonacoInitializationAsync(MonacoEditorControl editor, int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        var editorName = ReferenceEquals(editor, TextEditor)
            ? "TextEditor"
            : "HtmlEditor";

        _logger.LogTrace("Waiting for {EditorName} initialization - IsInitialized: {IsInitialized}", editorName, editor.IsInitialized);

        if (editor.IsInitialized)
        {
            _logger.LogTrace("{EditorName} already initialized", editorName);
            return;
        }

        var startTime = DateTime.UtcNow;
        var attempts = 0;
        while (!editor.IsInitialized && (DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            await Task.Delay(50, cancellationToken);
        }

        if (!editor.IsInitialized)
            _logger.LogWarning("{EditorName} failed to initialize within {TimeoutMs}ms after {Attempts} attempts", editorName, timeoutMs, attempts);
        else
        {
            _logger.LogTrace("{EditorName} initialized successfully after {Attempts} attempts ({ElapsedMs}ms)",
                editorName, attempts, (DateTime.UtcNow - startTime).TotalMilliseconds);
        }
    }

    private void ResetAllTabs()
    {
        // Hide all tabs first to ensure clean state
        HideAllTabs();

        // Reset editor - if not initialized, SetTextAsync will return early
        TextEditor.Text = string.Empty;
        // Don't reset HtmlPreview here - it will be initialized when HTML tab loads
        RtfViewer.CreateNewDocument();
        BitmapViewer.Source = null;
        PictureViewer.Source = null;
        HexViewer.Stream = null!;
        _binaryFormats.Clear();
        BinaryFormatComboBox.Items.Clear();
    }

    private void HideAllTabs()
    {
        TextTab.Visibility = Visibility.Collapsed;
        HtmlTab.Visibility = Visibility.Collapsed;
        RtfTab.Visibility = Visibility.Collapsed;
        BitmapTab.Visibility = Visibility.Collapsed;
        PictureTab.Visibility = Visibility.Collapsed;
        BinaryTab.Visibility = Visibility.Collapsed;
    }

    private void SelectFirstAvailableTab()
    {
        // Try to respect user's preferred default view first
        var preferences = _configurationService.Configuration.Preferences;
        var defaultView = preferences.DefaultEditorView;

        // First, try the user's preferred view
        switch (defaultView)
        {
            case EditorViewType.Text:
            case EditorViewType.Unicode:
                if (TextTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = TextTab;
                    return;
                }

                break;
            case EditorViewType.Html:
                if (HtmlTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = HtmlTab;
                    return;
                }

                break;
            case EditorViewType.Rtf:
                if (RtfTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = RtfTab;
                    return;
                }

                break;
            case EditorViewType.Picture:
                if (PictureTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = PictureTab;
                    return;
                }

                break;
            case EditorViewType.Bitmap:
                if (BitmapTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = BitmapTab;
                    return;
                }

                break;
            case EditorViewType.Binary:
                if (BinaryTab.Visibility == Visibility.Visible)
                {
                    FormatTabControl.SelectedItem = BinaryTab;
                    return;
                }

                break;
        }

        // Fallback: select first available in priority order (Text > HTML > RTF > Picture > Bitmap > Binary)
        if (TextTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = TextTab;
        else if (HtmlTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = HtmlTab;
        else if (RtfTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = RtfTab;
        else if (PictureTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = PictureTab;
        else if (BitmapTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = BitmapTab;
        else if (BinaryTab.Visibility == Visibility.Visible)
            FormatTabControl.SelectedItem = BinaryTab;
    }

    /// <summary>
    /// Applies editor settings from preferences configuration.
    /// </summary>
    private void ApplyEditorSettings()
    {
        var preferences = _configurationService.Configuration.Preferences;

        // Apply Binary tab visibility
        BinaryTab.Visibility = preferences.EnableBinaryView
            ? Visibility.Visible
            : Visibility.Collapsed;

        _logger.LogDebug("[ClipViewer] Applied editor settings - BinaryTab visible: {BinaryVisible}",
            preferences.EnableBinaryView);
    }

    /// <summary>
    /// Sets the default active tab based on preferences.
    /// </summary>
    private void SetDefaultActiveTab()
    {
        // SelectFirstAvailableTab now handles both preference and fallback logic
        SelectFirstAvailableTab();

        var preferences = _configurationService.Configuration.Preferences;
        _logger.LogDebug("[ClipViewer] Set active tab based on preference: {DefaultView}", preferences.DefaultEditorView);
    }

    /// <summary>
    /// Generates a clip title from text content (first line, max 100 characters).
    /// </summary>
    private static string GenerateClipTitle(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Empty Clip";

        // Get first line
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0
            ? lines[0].Trim()
            : text.Trim();

        // Limit to 100 characters
        if (firstLine.Length > 100)
            firstLine = string.Concat(firstLine.AsSpan(0, 100), "...");

        return firstLine;
    }

    private BitmapImage BytesToBitmapImage(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating BitmapImage from {Size} bytes", data.Length);
            throw;
        }
    }

    private static string FormatBytes(int bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    #endregion

    #region Toolbar Command Wiring

    private void WireToolbarCommands()
    {
        // Subscribe to toolbar ViewModel property changes
        _toolbarViewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(_toolbarViewModel.IsWordWrapEnabled))
                await TextEditor.SetWordWrapAsync(_toolbarViewModel.IsWordWrapEnabled);
            else if (e.PropertyName == nameof(_toolbarViewModel.ShowNonPrintingCharacters))
                await TextEditor.SetRenderWhitespaceAsync(_toolbarViewModel.ShowNonPrintingCharacters);
        };

        // Wire up toolbar command handlers
        _toolbarViewModel.OnNewClipRequested = () => Dispatcher.Invoke(HandleNewClip);
        _toolbarViewModel.OnCutRequested = () => Dispatcher.Invoke(async () => await HandleCutAsync());
        _toolbarViewModel.OnCopyRequested = () => Dispatcher.Invoke(async () => await HandleCopyAsync());
        _toolbarViewModel.OnPasteRequested = () => Dispatcher.Invoke(async () => await HandlePasteAsync());
        _toolbarViewModel.OnRemoveLineBreaksRequested = mode => Dispatcher.Invoke(async () => await HandleRemoveLineBreaksAsync(mode));
        _toolbarViewModel.OnConvertCaseRequested = caseType => Dispatcher.Invoke(async () => await HandleConvertCaseAsync(caseType));
        _toolbarViewModel.OnTrimRequested = () => Dispatcher.Invoke(async () => await HandleTrimAsync());
        _toolbarViewModel.OnOpenTextCleanupDialogRequested = () => Dispatcher.Invoke(HandleOpenTextCleanupDialog);
        _toolbarViewModel.OnUndoRequested = () => Dispatcher.Invoke(async () => await HandleUndoAsync());
        _toolbarViewModel.OnFindRequested = () => Dispatcher.Invoke(async () => await HandleFindAsync());
        _toolbarViewModel.OnShowHelpRequested = () => Dispatcher.Invoke(HandleShowHelp);
        _toolbarViewModel.OnLanguageChanged = language => Dispatcher.Invoke(() => TextEditor.Language = language);
    }

    private void HandleNewClip()
    {
        try
        {
            TextEditor.Text = string.Empty;
            _logger.LogDebug("New clip created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new clip");
        }
    }

    private async Task HandleCutAsync()
    {
        try
        {
            var selectedText = await TextEditor.GetSelectedTextAsync();
            if (!string.IsNullOrEmpty(selectedText))
            {
                Clipboard.SetText(selectedText);
                await TextEditor.ReplaceSelectionAsync(string.Empty);
                _logger.LogDebug("Cut {Length} characters", selectedText.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cutting text");
        }
    }

    private async Task HandleCopyAsync()
    {
        try
        {
            var selectedText = await TextEditor.GetSelectedTextAsync();
            if (!string.IsNullOrEmpty(selectedText))
            {
                Clipboard.SetText(selectedText);
                _logger.LogDebug("Copied {Length} characters", selectedText.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying text");
        }
    }

    private async Task HandlePasteAsync()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                await TextEditor.ReplaceSelectionAsync(text);
                _logger.LogDebug("Pasted {Length} characters", text.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pasting text");
        }
    }

    private async Task HandleRemoveLineBreaksAsync(string mode)
    {
        try
        {
            var text = await TextEditor.GetTextAsync();
            if (string.IsNullOrEmpty(text))
                return;

            var lineBreakMode = mode switch
            {
                "PreserveParagraphs" => LineBreakMode.PreserveParagraphs,
                "RemoveAll" => LineBreakMode.RemoveAll,
                "UrlCrunch" => LineBreakMode.UrlCrunch,
                var _ => LineBreakMode.PreserveParagraphs,
            };

            var result = _textTransformService.RemoveLineBreaks(text, lineBreakMode);
            await TextEditor.SetTextAsync(result);
            _logger.LogDebug("Removed line breaks with mode: {Mode}", mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing line breaks");
        }
    }

    private async Task HandleConvertCaseAsync(string caseType)
    {
        try
        {
            var selectedText = await TextEditor.GetSelectedTextAsync();
            var text = string.IsNullOrEmpty(selectedText)
                ? await TextEditor.GetTextAsync()
                : selectedText;

            if (string.IsNullOrEmpty(text))
                return;

            var conversion = caseType switch
            {
                "Uppercase" => CaseConversion.Uppercase,
                "Lowercase" => CaseConversion.Lowercase,
                "TitleCase" => CaseConversion.TitleCase,
                "SentenceCase" => CaseConversion.SentenceCase,
                "InvertCase" => CaseConversion.InvertCase,
                var _ => CaseConversion.Uppercase,
            };

            var result = _textTransformService.ConvertCase(text, conversion);

            if (string.IsNullOrEmpty(selectedText))
                await TextEditor.SetTextAsync(result);
            else
                await TextEditor.ReplaceSelectionAsync(result);

            _logger.LogDebug("Converted case to: {CaseType}", caseType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting case");
        }
    }

    private async Task HandleTrimAsync()
    {
        try
        {
            var text = await TextEditor.GetTextAsync();
            if (string.IsNullOrEmpty(text))
                return;

            var result = _textTransformService.TrimText(text);
            await TextEditor.SetTextAsync(result);
            _logger.LogDebug("Trimmed text");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trimming text");
        }
    }

    private async void HandleOpenTextCleanupDialog()
    {
        try
        {
            // Get current text from editor
            var currentText = await TextEditor.GetTextAsync();

            if (string.IsNullOrEmpty(currentText))
            {
                MessageBox.Show("No text to clean up.", "Text Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create and show dialog on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                // Get dialog from DI container
                var app = (App)Application.Current;
                var dialog = app.ServiceProvider.GetRequiredService<TextCleanupDialog>();

                // Set owner to prevent focus issues
                dialog.Owner = Application.Current.GetDialogOwner();

                // Set input text
                dialog.SetInputText(currentText);

                // Show dialog and get result
                var result = dialog.ShowDialog();

                if (result == true && !string.IsNullOrEmpty(dialog.ResultText))
                {
                    // Apply cleaned text to editor
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await TextEditor.SetTextAsync(dialog.ResultText);
                        _logger.LogInformation("Text cleanup applied - {Length} characters", dialog.ResultText.Length);
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening Text Cleanup dialog");
            MessageBox.Show($"Error opening Text Cleanup dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task HandleUndoAsync()
    {
        try
        {
            await TextEditor.TriggerUndoAsync();
            _logger.LogDebug("Undo triggered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering undo");
        }
    }

    private async Task HandleFindAsync()
    {
        try
        {
            await TextEditor.TriggerFindAsync();
            _logger.LogDebug("Find dialog triggered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering find");
        }
    }

    private void HandleShowHelp()
    {
        _logger.LogDebug("Help not yet implemented");
        MessageBox.Show("ClipViewer help will be available in the user manual.", "Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [GeneratedRegex(@"<!--SourceURL:\s*(.+?)-->")]
    private static partial Regex SourceUrlRegEx();

    [GeneratedRegex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
    private static partial Regex ScriptTagRegEx();

    [GeneratedRegex(@"javascript:\s*[^""'\s>]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex JavascriptUrlRegEx();

    [GeneratedRegex(@"\s+on\w+\s*=\s*[""'].*?[""']", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex InlineEventHandlerQuotedRegex();

    [GeneratedRegex(@"\s+on\w+\s*=\s*[^\s>]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex InlineEventHandlerUnquotedRegex();

    #endregion
}
