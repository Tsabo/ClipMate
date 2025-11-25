using System.Windows;
using System.Windows.Controls;
using ClipMate.App.ViewModels;
using DevExpress.Xpf.Grid;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for CollectionTreeView.xaml
/// Displays hierarchical collection tree using DevExpress TreeListControl.
/// Structure: Database -> Collections/Virtual Collections -> Folders
/// </summary>
public partial class CollectionTreeView : System.Windows.Controls.UserControl
{
    public CollectionTreeView()
    {
        InitializeComponent();
    }
}
