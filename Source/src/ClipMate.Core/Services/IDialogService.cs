using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for showing dialog boxes to the user.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog with a message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="button">The buttons to show.</param>
    /// <param name="icon">The icon to display.</param>
    /// <returns>The user's response.</returns>
    DialogResult ShowMessage(string message, string title, DialogButton button = DialogButton.OK, DialogIcon icon = DialogIcon.Information);
}
