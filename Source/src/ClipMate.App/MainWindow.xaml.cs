using System.Windows;
using System.Windows.Input;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Responsible only for window chrome (tray icon, window state, etc.)
/// All business logic is in MainWindowViewModel.
/// </summary>
public partial class MainWindow : Window, IWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindow>? _logger;
    private bool _isExiting = false;

    public MainWindow(
        MainWindowViewModel mainWindowViewModel,
        IServiceProvider serviceProvider,
        ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();

        _viewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;

        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Hide window and taskbar icon when minimized (will remain visible in system tray)
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
            _logger?.LogDebug("MainWindow minimized to tray");
        }
        else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
        {
            ShowInTaskbar = true;
        }
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
        _logger?.LogDebug("MainWindow shown from tray menu");
    }

    /// <summary>
    /// Prepares the window for application exit (skips minimize to tray behavior)
    /// </summary>
    public void PrepareForExit()
    {
        _isExiting = true;
    }
    
    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.T && (Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            ShowTextTools();
            e.Handled = true;
        }
    }
    
    private void TextTools_Click(object sender, RoutedEventArgs e)
    {
        ShowTextTools();
    }
    
    private void ShowTextTools()
    {
        try
        {
            if (_serviceProvider.GetService(typeof(TextToolsDialog)) is TextToolsDialog textToolsDialog)
            {
                textToolsDialog.Owner = this;
                textToolsDialog.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Text Tools dialog");
            MessageBox.Show($"Failed to open Text Tools: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ManageTemplates_Click(object sender, RoutedEventArgs e)
    {
        ShowManageTemplates();
    }

    private void ShowManageTemplates()
    {
        try
        {
            if (_serviceProvider.GetService(typeof(TemplateEditorDialog)) is TemplateEditorDialog templateEditorDialog)
            {
                templateEditorDialog.Owner = this;
                templateEditorDialog.ShowDialog();

                // Reload template menu after dialog closes
                LoadTemplateMenu();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Template Editor dialog");
            MessageBox.Show($"Failed to open Template Editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadTemplateMenu()
    {
        try
        {
            // Get the Insert Template menu item by name
            if (FindName("InsertTemplateMenu") is not System.Windows.Controls.MenuItem insertTemplateMenu)
            {
                return;
            }

            // Clear existing items
            insertTemplateMenu.Items.Clear();

            // Get template service from DI
            if (_serviceProvider.GetService(typeof(ITemplateService)) is not ITemplateService templateService)
            {
                _logger?.LogWarning("TemplateService not available in DI container");
                return;
            }

            // Load templates
            var templates = await templateService.GetAllAsync();

            if (templates.Count == 0)
            {
                var emptyItem = new System.Windows.Controls.MenuItem
                {
                    Header = "(No templates available)",
                    IsEnabled = false
                };
                insertTemplateMenu.Items.Add(emptyItem);
            }
            else
            {
                // Add template items
                foreach (var template in templates.OrderBy(t => t.Name))
                {
                    var menuItem = new System.Windows.Controls.MenuItem
                    {
                        Header = template.Name,
                        Tag = template,
                        ToolTip = template.Description
                    };
                    menuItem.Click += TemplateMenuItem_Click;
                    insertTemplateMenu.Items.Add(menuItem);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load template menu");
        }
    }

    private async void TemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: Template template })
        {
            return;
        }

        try
        {
            // Get template service
            if (_serviceProvider.GetService(typeof(ITemplateService)) is not ITemplateService templateService)
            {
                MessageBox.Show("Template service not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Extract variables from template
            var variables = templateService.ExtractVariables(template.Content);
            var customVariables = new Dictionary<string, string>();

            // Prompt for PROMPT variables
            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    // Check if it's a PROMPT variable
                    if (variable.StartsWith("PROMPT:", StringComparison.OrdinalIgnoreCase))
                    {
                        var label = variable[7..]; // Remove "PROMPT:" prefix
                        var promptDialog = new PromptDialog(label)
                        {
                            Owner = this
                        };

                        if (promptDialog.ShowDialog() == true && promptDialog.UserInput != null)
                        {
                            customVariables[variable] = promptDialog.UserInput;
                        }
                        else
                        {
                            // User cancelled, abort template expansion
                            return;
                        }
                    }
                }
            }

            // Expand template
            var expandedText = await templateService.ExpandTemplateAsync(template.Id, customVariables);

            // Insert into clipboard or current focus
            if (!string.IsNullOrEmpty(expandedText))
            {
                Clipboard.SetText(expandedText);
                MessageBox.Show($"Template '{template.Name}' has been copied to clipboard!", 
                    "Template Inserted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to insert template: {TemplateName}", template.Name);
            MessageBox.Show($"Failed to insert template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize MainWindowViewModel (loads all child VMs, data, etc.)
            await _viewModel.InitializeAsync();

            // Load template menu for Edit menu
            LoadTemplateMenu();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize MainWindow");
        }
    }

    /// <summary>
    /// Handles File → Exit menu click
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _logger?.LogInformation("Exit menu clicked - shutting down application");
        _isExiting = true;
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Handles the Closing event to minimize to tray instead of exiting.
    /// Hold Shift while closing to force exit.
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If already exiting (from File→Exit or tray menu), allow it
        if (_isExiting)
        {
            _logger?.LogInformation("MainWindow closing - application is exiting");
            return;
        }

        // Check if Shift key is held - if so, allow actual exit
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
        {
            _logger?.LogInformation("MainWindow closing with Shift key - allowing exit");
            _isExiting = true;
            return;
        }

        // Cancel the close and hide the window instead
        e.Cancel = true;
        Hide();
        _logger?.LogInformation("MainWindow minimized to system tray");
    }

    private void SecondaryClipListView_SelectionChanged(object sender, RoutedEventArgs e)
    {
        // Selection changes in secondary list (if dual mode is implemented)
        _logger?.LogDebug("Secondary ClipList selection changed");
    }
}
