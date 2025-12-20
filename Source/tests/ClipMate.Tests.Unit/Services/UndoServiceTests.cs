using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for UndoService.
/// Tests single-level undo functionality for text editor operations.
/// Per user manual: undo applies only to text edits and toolbar transformations, not clip operations.
/// </summary>
public class UndoServiceTests : TestFixtureBase
{
    [Test]
    public async Task CanUndo_Initially_ReturnsFalse()
    {
        // Arrange
        var service = new UndoService();

        // Act & Assert
        await Assert.That(service.CanUndo).IsFalse();
    }

    [Test]
    public async Task PushState_WithTextContent_EnablesUndo()
    {
        // Arrange
        var service = new UndoService();
        const string initialContent = "Original text";

        // Act
        service.PushState(initialContent);

        // Assert
        await Assert.That(service.CanUndo).IsTrue();
    }

    [Test]
    public async Task Undo_AfterPushState_RestoresPreviousContent()
    {
        // Arrange
        var service = new UndoService();
        const string originalContent = "Original text";
        service.PushState(originalContent);

        // Act
        var result = service.Undo();

        // Assert
        await Assert.That(result).IsEqualTo(originalContent);
    }

    [Test]
    public async Task Undo_WithoutPriorState_ReturnsNull()
    {
        // Arrange
        var service = new UndoService();

        // Act
        var result = service.Undo();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task CanUndo_AfterUndo_ReturnsFalse()
    {
        // Arrange
        var service = new UndoService();
        service.PushState("Some text");

        // Act
        service.Undo();

        // Assert - single-level undo, so after one undo, no more available
        await Assert.That(service.CanUndo).IsFalse();
    }

    [Test]
    public async Task PushState_MultipleTimes_KeepsOnlyLastState()
    {
        // Arrange
        var service = new UndoService();
        service.PushState("First state");
        service.PushState("Second state");
        service.PushState("Third state");

        // Act - single-level undo should only restore third state
        var result = service.Undo();

        // Assert
        await Assert.That(result).IsEqualTo("Third state");
        await Assert.That(service.CanUndo).IsFalse(); // No more undo available
    }

    [Test]
    public async Task Clear_RemovesUndoState()
    {
        // Arrange
        var service = new UndoService();
        service.PushState("Some text");

        // Act
        service.Clear();

        // Assert
        await Assert.That(service.CanUndo).IsFalse();
        await Assert.That(service.Undo()).IsNull();
    }

    [Test]
    public async Task PushState_WithNullContent_DoesNotEnableUndo()
    {
        // Arrange
        var service = new UndoService();

        // Act
        service.PushState(null);

        // Assert
        await Assert.That(service.CanUndo).IsFalse();
    }

    [Test]
    public async Task PushState_WithEmptyString_EnablesUndo()
    {
        // Arrange
        var service = new UndoService();

        // Act
        service.PushState(string.Empty);

        // Assert
        await Assert.That(service.CanUndo).IsTrue();
        await Assert.That(service.Undo()).IsEqualTo(string.Empty);
    }
}
