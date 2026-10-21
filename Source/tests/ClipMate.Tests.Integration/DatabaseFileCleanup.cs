using Microsoft.Data.Sqlite;

namespace ClipMate.Tests.Integration;

/// <summary>
/// Deletes a test's SQLite database file, which Windows can keep locked briefly after its connections close.
/// </summary>
internal static class DatabaseFileCleanup
{
    /// <summary>
    /// Deletes <paramref name="path" />, retrying with a growing back-off while the file is still locked.
    /// </summary>
    public static void Delete(string path, int attempts = 10)
    {
        if (!File.Exists(path))
            return;

        SqliteConnection.ClearAllPools();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                // Finalizers release connections that tests forgot to dispose
                GC.Collect();
                GC.WaitForPendingFinalizers();
                SqliteConnection.ClearAllPools();

                // The one wall-clock wait tests may use: an OS file lock has no signal to wait on
#pragma warning disable RS0030
                Thread.Sleep(100 * attempt);
#pragma warning restore RS0030
            }
        }
    }
}
