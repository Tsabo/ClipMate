using System.ComponentModel;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// The <b>Add Device manually</b> dialog: asks for another Device's address and Device ID.
/// </summary>
public partial class AddDeviceDialog
{
    private readonly AddDeviceDialogViewModel _viewModel;

    public AddDeviceDialog(AddDeviceDialogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += (_, _) => AddressTextEdit.Focus();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosing(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Dispatcher.InvokeAsync(Close);
}
