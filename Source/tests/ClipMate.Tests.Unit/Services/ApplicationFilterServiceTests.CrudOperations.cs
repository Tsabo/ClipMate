using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class ApplicationFilterServiceTests
{
    [Test]
    [Category("CrudOperations")]
    public async Task GetAllFiltersAsync_WithNoActiveDatabase_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

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

        _mockFilterRepository.Setup(applicationFilterRepository => applicationFilterRepository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(filters);

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
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

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

        _mockFilterRepository.Setup(p => p.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(filters);

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
        _mockFilterRepository.Setup(p => p.CreateAsync(It.IsAny<ApplicationFilter>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync((ApplicationFilter f, CancellationToken _) => f);

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
        _mockFilterRepository.Setup(p => p.CreateAsync(It.IsAny<ApplicationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationFilter f, CancellationToken _) => f);

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
        _mockFilterRepository.Setup(p => p.CreateAsync(It.IsAny<ApplicationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationFilter f, CancellationToken _) => f);

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
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

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
        _mockFilterRepository.Setup(p => p.UpdateAsync(It.IsAny<ApplicationFilter>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync(true);

        // Act
        await service.UpdateFilterAsync(filter);

        // Assert
        await Assert.That(capturedFilter).IsNotNull();
        await Assert.That(capturedFilter!.ModifiedAt).IsNotNull();
        _mockFilterRepository.Verify(p => p.UpdateAsync(filter, It.IsAny<CancellationToken>()), Times.Once);
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
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);
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
        _mockFilterRepository.Setup(p => p.DeleteAsync(filterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await service.DeleteFilterAsync(filterId);

        // Assert
        _mockFilterRepository.Verify(p => p.DeleteAsync(filterId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("CrudOperations")]
    public async Task DeleteFilterAsync_WithNoActiveDatabase_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

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

        _mockFilterRepository.Setup(p => p.GetByIdAsync(filterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filter);

        ApplicationFilter? capturedFilter = null;
        _mockFilterRepository.Setup(p => p.UpdateAsync(It.IsAny<ApplicationFilter>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationFilter, CancellationToken>((f, _) => capturedFilter = f)
            .ReturnsAsync(true);

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
        _mockFilterRepository.Setup(p => p.GetByIdAsync(filterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationFilter?)null);

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
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetFilterEnabledAsync(Guid.NewGuid(), true));
    }
}
