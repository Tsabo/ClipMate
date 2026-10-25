using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// Tests for how ClipMate names a clip and its collection outside the clip list (tray tooltip, hotkey popup).
/// </summary>
public class ClipDescriptionTests
{
    [Test]
    public async Task Title_PlainClip_IsItsTitle()
    {
        // Arrange
        var clip = new Clip { Title = "Meeting notes" };

        // Act
        var title = ClipDescription.TitleOf(clip);

        // Assert
        await Assert.That(title).IsEqualTo("Meeting notes");
    }

    [Test]
    public async Task Title_EncryptedClip_NeverShowsItsTitle()
    {
        // Arrange: renamed by the user and decrypted with a remembered key, so the clip list would show its title
        var clip = new Clip { Title = "Bank PIN", CustomTitle = true, Encrypted = true, IsDecrypted = true };

        // Act
        var title = ClipDescription.TitleOf(clip);

        // Assert
        await Assert.That(title).IsEqualTo("Encrypted clip");
    }

    [Test]
    public async Task Collection_LocalCollection_IsItsName()
    {
        // Act
        var collection = ClipDescription.CollectionOf("Inbox");

        // Assert
        await Assert.That(collection).IsEqualTo("Inbox");
    }

    [Test]
    public async Task Collection_RemoteDatabaseCollection_NamesThePeer()
    {
        // Act
        var collection = ClipDescription.CollectionOf("Inbox", "Work laptop");

        // Assert
        await Assert.That(collection).IsEqualTo("Inbox on Work laptop");
    }
}
