using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for QuickPaste: formatting strings and keystroke execution to paste clips into the Paste Target
/// (see <see cref="IPasteTarget" />).
/// </summary>
public interface IQuickPasteService
{
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
    /// Switches focus to the current target and sends a TAB keystroke.
    /// </summary>
    Task SendTabKeystroke();

    /// <summary>
    /// Switches focus to the current target and sends an ENTER keystroke.
    /// </summary>
    Task SendEnterKeystroke();

    /// <summary>
    /// Resets the sequence counter used by #SEQUENCE# macro to 1.
    /// </summary>
    void ResetSequence();
}
