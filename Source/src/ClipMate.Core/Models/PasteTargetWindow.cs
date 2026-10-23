namespace ClipMate.Core.Models;

/// <summary>
/// A window that QuickPaste pastes into: a top-level window of another program. A record, so a changed title counts
/// as a different window.
/// </summary>
/// <param name="Handle">The window handle.</param>
/// <param name="ProcessName">The program's process name in upper case, e.g. "WINWORD".</param>
/// <param name="ClassName">The window class name in upper case, e.g. "OPUSAPP".</param>
/// <param name="WindowTitle">The window title as shown.</param>
public sealed record PasteTargetWindow(nint Handle, string ProcessName, string ClassName, string WindowTitle)
{
    /// <summary>
    /// Gets the window as "PROCESSNAME:CLASSNAME", the form the GOOD and BAD target lists use.
    /// </summary>
    public string TargetString => $"{ProcessName}:{ClassName}";
}
