using ClipMate.App.Views;
using ClipMate.Core.Models.Configuration;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Views;

/// <summary>
/// The hotkey navigation popup draws its own frame, since the notification window does not follow the DevExpress theme.
/// </summary>
public class HotkeyNavigationPopupTemplateTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    [Arguments(AppTheme.Light)]
    [Arguments(AppTheme.Dark)]
    public async Task For_EveryAppTheme_HasAFrame(AppTheme theme)
    {
        // Act
        var template = new HotkeyNavigationPopupTemplate().For(theme);

        // Assert
        await Assert.That(template).IsNotNull();
    }
}
