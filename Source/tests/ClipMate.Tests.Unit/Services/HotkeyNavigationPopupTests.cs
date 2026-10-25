using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Mvvm;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

public class HotkeyNavigationPopupTests
{
    private readonly IClipService _clips = A.Fake<IClipService>();
    private readonly ICollectionService _collections = A.Fake<ICollectionService>();
    private readonly ClipMateConfiguration _configuration = new();
    private readonly StrongReferenceMessenger _messenger = new();
    private readonly IClipPopupNotifications _notifications = A.Fake<IClipPopupNotifications>();
    private readonly List<HotkeyNavigationPopupViewModel> _shown = [];

    public HotkeyNavigationPopupTests()
    {
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

    private HotkeyNavigationPopup CreatePopup() => new(
        new ClipDescriber(_collections, A.Fake<IRemoteDatabaseService>()),
        _clips,
        ConfigurationService(),
        _notifications,
        _messenger,
        NullLogger<HotkeyNavigationPopup>.Instance);

    private IConfigurationService ConfigurationService()
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(_configuration);
        return configurationService;
    }
}
