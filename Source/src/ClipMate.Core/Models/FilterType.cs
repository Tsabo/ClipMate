namespace ClipMate.Core.Models;

/// <summary>
/// Defines the type of filter to apply when matching applications.
/// </summary>
public enum FilterType
{
    /// <summary>
    /// Match against process name (e.g., "notepad.exe").
    /// </summary>
    ProcessName = 0,

    /// <summary>
    /// Match against window title (e.g., "*Password*").
    /// </summary>
    WindowTitle = 1,

    /// <summary>
    /// Match against window class name.
    /// </summary>
    WindowClass = 2
}
