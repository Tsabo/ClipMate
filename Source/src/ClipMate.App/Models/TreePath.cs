namespace ClipMate.App.Models;

/// <summary>
/// Where something is in the collection tree, written out as the dialogs and the clip list show it: "My Clips › Inbox
/// › Projects".
/// </summary>
public static class TreePath
{
    /// <summary>
    /// Goes between each level of a path.
    /// </summary>
    public const string Separator = " › ";

    /// <summary>
    /// The path through <paramref name="names" />, outermost first.
    /// </summary>
    public static string Join(params IEnumerable<string> names) => string.Join(Separator, names);
}
