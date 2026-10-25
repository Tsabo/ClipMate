using System.Runtime.CompilerServices;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Temporary diagnostics are tagged <c>[DEBUG-&lt;topic&gt;]</c> while a bug is chased, and removed before merging.
/// </summary>
public class TemporaryDiagnosticsTests
{
    private const string _marker = "[DEBUG-";

    [Test]
    public async Task Sources_HaveNoTemporaryDiagnostics()
    {
        // Arrange: Source/tests/ClipMate.Tests.Unit/<this file>
        var sourceDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", ".."));
        var separator = Path.DirectorySeparatorChar;

        // Act
        var tagged = Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}") && p != SourcePath())
            .Where(p => File.ReadAllText(p).Contains(_marker, StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(sourceDirectory, p))
            .ToList();

        // Assert
        await Assert.That(tagged).IsEmpty();
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
