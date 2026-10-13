using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class ClipAppendServiceTests
{
    /// <summary>
    /// Sets up ClipData/BlobTxt mocks simulating an existing text blob for the given clip,
    /// mirroring how text content actually lives in the database (not on the Clip row itself).
    /// </summary>
    private (IClipDataRepository ClipDataRepo, IBlobRepository BlobRepo, BlobTxt Blob) SetupExistingTextBlob(Guid clipId, string existingText)
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

        var mockClipDataRepo = A.Fake<IClipDataRepository>();
        A.CallTo(() => mockClipDataRepo.GetByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData> { clipData });

        var mockBlobRepo = A.Fake<IBlobRepository>();
        A.CallTo(() => mockBlobRepo.GetTextByClipIdAsync(clipId, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt> { blob });

        A.CallTo(() => mockBlobRepo.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => _mockContextFactory.GetClipDataRepository(_testDatabaseKey)).Returns(mockClipDataRepo);
        A.CallTo(() => _mockContextFactory.GetBlobRepository(_testDatabaseKey)).Returns(mockBlobRepo);

        return (mockClipDataRepo, mockBlobRepo, blob);
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithExistingClip_AppendsTextWithSeparator()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = A.Fake<IClipRepository>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo);

        var targetClip = CreateTestClip("First capture");
        A.CallTo(() => mockClipRepo.GetByIdAsync(targetClip.Id, A<CancellationToken>.Ignored)).Returns(targetClip);

        var (_, blobRepo, blob) = SetupExistingTextBlob(targetClip.Id, "First capture");

        Clip? updatedClip = null;
        A.CallTo(() => mockClipRepo.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((Clip clip, CancellationToken _) => updatedClip = clip)
            .Returns(true);

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "Second capture", "\\n", false);

        // Assert
        await Assert.That(result.TextContent).IsEqualTo("First capture\nSecond capture");
        await Assert.That(blob.Data).IsEqualTo("First capture\nSecond capture");
        await Assert.That(updatedClip).IsNotNull();
        await Assert.That(updatedClip!.TextContent).IsEqualTo("First capture\nSecond capture");
        A.CallTo(() => blobRepo.UpdateTextAsync(A<BlobTxt>.That.Matches(b => b.Id == blob.Id), A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => mockClipRepo.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockSoundService.PlaySoundAsync(SoundEvent.Append, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithStripTrailingLineBreaks_RemovesTrailingBreaksFromExistingContent()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = A.Fake<IClipRepository>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo);

        var targetClip = CreateTestClip("First\r\n");
        A.CallTo(() => mockClipRepo.GetByIdAsync(targetClip.Id, A<CancellationToken>.Ignored)).Returns(targetClip);
        A.CallTo(() => mockClipRepo.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).Returns(true);

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
        var mockClipRepo = A.Fake<IClipRepository>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo);

        var targetClip = CreateTestClip("First");
        var originalHash = targetClip.ContentHash;
        A.CallTo(() => mockClipRepo.GetByIdAsync(targetClip.Id, A<CancellationToken>.Ignored)).Returns(targetClip);
        A.CallTo(() => mockClipRepo.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).Returns(true);

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
        var mockClipRepo = A.Fake<IClipRepository>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo);

        var targetClip = CreateTestClip(string.Empty);
        A.CallTo(() => mockClipRepo.GetByIdAsync(targetClip.Id, A<CancellationToken>.Ignored)).Returns(targetClip);
        A.CallTo(() => mockClipRepo.UpdateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).Returns(true);

        var mockClipDataRepo = A.Fake<IClipDataRepository>();
        A.CallTo(() => mockClipDataRepo.GetByClipIdAsync(targetClip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<ClipData>());

        A.CallTo(() => mockClipDataRepo.CreateAsync(A<ClipData>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((ClipData cd, CancellationToken _) => cd);

        var mockBlobRepo = A.Fake<IBlobRepository>();
        A.CallTo(() => mockBlobRepo.GetTextByClipIdAsync(targetClip.Id, A<CancellationToken>.Ignored))
            .Returns(new List<BlobTxt>());

        BlobTxt? createdBlob = null;
        A.CallTo(() => mockBlobRepo.CreateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((BlobTxt blob, CancellationToken _) => createdBlob = blob)
            .ReturnsLazily((BlobTxt blob, CancellationToken _) => blob);

        A.CallTo(() => _mockContextFactory.GetClipDataRepository(_testDatabaseKey)).Returns(mockClipDataRepo);
        A.CallTo(() => _mockContextFactory.GetBlobRepository(_testDatabaseKey)).Returns(mockBlobRepo);

        // Act
        var result = await service.AppendCapturedTextAsync(targetClip.Id, "First real capture", "", false);

        // Assert
        await Assert.That(result.TextContent).IsEqualTo("First real capture");
        await Assert.That(createdBlob).IsNotNull();
        await Assert.That(createdBlob!.Data).IsEqualTo("First real capture");
        A.CallTo(() => mockBlobRepo.UpdateTextAsync(A<BlobTxt>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [Category("AppendCapturedTextAsync")]
    public async Task AppendCapturedTextAsync_WithUnknownClipId_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var mockClipRepo = A.Fake<IClipRepository>();
        A.CallTo(() => _mockContextFactory.GetClipRepository(_testDatabaseKey)).Returns(mockClipRepo);

        var missingClipId = Guid.NewGuid();
        A.CallTo(() => mockClipRepo.GetByIdAsync(missingClipId, A<CancellationToken>.Ignored)).Returns((Clip?)null);

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
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AppendCapturedTextAsync(Guid.NewGuid(), "Text", "", false));
    }
}
