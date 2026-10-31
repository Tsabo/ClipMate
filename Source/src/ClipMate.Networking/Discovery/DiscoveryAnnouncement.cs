using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClipMate.Core.Models.Networking;
using ClipMate.Networking.Discovery.Protocol;
using Google.Protobuf;

namespace ClipMate.Networking.Discovery;

/// <summary>
/// A verified discovery announcement: what a Device says about itself on the LAN.
/// </summary>
/// <param name="DeviceId">The sender's Device ID, proven by its certificate and signature.</param>
/// <param name="DeviceName">The sender's Device Name.</param>
/// <param name="Port">TCP port of the sender's Peer listener.</param>
/// <param name="ProtocolMajor">The sender's Peer protocol major version.</param>
/// <param name="ProtocolMinor">The sender's Peer protocol minor version.</param>
public sealed record DiscoveryAnnouncement(DeviceId DeviceId, string DeviceName, int Port, uint ProtocolMajor, uint ProtocolMinor)
{
    /// <summary>
    /// Version of the datagram envelope (see <c>Protos/discovery.proto</c>).
    /// </summary>
    public const byte FormatVersion = 1;

    /// <summary>
    /// Largest datagram accepted or produced.
    /// </summary>
    public const int MaxDatagramSize = 4096;

    /// <summary>
    /// Longest Device Name kept from an announcement; longer names are cut.
    /// </summary>
    public const int MaxDeviceNameLength = 100;

    private const int _headerLength = 5;
    private static readonly byte[] _magic = [.. "CMDA"u8];
    private static readonly byte[] _signingContext = [.. "ClipMate discovery announcement v1\0"u8];

    /// <summary>
    /// Builds a signed announcement datagram for <paramref name="identity" />.
    /// </summary>
    public static byte[] Encode(DeviceIdentity identity, string deviceName, int port) => Encode(identity, deviceName, port, null, null);

    /// <summary>
    /// Builds an announcement datagram signed with <paramref name="signingKey" />, optionally claiming a different Device ID.
    /// Only tests use the overrides, to produce forged announcements.
    /// </summary>
    internal static byte[] Encode(DeviceIdentity identity, string deviceName, int port, ECDsa? signingKey = null, DeviceId? claimedDeviceId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        var announcement = new Announcement
        {
            Certificate = ByteString.CopyFrom(identity.Certificate.RawData),
            DeviceId = (claimedDeviceId ?? identity.DeviceId).Value,
            DeviceName = Truncate(deviceName),
            Port = (uint)port,
            ProtocolMajor = PeerProtocol.Major,
            ProtocolMinor = PeerProtocol.Minor,
        }.ToByteArray();

        byte[] signature;
        if (signingKey != null)
            signature = Sign(signingKey, announcement);
        else
        {
            using var key = identity.Certificate.GetECDsaPrivateKey()
                            ?? throw new InvalidOperationException("The Device certificate has no ECDSA private key.");

            signature = Sign(key, announcement);
        }

        var envelope = new SignedAnnouncement
        {
            Announcement = ByteString.CopyFrom(announcement),
            Signature = ByteString.CopyFrom(signature),
        };

        var datagram = new byte[_headerLength + envelope.CalculateSize()];
        _magic.CopyTo(datagram, 0);
        datagram[4] = FormatVersion;
        envelope.WriteTo(datagram.AsSpan(_headerLength));
        if (datagram.Length > MaxDatagramSize)
            throw new InvalidOperationException("The discovery announcement is too large.");

        return datagram;
    }

    /// <summary>
    /// Parses and verifies a received datagram. Returns <c>false</c> for anything that is not a well-formed announcement
    /// signed by the key of the certificate it carries, or whose claimed Device ID does not match that certificate.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out DiscoveryAnnouncement? announcement)
    {
        announcement = null;
        if (datagram.Length <= _headerLength || datagram.Length > MaxDatagramSize
                                              || !datagram[..4].SequenceEqual(_magic) || datagram[4] != FormatVersion)
            return false;

        try
        {
            var envelope = SignedAnnouncement.Parser.ParseFrom(datagram[_headerLength..]);
            var payload = envelope.Announcement.Span;
            var content = Announcement.Parser.ParseFrom(payload);

            if (!DeviceId.TryParse(content.DeviceId, out var claimed)
                || claimed.Value != DeviceId.FromCertificateBytes(content.Certificate.Span)
                || content.Port is 0 or > IPEndPoint.MaxPort)
                return false;

            using var certificate = X509CertificateLoader.LoadCertificate(content.Certificate.Span);
            using var key = certificate.GetECDsaPublicKey();
            if (key == null || !key.VerifyData(SigningInput(payload), envelope.Signature.Span, HashAlgorithmName.SHA256))
                return false;

            announcement = new DiscoveryAnnouncement(claimed.Value, Truncate(content.DeviceName), (int)content.Port,
                content.ProtocolMajor, content.ProtocolMinor);
            return true;
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or CryptographicException)
        {
            return false;
        }
    }

    private static byte[] Sign(ECDsa key, byte[] announcement) => key.SignData(SigningInput(announcement), HashAlgorithmName.SHA256);

    private static byte[] SigningInput(ReadOnlySpan<byte> announcement) => [.. _signingContext, .. announcement];

    private static string Truncate(string deviceName)
    {
        var trimmed = deviceName.Trim();
        return trimmed.Length <= MaxDeviceNameLength ? trimmed : trimmed[..MaxDeviceNameLength];
    }
}
