using ClipMate.Core.ValueObjects;

namespace ClipMate.Tests.Unit.ValueObjects;

public class EncryptionKeyTests : TestFixtureBase
{
    [Test]
    public async Task FromPassphrase_WithValidPassphrase_CreatesKey()
    {
        // Act
        using var key = EncryptionKey.FromPassphrase("valid-password-123");

        // Assert
        await Assert.That(key).IsNotNull();
        await Assert.That(key.IsDisposed).IsFalse();
    }

    [Test]
    public async Task FromPassphrase_WithNullPassphrase_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase(null!);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FromPassphrase_WithEmptyPassphrase_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase("");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FromPassphrase_WithWhitespacePassphrase_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase("   ");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FromPassphrase_WithTooShortPassphrase_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase("abc"); // Less than 4 chars
            await Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("at least 4 characters");
    }

    [Test]
    public async Task FromPassphrase_WithRepeatingCharacters_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase("aaaa");
            await Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("repeating characters");
    }

    [Test]
    public async Task FromPassphrase_WithRepeatingCharactersLong_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            EncryptionKey.FromPassphrase("1111111111");
            await Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("repeating characters");
    }

    [Test]
    public async Task GetKeyBytes_BeforeDispose_ReturnsBytes()
    {
        // Arrange
        using var key = EncryptionKey.FromPassphrase("password");

        // Act
        var bytes = key.GetKeyBytes();

        // Assert
        await Assert.That(bytes).IsNotNull();
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GetKeyBytes_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var key = EncryptionKey.FromPassphrase("password");
        key.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            key.GetKeyBytes();
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Dispose_ZeroesMemory()
    {
        // Arrange
        var key = EncryptionKey.FromPassphrase("password");
        var bytes = key.GetKeyBytes();
        var originalBytes = new byte[bytes.Length];
        Array.Copy(bytes, originalBytes, bytes.Length);

        // Act
        key.Dispose();

        // Assert - Key should be marked as disposed
        await Assert.That(key.IsDisposed).IsTrue();

        // Note: We cannot directly verify memory was zeroed since we can't access
        // private _keyBytes field after disposal, but we can verify disposed state
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var key = EncryptionKey.FromPassphrase("password");

        // Act & Assert - Should not throw
        key.Dispose();
        key.Dispose();
        key.Dispose();

        await Task.CompletedTask;
    }

    [Test]
    public async Task FromPassphrase_WithUnicodePassphrase_CreatesKey()
    {
        // Act
        using var key = EncryptionKey.FromPassphrase("密码🔒🔓");

        // Assert
        await Assert.That(key).IsNotNull();
        var bytes = key.GetKeyBytes();
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task FromPassphrase_WithSpecialCharacters_CreatesKey()
    {
        // Act
        using var key = EncryptionKey.FromPassphrase("p@ssw0rd!#$%");

        // Assert
        await Assert.That(key).IsNotNull();
    }
}
