using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class CollectionServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private readonly ICollectionRepository _mockRepository;
    private SqliteConnection _connection = null!;
    private DbContextOptions<ClipMateDbContext> _contextOptions = null!;
    private IDatabaseManager _mockDatabaseManager = null!;

    public CollectionServiceTests()
    {
        _mockRepository = A.Fake<ICollectionRepository>();
    }

    /// <summary>
    /// Creates a new DbContext using the shared connection.
    /// Each context can be disposed independently while sharing the same database.
    /// </summary>
    private ClipMateDbContext CreateContext() => new(_contextOptions);

    [Before(Test)]
    public void Setup()
    {
        // Create in-memory database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Initialize database schema
        using (var context = CreateContext())
        {
            context.Database.EnsureCreated();
        }

        // Mock IDatabaseManager - interfaces don't need constructor arguments
        _mockDatabaseManager = A.Fake<IDatabaseManager>();
        A.CallTo(() => _mockDatabaseManager.CreateDatabaseContext(_testDatabaseKey))
            .ReturnsLazily(() => CreateContext());
    }

    [After(Test)]
    public void Cleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    private ICollectionService CreateCollectionService()
    {
        // Set up mock repository to query from the actual DbContext for GetByIdAsync
        A.CallTo(() => _mockRepository.GetByIdAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((Guid id, CancellationToken _) =>
            {
                using var ctx = CreateContext();
                return ctx.Collections.FirstOrDefault(c => c.Id == id);
            });

        // Set up GetAllAsync to return collections from DbContext
        A.CallTo(() => _mockRepository.GetAllAsync(A<CancellationToken>.Ignored))
            .ReturnsLazily(() =>
            {
                using var ctx = CreateContext();
                return ctx.Collections.ToList();
            });

        var mockContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => mockContextFactory.GetCollectionRepository(A<string>.Ignored))
            .Returns(_mockRepository);

        // Set up CreateAllDatabaseContexts to return a new context each time
        A.CallTo(() => _mockDatabaseManager.CreateAllDatabaseContexts())
            .ReturnsLazily(() => [(_testDatabaseKey, CreateContext())]);

        return new CollectionService(_mockDatabaseManager, mockContextFactory);
    }

    private Collection CreateTestCollection(Guid? id = null, string name = "Test Collection") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

    [Test]
    public async Task CreateAsync_WithValidName_ShouldCreateAndReturnCollection()
    {
        // Arrange
        const string name = "Test Collection";
        const string description = "Test Description";

        A.CallTo(() => _mockRepository.CreateAsync(A<Collection>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((Collection c, CancellationToken _) => c);

        // Add a dummy collection to the database so SetActiveAsync can find it
        var dummyCollection = CreateTestCollection();
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(dummyCollection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();
        await service.SetActiveAsync(dummyCollection.Id, _testDatabaseKey);

        // Act
        var result = await service.CreateAsync(name, description);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo(name);
        await Assert.That(result.Description).IsEqualTo(description);
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.CreatedAt).IsGreaterThan(DateTime.UtcNow.AddSeconds(-5));
        A.CallTo(() => _mockRepository.CreateAsync(A<Collection>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync(null!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CreateAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.CreateAsync("")).Throws<ArgumentException>();
    }

    [Test]
    public async Task GetByIdAsync_WithValidId_ShouldReturnCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var expectedCollection = CreateTestCollection(collectionId);

        // Add a dummy collection to the database so SetActiveAsync can find it
        var dummyCollection = CreateTestCollection();
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(dummyCollection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();

        // Setup mock AFTER CreateCollectionService to override the general setup
        A.CallTo(() => _mockRepository.GetByIdAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns(expectedCollection);

        await service.SetActiveAsync(dummyCollection.Id, _testDatabaseKey);

        // Act
        var result = await service.GetByIdAsync(collectionId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        A.CallTo(() => _mockRepository.GetByIdAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns((Collection?)null);

        // Add a dummy collection to the database so SetActiveAsync can find it
        var dummyCollection = CreateTestCollection();
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(dummyCollection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();
        await service.SetActiveAsync(dummyCollection.Id, _testDatabaseKey);

        // Act
        var result = await service.GetByIdAsync(collectionId);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllCollections()
    {
        // Arrange
        var collections = new List<Collection>
        {
            CreateTestCollection(name: "Collection 1"),
            CreateTestCollection(name: "Collection 2"),
            CreateTestCollection(name: "Collection 3"),
        };

        // Add collections to the DbContext since GetAllAsync queries the context directly
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.AddRange(collections);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();

        // Act
        var result = await service.GetAllAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        // Sort by name since database order might not be deterministic
        var sortedResult = result.OrderBy(c => c.Name).ToList();
        await Assert.That(sortedResult[0].Name).IsEqualTo("Collection 1");
        await Assert.That(sortedResult[1].Name).IsEqualTo("Collection 2");
        await Assert.That(sortedResult[2].Name).IsEqualTo("Collection 3");
    }

    [Test]
    public async Task UpdateAsync_WithValidCollection_ShouldUpdate()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.Name = "Updated Name";

        A.CallTo(() => _mockRepository.UpdateAsync(A<Collection>.Ignored, A<CancellationToken>.Ignored))
            .Returns(true);

        // Add a dummy collection to the database so SetActiveAsync can find it
        var dummyCollection = CreateTestCollection();
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(dummyCollection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();
        await service.SetActiveAsync(dummyCollection.Id, _testDatabaseKey);

        // Act
        await service.UpdateAsync(collection);

        // Assert
        await Assert.That(collection.Name).IsEqualTo("Updated Name");
        await Assert.That(collection.ModifiedAt).IsNotNull();
        A.CallTo(() => _mockRepository.UpdateAsync(collection, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task UpdateAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.UpdateAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collection = CreateTestCollection();
        A.CallTo(() => _mockRepository.UpdateAsync(A<Collection>.Ignored, A<CancellationToken>.Ignored))
            .Returns(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.UpdateAsync(collection)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteCollection()
    {
        // Arrange
        var dummyCollection = CreateTestCollection();
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(dummyCollection);
            await setupContext.SaveChangesAsync();
        }

        A.CallTo(() => _mockRepository.DeleteAsync(dummyCollection.Id, A<CancellationToken>.Ignored))
            .Returns(true);

        var service = CreateCollectionService();
        await service.SetActiveAsync(dummyCollection.Id, _testDatabaseKey);

        // Act
        await service.DeleteAsync(dummyCollection.Id);

        // Assert
        A.CallTo(() => _mockRepository.DeleteAsync(dummyCollection.Id, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteAsync_WhenRepositoryFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        A.CallTo(() => _mockRepository.DeleteAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns(false);

        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.DeleteAsync(collectionId)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetActiveAsync_WhenNoActiveSet_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = CreateCollectionService();

        // Act & Assert
        await Assert.That(async () => await service.GetActiveAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetActiveAsync_WithActiveSet_ShouldReturnActiveCollection()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);

        // Add collection to in-memory database
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(collection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();
        await service.SetActiveAsync(collectionId, _testDatabaseKey);

        // Act
        var result = await service.GetActiveAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task SetActiveAsync_WithValidId_ShouldSetActive()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);

        // Add collection to in-memory database
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(collection);
            await setupContext.SaveChangesAsync();
        }

        var service = CreateCollectionService();

        // Act
        await service.SetActiveAsync(collectionId, _testDatabaseKey);

        // Assert - GetActiveAsync should return the collection
        var active = await service.GetActiveAsync();
        await Assert.That(active.Id).IsEqualTo(collectionId);
    }

    [Test]
    public async Task SetActiveAsync_WithInvalidId_ShouldThrowArgumentException()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var service = CreateCollectionService();

        // Act & Assert - collection doesn't exist in database, should throw
        await Assert.That(async () => await service.SetActiveAsync(collectionId, _testDatabaseKey)).Throws<ArgumentException>();
    }

    [Test]
    public async Task DeleteAsync_WithActiveCollection_ShouldClearActive()
    {
        // Arrange
        var collectionId = Guid.NewGuid();
        var collection = CreateTestCollection(collectionId);

        // Add collection to in-memory database
        await using (var setupContext = CreateContext())
        {
            setupContext.Collections.Add(collection);
            await setupContext.SaveChangesAsync();
        }

        A.CallTo(() => _mockRepository.DeleteAsync(collectionId, A<CancellationToken>.Ignored))
            .Returns(true);

        var service = CreateCollectionService();
        await service.SetActiveAsync(collectionId, _testDatabaseKey);

        // Act
        await service.DeleteAsync(collectionId);

        // Assert - GetActiveAsync should throw since active was cleared
        await Assert.That(async () => await service.GetActiveAsync()).Throws<InvalidOperationException>();
    }
}
