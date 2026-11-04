namespace ClipMate.UITests.Fixtures;

/// <summary>
/// The UI toolkit of a <see cref="TestAppWindow" />. It decides how the window reads the clipboard on Ctrl+V.
/// </summary>
public enum TestAppKind
{
    /// <summary>A Win32 edit control, which opens the clipboard with its own window.</summary>
    WinForms,

    /// <summary>A WPF text box, which reads through the OLE clipboard.</summary>
    Wpf,

    /// <summary>A text box that opens the clipboard without a window on Ctrl+V, as browsers and Windows Terminal do.</summary>
    NoWindowReader,
}
