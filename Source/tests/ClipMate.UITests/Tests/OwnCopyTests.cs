using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// ClipMate marks its own copies with "Clipboard Viewer Ignore", so picking a clip doesn't capture it again, while the
/// same content copied by another program right afterwards is captured and left to the collection's Accept Duplicates:
/// a Duplicate of the newest clip is rejected, the text of an older clip is kept.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class OwnCopyTests(AppFixture appFixture)
{
    private const string _ignoreMarker = "Clipboard Viewer Ignore";
    private const string _duplicateLogLine = "Not saving a duplicate clip";

    [Test]
    public async Task PickAClip_IsNotCapturedAgain_ButTheSameCopyFromAnotherProgramIsADuplicate()
    {
        var picked = $"picked {Guid.NewGuid():N}";
        var other = $"other {Guid.NewGuid():N}";
        var probe = $"probe {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            await CopyUntilListedAsync(clipList, picked);
            await CopyUntilListedAsync(clipList, other);
            var clipsBefore = CountClips();

            // Act: pick a clip
            await PickAsync(clipList, picked);

            // Assert: ClipMate's copy carries the marker only, and isn't captured (a later copy is, and nothing else)
            var formats = await Poll.UntilAsync(SystemClipboard.TryGetRawFormatNames, "the clipboard's formats");
            await Assert.That(formats).Contains(_ignoreMarker);
            await Assert.That(formats).DoesNotContain("ExcludeClipboardContentFromMonitorProcessing");

            await CopyUntilListedAsync(clipList, probe);
            await Assert.That(CountClips()).IsEqualTo(clipsBefore + 1);
            await Assert.That(CountClips(picked)).IsEqualTo(1);

            // Act: another program copies the newest clip's text right after ClipMate's copy of it
            // (the newest clip, just captured, is already the current row: picking it copies nothing until another is picked)
            await PickAsync(clipList, other);
            var duplicatesBefore = CountDuplicateLines();
            await PickAsync(clipList, probe);
            using (var program = ClipboardTestProgram.CopyInBackground(probe, $"ClipMate UI test copier {Guid.NewGuid():N}"))
            {
                await Poll.UntilAsync(() => program.HasPrinted("written"), "the test program to copy");

                // Assert: captured and rejected as a Duplicate of the newest clip (Inbox doesn't accept duplicates)
                await Poll.UntilAsync(() => CountDuplicateLines() > duplicatesBefore, "ClipMate to capture the other program's copy");
            }

            await Assert.That(CountClips(probe)).IsEqualTo(1);

            // Act: another program copies an older clip's text, which ClipMate 7 keeps: only the newest clip counts
            using (var program = ClipboardTestProgram.CopyInBackground(picked, $"ClipMate UI test copier {Guid.NewGuid():N}"))
            {
                await Poll.UntilAsync(() => program.HasPrinted("written"), "the test program to copy");

                // Assert
                await Poll.UntilAsync(() => CountClips(picked) == 2 ? "saved" : null, "ClipMate to save the older clip's text again");
            }
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    // Picking a clip copies it back to the clipboard
    private async Task PickAsync(ClipListPage clipList, string text)
    {
        await clipList.SelectAsync(text);
        await Poll.UntilAsync(() => SystemClipboard.IsOwnedBy(appFixture.Process) && SystemClipboard.TryReadText(IntPtr.Zero) == text,
            $"ClipMate to copy \"{text}\"");
    }

    private static async Task CopyUntilListedAsync(ClipListPage clipList, string text)
    {
        var attempt = 0;
        await Poll.UntilAsync(() =>
        {
            if (attempt++ % 10 == 0)
                SystemClipboard.OnSta(() => Clipboard.SetText(text));

            return clipList.FindRow(text);
        }, $"\"{text}\" in the clip list");
    }

    private int CountDuplicateLines()
    {
        var log = appFixture.ReadLog();
        var count = 0;
        for (var index = log.IndexOf(_duplicateLogLine, StringComparison.Ordinal); index >= 0;
             index = log.IndexOf(_duplicateLogLine, index + 1, StringComparison.Ordinal))
            count++;

        return count;
    }

    private long CountClips(string? text = null)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = text == null
            ? "SELECT COUNT(*) FROM Clips"
            : "SELECT COUNT(DISTINCT c.Id) FROM Clips c JOIN BlobTxt b ON b.ClipId = c.Id WHERE b.Data = $text";
        if (text != null)
            command.Parameters.AddWithValue("$text", text);

        return (long)command.ExecuteScalar()!;
    }
}
