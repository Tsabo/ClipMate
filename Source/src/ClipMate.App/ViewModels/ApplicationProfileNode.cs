using ClipMate.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents an application profile node in the tree view for Options dialog.
/// Displays an application with its associated clipboard format filters.
/// </summary>
public partial class ApplicationProfileNode : TreeNodeBase
{
    [ObservableProperty]
    private bool _enabled;

    /// <summary>
    /// Creates a new application profile node.
    /// </summary>
    /// <param name="profile">The application profile model.</param>
    public ApplicationProfileNode(ApplicationProfile profile)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _enabled = profile.Enabled;

        // Populate format children
        foreach (var format in profile.Formats.OrderBy(f => f.Key))
        {
            var formatNode = new ApplicationProfileFormatNode(format.Key, format.Value)
            {
                Parent = this,
            };

            Children.Add(formatNode);
        }
    }

    /// <summary>
    /// The underlying application profile model.
    /// </summary>
    public ApplicationProfile Profile { get; }

    /// <summary>
    /// Display name is the application name.
    /// </summary>
    public override string Name => Profile.ApplicationName;

    /// <summary>
    /// Application icon (desktop app emoji).
    /// </summary>
    public override string Icon => "🖥️";

    /// <summary>
    /// Node type for application profile.
    /// </summary>
    public override TreeNodeType NodeType => TreeNodeType.ApplicationProfile;

    /// <summary>
    /// Updates the underlying profile's enabled state when checkbox changes.
    /// </summary>
    partial void OnEnabledChanged(bool value) => Profile.Enabled = value;
}
