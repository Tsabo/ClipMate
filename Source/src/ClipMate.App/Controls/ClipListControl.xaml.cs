using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Shortcut = ClipMate.Core.Models.Shortcut;

namespace ClipMate.App.Controls;

/// <summary>
/// Reusable ClipList grid component that displays clipboard clips.
/// Supports dual ClipList scenarios where multiple instances can be shown simultaneously.
/// </summary>
public partial class ClipListControl
{
    /// <summary>
    /// Dependency property for the collection of clips to display
    /// </summary>
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(ObservableCollection<Clip>),
            typeof(ClipListControl),
            new PropertyMetadata(null, OnItemsChanged));

    /// <summary>
    /// Dependency property for the currently selected clip
    /// </summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(Clip),
            typeof(ClipListControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Dependency property for the header text displayed above the grid
    /// </summary>
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(ClipListControl),
            new PropertyMetadata("Clips"));

    /// <summary>
    /// Dependency property for the selected items collection
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(ObservableCollection<Clip>),
            typeof(ClipListControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    /// <summary>
    /// Dependency property for the shortcut clips collection (displayed during shortcut mode)
    /// </summary>
    public static readonly DependencyProperty ShortcutClipsProperty =
        DependencyProperty.Register(
            nameof(ShortcutClips),
            typeof(ObservableCollection<ShortcutClipViewModel>),
            typeof(ClipListControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Dependency property for the shortcut filter text (displayed during shortcut mode)
    /// </summary>
    public static readonly DependencyProperty ShortcutFilterTextProperty =
        DependencyProperty.Register(
            nameof(ShortcutFilterText),
            typeof(string),
            typeof(ClipListControl),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Routed event for selection changes
    /// </summary>
    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ClipListControl));

    private readonly IClipService? _clipService;
    private readonly ILogger<ClipListControl> _logger;
    private readonly IMessenger _messenger;
    private readonly PreferencesConfiguration _preferences;
    private readonly IQuickPasteService? _quickPasteService;
    private readonly IShortcutService? _shortcutService;
    private string? _currentDatabaseKey;

    // Shortcut mode state
    private bool _isInShortcutMode;
    private string _shortcutFilter = string.Empty;

    public ClipListControl()
    {
        InitializeComponent();

        // Initialize ShortcutClips collection
        ShortcutClips = [];

        // Get services from DI container
        var app = (App)Application.Current;
        _messenger = app.ServiceProvider.GetService<IMessenger>()!;
        _shortcutService = app.ServiceProvider.GetService<IShortcutService>();
        _quickPasteService = app.ServiceProvider.GetService<IQuickPasteService>();
        _clipService = app.ServiceProvider.GetService<IClipService>();
        _logger = app.ServiceProvider.GetService<ILogger<ClipListControl>>()!;
        var options = app.ServiceProvider.GetService<IOptions<ClipMateConfiguration>>();
        _preferences = options?.Value.Preferences ?? new PreferencesConfiguration();

        // Register for clip updated messages
        _messenger.Register<ClipUpdatedMessage>(this, OnClipUpdated);

        // Attach keyboard event handlers to the UserControl itself to capture events
        // before DevExpress GridControl can handle them
        PreviewKeyDown += ClipDataGrid_PreviewKeyDown;
    }

    /// <summary>
    /// Gets or sets the collection of clips to display
    /// </summary>
    public ObservableCollection<Clip> Items
    {
        get => (ObservableCollection<Clip>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of selected clips
    /// </summary>
    public ObservableCollection<Clip> SelectedItems
    {
        get => (ObservableCollection<Clip>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected clip
    /// </summary>
    public Clip? SelectedItem
    {
        get => (Clip?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the header text displayed above the grid
    /// </summary>
    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection of shortcut clips (displayed during shortcut mode)
    /// </summary>
    public ObservableCollection<ShortcutClipViewModel> ShortcutClips
    {
        get => (ObservableCollection<ShortcutClipViewModel>)GetValue(ShortcutClipsProperty);
        set => SetValue(ShortcutClipsProperty, value);
    }

    /// <summary>
    /// Gets or sets the shortcut filter text (displayed during shortcut mode)
    /// </summary>
    public string ShortcutFilterText
    {
        get => (string)GetValue(ShortcutFilterTextProperty);
        set => SetValue(ShortcutFilterTextProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Selection sync is now handled by GridControlSelectionSync attached property
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ClipListControl control)
            return;

        var newCollection = e.NewValue as ObservableCollection<Clip>;
        control._logger.LogDebug("ClipListView.Items changed: Count={Count}", newCollection?.Count ?? 0);
    }

    /// <summary>
    /// Event raised when the selected clip changes
    /// </summary>
    public event RoutedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>
    /// Handles the CurrentItemChanged event from the DevExpress GridControl
    /// </summary>
    private async void ClipDataGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        // Determine which grid triggered the event
        var isShortcutGrid = Equals(sender, ShortcutsDataGrid);

        // Update the SelectedItem property
        // Handle both Clip objects and ShortcutClipViewModel wrappers
        ShortcutClipViewModel? shortcutVm = null;
        var newClip = e.NewItem switch
        {
            Clip clip => clip,
            ShortcutClipViewModel vm => (shortcutVm = vm).Clip,
            var _ => null,
        };

        _logger.LogDebug("CurrentItemChanged - Grid: {Grid}, ClipId: {ClipId}, Title: {Title}", 
            isShortcutGrid ? "Shortcuts" : "Clips", newClip?.Id, newClip?.DisplayTitle);
        SelectedItem = newClip;

        // Clear selection in the other grid
        if (isShortcutGrid)
        {
            // Shortcut grid was selected, clear main grid selection
            if (ClipDataGrid?.CurrentItem != null)
            {
                ClipDataGrid.CurrentItem = null;
                _logger.LogDebug("Cleared ClipDataGrid selection");
            }
        }
        else
        {
            // Main grid was selected, clear shortcut grid selection
            if (ShortcutsDataGrid?.CurrentItem != null)
            {
                ShortcutsDataGrid.CurrentItem = null;
                _logger.LogDebug("Cleared ShortcutsDataGrid selection");
            }
        }

        // Raise the SelectionChanged routed event
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));

        // For shortcut selections, explicitly push to clipboard
        if (shortcutVm == null || newClip == null)
            return;

        var app = (App)Application.Current;
        var clipService = (IClipService?)app.ServiceProvider.GetService(typeof(IClipService));
        if (clipService == null)
            return;

        _logger.LogDebug("Pushing shortcut selection to clipboard: {ClipId}", newClip.Id);
        await clipService.LoadAndSetClipboardAsync(shortcutVm.DatabaseKey, newClip.Id);
    }

    private async void ClipProperties_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
            return;

        var dialog = new ClipPropertiesDialog();
        var app = (App)Application.Current;
        if (app.ServiceProvider.GetService(typeof(ClipPropertiesViewModel)) is not ClipPropertiesViewModel viewModel)
            return;

        await viewModel.LoadClipAsync(SelectedItem);
        dialog.DataContext = viewModel;
        dialog.Owner = Application.Current.GetDialogOwner();
        dialog.ShowDialog();
    }

    private async void PasteNow_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
            return;

        // Get the parent ViewModel to access SetClipboardContentAsync
        var app = (App)Application.Current;
        if (app.ServiceProvider.GetService(typeof(ClipListViewModel)) is ClipListViewModel viewModel)
            await viewModel.SetClipboardContentAsync(SelectedItem);
    }

    private void CreateNewClip_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement create new clip
    }

    private void ViewClip_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement view clip
    }

    private void ExportClips_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var vm = ActivatorUtilities.CreateInstance<FlatFileExportViewModel>(app.ServiceProvider);
        vm.Initialize(SelectedItems);
        var dialog = new FlatFileExportDialog(vm)
        {
            Owner = Application.Current.GetDialogOwner(),
        };

        dialog.ShowDialog();
    }

    private async void ChangeTitle_Click(object sender, RoutedEventArgs e) => await OpenRenameClipDialogAsync();

    private void DeleteItems_Click(object sender, RoutedEventArgs e)
    {
        // Send delete event with IDs of currently selected clips
        var clipIds = SelectedItems.Select(p => p.Id).ToList();
        _messenger.Send(new DeleteClipsRequestedEvent(clipIds));
    }

    private void OpenSourceUrl_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem?.SourceUrl == null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedItem.SourceUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open URL: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles the start of a record drag operation to enable dragging clips to collection tree.
    /// Adds Clip objects to the drag data so they can be dropped on collections.
    /// </summary>
    private void TableView_StartRecordDrag(object sender, StartRecordDragEventArgs e)
    {
        // Get the dragged clips
        var draggedClips = e.Records.OfType<Clip>().ToList();
        if (draggedClips.Count != 0)
        {
            // Add clips to drag data using a custom format
            e.Data.SetData(typeof(Clip[]), draggedClips.ToArray());
        }
    }

    /// <summary>
    /// Handles keyboard input for shortcut mode activation and filtering.
    /// </summary>
    private async void ClipDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle ENTER key for Universal QuickPaste
        if (e.Key == Key.Enter && !_isInShortcutMode && _preferences.QuickPastePasteOnEnter)
        {
            e.Handled = true;
            await TriggerQuickPasteAsync();
            return;
        }

        // Handle Ctrl+R for Rename Clip dialog
        if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await OpenRenameClipDialogAsync();
            return;
        }

        // ESC exits shortcut mode
        if (e.Key == Key.Escape && _isInShortcutMode)
        {
            e.Handled = true;
            ExitShortcutMode();
            return;
        }

        // Backspace shortens the filter string
        if (e.Key == Key.Back && _isInShortcutMode && _shortcutFilter.Length > 0)
        {
            e.Handled = true;
            _shortcutFilter = _shortcutFilter[..^1];
            ShortcutFilterText = _shortcutFilter;

            // Exit shortcut mode if filter becomes empty (just the period)
            if (_shortcutFilter == ".")
            {
                ExitShortcutMode();
                return;
            }

            await FilterShortcutsAsync();
            return;
        }

        // Period (dot) key enters shortcut mode
        if (e.Key == Key.OemPeriod && !_isInShortcutMode)
        {
            e.Handled = true;
            await EnterShortcutModeAsync();
            return;
        }

        // In shortcut mode, capture alphanumeric and some special keys for filtering
        if (!_isInShortcutMode)
            return;

        var character = GetCharacterFromKey(e.Key, Keyboard.Modifiers);
        if (string.IsNullOrEmpty(character))
            return;

        e.Handled = true;
        _shortcutFilter += character;
        ShortcutFilterText = _shortcutFilter;
        await FilterShortcutsAsync();
    }

    /// <summary>
    /// Opens the Rename Clip dialog for the currently selected clip.
    /// </summary>
    private async Task OpenRenameClipDialogAsync()
    {
        if (SelectedItem == null)
            return;

        // Get database key from ViewModel
        // Note: When used in ExplorerWindow, DataContext is ExplorerWindowViewModel,
        // and we need to access PrimaryClipList.CurrentDatabaseKey through that
        var databaseKey = string.Empty;
        ClipListViewModel? clipListViewModel = null;

        if (DataContext is ClipListViewModel vm)
        {
            // Standalone usage - DataContext is ClipListViewModel
            clipListViewModel = vm;
            databaseKey = vm.CurrentDatabaseKey ?? string.Empty;
            _logger.LogDebug("Got database key from ClipListViewModel: '{DatabaseKey}'", databaseKey);
        }
        else if (DataContext is ExplorerWindowViewModel explorerVm)
        {
            // Used inside ExplorerWindow - get from PrimaryClipList
            clipListViewModel = explorerVm.PrimaryClipList;
            databaseKey = clipListViewModel.CurrentDatabaseKey ?? string.Empty;
            _logger.LogDebug("Got database key from ExplorerWindowViewModel.PrimaryClipList: '{DatabaseKey}'", databaseKey);
        }
        else
            _logger.LogDebug("DataContext is neither ClipListViewModel nor ExplorerWindowViewModel, it's {DataContextType}", 
                DataContext?.GetType().Name ?? "null");

        // Fallback to _currentDatabaseKey if ViewModel doesn't have it
        if (string.IsNullOrEmpty(databaseKey))
        {
            databaseKey = _currentDatabaseKey ?? string.Empty;
            _logger.LogDebug("Fell back to _currentDatabaseKey: '{DatabaseKey}'", databaseKey);
        }

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("No database key available. _currentDatabaseKey='{CurrentDatabaseKey}', ClipListViewModel.CurrentDatabaseKey='{ViewModelDatabaseKey}'", 
                _currentDatabaseKey, clipListViewModel?.CurrentDatabaseKey);
            MessageBox.Show("Database key not available. Please select a collection first.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);

            return;
        }

        _logger.LogDebug("Using database key: '{DatabaseKey}', Clip ID: {ClipId}, Clip CollectionId: {CollectionId}, Clip Title: '{Title}'", 
            databaseKey, SelectedItem.Id, SelectedItem.CollectionId, SelectedItem.Title);

        var app = (App)Application.Current;
        var renameViewModel = (RenameClipDialogViewModel?)app.ServiceProvider.GetService(typeof(RenameClipDialogViewModel));
        if (renameViewModel == null)
            return;

        // Get existing shortcut if any (handle table not existing gracefully)
        Shortcut? existingShortcut = null;
        if (_shortcutService != null)
        {
            try
            {
                existingShortcut = await _shortcutService.GetByClipIdAsync(databaseKey, SelectedItem.Id);
            }
            catch (Exception ex) when (ex.Message.Contains("no such table"))
            {
                // ShortCut table doesn't exist yet - this is OK, just means no shortcuts have been created
                // The table will be created automatically when the first shortcut is saved
            }
        }

        await renameViewModel.InitializeAsync(
            SelectedItem.Id,
            databaseKey,
            SelectedItem.Title,
            existingShortcut?.Nickname);

        var dialog = new RenameClipDialog
        {
            DataContext = renameViewModel,
            Owner = Application.Current.GetDialogOwner(),
        };

        if (dialog.ShowDialog() == true)
        {
            // Message will be sent by the ViewModel, which will trigger OnClipUpdated
            // and update the clip title + refresh the grid
        }
    }

    /// <summary>
    /// Handles ClipUpdatedMessage to refresh the grid when a clip is updated.
    /// </summary>
    private void OnClipUpdated(object recipient, ClipUpdatedMessage message)
    {
        // Find the clip in our collection
        var clip = Items.FirstOrDefault(p => p.Id == message.ClipId);
        if (clip == null)
            return;

        // Refresh the row in the grid to update DisplayTitle
        Dispatcher.InvokeAsync(() =>
        {
            var rowHandle = ClipDataGrid.FindRow(clip);
            if (rowHandle != DataControlBase.InvalidRowHandle)
                ClipDataGrid.RefreshRow(rowHandle);
        });
    }

    /// <summary>
    /// Enters shortcut mode by loading all shortcuts and displaying them.
    /// </summary>
    private async Task EnterShortcutModeAsync()
    {
        if (_shortcutService == null)
            return;

        _isInShortcutMode = true;
        _shortcutFilter = "."; // Start with just the period
        ShortcutFilterText = _shortcutFilter;

        // Show shortcuts grid and hide ClipDataGrid column headers
        ShortcutsPanel.Visibility = Visibility.Visible;
        ClipTableView.ShowColumnHeaders = false;

        // Load all shortcuts from all databases
        var shortcuts = await _shortcutService.GetAllFromAllDatabasesAsync();
        if (shortcuts.Count == 0)
        {
            // No shortcuts exist, exit mode
            ExitShortcutMode();
            return;
        }

        // Send status message
        _messenger.Send(new ShortcutModeStatusMessage(true, _shortcutFilter, shortcuts.Count));

        // Load clips for shortcuts and display them
        await DisplayShortcutsAsync(shortcuts);
    }

    /// <summary>
    /// Filters shortcuts based on the current filter string.
    /// </summary>
    private async Task FilterShortcutsAsync()
    {
        if (_shortcutService == null)
            return;

        // Get shortcuts matching the prefix from all databases
        var shortcuts = await _shortcutService.GetByNicknamePrefixFromAllDatabasesAsync(_shortcutFilter);

        // Send status message with updated count (even if 0)
        _messenger.Send(new ShortcutModeStatusMessage(true, _shortcutFilter, shortcuts.Count));

        // Display shortcuts (will show empty grid if no matches)
        await DisplayShortcutsAsync(shortcuts);
    }

    /// <summary>
    /// Displays clips associated with shortcuts.
    /// </summary>
    private async Task DisplayShortcutsAsync(IReadOnlyList<(string DatabaseKey, Shortcut Shortcut)> shortcuts)
    {
        var app = (App)Application.Current;
        var clipService = (IClipService?)app.ServiceProvider.GetService(typeof(IClipService));
        if (clipService == null)
            return;

        // Load clips for each shortcut and wrap them in view models
        var shortcutViewModels = new List<ShortcutClipViewModel>();
        foreach (var (databaseKey, shortcut) in shortcuts)
        {
            // Get the clip from the correct database
            var clip = await clipService.GetByIdAsync(databaseKey, shortcut.ClipId);
            if (clip == null)
                continue;

            // Create view model wrapper without modifying the original clip
            var viewModel = new ShortcutClipViewModel(clip, shortcut.Nickname, databaseKey, shortcut.Id);
            shortcutViewModels.Add(viewModel);
        }

        // Populate ShortcutClips collection for the shortcuts grid
        ShortcutClips.Clear();
        foreach (var item in shortcutViewModels.OrderBy(p => p.DisplayTitle))
            ShortcutClips.Add(item);

        // If there's only one shortcut, automatically push it to clipboard
        // This handles the case where CurrentItemChanged doesn't fire when re-selecting the same item
        if (shortcutViewModels.Count == 1)
        {
            var vm = shortcutViewModels[0];
            _logger.LogDebug("Single shortcut - automatically pushing to clipboard: {ClipId}", vm.Clip.Id);
            await clipService.LoadAndSetClipboardAsync(vm.DatabaseKey, vm.Clip.Id);
        }
    }

    /// <summary>
    /// Exits shortcut mode and hides the shortcuts grid.
    /// </summary>
    private void ExitShortcutMode()
    {
        _isInShortcutMode = false;
        _shortcutFilter = string.Empty;
        ShortcutFilterText = string.Empty;

        // Send message that shortcut mode has ended
        _messenger.Send(new ShortcutModeStatusMessage(false, string.Empty, 0));

        // Hide shortcuts grid and show ClipDataGrid column headers
        ShortcutsPanel.Visibility = Visibility.Collapsed;
        ClipTableView.ShowColumnHeaders = true;

        // Clear ShortcutClips collection
        ShortcutClips.Clear();
    }

    /// <summary>
    /// Converts a Key to its character representation.
    /// </summary>
    private static string? GetCharacterFromKey(Key key, ModifierKeys modifiers)
    {
        // Handle letters
        if (key is >= Key.A and <= Key.Z)
        {
            var letter = (char)('a' + (key - Key.A));
            return modifiers.HasFlag(ModifierKeys.Shift)
                ? letter.ToString().ToUpper()
                : letter.ToString();
        }

        // Handle numbers
        if (key is >= Key.D0 and <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        // Handle numpad
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return ((char)('0' + (key - Key.NumPad0))).ToString();

        // Handle special characters commonly used in shortcuts
        return key switch
        {
            Key.OemPeriod => ".",
            Key.OemMinus => "-",
            Key.OemPlus => "+",
            Key.Space => " ",
            var _ => null,
        };
    }

    /// <summary>
    /// Sets the current database key for shortcut operations.
    /// </summary>
    public void SetDatabaseKey(string? databaseKey) => _currentDatabaseKey = databaseKey;

    /// <summary>
    /// Called when ClipDataGrid is loaded. Sets keyboard focus on the grid to enable keyboard shortcuts.
    /// </summary>
    private void ClipDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Set focus on the grid to enable keyboard shortcuts like period key
        ClipDataGrid.Focus();
    }

    /// <summary>
    /// Synchronizes column widths between ShortcutsDataGrid and ClipDataGrid.
    /// Called when ShortcutsDataGrid is loaded.
    /// </summary>
    private void ShortcutsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (ShortcutsDataGrid.Columns.Count != ClipDataGrid.Columns.Count)
            return;

        // Bind column widths bidirectionally
        for (var i = 0; i < ShortcutsDataGrid.Columns.Count; i++)
        {
            BindingOperations.SetBinding(
                ShortcutsDataGrid.Columns[i],
                BaseColumn.WidthProperty,
                new Binding("ActualWidth")
                {
                    Source = ClipDataGrid.Columns[i],
                    Mode = BindingMode.OneWay,
                });

            BindingOperations.SetBinding(
                ClipDataGrid.Columns[i],
                BaseColumn.WidthProperty,
                new Binding("ActualWidth")
                {
                    Source = ShortcutsDataGrid.Columns[i],
                    Mode = BindingMode.OneWay,
                });
        }
    }

    /// <summary>
    /// Handles double-click on a clip row for Universal QuickPaste.
    /// </summary>
    private async void ClipDataGrid_RowDoubleClick(object sender, RowDoubleClickEventArgs e)
    {
        if (_preferences.QuickPastePasteOnDoubleClick)
            await TriggerQuickPasteAsync();
    }

    /// <summary>
    /// Triggers Universal QuickPaste for the currently selected clip.
    /// Pastes the clip to the auto-targeted application using QuickPasteService.
    /// </summary>
    private async Task TriggerQuickPasteAsync()
    {
        if (_quickPasteService == null || _clipService == null)
        {
            _logger.LogDebug("QuickPaste services not available");
            return;
        }

        var selectedClip = SelectedItem;
        if (selectedClip == null)
        {
            _logger.LogDebug("QuickPaste: No clip selected");
            return;
        }

        // Determine database key
        var databaseKey = _currentDatabaseKey;
        if (string.IsNullOrEmpty(databaseKey))
        {
            // Try to get from ViewModel context
            if (DataContext is ClipListViewModel clipListVm)
                databaseKey = clipListVm.CurrentDatabaseKey;
            else if (DataContext is ExplorerWindowViewModel explorerVm)
                databaseKey = explorerVm.PrimaryClipList.CurrentDatabaseKey;
        }

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogDebug("QuickPaste: No database key available");
            return;
        }

        try
        {
            // Load full clip with all formats from database
            var fullClip = await _clipService.GetByIdAsync(databaseKey, selectedClip.Id);
            if (fullClip == null)
            {
                _logger.LogDebug("QuickPaste: Could not load clip {ClipId}", selectedClip.Id);
                return;
            }

            _logger.LogDebug("QuickPaste: Pasting clip (ID: {ClipId}, Title: {Title}) to target application", 
                fullClip.Id, fullClip.Title);

            // Execute QuickPaste
            var success = await _quickPasteService.PasteClipAsync(fullClip);
            if (success)
                _logger.LogDebug("QuickPaste: Paste successful");
            else
                _logger.LogDebug("QuickPaste: Paste failed - no target or operation failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickPaste error");
        }
    }
}
