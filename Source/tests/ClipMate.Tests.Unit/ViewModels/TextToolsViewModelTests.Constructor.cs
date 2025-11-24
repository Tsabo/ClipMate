using ClipMate.App.ViewModels;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
    #region Constructor Tests

    [Test]
    public async Task Constructor_WithValidService_ShouldCreateInstance()
    {
        // Arrange & Act
        var vm = new TextToolsViewModel(_textTransformService);

        // Assert
        await Assert.That(vm).IsNotNull();
        await Assert.That(vm.InputText).IsEmpty();
        await Assert.That(vm.OutputText).IsEmpty();
    }

    [Test]
    public async Task Constructor_WithNullService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new TextToolsViewModel(null!))
            .Throws<ArgumentNullException>();
    }

    #endregion
}
