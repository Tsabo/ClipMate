using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipAppendServiceTests
{
    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithMultipleClips_CreatesAppendedClip()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("First clip", collectionId: collectionId),
            CreateTestClip("Second clip", collectionId: collectionId),
            CreateTestClip("Third clip", collectionId: collectionId),
        };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        var result = await service.AppendClipsAsync(clips, "", false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("First clipSecond clipThird clip");
        await Assert.That(capturedClip.Title).IsEqualTo("Appended (3 clips)");
        await Assert.That(capturedClip.Type).IsEqualTo(ClipType.Text);
        await Assert.That(capturedClip.CollectionId).IsEqualTo(collectionId);
        mockClipRepo.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSoundService.Verify(p => p.PlaySoundAsync(SoundEvent.Append, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithSeparator_UsesSeparatorBetweenClips()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("First", collectionId: collectionId),
            CreateTestClip("Second", collectionId: collectionId),
        };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, " | ", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("First | Second");
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithNewlineSeparator_ProcessesEscapeSequence()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("Line1", collectionId: collectionId),
            CreateTestClip("Line2", collectionId: collectionId),
        };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, "\\n", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("Line1\nLine2");
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithStripTrailingLineBreaks_RemovesTrailingBreaks()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("Text1\n\n", collectionId: collectionId),
            CreateTestClip("Text2\r\n", collectionId: collectionId),
        };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, "", true);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("Text1Text2");
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithEmptyClipList_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var emptyClips = Array.Empty<Clip>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.AppendClipsAsync(emptyClips, "", false));
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

        var clips = new[] { CreateTestClip("Test") };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AppendClipsAsync(clips, "", false));
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithClipMissingTextContent_LoadsBlobData()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clipId = Guid.NewGuid();
        var clipWithoutText = new Clip
        {
            Id = clipId,
            TextContent = null, // Missing text content - grid-bound clips don't carry blob content by default
            Type = ClipType.Text,
            CapturedAt = DateTimeOffset.Now,
            CollectionId = collectionId,
            Title = "Test Clip",
            Creator = "TestUser",
        };

        _mockClipService.Setup(p => p.LoadBlobDataAsync(_testDatabaseKey, clipWithoutText, It.IsAny<CancellationToken>()))
            .Callback<string, Clip, CancellationToken>((_, clip, _) => clip.TextContent = "Loaded text")
            .Returns(Task.CompletedTask);

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync([clipWithoutText], "", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("Loaded text");
        _mockClipService.Verify(p => p.LoadBlobDataAsync(_testDatabaseKey, clipWithoutText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_WithClipWithoutTextAndNotLoadable_SkipsClip()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var emptyClipId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("Valid clip", collectionId: collectionId),
            new Clip
            {
                Id = emptyClipId,
                TextContent = null,
                Type = ClipType.Text,
                CapturedAt = DateTimeOffset.Now,
                CollectionId = collectionId,
                Title = "Empty",
                Creator = "TestUser",
            },
        };

        // LoadBlobDataAsync is left unconfigured for the empty clip - it won't populate TextContent,
        // so the clip should be skipped.

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, " | ", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.TextContent).IsEqualTo("Valid clip");
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_CalculatesCorrectSize()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[] { CreateTestClip("Test", collectionId: collectionId) };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, "", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.Size).IsGreaterThan(0);
        await Assert.That(capturedClip.Size).IsEqualTo(Encoding.UTF8.GetByteCount("Test"));
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_GeneratesContentHashAndChecksum()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[] { CreateTestClip("Hash test", collectionId: collectionId) };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        await service.AppendClipsAsync(clips, "", false);

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.ContentHash).IsNotNull();
        await Assert.That(capturedClip.ContentHash).Length().IsEqualTo(64); // SHA-256 hex length
        await Assert.That(capturedClip.Checksum).IsNotEqualTo(0);
    }

    [Test]
    [Category("AppendClipsAsync")]
    public async Task AppendClipsAsync_SetsCorrectMetadata()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var collectionId = Guid.NewGuid();
        var clips = new[]
        {
            CreateTestClip("First", collectionId: collectionId),
            CreateTestClip("Second", collectionId: collectionId),
        };

        Clip? capturedClip = null;
        mockClipRepo.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => capturedClip = clip)
            .ReturnsAsync((Clip clip, CancellationToken _) => clip);

        // Act
        var before = DateTimeOffset.Now;
        await service.AppendClipsAsync(clips, "", false);
        var after = DateTimeOffset.Now;

        // Assert
        await Assert.That(capturedClip).IsNotNull();
        await Assert.That(capturedClip!.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(capturedClip.CapturedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(capturedClip.CapturedAt).IsLessThanOrEqualTo(after);
        await Assert.That(capturedClip.Creator).IsEqualTo(Environment.UserName);
    }
}
