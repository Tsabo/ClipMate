using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for QuickPaste functionality including auto-targeting, formatting strings,
/// and keystroke execution to paste clips into target applications.
/// </summary>
public interface IQuickPasteService
{
    /// <summary>
    /// Gets the current QuickPaste target information.
    /// </summary>
    /// <returns>A tuple containing (ProcessName, ClassName, WindowTitle) or null if no target.</returns>
    (string ProcessName, string ClassName, string WindowTitle)? GetCurrentTarget();

    /// <summary>
    /// Sets the target lock state.
    /// When locked, auto-targeting is disabled and the current target is preserved.
    /// </summary>
    /// <param name="locked">True to lock the target, false to enable auto-targeting.</param>
    void SetTargetLock(bool locked);

    /// <summary>
    /// Gets whether the target is currently locked.
    /// </summary>
    /// <returns>True if target is locked, false otherwise.</returns>
    bool IsTargetLocked();

    /// <summary>
    /// Gets the current GoBack state.
    /// When enabled, focus returns to ClipMate after pasting.
    /// </summary>
    /// <returns>True if GoBack is enabled, false otherwise.</returns>
    bool GetGoBackState();

    /// <summary>
    /// Sets the GoBack state.
    /// </summary>
    /// <param name="goBack">True to return focus to ClipMate after pasting, false to keep focus in target.</param>
    void SetGoBackState(bool goBack);

    /// <summary>
    /// Gets the currently selected formatting string.
    /// </summary>
    /// <returns>The active formatting string or null if none selected.</returns>
    QuickPasteFormattingString? GetSelectedFormattingString();

    /// <summary>
    /// Selects a formatting string for use in subsequent paste operations.
    /// </summary>
    /// <param name="format">The formatting string to select.</param>
    void SelectFormattingString(QuickPasteFormattingString? format);

    /// <summary>
    /// Pastes the specified clip to the current target using the selected formatting string.
    /// </summary>
    /// <param name="clip">The clip to paste.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if paste was successful, false otherwise.</returns>
    Task<bool> PasteClipAsync(Clip clip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a TAB keystroke to the current target.
    /// </summary>
    void SendTabKeystroke();

    /// <summary>
    /// Sends an ENTER keystroke to the current target.
    /// </summary>
    void SendEnterKeystroke();

    /// <summary>
    /// Resets the sequence counter used by #SEQUENCE# macro to 1.
    /// </summary>
    void ResetSequence();

    /// <summary>
    /// Manually updates the target by detecting the foreground window.
    /// </summary>
    void UpdateTarget();

    /// <summary>
    /// Gets the current target in format "PROCESSNAME:CLASSNAME".
    /// </summary>
    /// <returns>The target string or empty if no target.</returns>
    string GetCurrentTargetString();
}
