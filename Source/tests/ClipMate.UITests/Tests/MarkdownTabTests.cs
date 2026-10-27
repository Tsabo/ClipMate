using System.Text.RegularExpressions;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;

namespace ClipMate.UITests.Tests;

/// <summary>
/// The Markdown tab shows the clip in the viewer: with the tab open, each new HTML clip replaces the Markdown shown,
/// including after the editor has loaded its first text. The editor's content lives in WebView2, out of reach of UI
/// Automation, so the test follows ClipMate's debug log: each conversion and each text the editor loads, by length.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public partial class MarkdownTabTests(AppFixture appFixture)
{
    [Test]
    public async Task NewHtmlClip_ReplacesTheMarkdownShown()
    {
        var id = Guid.NewGuid().ToString("N");
        var first = $"first {id}";
        var link = $"https://github.com/Tsabo/ClipMate?{id}";
        var backup = SystemClipboard.Backup();
        try
        {
            // The viewer opens on the Markdown tab
            await appFixture.StartAppAsync("preferences.default_editor_view=\"Markdown\"", "preferences.log_level=\"Debug\"");
            var clipList = new ClipListPage(appFixture.Process);
            using var window = await ForegroundTestWindow.OpenAsync();

            window.Copy(() => HtmlCopy.Create($"<b>{first}</b>", first));
            await clipList.WaitForRowAsync(first);
            await Poll.UntilAsync(() => EditorLoads().Count > 0, "the editor to load the first clip's Markdown", TimeSpan.FromSeconds(30));
            var conversions = Conversions().Count;
            var loads = EditorLoads().Count;

            // Act: a link copied as a browser's address bar does, while the Markdown tab is open
            window.Copy(() => HtmlCopy.Create($"<a href=\"{link}\">GitHub - Tsabo/ClipMate</a>", link));
            await Poll.UntilAsync(() => Conversions().Count > conversions, "the link's conversion to Markdown");

            // Assert: the editor loads the link's Markdown
            var linkMarkdown = Conversions()[^1];
            await Poll.UntilAsync(() => EditorLoads().Skip(loads).Contains(linkMarkdown), $"the editor to load the link's {linkMarkdown} characters of Markdown");
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    /// <summary>
    /// The length of each Markdown conversion, in order.
    /// </summary>
    private List<int> Conversions() => Lengths(ConversionLine());

    /// <summary>
    /// The length of each text the Markdown editor has loaded, in order.
    /// </summary>
    private List<int> EditorLoads() => Lengths(EditorLoadLine());

    private List<int> Lengths(Regex line) =>
        line.Matches(appFixture.ReadLog()).Select(p => int.Parse(p.Groups["length"].Value)).ToList();

    [GeneratedRegex(@"\[MarkdownViewer\] Converted ""?\w+""? to ""?\w+""? Markdown \((?<length>\d+) chars\)")]
    private static partial Regex ConversionLine();

    // Only loads that reach Monaco: one made before the editor has initialized is logged as failed, separately
    [GeneratedRegex(@"\[MarkdownEditor\] LoadContentAsync - Text: (?<length>\d+) chars, Language: \w+, HasViewState: \w+, IsInitialized: true", RegexOptions.IgnoreCase)]
    private static partial Regex EditorLoadLine();
}
