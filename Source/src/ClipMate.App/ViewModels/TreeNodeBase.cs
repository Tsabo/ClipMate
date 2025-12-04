using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Image = Emoji.Wpf.Image;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Base class for all tree nodes in the collection tree hierarchy.
/// Supports Database -> Collections -> Virtual Collections structure.
/// </summary>
public abstract partial class TreeNodeBase : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private TreeNodeBase? _parent;

    /// <summary>
    /// Display name for the node.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Icon identifier for the node (emoji or DevExpress icon).
    /// </summary>
    public virtual string Icon => "📁";

    /// <summary>
    /// Icon image created from the emoji Icon property.
    /// Used by DevExpress TreeViewControl's ImageFieldName.
    /// </summary>
    public ImageSource? IconImage
    {
        get
        {
            var image = new DrawingImage();
            Image.SetSource(image, Icon);

            return image;
        }
    }

    /// <summary>
    /// Type of node for template selection and behavior.
    /// </summary>
    public abstract TreeNodeType NodeType { get; }

    /// <summary>
    /// Child nodes of this tree node.
    /// </summary>
    public ObservableCollection<TreeNodeBase> Children { get; } = [];

    /// <summary>
    /// Optional: Sort key for manual sorting (ClipMate 7.5 feature).
    /// </summary>
    public int SortKey { get; set; }
}
