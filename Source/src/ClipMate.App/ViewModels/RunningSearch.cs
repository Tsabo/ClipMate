namespace ClipMate.App.ViewModels;

/// <summary>
/// The search that is running, and who canceled it, so the log can say.
/// </summary>
internal sealed class RunningSearch : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();

    public CancellationToken Token => _cancellation.Token;

    /// <summary>
    /// Who canceled the search, or <c>null</c> while it has not been.
    /// </summary>
    public string? CancelReason { get; private set; }

    public void Cancel(string reason)
    {
        CancelReason ??= reason;
        _cancellation.Cancel();
    }

    public void Dispose() => _cancellation.Dispose();
}
