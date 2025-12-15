using ClipMate.UITests.Fixtures;
using TUnit.Core;

namespace ClipMate.UITests.Tests;

/// <summary>
/// Example UI tests demonstrating WPF Pilot usage and page object pattern.
/// These serve as templates for writing new UI tests.
/// TODO: Implement actual test logic once WPF Pilot API is researched and confirmed.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class ExampleTests(AppFixture appFixture)
{
    private readonly AppFixture _appFixture = appFixture;

    [Before(Test)]
    public async Task Setup()
    {
        await _appFixture.StartAppAsync();
    }

    /// <summary>
    /// Placeholder test - will be implemented with WPF Pilot API.
    /// </summary>
    [Test]
    [Skip("WPF Pilot API integration pending - stub implementation")]
    public async Task Example_Placeholder()
    {
        // Stub - to be implemented with correct WPF Pilot API
        await Task.CompletedTask;
    }
}
