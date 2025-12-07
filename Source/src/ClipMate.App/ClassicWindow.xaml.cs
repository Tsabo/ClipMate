using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;

namespace ClipMate.App;

/// <summary>
/// ClipMate Classic window with complete menu, toolbar, and collapsible clip list.
/// Features stay-on-top, auto-collapse, and DXSerializer layout persistence.
/// </summary>
public partial class ClassicWindow
{
    private readonly DispatcherTimer _autoCollapseTimer;
    private readonly IConfigurationService _configurationService;
    private readonly ClassicViewModel _viewModel;
    private readonly bool _isHotkeyTriggered;

    public ClassicWindow(ClassicViewModel viewModel, IConfigurationService configurationService, bool isHotkeyTriggered = false)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _isHotkeyTriggered = isHotkeyTriggered;
        DataContext = _viewModel;

        // Subscribe to close window flag
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Initialize auto-collapse timer (500ms delay)
        _autoCollapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };

        _autoCollapseTimer.Tick += AutoCollapseTimer_Tick;

        // Load configuration values
        Topmost = _configurationService.Configuration.Preferences.ClassicStayOnTop;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassicViewModel.ShouldCloseWindow) && _viewModel.ShouldCloseWindow)
            Close();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load DXSerializer layout for BarManager and GridControl
        var layoutPath = GetLayoutFilePath();
        if (File.Exists(layoutPath))
        {
            try
            {
                await using var stream = File.OpenRead(layoutPath);
                DXSerializer.Deserialize(BarManager, stream, null);
                DXSerializer.Deserialize(ClipListGrid, stream, null);

                // Restore window position/size from layout
                DXSerializer.Deserialize(this, stream, null);
            }
            catch (Exception ex)
            {
                // Log layout load failure (silent fallback to defaults)
                Debug.WriteLine($"Failed to load Classic layout: {ex.Message}");
            }
        }

        // Load configuration state
        var config = _configurationService.Configuration.Preferences;
        _viewModel.IsDroppedDown = config.ClassicIsDroppedDown;
        _viewModel.IsTacked = config.ClassicIsTacked;

        // Apply initial height based on dropdown state
        Height = _viewModel.IsDroppedDown
            ? 600
            : 220;

        // Load recent clips
        await _viewModel.LoadRecentClipsAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _autoCollapseTimer.Stop();

        // Save DXSerializer layout
        var layoutPath = GetLayoutFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            using var stream = File.Create(layoutPath);

            DXSerializer.Serialize(BarManager, stream, null);
            DXSerializer.Serialize(ClipListGrid, stream, null);
            DXSerializer.Serialize(this, stream, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save Classic layout: {ex.Message}");
        }

        // Save configuration state
        var config = _configurationService.Configuration.Preferences;
        config.ClassicIsDroppedDown = _viewModel.IsDroppedDown;
        config.ClassicIsTacked = _viewModel.IsTacked;
        _ = _configurationService.SaveAsync();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Auto-collapse unless tacked
        if (_viewModel is { IsDroppedDown: true, IsTacked: false })
            _autoCollapseTimer.Start();
    }

    private void AutoCollapseTimer_Tick(object? sender, EventArgs e)
    {
        _autoCollapseTimer.Stop();

        // Check if window was reactivated during timer
        if (!IsActive)
        {
            _viewModel.IsDroppedDown = false;
            Height = 220;
        }
    }

    private void ClipListGrid_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
    {
        if (ClipListGrid.SelectedItem is Clip selectedClip)
            _viewModel.SelectedClip = selectedClip;
    }

    /// <summary>
    /// Returns the layout file path based on trigger mode (hotkey vs taskbar).
    /// Two separate profiles allow different window positions/sizes per mode.
    /// </summary>
    private string GetLayoutFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var layoutDir = Path.Combine(appData, "ClipMate", "Layouts");
        var profileName = _isHotkeyTriggered
            ? "Classic_Hotkey.xml"
            : "Classic_Taskbar.xml";

        return Path.Combine(layoutDir, profileName);
    }
}
