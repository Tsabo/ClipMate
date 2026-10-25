using System.Diagnostics;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// A clip's Source Application is the program that owns the clipboard, not whichever window is in the foreground.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class SourceApplicationTests(AppFixture appFixture)
{
    [Test]
    public async Task BackgroundProgramCopies_SourceIsThatProgram()
    {
        var text = $"from the background {Guid.NewGuid():N}";
        var title = $"ClipMate UI test background copier {Guid.NewGuid():N}";
        var backup = SystemClipboard.Backup();
        try
        {
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);
            using var foreground = await ForegroundTestWindow.OpenAsync();

            // Act: a program without a visible window copies while the test window is in the foreground
            string programName;
            using (var program = ClipboardTestProgram.CopyInBackground(text, title))
            {
                programName = program.ProcessName;
                await Poll.UntilAsync(() => program.HasPrinted("written"), "the test program to copy");
                await clipList.WaitForRowAsync(text);
            }

            // Assert
            var (name, sourceTitle) = ReadSource(text);
            await Assert.That(name).IsEqualTo(programName);
            await Assert.That(name).IsNotEqualTo(Process.GetCurrentProcess().ProcessName);
            await Assert.That(sourceTitle).IsEqualTo(title);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    private (string? Name, string? Title) ReadSource(string text)
    {
        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT c.SourceApplicationName, c.SourceApplicationTitle
                              FROM Clips c JOIN BlobTxt b ON b.ClipId = c.Id
                              WHERE b.Data = $text
                              """;
        command.Parameters.AddWithValue("$text", text);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("The clip is not in the database");

        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
