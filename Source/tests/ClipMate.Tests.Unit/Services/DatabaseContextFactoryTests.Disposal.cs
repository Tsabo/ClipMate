namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Disposal behavior tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("Disposal")]
    public async Task Dispose_ClearsDatabaseRegistry()
    {
        // Arrange
        var factory = CreateFactory();
        factory.RegisterDatabase(_testDbPath);
        factory.RegisterDatabase(Path.Combine(Path.GetTempPath(), "db2.db"));

        // Act
        factory.Dispose();

        // Assert - Can't verify cleared directly, but subsequent calls should throw
        await Assert.That(() => factory.GetLoadedDatabasePaths())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Category("Disposal")]
    public Task Dispose_CalledMultipleTimes_IsIdempotent()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert - Should not throw
        factory.Dispose();
        factory.Dispose();
        factory.Dispose();

        return Task.CompletedTask;
    }
}
