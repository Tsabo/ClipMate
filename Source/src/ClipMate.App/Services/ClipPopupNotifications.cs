using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Services;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;

namespace ClipMate.App.Services;

/// <summary>
/// The hotkey navigation popup as a DevExpress custom notification: drawn by ClipMate in the application theme rather
/// than a Windows notification, which would stack in the Notification Centre, be hidden by Do Not Disturb and animate
/// too slowly for rapid presses.
/// </summary>
public sealed class ClipPopupNotifications : IClipPopupNotifications
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(3);
    private readonly IConfigurationService _configurationService;
    private NotificationService? _service;
    private HotkeyNavigationPopupTemplate? _templates;

    public ClipPopupNotifications(IConfigurationService configurationService) =>
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

    /// <inheritdoc />
    public INotification Create(HotkeyNavigationPopupViewModel viewModel)
    {
        var service = GetService();

        service.CustomNotificationTemplate = _templates!.For(_configurationService.Configuration.Appearance.AppTheme);
        return service.CreateCustomNotification(viewModel);
    }

    // Created on first use, on the UI thread: NotificationService and the templates are WPF objects
    private NotificationService GetService()
    {
        _templates ??= new HotkeyNavigationPopupTemplate();
        return _service ??= new NotificationService
        {
            UseWin8NotificationsIfAvailable = false,
            CustomNotificationPosition = NotificationPosition.BottomRight,
            CustomNotificationScreen = NotificationScreen.Primary,
            CustomNotificationDuration = _duration,
            CustomNotificationVisibleMaxCount = 1,
        };
    }
}
