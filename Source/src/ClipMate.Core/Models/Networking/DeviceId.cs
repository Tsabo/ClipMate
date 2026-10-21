using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// The fingerprint that uniquely identifies a Device: the SHA-256 hash of its certificate,
/// written in base32 (RFC 4648, no padding).
/// </summary>
public readonly record struct DeviceId
{
    private const string _alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int _length = 52; // ceil(256 bits / 5)
    private const int _groupSize = 4;

    private DeviceId(string value) => Value = value;

    /// <summary>
    /// The canonical form: 52 upper-case base32 characters without separators.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Derives the Device ID of a certificate.
    /// </summary>
    public static DeviceId FromCertificate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return FromCertificateBytes(certificate.RawData);
    }

    /// <summary>
    /// Derives the Device ID from the DER bytes of a certificate.
    /// </summary>
    public static DeviceId FromCertificateBytes(ReadOnlySpan<byte> certificateBytes) =>
        new(ToBase32(SHA256.HashData(certificateBytes)));

    /// <summary>
    /// Parses a Device ID in canonical or grouped form. Case, dashes, spaces and surrounding whitespace are ignored.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out DeviceId? deviceId)
    {
        deviceId = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var builder = new StringBuilder(_length);
        foreach (var item in text)
        {
            if (item is '-' or ' ' || char.IsWhiteSpace(item))
                continue;

            var upper = char.ToUpperInvariant(item);
            if (!_alphabet.Contains(upper, StringComparison.Ordinal))
                return false;

            builder.Append(upper);
        }

        if (builder.Length != _length)
            return false;

        deviceId = new DeviceId(builder.ToString());
        return true;
    }

    /// <summary>
    /// The human-readable form: groups of four characters separated by dashes.
    /// </summary>
    public string ToDisplayString() => string.Join('-', Value.Chunk(_groupSize).Select(p => new string(p)));

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsInBuffer = 0;
        foreach (var item in bytes)
        {
            buffer = ((buffer << 8) | item) & 0x1FFF; // at most 12 pending bits are ever needed
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                builder.Append(_alphabet[(buffer >> bitsInBuffer) & 31]);
            }
        }

        if (bitsInBuffer > 0)
            builder.Append(_alphabet[(buffer << (5 - bitsInBuffer)) & 31]);

        return builder.ToString();
    }
}
