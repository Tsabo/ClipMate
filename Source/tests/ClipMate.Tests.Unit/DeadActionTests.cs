using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClipMate.Tests.Unit;

/// <summary>
/// A user action that does nothing (a menu item with no handler, an empty command, a "not yet implemented" stub) reads
/// as a bug: right-click Append, Select Collection (F8) and the Manual Filter hotkey all did nothing until reported.
/// Known stubs are listed in <c>DeadActions.txt</c>, which only shrinks: an entry that is no longer found fails the test.
/// </summary>
public partial class DeadActionTests
{
    // Menu and bar items a user clicks; a BarSubItem only opens its items
    private static readonly HashSet<string> _clickableElements = ["MenuItem", "BarButtonItem", "BarCheckItem", "BarSplitButtonItem"];

    // An item doing something: a handler, a command, a bound check state, items of its own (or a drop-down menu holding them)
    private static readonly string[] _actionAttributes = ["Click", "Command", "ItemClick", "CheckedChanged", "IsChecked", "ItemsSource", "Checked", "Unchecked"];

    [Test]
    public async Task UserActions_AllDoSomething()
    {
        // Arrange
        var sourceDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "src"));
        var known = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(SourcePath())!, "DeadActions.txt"))
            .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var found = SourceFiles(sourceDirectory, "*.xaml").SelectMany(p => DeadMenuItems(sourceDirectory, p))
            .Concat(SourceFiles(sourceDirectory, "*.cs").SelectMany(p => DeadCode(sourceDirectory, p)))
            .ToHashSet(StringComparer.Ordinal);

        // Assert: nothing new, and nothing listed that is gone (joined, so a failure shows every entry)
        await Assert.That(string.Join(Environment.NewLine, found.Except(known).Order())).IsEmpty();
        await Assert.That(string.Join(Environment.NewLine, known.Except(found).Order())).IsEmpty();
    }

    private static IEnumerable<string> SourceFiles(string sourceDirectory, string pattern)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(sourceDirectory, pattern, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"));
    }

    // "<file>: <element> <caption>" for each enabled, visible item that has no action and no items of its own
    private static IEnumerable<string> DeadMenuItems(string sourceDirectory, string path)
    {
        var file = Path.GetRelativePath(sourceDirectory, path).Replace('\\', '/');
        return XDocument.Load(path).Descendants()
            .Where(p => _clickableElements.Contains(p.Name.LocalName))
            .Where(p => !_actionAttributes.Any(a => p.Attribute(a) != null || p.Element(p.Name + "." + a) != null))
            .Where(p => !p.Descendants().Any(c => _clickableElements.Contains(c.Name.LocalName) || c.Name.LocalName == "BarSubItem"))
            .Where(p => (string?)p.Attribute("IsEnabled") != "False" && (string?)p.Attribute("Visibility") != "Collapsed")
            .Select(p => $"{file}: {p.Name.LocalName} {(string?)p.Attribute("Header") ?? (string?)p.Attribute("Content") ?? "(no caption)"}");
    }

    // "<file>: empty command <method>" and "<file>: stub <method>" for code that does nothing when the user acts
    private static IEnumerable<string> DeadCode(string sourceDirectory, string path)
    {
        var file = Path.GetRelativePath(sourceDirectory, path).Replace('\\', '/');
        var code = File.ReadAllText(path);

        foreach (Match item in EmptyRelayCommand().Matches(code))
            yield return $"{file}: empty command {item.Groups["method"].Value}";

        foreach (Match item in NotYetImplemented().Matches(code))
            yield return $"{file}: stub {MethodAround(code, item.Index)}";
    }

    // The name of the method declared nearest before the given position
    private static string MethodAround(string code, int index) =>
        MethodDeclaration().Matches(code[..index]).LastOrDefault()?.Groups["method"].Value ?? "(unknown)";

    [GeneratedRegex(@"\[RelayCommand[^\]]*\]\s*(?:(?:private|public|internal|protected|async|static)\s+)*\w+\s+(?<method>\w+)\([^)]*\)\s*\{\s*\}")]
    private static partial Regex EmptyRelayCommand();

    // In a string literal, i.e. logged or shown when the user acts; a comment saying so is not an action
    [GeneratedRegex(@"""[^""\r\n]*not yet implemented[^""\r\n]*""", RegexOptions.IgnoreCase)]
    private static partial Regex NotYetImplemented();

    [GeneratedRegex(@"(?:private|public|internal|protected)\s+(?:(?:async|static|override|virtual)\s+)*[\w<>?,\s]+?\s+(?<method>\w+)\s*\(")]
    private static partial Regex MethodDeclaration();

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
