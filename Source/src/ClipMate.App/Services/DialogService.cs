using ClipMate.Core.Services;
using DevExpress.Xpf.Core;
using CoreDialogResult = ClipMate.Core.Models.DialogResult;
using CoreDialogButton = ClipMate.Core.Models.DialogButton;
using CoreDialogIcon = ClipMate.Core.Models.DialogIcon;

namespace ClipMate.App.Services;

/// <summary>
/// Dialog service implementation using DevExpress DXMessageBox.
/// </summary>
public class DialogService : IDialogService
{
    /// <inheritdoc />
    public CoreDialogResult ShowMessage(string message, string title, CoreDialogButton button = CoreDialogButton.OK, CoreDialogIcon icon = CoreDialogIcon.Information)
    {
        var wpfButton = MapButton(button);
        var wpfIcon = MapIcon(icon);

        var wpfResult = DXMessageBox.Show(message, title, wpfButton, wpfIcon);

        return MapResult(wpfResult);
    }

    private static MessageBoxButton MapButton(CoreDialogButton button) => button switch
    {
        CoreDialogButton.OK => MessageBoxButton.OK,
        CoreDialogButton.OKCancel => MessageBoxButton.OKCancel,
        CoreDialogButton.YesNo => MessageBoxButton.YesNo,
        CoreDialogButton.YesNoCancel => MessageBoxButton.YesNoCancel,
        var _ => MessageBoxButton.OK,
    };

    private static MessageBoxImage MapIcon(CoreDialogIcon icon) => icon switch
    {
        CoreDialogIcon.Information => MessageBoxImage.Information,
        CoreDialogIcon.Question => MessageBoxImage.Question,
        CoreDialogIcon.Warning => MessageBoxImage.Warning,
        CoreDialogIcon.Error => MessageBoxImage.Error,
        var _ => MessageBoxImage.Information,
    };

    private static CoreDialogResult MapResult(MessageBoxResult result) => result switch
    {
        MessageBoxResult.OK => CoreDialogResult.OK,
        MessageBoxResult.Cancel => CoreDialogResult.Cancel,
        MessageBoxResult.Yes => CoreDialogResult.Yes,
        MessageBoxResult.No => CoreDialogResult.No,
        var _ => CoreDialogResult.Cancel,
    };
}
