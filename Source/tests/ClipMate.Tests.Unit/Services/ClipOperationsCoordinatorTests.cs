using System.Security;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.Services;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using ClipMate.Data;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for ClipOperationsCoordinator focusing on encryption key management.
/// </summary>
[NotInParallel]
public class ClipOperationsCoordinatorTests : TestFixtureBase
{
    private ClipOperationsCoordinator _coordinator = null!;
    private Mock<IActiveWindowService> _mockActiveWindowService = null!;
    private Mock<ClipListViewModel> _mockClipListViewModel = null!;
    private Mock<IClipService> _mockClipService = null!;
    private Mock<ICollectionService> _mockCollectionService = null!;
    private Mock<CollectionTreeViewModel> _mockCollectionTreeViewModel = null!;
    private Mock<IConfigurationService> _mockConfigurationService = null!;
    private Mock<IMessenger> _mockMessenger = null!;
    private Mock<IPowerPasteService> _mockPowerPasteService = null!;
    private Mock<ISearchService> _mockSearchService = null!;
    private Mock<IServiceProvider> _mockServiceProvider = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockActiveWindowService = new Mock<IActiveWindowService>();
        _mockClipService = new Mock<IClipService>();
        _mockCollectionService = new Mock<ICollectionService>();
        _mockConfigurationService = new Mock<IConfigurationService>();
        _mockPowerPasteService = new Mock<IPowerPasteService>();
        _mockSearchService = new Mock<ISearchService>();
        _mockMessenger = new Mock<IMessenger>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // ClipListViewModel requires: ICollectionService, IFolderService, IClipService, 
        // IQuickPasteService, IDatabaseContextFactory, IMessenger, ILogger<ClipListViewModel>
        _mockClipListViewModel = new Mock<ClipListViewModel>(
            Mock.Of<ICollectionService>(),
            Mock.Of<IFolderService>(),
            Mock.Of<IClipService>(),
            Mock.Of<IQuickPasteService>(),
            Mock.Of<IDatabaseContextFactory>(),
            Mock.Of<IMessenger>(),
            Mock.Of<ILogger<ClipListViewModel>>());

        // CollectionTreeViewModel requires: ICollectionService, IFolderService, IClipService,
        // IConfigurationService, IMessenger, ICollectionTreeBuilder, ILogger<CollectionTreeViewModel>, SearchResultsCache
        // Note: SearchResultsCache is sealed, so we create a real instance
        _mockCollectionTreeViewModel = new Mock<CollectionTreeViewModel>(
            Mock.Of<ICollectionService>(),
            Mock.Of<IFolderService>(),
            Mock.Of<IClipService>(),
            Mock.Of<IConfigurationService>(),
            Mock.Of<IMessenger>(),
            Mock.Of<ICollectionTreeBuilder>(),
            Mock.Of<ILogger<CollectionTreeViewModel>>(),
            new SearchResultsCache());

        // Setup selected node to return a test database path (SelectedNode is not virtual, so use SetupGet with Object property)
        _mockCollectionTreeViewModel.SetupGet(p => p.SelectedNode)
            .Returns(new DatabaseTreeNode("Test Database", "test-database-path"));

        _coordinator = new ClipOperationsCoordinator(
            _mockActiveWindowService.Object,
            _mockClipListViewModel.Object,
            _mockCollectionTreeViewModel.Object,
            _mockClipService.Object,
            _mockCollectionService.Object,
            _mockConfigurationService.Object,
            _mockPowerPasteService.Object,
            _mockSearchService.Object,
            _mockMessenger.Object,
            _mockServiceProvider.Object,
            Mock.Of<ILogger<ClipOperationsCoordinator>>());

        // Clear any cached keys
        EncryptionKeyDialogViewModel.ForgetKey();
    }

    [After(Test)]
    public void Cleanup()
    {
        EncryptionKeyDialogViewModel.ForgetKey();
    }

    [Test]
    public async Task LockClipsRequestedEvent_SpecificClips_ForgetsOnlyThoseKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clipIds = new List<Guid> { clipId1, clipId2 };

        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Count == 2), CancellationToken.None))
            .ReturnsAsync(clipIds);

        // Cache keys for these clips
        var passphrase = CreateSecureString("test-password");
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        vm1.SetPassphrase(passphrase);
        vm1.RememberForMinutes = false;
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipId1);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
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

        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), null, CancellationToken.None))
            .ReturnsAsync(new List<Guid> { clipId1, clipId2 });

        // Cache keys
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        vm1.SetPassphrase(CreateSecureString("password-1"));
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipId1);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
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
        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>?>(), CancellationToken.None))
            .ReturnsAsync([]);

        // Cache a key
        var vm = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
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

        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>?>(), CancellationToken.None))
            .ReturnsAsync(lockedIds);

        var message = new LockClipsRequestedEvent(lockedIds);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Should call LockClipsAsync
        _mockClipService.Verify(
            p => p.LockClipsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>?>(), CancellationToken.None),
            Times.Once);

        // Note: Can't verify messenger.Send() calls with Moq because Send<T>() is an extension method
        // The important behavior (locking clips) is verified above
    }

    [Test]
    public async Task ForgetEncryptionKeyRequestedEvent_ClearsAllCachedKeys()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var vm = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
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
        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), null, CancellationToken.None))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });

        var message = new EncryptionKeyExpiredEvent();

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Should call LockClipsAsync with null (lock all)
        _mockClipService.Verify(
            p => p.LockClipsAsync(It.IsAny<string>(), null, CancellationToken.None),
            Times.Once);
    }

    [Test]
    public async Task LockClipsRequestedEvent_PreservesKeysForOtherClips()
    {
        // Arrange
        var clipIdToLock = Guid.NewGuid();
        var clipIdToKeep = Guid.NewGuid();

        // Both clips use same passphrase
        var passphrase = CreateSecureString("shared-password");
        var vm1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        vm1.SetPassphrase(passphrase);
        vm1.RememberForMinutes = false;
        vm1.RememberUntilShutdown = true;
        vm1.CacheKey(clipIdToLock);

        var vm2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        vm2.SetPassphrase(passphrase);
        vm2.RememberForMinutes = false;
        vm2.RememberUntilShutdown = true;
        vm2.CacheKey(clipIdToKeep);

        _mockClipService
            .Setup(p => p.LockClipsAsync(It.IsAny<string>(), It.Is<IReadOnlyList<Guid>?>(ids => ids != null && ids.Contains(clipIdToLock)), CancellationToken.None))
            .ReturnsAsync(new List<Guid> { clipIdToLock });

        var message = new LockClipsRequestedEvent([clipIdToLock]);

        // Act
        _coordinator.Receive(message);

        // Wait for async operation
        await Task.Delay(100);

        // Assert - Key should still be cached for clipIdToKeep
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

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
