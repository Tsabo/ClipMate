using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Platform;

public class DeviceIdentityServiceTests
{
    private string _directory = null!;

    [Before(Test)]
    public void Setup() => _directory = Path.Combine(Path.GetTempPath(), $"clipmate_identity_{Guid.NewGuid():N}");

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public async Task GetOrCreateAsync_FirstCall_CreatesIdentityWithPrivateKey()
    {
        // Arrange
        var service = CreateService();

        // Act
        using var identity = await service.GetOrCreateAsync();

        // Assert
        await Assert.That(identity.Certificate.HasPrivateKey).IsTrue();
        await Assert.That(identity.DeviceId).IsEqualTo(DeviceId.FromCertificate(identity.Certificate));
        await Assert.That(identity.Certificate.NotAfter).IsGreaterThan(DateTime.Now.AddYears(10));
    }

    [Test]
    public async Task GetOrCreateAsync_AfterRestart_ReusesStoredIdentity()
    {
        // Arrange
        DeviceId firstId;
        using (var first = await CreateService().GetOrCreateAsync())
            firstId = first.DeviceId;

        // Act
        using var reloaded = await CreateService().GetOrCreateAsync();

        // Assert
        await Assert.That(reloaded.DeviceId).IsEqualTo(firstId);
        await Assert.That(reloaded.Certificate.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task TryLoadAsync_NoIdentityYet_ReturnsNullAndCreatesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        var identity = await service.TryLoadAsync();

        // Assert
        await Assert.That(identity).IsNull();
        await Assert.That(Directory.Exists(_directory) && Directory.EnumerateFiles(_directory).Any()).IsFalse();
    }

    [Test]
    public async Task TryLoadAsync_AfterCreate_ReturnsSameDeviceId()
    {
        // Arrange
        using var created = await CreateService().GetOrCreateAsync();

        // Act
        using var loaded = await CreateService().TryLoadAsync();

        // Assert
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.DeviceId).IsEqualTo(created.DeviceId);
    }

    [Test]
    public async Task GetOrCreateAsync_StoresKeyDpapiProtectedForCurrentUser()
    {
        // Arrange
        using var identity = await CreateService().GetOrCreateAsync();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, DeviceIdentityService.FileName));

        // Act / Assert: the file itself is not a readable PKCS#12 blob...
        await Assert.That(() => X509CertificateLoader.LoadPkcs12(bytes, null)).Throws<CryptographicException>();

        // ...but unprotecting it as the current user yields the certificate with its private key.
        var unprotected = ProtectedData.Unprotect(bytes, DeviceIdentityService.Entropy, DataProtectionScope.CurrentUser);
        using var certificate = X509CertificateLoader.LoadPkcs12(unprotected, null);
        await Assert.That(certificate.HasPrivateKey).IsTrue();
        await Assert.That(DeviceId.FromCertificate(certificate)).IsEqualTo(identity.DeviceId);
    }

    private DeviceIdentityService CreateService() => new(_directory, NullLogger<DeviceIdentityService>.Instance);
}
