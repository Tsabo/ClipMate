using ClipMate.App.Models;

namespace ClipMate.App.Services;

/// <summary>
/// Tells the search dialog what to search, from the selection in the collection tree.
/// </summary>
public interface ISearchTargetSource
{
    /// <summary>
    /// Gets the Database (local or remote), collection and folder of the selected tree node, or <c>null</c> when nothing
    /// is selected.
    /// </summary>
    DatabaseSearchTarget? GetSearchTarget();
}
