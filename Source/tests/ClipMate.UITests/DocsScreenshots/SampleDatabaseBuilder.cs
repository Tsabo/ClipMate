using System.IO;
using System.Runtime.CompilerServices;
using ClipMate.UITests.Fixtures;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// Regenerates DocsScreenshots\SampleData\clipmate.db in the source tree, the database the user docs' screenshots show.
/// Lets ClipMate create the schema and default collections, so the file always matches the current build. Explicit, so
/// a plain UI-Test run skips it: <c>build/build.ps1 -Target UI-Test --test-filter=/*/*/SampleDatabaseBuilder/*</c>.
/// </summary>
public class SampleDatabaseBuilder
{
    [Test]
    [Explicit]
    public async Task Build()
    {
        await using var fixture = new AppFixture();

        // Only until it has created its database, so nothing is put on the clipboard
        await fixture.RunFirstTimeAsync(new AppStartOptions { SkipStartupCapture = true });
        SampleDatabaseWriter.Write(fixture.DatabasePath);

        var target = TargetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(fixture.DatabasePath, target, true);

        await Assert.That(ReadScalar(target, "Pragma journal_mode")).IsEqualTo("delete");
        await Assert.That(ReadScalar(target, "Pragma integrity_check")).IsEqualTo("ok");
        await Assert.That(File.Exists(target + "-wal")).IsFalse();
    }

    private static string? ReadScalar(string databasePath, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()?.ToString();
    }

    private static string TargetPath([CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, "SampleData", "clipmate.db");
}
