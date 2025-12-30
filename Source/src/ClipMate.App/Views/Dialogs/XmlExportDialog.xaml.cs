using ClipMate.App.ViewModels;
using DevExpress.Xpf.Dialogs;

namespace ClipMate.App.Views.Dialogs;

public partial class XmlExportDialog
{
    private readonly XmlExportViewModel _viewModel;

    public XmlExportDialog(XmlExportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void BrowseXmlExportFile_Click(object sender, RoutedEventArgs e)
    {
        DXSaveFileDialog? saveDialog = null;
        try
        {
            saveDialog = new DXSaveFileDialog();
            saveDialog.Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*";
            saveDialog.DefaultExt = ".xml";

            if (saveDialog.ShowDialog() == true)
                _viewModel.SetXmlExportFilePath(saveDialog.FileName);
        }
        finally
        {
            (saveDialog as IDisposable)?.Dispose();
        }
    }
}
