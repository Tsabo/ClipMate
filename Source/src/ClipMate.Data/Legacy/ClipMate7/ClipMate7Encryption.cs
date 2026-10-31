using System.Buffers.Binary;
using ClipMate.Data.Legacy.Dbisam;

namespace ClipMate.Data.Legacy.ClipMate7;

/// <summary>
/// Decrypts clips encrypted by ClipMate 7 ("Encrypt clip using ARC4 Encryption").
/// </summary>
/// <remarks>
/// ClipMate 7 encrypts only a clip's TEXT (CF_TEXT) format, plus its title when the title is not custom. The stored
/// TEXT is <c>salt + ":" + Base64(RC4(text))</c>, where the salt is the last 6 characters of the clip's timestamp
/// as text. The RC4 key is 7 bytes ("56-bit"): characters 2 to 8 of <c>passphrase + salt</c>, repeating when that is
/// shorter. When ClipMate 7's AllowChecksum option was on, CLIP.CHECKSUM holds the CRC-32 of the plain text XORed
/// with the next 4 bytes of the RC4 keystream, which lets a wrong key be detected; otherwise it is 0.
/// </remarks>
internal sealed class ClipMate7Encryption
{
    private const int _saltLength = 6;
    private const int _rc4KeyLength = 7;

    private readonly byte[] _passphrase;

    /// <param name="passphrase">The key the user typed in ClipMate 7, used as ANSI text exactly as typed.</param>
    public ClipMate7Encryption(string passphrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        _passphrase = DbisamTable.Ansi.GetBytes(passphrase);
    }

    /// <summary>
    /// Decrypts the stored TEXT of an encrypted clip.
    /// </summary>
    /// <param name="stored">The TEXT format's BLOB data.</param>
    /// <param name="checksum">CLIP.CHECKSUM; 0 when ClipMate 7 did not store one.</param>
    /// <param name="plainText">The decrypted ANSI text bytes.</param>
    /// <param name="salt">The salt, needed to decrypt the clip's title.</param>
    /// <returns>False if the data is not ClipMate 7 ciphertext or the key is wrong.</returns>
    public bool TryDecryptText(byte[] stored, int checksum, out byte[] plainText, out byte[] salt)
    {
        plainText = [];
        salt = [];

        var length = Array.IndexOf(stored, (byte)0) is var nul and >= 0
            ? nul
            : stored.Length;

        if (length <= _saltLength || stored[_saltLength] != ':')
            return false;

        salt = stored[.._saltLength];
        if (!TryFromBase64(stored.AsSpan(_saltLength + 1, length - _saltLength - 1), out var cipher))
            return false;

        var rc4 = new Rc4(CreateKey(salt));
        plainText = rc4.Transform(cipher);

        if (checksum != 0)
        {
            var mask = BinaryPrimitives.ReadUInt32BigEndian(rc4.Transform(new byte[4]));
            var plainLength = Array.IndexOf(plainText, (byte)0) is var end and >= 0
                ? end
                : plainText.Length;

            return (Crc32(plainText.AsSpan(0, plainLength)) ^ mask) == unchecked((uint)checksum);
        }

        // No checksum: a wrong key yields binary noise, which text never contains
        return plainText.All(p => p is >= 0x20 or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0);
    }

    /// <summary>
    /// Decrypts a title that ClipMate 7 encrypted along with the clip (only titles that are not custom are).
    /// </summary>
    /// <returns>The title, or null if it cannot be decrypted.</returns>
    /// <remarks>
    /// The Base64 is truncated to the TITLE field's width when the title is long; as RC4 is a stream cipher, the
    /// complete 4-character groups still decrypt to the start of the title.
    /// </remarks>
    public string? TryDecryptTitle(string title, byte[] salt)
    {
        var base64 = DbisamTable.Ansi.GetBytes(title.TrimEnd());
        if (base64.Length < 4 || !TryFromBase64(base64.AsSpan(0, base64.Length / 4 * 4), out var cipher))
            return null;

        var plain = new Rc4(CreateKey(salt)).Transform(cipher);
        return plain.Any(p => p < 0x20 && p != '\t')
            ? null
            : DbisamTable.Ansi.GetString(plain);
    }

    /// <summary>
    /// The 7-byte RC4 key: characters 2..8 of passphrase + salt, wrapping around when shorter.
    /// </summary>
    private byte[] CreateKey(byte[] salt)
    {
        byte[] fullKey = [.. _passphrase, .. salt];
        var key = new byte[_rc4KeyLength];
        for (var i = 0; i < key.Length; i++)
            key[i] = fullKey[(i + 1) % fullKey.Length];

        return key;
    }

    /// <summary>
    /// Standard CRC-32 (polynomial 0xEDB88320), as used by ClipMate 7.
    /// </summary>
    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0
                    ? crc >> 1 ^ 0xEDB88320
                    : crc >> 1;
        }

        return ~crc;
    }

    private static bool TryFromBase64(ReadOnlySpan<byte> base64, out byte[] data)
    {
        data = new byte[base64.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64Chars(DbisamTable.Ansi.GetString(base64), data, out var written))
            return false;

        data = data[..written];
        return true;
    }

    /// <summary>
    /// RC4 (ARC4) keystream. The state carries on across calls, as ClipMate 7's checksum relies on.
    /// </summary>
    private sealed class Rc4
    {
        private readonly byte[] _s = new byte[256];
        private int _i;
        private int _j;

        public Rc4(byte[] key)
        {
            for (var i = 0; i < 256; i++)
                _s[i] = (byte)i;

            for (int i = 0, j = 0; i < 256; i++)
            {
                j = j + _s[i] + key[i % key.Length] & 0xFF;
                (_s[i], _s[j]) = (_s[j], _s[i]);
            }
        }

        public byte[] Transform(byte[] data)
        {
            var result = new byte[data.Length];
            for (var k = 0; k < data.Length; k++)
            {
                _i = _i + 1 & 0xFF;
                _j = _j + _s[_i] & 0xFF;
                (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
                result[k] = (byte)(data[k] ^ _s[_s[_i] + _s[_j] & 0xFF]);
            }

            return result;
        }
    }
}
