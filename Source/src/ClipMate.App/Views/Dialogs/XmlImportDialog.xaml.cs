using ClipMate.App.ViewModels;
using DevExpress.Xpf.Dialogs;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;

namespace ClipMate.App.Views.Dialogs;

public partial class XmlImportDialog
{
    private readonly XmlImportViewModel _viewModel;

    public XmlImportDialog(XmlImportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e) => await _viewModel.InitializeAsync();

    private void BrowseXmlImportFile_Click(object sender, RoutedEventArgs e)
    {
        DXOpenFileDialog? openDialog = null;
        try
        {
            openDialog = new DXOpenFileDialog();
            openDialog.Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*";

            if (openDialog.ShowDialog() == true)
                _viewModel.SetXmlImportFilePath(openDialog.FileName);
        }
        finally
        {
            (openDialog as IDisposable)?.Dispose();
        }
    }

    private void CollectionTreeView_SelectedItemChanged(object sender, SelectedItemChangedEventArgs e)
    {
        // Only allow selecting Collection nodes (not Database nodes or folders)
        if (e.NewItem is not CollectionTreeNode collectionNode)
            return;

        _viewModel.SelectedCollection = collectionNode;

        // Update the display text and close the popup
        CollectionLookUp.EditValue = collectionNode;
        CollectionLookUp.ClosePopup();
    }

    private void CollectionLookUp_OnCustomDisplayText(object sender, CustomDisplayTextEventArgs e) => e.DisplayText = _viewModel?.SelectedCollection?.Name;
}
