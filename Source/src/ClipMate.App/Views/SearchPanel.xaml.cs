using System.Windows.Controls;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for SearchPanel.xaml
/// </summary>
public partial class SearchPanel : UserControl
{
    public SearchPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Constructor for dependency injection with ViewModel
    /// </summary>
    /// <param name="viewModel">The SearchViewModel instance</param>
    public SearchPanel(SearchViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
