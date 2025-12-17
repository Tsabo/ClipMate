using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
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

        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _logger = serviceProvider.GetRequiredService<ILogger<ClipViewerControl>>();
        _messenger = serviceProvider.GetRequiredService<IMessenger>();
        _configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
        _textTransformService = serviceProvider.GetRequiredService<ITextTransformService>();

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
        HtmlEditor.EditorOptions = monacoOptions;

        // Register for ClipSelectedEvent and PreferencesChangedEvent messages
        Loaded += (_, _) =>
        {
            _messenger.Register<ClipSelectedEvent>(this);
            _messenger.Register<PreferencesChangedEvent>(this);

            // Apply initial editor settings
            ApplyEditorSettings();

            // Set up debounced auto-save (1 second after last change)
            _textEditorSaveTimer = new Timer(1000) { AutoReset = false };
            _textEditorSaveTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(async () => await SaveTextEditorAsync());

            _htmlEditorSaveTimer = new Timer(1000) { AutoReset = false };
            _htmlEditorSaveTimer.Elapsed += async (_, _) => await Dispatcher.InvokeAsync(async () => await SaveHtmlEditorAsync());

            // Monitor Text property changes on editors
            var textDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            textDescriptor?.AddValueChanged(TextEditor, OnTextEditorTextChanged);

            var htmlTextDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            htmlTextDescriptor?.AddValueChanged(HtmlEditor, OnHtmlEditorTextChanged);

            // Monitor Language property changes
            var textLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            textLangDescriptor?.AddValueChanged(TextEditor, OnTextEditorLanguageChanged);

            var htmlLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            htmlLangDescriptor?.AddValueChanged(HtmlEditor, OnHtmlEditorLanguageChanged);
        };

        Unloaded += (_, _) =>
        {
            _messenger.Unregister<ClipSelectedEvent>(this);
            _messenger.Unregister<PreferencesChangedEvent>(this);

            // Clean up timers
            _textEditorSaveTimer?.Dispose();
            _htmlEditorSaveTimer?.Dispose();

            // Remove value change handlers
            var textDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            textDescriptor?.RemoveValueChanged(TextEditor, OnTextEditorTextChanged);

            var htmlTextDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.TextProperty, typeof(MonacoEditorControl));
            htmlTextDescriptor?.RemoveValueChanged(HtmlEditor, OnHtmlEditorTextChanged);

            var textLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            textLangDescriptor?.RemoveValueChanged(TextEditor, OnTextEditorLanguageChanged);

            var htmlLangDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.LanguageProperty, typeof(MonacoEditorControl));
            htmlLangDescriptor?.RemoveValueChanged(HtmlEditor, OnHtmlEditorLanguageChanged);
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
            await control.LoadClipDataAsync(clipId);
        }
        else
        {
            // No clip selected - hide all tabs
            control.HideAllTabs();
        }
    }

    #endregion

    #region Messenger Event Handlers

    /// <summary>
    /// Receives ClipSelectedEvent messages from the messenger.
    /// </summary>
    public void Receive(ClipSelectedEvent message)
    {
        var clipId = message.SelectedClip?.Id;
        _logger.LogInformation("[ClipViewer] Received ClipSelectedEvent - ClipId: {ClipId}", clipId);
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

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ClipViewerControl> _logger;
    private readonly IMessenger _messenger;
    private readonly IConfigurationService _configurationService;
    private readonly ClipViewerToolbarViewModel _toolbarViewModel;
    private readonly ITextTransformService _textTransformService;

    private List<ClipData> _currentClipData = [];
    private Dictionary<Guid, BlobTxt> _textBlobs = [];
    private Dictionary<Guid, BlobJpg> _jpgBlobs = [];
    private Dictionary<Guid, BlobPng> _pngBlobs = [];
    private Dictionary<Guid, BlobBlob> _binaryBlobs = [];
    private readonly Dictionary<string, byte[]> _binaryFormats = [];

    // Track which ClipData entries are being edited
    private Guid? _textFormatClipDataId;
    private Guid? _htmlFormatClipDataId;

    // Track when we're loading content (not editing)
    private bool _isLoadingContent;

    // Track what changed to optimize saves
    private bool _textEditorTextDirty;
    private bool _textEditorLanguageDirty;
    private bool _htmlEditorTextDirty;
    private bool _htmlEditorLanguageDirty;

    // Timers for debounced auto-save
    private Timer? _textEditorSaveTimer;
    private Timer? _htmlEditorSaveTimer;

    #endregion

    #region Data Loading

    private async Task LoadClipDataAsync(Guid clipId)
    {
        IsLoading = true;
        _isLoadingContent = true; // Suppress save operations during load

        // Reset dirty flags for new clip
        _textEditorTextDirty = false;
        _textEditorLanguageDirty = false;
        _htmlEditorTextDirty = false;
        _htmlEditorLanguageDirty = false;

        _logger.LogInformation("[ClipViewer] LoadClipDataAsync START - ClipId: {ClipId}", clipId);

        try
        {
            // Create scope to access scoped repositories
            using var scope = _serviceScopeFactory.CreateScope();
            var clipDataRepository = scope.ServiceProvider.GetRequiredService<IClipDataRepository>();
            var blobRepository = scope.ServiceProvider.GetRequiredService<IBlobRepository>();
            var monacoStateRepository = scope.ServiceProvider.GetRequiredService<IMonacoEditorStateRepository>();

            // Load all ClipData entries for this clip
            _currentClipData = (await clipDataRepository.GetByClipIdAsync(clipId))
                .OrderBy(cd => cd.FormatName)
                .ToList();

            _logger.LogInformation("[ClipViewer] Found {Count} ClipData entries", _currentClipData.Count);

            if (_currentClipData.Count == 0)
            {
                _logger.LogWarning("No ClipData entries found for ClipId: {ClipId}", clipId);
                HideAllTabs();
                return;
            }

            // Load all BLOBs upfront
            var textBlobs = await blobRepository.GetTextByClipIdAsync(clipId);
            var jpgBlobs = await blobRepository.GetJpgByClipIdAsync(clipId);
            var pngBlobs = await blobRepository.GetPngByClipIdAsync(clipId);
            var binaryBlobs = await blobRepository.GetBlobByClipIdAsync(clipId);

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
            await LoadTextFormatsAsync(monacoStateRepository);
            await LoadHtmlFormatAsync(monacoStateRepository);
            await LoadRtfFormatAsync();
            await LoadBitmapFormatsAsync();
            await LoadPictureFormatsAsync();
            await LoadBinaryFormatsAsync();
            _logger.LogInformation("[ClipViewer] All formats loaded for ClipId: {ClipId}", clipId);

            // Select default tab based on preferences
            SetDefaultActiveTab();
        } // scope is disposed here
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading clip data for ClipId: {ClipId}", clipId);
            MessageBox.Show($"Error loading clip data: {ex.Message}", "Load Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            _isLoadingContent = false; // Re-enable save operations
        }
    }

    private async Task LoadTextFormatsAsync(IMonacoEditorStateRepository monacoStateRepository)
    {
        // Look for text formats (CF_TEXT or CF_UNICODETEXT)
        var textFormat = _currentClipData
            .FirstOrDefault(p =>
                p is { StorageType: StorageType.Text } &&
                (p.Format == Formats.Text.Code || p.Format == Formats.UnicodeText.Code));

        if (textFormat != null && _textBlobs.TryGetValue(textFormat.Id, out var blobData))
        {
            if (blobData?.Data != null)
            {
                // Wait for Monaco to initialize before setting text
                await WaitForMonacoInitializationAsync(TextEditor);

                // Load saved language and view state from MonacoEditorState
                var editorState = await monacoStateRepository.GetByClipDataIdAsync(textFormat.Id);
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
        }

        // No text format available
        TextEditor.Visibility = Visibility.Collapsed;
        NoTextMessage.Visibility = Visibility.Visible;
        TextTab.Visibility = Visibility.Collapsed;
    }

    private async Task LoadHtmlFormatAsync(IMonacoEditorStateRepository monacoStateRepository)
    {
        var htmlFormat = _currentClipData
            .FirstOrDefault(p =>
                (p.Format == Formats.Html.Code || p.Format == Formats.HtmlAlt.Code) && p.StorageType == StorageType.Text);

        if (htmlFormat != null && _textBlobs.TryGetValue(htmlFormat.Id, out var blobData))
        {
            if (blobData?.Data != null)
            {
                // Wait for Monaco to initialize and set HTML source
                await WaitForMonacoInitializationAsync(HtmlEditor);

                // Load saved language and view state from MonacoEditorState
                var editorState = await monacoStateRepository.GetByClipDataIdAsync(htmlFormat.Id);
                var savedLanguage = editorState?.Language ?? "html";

                // Load everything in one atomic operation
                await HtmlEditor.LoadContentAsync(blobData.Data, savedLanguage, editorState?.ViewState);

                HtmlEditor.IsReadOnly = false; // Allow editing

                // Initialize WebView2 if needed for preview
                await HtmlPreview.EnsureCoreWebView2Async();
                HtmlPreview.NavigateToString(blobData.Data);

                HtmlPreview.Visibility = Visibility.Visible;
                HtmlEditor.Visibility = Visibility.Collapsed;
                NoHtmlMessage.Visibility = Visibility.Collapsed;
                HtmlTab.Visibility = Visibility.Visible;

                // Track the ClipData ID for saving changes
                _htmlFormatClipDataId = htmlFormat.Id;
                return;
            }
        }

        // No HTML format available
        HtmlPreview.Visibility = Visibility.Collapsed;
        HtmlEditor.Visibility = Visibility.Collapsed;
        NoHtmlMessage.Visibility = Visibility.Visible;
        HtmlTab.Visibility = Visibility.Collapsed;
    }

    private Task LoadRtfFormatAsync()
    {
        var rtfFormat = _currentClipData
            .FirstOrDefault(p => p.FormatName?.Contains("RTF", StringComparison.OrdinalIgnoreCase) == true &&
                                 p.StorageType == StorageType.Text);

        if (rtfFormat != null && _textBlobs.TryGetValue(rtfFormat.Id, out var blobData))
        {
            if (blobData?.Data != null)
            {
                try
                {
                    // Convert RTF string to FlowDocument
                    var flowDocument = new FlowDocument();
                    var textRange = new TextRange(flowDocument.ContentStart, flowDocument.ContentEnd);

                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(blobData.Data));
                    textRange.Load(stream, DataFormats.Rtf);

                    RtfViewer.Document = flowDocument;
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
        }

        // No RTF format available
        RtfViewer.Visibility = Visibility.Collapsed;
        NoRtfMessage.Visibility = Visibility.Visible;
        RtfTab.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    private Task LoadBitmapFormatsAsync()
    {
        // Look for CF_BITMAP or CF_DIB formats (stored in BLOBBLOB)
        var bitmapFormat = _currentClipData
            .FirstOrDefault(p =>
                (p.Format == Formats.Bitmap.Code || p.Format == Formats.Dib.Code) && p.StorageType == StorageType.Binary);

        if (bitmapFormat != null && _binaryBlobs.TryGetValue(bitmapFormat.Id, out var blobData))
        {
            if (blobData?.Data != null)
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
        }

        // No bitmap format available
        BitmapViewer.Visibility = Visibility.Collapsed;
        NoBitmapMessage.Visibility = Visibility.Visible;
        BitmapTab.Visibility = Visibility.Collapsed;
        return Task.CompletedTask;
    }

    private Task LoadPictureFormatsAsync()
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
            imageData = pngBlob?.Data;
            _logger.LogDebug("[LoadPicture] Retrieved PNG data: {Size} bytes", imageData?.Length ?? 0);
        }
        else if (jpgFormat != null && _jpgBlobs.TryGetValue(jpgFormat.Id, out var jpgBlob))
        {
            imageData = jpgBlob?.Data;
            _logger.LogDebug("[LoadPicture] Retrieved JPG data: {Size} bytes", imageData?.Length ?? 0);
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

    private Task LoadBinaryFormatsAsync()
    {
        _binaryFormats.Clear();
        BinaryFormatComboBox.Items.Clear();

        // Load all available formats into binary viewer
        foreach (var clipData in _currentClipData)
        {
            var data = clipData.StorageType switch
            {
                StorageType.Text when _textBlobs.TryGetValue(clipData.Id, out var txt) =>
                    Encoding.UTF8.GetBytes(txt?.Data ?? ""),
                StorageType.Jpeg when _jpgBlobs.TryGetValue(clipData.Id, out var jpg) =>
                    jpg?.Data,
                StorageType.Png when _pngBlobs.TryGetValue(clipData.Id, out var png) =>
                    png?.Data,
                StorageType.Binary when _binaryBlobs.TryGetValue(clipData.Id, out var blob) =>
                    blob?.Data,
                var _ => null,
            };

            if (data is { Length: > 0 })
            {
                var displayName = $"{clipData.FormatName} ({clipData.Format}) - {FormatBytes(clipData.Size)}";
                _binaryFormats[displayName] = data;
                BinaryFormatComboBox.Items.Add(displayName);
            }
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

    #region Event Handlers

    private void OnHtmlViewChanged(object sender, RoutedEventArgs e)
    {
        // Guard against initialization before controls are loaded
        if (HtmlPreviewRadio == null || HtmlPreview == null || HtmlEditor == null)
            return;

        if (HtmlPreviewRadio.IsChecked == true)
        {
            HtmlPreview.Visibility = Visibility.Visible;
            HtmlEditor.Visibility = Visibility.Collapsed;
        }
        else
        {
            HtmlPreview.Visibility = Visibility.Collapsed;
            HtmlEditor.Visibility = Visibility.Visible;
        }
    }

    private void OnBinaryFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BinaryFormatComboBox.SelectedItem is string formatName &&
            _binaryFormats.TryGetValue(formatName, out var data))
            HexViewer.Stream = new MemoryStream(data);
    }

    #endregion

    #region Editor Change Handlers

    private void OnTextEditorTextChanged(object? sender, EventArgs e)
    {
        // Don't save if we're loading content - only save actual user edits
        if (_isLoadingContent)
            return;

        if (_textFormatClipDataId != null && !TextEditor.IsReadOnly)
        {
            _textEditorTextDirty = true;
            // Restart the debounce timer
            _textEditorSaveTimer?.Stop();
            _textEditorSaveTimer?.Start();
            _logger.LogDebug("[ClipViewer] Text editor text changed by user, save scheduled");
        }
    }

    private void OnHtmlEditorTextChanged(object? sender, EventArgs e)
    {
        // Don't save if we're loading content - only save actual user edits
        if (_isLoadingContent)
            return;

        if (_htmlFormatClipDataId != null && !HtmlEditor.IsReadOnly)
        {
            _htmlEditorTextDirty = true;
            // Restart the debounce timer
            _htmlEditorSaveTimer?.Stop();
            _htmlEditorSaveTimer?.Start();
            _logger.LogDebug("[ClipViewer] HTML editor text changed by user, save scheduled");
        }
    }

    private void OnTextEditorLanguageChanged(object? sender, EventArgs e)
    {
        // Don't save if we're loading content - only save actual user changes
        if (_isLoadingContent)
            return;

        if (_textFormatClipDataId != null && !TextEditor.IsReadOnly)
        {
            _textEditorLanguageDirty = true;
            // Save immediately when user changes language (no debounce)
            _ = SaveTextEditorAsync();
            _logger.LogInformation("[ClipViewer] Text editor language changed by user to: {TextEditorLanguage}", TextEditor.Language);
        }
    }

    private void OnHtmlEditorLanguageChanged(object? sender, EventArgs e)
    {
        // Don't save if we're loading content - only save actual user changes
        if (_isLoadingContent)
            return;

        if (_htmlFormatClipDataId != null && !HtmlEditor.IsReadOnly)
        {
            _htmlEditorLanguageDirty = true;
            // Save immediately when user changes language (no debounce)
            _ = SaveHtmlEditorAsync();
            _logger.LogInformation("[ClipViewer] HTML editor language changed by user to: {HtmlEditorLanguage}", HtmlEditor.Language);
        }
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

            // Create scope to access scoped repositories
            using var scope = _serviceScopeFactory.CreateScope();
            var blobRepository = scope.ServiceProvider.GetRequiredService<IBlobRepository>();
            var monacoStateRepository = scope.ServiceProvider.GetRequiredService<IMonacoEditorStateRepository>();

            // Save text content to BlobTxt only if text changed
            if (textChanged && _textBlobs.TryGetValue(_textFormatClipDataId.Value, out var blob))
            {
                blob.Data = newText;
                await blobRepository.UpdateTextAsync(blob);
                _logger.LogDebug("[ClipViewer] Saved text content ({TextLength} chars)", newText.Length);

                // Update clip title if AutoChangeClipTitles is enabled
                if (_configurationService.Configuration.Preferences.AutoChangeClipTitles)
                {
                    var clipRepository = scope.ServiceProvider.GetRequiredService<IClipRepository>();
                    var clipData = _currentClipData.FirstOrDefault(cd => cd.Id == _textFormatClipDataId.Value);
                    if (clipData != null)
                    {
                        var clip = await clipRepository.GetByIdAsync(clipData.ClipId);
                        if (clip != null)
                        {
                            var newTitle = GenerateClipTitle(newText);
                            if (clip.Title != newTitle)
                            {
                                clip.Title = newTitle;
                                await clipRepository.UpdateAsync(clip);
                                _logger.LogInformation("[ClipViewer] Updated clip title to: {Title}", newTitle);
                            }
                        }
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving text editor changes");
            MessageBox.Show($"Error saving text: {ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task SaveHtmlEditorAsync()
    {
        if (_htmlFormatClipDataId == null)
        {
            _logger.LogWarning("Cannot save HTML editor - no ClipData ID tracked");
            return;
        }

        try
        {
            var textChanged = _htmlEditorTextDirty;
            var languageChanged = _htmlEditorLanguageDirty;

            // Early exit if nothing changed
            if (!textChanged && !languageChanged)
            {
                _logger.LogDebug("[ClipViewer] No changes to save for HTML editor");
                return;
            }

            var newHtml = HtmlEditor.Text;
            var newLanguage = HtmlEditor.Language;

            // Create scope to access scoped repositories
            using var scope = _serviceScopeFactory.CreateScope();
            var blobRepository = scope.ServiceProvider.GetRequiredService<IBlobRepository>();
            var monacoStateRepository = scope.ServiceProvider.GetRequiredService<IMonacoEditorStateRepository>();

            // Save HTML content to BlobTxt only if text changed
            if (textChanged && _textBlobs.TryGetValue(_htmlFormatClipDataId.Value, out var blob))
            {
                blob.Data = newHtml;
                await blobRepository.UpdateTextAsync(blob);
                _logger.LogDebug("[ClipViewer] Saved HTML content ({TextLength} chars)", newHtml.Length);

                // Update preview if visible
                if (HtmlPreview.Visibility == Visibility.Visible)
                    HtmlPreview.NavigateToString(newHtml);

                // Update clip title if AutoChangeClipTitles is enabled
                if (_configurationService.Configuration.Preferences.AutoChangeClipTitles)
                {
                    var clipRepository = scope.ServiceProvider.GetRequiredService<IClipRepository>();
                    var clipData = _currentClipData.FirstOrDefault(cd => cd.Id == _htmlFormatClipDataId.Value);
                    if (clipData != null)
                    {
                        var clip = await clipRepository.GetByIdAsync(clipData.ClipId);
                        if (clip != null)
                        {
                            var newTitle = GenerateClipTitle(newHtml);
                            if (clip.Title != newTitle)
                            {
                                clip.Title = newTitle;
                                await clipRepository.UpdateAsync(clip);
                                _logger.LogInformation("[ClipViewer] Updated clip title to: {Title}", newTitle);
                            }
                        }
                    }
                }
            }

            // Save language and view state to MonacoEditorState
            var editorState = await monacoStateRepository.GetByClipDataIdAsync(_htmlFormatClipDataId.Value)
                              ?? new MonacoEditorState
                              {
                                  Id = Guid.NewGuid(),
                                  ClipDataId = _htmlFormatClipDataId.Value,
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
                    var viewState = await HtmlEditor.SaveViewStateAsync();
                    if (!string.IsNullOrEmpty(viewState))
                    {
                        editorState.ViewState = viewState;
                        _logger.LogDebug("[ClipViewer] Saved view state ({ViewStateLength} chars)", viewState.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save view state for HTML editor");
                }
            }
            else
                _logger.LogDebug("[ClipViewer] Skipped view state fetch (language-only change)");

            editorState.LastModified = DateTime.UtcNow;
            await monacoStateRepository.UpsertAsync(editorState);

            // Clear dirty flags
            _htmlEditorTextDirty = false;
            _htmlEditorLanguageDirty = false;

            _logger.LogInformation("[ClipViewer] Saved HTML editor changes (text: {TextChanged}, language: {LanguageChanged})",
                textChanged, languageChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving HTML editor changes");
            MessageBox.Show($"Error saving HTML: {ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Helper Methods

    private async Task WaitForMonacoInitializationAsync(MonacoEditorControl editor, int timeoutMs = 10000)
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
            attempts++;
            await Task.Delay(50);
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
        // Reset editors - if not initialized, SetTextAsync will return early
        TextEditor.Text = string.Empty;
        HtmlEditor.Text = string.Empty;
        // Don't reset HtmlPreview here - it will be initialized when HTML tab loads
        RtfViewer.Document = new FlowDocument();
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
            firstLine = firstLine.Substring(0, 100) + "...";

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
                dialog.Owner = Window.GetWindow(this);

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

    #endregion
}
