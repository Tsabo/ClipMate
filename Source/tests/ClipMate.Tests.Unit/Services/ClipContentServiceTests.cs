using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class ClipContentServiceTests
{
    private static readonly RemoteDatabaseAddress _remote =
        new(DeviceId.FromCertificateBytes([4, 2]), Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private IBlobRepository _blobRepository = null!;
    private IClipDataRepository _clipDataRepository = null!;
    private IClipRepository _clipRepository = null!;
    private IDatabaseContextFactory _factory = null!;
    private IRemoteDatabaseService _remoteDatabases = null!;
    private ClipContentService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _factory = A.Fake<IDatabaseContextFactory>();
        _clipRepository = A.Fake<IClipRepository>();
        _clipDataRepository = A.Fake<IClipDataRepository>();
        _blobRepository = A.Fake<IBlobRepository>();
        A.CallTo(() => _factory.GetClipRepository("db_local01")).Returns(_clipRepository);
        A.CallTo(() => _factory.GetClipDataRepository("db_local01")).Returns(_clipDataRepository);
        A.CallTo(() => _factory.GetBlobRepository("db_local01")).Returns(_blobRepository);
        _remoteDatabases = A.Fake<IRemoteDatabaseService>();
        _service = new ClipContentService(_factory, _remoteDatabases);
    }

    [Test]
    public async Task GetClipContentAsync_LocalDatabase_ReadsTheClipFormatsAndContentFromItsFile()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), Title = "Hello" };
        var format = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id };
        A.CallTo(() => _clipRepository.GetByIdAsync(clip.Id, A<CancellationToken>._)).Returns(clip);
        A.CallTo(() => _clipDataRepository.GetByClipIdAsync(clip.Id, A<CancellationToken>._)).Returns([format]);
        A.CallTo(() => _blobRepository.GetTextByClipIdAsync(clip.Id, A<CancellationToken>._))
            .Returns([new BlobTxt { ClipId = clip.Id, ClipDataId = format.Id, Data = "Hello" }]);

        // Act
        var content = await _service.GetClipContentAsync("db_local01", clip.Id);

        // Assert
        await Assert.That(content).IsNotNull();
        await Assert.That(content!.Clip.Title).IsEqualTo("Hello");
        await Assert.That(content.Formats.Single().Id).IsEqualTo(format.Id);
        await Assert.That(content.TextBlobs.Single().Data).IsEqualTo("Hello");
        A.CallTo(_remoteDatabases).MustNotHaveHappened();
    }

    [Test]
    public async Task GetClipContentAsync_LocalClipMissing_ReturnsNull()
    {
        // Arrange
        A.CallTo(() => _clipRepository.GetByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns((Clip?)null);

        // Act
        var content = await _service.GetClipContentAsync("db_local01", Guid.NewGuid());

        // Assert
        await Assert.That(content).IsNull();
    }

    [Test]
    public async Task GetClipContentAsync_RemoteDatabase_AsksThePeerAndNeverTouchesALocalFile()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var remoteContent = new ClipContent(new Clip { Id = clipId }, [], [], [], [], []);
        A.CallTo(() => _remoteDatabases.GetClipContentAsync(_remote, clipId, A<CancellationToken>._)).Returns(remoteContent);

        // Act
        var content = await _service.GetClipContentAsync(RemoteDatabaseKey.From(_remote), clipId);

        // Assert
        await Assert.That(content).IsSameReferenceAs(remoteContent);
        A.CallTo(_factory).MustNotHaveHappened();
    }
}
