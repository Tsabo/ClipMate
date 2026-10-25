using System.Text;
using ClipMate.Data.Legacy.ClipMate7;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for decrypting clips encrypted by ClipMate 7, using a clip captured from a real ClipMate 7 database:
/// the text "webAuthn.js" encrypted with the key "test" (AllowChecksum on), timestamp 20:56:55.125.
/// </summary>
public class ClipMate7EncryptionTests
{
    private const string _key = "test";
    private const string _plainText = "webAuthn.js";
    private const string _storedText = "55.125:OaDZ/o6mCsvmqWk=";
    private const string _storedTitle = "OaDZ/o6mCsvmqWk=";
    private const int _checksum = -98664153;

    private static byte[] Stored(string text) => Encoding.ASCII.GetBytes(text);

    [Test]
    public async Task TryDecryptText_CapturedClip_ReturnsPlainTextAndSalt()
    {
        // Act
        var ok = new ClipMate7Encryption(_key).TryDecryptText(Stored(_storedText), _checksum, out var plain, out var salt);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(plain)).IsEqualTo(_plainText);
        await Assert.That(Encoding.ASCII.GetString(salt)).IsEqualTo("55.125");
    }

    [Test]
    public async Task TryDecryptText_TrailingNul_IsIgnored()
    {
        // Arrange: CF_TEXT data as stored in BLOBTXT may be NUL-terminated
        var stored = Stored(_storedText + "\0");

        // Act
        var ok = new ClipMate7Encryption(_key).TryDecryptText(stored, _checksum, out var plain, out _);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(plain)).IsEqualTo(_plainText);
    }

    [Test]
    [Arguments("wrong")]
    [Arguments("test2")]
    [Arguments("tset")]
    public async Task TryDecryptText_WrongKey_FailsTheChecksum(string key)
    {
        // Act
        var ok = new ClipMate7Encryption(key).TryDecryptText(Stored(_storedText), _checksum, out _, out _);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    [Arguments("best")]
    [Arguments("Test")]
    public async Task TryDecryptText_KeyDifferingOnlyInFirstCharacter_Decrypts(string key)
    {
        // ClipMate 7's RC4 key is characters 2..8 of passphrase + salt, so the first character never counts

        // Act
        var ok = new ClipMate7Encryption(key).TryDecryptText(Stored(_storedText), _checksum, out var plain, out _);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(plain)).IsEqualTo(_plainText);
    }

    [Test]
    public async Task TryDecryptText_NoChecksum_DecryptsWithRightKeyAndRejectsBinaryNoise()
    {
        var stored = Stored(_storedText);

        // Act
        var right = new ClipMate7Encryption(_key).TryDecryptText(stored, 0, out var plain, out _);
        var wrong = new ClipMate7Encryption("test2").TryDecryptText(stored, 0, out _, out _);

        // Assert
        await Assert.That(right).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(plain)).IsEqualTo(_plainText);
        await Assert.That(wrong).IsFalse();
    }

    [Test]
    [Arguments("webAuthn.js")]
    [Arguments("55.125")]
    [Arguments("55.125;OaDZ/o6mCsvmqWk=")]
    [Arguments("55.125:not base64!")]
    public async Task TryDecryptText_NotClipMate7Ciphertext_ReturnsFalse(string stored)
    {
        // Act
        var ok = new ClipMate7Encryption(_key).TryDecryptText(Stored(stored), _checksum, out _, out _);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task TryDecryptTitle_CapturedTitle_ReturnsTitle()
    {
        // Act
        var title = new ClipMate7Encryption(_key).TryDecryptTitle(_storedTitle, Stored("55.125"));

        // Assert
        await Assert.That(title).IsEqualTo(_plainText);
    }

    [Test]
    public async Task TryDecryptTitle_TruncatedBase64_ReturnsStartOfTitle()
    {
        // Arrange: ClipMate 7 cuts long encrypted titles off at the TITLE field width
        var truncated = _storedTitle[..10];

        // Act
        var title = new ClipMate7Encryption(_key).TryDecryptTitle(truncated, Stored("55.125"));

        // Assert: two complete Base64 groups = 6 bytes
        await Assert.That(title).IsEqualTo(_plainText[..6]);
    }

    [Test]
    public async Task TryDecryptTitle_WrongKey_ReturnsNullOrDifferentTitle()
    {
        // Act
        var title = new ClipMate7Encryption("test2").TryDecryptTitle(_storedTitle, Stored("55.125"));

        // Assert
        await Assert.That(title).IsNotEqualTo(_plainText);
    }
}
