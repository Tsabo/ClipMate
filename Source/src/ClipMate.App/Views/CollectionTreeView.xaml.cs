using System.Windows;
using System.Windows.Controls;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for CollectionTreeView.xaml
/// </summary>
public partial class CollectionTreeView : UserControl
{
    public CollectionTreeView()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CollectionTreeViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue;
        }
    }
}
