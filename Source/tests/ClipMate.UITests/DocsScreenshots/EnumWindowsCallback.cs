namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// The callback of user32's EnumWindows.
/// </summary>
internal delegate bool EnumWindowsCallback(nint window, nint parameter);
