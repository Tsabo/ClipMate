using System.Security.Cryptography;
using System.Text;

namespace ClipMate.Core.Models.Networking;

/// <summary>
/// The short code both Devices show during Pairing (e.g. <c>K7QM-2XPA</c>), derived from both Device IDs,
/// so the user can confirm they are pairing with the right Device.
/// </summary>
public static class VerificationCode
{
    private const string _alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string _context = "ClipMate Pairing Verification Code v1";

    /// <summary>
    /// Derives the Verification Code for a Pairing between two Devices. The order of the arguments does not matter,
    /// so both Devices compute the same code.
    /// </summary>
    public static string Derive(DeviceId first, DeviceId second)
    {
        var (low, high) = string.CompareOrdinal(first.Value, second.Value) <= 0 ? (first, second) : (second, first);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes($"{_context}\n{low.Value}\n{high.Value}"));

        // The first 40 bits as eight base32 characters.
        var bits = 0UL;
        for (var i = 0; i < 5; i++)
            bits = (bits << 8) | hash[i];

        var builder = new StringBuilder(9);
        for (var i = 7; i >= 0; i--)
        {
            builder.Append(_alphabet[(int)((bits >> (i * 5)) & 31)]);
            if (i == 4)
                builder.Append('-');
        }

        return builder.ToString();
    }
}
