using System.Windows;
using Application = System.Windows.Application;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Requesting side of Pairing: sends the request and shows the pairing dialog.
/// </summary>
public sealed class PairingDialogService : IPairingDialogService
{
    private readonly IPairingClient _client;
    private readonly ILogger<PairingDialogService> _logger;
    private readonly IMessenger _messenger;

    public PairingDialogService(IPairingClient client, IMessenger messenger, ILogger<PairingDialogService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PairAsync(PairingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        IPairingSession session;
        try
        {
            session = _client.Start(target);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot pair with {DeviceName}", target.DeviceName);
            _messenger.Send(new StatusUpdateEvent(ex.Message, true));
            return;
        }

        await using (session)
        {
            var viewModel = PairingDialogViewModel.ForRequest(session);
            var dialog = new PairingDialog(viewModel)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(p => p.IsActive) ?? Application.Current.GetDialogOwner(),
            };
            dialog.ShowDialog();

            if (viewModel.Status == PairingStatus.Paired)
                _messenger.Send(new StatusUpdateEvent($"Paired with {viewModel.OtherDeviceName}", false));
        }
    }
}
