using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using ClipMate.Data;
using DevExpress.Mvvm;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using IMessenger = CommunityToolkit.Mvvm.Messaging.IMessenger;

namespace ClipMate.Tests.Unit.Services;

public sealed class HotkeyNavigationPopupTests : IDisposable
{
    private readonly IClipService _clips = A.Fake<IClipService>();
    private readonly ClipListViewModel _clipList;
    private readonly ICollectionService _collections = A.Fake<ICollectionService>();
    private readonly ClipMateConfiguration _configuration = new();
    private readonly StrongReferenceMessenger _messenger = new();
    private readonly IClipPopupNotifications _notifications = A.Fake<IClipPopupNotifications>();
    private readonly List<HotkeyNavigationPopupViewModel> _shown = [];

    public HotkeyNavigationPopupTests()
    {
        _clipList = new ClipListViewModel(_collections, A.Fake<IFolderService>(), _clips, A.Fake<IQuickPasteService>(), new PowerPasteStatus(),
            A.Fake<IDatabaseContextFactory>(), A.Fake<IRemoteDatabaseService>(), A.Fake<IMessenger>(), NullLogger<ClipListViewModel>.Instance);

        A.CallTo(() => _notifications.Create(A<HotkeyNavigationPopupViewModel>._))
            .ReturnsLazily((HotkeyNavigationPopupViewModel viewModel) =>
            {
                _shown.Add(viewModel);
                return A.Fake<INotification>();
            });
    }

    [Test]
    public async Task ClipReachedByHotkey_TextClip_ShowsItsTitleCollectionAndFirstThreeLines()
    {
        // Arrange
        var inbox = new Collection { Id = Guid.NewGuid(), Title = "Inbox" };
        A.CallTo(() => _collections.GetAllByDatabaseKeyAsync("db_local", A<CancellationToken>._)).Returns([inbox]);
        var clip = new Clip
        {
            Id = Guid.NewGuid(), CollectionId = inbox.Id, Title = "Shopping list", Type = ClipType.Text,
            TextContent = "milk\r\neggs\r\nbread\r\nbutter",
        };

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(clip, "db_local"));

        // Assert
        await Assert.That(_shown).Count().IsEqualTo(1);
        await Assert.That(_shown[0].Title).IsEqualTo("Shopping list");
        await Assert.That(_shown[0].Collection).IsEqualTo("Inbox");
        await Assert.That(_shown[0].PreviewText).IsEqualTo("milk\neggs\nbread");
    }

    [Test]
    public async Task ClipReachedByHotkey_ContentNotLoaded_LoadsItForThePreview()
    {
        // Arrange - clips in the list carry only their metadata
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Note", Type = ClipType.Text };
        A.CallTo(() => _clips.LoadBlobDataAsync("db_local", clip, A<CancellationToken>._))
            .Invokes(() => clip.TextContent = "first line");

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(clip, "db_local"));

        // Assert
        await Assert.That(_shown.Single().PreviewText).IsEqualTo("first line");
    }

    [Test]
    public async Task ClipReachedByHotkey_EncryptedClip_ShowsNeitherTitleNorContent()
    {
        // Arrange - its key is remembered, so the clip list would show its text
        var clip = new Clip
        {
            Id = Guid.NewGuid(), Title = "Bank PIN", CustomTitle = true, Encrypted = true, IsDecrypted = true, Type = ClipType.Text,
            TextContent = "1234",
        };

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(clip, "db_local"));

        // Assert
        var shown = _shown.Single();
        await Assert.That(shown.Title).IsEqualTo("Encrypted clip");
        await Assert.That(shown.PreviewText).IsNull();
        await Assert.That(shown.Thumbnail).IsNull();
        A.CallTo(() => _clips.LoadBlobDataAsync(A<string>._, A<Clip>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ClipReachedByHotkey_OptionTurnedOff_ShowsNothing()
    {
        // Arrange
        _configuration.Appearance.ShowClipWhenScrollingWithHotkeys = false;
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Note", TextContent = "text" };

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(clip, "db_local"));

        // Assert
        await Assert.That(_shown).IsEmpty();
    }

    [Test]
    public async Task ShowClipWhenScrollingWithHotkeys_ByDefault_IsOn()
    {
        await Assert.That(new AppearanceConfiguration().ShowClipWhenScrollingWithHotkeys).IsTrue();
    }

    [Test]
    public async Task ClipReachedByHotkey_WhileAPopupIsShown_HidesItBeforeShowingTheNext()
    {
        // Arrange
        var notifications = new List<INotification>();
        A.CallTo(() => _notifications.Create(A<HotkeyNavigationPopupViewModel>._)).ReturnsLazily(() =>
        {
            var notification = A.Fake<INotification>();
            A.CallTo(() => notification.ShowAsync()).Returns(new TaskCompletionSource<NotificationResult>().Task);
            notifications.Add(notification);
            return notification;
        });

        var popup = CreatePopup();

        // Act
        await popup.ShowAsync(new ClipReachedByHotkeyEvent(new Clip { Id = Guid.NewGuid(), TextContent = "one" }, "db_local"));
        await popup.ShowAsync(new ClipReachedByHotkeyEvent(new Clip { Id = Guid.NewGuid(), TextContent = "two" }, "db_local"));

        // Assert
        A.CallTo(() => notifications[0].Hide()).MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => notifications[1].ShowAsync()).MustHaveHappenedOnceExactly());
        A.CallTo(() => notifications[1].Hide()).MustNotHaveHappened();
    }

    [Test]
    public async Task ClipReachedByHotkey_EarlierClipStillLoading_ShowsOnlyTheLaterClip()
    {
        // Arrange - rapid presses: the first clip's content arrives after the second clip's popup is shown
        var firstLoaded = new TaskCompletionSource();
        var first = new Clip { Id = Guid.NewGuid(), Title = "First" };
        var second = new Clip { Id = Guid.NewGuid(), Title = "Second", TextContent = "ready" };
        A.CallTo(() => _clips.LoadBlobDataAsync("db_local", first, A<CancellationToken>._)).Returns(firstLoaded.Task);
        var popup = CreatePopup();

        // Act
        var showingFirst = popup.ShowAsync(new ClipReachedByHotkeyEvent(first, "db_local"));
        await popup.ShowAsync(new ClipReachedByHotkeyEvent(second, "db_local"));
        firstLoaded.SetResult();
        await showingFirst;

        // Assert
        await Assert.That(_shown.Select(p => p.Title)).IsEquivalentTo(["Second"]);
    }

    [Test]
    public async Task PopupClicked_OpensClipMateWithThatClipSelected()
    {
        // Arrange - the user moved on in the clip list before clicking
        var reached = new Clip { Id = Guid.NewGuid(), TextContent = "reached" };
        var other = new Clip { Id = Guid.NewGuid(), TextContent = "other" };
        _clipList.Clips.Add(reached);
        _clipList.Clips.Add(other);
        _clipList.SelectClipWithoutCopying(other.Id);

        var clicked = new TaskCompletionSource<NotificationResult>();
        var notification = A.Fake<INotification>();
        A.CallTo(() => notification.ShowAsync()).Returns(clicked.Task);
        A.CallTo(() => _notifications.Create(A<HotkeyNavigationPopupViewModel>._)).Returns(notification);
        var explorerShown = false;
        _messenger.Register<ShowExplorerWindowEvent>(this, (_, _) => explorerShown = true);
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(reached, "db_local"));

        // Act
        clicked.SetResult(NotificationResult.Activated);

        // Assert
        await Assert.That(explorerShown).IsTrue();
        await Assert.That(_clipList.SelectedClip).IsEqualTo(reached);
    }

    [Test]
    public async Task PopupTimedOut_LeavesClipMateClosed()
    {
        // Arrange
        var notification = A.Fake<INotification>();
        A.CallTo(() => notification.ShowAsync()).Returns(NotificationResult.TimedOut);
        A.CallTo(() => _notifications.Create(A<HotkeyNavigationPopupViewModel>._)).Returns(notification);
        var explorerShown = false;
        _messenger.Register<ShowExplorerWindowEvent>(this, (_, _) => explorerShown = true);

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(new Clip { Id = Guid.NewGuid() }, "db_local"));

        // Assert
        await Assert.That(explorerShown).IsFalse();
    }

    [Test]
    public async Task ClipReachedByHotkey_ImageClip_ShowsAThumbnail()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Screenshot", Type = ClipType.Image, ImageData = CreatePng(400, 300) };

        // Act
        await CreatePopup().ShowAsync(new ClipReachedByHotkeyEvent(clip, "db_local"));

        // Assert
        var shown = _shown.Single();
        var thumbnail = shown.Thumbnail as BitmapSource;
        await Assert.That(thumbnail).IsNotNull();
        await Assert.That(thumbnail!.PixelWidth).IsEqualTo(HotkeyNavigationPopupViewModel.ThumbnailSize);
        await Assert.That(thumbnail.PixelHeight).IsEqualTo(72);
        await Assert.That(shown.PreviewText).IsNull();
    }

    [Test]
    public async Task ClipReachedByHotkeyEvent_Sent_ShowsThePopup()
    {
        // Arrange
        _ = CreatePopup();

        // Act
        _messenger.Send(new ClipReachedByHotkeyEvent(new Clip { Id = Guid.NewGuid(), Title = "Sent", TextContent = "text" }, "db_local"));

        // Assert - every call it makes completes at once, so the popup is already shown
        await Assert.That(_shown.Single().Title).IsEqualTo("Sent");
    }

    private static byte[] CreatePng(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private HotkeyNavigationPopup CreatePopup() => new(
        new ClipDescriber(_collections, A.Fake<IRemoteDatabaseService>()),
        _clips,
        ConfigurationService(),
        _notifications,
        _clipList,
        _messenger,
        NullLogger<HotkeyNavigationPopup>.Instance);

    private IConfigurationService ConfigurationService()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        return configurationService;
    }

    public void Dispose() => _clipList.Dispose();
}
