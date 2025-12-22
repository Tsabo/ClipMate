using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Controls;

/// <summary>
/// Monaco Editor control wrapper for WebView2
/// </summary>
public partial class MonacoEditorControl
{
    private const int _maxTextLength = 10_000_000; // 10MB limit. Windows clipboard ~2GB max but Monaco performs well to 10MB

    [GeneratedRegex(@"near\s+\""([^""]+)\""", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ExtractToken();

    public static readonly DependencyProperty EnableDebugProperty =
        DependencyProperty.Register(
            nameof(EnableDebug),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(false, OnEnableDebugChanged));

    private readonly ILogger<MonacoEditorControl> _logger;
    private TaskCompletionSource<bool>? _initializationTcs;
#pragma warning disable CS0649 // Field is assigned via conditional access in message handlers
    private TaskCompletionSource<string>? _pendingCommandResult;
#pragma warning restore CS0649
    private ISearchService? _searchService;
    private bool _suppressLanguageChanged;
    private bool _suppressTextChanged;

    public MonacoEditorControl()
    {
        InitializeComponent();

        // Get logger from application service provider
        _logger = ((App)Application.Current).ServiceProvider.GetRequiredService<ILogger<MonacoEditorControl>>();

        AvailableLanguages = [];
        LanguageComboBox.ItemsSource = AvailableLanguages;

        // Set up WebView2
        EditorWebView.DefaultBackgroundColor = Color.Transparent;
        EditorWebView.NavigationCompleted += OnNavigationCompleted;
    }

    public bool EnableDebug
    {
        get => (bool)GetValue(EnableDebugProperty);
        set => SetValue(EnableDebugProperty, value);
    }

    private static async void OnEnableDebugChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MonacoEditorControl { IsInitialized: true } control)
            return;

        var enabled = (bool)e.NewValue;
        await control.EditorWebView.ExecuteScriptAsync($"setDebugMode({enabled.ToString().ToLower()});");

        if (enabled && control.EditorWebView.CoreWebView2 != null)
            control.EditorWebView.CoreWebView2.OpenDevToolsWindow();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            return;

        try
        {
            // Load Monaco Editor HTML - this triggers NavigationCompleted
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var htmlPath = Path.Combine(baseDir, "Assets", "monaco", "index.html");

            _logger.LogDebug("Loading Monaco Editor from: {HtmlPath}", htmlPath);

            if (!File.Exists(htmlPath))
            {
                _logger.LogError("Monaco Editor index.html not found at: {HtmlPath}", htmlPath);
                throw new FileNotFoundException($"Monaco Editor index.html not found at: {htmlPath}");
            }

            EditorWebView.Source = new Uri(htmlPath);
            _logger.LogDebug("Monaco source set, waiting for NavigationCompleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Monaco Editor");
            MessageBox.Show($"Failed to load Monaco Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _logger.LogDebug("[{ControlName}] NavigationCompleted - IsSuccess: {IsSuccess}", Name ?? "Monaco", e.IsSuccess);

        if (!e.IsSuccess)
        {
            _logger.LogError("[{ControlName}] Monaco navigation failed: {WebErrorStatus}", Name ?? "Monaco", e.WebErrorStatus);
            _initializationTcs?.TrySetResult(false);
            return;
        }

        if (IsInitialized)
        {
            _logger.LogDebug("[{ControlName}] Already initialized, skipping", Name ?? "Monaco");
            return;
        }

        try
        {
            // Create TaskCompletionSource to track initialization
            _initializationTcs = new TaskCompletionSource<bool>();

            // Register WebMessageReceived handler
            _logger.LogDebug("[{ControlName}] Registering WebMessageReceived handler", Name ?? "Monaco");
            EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Enable debug mode if requested
            _logger.LogDebug("[{ControlName}] EnableDebug property value: {EnableDebug}", Name ?? "Monaco", EnableDebug);
            if (EnableDebug)
            {
                await EditorWebView.ExecuteScriptAsync("setDebugMode(true);");
                EditorWebView.CoreWebView2.OpenDevToolsWindow();
                _logger.LogInformation("[{ControlName}] Debug mode enabled, dev tools opened", Name ?? "Monaco");
            }
            else
                _logger.LogDebug("[{ControlName}] Debug mode NOT enabled (EnableDebug = false)", Name ?? "Monaco");

            // Initialize editor using JavaScript function
            var options = EditorOptions ?? new MonacoEditorConfiguration();
            var initScript = $$"""
                               initializeEditor({
                                   value: '',
                                   language: '{{Language}}',
                                   theme: '{{options.Theme}}',
                                   fontSize: {{options.FontSize}},
                                   fontFamily: '{{options.FontFamily}}',
                                   wordWrap: '{{(options.WordWrap ? "on" : "off")}}',
                                   lineNumbers: '{{(options.ShowLineNumbers ? "on" : "off")}}',
                                   minimapEnabled: {{options.ShowMinimap.ToString().ToLower()}},
                                   tabSize: {{options.TabSize}},
                                   smoothScrolling: {{options.SmoothScrolling.ToString().ToLower()}},
                                   readOnly: {{IsReadOnly.ToString().ToLower()}}
                               });
                               """;

            _logger.LogDebug("[{ControlName}] Executing initializeEditor", Name ?? "Monaco");
            var result = await EditorWebView.ExecuteScriptAsync(initScript);
            _logger.LogTrace("[{ControlName}] initializeEditor result: {Result}", Name ?? "Monaco", result);

            // Wait for initialization message (with timeout)
            var initTask = _initializationTcs.Task;
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(initTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("[{ControlName}] Initialization timeout after 5 seconds", Name ?? "Monaco");
                throw new TimeoutException("Monaco editor initialization timed out");
            }

            if (!await initTask)
                throw new InvalidOperationException("Monaco editor initialization returned false");

            // Populate common languages
            AvailableLanguages.Clear();
            var commonLanguages = new[]
            {
                "plaintext", "csharp", "cpp", "css", "html", "java", "javascript",
                "json", "markdown", "php", "python", "sql", "typescript", "xml", "yaml",
            };

            foreach (var item in commonLanguages)
                AvailableLanguages.Add(item);

            IsInitialized = true;
            _logger.LogInformation("[{ControlName}] Monaco editor initialization complete", Name ?? "Monaco");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ControlName}] Monaco editor initialization failed", Name ?? "Monaco");
            _initializationTcs?.TrySetResult(false);
            MessageBox.Show($"Failed to initialize Monaco Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        _logger.LogTrace("[{ControlName}] WebMessage received: {Message}", Name ?? "Monaco", e.WebMessageAsJson);
        try
        {
            var message = JsonDocument.Parse(e.WebMessageAsJson);

            if (!message.RootElement.TryGetProperty("type", out var typeElement))
                return;

            var messageType = typeElement.GetString();

            switch (messageType)
            {
                case "initialized":
                    var success = message.RootElement.TryGetProperty("success", out var successElement) && successElement.GetBoolean();
                    if (!success && message.RootElement.TryGetProperty("error", out var errorElement))
                        _logger.LogError("[{ControlName}] Monaco initialization error: {Error}", Name ?? "Monaco", errorElement.GetString());
                    else
                        _logger.LogDebug("[{ControlName}] Monaco initialization confirmed from JavaScript", Name ?? "Monaco");

                    _initializationTcs?.TrySetResult(success);
                    break;

                case "textChanged":
                    OnTextChangedFromEditor();
                    break;

                case "error":
                    if (message.RootElement.TryGetProperty("message", out var errorMsg))
                        _logger.LogError("[{ControlName}] Monaco Editor JavaScript Error: {ErrorMessage}", Name ?? "Monaco", errorMsg.GetString());

                    break;

                case "optionChanged":
                    // Handle option changes from JavaScript (e.g., word wrap toggle)
                    break;

                case "toggleToolbar":
                    ShowToolbar = !ShowToolbar;
                    break;

                case "validateSql":
                    if (message.RootElement.TryGetProperty("sql", out var sqlElement))
                        _ = ValidateAndUpdateMarkersAsync(sqlElement.GetString() ?? "");

                    break;

                case "result":
                    if (message.RootElement.TryGetProperty("result", out var result))
                        _pendingCommandResult?.TrySetResult(result.GetRawText());

                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing web message");
        }
    }

    private async void OnTextChangedFromEditor()
    {
        _logger.LogTrace("Text changed from editor");
        try
        {
            _suppressTextChanged = true;
            var text = await GetTextAsync();
            _logger.LogTrace("Text retrieved from editor: {TextLength} chars", text.Length);

            if (text == Text)
                return;

            Text = text;

            // Update word/char count
            if (!DisplayWordAndCharacterCounts)
                return;

            var words = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            var chars = text.Length;
            await Dispatcher.InvokeAsync(() =>
            {
                WordCharCountTextBlock.Text = $"{words:N0} words, {chars:N0} characters";
            });
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressTextChanged && IsInitialized && LanguageComboBox.SelectedItem is string language)
            _ = SetLanguageAsync(language);
    }

    #region Helper Classes

    private class CursorPosition
    {
        public int LineNumber { get; set; }
        public int Column { get; set; }
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MonacoEditorControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextPropertyChanged));

    public new static readonly DependencyProperty LanguageProperty =
        DependencyProperty.Register(
            nameof(Language),
            typeof(string),
            typeof(MonacoEditorControl),
            new PropertyMetadata("plaintext", OnLanguagePropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(false, OnIsReadOnlyPropertyChanged));

    public static readonly DependencyProperty EditorOptionsProperty =
        DependencyProperty.Register(
            nameof(EditorOptions),
            typeof(MonacoEditorConfiguration),
            typeof(MonacoEditorControl),
            new PropertyMetadata(null, OnEditorOptionsPropertyChanged));

    public static readonly DependencyProperty IsInitializedProperty =
        DependencyProperty.Register(
            nameof(IsInitialized),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowToolbarProperty =
        DependencyProperty.Register(
            nameof(ShowToolbar),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty DisplayWordAndCharacterCountsProperty =
        DependencyProperty.Register(
            nameof(DisplayWordAndCharacterCounts),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty AvailableLanguagesProperty =
        DependencyProperty.Register(
            nameof(AvailableLanguages),
            typeof(ObservableCollection<string>),
            typeof(MonacoEditorControl),
            new PropertyMetadata(new ObservableCollection<string>()));

    public static readonly DependencyProperty SearchServiceProperty =
        DependencyProperty.Register(
            nameof(SearchService),
            typeof(ISearchService),
            typeof(MonacoEditorControl),
            new PropertyMetadata(null, OnSearchServiceChanged));

    private static void OnSearchServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl control)
            control._searchService = e.NewValue as ISearchService;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public new string Language
    {
        get => (string)GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public MonacoEditorConfiguration? EditorOptions
    {
        get => (MonacoEditorConfiguration?)GetValue(EditorOptionsProperty);
        set => SetValue(EditorOptionsProperty, value);
    }

    public new bool IsInitialized
    {
        get => (bool)GetValue(IsInitializedProperty);
        private set => SetValue(IsInitializedProperty, value);
    }

    public bool ShowToolbar
    {
        get => (bool)GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    public bool DisplayWordAndCharacterCounts
    {
        get => (bool)GetValue(DisplayWordAndCharacterCountsProperty);
        set => SetValue(DisplayWordAndCharacterCountsProperty, value);
    }

    public ObservableCollection<string> AvailableLanguages
    {
        get => (ObservableCollection<string>)GetValue(AvailableLanguagesProperty);
        private set => SetValue(AvailableLanguagesProperty, value);
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl { _suppressTextChanged: false } control)
            _ = control.SetTextAsync((string)e.NewValue);
    }

    private static void OnLanguagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl { IsInitialized: true, _suppressLanguageChanged: false } control)
            _ = control.SetLanguageAsync((string)e.NewValue);
    }

    private static void OnIsReadOnlyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl { IsInitialized: true } control)
            _ = control.UpdateOptionsAsync();
    }

    private static async void OnEditorOptionsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MonacoEditorControl control || e.NewValue is not MonacoEditorConfiguration options)
            return;

        var oldDebug = control.EnableDebug;
        control._logger.LogInformation("[{ControlName}] OnEditorOptionsPropertyChanged - Setting EnableDebug from {OldValue} to {NewValue}",
            control.Name ?? "Monaco", oldDebug, options.EnableDebug);

        control.ShowToolbar = options.ShowToolbar;
        control.DisplayWordAndCharacterCounts = options.DisplayWordAndCharacterCounts;
        control.EnableDebug = options.EnableDebug;

        if (!control.IsInitialized)
            return;

        await control.UpdateOptionsAsync();

        // Handle EnableDebug change at runtime
        if (oldDebug == options.EnableDebug || control.EditorWebView.CoreWebView2 == null)
            return;

        await control.EditorWebView.ExecuteScriptAsync($"setDebugMode({options.EnableDebug.ToString().ToLower()});");

        if (options.EnableDebug)
        {
            control.EditorWebView.CoreWebView2.OpenDevToolsWindow();
            control._logger.LogInformation("[{ControlName}] Debug mode enabled at runtime", control.Name ?? "Monaco");
        }
        else
            control._logger.LogInformation("[{ControlName}] Debug mode disabled at runtime", control.Name ?? "Monaco");
    }

    #endregion

    #region Public Async Methods

    public async Task<string> GetTextAsync()
    {
        if (!IsInitialized)
            return string.Empty;

        var result = await EditorWebView.ExecuteScriptAsync("monacoEditor.getValue()");
        // ExecuteScriptAsync returns JSON string, need to deserialize
        return JsonSerializer.Deserialize<string>(result) ?? string.Empty;
    }

    /// <summary>
    /// Sets text, language, and optionally restores view state in a single atomic operation.
    /// </summary>
    public async Task<bool> LoadContentAsync(string text, string? languageId = null, string? viewStateJson = null)
    {
        var controlName = Name ?? "Monaco";
        _logger.LogDebug("[{ControlName}] LoadContentAsync - Text: {TextLength} chars, Language: {Language}, HasViewState: {HasViewState}, IsInitialized: {IsInitialized}",
            controlName, text.Length, languageId ?? "null", !string.IsNullOrEmpty(viewStateJson), IsInitialized);

        if (!IsInitialized)
        {
            _logger.LogWarning("[{ControlName}] LoadContentAsync failed - editor not initialized", controlName);
            return false;
        }

        languageId ??= "plaintext";

        if (text.Length > _maxTextLength)
        {
            _logger.LogWarning("[{ControlName}] Text exceeds max length ({TextLength} > {MaxLength}), truncating",
                controlName, text.Length, _maxTextLength);

            MessageBox.Show(
                $"Text exceeds maximum length of {_maxTextLength:N0} characters. Text will be truncated.",
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            text = text[.._maxTextLength];
        }

        try
        {
            _suppressTextChanged = true;
            _suppressLanguageChanged = true;

            // Escape text and viewState for JavaScript
            var escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

            string? escapedViewState = null;
            if (!string.IsNullOrEmpty(viewStateJson))
                escapedViewState = viewStateJson.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

            // Call JavaScript function to load content atomically
            var script = $"loadContent(\"{escapedText}\", \"{languageId}\", {(escapedViewState != null ? $"\"{escapedViewState}\"" : "null")});";

            _logger.LogTrace("[{ControlName}] Executing loadContent JavaScript", controlName);
            var result = await EditorWebView.ExecuteScriptAsync(script);
            var success = result == "true";

            if (!success)
            {
                _logger.LogWarning("[{ControlName}] loadContent returned false", controlName);
                return false;
            }

            // Update properties to stay in sync
            Text = text;
            Language = languageId;

            _logger.LogDebug("[{ControlName}] LoadContentAsync completed successfully", controlName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ControlName}] LoadContentAsync failed with exception", controlName);
            return false;
        }
        finally
        {
            _suppressTextChanged = false;
            _suppressLanguageChanged = false;
        }
    }

    public async Task<bool> SetTextAsync(string text) => await LoadContentAsync(text, Language);

    public async Task<string> GetLanguageAsync()
    {
        if (!IsInitialized)
            return "plaintext";

        var result = await EditorWebView.ExecuteScriptAsync("monacoEditor.getModel().getLanguageId()");
        return JsonSerializer.Deserialize<string>(result) ?? "plaintext";
    }

    public async Task SetLanguageAsync(string languageId)
    {
        var controlName = Name ?? "Monaco";

        if (!IsInitialized)
        {
            _logger.LogWarning("[{ControlName}] SetLanguageAsync failed - editor not initialized", controlName);
            return;
        }

        try
        {
            _suppressLanguageChanged = true;
            _logger.LogDebug("[{ControlName}] Setting language to: {LanguageId}", controlName, languageId);

            // Use JavaScript function instead of inline script
            var result = await EditorWebView.ExecuteScriptAsync($"setLanguage('{languageId}');");
            var success = result == "true";

            if (!success)
            {
                _logger.LogWarning("[{ControlName}] setLanguage returned false", controlName);
                return;
            }

            // Update the Language property so UI reflects the change
            Language = languageId;
            _logger.LogTrace("[{ControlName}] SetLanguageAsync completed", controlName);
        }
        finally
        {
            _suppressLanguageChanged = false;
        }
    }

    public async Task UpdateOptionsAsync()
    {
        if (!IsInitialized)
            return;

        var options = EditorOptions ?? new MonacoEditorConfiguration();

        try
        {
            // Update editor options using Monaco's updateOptions API
            var updateScript = $@"
                monacoEditor.updateOptions({{
                    theme: '{options.Theme}',
                    fontSize: {options.FontSize},
                    fontFamily: '{options.FontFamily}',
                    wordWrap: '{(options.WordWrap ? "on" : "off")}',
                    lineNumbers: '{(options.ShowLineNumbers ? "on" : "off")}',
                    minimap: {{ enabled: {options.ShowMinimap.ToString().ToLower()} }},
                    tabSize: {options.TabSize},
                    smoothScrolling: {options.SmoothScrolling.ToString().ToLower()},
                    readOnly: {IsReadOnly.ToString().ToLower()}
                }});
            ";

            await EditorWebView.ExecuteScriptAsync(updateScript);
            _logger.LogDebug("Options updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update options");
        }
    }

    public async Task<string> SaveViewStateAsync()
    {
        var controlName = Name ?? "Monaco";

        if (!IsInitialized)
        {
            _logger.LogWarning("[{ControlName}] SaveViewStateAsync failed - editor not initialized", controlName);
            return string.Empty;
        }

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("saveViewState()");
            if (result == "null")
            {
                _logger.LogTrace("[{ControlName}] SaveViewStateAsync - no view state available", controlName);
                return string.Empty;
            }

            var viewState = JsonSerializer.Deserialize<string>(result) ?? string.Empty;
            _logger.LogTrace("[{ControlName}] SaveViewStateAsync - saved {ViewStateLength} chars", controlName, viewState.Length);
            return viewState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ControlName}] Failed to save view state", controlName);
            return string.Empty;
        }
    }

    public async Task RestoreViewStateAsync(string viewStateJson)
    {
        if (!IsInitialized)
        {
            _logger.LogWarning("RestoreViewStateAsync failed - editor not initialized");
            return;
        }

        if (string.IsNullOrEmpty(viewStateJson))
        {
            _logger.LogTrace("RestoreViewStateAsync - empty view state, skipping");
            return;
        }

        try
        {
            _logger.LogTrace("RestoreViewStateAsync - restoring {ViewStateLength} chars", viewStateJson.Length);
            // Escape the JSON string for JavaScript
            var escapedJson = viewStateJson.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            await EditorWebView.ExecuteScriptAsync($"monacoEditor.restoreViewState(JSON.parse(\"{escapedJson}\"))");
            _logger.LogTrace("RestoreViewStateAsync completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore view state");
        }
    }

    /// <summary>
    /// Gets the currently selected text in the editor.
    /// </summary>
    public async Task<string> GetSelectedTextAsync()
    {
        if (!IsInitialized)
            return string.Empty;

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("getSelectedText()");
            return JsonSerializer.Deserialize<string>(result) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get selected text");
            return string.Empty;
        }
    }

    /// <summary>
    /// Replaces the currently selected text with the provided text.
    /// </summary>
    public async Task<bool> ReplaceSelectionAsync(string text)
    {
        if (!IsInitialized)
            return false;

        try
        {
            var escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            var result = await EditorWebView.ExecuteScriptAsync($"replaceSelection(\"{escapedText}\")");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replace selection");
            return false;
        }
    }

    /// <summary>
    /// Inserts text at the current cursor position.
    /// </summary>
    public async Task<bool> InsertAtCursorAsync(string text)
    {
        if (!IsInitialized)
            return false;

        try
        {
            var escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            var result = await EditorWebView.ExecuteScriptAsync($"insertAtCursor(\"{escapedText}\")");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert at cursor");
            return false;
        }
    }

    /// <summary>
    /// Gets the current cursor position as a JSON object with lineNumber and column.
    /// </summary>
    public async Task<(int lineNumber, int column)> GetCursorPositionAsync()
    {
        if (!IsInitialized)
            return (1, 1);

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("getCursorPosition()");
            var position = JsonSerializer.Deserialize<CursorPosition>(result);
            return (position?.LineNumber ?? 1, position?.Column ?? 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cursor position");
            return (1, 1);
        }
    }

    /// <summary>
    /// Triggers the undo action in the editor.
    /// </summary>
    public async Task<bool> TriggerUndoAsync()
    {
        if (!IsInitialized)
            return false;

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("triggerUndo()");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger undo");
            return false;
        }
    }

    /// <summary>
    /// Triggers the find dialog in the editor.
    /// </summary>
    public async Task<bool> TriggerFindAsync()
    {
        if (!IsInitialized)
            return false;

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("triggerFind()");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger find");
            return false;
        }
    }

    /// <summary>
    /// Sets the word wrap mode for the editor.
    /// </summary>
    public async Task<bool> SetWordWrapAsync(bool enabled)
    {
        if (!IsInitialized)
            return false;

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync($"setWordWrap({enabled.ToString().ToLower()})");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set word wrap");
            return false;
        }
    }

    /// <summary>
    /// Sets whether to render whitespace characters in the editor.
    /// </summary>
    public async Task<bool> SetRenderWhitespaceAsync(bool enabled)
    {
        if (!IsInitialized)
            return false;

        try
        {
            var renderMode = enabled
                ? "all"
                : "none";

            var result = await EditorWebView.ExecuteScriptAsync($"setRenderWhitespace(\"{renderMode}\")");
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set render whitespace");
            return false;
        }
    }

    #endregion

    #region SQL IntelliSense

    /// <summary>
    /// Registers SQL IntelliSense with schema data.
    /// </summary>
    /// <param name="schemaJson">JSON-serialized SqlSchema object.</param>
    public async Task<bool> RegisterSqlIntelliSenseAsync(string schemaJson)
    {
        _logger.LogDebug("[{ControlName}] RegisterSqlIntelliSenseAsync called, IsInitialized: {IsInitialized}", 
            Name ?? "Monaco", IsInitialized);

        if (!IsInitialized)
        {
            _logger.LogWarning("[{ControlName}] Cannot register SQL IntelliSense - editor not initialized", Name ?? "Monaco");
            return false;
        }

        try
        {
            _logger.LogDebug("[{ControlName}] Escaping schema JSON (length: {Length})", Name ?? "Monaco", schemaJson.Length);

            var escapedJson = schemaJson
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            _logger.LogDebug("[{ControlName}] Calling JavaScript registerSqlIntelliSense function", Name ?? "Monaco");

            var result = await EditorWebView.ExecuteScriptAsync($"registerSqlIntelliSense(\"{escapedJson}\");");
            
            _logger.LogInformation("[{ControlName}] SQL IntelliSense registration result: {Result}", Name ?? "Monaco", result);
            
            return result == "true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ControlName}] Failed to register SQL IntelliSense", Name ?? "Monaco");
            return false;
        }
    }

    /// <summary>
    /// Gets or sets the search service for SQL validation.
    /// </summary>
    public ISearchService? SearchService
    {
        get => (ISearchService?)GetValue(SearchServiceProperty);
        set => SetValue(SearchServiceProperty, value);
    }

    /// <summary>
    /// Validates SQL and updates Monaco markers.
    /// </summary>
    private async Task ValidateAndUpdateMarkersAsync(string sql)
    {
        if (_searchService == null)
        {
            _logger.LogWarning("[{ControlName}] SearchService not set, cannot validate SQL", Name ?? "Monaco");
            return;
        }

        try
        {
            _logger.LogDebug("[{ControlName}] Validating SQL: {SqlPreview}", Name ?? "Monaco", 
                sql.Length > 50 ? sql.Substring(0, 50) + "..." : sql);

            var (isValid, errorMessage) = await _searchService.ValidateSqlQueryAsync(sql, CancellationToken.None);

            var markers = new List<object>();
            if (!isValid && !string.IsNullOrEmpty(errorMessage))
            {
                _logger.LogDebug("[{ControlName}] SQL validation failed: {Error}", Name ?? "Monaco", errorMessage);

                // Parse error position from "near token" if present
                var position = ParseErrorPosition(sql, errorMessage);

                markers.Add(new
                {
                    severity = 8, // monaco.MarkerSeverity.Error
                    startLineNumber = position.LineNumber,
                    startColumn = position.Column,
                    endLineNumber = position.LineNumber,
                    endColumn = position.EndColumn,
                    message = errorMessage,
                });
            }
            else
            {
                _logger.LogDebug("[{ControlName}] SQL validation passed", Name ?? "Monaco");
            }

            var markersJson = JsonSerializer.Serialize(markers);
            var escapedJson = markersJson
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            await EditorWebView.ExecuteScriptAsync($"setValidationMarkers(JSON.parse(\"{escapedJson}\"));");
            _logger.LogDebug("[{ControlName}] Validation markers updated: {IsValid}, MarkerCount: {Count}", 
                Name ?? "Monaco", isValid, markers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ControlName}] Failed to validate SQL and update markers", Name ?? "Monaco");
        }
    }

    /// <summary>
    /// Parses SQLite error position from "near token" message.
    /// </summary>
    private (int LineNumber, int Column, int EndColumn) ParseErrorPosition(string sql, string errorMessage)
    {
        try
        {
            // Extract token from "near \"TOKEN\"" pattern
            var match = ExtractToken().Match(errorMessage);
            if (!match.Success)
            {
                // Default to start of text
                return (1, 1, sql.Length > 0
                    ? sql.Split('\n')[0].Length + 1
                    : 100);
            }

            var token = match.Groups[1].Value;
            var tokenIndex = sql.IndexOf(token, StringComparison.OrdinalIgnoreCase);

            if (tokenIndex == -1)
                return (1, 1, sql.Length > 0
                    ? sql.Split('\n')[0].Length + 1
                    : 100);

            // Calculate line and column from character index
            var lines = sql.Substring(0, tokenIndex).Split('\n');
            var lineNumber = lines.Length;
            var column = lines[^1].Length + 1;
            var endColumn = column + token.Length;

            return (lineNumber, column, endColumn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse error position");
            return (1, 1, 100);
        }
    }

    #endregion
}
