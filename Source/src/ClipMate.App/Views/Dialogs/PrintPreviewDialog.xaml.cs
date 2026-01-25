using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.App.Views.Dialogs;

public partial class PrintPreviewDialog
{
    private readonly PrintPreviewViewModel _printPreviewViewModel;

    public PrintPreviewDialog(PrintPreviewViewModel printPreviewViewModel)
    {
        _printPreviewViewModel = printPreviewViewModel ?? throw new ArgumentNullException(nameof(printPreviewViewModel));
        DataContext = _printPreviewViewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Sets the clips to print and configuration.
    /// </summary>
    public async Task SetClips(List<Guid> clipIds, string databaseKey, PrintConfiguration config)
        => await _printPreviewViewModel.SetClips(clipIds, databaseKey, config);
}
