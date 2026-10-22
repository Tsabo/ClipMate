using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Encrypts a clip's stored content in place: the title (unless custom) and every BLOB, all with one salt and IV.
/// Shared by Encrypt Clip and database import.
/// </summary>
internal static class ClipContentEncryption
{
    /// <summary>
    /// PBKDF2 iteration count used for clip encryption.
    /// </summary>
    public const int Iterations = 600_000;

    /// <summary>
    /// Encrypts the title and BLOB entities and sets the clip's encryption fields. Text (title, TEXT BLOBs) is
    /// stored as Base64 of the encrypted UTF-8; binary BLOBs as the encrypted bytes. The caller persists the
    /// entities and clears the clip's transient content.
    /// </summary>
    public static async Task EncryptAsync(IEncryptionService encryptionService,
        EncryptionKey encryptionKey,
        Clip clip,
        IEnumerable<BlobTxt> textBlobs,
        IEnumerable<BlobJpg> jpgBlobs,
        IEnumerable<BlobPng> pngBlobs,
        IEnumerable<BlobBlob> binaryBlobs)
    {
        // The first encryption generates the salt and IV; everything else reuses them
        EncryptionMetadata? sharedMetadata = null;

        if (!clip.CustomTitle && !string.IsNullOrWhiteSpace(clip.Title))
        {
            var result = await encryptionService.EncryptAsync(Encoding.UTF8.GetBytes(clip.Title), encryptionKey, Iterations);
            clip.Title = Convert.ToBase64String(result.EncryptedData);
            UseMetadata(result.Metadata);
        }

        foreach (var item in textBlobs)
            item.Data = Convert.ToBase64String(await EncryptAsync(Encoding.UTF8.GetBytes(item.Data)));

        foreach (var item in jpgBlobs)
            item.Data = await EncryptAsync(item.Data);

        foreach (var item in pngBlobs)
            item.Data = await EncryptAsync(item.Data);

        foreach (var item in binaryBlobs)
            item.Data = await EncryptAsync(item.Data);

        clip.Encrypted = true;
        clip.IsDecrypted = false;
        return;

        async Task<byte[]> EncryptAsync(byte[] data)
        {
            var result = await encryptionService.EncryptAsync(data, encryptionKey, Iterations, sharedMetadata);
            if (sharedMetadata is null)
                UseMetadata(result.Metadata);

            return result.EncryptedData;
        }

        void UseMetadata(EncryptionMetadata metadata)
        {
            sharedMetadata = metadata;
            clip.EncryptionSalt = metadata.Salt;
            clip.EncryptionIv = metadata.IV;
            clip.EncryptionMethod = metadata.Algorithm;
        }
    }
}
