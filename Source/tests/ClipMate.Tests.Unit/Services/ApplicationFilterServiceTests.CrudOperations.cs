using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class ApplicationFilterServiceTests
{
    [Test]
    [Category("CrudOperations")]
    public async Task GetAllFiltersAsync_WithNoActiveDatabase_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act
        var result = await service.GetAllFiltersAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task GetAllFiltersAsync_ReturnsAllFilters()
    {
        // Arrange
        var service = CreateService();
        var filters = new[]
        {
            CreateTestFilter("Filter1"),
            CreateTestFilter("Filter2"),
            CreateTestFilter("Filter3", isEnabled: false),
        };

        A.CallTo(() => _mockFilterRepository.GetAllAsync(A<CancellationToken>.Ignored))
            .Returns(filters);

        // Act
        var result = await service.GetAllFiltersAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task GetEnabledFiltersAsync_WithNoActiveDatabase_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act
        var result = await service.GetEnabledFiltersAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task GetEnabledFiltersAsync_ReturnsOnlyEnabledFilters()
    {
        // Arrange
        var service = CreateService();
        var filters = new[]
        {
            CreateTestFilter("Filter1", isEnabled: true),
            CreateTestFilter("Filter2", isEnabled: true),
        };

        A.CallTo(() => _mockFilterRepository.GetEnabledAsync(A<CancellationToken>.Ignored))
            .Returns(filters);

        // Act
        var result = await service.GetEnabledFiltersAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithValidData_CreatesFilter()
    {
        // Arrange
        var service = CreateService();
        ApplicationFilter? capturedFilter = null;
        A.CallTo(() => _mockFilterRepository.CreateAsync(A<ApplicationFilter>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ApplicationFilter f, CancellationToken _) => capturedFilter = f)
            .ReturnsLazily((ApplicationFilter f, CancellationToken _) => f);

        // Act
        var result = await service.CreateFilterAsync("Test Filter", "notepad.exe", "*password*");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(capturedFilter).IsNotNull();
        await Assert.That(capturedFilter!.Name).IsEqualTo("Test Filter");
        await Assert.That(capturedFilter.ProcessName).IsEqualTo("notepad.exe");
        await Assert.That(capturedFilter.WindowTitlePattern).IsEqualTo("*password*");
        await Assert.That(capturedFilter.IsEnabled).IsTrue();
        await Assert.That(capturedFilter.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithOnlyProcessName_CreatesFilter()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockFilterRepository.CreateAsync(A<ApplicationFilter>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((ApplicationFilter f, CancellationToken _) => f);

        // Act
        var result = await service.CreateFilterAsync("Test", "notepad.exe", null);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ProcessName).IsEqualTo("notepad.exe");
        await Assert.That(result.WindowTitlePattern).IsNull();
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithOnlyWindowTitlePattern_CreatesFilter()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockFilterRepository.CreateAsync(A<ApplicationFilter>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((ApplicationFilter f, CancellationToken _) => f);

        // Act
        var result = await service.CreateFilterAsync("Test", null, "*password*");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ProcessName).IsNull();
        await Assert.That(result.WindowTitlePattern).IsEqualTo("*password*");
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.CreateFilterAsync(null!, "notepad.exe", null));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.CreateFilterAsync("", "notepad.exe", null));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithNoBothProcessAndTitle_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.CreateFilterAsync("Test", null, null));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task CreateFilterAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateFilterAsync("Test", "notepad.exe", null));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task UpdateFilterAsync_WithValidFilter_UpdatesFilter()
    {
        // Arrange
        var service = CreateService();
        var filter = CreateTestFilter("Test", "notepad.exe");
        ApplicationFilter? capturedFilter = null;
        A.CallTo(() => _mockFilterRepository.UpdateAsync(A<ApplicationFilter>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ApplicationFilter f, CancellationToken _) => capturedFilter = f)
            .Returns(true);

        // Act
        await service.UpdateFilterAsync(filter);

        // Assert
        await Assert.That(capturedFilter).IsNotNull();
        await Assert.That(capturedFilter!.ModifiedAt).IsNotNull();
        A.CallTo(() => _mockFilterRepository.UpdateAsync(filter, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("CrudOperations")]
    public async Task UpdateFilterAsync_WithNullFilter_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.UpdateFilterAsync(null!));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task UpdateFilterAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);
        var filter = CreateTestFilter("Test", "notepad.exe");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateFilterAsync(filter));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task DeleteFilterAsync_WithValidId_DeletesFilter()
    {
        // Arrange
        var service = CreateService();
        var filterId = Guid.NewGuid();
        A.CallTo(() => _mockFilterRepository.DeleteAsync(filterId, A<CancellationToken>.Ignored))
            .Returns(true);

        // Act
        await service.DeleteFilterAsync(filterId);

        // Assert
        A.CallTo(() => _mockFilterRepository.DeleteAsync(filterId, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("CrudOperations")]
    public async Task DeleteFilterAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.DeleteFilterAsync(Guid.NewGuid()));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task SetFilterEnabledAsync_WithValidId_UpdatesEnabledState()
    {
        // Arrange
        var service = CreateService();
        var filterId = Guid.NewGuid();
        var filter = CreateTestFilter("Test", "notepad.exe", id: filterId);
        filter.IsEnabled = false;

        A.CallTo(() => _mockFilterRepository.GetByIdAsync(filterId, A<CancellationToken>.Ignored))
            .Returns(filter);

        ApplicationFilter? capturedFilter = null;
        A.CallTo(() => _mockFilterRepository.UpdateAsync(A<ApplicationFilter>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ApplicationFilter f, CancellationToken _) => capturedFilter = f)
            .Returns(true);

        // Act
        await service.SetFilterEnabledAsync(filterId, true);

        // Assert
        await Assert.That(capturedFilter).IsNotNull();
        await Assert.That(capturedFilter!.IsEnabled).IsTrue();
        await Assert.That(capturedFilter.ModifiedAt).IsNotNull();
    }

    [Test]
    [Category("CrudOperations")]
    public async Task SetFilterEnabledAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var filterId = Guid.NewGuid();
        A.CallTo(() => _mockFilterRepository.GetByIdAsync(filterId, A<CancellationToken>.Ignored))
            .Returns((ApplicationFilter?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SetFilterEnabledAsync(filterId, true));
    }

    [Test]
    [Category("CrudOperations")]
    public async Task SetFilterEnabledAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetFilterEnabledAsync(Guid.NewGuid(), true));
    }
}
