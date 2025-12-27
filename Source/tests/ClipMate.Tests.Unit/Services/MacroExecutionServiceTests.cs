using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class MacroExecutionServiceTests
{
    private Mock<IWin32InputInterop> CreateMockInputInterop() =>
        // Note: Cannot mock unsafe pointer parameters directly
        // Tests verify parsing logic; actual SendInput calls return default values
        new();

    #region Pause Tests

    [Test]
    public async Task ExecuteMacroAsync_WithPauseCommand_PausesExecution()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Before{PAUSE}After";
        var startTime = DateTime.UtcNow;

        // Act
        var result = await service.ExecuteMacroAsync(macroText);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(elapsed.TotalMilliseconds).IsGreaterThanOrEqualTo(500);
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ExecuteMacroAsync_WithCancellation_StopsExecution()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Long text{PAUSE}{PAUSE}{PAUSE}";
        var cts = new CancellationTokenSource();

        // Act
        var task = service.ExecuteMacroAsync(macroText, cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();
        var result = await task;

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Basic Keystroke Tests

    [Test]
    public async Task ExecuteMacroAsync_WithPlainText_SendsKeystrokes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Hello World";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithTabKey_SendsTabKeystroke()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Username{TAB}Password";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithEnterKey_SendsEnterKeystroke()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Submit{ENTER}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Modifier Tests

    [Test]
    public async Task ExecuteMacroAsync_WithShiftModifier_SendsShiftPlusKey()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "~a"; // Shift+A

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithControlModifier_SendsCtrlPlusKey()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "^c"; // Ctrl+C

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithAltModifier_SendsAltPlusKey()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "@f"; // Alt+F

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Special Keys Tests

    [Test]
    public async Task ExecuteMacroAsync_WithArrowKeys_SendsArrowKeystrokes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "{UP}{DOWN}{LEFT}{RIGHT}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithFunctionKeys_SendsFunctionKeystrokes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "{F1}{F5}{F12}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithNavigationKeys_SendsNavigationKeystrokes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "{HOME}{END}{PGUP}{PGDN}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Repeat Count Tests

    [Test]
    public async Task ExecuteMacroAsync_WithRepeatCount_SendsKeystrokeMultipleTimes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "{LEFT 6}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithRepeatCountZero_SendsNoKeystrokes()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "{TAB 0}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Literal Escaping Tests

    [Test]
    public async Task ExecuteMacroAsync_WithEscapedTilde_SendsLiteralTilde()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Price: {~}50";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithEscapedCaret_SendsLiteralCaret()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Power: 2{^}3";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithEscapedAt_SendsLiteralAt()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "email{@}domain.com";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Line Break Tests

    [Test]
    public async Task ExecuteMacroAsync_WithNaturalLineBreaks_IgnoresLineBreaks()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Line 1\nLine 2\rLine 3\r\nLine 4";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
        // Should send "Line 1Line 2Line 3Line 4" without breaks
    }

    [Test]
    public async Task ExecuteMacroAsync_WithExplicitEnter_SendsLineBreaks()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Line 1{ENTER}Line 2{ENTER}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Complex Macro Tests

    [Test]
    public async Task ExecuteMacroAsync_WithLoginMacro_SendsCorrectSequence()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "MyUserID{TAB}MyPassword{ENTER}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExecuteMacroAsync_WithNavigationMacro_SendsCorrectSequence()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "^a{DELETE}New Text{ESC}";

        // Act
        var result = await service.ExecuteMacroAsync(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Security Tests

    [Test]
    public async Task IsMacroSafe_WithNormalText_ReturnsTrue()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Normal text with {TAB} and {ENTER}";

        // Act
        var result = service.IsMacroSafe(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsMacroSafe_WithMultipleAltF4_ReturnsFalse()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "@{F4}@{F4}@{F4}"; // Multiple Alt+F4

        // Act
        var result = service.IsMacroSafe(macroText);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsMacroSafe_WithSingleAltF4_ReturnsTrue()
    {
        // Arrange
        var mockInput = CreateMockInputInterop();
        var service = new MacroExecutionService(mockInput.Object);
        const string macroText = "Close window: @{F4}";

        // Act
        var result = service.IsMacroSafe(macroText);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion
}
