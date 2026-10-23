namespace ClipMate.Platform.Helpers;

/// <summary>
/// Recognises ClipMate's own process by its process ID, which, unlike the process name, holds under a test host or a
/// renamed executable.
/// </summary>
public static class OwnProcess
{
    /// <summary>
    /// ClipMate's process ID.
    /// </summary>
    public static uint Id { get; } = (uint)Environment.ProcessId;

    /// <summary>
    /// Whether <paramref name="processId" /> is ClipMate's own process.
    /// </summary>
    public static bool Is(uint processId) => processId == Id;
}
