using System.Security.Cryptography;
using System.Text;

namespace ClipMate.Platform.Helpers;

/// <summary>
/// Helper class for generating content hashes for duplicate detection.
/// </summary>
public static class ContentHasher
{
    /// <summary>
    /// Generates a SHA256 hash of the given text content.
    /// </summary>
    /// <param name="content">The text content to hash.</param>
    /// <returns>SHA256 hash as a hex string (64 characters).</returns>
    public static string HashText(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Generates a SHA256 hash of the given binary data.
    /// </summary>
    /// <param name="data">The binary data to hash.</param>
    /// <returns>SHA256 hash as a hex string (64 characters).</returns>
    public static string HashBytes(byte[]? data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Generates a SHA256 hash of multiple file paths combined.
    /// </summary>
    /// <param name="filePaths">The file paths to hash.</param>
    /// <returns>SHA256 hash as a hex string (64 characters).</returns>
    public static string HashFilePaths(IEnumerable<string>? filePaths)
    {
        if (filePaths == null || !filePaths.Any())
            return string.Empty;

        var combined = string.Join("|", filePaths.OrderBy(p => p));
        return HashText(combined);
    }

    /// <summary>
    /// Calculates the content hash for a Clip based on its type and content.
    /// This provides a standardized way to hash clips for duplicate detection and suppression.
    /// </summary>
    /// <param name="clip">The clip to hash.</param>
    /// <returns>SHA256 hash as a hex string (64 characters).</returns>
    public static string HashClip(Core.Models.Clip clip)
    {
        return clip.Type switch
        {
            Core.Models.ClipType.Text or Core.Models.ClipType.Html => HashText(clip.TextContent ?? string.Empty),
            Core.Models.ClipType.Image => HashBytes(clip.ImageData ?? Array.Empty<byte>()),
            Core.Models.ClipType.Files => HashText(clip.FilePathsJson ?? string.Empty),
            _ => string.Empty
        };
    }
}
