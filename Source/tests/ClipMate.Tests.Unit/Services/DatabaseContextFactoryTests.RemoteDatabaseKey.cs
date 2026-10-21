using ClipMate.Core.Models.Networking;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Remote Databases have no local file and are read-only, so local data access refuses their routing keys
/// instead of treating them as a file path (which would create a stray database file).
/// </summary>
public partial class DatabaseContextFactoryTests
{
    private static readonly string _remoteKey =
        RemoteDatabaseKey.From(new RemoteDatabaseAddress(DeviceId.FromCertificateBytes([7]), Guid.NewGuid()));

    [Test]
    public async Task GetClipRepository_RemoteDatabaseKey_IsRefused()
    {
        // Arrange
        using var factory = CreateFactory();

        // Act / Assert
        await Assert.That(() => factory.GetClipRepository(_remoteKey)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetBlobRepository_RemoteDatabaseKey_IsRefused()
    {
        // Arrange
        using var factory = CreateFactory();

        // Act / Assert
        await Assert.That(() => factory.GetBlobRepository(_remoteKey)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CreateContext_RemoteDatabaseKey_IsRefused()
    {
        // Arrange
        using var factory = CreateFactory();

        // Act / Assert
        await Assert.That(() => factory.CreateContext(_remoteKey)).Throws<InvalidOperationException>();
    }
}
