using System.Collections.Specialized;
using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
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
[NotInParallel(SystemClipboard.ParallelKey)]
public class ClipboardFileDropRoundTripTests(AppFixture appFixture)
{
    private const int _dropEffectMove = 2;
    private const string _probeText = "probe text";

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
            TestProgramProfile.Write(appFixture, "CF_HDROP");
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Arrange: an Explorer-style cut (file list plus DROPEFFECT_MOVE, no text)
            using var source = await ForegroundTestWindow.OpenAsync();
            source.Copy(() =>
            {
                var cut = new DataObject();
                var fileDropList = new StringCollection();
                fileDropList.AddRange(files);
                cut.SetFileDropList(fileDropList);
                cut.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(_dropEffectMove)));
                return cut;
            });

            var storedFormats = await Poll.UntilAsync(ReadStoredFormatsOfClipWithFiles, "ClipMate to capture the cut");
            await Assert.That(storedFormats.Keys).Contains("HDROP");
            await Assert.That(storedFormats.Keys).Contains("CF_UNICODETEXT");
            await Assert.That(storedFormats["Preferred DropEffect"]).IsEquivalentTo(BitConverter.GetBytes(_dropEffectMove));

            // Act: put other text on the clipboard, then pick the files clip (selecting a clip copies it back).
            // ClipMate's own copy-back of the new files clip can overwrite the probe, so set it again until it is listed.
            await clipList.WaitForRowAsync(ClipListPage.FilesIcon);
            var probeAttempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (probeAttempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(_probeText));

                return clipList.FindRow(_probeText);
            }, "the probe clip in the clip list");

            await clipList.SelectAsync(ClipListPage.FilesIcon);

            var offered = await Poll.UntilAsync(() =>
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

            await Poll.UntilAsync(() => files.All(p => File.Exists(Path.Combine(targetFolder.FullName, Path.GetFileName(p)))),
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
}
