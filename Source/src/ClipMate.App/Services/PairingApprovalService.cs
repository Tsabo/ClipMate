using System.Windows;
using Application = System.Windows.Application;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Approving side of Pairing: a pairing request shows a tray notification; clicking it opens the pairing dialog with the
/// Verification Code and the "Show as" field. Closing the notification declines. If the notification goes unanswered,
/// the dialog opens by itself so the request can still be answered before it expires.
/// </summary>
public sealed class PairingApprovalService : IPairingApprovalHandler
{
    private readonly ILogger<PairingApprovalService> _logger;
    private NotificationService? _notificationService;

    public PairingApprovalService(ILogger<PairingApprovalService> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<PairingDecision> RequestApprovalAsync(PairingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
            return PairingDecision.Declined;

        try
        {
            var decision = await dispatcher.InvokeAsync(() => ShowPrompt(request, cancellationToken)).Task.Unwrap();
            _logger.LogInformation("Pairing request from {DeviceName}: {Decision}", request.DeviceName, decision.Accepted ? "accepted" : "declined");
            return decision;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to ask about the pairing request from {DeviceName}", request.DeviceName);
            return PairingDecision.Declined;
        }
    }

    private async Task<PairingDecision> ShowPrompt(PairingRequest request, CancellationToken cancellationToken)
    {
        var viewModel = PairingDialogViewModel.ForApproval(request);
        var notification = GetNotificationService().CreatePredefinedNotification(
            "Pairing request",
            $"{request.DeviceName} wants to pair with this Device.",
            $"Verification Code: {request.VerificationCode}",
            null,
            null);

        // The requesting Device withdrew the request, or it expired: close whatever is showing.
        await using var registration = cancellationToken.Register(() => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            notification.Hide();
            viewModel.Abandon();
        }));

        var result = await notification.ShowAsync();
        if (result == NotificationResult.UserCanceled)
            viewModel.DeclineCommand.Execute(null);
        else if (!viewModel.IsFinished)
            ShowDialog(viewModel);

        return await viewModel.Decision;
    }

    private static void ShowDialog(PairingDialogViewModel viewModel)
    {
        var dialog = new PairingDialog(viewModel) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        dialog.Show();
        dialog.Activate();
    }

    private NotificationService GetNotificationService() => _notificationService ??= new NotificationService
    {
        UseWin8NotificationsIfAvailable = false,
        PredefinedNotificationTemplate = NotificationTemplate.ShortHeaderAndTwoTextFields,
        PredefinedNotificationDuration = PredefinedNotificationDuration.Long,
    };
}
