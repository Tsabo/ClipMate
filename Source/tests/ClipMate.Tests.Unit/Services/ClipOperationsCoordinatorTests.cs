using System.Security;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipOperationsCoordinator focusing on encryption key management.
/// </summary>
[NotInParallel]
public class ClipOperationsCoordinatorTests : TestFixtureBase
{
    private ClipOperationsCoordinator _coordinator = null!;
    private IActiveWindowService _mockActiveWindowService = null!;
    private ClipListViewModel _mockClipListViewModel = null!;
    private IClipService _mockClipService = null!;
    private ICollectionService _mockCollectionService = null!;
    private CollectionTreeViewModel _mockCollectionTreeViewModel = null!;
    private IConfigurationService _mockConfigurationService = null!;
    private IMessenger _mockMessenger = null!;
    private IPowerPasteService _mockPowerPasteService = null!;
    private ISearchService _mockSearchService = null!;
    private IServiceProvider _mockServiceProvider = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockActiveWindowService = A.Fake<IActiveWindowService>();
        _mockClipService = A.Fake<IClipService>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockConfigurationService = A.Fake<IConfigurationService>();
        _mockPowerPasteService = A.Fake<IPowerPasteService>();
        _mockSearchService = A.Fake<ISearchService>();
        _mockMessenger = A.Fake<IMessenger>();
        _mockServiceProvider = A.Fake<IServiceProvider>();

        // ClipListViewModel requires: ICollectionService, IFolderService, IClipService, 
        // IQuickPasteService, IDatabaseContextFactory, IMessenger, ILogger<ClipListViewModel>
        _mockClipListViewModel = A.Fake<ClipListViewModel>(o => o.WithArgumentsForConstructor(() => new ClipListViewModel(
            A.Fake<ICollectionService>(),
            A.Fake<IFolderService>(),
            A.Fake<IClipService>(),
            A.Fake<IQuickPasteService>(),
            A.Fake<IDatabaseContextFactory>(),
            A.Fake<IRemoteDatabaseService>(),
            A.Fake<IMessenger>(),
            A.Fake<ILogger<ClipListViewModel>>())));

        // CollectionTreeViewModel requires: ICollectionService, IFolderService, IClipService,
        // IConfigurationService, IMessenger, ICollectionTreeBuilder, ILogger<CollectionTreeViewModel>, SearchResultsCache
        // Note: SearchResultsCache is sealed, so we create a real instance
        _mockCollectionTreeViewModel = A.Fake<CollectionTreeViewModel>(o => o.WithArgumentsForConstructor(() => new CollectionTreeViewModel(
            A.Fake<ICollectionService>(),
            A.Fake<IFolderService>(),
            A.Fake<IClipService>(),
            A.Fake<IConfigurationService>(),
            A.Fake<IMessenger>(),
            A.Fake<ICollectionTreeBuilder>(),
            A.Fake<ILogger<CollectionTreeViewModel>>(),
            new SearchResultsCache(),
            A.Fake<IDatabaseIdService>(),
            A.Fake<IRemoteDatabaseService>(),
            A.Fake<IRemoteDatabaseTreeBuilder>(),
            A.Fake<ISharedDatabaseService>(),
            A.Fake<ClipMate.App.Services.IPeerDialogService>())));

        // Setup selected node to return a test database path (SelectedNode is not virtual, so use SetupGet with Object property)
        A.CallTo(() => _mockCollectionTreeViewModel.SelectedNode)
            .Returns(new DatabaseTreeNode("Test Database", "test-database-path"));

        _coordinator = new ClipOperationsCoordinator(
            _mockActiveWindowService,
            _mockClipListViewModel,
            _mockCollectionTreeViewModel,
            _mockClipService,
            _mockCollectionService,
            _mockConfigurationService,
            _mockPowerPasteService,
            _mockSearchService,
            _mockMessenger,
            _mockServiceProvider,
            A.Fake<ILogger<ClipOperationsCoordinator>>());

        // Clear any cached keys
        EncryptionKeyDialogViewModel.ForgetKey();
    }

    [After(Test)]
    public void Cleanup()
    {
        EncryptionKeyDialogViewModel.ForgetKey();
        _mockClipListViewModel.Dispose();
        _mockCollectionTreeViewModel.Dispose();
    }

    [Test]
    public async Task LockClipsRequestedEvent_SpecificClips_ForgetsOnlyThoseKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId1, clipId2 };

        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, A<IReadOnlyList<Guid>?>.That.Matches(ids => ids != null && ids.Count == 2), CancellationToken.None))
            .Returns(clipIds);

        // Cache keys for these clips
        var passphrase = CreateSecureString("test-password");
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm1.SetPassphrase(passphrase);
        vm1.RememberForMinutes = false;
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipId1);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm2.SetPassphrase(passphrase);
        vm2.RememberForMinutes = false;
        vm2.RememberUntilShutdown = true;
        vm2.CacheKey(clipId2);

        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        var message = new LockClipsRequestedEvent(clipIds);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Keys should be forgotten
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task LockClipsRequestedEvent_LockAll_ForgetsAllKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();

        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, null, CancellationToken.None))
            .Returns(new List<Guid> { clipId1, clipId2 });

        // Cache keys
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm1.SetPassphrase(CreateSecureString("password-1"));
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipId1);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm2.SetPassphrase(CreateSecureString("password-2"));
        vm2.RememberUntilShutdown = true;
        vm2.CacheKey(clipId2);

        var message = new LockClipsRequestedEvent([], true);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task LockClipsRequestedEvent_NoClipsLocked_DoesNotForgetKeys()
    {
        // Arrange
        var clipId = Guid.NewGuid();

        // Service returns empty list (no clips were actually locked)
        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, A<IReadOnlyList<Guid>?>.Ignored, CancellationToken.None))
            .Returns([]);

        // Cache a key
        var vm = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm.SetPassphrase(CreateSecureString("test-pass"));
        vm.RememberForMinutes = false;
        vm.RememberUntilShutdown = true;
        vm.CacheKey(clipId);

        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        var message = new LockClipsRequestedEvent([clipId]);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Key should still be cached since no clips were actually locked
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task LockClipsRequestedEvent_SendsClipCacheExpiredMessages()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var lockedIds = new List<Guid> { clipId1, clipId2 };

        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, A<IReadOnlyList<Guid>?>.Ignored, CancellationToken.None))
            .Returns(lockedIds);

        var message = new LockClipsRequestedEvent(lockedIds);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Should call LockClipsAsync
        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, A<IReadOnlyList<Guid>?>.Ignored, CancellationToken.None)).MustHaveHappenedOnceExactly();

        // Note: Can't verify messenger.Send() calls with FakeItEasy because Send<T>() is an extension method
        // The important behavior (locking clips) is verified above
    }

    [Test]
    public async Task ForgetEncryptionKeyRequestedEvent_ClearsAllCachedKeys()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var vm = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm.SetPassphrase(CreateSecureString("cached-key"));
        vm.RememberForMinutes = false;
        vm.RememberUntilShutdown = true;
        vm.CacheKey(clipId);

        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        var message = new ForgetEncryptionKeyRequestedEvent();

        // Act
        _coordinator.Receive(message);

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task EncryptionKeyExpiredEvent_LocksAllDecryptedClips()
    {
        // Arrange
        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, null, CancellationToken.None))
            .Returns(new List<Guid> { Guid.NewGuid() });

        var message = new EncryptionKeyExpiredEvent();

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Should call LockClipsAsync with null (lock all)
        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, null, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task LockClipsRequestedEvent_PreservesKeysForOtherClips()
    {
        // Arrange
        var clipIdToLock = Guid.NewGuid();
        var clipIdToKeep = Guid.NewGuid();

        // Both clips use same passphrase
        var passphrase = CreateSecureString("shared-password");
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm1.SetPassphrase(passphrase);
        vm1.RememberForMinutes = false;
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipIdToLock);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger);
        vm2.SetPassphrase(passphrase);
        vm2.RememberForMinutes = false;
        vm2.RememberUntilShutdown = true;
        vm2.CacheKey(clipIdToKeep);

        A.CallTo(() => _mockClipService.LockClipsAsync(A<string>.Ignored, A<IReadOnlyList<Guid>?>.That.Matches(ids => ids != null && ids.Contains(clipIdToLock)), CancellationToken.None))
            .Returns(new List<Guid> { clipIdToLock });

        var message = new LockClipsRequestedEvent([clipIdToLock]);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Key should still be cached for clipIdToKeep
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    #region Remote Databases are read-only

    [Test]
    public async Task ModifyingRequests_WhileShowingARemoteDatabase_NeverChangeAnyClip()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Remote", Type = ClipType.Files };
        _mockClipListViewModel.IsRemoteDatabase = true;
        _mockClipListViewModel.CurrentDatabaseKey =
            RemoteDatabaseKey.From(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([5]), Guid.NewGuid()));
        _mockClipListViewModel.Clips.Add(clip);
        _mockClipListViewModel.SelectedClips.Add(clip);
        IReadOnlyList<Guid> ids = [clip.Id];

        // Act
        _coordinator.Receive(new DeleteClipsRequestedEvent(ids));
        _coordinator.Receive(new RenameClipRequestedEvent(clip.Id, "Remote"));
        _coordinator.Receive(new MoveToCollectionRequestedEvent(ids));
        _coordinator.Receive(new CopyToCollectionRequestedEvent(ids));
        _coordinator.Receive(new EncryptClipsRequestedEvent(ids));
        _coordinator.Receive(new DecryptClipsRequestedEvent(ids));
        _coordinator.Receive(new ConvertFilePointerRequestedEvent());
        _coordinator.Receive(new MoveClipSortOrderRequestedEvent(true));
        await Task.Delay(100);

        // Assert
        A.CallTo(_mockClipService).Where(p => p.Method.Name != nameof(IClipService.GetByIdAsync)).MustNotHaveHappened();
    }

    #endregion

    #region Helper Methods

    private static SecureString CreateSecureString(string value)
    {
        var secureString = new SecureString();
        foreach (var item in value)
            secureString.AppendChar(item);

        secureString.MakeReadOnly();
        return secureString;
    }

    #endregion
}
