using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

// Alias to resolve ambiguity with System.Windows.Application
using WinFormsApplication = System.Windows.Forms.Application;

namespace ClipMate.Platform.Services;

/// <summary>
/// Manages the system tray icon and context menu for ClipMate.
/// Provides quick access to show/hide window, collection switching, and exit.
/// </summary>
public class SystemTrayService : IDisposable
{
    private readonly ICollectionService _collectionService;
    private readonly ILogger<SystemTrayService> _logger;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;

    /// <summary>
    /// Raised when the user requests to show the main window.
    /// </summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>
    /// Raised when the user requests to hide the main window.
    /// </summary>
    public event EventHandler? HideWindowRequested;

    /// <summary>
    /// Raised when the user requests to exit the application.
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when the user selects a different collection.
    /// </summary>
    public event EventHandler<Guid>? CollectionChanged;

    public SystemTrayService(ICollectionService collectionService, ILogger<SystemTrayService> logger)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the system tray icon and context menu.
    /// </summary>
    public void Initialize()
    {
        if (_notifyIcon != null)
        {
            _logger.LogWarning("SystemTrayService already initialized");
            return;
        }

        _logger.LogInformation("Initializing system tray icon");

        // Create the NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Text = "ClipMate - Clipboard Manager",
            Visible = true
        };

        // Set the icon
        // TODO: Replace with actual ClipMate icon from embedded resource
        // See: Source/src/ClipMate.App/Resources/README.md for icon requirements
        // Current: Using Windows default application icon as placeholder
        _notifyIcon.Icon = SystemIcons.Application;

        // Wire up events
        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;

        // Create context menu
        BuildContextMenu();

        _logger.LogInformation("System tray icon initialized");
    }

    /// <summary>
    /// Rebuilds the context menu, typically after collection changes.
    /// </summary>
    public async Task RebuildContextMenuAsync()
    {
        _logger.LogDebug("Rebuilding system tray context menu");
        BuildContextMenu();
        await LoadCollectionsIntoMenuAsync();
    }

    /// <summary>
    /// Shows a balloon notification from the tray icon.
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="icon">Notification icon type</param>
    /// <param name="timeout">Display timeout in milliseconds (default 3000)</param>
    public void ShowBalloonNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        if (_notifyIcon == null)
        {
            _logger.LogWarning("Cannot show balloon notification: NotifyIcon not initialized");
            return;
        }

        _notifyIcon.ShowBalloonTip(timeout, title, message, icon);
        _logger.LogDebug("Showed balloon notification: {Title}", title);
    }

    /// <summary>
    /// Updates the tray icon tooltip text.
    /// </summary>
    /// <param name="text">New tooltip text (max 63 characters)</param>
    public void SetTooltip(string text)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        // NotifyIcon.Text is limited to 63 characters
        _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
    }

    private void BuildContextMenu()
    {
        _contextMenu?.Dispose();
        _contextMenu = new ContextMenuStrip();

        // Show/Hide menu item
        var showHideItem = new ToolStripMenuItem("Show ClipMate", null, OnShowHideClick)
        {
            Font = new Font(_contextMenu.Font, FontStyle.Bold)
        };
        _contextMenu.Items.Add(showHideItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Collections submenu (will be populated async)
        var collectionsItem = new ToolStripMenuItem("Collections");
        collectionsItem.DropDownOpening += OnCollectionsMenuOpening;
        _contextMenu.Items.Add(collectionsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit menu item
        _contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, OnExitClick));

        if (_notifyIcon != null)
        {
            _notifyIcon.ContextMenuStrip = _contextMenu;
        }
    }

    private async void OnCollectionsMenuOpening(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem collectionsItem)
        {
            return;
        }

        // Clear existing collection items
        collectionsItem.DropDownItems.Clear();

        try
        {
            var collections = await _collectionService.GetAllAsync();

            if (collections.Count == 0)
            {
                collectionsItem.DropDownItems.Add(new ToolStripMenuItem("(No collections)", null, (EventHandler?)null)
                {
                    Enabled = false
                });
                return;
            }

            // Get active collection if available
            Guid? activeCollectionId = null;
            try
            {
                var activeCollection = await _collectionService.GetActiveAsync();
                activeCollectionId = activeCollection?.Id;
            }
            catch
            {
                // No active collection set
            }

            foreach (var collection in collections)
            {
                var item = new ToolStripMenuItem(collection.Name, null, OnCollectionClick)
                {
                    Tag = collection.Id,
                    Checked = collection.Id == activeCollectionId
                };
                collectionsItem.DropDownItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load collections into context menu");
            collectionsItem.DropDownItems.Add(new ToolStripMenuItem("(Error loading collections)", null, (EventHandler?)null)
            {
                Enabled = false
            });
        }
    }

    private async Task LoadCollectionsIntoMenuAsync()
    {
        if (_contextMenu == null)
        {
            return;
        }

        var collectionsItem = _contextMenu.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text == "Collections");

        if (collectionsItem == null)
        {
            return;
        }

        // Trigger the opening event to load collections
        OnCollectionsMenuOpening(collectionsItem, EventArgs.Empty);
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        _logger.LogDebug("Tray icon double-clicked - toggling window visibility");
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        // Right-click is handled by ContextMenuStrip automatically
        // We only care about left-click here if needed
        if (e.Button == MouseButtons.Left)
        {
            // Optional: Single left-click could also show window
            // For now, we'll rely on double-click
        }
    }

    private void OnShowHideClick(object? sender, EventArgs e)
    {
        _logger.LogDebug("Show/Hide menu item clicked");
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCollectionClick(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not Guid collectionId)
        {
            return;
        }

        _logger.LogInformation("Collection selected from tray menu: {CollectionId}", collectionId);
        CollectionChanged?.Invoke(this, collectionId);
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        _logger.LogInformation("Exit requested from tray menu");
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing SystemTrayService");

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;

        _disposed = true;
    }
}
