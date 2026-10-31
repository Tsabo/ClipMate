using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// QuickPaste pastes into the Paste Target as <see cref="IPasteTarget" /> holds it, without looking for a new one.
/// </summary>
public partial class QuickPasteServiceTests
{
    [Test]
    public async Task PasteClipAsync_FocusesThePasteTarget_WithoutLookingForANewOne()
    {
        // Arrange: Word is the Paste Target while ClipMate is in front
        var desktop = new FakeDesktop();
        var clipMate = desktop.OpenClipMate();
        var word = desktop.Open("winword", "OpusApp", "Document1 - Word");
        desktop.Activate(clipMate);
        var pasteTarget = CreateMockPasteTarget();
        A.CallTo(() => pasteTarget.Window).Returns(new PasteTargetWindow(word.Handle, "WINWORD", "OPUSAPP", word.Title));

        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(new ClipMateConfiguration());
        var service = new QuickPasteService(
            desktop,
            pasteTarget,
            CreateMockClipboardService(),
            configurationService,
            new StrongReferenceMessenger(),
            CreateMockMacroExecutionService(),
            CreateMockTemplateService(),
            CreateMockDialogService(),
            CreateMockLogger());

        // Act
        var pasted = await service.PasteClipAsync(CreateTestClip());

        // Assert
        await Assert.That(pasted).IsTrue();
        await Assert.That(desktop.ForegroundRequests).IsEquivalentTo(new[] { word.Handle });
    }

    [Test]
    public async Task PasteClipAsync_WithoutAPasteTarget_DoesNothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        var pasted = await service.PasteClipAsync(CreateTestClip());

        // Assert
        await Assert.That(pasted).IsFalse();
        A.CallTo(() => _mockClipboardService.SetClipboardContentAsync(A<Clip>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
