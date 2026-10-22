using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using ClipMate.UITests.Fixtures;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Files cut in Explorer, captured by ClipMate, then picked from the clip list: the clipboard must offer what Explorer
/// offered (CF_HDROP, FileName(W), Shell IDList Array, Preferred DropEffect) plus the path text, and a shell paste
/// must move the files.
/// <para>
/// Uses the real, shared clipboard: anything else watching it (another clipboard manager) sees the test's clips. The
/// clipboard's previous content is put back afterwards.
/// </para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
public class ClipboardFileDropRoundTripTests(AppFixture appFixture)
{
    private const int _dropEffectMove = 2;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    [Test]
    public async Task CutFiles_CapturedAndPicked_PasteAsAMoveIntoAnotherFolder()
    {
        var root = Directory.CreateTempSubdirectory("clipmate-roundtrip-");
        var sourceFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "source"));
        var targetFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "target"));
        string[] files = [Path.Combine(sourceFolder.FullName, "alpha.txt"), Path.Combine(sourceFolder.FullName, "beta.txt")];
        foreach (var item in files)
            await File.WriteAllTextAsync(item, Path.GetFileName(item));

        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();

            // Arrange: an Explorer-style cut (file list plus DROPEFFECT_MOVE, no text)
            SystemClipboard.OnSta(() =>
            {
                var cut = new DataObject();
                var fileDropList = new StringCollection();
                fileDropList.AddRange(files);
                cut.SetFileDropList(fileDropList);
                cut.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(_dropEffectMove)));
                Clipboard.SetDataObject(cut, true);
            });

            var storedFormats = await WaitForAsync(() => ReadStoredFormatsOfClipWithFiles(), "ClipMate to capture the cut");
            await Assert.That(storedFormats.Keys).Contains("HDROP");
            await Assert.That(storedFormats.Keys).Contains("CF_UNICODETEXT");
            await Assert.That(storedFormats["Preferred DropEffect"]).IsEquivalentTo(BitConverter.GetBytes(_dropEffectMove));

            // Act: replace the clipboard, then pick the files clip from the list (selecting a clip copies it back).
            // ClipMate copies a newly captured clip back once its selection settles, which can overwrite the probe,
            // so the probe is set again until its clip shows up.
            await WaitForAsync(() => FindClipRow("📁"), "the files clip in the clip list"); // titles are truncated; the row's icons are not
            var probeAttempt = 0;
            await WaitForAsync(() =>
            {
                if (probeAttempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText("probe text"));

                return FindClipRow("probe text");
            }, "the probe clip in the clip list");

            // Look the row up again: the grid recreates its rows when the probe clip is inserted above it
            var filesRow = await WaitForAsync(() => FindClipRow("📁"), "the files clip in the clip list");
            ((SelectionItemPattern)filesRow.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

            var offered = await WaitForAsync(() =>
            {
                var formats = SystemClipboard.TryGetRawFormatNames();
                return formats?.Contains("Shell IDList Array") == true ? formats : null;
            }, "the files clip to be copied back");

            // Assert: Explorer's formats are offered, then a shell paste moves the files
            await Assert.That(offered).Contains("#15"); // CF_HDROP
            await Assert.That(offered).Contains("#13"); // CF_UNICODETEXT
            await Assert.That(offered).Contains("FileNameW");
            await Assert.That(offered).Contains("Preferred DropEffect");

            var (pastedFiles, dropEffect) = SystemClipboard.OnSta(() =>
            {
                var data = Clipboard.GetDataObject()!;
                var effect = (MemoryStream)data.GetData("Preferred DropEffect")!;
                return (Clipboard.GetFileDropList().Cast<string>().ToList(), BitConverter.ToInt32(effect.ToArray()));
            });

            await Assert.That(pastedFiles).IsEquivalentTo(files);
            await Assert.That(dropEffect).IsEqualTo(_dropEffectMove);

            SystemClipboard.OnSta(() =>
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
                shell.NameSpace(targetFolder.FullName).Self.InvokeVerb("paste");
            });

            await WaitForAsync(() => files.All(p => File.Exists(Path.Combine(targetFolder.FullName, Path.GetFileName(p)))) ? true : (bool?)null,
                "the shell to paste the files");
            await Assert.That(sourceFolder.EnumerateFiles().Any()).IsFalse();
        }
        finally
        {
            SystemClipboard.Restore(backup);
            root.Delete(true);
        }
    }

    /// <summary>
    /// The stored formats (name to binary content) of the clip that has a file list, or null until it is captured.
    /// </summary>
    private Dictionary<string, byte[]>? ReadStoredFormatsOfClipWithFiles()
    {
        if (!File.Exists(appFixture.DatabasePath))
            return null;

        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT d.FormatName, b.Data FROM ClipData d LEFT JOIN BlobBlob b ON b.ClipDataId = d.Id
                              WHERE d.ClipId = (SELECT ClipId FROM ClipData WHERE FormatName = 'HDROP' LIMIT 1)
                              """;

        using var reader = command.ExecuteReader();
        var formats = new Dictionary<string, byte[]>();
        while (reader.Read())
            formats[reader.GetString(0)] = reader.IsDBNull(1) ? [] : (byte[])reader[1];

        return formats.Count > 0 ? formats : null;
    }

    /// <summary>
    /// The clip-list row whose text contains <paramref name="text" />, found through UI Automation.
    /// </summary>
    private AutomationElement? FindClipRow(string text)
    {
        var window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, appFixture.Process.Id),
            new PropertyCondition(AutomationElement.NameProperty, "ClipMate - Clipboard Manager")));

        return window?.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem))
            .Cast<AutomationElement>()
            .FirstOrDefault(p => p.Current.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ClipMate is a separate process, so its progress can only be observed by polling until a deadline.
    /// </summary>
    private static async Task<T> WaitForAsync<T>(Func<T?> probe, string what) where T : class
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (probe() is { } result)
                return result;

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out after {_timeout.TotalSeconds}s waiting for {what}");
    }

    private static async Task WaitForAsync(Func<bool?> probe, string what) =>
        await WaitForAsync(() => probe() == true ? "done" : null, what);
}
