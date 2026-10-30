using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// The tray icon's tooltip: the Current Clip's title, or the current collection (ClipMate 7's System Tray Hint).
/// </summary>
public class TrayTooltipViewModelTests
{
    private ICollectionService _collectionService = null!;
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
        _collectionService = A.Fake<ICollectionService>();

        _viewModel = new TrayTooltipViewModel(_messenger, configurationService, _powerPasteService, _collectionService);
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
    public async Task Text_ClipCaptured_ShowsOnlyItsTitle()
    {
        // Act
        _messenger.Send(new ClipAddedEvent(new Clip { Title = "Meeting notes" }, false, "db") { Captured = true });

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Meeting notes");
    }

    [Test]
    public async Task Text_ClipPutOnTheClipboard_ShowsItsTitle()
    {
        // Arrange
        _messenger.Send(new ClipAddedEvent(new Clip { Title = "Meeting notes" }, false, "db") { Captured = true });

        // Act: picked in a ClipMate window, or reached with the Scroll Next / Previous hotkeys
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Shopping list" }));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Shopping list");
    }

    [Test]
    public async Task Text_ClipAddedWithoutTheClipboard_StillShowsTheCurrentClip()
    {
        // Arrange
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Meeting notes" }));

        // Act: Append Clips saves the appended clip without putting it on the clipboard
        _messenger.Send(new ClipAddedEvent(new Clip { Title = "Appended" }, false, "db"));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Meeting notes");
    }

    [Test]
    public async Task Text_TooLongWithASurrogatePairAtTheCut_KeepsThePairWhole()
    {
        // Arrange: the emoji's two UTF-16 halves fall either side of the cut at 126
        var title = new string('x', 125) + "\U0001F600" + new string('y', 50);

        // Act
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = title, CustomTitle = true }));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo(new string('x', 125) + "…");
    }

    [Test]
    public async Task Text_PowerPasteMovesOn_ShowsTheClipWaitingToBePasted()
    {
        // Act
        _powerPasteService.PositionChanged += Raise.With(new PowerPastePositionChangedEventArgs { Position = 1, TotalCount = 2, CurrentClip = new Clip { Title = "Second" } });

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Second");
    }

    [Test]
    public async Task Text_EncryptedClip_NeverShowsItsTitle()
    {
        // Act
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Bank PIN", CustomTitle = true, Encrypted = true, IsDecrypted = true }));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Encrypted clip");
    }

    [Test]
    public async Task Text_LongerThanTheWindowsLimit_IsShortenedWithAnEllipsis()
    {
        // Act
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = new string('x', 200) }));

        // Assert: Windows shows at most 127 characters in a tray tooltip
        await Assert.That(_viewModel.Text).IsEqualTo(new string('x', 126) + "…");
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
        await Assert.That(_viewModel.Text).IsEqualTo("Inbox on Work laptop");
    }

    [Test]
    public async Task Text_ClipTitleMode_IgnoresTheCollection()
    {
        // Arrange
        _messenger.Send(new ClipboardCopiedEvent(new Clip { Title = "Meeting notes" }));

        // Act
        _messenger.Send(new CurrentCollectionChangedEvent("InBox"));

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Meeting notes");
    }

    [Test]
    public async Task Text_CollectionModeAtStartup_ShowsTheActiveCollection()
    {
        // Arrange: started in the tray, so no collection has been picked in the tree yet
        _configuration.Appearance.SystemTrayHint = SystemTrayHint.Collection;
        A.CallTo(() => _collectionService.GetActiveAsync(A<CancellationToken>.Ignored)).Returns(new Collection { Title = "Inbox" });

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Inbox");
    }

    [Test]
    public async Task Text_CollectionPickedWhileStarting_KeepsThePickedCollection()
    {
        // Arrange
        _configuration.Appearance.SystemTrayHint = SystemTrayHint.Collection;
        var active = new TaskCompletionSource<Collection>();
        A.CallTo(() => _collectionService.GetActiveAsync(A<CancellationToken>.Ignored)).Returns(active.Task);

        // Act
        var initializing = _viewModel.InitializeAsync();
        _messenger.Send(new CurrentCollectionChangedEvent("Safe"));
        active.SetResult(new Collection { Title = "Inbox" });
        await initializing;

        // Assert
        await Assert.That(_viewModel.Text).IsEqualTo("Safe");
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
        await Assert.That(_viewModel.Text).IsEqualTo("InBox");
    }

    [Test]
    public async Task Text_HotkeyMovesPowerPasteWithinItsSequence_FollowsPowerPaste()
    {
        // Arrange: Power Paste owns the Current Clip while it runs
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        var powerPaste = new PowerPasteService(configurationService, A.Fake<ISoundService>(), A.Fake<ILogger<PowerPasteService>>());
        using var viewModel = new TrayTooltipViewModel(_messenger, configurationService, powerPaste, _collectionService);
        var third = new Clip { Id = Guid.NewGuid(), Title = "Third", TextContent = "Third", Type = ClipType.Text };
        await powerPaste.StartAsync(
        [
            new Clip { Id = Guid.NewGuid(), Title = "First", TextContent = "First", Type = ClipType.Text },
            new Clip { Id = Guid.NewGuid(), Title = "Second", TextContent = "Second", Type = ClipType.Text },
            third,
        ], PowerPasteDirection.Down);

        // Act: Scroll Next reached a clip of the sequence, so the sequence moves there (ClipOperationsCoordinator)
        powerPaste.MoveTo(third.Id);

        // Assert
        await Assert.That(viewModel.Text).IsEqualTo("Third");
    }

    [Test]
    public async Task SystemTrayHint_Default_IsClipTitle()
    {
        // Assert: ClipMate 7's default (Preferences\UseCollTitle = 0)
        await Assert.That(new AppearanceConfiguration().SystemTrayHint).IsEqualTo(SystemTrayHint.ClipTitle);
    }
}
