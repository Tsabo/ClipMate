using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClipMate.Tests.Unit;

/// <summary>
/// A <c>ThemedWindowDialogButton</c> closes its window as soon as its Click handler returns, which for an async handler
/// is at its first <c>await</c>. The search dialog's Go! closed the dialog 85 ms into a Remote Search, and closing it
/// canceled the search, so searching a Peer did nothing. A button with an async handler sets
/// <c>AllowCloseWindow="False"</c> and closes the window itself when the work is done.
/// </summary>
public partial class DialogButtonTests
{
    [Test]
    public async Task DialogButtons_WithAsyncHandlers_DoNotCloseTheWindow()
    {
        // Arrange
        var sourceDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "src"));
        var separator = Path.DirectorySeparatorChar;
        var xamlFiles = Directory.EnumerateFiles(sourceDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"));

        // Act
        var closing = xamlFiles.SelectMany(p => ClosingAsyncButtons(sourceDirectory, p)).Order().ToList();

        // Assert (joined, so a failure shows every button)
        await Assert.That(string.Join(Environment.NewLine, closing)).IsEmpty();
    }

    // "<file>: <handler>" for each dialog button whose async Click handler leaves the window free to close
    private static IEnumerable<string> ClosingAsyncButtons(string sourceDirectory, string path)
    {
        var codeBehind = path + ".cs";
        if (!File.Exists(codeBehind))
            yield break;

        var code = File.ReadAllText(codeBehind);
        var file = Path.GetRelativePath(sourceDirectory, path).Replace('\\', '/');

        foreach (var button in XDocument.Load(path).Descendants().Where(p => p.Name.LocalName == "ThemedWindowDialogButton"))
        {
            var handler = (string?)button.Attribute("Click");
            if (handler == null || (string?)button.Attribute("AllowCloseWindow") == "False")
                continue;

            if (AsyncVoidMethod().Matches(code).Any(p => p.Groups["method"].Value == handler))
                yield return $"{file}: {handler}";
        }
    }

    [GeneratedRegex(@"async\s+void\s+(?<method>\w+)\s*\(")]
    private static partial Regex AsyncVoidMethod();

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
