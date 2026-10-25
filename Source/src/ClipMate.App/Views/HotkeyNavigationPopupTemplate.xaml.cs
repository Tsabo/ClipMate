using System.Windows;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.App.Views;

/// <summary>
/// The template of the hotkey navigation popup.
/// </summary>
public partial class HotkeyNavigationPopupTemplate
{
    public HotkeyNavigationPopupTemplate() => InitializeComponent();

    /// <summary>
    /// The popup's frame for <paramref name="theme" />: the notification window does not follow the DevExpress theme.
    /// </summary>
    public DataTemplate For(AppTheme theme) => (DataTemplate)this[theme];
}
