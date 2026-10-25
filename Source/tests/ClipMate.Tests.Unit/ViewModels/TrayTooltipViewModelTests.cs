using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The tray icon's tooltip: the Current Clip's title, or the current collection (ClipMate 7's System Tray Hint).
/// </summary>
public class TrayTooltipViewModelTests
{
    private ClipMateConfiguration _configuration = null!;
    private StrongReferenceMessenger _messenger = null!;
    private IPowerPasteService _powerPasteService = null!;
    private TrayTooltipViewModel _viewModel = null!;

    [Before(Test)]
    public void Setup()
    {
        _configuration = new ClipMateConfiguration();
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        _messenger = new StrongReferenceMessenger();
        _powerPasteService = A.Fake<IPowerPasteService>();

        _viewModel = new TrayTooltipViewModel(_messenger, configurationService, _powerPasteService);
    }

    [After(Test)]
    public void Cleanup() => _viewModel.Dispose();

    [Test]
    public async Task Text_NoCurrentClip_IsClipMate()
    {
        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate");
    }

    [Test]
    public async Task Text_ClipCaptured_ShowsItsTitle()
    {
        // Act
        _messenger.Send(new ClipAddedEvent(new Clip { Title = "Meeting notes" }, false, "db"));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Meeting notes");
    }

    [Test]
    public async Task Text_ClipPutOnTheClipboard_ShowsItsTitle()
    {
        // Arrange
        _messenger.Send(new ClipAddedEvent(new Clip { Title = "Meeting notes" }, false, "db"));

        // Act: picked in a ClipMate window, or reached with the Scroll Next / Previous hotkeys
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Shopping list" }));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Shopping list");
    }

    [Test]
    public async Task Text_PowerPasteMovesOn_ShowsTheClipWaitingToBePasted()
    {
        // Act
        _powerPasteService.PositionChanged += Raise.With(new PowerPastePositionChangedEventArgs { Position = 1, TotalCount = 2, CurrentClip = new Clip { Title = "Second" } });

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Second");
    }

    [Test]
    public async Task Text_EncryptedClip_NeverShowsItsTitle()
    {
        // Act
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Bank PIN", CustomTitle = true, Encrypted = true, IsDecrypted = true }));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Encrypted clip");
    }

    [Test]
    public async Task Text_LongerThanTheWindowsLimit_IsShortenedWithAnEllipsis()
    {
        // Act
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = new string('x', 200) }));

        // Assert: Windows shows at most 127 characters in a tray tooltip
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - " + new string('x', 115) + "…");
    }

    [Test]
    public async Task Text_CollectionMode_ShowsTheCurrentCollectionAndFollowsIt()
    {
        // Arrange
        _configuration.Appearance.SystemTrayHint = SystemTrayHint.Collection;
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Meeting notes" }));
        _messenger.Send(new CurrentCollectionChangedEvent("InBox"));

        // Act
        _messenger.Send(new CurrentCollectionChangedEvent("Inbox on Work laptop"));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Inbox on Work laptop");
    }

    [Test]
    public async Task Text_ClipTitleMode_IgnoresTheCollection()
    {
        // Arrange
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Meeting notes" }));

        // Act
        _messenger.Send(new CurrentCollectionChangedEvent("InBox"));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - Meeting notes");
    }

    [Test]
    public async Task Text_OptionSwitchedToCollection_ShowsTheCollectionAtOnce()
    {
        // Arrange
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Meeting notes" }));
        _messenger.Send(new CurrentCollectionChangedEvent("InBox"));

        // Act: the Options dialog saved
        _configuration.Appearance.SystemTrayHint = SystemTrayHint.Collection;
        _messenger.Send(new PreferencesChangedEvent());

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("ClipMate - InBox");
    }

    [Test]
    public async Task SystemTrayHint_Default_IsClipTitle()
    {
        // Assert: ClipMate 7's default (Preferences\UseCollTitle = 0)
        await Assert.That(new AppearanceConfiguration().SystemTrayHint).IsEqualTo(SystemTrayHint.ClipTitle);
    }
}
