using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ClipMate.Tests.Unit;

/// <summary>
/// Every type this project passes to <c>A.Fake&lt;T&gt;()</c> must be in <see cref="FakeProxyTypeWarmup" />'s list, or
/// its first fake during a parallel run can crash the test process (see the warm-up's remarks).
/// </summary>
public partial class FakeProxyTypeWarmupTests
{
    [Test]
    public async Task EveryFakedType_IsWarmedUp()
    {
        // Arrange: the test sources sit next to this file
        var projectDirectory = Path.GetDirectoryName(SourcePath())!;
        var sources = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        var warmup = await File.ReadAllTextAsync(Path.Combine(projectDirectory, "FakeProxyTypeWarmup.cs"));
        var warmedUp = TypeOfPattern().Matches(warmup).Select(p => Normalize(p.Groups[1].Value)).ToHashSet();

        // Act
        var faked = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var item in sources)
        {
            foreach (Match match in FakePattern().Matches(await File.ReadAllTextAsync(item)))
                faked.Add(Normalize(match.Groups[1].Value));
        }

        // Assert: a generic helper's type parameter (A.Fake<ILogger<T>>) is warmed up by its callers' concrete types
        var missing = faked.Where(p => !warmedUp.Contains(p) && !OpenTypeParameter().IsMatch(p)).ToList();
        await Assert.That(missing).IsEmpty();
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;

    // "ClipMate.App.Services.IPeerDialogService" and "IPeerDialogService" name the same type; ignore spacing
    private static string Normalize(string type)
    {
        var compact = Regex.Replace(type, @"\s+", string.Empty);
        var genericStart = compact.IndexOf('<');
        var name = genericStart < 0 ? compact : compact[..genericStart];
        var arguments = genericStart < 0 ? string.Empty : compact[genericStart..];
        return name[(name.LastIndexOf('.') + 1)..] + arguments;
    }

    [GeneratedRegex(@"A\.Fake<((?:[\w.]+)(?:<[\w.,\s]+>)?)>\s*\(")]
    private static partial Regex FakePattern();

    [GeneratedRegex(@"typeof\(((?:[\w.]+)(?:<[\w.,\s]+>)?)\)")]
    private static partial Regex TypeOfPattern();

    [GeneratedRegex(@"<(T\w*)(,\s*T\w*)*>$")]
    private static partial Regex OpenTypeParameter();
}
