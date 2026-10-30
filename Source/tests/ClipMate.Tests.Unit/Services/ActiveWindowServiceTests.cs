using System.Windows;
using ClipMate.App.Services;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Dialogs take their Owner from <see cref="ActiveWindowService.DialogOwner" />, and WPF throws when a dialog's Owner
/// has never been shown or has been closed. ClipMate starts in the tray, so the last active window is often not on screen.
/// </summary>
[NotInParallel(StaTestIsolationTests.StaKey)]
public class ActiveWindowServiceTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task DialogOwner_WindowNeverShown_IsNull()
    {
        // Arrange: the Explorer window when ClipMate starts in the tray
        var service = new ActiveWindowService { DialogOwner = OffScreenWindow() };

        // Act
        var owner = service.DialogOwner;

        // Assert
        await Assert.That(owner).IsNull();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task DialogOwner_WindowClosed_IsNull()
    {
        // Arrange: Switch View closes the window that was last active
        var window = OffScreenWindow();
        window.Show();
        var service = new ActiveWindowService { DialogOwner = window };
        window.Close();

        // Act
        var owner = service.DialogOwner;

        // Assert
        await Assert.That(owner).IsNull();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task DialogOwner_WindowMinimizedToTray_IsNull()
    {
        // Arrange
        var window = OffScreenWindow();
        window.Show();
        var service = new ActiveWindowService { DialogOwner = window };
        window.Hide();

        // Act
        var owner = service.DialogOwner;

        // Assert
        await Assert.That(owner).IsNull();
        window.Close();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task DialogOwner_WindowOnScreen_IsThatWindow()
    {
        // Arrange
        var window = OffScreenWindow();
        window.Show();
        var service = new ActiveWindowService { DialogOwner = window };

        // Act
        var owner = service.DialogOwner;

        // Assert
        await Assert.That(owner).IsSameReferenceAs(window);
        window.Close();
    }

    private static Window OffScreenWindow() => new()
    {
        Left = -10000,
        Top = -10000,
        Width = 1,
        Height = 1,
        WindowStyle = WindowStyle.None,
        ShowInTaskbar = false,
        ShowActivated = false,
    };
}
