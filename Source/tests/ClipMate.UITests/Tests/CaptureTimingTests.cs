using System.Runtime.InteropServices;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// When ClipMate reads a clipboard change: once, after the source has finished writing its formats, and on a thread
/// of its own, so a source that is slow to supply its data never freezes ClipMate's window.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public partial class CaptureTimingTests(AppFixture appFixture)
{
    [Test]
    public async Task FormatsWrittenOneAtATime_AreCapturedOnce()
    {
        var text = $"in steps {Guid.NewGuid():N}";
        var probe = $"probe {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Act: text, then HTML 50 ms later, then RTF 50 ms after that; each step alone would make a different clip
            using (var program = ClipboardTestProgram.WriteInSteps(text, 50))
            {
                await Poll.UntilAsync(() => program.HasPrinted("written"), "the test program to write its formats");
                await clipList.WaitForRowAsync(text);
            }

            // A later copy is captured after any capture of the earlier change, so by then every capture has been made
            var attempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (attempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(probe));

                return clipList.FindRow(probe);
            }, "the probe copy in the clip list");

            // Assert: one clip, holding every format
            var clips = ReadClipIdsWithText(text);
            await Assert.That(clips).HasSingleItem();
            var formats = ReadFormatNames(clips[0]);
            await Assert.That(formats.Any(p => p.Contains("HTML", StringComparison.OrdinalIgnoreCase))).IsTrue();
            await Assert.That(formats.Any(p => p.Contains("RTF", StringComparison.OrdinalIgnoreCase))).IsTrue();
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    [Test]
    public async Task SourceSlowToRenderItsData_DoesNotFreezeClipMatesWindow()
    {
        var text = $"slow {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        ClipboardTestProgram? program = null;
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            appFixture.Process.Refresh();
            var mainWindow = appFixture.Process.MainWindowHandle;
            await Assert.That(mainWindow).IsNotEqualTo(IntPtr.Zero);

            // Act: a copy whose text the source takes 5 seconds to supply once ClipMate asks for it
            program = ClipboardTestProgram.OfferSlowly(text, 5000, appFixture.Process);
            await Poll.UntilAsync(() => program.HasPrinted("rendering"), "ClipMate to ask the test program for its text");

            // Assert: while the capture waits, ClipMate's window still handles messages; the capture then completes
            var responsive = SendMessageTimeout(mainWindow, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero, 0x0002 /* SMTO_ABORTIFHUNG */, 1000, out _) != IntPtr.Zero;
            var stillRendering = !program.HasPrinted("rendered");
            await Assert.That(stillRendering).IsTrue();
            await Assert.That(responsive).IsTrue();
            await clipList.WaitForRowAsync(text);
        }
        finally
        {
            program?.Dispose();
            SystemClipboard.Restore(backup);
        }
    }

    private List<object> ReadClipIdsWithText(string text)
    {
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT ClipId FROM BlobTxt WHERE Data = $text";
        command.Parameters.AddWithValue("$text", text);

        using var reader = command.ExecuteReader();
        var ids = new List<object>();
        while (reader.Read())
            ids.Add(reader.GetValue(0));

        return ids;
    }

    private List<string> ReadFormatNames(object clipId)
    {
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FormatName FROM ClipData WHERE ClipId = $clipId";
        command.Parameters.AddWithValue("$clipId", clipId);

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names;
    }

    private SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection;
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);
}
