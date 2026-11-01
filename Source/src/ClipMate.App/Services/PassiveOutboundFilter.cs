using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Passive Outbound Filtering: when ClipMate loses focus, a copy captured from another program is put back on the
/// clipboard filtered, as ClipMate 7 does from <c>Application.OnDeactivate</c> (manual: Pasting Options, "Filter Outbound
/// Clips").
/// </summary>
public sealed class PassiveOutboundFilter : IHostedService
{
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly ILogger<PassiveOutboundFilter> _logger;

    public PassiveOutboundFilter(ICurrentClipWriter currentClipWriter, ILogger<PassiveOutboundFilter> logger)
    {
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        UiThread.Run(() =>
        {
            if (Application.Current != null)
                Application.Current.Deactivated += OnDeactivated;
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        UiThread.Run(() =>
        {
            if (Application.Current != null)
                Application.Current.Deactivated -= OnDeactivated;
        });

        return Task.CompletedTask;
    }

    private async void OnDeactivated(object? sender, EventArgs e)
    {
        try
        {
            await _currentClipWriter.FilterCapturedClipAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Passive Outbound Filter could not put the Current Clip back on the clipboard");
        }
    }
}
