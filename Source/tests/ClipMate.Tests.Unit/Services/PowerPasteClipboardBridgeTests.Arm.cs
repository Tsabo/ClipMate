using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteClipboardBridgeTests
{
    [Test]
    [Category("Arm")]
    public async Task Arm_WithImageClip_OffersItsFormatsForDelayedRenderingInsteadOfSettingTheClipboard()
    {
        // Arrange - every clip type is delayed-rendered, so image and file clips advance like text clips
        var bridge = CreateBridge();
        var clip = CreateImageClip();
        var formats = new List<ClipboardFormatData> { new((uint)Formats.Dib.Code, [1, 2, 3]), new(49_000, [4]) };
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(clip)).Returns(formats);

        // Act
        bridge.Arm(clip);

        // Assert
        await Assert.That(bridge.ArmedFormats).IsEquivalentTo(formats);
        A.CallTo(() => _mockClipboardService.SetClipboardContentAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithTextClip_OffersEveryFormatOfTheClip()
    {
        // Arrange
        var bridge = CreateBridge();
        var clip = CreateTextClip();
        var formats = new List<ClipboardFormatData>
        {
            new((uint)Formats.UnicodeText.Code, [1]),
            new((uint)Formats.RichText.Code, [2]),
            new((uint)Formats.HtmlAlt.Code, [3]),
        };

        A.CallTo(() => _mockClipboardService.GetClipboardFormats(clip)).Returns(formats);

        // Act
        bridge.Arm(clip);

        // Assert
        await Assert.That(bridge.ArmedFormats).IsEquivalentTo(formats);
        A.CallTo(() => _mockClipboardService.SetClipboardContentAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithActiveTemplate_OffersTheTemplatedClip()
    {
        // Arrange
        var bridge = CreateBridge();
        var clip = CreateTextClip("Hello");
        var templated = CreateTextClip("Dear Hello, #1");
        A.CallTo(() => _mockTemplateService.TryApplyTemplate(clip)).Returns(templated);

        // Act
        bridge.Arm(clip);

        // Assert
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(templated)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(clip)).MustNotHaveHappened();
        await Assert.That(templated.TextContent).IsEqualTo("Dear Hello, #1");
    }

    [Test]
    [Category("Arm")]
    public Task Arm_EachWaitingClip_AppliesTheTemplateOnce()
    {
        // Arrange - #SEQUENCE# advances once per paste, and each paste arms the next clip once
        var bridge = CreateBridge();

        // Act
        bridge.Arm(CreateTextClip("First"));
        bridge.Arm(CreateTextClip("Second"));

        // Assert
        A.CallTo(() => _mockTemplateService.TryApplyTemplate(A<Clip>.Ignored)).MustHaveHappenedTwiceExactly();

        return Task.CompletedTask;
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithExplodePiece_OffersPlainTextOnly()
    {
        // Arrange
        var bridge = CreateBridge();
        var piece = CreateTextClip("piece");

        // Act
        bridge.Arm(piece, true);

        // Assert
        await Assert.That(bridge.ArmedFormats.Count).IsEqualTo(1);
        await Assert.That(bridge.ArmedFormats[0].FormatId).IsEqualTo((uint)Formats.UnicodeText.Code);
        await Assert.That(System.Text.Encoding.Unicode.GetString(bridge.ArmedFormats[0].Data)).IsEqualTo("piece\0");
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(A<Clip>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithExplodePieceAndActiveTemplate_OffersTheTemplatedText()
    {
        // Arrange
        var bridge = CreateBridge();
        var piece = CreateTextClip("piece");
        A.CallTo(() => _mockTemplateService.TryApplyTemplate(piece)).Returns(CreateTextClip("<piece>"));

        // Act
        bridge.Arm(piece, true);

        // Assert
        await Assert.That(System.Text.Encoding.Unicode.GetString(bridge.ArmedFormats[0].Data)).IsEqualTo("<piece>\0");
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithNullClip_DoesNotThrow()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act & Assert
        await Assert.That(() => bridge.Arm(null)).ThrowsNothing();
        await Assert.That(bridge.ArmedFormats).IsEmpty();
    }

    [Test]
    [Category("Arm")]
    public async Task Arm_WithoutActiveWindow_DoesNotThrow()
    {
        // Arrange - no message window exists yet (PowerPaste not Active), so the Win32
        // registration is skipped rather than thrown
        var bridge = CreateBridge();
        var clip = CreateTextClip();
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(clip)).Returns([new ClipboardFormatData((uint)Formats.UnicodeText.Code, [1])]);

        // Act & Assert
        await Assert.That(() => bridge.Arm(clip)).ThrowsNothing();
    }
}
