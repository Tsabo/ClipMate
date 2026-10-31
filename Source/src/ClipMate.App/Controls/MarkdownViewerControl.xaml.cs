using System.ComponentModel;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Controls;

/// <summary>
/// Markdown tab of the ClipViewer: shows the clip's HTML/RTF converted to Markdown,
/// either as highlighted source (Monaco) or as a rendered preview.
/// </summary>
public partial class MarkdownViewerControl
{
    private readonly ILogger<MarkdownViewerControl> _logger;
    private readonly IConfigurationService _configurationService;

    public MarkdownViewerControl()
    {
        InitializeComponent();

        // Get services from DI container
        var serviceProvider = ((App)Application.Current).ServiceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<MarkdownViewerControl>>();
        _configurationService = serviceProvider.GetRequiredService<IConfigurationService>();

        ViewModel = serviceProvider.GetRequiredService<MarkdownViewerViewModel>();
        ViewModel.OnCopyRequested = () => _ = CopyAsync();
        DataContext = ViewModel;

        MarkdownEditor.EditorOptions = _configurationService.Configuration.MonacoEditor;

        // Only convert while the tab is actually shown
        IsVisibleChanged += (_, e) => ViewModel.IsActive = (bool)e.NewValue;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) =>
        {
            var isInitDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.IsInitializedProperty, typeof(MonacoEditorControl));
            isInitDescriptor?.AddValueChanged(MarkdownEditor, OnEditorInitialized);
        };

        Unloaded += (_, _) =>
        {
            var isInitDescriptor = DependencyPropertyDescriptor.FromProperty(MonacoEditorControl.IsInitializedProperty, typeof(MonacoEditorControl));
            isInitDescriptor?.RemoveValueChanged(MarkdownEditor, OnEditorInitialized);
        };
    }

    /// <summary>
    /// Gets the ViewModel driving this control.
    /// </summary>
    public MarkdownViewerViewModel ViewModel { get; }

    /// <summary>
    /// Re-applies Monaco editor options (e.g. theme) after preferences change.
    /// </summary>
    public async Task ApplyEditorSettingsAsync()
    {
        await MarkdownEditor.UpdateOptionsAsync();

        // UpdateOptionsAsync resets word wrap from the global options
        await MarkdownEditor.SetWordWrapAsync(ViewModel.IsWordWrapEnabled);
    }

    private void OnEditorInitialized(object? sender, EventArgs e)
    {
        if (MarkdownEditor.IsInitialized)
            _ = MarkdownEditor.SetWordWrapAsync(ViewModel.IsWordWrapEnabled);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkdownViewerViewModel.IsWordWrapEnabled))
            _ = MarkdownEditor.SetWordWrapAsync(ViewModel.IsWordWrapEnabled);
    }

    private async Task CopyAsync()
    {
        try
        {
            // The editor selection is only meaningful while the source view is shown
            var selectedText = ViewModel.IsPreviewMode
                ? null
                : await MarkdownEditor.GetSelectedTextAsync();

            var text = string.IsNullOrEmpty(selectedText)
                ? ViewModel.MarkdownText
                : selectedText;

            if (string.IsNullOrEmpty(text))
                return;

            OwnClipboardCopy.SetText(text);
            _logger.LogDebug("[MarkdownViewer] Copied {Length} characters of Markdown", text.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MarkdownViewer] Error copying Markdown");
        }
    }
}
