using Application = System.Windows.Application;

namespace ClipMate.Platform.Helpers;

/// <summary>
/// Runs work on the WPF UI thread.
/// </summary>
public static class UiThread
{
    /// <summary>
    /// Runs the action now when called on the UI thread (or when there is no application, as in unit tests), otherwise
    /// queues it there without waiting.
    /// </summary>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Runs the action on the UI thread, now when called there (or when there is no application, as in unit tests),
    /// and completes once it has run.
    /// </summary>
    public static Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            return dispatcher.InvokeAsync(action).Task;

        action();
        return Task.CompletedTask;
    }
}
