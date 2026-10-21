using System.ComponentModel;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// The pairing dialog, shown on both Devices during Pairing.
/// </summary>
public partial class PairingDialog
{
    private readonly PairingDialogViewModel _viewModel;

    public PairingDialog(PairingDialogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += (_, _) => ShowAsTextEdit.Focus();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.OnClosing();
        base.OnClosing(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Dispatcher.InvokeAsync(Close);
}
