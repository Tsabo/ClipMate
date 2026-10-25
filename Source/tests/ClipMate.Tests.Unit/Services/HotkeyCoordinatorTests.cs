using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.Tests.Unit.Services;

public class HotkeyCoordinatorTests
{
    private readonly StrongReferenceMessenger _messenger = new();

    [Test]
    public async Task ScrollNextHotkey_Pressed_AsksForTheNextClipFromAHotkey()
    {
        // Arrange
        SelectNextClipEvent? received = null;
        _messenger.Register<SelectNextClipEvent>(this, (_, message) => received = message);

        // Act
        Press('N');

        // Assert
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.FromHotkey).IsTrue();
    }

    [Test]
    public async Task ScrollPreviousHotkey_Pressed_AsksForThePreviousClipFromAHotkey()
    {
        // Arrange
        SelectPreviousClipEvent? received = null;
        _messenger.Register<SelectPreviousClipEvent>(this, (_, message) => received = message);

        // Act
        Press('P');

        // Assert
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.FromHotkey).IsTrue();
    }

    /// <summary>
    /// Registers Ctrl+Alt+N as Scroll Next and Ctrl+Alt+P as Scroll Previous, then presses the one with <paramref name="key" />.
    /// </summary>
    private void Press(char key)
    {
        var configuration = new ClipMateConfiguration();
        configuration.Hotkeys.SelectNext = "Ctrl+Alt+N";
        configuration.Hotkeys.SelectPrevious = "Ctrl+Alt+P";
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration);

        var actions = new Dictionary<int, Action>();
        var hotkeyService = A.Fake<IHotkeyService>();
        A.CallTo(() => hotkeyService.RegisterHotkey(A<int>._, A<ModifierKeys>._, A<int>._, A<Action>._))
            .ReturnsLazily((int _, ModifierKeys _, int virtualKey, Action action) =>
            {
                actions.TryAdd(virtualKey, action);
                return true;
            });

        new HotkeyCoordinator(hotkeyService, configurationService, _messenger, NullLogger<HotkeyCoordinator>.Instance).RegisterAllHotkeys();

        actions[key]();
    }
}
