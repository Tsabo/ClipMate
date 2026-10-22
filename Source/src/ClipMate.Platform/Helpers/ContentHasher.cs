using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.Models;

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
    /// The content hash of a clip, used for duplicate detection and for suppressing the re-capture of a clip ClipMate
    /// put on the clipboard itself. Covers every Format the clip holds, so two clips differ if any Format differs.
    /// A clip holding a single Format hashes exactly as that content alone, matching hashes stored before
    /// multi-Format clips existed.
    /// </summary>
    /// <param name="clip">The clip to hash, with its transient content loaded.</param>
    /// <returns>SHA256 hash as a hex string (64 characters), or empty for a clip with no content.</returns>
    public static string HashClip(Clip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var formats = new List<(string Name, string Hash)>();
        if (!string.IsNullOrEmpty(clip.TextContent))
            formats.Add(("TEXT", HashText(clip.TextContent)));

        if (!string.IsNullOrEmpty(clip.RtfContent))
            formats.Add(("RTF", HashText(clip.RtfContent)));

        if (!string.IsNullOrEmpty(clip.HtmlContent))
            formats.Add(("HTML", HashText(clip.HtmlContent)));

        if (clip.ImageData is { Length: > 0 })
            formats.Add(("IMAGE", HashBytes(clip.ImageData)));

        if (!string.IsNullOrEmpty(clip.FilePathsJson))
            formats.Add(("FILES", HashText(clip.FilePathsJson + (clip.FileDropEffect is { } effect ? $"|{effect}" : ""))));

        return formats.Count switch
        {
            0 => string.Empty,
            1 => formats[0].Hash,
            var _ => HashText(string.Join("\n", formats.Select(p => $"{p.Name}:{p.Hash}"))),
        };
    }
}
