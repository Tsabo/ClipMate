using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClipMate.Core.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Controls;

/// <summary>
///     Monaco Editor control wrapper for WebView2
/// </summary>
public partial class MonacoEditorControl
{
    private const int _maxTextLength = 10_000_000; // 10MB limit. Windows clipboard ~2GB max but Monaco performs well to 10MB

    public static readonly DependencyProperty EnableDebugProperty =
        DependencyProperty.Register(
            nameof(EnableDebug),
            typeof(bool),
            typeof(MonacoEditorControl),
            new PropertyMetadata(false, OnEnableDebugChanged));

    private readonly ILogger<MonacoEditorControl> _logger;
    private TaskCompletionSource<bool>? _initializationTcs;
    private TaskCompletionSource<string>? _pendingCommandResult;
    private bool _suppressLanguageChanged;
    private bool _suppressTextChanged;

    public MonacoEditorControl()
    {
        InitializeComponent();

        // Get logger from application service provider
        _logger = ((App)Application.Current).ServiceProvider.GetRequiredService<ILogger<MonacoEditorControl>>();

        AvailableLanguages = new ObservableCollection<string>();
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
            {
                _logger.LogDebug("[{ControlName}] Debug mode NOT enabled (EnableDebug = false)", Name ?? "Monaco");
            }

            // Initialize editor using JavaScript function
            var options = EditorOptions ?? new MonacoEditorConfiguration();
            var initScript = $@"
                initializeEditor({{
                    value: '',
                    language: '{Language ?? "plaintext"}',
                    theme: '{options.Theme}',
                    fontSize: {options.FontSize},
                    fontFamily: '{options.FontFamily}',
                    wordWrap: '{(options.WordWrap ? "on" : "off")}',
                    lineNumbers: '{(options.ShowLineNumbers ? "on" : "off")}',
                    minimapEnabled: {options.ShowMinimap.ToString().ToLower()},
                    tabSize: {options.TabSize},
                    smoothScrolling: {options.SmoothScrolling.ToString().ToLower()},
                    readOnly: {IsReadOnly.ToString().ToLower()}
                }});
            ";

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

            foreach (var lang in commonLanguages)
                AvailableLanguages.Add(lang);

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
            if (DisplayWordAndCharacterCounts)
            {
                var words = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
                var chars = text.Length;
                await Dispatcher.InvokeAsync(() =>
                {
                    WordCharCountTextBlock.Text = $"{words:N0} words, {chars:N0} characters";
                });
            }
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

    private async Task<string> ExecuteCommandAsync(string commandJson)
    {
        if (EditorWebView.CoreWebView2 == null)
            return string.Empty;

        _pendingCommandResult = new TaskCompletionSource<string>();

        try
        {
            EditorWebView.CoreWebView2.PostWebMessageAsJson(commandJson);

            var timeout = Task.Delay(5000); // 5 second timeout
            var completedTask = await Task.WhenAny(_pendingCommandResult.Task, timeout);

            if (completedTask == timeout)
                throw new TimeoutException($"Command execution timed out: {commandJson}");

            return await _pendingCommandResult.Task;
        }
        finally
        {
            _pendingCommandResult = null;
        }
    }

    private class LanguageInfo
    {
        public string Id { get; set; } = string.Empty;
        public string[]? Aliases { get; set; }
    }

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
        if (d is MonacoEditorControl control && !control._suppressTextChanged)
            _ = control.SetTextAsync((string)e.NewValue);
    }

    private static void OnLanguagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl control && control.IsInitialized && !control._suppressLanguageChanged)
            _ = control.SetLanguageAsync((string)e.NewValue);
    }

    private static void OnIsReadOnlyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl control && control.IsInitialized)
            _ = control.UpdateOptionsAsync();
    }

    private static async void OnEditorOptionsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonacoEditorControl control && e.NewValue is MonacoEditorConfiguration options)
        {
            var oldDebug = control.EnableDebug;
            control._logger.LogInformation("[{ControlName}] OnEditorOptionsPropertyChanged - Setting EnableDebug from {OldValue} to {NewValue}", 
                control.Name ?? "Monaco", oldDebug, options.EnableDebug);
            
            control.ShowToolbar = options.ShowToolbar;
            control.DisplayWordAndCharacterCounts = options.DisplayWordAndCharacterCounts;
            control.EnableDebug = options.EnableDebug;

            if (control.IsInitialized)
            {
                await control.UpdateOptionsAsync();
                
                // Handle EnableDebug change at runtime
                if (oldDebug != options.EnableDebug && control.EditorWebView.CoreWebView2 != null)
                {
                    await control.EditorWebView.ExecuteScriptAsync($"setDebugMode({options.EnableDebug.ToString().ToLower()});");
                    
                    if (options.EnableDebug)
                    {
                        control.EditorWebView.CoreWebView2.OpenDevToolsWindow();
                        control._logger.LogInformation("[{ControlName}] Debug mode enabled at runtime", control.Name ?? "Monaco");
                    }
                    else
                    {
                        control._logger.LogInformation("[{ControlName}] Debug mode disabled at runtime", control.Name ?? "Monaco");
                    }
                }
            }
        }
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
    ///     Sets text, language, and optionally restores view state in a single atomic operation.
    /// </summary>
    public async Task<bool> LoadContentAsync(string text, string? languageId = null, string? viewStateJson = null)
    {
        var controlName = Name ?? "Monaco";
        _logger.LogDebug("[{ControlName}] LoadContentAsync - Text: {TextLength} chars, Language: {Language}, HasViewState: {HasViewState}, IsInitialized: {IsInitialized}",
            controlName, text?.Length ?? 0, languageId ?? "null", !string.IsNullOrEmpty(viewStateJson), IsInitialized);

        if (!IsInitialized)
        {
            _logger.LogWarning("[{ControlName}] LoadContentAsync failed - editor not initialized", controlName);
            return false;
        }

        text ??= string.Empty;
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

    #endregion
}
