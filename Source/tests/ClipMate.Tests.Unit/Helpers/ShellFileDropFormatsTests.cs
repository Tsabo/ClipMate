using System.Buffers.Binary;
using System.IO;
using ClipMate.Platform.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// Tests for building the shell formats that accompany a restored file list.
/// </summary>
public class ShellFileDropFormatsTests
{
    [Test]
    public async Task CreateShellIdListArray_ExistingFiles_WritesDesktopParentAndOneItemIdListPerFile()
    {
        // Arrange
        var folder = Directory.CreateTempSubdirectory("clipmate-cida-");
        try
        {
            var first = Path.Combine(folder.FullName, "a.txt");
            var second = Path.Combine(folder.FullName, "b.txt");
            await File.WriteAllTextAsync(first, "a");
            await File.WriteAllTextAsync(second, "b");

            // Act
            using var stream = ShellFileDropFormats.CreateShellIdListArray([first, second]);

            // Assert
            await Assert.That(stream).IsNotNull();
            var data = stream!.ToArray();
            var count = BinaryPrimitives.ReadUInt32LittleEndian(data);
            var parentOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
            var firstOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
            var secondOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));

            await Assert.That(count).IsEqualTo(2u);
            await Assert.That(parentOffset).IsEqualTo(16);
            await Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(parentOffset))).IsEqualTo((ushort)0); // desktop: empty ID list
            await Assert.That(firstOffset).IsEqualTo(parentOffset + 2);
            await Assert.That(secondOffset).IsGreaterThan(firstOffset);
            await Assert.That(secondOffset).IsLessThan(data.Length);
            await Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(data.Length - 2))).IsEqualTo((ushort)0); // last ID list terminator
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Test]
    public async Task CreateShellIdListArray_FileNoLongerExists_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"clipmate-missing-{Guid.NewGuid():N}.txt");

        var stream = ShellFileDropFormats.CreateShellIdListArray([missing]);

        await Assert.That(stream).IsNull();
    }

    [Test]
    public async Task PreferredDropEffect_RoundTripsThroughTheClipboardEncoding()
    {
        using var stream = ShellFileDropFormats.CreatePreferredDropEffect(2);

        await Assert.That(stream.ToArray()).IsEquivalentTo(new byte[] { 2, 0, 0, 0 });
        await Assert.That(ShellFileDropFormats.ReadPreferredDropEffect(stream)).IsEqualTo(2);
    }

    [Test]
    public async Task ReadPreferredDropEffect_MissingOrShortData_ReturnsNull()
    {
        await Assert.That(ShellFileDropFormats.ReadPreferredDropEffect(null)).IsNull();
        await Assert.That(ShellFileDropFormats.ReadPreferredDropEffect(new MemoryStream([1, 0]))).IsNull();
    }
}
