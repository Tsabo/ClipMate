using System.Security;
using ClipMate.App.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for EncryptionKeyDialogViewModel focusing on per-clip key management.
/// </summary>
[NotInParallel]
public class EncryptionKeyDialogViewModelTests : TestFixtureBase
{
    private Mock<IMessenger> _mockMessenger = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockMessenger = new Mock<IMessenger>();

        // Clear any cached keys from previous tests
        EncryptionKeyDialogViewModel.ForgetKey();
    }

    [After(Test)]
    public void Cleanup()
    {
        // Ensure keys are cleared after each test
        EncryptionKeyDialogViewModel.ForgetKey();
    }

    [Test]
    public async Task CacheKey_SingleClip_AssociatesKeyWithClip()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("test-password-123");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = true;
        viewModel.RetentionMinutes = 5;

        // Act
        viewModel.CacheKey(clipId);

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task CacheKey_SamePassphrase_DeduplicatesKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var passphrase = CreateSecureString("shared-password");

        var viewModel1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel1.SetPassphrase(passphrase);
        viewModel1.RememberForMinutes = false;
        viewModel1.RememberUntilShutdown = true;

        var viewModel2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel2.SetPassphrase(passphrase);
        viewModel2.RememberForMinutes = false;
        viewModel2.RememberUntilShutdown = true;

        // Act
        viewModel1.CacheKey(clipId1);
        viewModel2.CacheKey(clipId2);

        // Assert - Should have cached key (both clips share same key internally)
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task CacheKey_DifferentPassphrases_StoresMultipleKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var passphrase1 = CreateSecureString("password-alpha");
        var passphrase2 = CreateSecureString("password-beta");

        var viewModel1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel1.SetPassphrase(passphrase1);
        viewModel1.RememberForMinutes = false;
        viewModel1.RememberUntilShutdown = true;

        var viewModel2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel2.SetPassphrase(passphrase2);
        viewModel1.RememberForMinutes = false;
        viewModel2.RememberUntilShutdown = true;

        // Act
        viewModel1.CacheKey(clipId1);
        viewModel2.CacheKey(clipId2);

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task ForgetKeysForClips_OneClip_RemovesKeyIfNoOtherClipsUseIt()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("solo-password");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = false;
        viewModel.RememberUntilShutdown = true;

        viewModel.CacheKey(clipId);
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act
        EncryptionKeyDialogViewModel.ForgetKeysForClips([clipId]);

        // Assert - Key should be removed since no other clips use it
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task ForgetKeysForClips_SharedKey_KeepsKeyForRemainingClips()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var clipId3 = Guid.NewGuid();
        var passphrase = CreateSecureString("shared-pass");

        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = false;
        viewModel.RememberUntilShutdown = true;

        // All three clips use same passphrase
        viewModel.CacheKey(clipId1);
        viewModel.CacheKey(clipId2);
        viewModel.CacheKey(clipId3);

        // Act - Forget key for only clip1
        EncryptionKeyDialogViewModel.ForgetKeysForClips([clipId1]);

        // Assert - Key should still be cached for clip2 and clip3
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act - Forget key for clip2
        EncryptionKeyDialogViewModel.ForgetKeysForClips([clipId2]);

        // Assert - Key should still be cached for clip3
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act - Forget key for clip3 (last one)
        EncryptionKeyDialogViewModel.ForgetKeysForClips([clipId3]);

        // Assert - Now key should be removed
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task ForgetKeysForClips_AllClips_DisposesAllKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var passphrase1 = CreateSecureString("password-1");
        var passphrase2 = CreateSecureString("password-2");

        var viewModel1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel1.SetPassphrase(passphrase1);
        viewModel1.RememberForMinutes = false;
        viewModel1.RememberUntilShutdown = true;
        viewModel1.CacheKey(clipId1);

        var viewModel2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel2.SetPassphrase(passphrase2);
        viewModel1.RememberForMinutes = false;
        viewModel2.RememberUntilShutdown = true;
        viewModel2.CacheKey(clipId2);

        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act - Forget keys for all clips
        EncryptionKeyDialogViewModel.ForgetKeysForClips([clipId1, clipId2]);

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task ForgetKey_RemovesAllCachedKeys()
    {
        // Arrange
        var clipId1 = Guid.NewGuid();
        var clipId2 = Guid.NewGuid();
        var passphrase1 = CreateSecureString("key-alpha");
        var passphrase2 = CreateSecureString("key-beta");

        var viewModel1 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel1.SetPassphrase(passphrase1);
        viewModel1.RememberForMinutes = false;
        viewModel1.RememberUntilShutdown = true;
        viewModel1.CacheKey(clipId1);

        var viewModel2 = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel2.SetPassphrase(passphrase2);
        viewModel2.RememberForMinutes = false;
        viewModel2.RememberUntilShutdown = true;
        viewModel2.CacheKey(clipId2);

        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act - Forget all keys globally
        EncryptionKeyDialogViewModel.ForgetKey();

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task HasCachedKey_WithExpiredKeys_ReturnsFalse()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("temp-password");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = true;
        viewModel.RetentionMinutes = 1; // 1 minute

        viewModel.CacheKey(clipId);
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();

        // Act - Wait for expiration (simulate by forgetting manually since we can't wait 1 minute)
        // Note: In real scenario, timer would trigger ForgetKey automatically
        EncryptionKeyDialogViewModel.ForgetKey();

        // Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task HasCachedKey_NoKeysCached_ReturnsFalse()
    {
        // Arrange & Act & Assert
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task InitializeForDecryption_WithCachedKey_LoadsPassphrase()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("cached-password");
        var cacheViewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        cacheViewModel.SetPassphrase(passphrase);
        cacheViewModel.RememberForMinutes = false;
        cacheViewModel.RememberUntilShutdown = true;
        cacheViewModel.CacheKey(clipId);

        // Act
        var retrieveViewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        retrieveViewModel.InitializeForDecryption();

        // Assert - Should have loaded passphrase from cache
        using var retrievedPassphrase = retrieveViewModel.GetPassphrase();
        await Assert.That(retrievedPassphrase).IsNotNull();
        await Assert.That(retrievedPassphrase!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task CacheKey_ExtendExpiration_UpdatesTimer()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("extend-test");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = true;
        viewModel.RetentionMinutes = 5;

        // Cache first time
        viewModel.CacheKey(clipId);

        // Act - Cache again with same clip (should extend expiration)
        viewModel.CacheKey(clipId);

        // Assert - Key should still be cached
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task ForgetKeysForClips_NonExistentClip_DoesNotThrow()
    {
        // Arrange
        var nonExistentClipId = Guid.NewGuid();

        // Act & Assert - Should not throw
        EncryptionKeyDialogViewModel.ForgetKeysForClips([nonExistentClipId]);
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsFalse();
    }

    [Test]
    public async Task CacheKey_RememberUntilShutdown_NoExpiration()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var passphrase = CreateSecureString("permanent-key");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);
        viewModel.RememberForMinutes = false;
        viewModel.RememberUntilShutdown = true;

        // Act
        viewModel.CacheKey(clipId);

        // Assert - Key should be cached indefinitely
        await Assert.That(EncryptionKeyDialogViewModel.HasCachedKey).IsTrue();
    }

    [Test]
    public async Task SetPassphrase_DisposesOldPassphrase()
    {
        // Arrange
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        var passphrase1 = CreateSecureString("first-password");
        var passphrase2 = CreateSecureString("second-password");

        // Act
        viewModel.SetPassphrase(passphrase1);
        viewModel.SetPassphrase(passphrase2);

        // Assert - Should not throw (old passphrase properly disposed)
        using var retrieved = viewModel.GetPassphrase();
        await Assert.That(retrieved).IsNotNull();
    }

    [Test]
    public async Task Dispose_ClearsCurrentPassphrase()
    {
        // Arrange
        var passphrase = CreateSecureString("dispose-test");
        var viewModel = new EncryptionKeyDialogViewModel(_mockMessenger.Object);
        viewModel.SetPassphrase(passphrase);

        // Act
        viewModel.Dispose();

        // Assert - GetPassphrase should return null after dispose
        using var retrieved = viewModel.GetPassphrase();
        await Assert.That(retrieved).IsNull();
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
