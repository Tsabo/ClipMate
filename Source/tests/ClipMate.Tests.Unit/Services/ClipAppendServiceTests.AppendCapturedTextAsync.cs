using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipAppendServiceTests
{
    /// <summary>
    /// Sets up ClipData/BlobTxt mocks simulating an existing text blob for the given clip,
    /// mirroring how text content actually lives in the database (not on the Clip row itself).
    /// </summary>
    private (Mock<IClipDataRepository> ClipDataRepo, Mock<IBlobRepository> BlobRepo, BlobTxt Blob) SetupExistingTextBlob(Guid clipId, string existingText)
    {
        var clipData = new ClipData
        {
            Id = Guid.NewGuid(),
            ClipId = clipId,
            Format = Formats.UnicodeText.Code,
            FormatName = Formats.UnicodeText.Name,
            StorageType = 1,
        };

        var blob = new BlobTxt { Id = Guid.NewGuid(), ClipDataId = clipData.Id, ClipId = clipId, Data = existingText };

        var mockClipDataRepo = new Mock<IClipDataRepository>();
        mockClipDataRepo.Setup(p => p.GetByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData> { clipData });

        var mockBlobRepo = new Mock<IBlobRepository>();
        mockBlobRepo.Setup(p => p.GetTextByClipIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt> { blob });

        mockBlobRepo.Setup(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockContextFactory.Setup(p => p.GetClipDataRepository(_testDatabaseKey)).Returns(mockClipDataRepo.Object);
        _mockContextFactory.Setup(p => p.GetBlobRepository(_testDatabaseKey)).Returns(mockBlobRepo.Object);

        return (mockClipDataRepo, mockBlobRepo, blob);
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithExistingClip_AppendsTextWithSeparator()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var targetClip = CreateTestClip("First capture");
        mockClipRepo.Setup(p => p.GetByIdAsync(targetClip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(targetClip);

        var (_, blobRepo, blob) = SetupExistingTextBlob(targetClip.Id, "First capture");

        Clip? updatedClip = null;
        mockClipRepo.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .Callback<Clip, CancellationToken>((clip, _) => updatedClip = clip)
            .ReturnsAsync(true);

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "Second capture", "\\n", false);

        // Assert
        await Assert.That(result.TextContent).IsEqualTo("First capture\nSecond capture");
        await Assert.That(blob.Data).IsEqualTo("First capture\nSecond capture");
        await Assert.That(updatedClip).IsNotNull();
        await Assert.That(updatedClip!.TextContent).IsEqualTo("First capture\nSecond capture");
        blobRepo.Verify(p => p.UpdateTextAsync(It.Is<BlobTxt>(b => b.Id == blob.Id), It.IsAny<CancellationToken>()), Times.Once);
        mockClipRepo.Verify(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSoundService.Verify(p => p.PlaySoundAsync(SoundEvent.Append, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithStripTrailingLineBreaks_RemovesTrailingBreaksFromExistingContent()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var targetClip = CreateTestClip("First\r\n");
        mockClipRepo.Setup(p => p.GetByIdAsync(targetClip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(targetClip);
        mockClipRepo.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        SetupExistingTextBlob(targetClip.Id, "First\r\n");

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "Second", "", true);

        // Assert
        await Assert.That(result.TextContent).IsEqualTo("FirstSecond");
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_RecomputesSizeAndHash()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var targetClip = CreateTestClip("First");
        var originalHash = targetClip.ContentHash;
        mockClipRepo.Setup(p => p.GetByIdAsync(targetClip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(targetClip);
        mockClipRepo.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        SetupExistingTextBlob(targetClip.Id, "First");

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "Second", "", false);

        // Assert
        await Assert.That(result.Size).IsEqualTo(Encoding.UTF8.GetByteCount("FirstSecond"));
        await Assert.That(result.ContentHash).IsNotEqualTo(originalHash);
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithNoExistingTextBlob_CreatesNewBlob()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var targetClip = CreateTestClip(string.Empty);
        mockClipRepo.Setup(p => p.GetByIdAsync(targetClip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(targetClip);
        mockClipRepo.Setup(p => p.UpdateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var mockClipDataRepo = new Mock<IClipDataRepository>();
        mockClipDataRepo.Setup(p => p.GetByClipIdAsync(targetClip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClipData>());

        mockClipDataRepo.Setup(p => p.CreateAsync(It.IsAny<ClipData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClipData cd, CancellationToken _) => cd);

        var mockBlobRepo = new Mock<IBlobRepository>();
        mockBlobRepo.Setup(p => p.GetTextByClipIdAsync(targetClip.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobTxt>());

        BlobTxt? createdBlob = null;
        mockBlobRepo.Setup(p => p.CreateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()))
            .Callback<BlobTxt, CancellationToken>((blob, _) => createdBlob = blob)
            .ReturnsAsync((BlobTxt blob, CancellationToken _) => blob);

        _mockContextFactory.Setup(p => p.GetClipDataRepository(_testDatabaseKey)).Returns(mockClipDataRepo.Object);
        _mockContextFactory.Setup(p => p.GetBlobRepository(_testDatabaseKey)).Returns(mockBlobRepo.Object);

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "First real capture", "", false);

        // Assert
        await Assert.That(result.TextContent).IsEqualTo("First real capture");
        await Assert.That(createdBlob).IsNotNull();
        await Assert.That(createdBlob!.Data).IsEqualTo("First real capture");
        mockBlobRepo.Verify(p => p.UpdateTextAsync(It.IsAny<BlobTxt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithUnknownClipId_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = new Mock<IClipRepository>();
        _mockContextFactory.Setup(p => p.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo.Object);

        var missingClipId = Guid.NewGuid();
        mockClipRepo.Setup(p => p.GetByIdAsync(missingClipId, It.IsAny<CancellationToken>())).ReturnsAsync((Clip?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AppendCapturedTextAsync(missingClipId, "Text", "", false));
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AppendCapturedTextAsync(Guid.NewGuid(), "Text", "", false));
    }
}
