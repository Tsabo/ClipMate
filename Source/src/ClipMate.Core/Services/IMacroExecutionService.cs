namespace ClipMate.Core.Services;

/// <summary>
/// Service for executing macro clips by sending keystrokes to the target application.
/// Macros allow dynamic text insertion with special keys (TAB, ENTER) and modifiers (Shift, Ctrl, Alt).
/// </summary>
public interface IMacroExecutionService
{
    /// <summary>
    /// Executes a macro clip by parsing the text and sending keystrokes to the active window.
    /// </summary>
    /// <param name="macroText">The macro text containing special keys and modifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if macro executed successfully, false if cancelled or failed.</returns>
    /// <remarks>
    /// Supported modifiers:
    /// - ~ = Shift
    /// - ^ = Control
    /// - @ = Alt
    /// Supported special keys (surround with {} braces):
    /// - {TAB}, {ENTER}, {ESC}, {BACKSPACE}
    /// - {UP}, {DOWN}, {LEFT}, {RIGHT}
    /// - {HOME}, {END}, {PGUP}, {PGDN}
    /// - {F1}-{F16}, {DELETE}, {INSERT}
    /// - {PAUSE} - Pause for 500ms
    /// Repeat counts: {LEFT 6} sends LEFT arrow 6 times
    /// Literal escaping: {@} sends @ character (not Alt modifier)
    /// Line breaks: Natural line breaks ignored, must use {ENTER}
    /// </remarks>
    Task<bool> ExecuteMacroAsync(string macroText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates macro text for potentially dangerous commands.
    /// </summary>
    /// <param name="macroText">The macro text to validate.</param>
    /// <returns>True if macro appears safe, false if potentially dangerous.</returns>
    /// <remarks>
    /// Dangerous patterns include:
    /// - Multiple ALT+F4 (close window)
    /// - System commands that could harm data
    /// </remarks>
    bool IsMacroSafe(string macroText);
}
