namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// A reset asked for in Options > Advanced, carried out the next time ClipMate starts. Ordered by how much it resets.
/// </summary>
public enum PendingSettingsReset
{
    /// <summary>No reset is waiting.</summary>
    None,

    /// <summary>ClipMate Classic's position and size and the editor font (ClipMate 7's Position and Fonts sections).</summary>
    LayoutAndFonts,

    /// <summary>Every setting except what the user has built up, see <see cref="SettingsReset" />.</summary>
    Everything,
}
