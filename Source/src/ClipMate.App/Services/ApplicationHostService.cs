using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Services;

/// <summary>
/// Managed host of the application.
/// </summary>
internal class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        // If you want, you can do something with these services at the beginning of loading the application.
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken) => HandleActivationAsync();

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Creates main window during activation.
    /// </summary>
    private async Task HandleActivationAsync()
    {
        if (Application.Current.Windows.OfType<ExplorerWindow>().Any())
            return;

        // Ensure we're on the UI thread when creating and showing the window
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var explorerWindow = _serviceProvider.GetRequiredService<ExplorerWindow>();
                explorerWindow?.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to show main window: {ex.Message}\n\n{ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                throw;
            }
        });
    }
}
