namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Waits on another process (ClipMate, the shell, the clipboard), whose progress can only be observed by polling
/// until a deadline.
/// </summary>
public static class Poll
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Calls <paramref name="probe" /> until it returns a value, then returns it.
    /// </summary>
    /// <param name="what">What is awaited, for the timeout message ("the files clip in the clip list").</param>
    public static async Task<T> UntilAsync<T>(Func<T?> probe, string what, TimeSpan? timeout = null) where T : class
    {
        var limit = timeout ?? DefaultTimeout;
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline)
        {
            if (probe() is { } result)
                return result;

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out after {limit.TotalSeconds}s waiting for {what}");
    }

    /// <summary>
    /// Calls <paramref name="condition" /> until it is true.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, string what, TimeSpan? timeout = null) =>
        await UntilAsync(() => condition() ? "done" : null, what, timeout);
}
