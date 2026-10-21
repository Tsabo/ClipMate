using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Stores the Device identity as a PKCS#12 blob encrypted with DPAPI (current user) in the ClipMate data folder.
/// </summary>
public sealed class DeviceIdentityService : IDeviceIdentityService
{
    /// <summary>
    /// Name of the protected identity file inside the data folder.
    /// </summary>
    internal const string FileName = "device-identity.dat";

    private static readonly TimeSpan _certificateLifetime = TimeSpan.FromDays(365 * 50);

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private readonly ILogger<DeviceIdentityService> _logger;

    /// <summary>
    /// Creates the service for the given ClipMate data folder.
    /// </summary>
    public DeviceIdentityService(string dataDirectory, ILogger<DeviceIdentityService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _filePath = Path.Combine(dataDirectory, FileName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Optional DPAPI entropy that ties the blob to ClipMate.
    /// </summary>
    internal static byte[] Entropy => "ClipMate.DeviceIdentity.v1"u8.ToArray();

    /// <inheritdoc />
    public Task<DeviceIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            lock (_lock)
                return Load() ?? Create();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<DeviceIdentity?> TryLoadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            lock (_lock)
                return Load();
        }, cancellationToken);

    private DeviceIdentity? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        var pkcs12 = ProtectedData.Unprotect(File.ReadAllBytes(_filePath), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            return new DeviceIdentity(X509CertificateLoader.LoadPkcs12(pkcs12, null, X509KeyStorageFlags.UserKeySet));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs12);
        }
    }

    private DeviceIdentity Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ClipMate Device", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2")], false)); // server + client authentication

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        using var certificate = request.CreateSelfSigned(notBefore, notBefore + _certificateLifetime);

        var pkcs12 = certificate.Export(X509ContentType.Pkcs12);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllBytes(temporaryPath, ProtectedData.Protect(pkcs12, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temporaryPath, _filePath, true);

            // Reload from the PKCS#12 blob so the private key is usable by SChannel (TLS) on Windows.
            var identity = new DeviceIdentity(X509CertificateLoader.LoadPkcs12(pkcs12, null, X509KeyStorageFlags.UserKeySet));
            _logger.LogInformation("Generated new Device identity {DeviceId}", identity.DeviceId.ToDisplayString());
            return identity;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs12);
        }
    }
}
