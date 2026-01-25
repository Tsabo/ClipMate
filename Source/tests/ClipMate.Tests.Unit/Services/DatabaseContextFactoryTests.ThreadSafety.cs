using ClipMate.Data;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Thread safety tests for DatabaseContextFactory.
/// </summary>
public partial class DatabaseContextFactoryTests
{
    [Test]
    [Category("ThreadSafety")]
    public async Task RegisterDatabase_FromMultipleThreads_HandlesCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var tasks = new List<Task>();
        const int threadCount = 10;

        // Act - Register same database from multiple threads
        for (var i = 0; i < threadCount; i++)
            tasks.Add(Task.Run(() => factory.RegisterDatabase(_testDbPath)));

        await Task.WhenAll(tasks);

        // Assert - Should only be registered once despite multiple threads
        var paths = factory.GetLoadedDatabasePaths();
        await Assert.That(paths.Count).IsEqualTo(1);
    }

    [Test]
    [Category("ThreadSafety")]
    public async Task CreateContext_FromMultipleThreads_CreatesMultipleInstances()
    {
        // Arrange
        var factory = CreateFactory();
        var contexts = new List<ClipMateDbContext>();
        var tasks = new List<Task>();
        var lockObj = new object();

        // Act - Create contexts from multiple threads
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var context = factory.CreateContext(_testDbPath);
                lock (lockObj)
                {
                    contexts.Add(context);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Each thread should get its own context instance
        await Assert.That(contexts.Count).IsEqualTo(5);

        // Cleanup
        foreach (var context in contexts)
            await context.DisposeAsync();
    }
}
