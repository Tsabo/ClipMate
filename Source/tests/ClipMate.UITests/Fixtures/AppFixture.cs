using TUnit.Core;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Shared fixture for initializing and cleaning up the ClipMate application in UI tests.
/// TODO: Implement actual WPF Pilot integration once API is verified.
/// </summary>
public class AppFixture : IAsyncDisposable
{
    /// <summary>
    /// Starts the ClipMate application with test database.
    /// TODO: Implement with correct WPF Pilot API.
    /// </summary>
    public Task StartAppAsync()
    {
        // Stub implementation - to be implemented with WPF Pilot API research
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
