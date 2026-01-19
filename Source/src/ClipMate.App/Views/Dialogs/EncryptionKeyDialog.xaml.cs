using System.Security;
using ClipMate.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for entering encryption keys with simple (encrypt) and extended (decrypt) modes.
/// </summary>
public partial class EncryptionKeyDialog
{
    public EncryptionKeyDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        ViewModel = serviceProvider.GetRequiredService<EncryptionKeyDialogViewModel>();
        DataContext = ViewModel;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Gets the ViewModel for external access.
    /// </summary>
    public EncryptionKeyDialogViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-focus the password box
        KeyPasswordBox.Focus();
    }

    private void KeyPasswordBox_OnEditValueChanged(object sender, RoutedEventArgs e)
    {
        // DevExpress PasswordBoxEdit uses EditValue (string) instead of SecurePassword
        // Get password as string and convert to SecureString
        var password = KeyPasswordBox.EditValue as string ?? string.Empty;
        using var securePassword = new SecureString();
        foreach (var item in password)
            securePassword.AppendChar(item);

        securePassword.MakeReadOnly();

        ViewModel.SetPassphrase(securePassword);

        // Sync to plain text when not hiding (for when user unchecks HideKey)
        if (!ViewModel.HideKey)
            ViewModel.PlainTextPassword = password;
    }

    private void HideKeyCheckEdit_OnChecked(object sender, RoutedEventArgs e)
    {
        // When hiding key, copy text from TextBox to PasswordBox
        if (!string.IsNullOrEmpty(ViewModel.PlainTextPassword))
            KeyPasswordBox.EditValue = ViewModel.PlainTextPassword;
    }

    private void HideKeyCheckEdit_OnUnchecked(object sender, RoutedEventArgs e)
    {
        // When showing key, copy password from PasswordBox to TextBox
        ViewModel.PlainTextPassword = KeyPasswordBox.EditValue as string ?? string.Empty;
    }
}
