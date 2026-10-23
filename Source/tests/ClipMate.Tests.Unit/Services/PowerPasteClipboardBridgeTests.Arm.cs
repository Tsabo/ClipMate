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
    public async Task Arm_WithActiveTemplate_OffersEveryFormatWithTheTemplatedText()
    {
        // Arrange - an HTML clip keeps its HTML; only the plain text is merged with the template
        var bridge = CreateBridge();
        var clip = CreateTextClip("Hello");
        clip.Type = ClipType.Html;
        clip.HtmlContent = "<b>Hello</b>";
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(clip)).Returns("Dear Hello, #1");
        Clip? offered = null;
        A.CallTo(() => _mockClipboardService.GetClipboardFormats(A<Clip>.Ignored))
            .Invokes((Clip p) => offered = p)
            .Returns([new ClipboardFormatData((uint)Formats.UnicodeText.Code, [1])]);

        // Act
        bridge.Arm(clip);

        // Assert
        await Assert.That(offered?.TextContent).IsEqualTo("Dear Hello, #1");
        await Assert.That(offered?.HtmlContent).IsEqualTo("<b>Hello</b>");
        await Assert.That(clip.TextContent).IsEqualTo("Hello"); // the clip in the list is unchanged
    }

    [Test]
    [Category("Arm")]
    public Task Arm_WithoutAPaste_DoesNotUseUpASequenceNumber()
    {
        // Arrange - starting, moving inside the sequence and restarting arm clips without pasting them
        var bridge = CreateBridge();
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(A<Clip>.Ignored)).Returns("templated");

        // Act
        bridge.Arm(CreateTextClip("First"));
        bridge.Arm(CreateTextClip("Second"));
        bridge.Arm(null);

        // Assert
        A.CallTo(() => _mockTemplateService.AdvanceSequence()).MustNotHaveHappened();

        return Task.CompletedTask;
    }

    [Test]
    [Category("Arm")]
    public Task AcceptPaste_OfATemplatedClip_UsesUpOneSequenceNumber()
    {
        // Arrange
        var bridge = CreateBridge();
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(A<Clip>.Ignored)).Returns("templated");
        bridge.Arm(CreateTextClip("First"));

        // Act
        bridge.AcceptPaste();

        // Assert
        A.CallTo(() => _mockTemplateService.AdvanceSequence()).MustHaveHappenedOnceExactly();

        return Task.CompletedTask;
    }

    [Test]
    [Category("Arm")]
    public Task AcceptPaste_WithoutActiveTemplate_LeavesTheSequenceAlone()
    {
        // Arrange
        var bridge = CreateBridge();
        bridge.Arm(CreateTextClip("First"));

        // Act
        bridge.AcceptPaste();

        // Assert
        A.CallTo(() => _mockTemplateService.AdvanceSequence()).MustNotHaveHappened();

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
        A.CallTo(() => _mockTemplateService.PreviewTemplateText(piece)).Returns("<piece>");

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
