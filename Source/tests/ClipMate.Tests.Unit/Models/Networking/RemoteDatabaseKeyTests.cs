using ClipMate.Core.Models.Networking;

namespace ClipMate.Tests.Unit.Models.Networking;

public class RemoteDatabaseKeyTests
{
    private static readonly RemoteDatabaseAddress _address =
        new(DeviceId.FromCertificateBytes([1, 2, 3]), Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Test]
    public async Task TryParse_KeyMadeFromAnAddress_GivesTheSameAddressBack()
    {
        // Arrange
        var key = RemoteDatabaseKey.From(_address);

        // Act
        var parsed = RemoteDatabaseKey.TryParse(key, out var address);

        // Assert
        await Assert.That(parsed).IsTrue();
        await Assert.That(address).IsEqualTo(_address);
    }

    [Test]
    public async Task From_Address_IsNeverMistakenForAFilePathOrADatabaseConfigurationKey()
    {
        // Act
        var key = RemoteDatabaseKey.From(_address);

        // Assert
        await Assert.That(key).DoesNotContain("\\");
        await Assert.That(key).DoesNotContain("/");
        await Assert.That(key.StartsWith("db_", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("db_12345678")]
    [Arguments("C:\\Users\\me\\clipmate.db")]
    [Arguments("remote:not-a-device:11111111111111111111111111111111")]
    [Arguments("remote:")]
    public async Task IsRemote_LocalOrMalformedKey_IsFalse(string? key)
    {
        // Act / Assert
        await Assert.That(RemoteDatabaseKey.IsRemote(key)).IsFalse();
    }

    [Test]
    public async Task IsRemote_KeyMadeFromAnAddress_IsTrue()
    {
        // Act / Assert
        await Assert.That(RemoteDatabaseKey.IsRemote(RemoteDatabaseKey.From(_address))).IsTrue();
    }
}
