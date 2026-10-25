using System.Windows;
using System.Windows.Controls;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using Size = System.Windows.Size;

namespace ClipMate.App.Views;

/// <summary>
/// The template of the hotkey navigation popup.
/// </summary>
public partial class HotkeyNavigationPopupTemplate
{
    private const double _popupWidth = 360;

    public HotkeyNavigationPopupTemplate() => InitializeComponent();

    /// <summary>
    /// The popup's frame for <paramref name="theme" />: the notification window does not follow the DevExpress theme.
    /// </summary>
    public DataTemplate For(AppTheme theme) => (DataTemplate)this[theme];

    /// <summary>
    /// The size of the popup showing <paramref name="viewModel" />: the notification window takes a fixed size rather
    /// than sizing to its content, so it is measured here at the popup's width.
    /// </summary>
    public Size SizeOf(HotkeyNavigationPopupViewModel viewModel, AppTheme theme)
    {
        var popup = new ContentPresenter { Content = viewModel, ContentTemplate = For(theme) };
        popup.Measure(new Size(_popupWidth, double.PositiveInfinity));

        return new Size(_popupWidth, Math.Ceiling(popup.DesiredSize.Height));
    }
}
