using System.Runtime.CompilerServices;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Every clip goes on the clipboard through the Current Clip writer, so the template, the Outbound Filter and the
/// Current Clip apply to every path (ADR-0004).
/// </summary>
public class ClipboardWritePathTests
{
    // Calls that put something on the clipboard, and the only files allowed to make each
    private static readonly (string Call, string[] AllowedFiles)[] _writes =
    [
        // The port: only the writer calls it
        (".SetClipboardContentAsync(", ["CurrentClipWriter.cs"]),

        // Raw writes: the port's implementation, Power Paste's delayed rendering (offered through the writer's prepare
        // step), the Paste Trace diagnostic, and plain text that isn't a clip (selected text, diagnostics)
        ("SetClipboardData(", ["PowerPasteClipboardBridge.cs", "PasteTraceService.cs", "OwnClipboardCopy.cs"]),
        ("SetClipboardDataObject(", ["ClipboardService.cs", "IWin32ClipboardInterop.cs", "Win32ClipboardInterop.cs"]),
        ("Clipboard.SetDataObject(", ["Win32ClipboardInterop.cs", "OwnClipboardCopy.cs"]),
    ];

    [Test]
    public async Task ClipboardWrites_GoThroughTheCurrentClipWriter()
    {
        // Arrange: Source/tests/ClipMate.Tests.Unit/<this file>, so Source/src is two folders up
        var sourceDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "src"));
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"))

            // A third-party control that copies its own bytes
            .Where(p => !p.Contains($"{separator}ClipMate.WpfHexEditor{separator}"))
            .Select(p => (Path: p, Text: File.ReadAllText(p)))
            .ToList();

        // Act
        var strays = (from write in _writes
                from file in files
                where file.Text.Contains(write.Call, StringComparison.Ordinal) &&
                      !write.AllowedFiles.Contains(Path.GetFileName(file.Path))
                select $"{Path.GetRelativePath(sourceDirectory, file.Path)}: {write.Call}")
            .ToList();

        // Assert
        await Assert.That(strays).IsEmpty();
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
