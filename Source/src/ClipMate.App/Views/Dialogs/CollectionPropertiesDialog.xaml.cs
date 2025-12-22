using ClipMate.App.ViewModels;
using ClipMate.Core.Services;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Collection Properties dialog window.
/// </summary>
public partial class CollectionPropertiesDialog
{
    public CollectionPropertiesDialog(CollectionPropertiesViewModel viewModel, IConfigurationService configurationService)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Set Monaco Editor options from configuration
        SqlEditor.EditorOptions = configurationService.Configuration.MonacoEditor;
    }

    /// <summary>
    /// Call this before closing to sync SQL editor text to ViewModel.
    /// </summary>
    public void SyncSqlEditorToViewModel()
    {
        // SQL editor text is bound directly via TwoWay binding in XAML
        // No manual synchronization needed
    }
}
