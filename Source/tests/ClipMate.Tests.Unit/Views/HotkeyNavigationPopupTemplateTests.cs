using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Views;

/// <summary>
/// The hotkey navigation popup draws its own frame, since the notification window does not follow the DevExpress theme.
/// Not in parallel: WPF can deadlock when several STA threads load XAML for the first time at once.
/// </summary>
[NotInParallel]
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

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task SizeOf_MorePreviewLines_IsTallerAndSameWidth()
    {
        // Arrange: the notification window takes a fixed size, so it must be sized to what the popup shows
        var templates = new HotkeyNavigationPopupTemplate();
        var description = new ClipDescription("Title", "Inbox");
        var oneLine = new HotkeyNavigationPopupViewModel { Description = description, PreviewText = "one" };
        var threeLines = new HotkeyNavigationPopupViewModel { Description = description, PreviewText = "one\ntwo\nthree" };

        // Act
        var oneLineSize = templates.SizeOf(oneLine, AppTheme.Light);
        var threeLinesSize = templates.SizeOf(threeLines, AppTheme.Light);

        // Assert
        await Assert.That(threeLinesSize.Height).IsGreaterThan(oneLineSize.Height);
        await Assert.That(threeLinesSize.Width).IsEqualTo(oneLineSize.Width);
    }
}
