namespace ClipMate.Tests.Integration.Fakes;

/// <summary>
/// A query that blocks the thread it runs on until released, the way Microsoft.Data.Sqlite's async methods complete
/// synchronously. Shows whether a caller gets its task back before the query finishes.
/// </summary>
internal sealed class SlowQuery : IDisposable
{
    // Long enough to tell a blocked caller from a returned one; short enough that a failing test does not hang
    private static readonly TimeSpan _blockFor = TimeSpan.FromSeconds(5);

    private readonly ManualResetEventSlim _release = new();

    /// <summary>
    /// Blocks until released (or the timeout passes), then returns <paramref name="result" /> as a completed task.
    /// </summary>
    public Task<T> Run<T>(T result)
    {
        _release.Wait(_blockFor);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Calls <paramref name="read" /> and asserts its task is not yet complete while the query is held, then releases
    /// the query and awaits the task.
    /// </summary>
    public async Task AssertCallerGetsControlBackAsync<T>(Func<Task<T>> read)
    {
        var task = read();
        var completedWhileHeld = task.IsCompleted;
        _release.Set();
        await task;

        await Assert.That(completedWhileHeld).IsFalse();
    }

    public void Dispose()
    {
        _release.Set();
        _release.Dispose();
    }
}
